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
        [SerializeField] private Material terrainMaterial;

        [Header("Generation Settings")]
        [SerializeField] private float heightMultiplier = 25f;
        [SerializeField] private int terrainSize = 256;
        [SerializeField] private float terrainScale = 1f;

        private Entity terrainEntity;
        private TerrainDataComponent terrainData;
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
            terrainNoise.SetFrequency(0.01f); // Adjusted for a single terrain
            terrainNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            terrainNoise.SetFractalOctaves(6);

            biomeNoise = new FastNoiseLite(43);
            biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            biomeNoise.SetFrequency(0.005f); // Wider biome regions
            biomeNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            biomeNoise.SetFractalOctaves(3);

            detailNoise = new FastNoiseLite(44);
            detailNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            detailNoise.SetFrequency(0.02f); // Finer detail scale
            detailNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            detailNoise.SetFractalOctaves(3);

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

            // Create a single terrain entity
            CreateTerrain();
            
            Debug.Log("[TerrainSystem] Initialized with single terrain plane");
        }

        public override void UpdateSystem()
        {
            if (!isInitialized) return;
            
            // No need for continuous updates in the simplified system
            // The terrain is generated once and remains static
        }

        private void CreateTerrain()
        {
            // Create terrain game object
            var go = new GameObject("Terrain");
            
            // Add components
            terrainEntity = go.AddComponent<Entity>();
            terrainData = go.AddComponent<TerrainDataComponent>();
            
            // Initialize terrain data
            terrainData.terrainSize = terrainSize;
            terrainData.terrainScale = terrainScale;
            terrainData.Initialize();
            terrainData.SetMaterial(customTerrainMaterial);
            
            // Center the terrain at origin
            float halfSize = (terrainSize * terrainScale) / 2f;
            go.transform.position = new Vector3(-halfSize, 0, -halfSize);
            
            // Generate terrain
            GenerateTerrain();
            
            Debug.Log("[TerrainSystem] Created terrain entity centered at origin");
        }

        private void GenerateTerrain()
        {
            if (terrainData == null) return;

            int size = terrainData.terrainSize;
            
            // Generate height data
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Calculate normalized coordinates for noise
                    float normalizedX = (float)x / size;
                    float normalizedZ = (float)z / size;
                    
                    // Generate base terrain
                    float baseHeight = terrainNoise.GetNoise(x, z);
                    baseHeight = (baseHeight + 1f) * 0.5f;

                    // Generate biome variation
                    float biomeValue = biomeNoise.GetNoise(x, z);
                    float biomeHeight = (biomeValue + 1f) * 0.5f;
                    
                    // Add fine detail
                    float detailValue = detailNoise.GetNoise(x * 5f, z * 5f);
                    float detail = (detailValue + 1f) * 0.5f * 0.15f;

                    // Combine heights with weighting
                    float combinedHeight = baseHeight * 0.6f + biomeHeight * 0.3f + detail * 0.1f;
                    float finalHeight = Mathf.SmoothStep(0f, 1f, combinedHeight) * heightMultiplier;

                    terrainData.heightData[x, z] = finalHeight;

                    // Set biome based on height
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
            Debug.Log($"[TerrainSystem] Generated terrain with {size}x{size} vertices");
        }

        public float GetHeightAt(Vector3 worldPosition)
        {
            if (terrainData == null) return 0f;

            // Convert world position to local position, accounting for the centered terrain
            Vector3 localPosition = worldPosition - terrainEntity.transform.position;
            Vector2 localPos = new Vector2(
                localPosition.x / terrainData.terrainScale,
                localPosition.z / terrainData.terrainScale
            );
            
            return terrainData.GetHeightAt(localPos);
        }

        public TerrainDataComponent.BiomeType GetBiomeAt(Vector3 worldPosition)
        {
            if (terrainData == null) return TerrainDataComponent.BiomeType.Plains;

            // Convert world position to local position, accounting for the centered terrain
            Vector3 localPosition = worldPosition - terrainEntity.transform.position;
            Vector2 localPos = new Vector2(
                localPosition.x / terrainData.terrainScale,
                localPosition.z / terrainData.terrainScale
            );
            
            return terrainData.GetBiomeAt(localPos);
        }
    }
}
