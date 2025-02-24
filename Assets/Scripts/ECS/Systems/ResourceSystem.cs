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
        [SerializeField] private int initialWorkAreas = 2;
        [SerializeField] private float spawnRadius = 20f;

        private Dictionary<ResourceComponent.ResourceType, List<ResourceComponent>> resourcesByType = 
            new Dictionary<ResourceComponent.ResourceType, List<ResourceComponent>>();

        protected override void Initialize()
        {
            // Initialize dictionary for all resource types
            foreach (ResourceComponent.ResourceType type in System.Enum.GetValues(typeof(ResourceComponent.ResourceType)))
            {
                resourcesByType[type] = new List<ResourceComponent>();
            }

            // Spawn initial resources
            SpawnInitialResources();

            base.Initialize();
        }

        public override void UpdateSystem()
        {
            if (!isInitialized)
            {
                Initialize();
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
            // Handle resource replenishment
            if (!resource.isBeingUsed)
            {
                resource.Replenish(Time.deltaTime);
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

        // Find the best available resource of a given type for an NPC
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

        // Get all resources of a specific type within range
        public IEnumerable<ResourceComponent> GetResourcesInRange(ResourceComponent.ResourceType type, Vector3 position, float range)
        {
            if (!resourcesByType.ContainsKey(type))
                return Enumerable.Empty<ResourceComponent>();

            return resourcesByType[type]
                .Where(r => r != null && Vector3.Distance(position, r.transform.position) <= range);
        }

        // Create a new resource
        public ResourceComponent CreateResource(ResourceComponent.ResourceType type, Vector3 position)
        {
            GameObject resourceObj = new GameObject($"{type} Resource");
            resourceObj.transform.position = position;

            var entity = resourceObj.AddComponent<Entity>();
            var resource = resourceObj.AddComponent<ResourceComponent>();
            
            resource.type = type;

            // Configure resource properties based on type
            switch (type)
            {
                case ResourceComponent.ResourceType.Food:
                    resource.properties.quantity = 100f;
                    resource.properties.consumptionRate = 10f;
                    resource.properties.replenishRate = 1f;
                    resource.properties.replenishDelay = 30f;
                    break;

                case ResourceComponent.ResourceType.Water:
                    resource.properties.quantity = 200f;
                    resource.properties.consumptionRate = 15f;
                    resource.properties.replenishRate = 2f;
                    resource.properties.replenishDelay = 20f;
                    break;

                case ResourceComponent.ResourceType.RestArea:
                    resource.properties.isInfinite = true;
                    resource.properties.qualityMultiplier = 1f;
                    break;

                case ResourceComponent.ResourceType.WorkArea:
                    resource.properties.isInfinite = true;
                    resource.properties.qualityMultiplier = 1f;
                    break;
            }

            return resource;
        }

        private void SpawnInitialResources()
        {
            // Create food sources
            for (int i = 0; i < initialFoodSources; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition();
                CreateResource(ResourceComponent.ResourceType.Food, randomPos);
            }

            // Create water sources
            for (int i = 0; i < initialWaterSources; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition();
                CreateResource(ResourceComponent.ResourceType.Water, randomPos);
            }

            // Create rest areas
            for (int i = 0; i < initialRestAreas; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition();
                CreateResource(ResourceComponent.ResourceType.RestArea, randomPos);
            }

            // Create work areas
            for (int i = 0; i < initialWorkAreas; i++)
            {
                Vector3 randomPos = GetRandomSpawnPosition();
                CreateResource(ResourceComponent.ResourceType.WorkArea, randomPos);
            }

            Debug.Log($"Spawned initial resources: {initialFoodSources} food, {initialWaterSources} water, {initialRestAreas} rest areas, {initialWorkAreas} work areas");
        }

        private Vector3 GetRandomSpawnPosition()
        {
            return new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0,
                Random.Range(-spawnRadius, spawnRadius)
            );
        }
    }
}
