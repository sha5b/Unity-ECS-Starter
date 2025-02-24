using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class TimeComponent : ComponentBase
    {
        public float CurrentTime { get; private set; }
        public int CurrentDay { get; private set; }
        public DayPeriod CurrentPeriod { get; private set; }

        public enum DayPeriod
        {
            Dawn,
            Day,
            Dusk,
            Night
        }

        public void UpdateTime(float newTime, int day, DayPeriod period)
        {
            CurrentTime = newTime;
            CurrentDay = day;
            CurrentPeriod = period;
        }
    }
}
