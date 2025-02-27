using UnityEngine;
using ECS.Core;
using ECS.Core.Messaging;

namespace ECS.Systems
{
    /// <summary>
    /// Example system that demonstrates message subscription and handling
    /// </summary>
    public class MessageDebugSystem : SystemBase
    {
        protected override void Initialize()
        {
            // Subscribe to system initialization messages
            SubscribeToMessage<SystemInitializedMessage>(OnSystemInitialized);
            
            // Subscribe to time change messages
            SubscribeToMessage<TimeChangedMessage>(OnTimeChanged);
            
            base.Initialize();
        }

        private void OnSystemInitialized(SystemInitializedMessage message)
        {
            Debug.Log($"[MessageDebugSystem] System initialized: {message.SystemName}");
        }

        private void OnTimeChanged(TimeChangedMessage message)
        {
            Debug.Log($"[MessageDebugSystem] Time period changed from {message.PreviousPeriod} to {message.NewPeriod} on day {message.CurrentDay}");
        }

        protected override void OnDestroy()
        {
            // Unsubscribe from messages (although SystemBase.OnDestroy already handles this through MessageBus.ClearSubscriptions)
            if (MessageBus != null)
            {
                UnsubscribeFromMessage<SystemInitializedMessage>(OnSystemInitialized);
                UnsubscribeFromMessage<TimeChangedMessage>(OnTimeChanged);
            }
            
            base.OnDestroy();
        }
    }
}
