using UnityEngine;
using ECS.Components;

namespace ECS.Core.Messaging
{
    // Message sent when a system is initialized
    public class SystemInitializedMessage : Message
    {
        public readonly SystemBase System;
        public readonly string SystemName;

        public SystemInitializedMessage(SystemBase system) : base(null)
        {
            System = system;
            SystemName = system.GetType().Name;
            Priority = MessagePriority.Normal;
        }
    }

    // Message sent when a resource state changes
    public class ResourceStateChangedMessage : Message
    {
        public readonly Vector3 Position;
        public readonly string ResourceType;
        public readonly float CurrentQuantity;
        public readonly float MaxQuantity;
        public readonly bool IsDepleted;

        public ResourceStateChangedMessage(
            Entity source,
            Vector3 position,
            string resourceType,
            float currentQuantity,
            float maxQuantity,
            bool isDepleted
        ) : base(source)
        {
            Position = position;
            ResourceType = resourceType;
            CurrentQuantity = currentQuantity;
            MaxQuantity = maxQuantity;
            IsDepleted = isDepleted;
            Priority = MessagePriority.Normal;
        }
    }

    // Message sent when an NPC's needs change significantly
    public class NPCNeedsChangedMessage : Message
    {
        public readonly string NPCName;
        public readonly string NeedType;
        public readonly float PreviousValue;
        public readonly float NewValue;
        public readonly bool IsUrgent;

        public NPCNeedsChangedMessage(
            Entity source,
            string npcName,
            string needType,
            float previousValue,
            float newValue,
            bool isUrgent
        ) : base(source)
        {
            NPCName = npcName;
            NeedType = needType;
            PreviousValue = previousValue;
            NewValue = newValue;
            IsUrgent = isUrgent;
            Priority = isUrgent ? MessagePriority.High : MessagePriority.Normal;
        }
    }

    // Message sent when terrain generation is complete
    public class TerrainChunkGeneratedMessage : Message
    {
        public readonly Vector2Int ChunkCoord;
        public readonly Vector3 WorldPosition;
        public readonly TerrainDataComponent.BiomeType PrimaryBiome;

        public TerrainChunkGeneratedMessage(
            Entity source,
            Vector2Int chunkCoord,
            Vector3 worldPosition,
            TerrainDataComponent.BiomeType primaryBiome
        ) : base(source)
        {
            ChunkCoord = chunkCoord;
            WorldPosition = worldPosition;
            PrimaryBiome = primaryBiome;
            Priority = MessagePriority.Low;
        }
    }

    // Message sent when day/night cycle changes
    public class TimeChangedMessage : Message
    {
        public readonly float CurrentTime;
        public readonly int CurrentDay;
        public readonly TimeComponent.DayPeriod NewPeriod;
        public readonly TimeComponent.DayPeriod PreviousPeriod;

        public TimeChangedMessage(
            Entity source,
            float currentTime,
            int currentDay,
            TimeComponent.DayPeriod newPeriod,
            TimeComponent.DayPeriod previousPeriod
        ) : base(source)
        {
            CurrentTime = currentTime;
            CurrentDay = currentDay;
            NewPeriod = newPeriod;
            PreviousPeriod = previousPeriod;
            Priority = MessagePriority.Normal;
        }
    }
}
