using UnityEngine;
using ECS.Core;
using ECS.Components;
using ECS.Core.Messaging;

namespace ECS.Systems
{
    public class TimeSystem : SystemBase
    {
        [Header("Time Settings")]
        [SerializeField] private float dayLengthInMinutes = 24f;
        [SerializeField] private float dawnDuskLengthInMinutes = 1f;
        
        private float currentTime;
        private int currentDay;
        private TimeComponent.DayPeriod currentPeriod;
        
        private float minutesPerSecond;
        private float dawnTime;
        private float dayTime;
        private float duskTime;
        private float nightTime;

        protected override void Initialize()
        {
            // Calculate time constants
            minutesPerSecond = 24f / (dayLengthInMinutes * 60f);
            
            // Set period times (in hours)
            dawnTime = 6f;  // 6 AM
            dayTime = dawnTime + dawnDuskLengthInMinutes / 60f;
            duskTime = 18f; // 6 PM
            nightTime = duskTime + dawnDuskLengthInMinutes / 60f;

            // Initialize time
            currentTime = dawnTime;
            currentDay = 1;
            currentPeriod = TimeComponent.DayPeriod.Dawn;

            Debug.Log($"TimeSystem initialized: Day {currentDay}, Time {currentTime:F1}, Period {currentPeriod}");
            base.Initialize();
        }

        public override void UpdateSystem()
        {
            if (!isInitialized)
            {
                Initialize();
                return;
            }

            // Update time
            currentTime += Time.deltaTime * minutesPerSecond;
            
            // Handle day rollover
            if (currentTime >= 24f)
            {
                currentTime -= 24f;
                currentDay++;
                Debug.Log($"New day started: Day {currentDay}");
            }

            // Update period
            UpdateDayPeriod();

            // Update all registered entities
            foreach (var entity in registeredEntities)
            {
                var timeComponent = entity.GetComponent<TimeComponent>();
                if (timeComponent != null)
                {
                    timeComponent.UpdateTime(currentTime, currentDay, currentPeriod);
                }
            }
        }

        private void UpdateDayPeriod()
        {
            TimeComponent.DayPeriod newPeriod = currentPeriod;

            if (currentTime >= dawnTime && currentTime < dayTime)
            {
                newPeriod = TimeComponent.DayPeriod.Dawn;
            }
            else if (currentTime >= dayTime && currentTime < duskTime)
            {
                newPeriod = TimeComponent.DayPeriod.Day;
            }
            else if (currentTime >= duskTime && currentTime < nightTime)
            {
                newPeriod = TimeComponent.DayPeriod.Dusk;
            }
            else
            {
                newPeriod = TimeComponent.DayPeriod.Night;
            }

            if (newPeriod != currentPeriod)
            {
                var oldPeriod = currentPeriod;
                currentPeriod = newPeriod;
                
                // Publish time changed message
                PublishMessage(new TimeChangedMessage(
                    null,
                    currentTime,
                    currentDay,
                    newPeriod,
                    oldPeriod
                ));
                
                OnPeriodChanged();
            }
        }

        protected virtual void OnPeriodChanged()
        {
            Debug.Log($"Day {currentDay}: Period changed to {currentPeriod} at {currentTime:F1}");
        }

        public override void RegisterEntity(Entity entity)
        {
            if (entity.HasComponent<TimeComponent>())
            {
                base.RegisterEntity(entity);
                
                // Initialize the time component
                var timeComponent = entity.GetComponent<TimeComponent>();
                timeComponent.UpdateTime(currentTime, currentDay, currentPeriod);
            }
        }

        // Public getters for other systems to access time information
        public float GetCurrentTime() => currentTime;
        public int GetCurrentDay() => currentDay;
        public TimeComponent.DayPeriod GetCurrentPeriod() => currentPeriod;
        public float GetDayProgress() => (currentTime - dawnTime) / (24f - dawnTime);
    }
}
