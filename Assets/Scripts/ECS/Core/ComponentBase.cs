using UnityEngine;

namespace ECS.Core
{
    public abstract class ComponentBase : MonoBehaviour
    {
        protected WorldManager worldManager;
        protected Entity entity;

        protected virtual void Awake()
        {
            // Get or create entity component
            entity = GetComponent<Entity>();
            if (entity == null)
            {
                entity = gameObject.AddComponent<Entity>();
                Debug.Log($"[{GetType().Name}] Created Entity component");
            }

            // Find world manager
            worldManager = Object.FindFirstObjectByType<WorldManager>(FindObjectsInactive.Include);
            if (worldManager == null)
            {
                var go = new GameObject("World Manager");
                worldManager = go.AddComponent<WorldManager>();
                Debug.Log($"[{GetType().Name}] Created WorldManager automatically");
            }

            // Register with entity
            if (entity != null)
            {
                entity.RegisterComponent(this);
                Debug.Log($"[{GetType().Name}] Registered with Entity {entity.gameObject.name}");
            }
            else
            {
                Debug.LogError($"[{GetType().Name}] Failed to get or create Entity component");
            }
        }

        protected virtual void OnDestroy()
        {
            if (entity != null)
            {
                entity.UnregisterComponent(this);
                Debug.Log($"[{GetType().Name}] Unregistered from Entity {entity.gameObject.name}");
            }
        }

        public virtual void Initialize() 
        {
            Debug.Log($"[{GetType().Name}] Initialized");
        }

        // Helper method to get the entity
        public Entity GetEntity() => entity;
    }
}
