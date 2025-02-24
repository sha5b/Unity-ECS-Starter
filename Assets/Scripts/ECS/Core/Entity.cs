using UnityEngine;
using System.Collections.Generic;

namespace ECS.Core
{
    public class Entity : MonoBehaviour
    {
        private WorldManager worldManager;
        private HashSet<ComponentBase> components = new HashSet<ComponentBase>();
        private HashSet<SystemBase> registeredSystems = new HashSet<SystemBase>();

        private void Awake()
        {
            // Find world manager
            worldManager = Object.FindFirstObjectByType<WorldManager>(FindObjectsInactive.Include);
            if (worldManager == null)
            {
                var go = new GameObject("World Manager");
                worldManager = go.AddComponent<WorldManager>();
                Debug.Log($"[Entity:{gameObject.name}] Created WorldManager automatically");
            }

            // Register with world manager
            if (worldManager != null)
            {
                worldManager.RegisterEntity(this);
                Debug.Log($"[Entity:{gameObject.name}] Registered with WorldManager");
            }
            else
            {
                Debug.LogError($"[Entity:{gameObject.name}] Failed to find or create WorldManager");
            }
        }

        private void OnDestroy()
        {
            // Unregister from world manager
            if (worldManager != null)
            {
                worldManager.UnregisterEntity(this);
                Debug.Log($"[Entity:{gameObject.name}] Unregistered from WorldManager");
            }

            // Unregister from all systems
            foreach (var system in registeredSystems)
            {
                system.UnregisterEntity(this);
                Debug.Log($"[Entity:{gameObject.name}] Unregistered from {system.GetType().Name}");
            }
            registeredSystems.Clear();
        }

        public void RegisterComponent(ComponentBase component)
        {
            if (component != null && !components.Contains(component))
            {
                components.Add(component);
                Debug.Log($"[Entity:{gameObject.name}] Registered component: {component.GetType().Name}");
                
                // Notify world manager of new component
                if (worldManager != null)
                {
                    worldManager.OnEntityComponentAdded(this, component);
                }
            }
        }

        public void UnregisterComponent(ComponentBase component)
        {
            if (component != null && components.Contains(component))
            {
                components.Remove(component);
                Debug.Log($"[Entity:{gameObject.name}] Unregistered component: {component.GetType().Name}");
                
                // Notify world manager of removed component
                if (worldManager != null)
                {
                    worldManager.OnEntityComponentRemoved(this, component);
                }
            }
        }

        public void RegisterWithSystem(SystemBase system)
        {
            if (system != null && !registeredSystems.Contains(system))
            {
                registeredSystems.Add(system);
                Debug.Log($"[Entity:{gameObject.name}] Registered with system: {system.GetType().Name}");
            }
        }

        public void UnregisterFromSystem(SystemBase system)
        {
            if (system != null && registeredSystems.Contains(system))
            {
                registeredSystems.Remove(system);
                Debug.Log($"[Entity:{gameObject.name}] Unregistered from system: {system.GetType().Name}");
            }
        }

        public new T GetComponent<T>() where T : ComponentBase
        {
            foreach (var component in components)
            {
                if (component is T typedComponent)
                {
                    return typedComponent;
                }
            }
            return null;
        }

        public bool HasComponent<T>() where T : ComponentBase
        {
            foreach (var component in components)
            {
                if (component is T)
                {
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<ComponentBase> GetAllComponents()
        {
            return components;
        }

        public IEnumerable<SystemBase> GetRegisteredSystems()
        {
            return registeredSystems;
        }
    }
}
