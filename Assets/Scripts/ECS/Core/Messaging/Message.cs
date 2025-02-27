using UnityEngine;
using System;

namespace ECS.Core.Messaging
{
    public class Message
    {
        // Unique ID for message tracking
        public readonly Guid Id = Guid.NewGuid();
        
        // Timestamp when message was created
        public readonly float Timestamp = Time.time;
        
        // Source of the message (can be null)
        public readonly Entity Source;
        
        // Priority of the message
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;
        
        public Message(Entity source = null)
        {
            Source = source;
        }
    }

    // Priority levels for message processing
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}
