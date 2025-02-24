using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;
using System.Linq;

namespace ECS.Systems
{
    public class TerrainSystem : SystemBase
    {
        [Header("Terrain Settings")]
        [SerializeField] private int viewDistance = 5;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private Material terrainMaterial;

        [Header("Generation Settings")]
        [SerializeField] private float baseNoiseScale = 100f; // Increased for smoother terrain
        [SerializeField] private int baseOctaves = 6; // Increased for more detail
        [SerializeField] private float basePersistence = 0.4f; // Reduced for smoother transitions
        [SerializeField] private float baseLacunarity = 2f;
        [SerializeField] private Vector2 baseOffset;
        [SerializeField] private float heightMultiplier = 40f; // Increased for more dramatic terrain
        [SerializeField] private float blendDistance = 1f;

        private Dictionary<Vector2Int, Entity> activeChunks = new Dictionary<Vector2Int, Entity>();
        private Queue<Entity> chunkPool = new Queue<Entity>();
        private Transform viewer;
        private Transform chunksParent;
        private Material customTerrainMaterial;

        // Biome transition thresholds
        private const float PLAINS_THRESHOLD = 0.3f;
        private const float FOREST_THRESHOLD = 0.6f;

        protected override void Initialize()
        {
            // Create chunks parent
            var parentObj = new GameObject("Terrain Chunks");
            chunksParent = parentObj.transform;
            Debug.Log("[TerrainSystem] Created chunks parent object");

            // Find the main camera as our viewer
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                viewer = mainCamera.transform;
                Debug.Log($"[TerrainSystem] Using main camera as viewer");
            }
            else
            {
                Debug.LogError("[TerrainSystem] No main camera found!");
                return;
            }

            // Create terrain material with custom shader
            if (customTerrainMaterial == null)
            {
                var shader = Shader.Find("Custom/TerrainShader");
                if (shader != null)
                {
                    customTerrainMaterial = new Material(shader)
                    {
                        enableInstancing = true
                    };

                    // Set biome colors
                    customTerrainMaterial.SetColor("_PlainsColor", new Color(0.4f, 0.8f, 0.4f));
                    customTerrainMaterial.SetColor("_ForestColor", new Color(0.2f, 0.6f, 0.2f));
                    customTerrainMaterial.SetColor("_MountainColor", new Color(0.6f, 0.6f, 0.6f));
                    customTerrainMaterial.SetColor("_DesertColor", new Color(0.9f, 0.8f, 0.5f));
                    customTerrainMaterial.SetFloat("_BlendDistance", blendDistance);
                    customTerrainMaterial.SetFloat("_HeightBlend", 0.3f);

                    Debug.Log("[TerrainSystem] Created custom terrain material with shader");
                }
                else
                {
                    Debug.LogError("[TerrainSystem] Failed to find Custom/TerrainShader!");
                    customTerrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
            }

            base.Initialize();

            // Create initial chunks around viewer
            Vector2Int viewerChunkCoord = WorldToChunkCoord(viewer.position);
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector2Int coord = new Vector2Int(viewerChunkCoord.x + x, viewerChunkCoord.y + z);
                    CreateChunk(coord);
                }
            }

