using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;
using System.Linq;

namespace ECS.Systems
{
    public class ResourceSystem : SystemBase
    {
        [Header("Initial Resource Counts")]
        [SerializeField] private int initialFoodSources = 5;
        [SerializeField] private int initialWaterSources = 3;
        [SerializeField] private int initialRestAreas = 4;
        [SerializeField] private float spawnRadius = 20f;

        [Header("Biome Settings")]
        [SerializeField] private float forestFoodMultiplier = 2f;
        [SerializeField] private float mountainsRestMultiplier = 1.5f;
        [SerializeField] private float plainsWaterMultiplier = 1.5f;

        private Dictionary<ResourceComponent.ResourceType, List<ResourceComponent>> resourcesByType = 
            new Dictionary<ResourceComponent.ResourceType, List<ResourceComponent>>();

        private TerrainSystem terrainSystem;

        protected override bool CheckDependencies()
        {
            return IsSystemReady<TerrainSystem>();
        }

        protected override void Initialize()
        {
            // Initialize dictionary for all resource types
            foreach (ResourceComponent.ResourceType type in System.Enum.GetValues(typeof(ResourceComponent.ResourceType)))
            {
                resourcesByType[type] = new List<ResourceComponent>();
            }

            terrainSystem = GetSystem<TerrainSystem>();
            if (terrainSystem != null)
            {
                // Spawn initial resources
                SpawnInitialResources();
                base.Initialize();
            }
            else
            {
                Debug.LogError("ResourceSystem initialization failed: TerrainSystem not found");
                enabled = false;
            }
        }

        public override void UpdateSystem()
        {
            if (!isInitialized)
            {
                if (CheckDependencies())
                {
                    Initialize();
                }
                return;
            }

            // Update all resources
            foreach (var entity in registeredEntities)
            {
                var resource = entity.GetComponent<ResourceComponent>();
                if (resource != null)
                {
                    UpdateResource(resource);
                }
            }
        }

        private void UpdateResource(ResourceComponent resource)
        {
            if (!resource.isBeingUsed)
            {
                // Get biome at resource location
                var biome = terrainSystem.GetBiomeAt(resource.transform.position);
                float replenishMultiplier = GetBiomeReplenishMultiplier(resource.type, biome);
                
                // Replenish with biome modifier
                resource.Replenish(Time.deltaTime * replenishMultiplier);
            }
        }

        private float GetBiomeReplenishMultiplier(ResourceComponent.ResourceType resourceType, TerrainDataComponent.BiomeType biome)
        {
            switch (biome)
            {
                case TerrainDataComponent.BiomeType.Forest when resourceType == ResourceComponent.ResourceType.Food:
                    return forestFoodMultiplier;
                case TerrainDataComponent.BiomeType.Mountains when resourceType == ResourceComponent.ResourceType.RestArea:
                    return mountainsRestMultiplier;
                case TerrainDataComponent.BiomeType.Plains when resourceType == ResourceComponent.ResourceType.Water:
                    return plainsWaterMultiplier;
                default:
                    return 1f;
            }
        }

        public override void RegisterEntity(Entity entity)
        {
            if (entity.HasComponent<ResourceComponent>())
            {
                base.RegisterEntity(entity);
                
                // Add to type-specific list
                var resource = entity.GetComponent<ResourceComponent>();
                resourcesByType[resource.type].Add(resource);
            }
        }

        public override void UnregisterEntity(Entity entity)
        {
            if (entity.HasComponent<ResourceComponent>())
            {
                var resource = entity.GetComponent<ResourceComponent>();
                resourcesByType[resource.type].Remove(resource);
                
                base.UnregisterEntity(entity);
            }
        }

        public ResourceComponent FindBestResource(ResourceComponent.ResourceType type, Vector3 position, float maxDistance = 50f)
        {
            if (!resourcesByType.ContainsKey(type))
                return null;

            return resourcesByType[type]
                .Where(r => r != null && r.CanBeUsed() && Vector3.Distance(position, r.transform.position) <= maxDistance)
                .OrderBy(r => Vector3.Distance(position, r.transform.position)) // Prioritize closest
                .ThenByDescending(r => r.properties.qualityMultiplier)         // Then highest quality
                .ThenByDescending(r => r.GetQuantityPercentage())             // Then most available
                .FirstOrDefault();
        }

        public IEnumerable<ResourceComponent> GetResourcesInRange(ResourceComponent.ResourceType type, Vector3 position, float range)
        {
            if (!resourcesByType.ContainsKey(type))
                return Enumerable.Empty<ResourceComponent>();

            return resourcesByType[type]
                .Where(r => r != null && Vector3.Distance(position, r.transform.position) <= range);
        }

