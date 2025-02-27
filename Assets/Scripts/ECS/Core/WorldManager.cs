using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using ECS.Core.Messaging;

namespace ECS.Core
{
    public class WorldManager : MonoBehaviour
    {
        private HashSet<SystemBase> systems = new HashSet<SystemBase>();
        private HashSet<Entity> entities = new HashSet<Entity>();
        private Dictionary<System.Type, List<Entity>> componentEntityMap = new Dictionary<System.Type, List<Entity>>();
        private bool isInitializing = false;

        // Message bus for system communication
        private MessageBus messageBus;
        public MessageBus MessageBus => messageBus;

        private void Awake()
        {
            // Initialize message bus
            messageBus = new MessageBus();
            Debug.Log("[WorldManager] MessageBus initialized");
        }

        private void Start()
        {
            // Find all systems in the scene using the newer FindObjectsByType method
            var sceneSystems = UnityEngine.Object.FindObjectsByType<SystemBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            // First register all systems
            foreach (var system in sceneSystems)
            {
                RegisterSystem(system);
            }

            Debug.Log($"[WorldManager] Found {sceneSystems.Length} systems in scene");

            // Start initialization process
            StartInitialization();
        }

        private void StartInitialization()
        {
            if (isInitializing) return;
            isInitializing = true;

            Debug.Log("[WorldManager] Starting system initialization");
            
            // Try to initialize all systems
            bool allInitialized;
            int maxAttempts = 10;
            int attempt = 0;

            do
            {
                attempt++;
                allInitialized = true;
                int initializedThisAttempt = 0;

                foreach (var system in systems)
                {
                    if (!system.enabled)
                    {
                        system.enabled = true;
                        if (!system.enabled) // If system disabled itself due to dependencies
                        {
                            allInitialized = false;
                            Debug.Log($"[WorldManager] System {system.GetType().Name} waiting for dependencies");
                        }
                        else
                        {
                            initializedThisAttempt++;
                            Debug.Log($"[WorldManager] System {system.GetType().Name} initialized");
                        }
                    }
                }

                Debug.Log($"[WorldManager] Initialization attempt {attempt}: Initialized {initializedThisAttempt} systems");

                if (!allInitialized && attempt >= maxAttempts)
                {
                    var uninitialized = systems.Where(s => !s.enabled).Select(s => s.GetType().Name);
                    Debug.LogError($"[WorldManager] Failed to initialize systems after {maxAttempts} attempts. Uninitialized systems: {string.Join(", ", uninitialized)}");
                    break;
                }

            } while (!allInitialized && attempt < maxAttempts);

            isInitializing = false;
            Debug.Log($"[WorldManager] Initialization complete. {systems.Count(s => s.enabled)} systems active");
        }

        private void Update()
        {
            if (!isInitializing)
            {
                // Process any pending messages before updating systems
                messageBus.ProcessMessages();

                // Update all systems in registration order
                foreach (var system in systems)
                {
                    if (system.enabled)
                    {
                        system.UpdateSystem();
                    }
                }

                // Check if any systems need initialization
                bool anyNeedInit = systems.Any(s => !s.enabled);
                if (anyNeedInit)
                {
                    StartInitialization();
                }

                // Process any messages generated during system updates
                messageBus.ProcessMessages();
            }
        }

        public void RegisterSystem(SystemBase system)
        {
            if (system != null && !systems.Contains(system))
            {
                systems.Add(system);
                Debug.Log($"[WorldManager] Registered system: {system.GetType().Name}");
            }
        }

        public void UnregisterSystem(SystemBase system)
        {
            if (system != null && systems.Contains(system))
            {
                systems.Remove(system);
                Debug.Log($"[WorldManager] Unregistered system: {system.GetType().Name}");
            }
        }

        public void RegisterEntity(Entity entity)
        {
            if (entity != null && !entities.Contains(entity))
            {
                entities.Add(entity);
                Debug.Log($"[WorldManager] Registered entity: {entity.gameObject.name}");

                // Register with appropriate systems
                foreach (var system in systems)
                {
                    if (system.enabled)
                    {
                        system.RegisterEntity(entity);
                    }
                }
            }
        }

        public void UnregisterEntity(Entity entity)
        {
            if (entity != null && entities.Contains(entity))
            {
                entities.Remove(entity);
                Debug.Log($"[WorldManager] Unregistered entity: {entity.gameObject.name}");

                // Remove from component-entity map
                foreach (var kvp in componentEntityMap)
                {
                    kvp.Value.Remove(entity);
                }

                // Unregister from all systems
                foreach (var system in systems)
                {
                    system.UnregisterEntity(entity);
                }
            }
        }

        public void OnEntityComponentAdded(Entity entity, ComponentBase component)
        {
            var componentType = component.GetType();
            
            // Add to component-entity map
            if (!componentEntityMap.ContainsKey(componentType))
            {
                componentEntityMap[componentType] = new List<Entity>();
            }
            if (!componentEntityMap[componentType].Contains(entity))
            {
                componentEntityMap[componentType].Add(entity);
                Debug.Log($"[WorldManager] Added {componentType.Name} to entity: {entity.gameObject.name}");
            }

            // Check if entity should be registered with any systems
            foreach (var system in systems)
            {
                if (system.enabled)
                {
                    system.RegisterEntity(entity);
                }
            }
        }

        public void OnEntityComponentRemoved(Entity entity, ComponentBase component)
        {
            var componentType = component.GetType();
            
            // Remove from component-entity map
            if (componentEntityMap.ContainsKey(componentType))
            {
                componentEntityMap[componentType].Remove(entity);
                Debug.Log($"[WorldManager] Removed {componentType.Name} from entity: {entity.gameObject.name}");
            }

            // Check if entity should be unregistered from any systems
            foreach (var system in systems)
            {
                system.UnregisterEntity(entity);
            }
        }

        public IEnumerable<Entity> GetEntitiesWithComponent<T>() where T : ComponentBase
        {
            var componentType = typeof(T);
            if (componentEntityMap.ContainsKey(componentType))
            {
                return componentEntityMap[componentType];
            }
            return Enumerable.Empty<Entity>();
        }

        public IEnumerable<T> GetAllComponentsOfType<T>() where T : ComponentBase
        {
            return GetEntitiesWithComponent<T>()
                .Select(entity => entity.GetComponent<T>())
                .Where(component => component != null);
        }

        public IEnumerable<SystemBase> GetAllSystems()
        {
            return systems;
        }

        public T GetSystem<T>() where T : SystemBase
        {
            return systems.OfType<T>().FirstOrDefault();
        }

        private void OnDestroy()
        {
            // Clean up all entities
            foreach (var entity in entities.ToList())
            {
                UnregisterEntity(entity);
            }
            entities.Clear();
            componentEntityMap.Clear();

            // Clean up all systems
            foreach (var system in systems.ToList())
            {
                UnregisterSystem(system);
            }
            systems.Clear();

            // Clear message bus subscriptions
            messageBus.ClearSubscriptions();

            Debug.Log("[WorldManager] Cleanup complete");
        }
    }
}
