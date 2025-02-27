using UnityEngine;
using System;
using System.Collections.Generic;
using ECS.Core.Messaging;

namespace ECS.Core
{
    public abstract class SystemBase : MonoBehaviour
    {
        protected WorldManager worldManager;
        protected HashSet<Entity> registeredEntities = new HashSet<Entity>();
        protected bool isInitialized = false;
        
        // Protected access to message bus
        protected MessageBus MessageBus => worldManager?.MessageBus;

        // Helper methods for message handling
        protected void PublishMessage<T>(T message) where T : Messaging.Message
        {
            MessageBus?.Publish(message);
        }

        protected void SubscribeToMessage<T>(Action<T> handler) where T : Messaging.Message
        {
            MessageBus?.Subscribe(handler);
        }

        protected void UnsubscribeFromMessage<T>(Action<T> handler) where T : Messaging.Message
        {
            MessageBus?.Unsubscribe(handler);
        }

        protected virtual void Awake()
        {
            // Disable the system initially
            enabled = false;

            // Find or create world manager
            worldManager = UnityEngine.Object.FindFirstObjectByType<WorldManager>(FindObjectsInactive.Include);
            if (worldManager == null)
            {
                var go = new GameObject("World Manager");
                worldManager = go.AddComponent<WorldManager>();
                Debug.Log($"[{GetType().Name}] Created WorldManager automatically");
            }

            Debug.Log($"[{GetType().Name}] Awake completed");
        }

        protected virtual void Start()
        {
            // Register with world manager
            if (worldManager != null)
            {
                worldManager.RegisterSystem(this);
                Debug.Log($"[{GetType().Name}] Registered with WorldManager");
            }
        }

        protected virtual void OnEnable()
        {
            if (!isInitialized && worldManager != null)
            {
                Debug.Log($"[{GetType().Name}] OnEnable - Checking dependencies");
                if (CheckDependencies())
                {
                    Debug.Log($"[{GetType().Name}] Dependencies satisfied, initializing");
                    Initialize();
                }
                else
                {
                    Debug.Log($"[{GetType().Name}] Dependencies not ready, disabling");
                    enabled = false;
                }
            }
        }

        protected virtual void OnDisable()
        {
            Debug.Log($"[{GetType().Name}] Disabled");
        }

        protected virtual void OnDestroy()
        {
            if (worldManager != null)
            {
                // Automatically unsubscribe from all messages when system is destroyed
                if (MessageBus != null)
                {
                    MessageBus.ClearSubscriptions();
                }
                
                worldManager.UnregisterSystem(this);
                Debug.Log($"[{GetType().Name}] Unregistered from WorldManager");
            }
        }

        // Override this to check if required systems are ready
        protected virtual bool CheckDependencies()
        {
            return true;
        }

        // Override this to perform initialization
        protected virtual void Initialize()
        {
            isInitialized = true;
            
            // Publish initialization message
            PublishMessage(new SystemInitializedMessage(this));
            
            Debug.Log($"[{GetType().Name}] Initialized");
        }

        public virtual void RegisterEntity(Entity entity)
        {
            if (entity != null && !registeredEntities.Contains(entity))
            {
                registeredEntities.Add(entity);
                OnEntityRegistered(entity);
                Debug.Log($"[{GetType().Name}] Registered entity: {entity.gameObject.name}");
            }
        }

        public virtual void UnregisterEntity(Entity entity)
        {
            if (entity != null && registeredEntities.Contains(entity))
            {
                registeredEntities.Remove(entity);
                OnEntityUnregistered(entity);
                Debug.Log($"[{GetType().Name}] Unregistered entity: {entity.gameObject.name}");
            }
        }

        protected virtual void OnEntityRegistered(Entity entity) { }
        protected virtual void OnEntityUnregistered(Entity entity) { }

        // Called by WorldManager during Update
        public virtual void UpdateSystem() 
        {
            if (!isInitialized)
            {
                if (CheckDependencies())
                {
                    Debug.Log($"[{GetType().Name}] Late initialization");
                    Initialize();
                    enabled = true;
                }
                else
                {
                    enabled = false;
                }
            }
        }

        // Helper method to get another system
        protected T GetSystem<T>() where T : SystemBase
        {
            var system = worldManager?.GetSystem<T>();
            if (system == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Failed to get system of type {typeof(T).Name}");
            }
            return system;
        }

        // Helper method to check if a system exists and is initialized
        protected bool IsSystemReady<T>() where T : SystemBase
        {
            var system = GetSystem<T>();
            var isReady = system != null && system.isInitialized;
            Debug.Log($"[{GetType().Name}] Checking if {typeof(T).Name} is ready: {isReady}");
            return isReady;
        }
    }
}