            Debug.Log($"[TerrainSystem] Initialized with {activeChunks.Count} chunks");
        }

        public override void UpdateSystem()
        {
            if (!isInitialized || viewer == null) return;

            // Get viewer position in chunk coordinates
            Vector2Int viewerChunkCoord = WorldToChunkCoord(viewer.position);

            // Update existing chunks
            foreach (var chunk in registeredEntities.ToList())
            {
                var chunkState = chunk.GetComponent<ChunkStateComponent>();
                if (chunkState != null && chunkState.NeedsUpdate(updateInterval))
                {
                    chunkState.UpdateState(viewer.position);

                    // Handle chunk state changes
                    switch (chunkState.currentState)
                    {
                        case ChunkStateComponent.LoadState.Loading:
                            GenerateChunkTerrain(chunk);
                            chunkState.currentState = ChunkStateComponent.LoadState.Active;
                            break;

                        case ChunkStateComponent.LoadState.UnloadPending:
                            UnloadChunk(chunk);
                            break;
                    }
                }
            }

            // Create new chunks
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector2Int coord = new Vector2Int(viewerChunkCoord.x + x, viewerChunkCoord.y + z);
                    if (!activeChunks.ContainsKey(coord))
                    {
                        CreateChunk(coord);
                    }
                }
            }
        }

        private void CreateChunk(Vector2Int coord)
        {
            Entity chunk;
            TerrainDataComponent terrainData;
            ChunkStateComponent chunkState;

            if (chunkPool.Count > 0)
            {
                // Reuse chunk from pool
                chunk = chunkPool.Dequeue();
                chunk.gameObject.SetActive(true);
                terrainData = chunk.GetComponent<TerrainDataComponent>();
                chunkState = chunk.GetComponent<ChunkStateComponent>();
                Debug.Log($"[TerrainSystem] Reused chunk from pool for coord {coord}");
            }
            else
            {
                // Create new chunk
                var go = new GameObject($"Chunk_{coord.x}_{coord.y}");
                go.transform.parent = chunksParent;
                
                // Add components in the correct order
                chunk = go.AddComponent<Entity>();
                chunkState = go.AddComponent<ChunkStateComponent>();
                terrainData = go.AddComponent<TerrainDataComponent>();
                
                Debug.Log($"[TerrainSystem] Created new chunk for coord {coord}");
            }

            // Initialize components
            terrainData.Initialize();
            terrainData.SetMaterial(customTerrainMaterial);
            terrainData.InitializeArrays(chunkState.size);

            // Configure chunk state
            chunkState.chunkCoord = coord;
            chunkState.currentState = ChunkStateComponent.LoadState.Loading;

            // Position the chunk
            chunk.transform.position = new Vector3(
                coord.x * (chunkState.size - 1) * chunkState.scale,
                0,
                coord.y * (chunkState.size - 1) * chunkState.scale
            );

            // Add to active chunks
            activeChunks[coord] = chunk;

            // Set neighbors
            UpdateChunkNeighbors(coord);
        }

        private void UnloadChunk(Entity chunk)
        {
            var state = chunk.GetComponent<ChunkStateComponent>();
            if (state != null)
            {
                // Remove from active chunks
                activeChunks.Remove(state.chunkCoord);

                // Add to pool
                chunk.gameObject.SetActive(false);
                chunkPool.Enqueue(chunk);

                state.currentState = ChunkStateComponent.LoadState.Unloaded;
                Debug.Log($"[TerrainSystem] Unloaded chunk at {state.chunkCoord}");

                // Update neighbors of surrounding chunks
                UpdateChunkNeighbors(state.chunkCoord + Vector2Int.up);
                UpdateChunkNeighbors(state.chunkCoord + Vector2Int.down);
                UpdateChunkNeighbors(state.chunkCoord + Vector2Int.left);
                UpdateChunkNeighbors(state.chunkCoord + Vector2Int.right);
            }
        }

        private void GenerateChunkTerrain(Entity chunk)
        {
            var terrainData = chunk.GetComponent<TerrainDataComponent>();
            var state = chunk.GetComponent<ChunkStateComponent>();
            if (terrainData == null || state == null) return;

            // Set generation parameters
            terrainData.noiseScale = baseNoiseScale;
            terrainData.octaves = baseOctaves;
            terrainData.persistence = basePersistence;
            terrainData.lacunarity = baseLacunarity;
            terrainData.offset = baseOffset + new Vector2(state.chunkCoord.x * 1000f, state.chunkCoord.y * 1000f);

            // Generate height data
            for (int z = 0; z < state.size; z++)
            {
                for (int x = 0; x < state.size; x++)
                {
                    float xCoord = (float)x / state.size * terrainData.noiseScale + terrainData.offset.x;
                    float zCoord = (float)z / state.size * terrainData.noiseScale + terrainData.offset.y;

                    // Add a second layer of noise for more variation
                    float detailScale = 2.5f;
                    float detailNoiseX = xCoord * detailScale;
                    float detailNoiseZ = zCoord * detailScale;
                    float detailNoise = Mathf.PerlinNoise(detailNoiseX, detailNoiseZ) * 0.2f;

                    float noiseHeight = 0f;
                    float amplitude = 1f;
                    float frequency = 1f;
                    float totalAmplitude = 0f;

                    // Generate octaves
                    for (int i = 0; i < terrainData.octaves; i++)
                    {
                        float perlinValue = Mathf.PerlinNoise(xCoord * frequency, zCoord * frequency);
                        noiseHeight += perlinValue * amplitude;
                        totalAmplitude += amplitude;

                        amplitude *= terrainData.persistence;
                        frequency *= terrainData.lacunarity;
                    }

                    // Normalize noise height
                    noiseHeight /= totalAmplitude;

                    // Add detail noise
                    noiseHeight += detailNoise;

                    // Apply smooth step for more natural transitions
                    noiseHeight = Mathf.SmoothStep(0f, 1f, noiseHeight);

                    // Scale height
                    terrainData.heightData[x, z] = noiseHeight * heightMultiplier;

                    // Set biome with smooth transitions
                    if (noiseHeight < PLAINS_THRESHOLD)
                    {
                        terrainData.biomeData[x, z] = TerrainDataComponent.BiomeType.Plains;
                    }
                    else if (noiseHeight < FOREST_THRESHOLD)
                    {
                        float t = (noiseHeight - PLAINS_THRESHOLD) / (FOREST_THRESHOLD - PLAINS_THRESHOLD);
                        terrainData.biomeData[x, z] = t < 0.5f ? TerrainDataComponent.BiomeType.Plains : TerrainDataComponent.BiomeType.Forest;
                    }
                    else
                    {
                        float t = (noiseHeight - FOREST_THRESHOLD) / (1f - FOREST_THRESHOLD);
                        terrainData.biomeData[x, z] = t < 0.5f ? TerrainDataComponent.BiomeType.Forest : TerrainDataComponent.BiomeType.Mountains;
                    }
                }
            }

            // Update mesh
            terrainData.UpdateMesh();
            Debug.Log($"[TerrainSystem] Generated terrain for chunk at {state.chunkCoord}");
        }

        private void UpdateChunkNeighbors(Vector2Int coord)
        {
            if (activeChunks.TryGetValue(coord, out Entity chunk))
            {
                var state = chunk.GetComponent<ChunkStateComponent>();
                if (state != null)
                {
                    // Get neighboring chunks
                    activeChunks.TryGetValue(coord + Vector2Int.up, out Entity north);
                    activeChunks.TryGetValue(coord + Vector2Int.down, out Entity south);
                    activeChunks.TryGetValue(coord + Vector2Int.right, out Entity east);
                    activeChunks.TryGetValue(coord + Vector2Int.left, out Entity west);

                    // Set neighbors
                    state.SetNeighbors(
                        north?.GetComponent<ChunkStateComponent>(),
                        south?.GetComponent<ChunkStateComponent>(),
                        east?.GetComponent<ChunkStateComponent>(),
                        west?.GetComponent<ChunkStateComponent>()
                    );
                }
            }
        }

        private Vector2Int WorldToChunkCoord(Vector3 worldPosition)
        {
            // Get a sample chunk to use its size and scale
            var sampleChunk = registeredEntities.FirstOrDefault()?.GetComponent<ChunkStateComponent>();
            float chunkSize = (sampleChunk?.size ?? 32) - 1;
            float chunkScale = sampleChunk?.scale ?? 1f;

            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / (chunkSize * chunkScale)),
                Mathf.FloorToInt(worldPosition.z / (chunkSize * chunkScale))
            );
        }

        public float GetHeightAt(Vector3 worldPosition)
        {
            Vector2Int chunkCoord = WorldToChunkCoord(worldPosition);
            if (activeChunks.TryGetValue(chunkCoord, out Entity chunk))
            {
                var terrainData = chunk.GetComponent<TerrainDataComponent>();
                var state = chunk.GetComponent<ChunkStateComponent>();
                if (terrainData != null && state != null)
                {
                    // Convert world position to chunk local position
                    Vector2 localPos = new Vector2(
                        worldPosition.x - (chunkCoord.x * (state.size - 1) * state.scale),
                        worldPosition.z - (chunkCoord.y * (state.size - 1) * state.scale)
                    );
                    return terrainData.GetHeightAt(localPos);
                }
            }
            return 0f;
        }

        public TerrainDataComponent.BiomeType GetBiomeAt(Vector3 worldPosition)
        {
            Vector2Int chunkCoord = WorldToChunkCoord(worldPosition);
            if (activeChunks.TryGetValue(chunkCoord, out Entity chunk))
            {
                var terrainData = chunk.GetComponent<TerrainDataComponent>();
                var state = chunk.GetComponent<ChunkStateComponent>();
                if (terrainData != null && state != null)
                {
                    Vector2 localPos = new Vector2(
                        worldPosition.x - (chunkCoord.x * (state.size - 1) * state.scale),
                        worldPosition.z - (chunkCoord.y * (state.size - 1) * state.scale)
                    );
                    return terrainData.GetBiomeAt(localPos);
                }
            }
            return TerrainDataComponent.BiomeType.Plains;
        }
    }
}
