using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class MLAgentComponent : ComponentBase
    {
        // Reference to the NPC component
        public NPCComponent npcComponent;
        
        // Flag to determine if ML-Agent is in control
        public bool isActive = true;
        
        // Last decision made by the ML-Agent (0=Idle, 1=Food, 2=Water, 3=Rest, 4=Social)
        public int lastAction;
        
        // Reward accumulation
        public float accumulatedReward = 0f;
        
        protected override void Awake()
        {
            base.Awake();
            
            // Get the NPC component
            npcComponent = GetComponent<NPCComponent>();
        }
        
        public void AddReward(float reward)
        {
            accumulatedReward += reward;
        }
        
        public void SetReward(float reward)
        {
            accumulatedReward = reward;
        }
        
        public void EndEpisode()
        {
            accumulatedReward = 0f;
        }
    }
}
