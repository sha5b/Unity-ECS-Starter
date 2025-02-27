using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECS.Core.Messaging
{
    public class MessageBus
    {
        // Dictionary to store message handlers by type
        private readonly Dictionary<Type, List<Action<Message>>> handlers = new Dictionary<Type, List<Action<Message>>>();
        
        // Queue for handling messages in order
        private readonly Queue<Message> messageQueue = new Queue<Message>();
        
        // Lock object for thread safety
        private readonly object queueLock = new object();

        // Subscribe to a specific message type
        public void Subscribe<T>(Action<T> handler) where T : Message
        {
            var messageType = typeof(T);
            
            if (!handlers.ContainsKey(messageType))
            {
                handlers[messageType] = new List<Action<Message>>();
            }
            
            // Wrap the typed handler in a Message handler
            void MessageHandler(Message message) => handler((T)message);
            handlers[messageType].Add(MessageHandler);
            
            Debug.Log($"[MessageBus] Subscribed to {messageType.Name}");
        }

        // Unsubscribe from a specific message type
        public void Unsubscribe<T>(Action<T> handler) where T : Message
        {
            var messageType = typeof(T);
            
            if (handlers.ContainsKey(messageType))
            {
                // Remove all instances of this handler
                handlers[messageType].RemoveAll(h => h.Target == handler.Target && h.Method == handler.Method);
                
                // Remove the type entry if no handlers remain
                if (handlers[messageType].Count == 0)
                {
                    handlers.Remove(messageType);
                }
                
                Debug.Log($"[MessageBus] Unsubscribed from {messageType.Name}");
            }
        }

        // Publish a message to all subscribers
        public void Publish<T>(T message) where T : Message
        {
            if (message == null)
            {
                Debug.LogError("[MessageBus] Attempted to publish null message");
                return;
            }

            var messageType = message.GetType();
            
            lock (queueLock)
            {
                messageQueue.Enqueue(message);
            }
            
            Debug.Log($"[MessageBus] Published {messageType.Name} (ID: {message.Id})");
        }

        // Process all queued messages
        public void ProcessMessages()
        {
            lock (queueLock)
            {
                // Process messages by priority
                var messages = messageQueue.ToList()
                    .OrderByDescending(m => m.Priority)
                    .ThenBy(m => m.Timestamp);

                messageQueue.Clear();

                foreach (var message in messages)
                {
                    var messageType = message.GetType();
                    
                    if (handlers.ContainsKey(messageType))
                    {
                        foreach (var handler in handlers[messageType])
                        {
                            try
                            {
                                handler(message);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"[MessageBus] Error processing {messageType.Name}: {e}");
                            }
                        }
                    }
                }
            }
        }

        // Clear all subscriptions
        public void ClearSubscriptions()
        {
            handlers.Clear();
            Debug.Log("[MessageBus] Cleared all subscriptions");
        }

        // Get number of subscribers for a message type
        public int GetSubscriberCount<T>() where T : Message
        {
            var messageType = typeof(T);
            return handlers.ContainsKey(messageType) ? handlers[messageType].Count : 0;
        }
    }
}