        public ResourceComponent CreateResource(ResourceComponent.ResourceType type, Vector3 position)
        {
            GameObject resourceObj = new GameObject($"{type} Resource");
            resourceObj.transform.position = position;

            var entity = resourceObj.AddComponent<Entity>();
            var resource = resourceObj.AddComponent<ResourceComponent>();
            
            resource.type = type;

            // Configure resource properties based on type and biome
            var biome = terrainSystem.GetBiomeAt(position);
            ConfigureResourceProperties(resource, biome);

            // Adjust position to terrain height
            position.y = terrainSystem.GetHeightAt(position);
            resourceObj.transform.position = position;

            return resource;
        }

        private void ConfigureResourceProperties(ResourceComponent resource, TerrainDataComponent.BiomeType biome)
        {
            switch (resource.type)
            {
                case ResourceComponent.ResourceType.Food:
                    resource.properties.quantity = 100f;
                    resource.properties.consumptionRate = 10f;
                    resource.properties.replenishRate = biome == TerrainDataComponent.BiomeType.Forest ? 2f : 1f;
                    resource.properties.replenishDelay = 30f;
                    resource.properties.qualityMultiplier = biome == TerrainDataComponent.BiomeType.Forest ? 1.5f : 1f;
                    break;

                case ResourceComponent.ResourceType.Water:
                    resource.properties.quantity = 200f;
                    resource.properties.consumptionRate = 15f;
                    resource.properties.replenishRate = biome == TerrainDataComponent.BiomeType.Plains ? 3f : 2f;
                    resource.properties.replenishDelay = 20f;
                    resource.properties.qualityMultiplier = biome == TerrainDataComponent.BiomeType.Plains ? 1.5f : 1f;
                    break;

                case ResourceComponent.ResourceType.RestArea:
                    resource.properties.isInfinite = true;
                    resource.properties.qualityMultiplier = biome == TerrainDataComponent.BiomeType.Mountains ? 1.5f : 1f;
                    break;
            }
        }

        private void SpawnInitialResources()
        {
            // Create food sources
            for (int i = 0; i < initialFoodSources; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition(ResourceComponent.ResourceType.Food);
                CreateResource(ResourceComponent.ResourceType.Food, randomPos);
            }

            // Create water sources
            for (int i = 0; i < initialWaterSources; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition(ResourceComponent.ResourceType.Water);
                CreateResource(ResourceComponent.ResourceType.Water, randomPos);
            }

            // Create rest areas
            for (int i = 0; i < initialRestAreas; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition(ResourceComponent.ResourceType.RestArea);
                CreateResource(ResourceComponent.ResourceType.RestArea, randomPos);
            }

            Debug.Log($"[ResourceSystem] Spawned initial resources: {initialFoodSources} food, {initialWaterSources} water, {initialRestAreas} rest areas");
        }

        private Vector3 GetRandomSpawnPosition(ResourceComponent.ResourceType type)
        {
            Vector3 position;
            TerrainDataComponent.BiomeType biome;
            int maxAttempts = 10;
            int attempt = 0;

            do
            {
                position = new Vector3(
                    Random.Range(-spawnRadius, spawnRadius),
                    0,
                    Random.Range(-spawnRadius, spawnRadius)
                );

                biome = terrainSystem.GetBiomeAt(position);
                attempt++;

                // Check if this biome is suitable for this resource type
                if (IsSuitableBiome(type, biome))
                {
                    position.y = terrainSystem.GetHeightAt(position);
                    return position;
                }

            } while (attempt < maxAttempts);

            // If we couldn't find a suitable biome, just return any position
            position.y = terrainSystem.GetHeightAt(position);
            return position;
        }

        private bool IsSuitableBiome(ResourceComponent.ResourceType type, TerrainDataComponent.BiomeType biome)
        {
            switch (type)
            {
                case ResourceComponent.ResourceType.Food:
                    return biome == TerrainDataComponent.BiomeType.Forest || biome == TerrainDataComponent.BiomeType.Plains;
                case ResourceComponent.ResourceType.Water:
                    return biome == TerrainDataComponent.BiomeType.Plains;
                case ResourceComponent.ResourceType.RestArea:
                    return biome == TerrainDataComponent.BiomeType.Mountains || biome == TerrainDataComponent.BiomeType.Forest;
                default:
                    return true;
            }
        }
    }
}
