using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class NPCAgent : MonoBehaviour
    {
        private NPCComponent npcComponent;
        private MLAgentComponent mlAgentComponent;
        private WorldManager worldManager;
        
        [SerializeField] private float decisionInterval = 0.5f;
        private float nextDecisionTime;
        
        private void Awake()
        {
            npcComponent = GetComponent<NPCComponent>();
            mlAgentComponent = GetComponent<MLAgentComponent>();
            worldManager = FindObjectOfType<WorldManager>();
            
            nextDecisionTime = Time.time + decisionInterval;
        }
        
        private void Update()
        {
            if (Time.time >= nextDecisionTime)
            {
                nextDecisionTime = Time.time + decisionInterval;
                MakeDecision();
            }
        }
        
        public void MakeDecision()
        {
            if (npcComponent == null || mlAgentComponent == null) return;
            
            // Simple heuristic based on NPC needs
            float highestNeed = 0f;
            int actionType = 0; // Default to idle
            
            if (npcComponent.needs.hunger > highestNeed)
            {
                highestNeed = npcComponent.needs.hunger;
                actionType = 1; // Seek food
            }
            
            if (npcComponent.needs.thirst > highestNeed)
            {
                highestNeed = npcComponent.needs.thirst;
                actionType = 2; // Seek water
            }
            
            if (npcComponent.needs.energy > highestNeed)
            {
                highestNeed = npcComponent.needs.energy;
                actionType = 3; // Seek rest
            }
            
            if (npcComponent.needs.social > highestNeed * npcComponent.personality.sociability)
            {
                highestNeed = npcComponent.needs.social;
                actionType = 4; // Seek social
            }
            
            // Add some randomness based on curiosity
            if (Random.value < npcComponent.personality.curiosity * 0.2f)
            {
                actionType = Random.Range(0, 5);
            }
            
            // Store the action
            mlAgentComponent.lastAction = actionType;
            
            // Calculate and apply rewards
            CalculateRewards();
        }
        
        private void CalculateRewards()
        {
            float reward = 0f;
            
            // Reward for satisfying needs
            if (npcComponent.needs.hunger < 0.3f) reward += 0.1f;
            if (npcComponent.needs.thirst < 0.3f) reward += 0.1f;
            if (npcComponent.needs.energy < 0.3f) reward += 0.1f;
            if (npcComponent.needs.social < 0.3f) reward += 0.1f;
            
            // Penalize for critical needs
            if (npcComponent.needs.hunger > 0.8f) reward -= 0.2f;
            if (npcComponent.needs.thirst > 0.8f) reward -= 0.2f;
            if (npcComponent.needs.energy > 0.8f) reward -= 0.2f;
            if (npcComponent.needs.social > 0.8f) reward -= 0.2f;
            
            // Apply the reward
            mlAgentComponent.AddReward(reward);
        }
    }
}
