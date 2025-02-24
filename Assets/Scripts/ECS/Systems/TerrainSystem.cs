using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;
using System.Linq;

namespace ECS.Systems
{
    public class FastNoiseLite
    {
        public enum NoiseType { OpenSimplex2 }
        public enum FractalType { FBm }

        private System.Random random;
        private float frequency = 1f;
        private int octaves = 1;

        public FastNoiseLite(int seed)
        {
            random = new System.Random(seed);
        }

        public void SetNoiseType(NoiseType type) { }
        public void SetFrequency(float freq) { frequency = freq; }
        public void SetFractalType(FractalType type) { }
        public void SetFractalOctaves(int o) { octaves = o; }

        public float GetNoise(float x, float y)
        {
            return GetNoise(x, 0, y);
        }

        public float GetNoise(float x, float y, float z)
        {
            x *= frequency;
            y *= frequency;
            z *= frequency;

            float sum = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float freq = 1f;

            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * (Mathf.PerlinNoise(x * freq, z * freq) * 2f - 1f);
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                freq *= 2f;
            }

            return sum / amplitudeSum;
        }
    }

    public class TerrainSystem : SystemBase
    {
        [Header("Terrain Settings")]
        [SerializeField] private int viewDistance = 5;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private Material terrainMaterial;

        [Header("Generation Settings")]
        [SerializeField] private float heightMultiplier = 25f; // Reduced for gentler slopes
        [SerializeField] private float blendDistance = 24f; // Further increased for even wider transitions

        private Dictionary<Vector2Int, Entity> activeChunks = new Dictionary<Vector2Int, Entity>();
        private Queue<Entity> chunkPool = new Queue<Entity>();
        private Transform viewer;
        private Transform chunksParent;
        private Material customTerrainMaterial;

        // Noise generators
        private FastNoiseLite terrainNoise;
        private FastNoiseLite biomeNoise;
        private FastNoiseLite detailNoise;

        // Biome transition thresholds
        private const float PLAINS_THRESHOLD = 0.3f;
        private const float FOREST_THRESHOLD = 0.6f;

        protected override void Initialize()
        {
            // Initialize noise generators
            terrainNoise = new FastNoiseLite(42);
            terrainNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            terrainNoise.SetFrequency(1f); // Base frequency for world coordinates
            terrainNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            terrainNoise.SetFractalOctaves(6);

            biomeNoise = new FastNoiseLite(43);
            biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            biomeNoise.SetFrequency(0.5f); // Wider biome regions
            biomeNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            biomeNoise.SetFractalOctaves(3);

            detailNoise = new FastNoiseLite(44);
            detailNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            detailNoise.SetFrequency(2f); // Finer detail scale
            detailNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            detailNoise.SetFractalOctaves(3);

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

            // Initialize terrain data
            terrainData.Initialize();

            // Cache neighbor heights for blending
            float[,] northHeights = null, southHeights = null, eastHeights = null, westHeights = null;
            if (state.northNeighbor != null) northHeights = state.northNeighbor.GetComponent<TerrainDataComponent>()?.heightData;
            if (state.southNeighbor != null) southHeights = state.southNeighbor.GetComponent<TerrainDataComponent>()?.heightData;
            if (state.eastNeighbor != null) eastHeights = state.eastNeighbor.GetComponent<TerrainDataComponent>()?.heightData;
            if (state.westNeighbor != null) westHeights = state.westNeighbor.GetComponent<TerrainDataComponent>()?.heightData;

            // Generate height data
            for (int z = 0; z < state.size; z++)
            {
                for (int x = 0; x < state.size; x++)
                {
                    // Calculate world position
                    float worldX = (state.chunkCoord.x * (state.size - 1) + x) * state.scale;
                    float worldZ = (state.chunkCoord.y * (state.size - 1) + z) * state.scale;

                    // Calculate absolute world coordinates for continuous noise
                    float absoluteX = state.chunkCoord.x + (float)x / state.size;
                    float absoluteZ = state.chunkCoord.y + (float)z / state.size;

                    // Generate base terrain using absolute coordinates
                    float baseHeight = terrainNoise.GetNoise(absoluteX, absoluteZ);
                    baseHeight = (baseHeight + 1f) * 0.5f;

                    // Generate biome variation using larger scale coordinates
                    float biomeValue = biomeNoise.GetNoise(absoluteX * 0.2f, absoluteZ * 0.2f);
                    float biomeHeight = (biomeValue + 1f) * 0.5f;
                    
                    // Add fine detail using smaller scale coordinates
                    float detailValue = detailNoise.GetNoise(absoluteX * 5f, absoluteZ * 5f);
                    float detail = (detailValue + 1f) * 0.5f * 0.15f; // Slightly reduced detail influence

                    // Combine heights with improved weighting
                    float combinedHeight = baseHeight * 0.6f + biomeHeight * 0.3f + detail * 0.1f;
                    float finalBaseHeight = Mathf.SmoothStep(0f, 1f, combinedHeight) * heightMultiplier;

                    float finalHeight = finalBaseHeight;

                    // Calculate blend weights based on distance from edges
                    float edgeBlendX = 1f;
                    float edgeBlendZ = 1f;

                    // X-axis blending
                    if (x < blendDistance)
                        edgeBlendX = x / blendDistance;
                    else if (x > state.size - blendDistance)
                        edgeBlendX = (state.size - x) / blendDistance;

                    // Z-axis blending
                    if (z < blendDistance)
                        edgeBlendZ = z / blendDistance;
                    else if (z > state.size - blendDistance)
                        edgeBlendZ = (state.size - z) / blendDistance;

                    // Apply neighbor blending with extended transition zones
                    float blendZoneSize = blendDistance * 2;
                    
                    // West edge blending
                    if (x < blendZoneSize && westHeights != null)
                    {
                        float t = x / blendZoneSize;
                        float neighborHeight = westHeights[state.size - 1, z];
                        finalHeight = Mathf.Lerp(neighborHeight, finalHeight, Mathf.SmoothStep(0, 1, t));
                    }
                    // East edge blending
                    else if (x > state.size - blendZoneSize && eastHeights != null)
                    {
                        float t = (state.size - x) / blendZoneSize;
                        float neighborHeight = eastHeights[0, z];
                        finalHeight = Mathf.Lerp(neighborHeight, finalHeight, Mathf.SmoothStep(0, 1, t));
                    }

                    // South edge blending
                    if (z < blendZoneSize && southHeights != null)
                    {
                        float t = z / blendZoneSize;
                        float neighborHeight = southHeights[x, state.size - 1];
                        finalHeight = Mathf.Lerp(neighborHeight, finalHeight, Mathf.SmoothStep(0, 1, t));
                    }
                    // North edge blending
                    else if (z > state.size - blendZoneSize && northHeights != null)
                    {
                        float t = (state.size - z) / blendZoneSize;
                        float neighborHeight = northHeights[x, 0];
                        finalHeight = Mathf.Lerp(neighborHeight, finalHeight, Mathf.SmoothStep(0, 1, t));
                    }

                    // Enhanced corner blending with radial falloff
                    if (x < blendZoneSize && z < blendZoneSize) // Southwest corner
                    {
                        float dx = x / blendZoneSize;
                        float dz = z / blendZoneSize;
                        float cornerDist = Mathf.Sqrt(dx * dx + dz * dz);
                        float cornerBlend = Mathf.SmoothStep(0, 1.414f, cornerDist); // sqrt(2) for diagonal normalization
                        finalHeight = Mathf.Lerp(finalBaseHeight, finalHeight, cornerBlend);
                    }
                    else if (x < blendZoneSize && z > state.size - blendZoneSize) // Northwest corner
                    {
                        float dx = x / blendZoneSize;
                        float dz = (state.size - z) / blendZoneSize;
                        float cornerDist = Mathf.Sqrt(dx * dx + dz * dz);
                        float cornerBlend = Mathf.SmoothStep(0, 1.414f, cornerDist);
                        finalHeight = Mathf.Lerp(finalBaseHeight, finalHeight, cornerBlend);
                    }
                    else if (x > state.size - blendZoneSize && z < blendZoneSize) // Southeast corner
                    {
                        float dx = (state.size - x) / blendZoneSize;
                        float dz = z / blendZoneSize;
                        float cornerDist = Mathf.Sqrt(dx * dx + dz * dz);
                        float cornerBlend = Mathf.SmoothStep(0, 1.414f, cornerDist);
                        finalHeight = Mathf.Lerp(finalBaseHeight, finalHeight, cornerBlend);
                    }
                    else if (x > state.size - blendZoneSize && z > state.size - blendZoneSize) // Northeast corner
                    {
                        float dx = (state.size - x) / blendZoneSize;
                        float dz = (state.size - z) / blendZoneSize;
                        float cornerDist = Mathf.Sqrt(dx * dx + dz * dz);
                        float cornerBlend = Mathf.SmoothStep(0, 1.414f, cornerDist);
                        finalHeight = Mathf.Lerp(finalBaseHeight, finalHeight, cornerBlend);
                    }

                    terrainData.heightData[x, z] = finalHeight;

                    // Set biome with smooth transitions
                    float normalizedHeight = finalHeight / heightMultiplier;
                    if (normalizedHeight < PLAINS_THRESHOLD)
                    {
                        terrainData.biomeData[x, z] = TerrainDataComponent.BiomeType.Plains;
                    }
                    else if (normalizedHeight < FOREST_THRESHOLD)
                    {
                        float t = (normalizedHeight - PLAINS_THRESHOLD) / (FOREST_THRESHOLD - PLAINS_THRESHOLD);
                        terrainData.biomeData[x, z] = t < 0.5f ? TerrainDataComponent.BiomeType.Plains : TerrainDataComponent.BiomeType.Forest;
                    }
                    else
                    {
                        float t = (normalizedHeight - FOREST_THRESHOLD) / (1f - FOREST_THRESHOLD);
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
