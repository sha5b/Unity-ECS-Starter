using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;
using System.Linq;

namespace ECS.Systems
{
    public class MLAgentSystem : SystemBase
    {
        [Header("ML-Agent Settings")]
        [SerializeField] private bool enableTraining = true;
        [SerializeField] private float decisionInterval = 0.5f;
        
        private NPCSystem npcSystem;
        private ResourceSystem resourceSystem;
        private TerrainSystem terrainSystem;
        
        private float nextDecisionTime;
        
        protected override bool CheckDependencies()
        {
            return IsSystemReady<NPCSystem>() && IsSystemReady<ResourceSystem>() && IsSystemReady<TerrainSystem>();
        }
        
        protected override void Initialize()
        {
            npcSystem = GetSystem<NPCSystem>();
            resourceSystem = GetSystem<ResourceSystem>();
            terrainSystem = GetSystem<TerrainSystem>();
            
            if (npcSystem != null && resourceSystem != null && terrainSystem != null)
            {
                nextDecisionTime = Time.time + decisionInterval;
                base.Initialize();
            }
            else
            {
                Debug.LogError("MLAgentSystem initialization failed: Required systems not found");
                enabled = false;
            }
        }
        
        public override void UpdateSystem()
        {
            if (!isInitialized)
            {
                if (CheckDependencies())
                {
                    Initialize();
                }
                return;
            }
            
            float currentTime = Time.time;
            
            // Process ML-Agent decisions at regular intervals
            if (currentTime >= nextDecisionTime)
            {
                nextDecisionTime = currentTime + decisionInterval;
                
                // Process ML-Agent decisions for each entity
                foreach (var entity in registeredEntities)
                {
                    var mlAgentComponent = entity.GetComponent<MLAgentComponent>();
                    if (mlAgentComponent != null && mlAgentComponent.isActive)
                    {
                        ProcessAgentDecision(mlAgentComponent);
                    }
                }
            }
        }
        
        private void ProcessAgentDecision(MLAgentComponent mlAgentComponent)
        {
            // Get the NPC component
            var npc = mlAgentComponent.npcComponent;
            if (npc == null) return;
            
            // Get the last action from the ML-Agent
            int actionType = mlAgentComponent.lastAction;
            
            // For the proof of concept, we'll implement a simple decision-making process
            // In a more comprehensive implementation, we would interpret the actions more sophisticatedly
            
            // Example: Discrete actions
            // Action: 0 = Idle, 1 = Seek Food, 2 = Seek Water, 3 = Seek Rest, 4 = Seek Social
            switch (actionType)
            {
                case 0: // Idle
                    npc.SetState(NPCComponent.NPCState.Idle);
                    break;
                case 1: // Seek Food
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.Food);
                    break;
                case 2: // Seek Water
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.Water);
                    break;
                case 3: // Seek Rest
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.RestArea);
                    break;
                case 4: // Seek Social
                    FindAndSetSocialTarget(npc);
                    break;
            }
        }
        
        private void FindAndSetResourceTarget(NPCComponent npc, ResourceComponent.ResourceType resourceType)
        {
            if (resourceSystem == null) return;
            
            var resource = resourceSystem.FindBestResource(resourceType, npc.transform.position);
            
            if (resource != null && resource.CanBeUsed())
            {
                npc.SetTarget(resource.transform);
                npc.SetState(NPCComponent.NPCState.Moving);
            }
            else
            {
                // If no resource found, move to a random point
                Vector3 randomPoint = GetRandomPointInRange(npc.transform.position, 10f);
                npc.SetTarget(randomPoint);
                npc.SetState(NPCComponent.NPCState.Moving);
            }
        }
        
        private void FindAndSetSocialTarget(NPCComponent npc)
        {
            var nearbyNPCs = registeredEntities
                .Where(e => e.GetComponent<NPCComponent>() != null && e != npc.GetEntity())
                .Select(e => e.GetComponent<NPCComponent>())
                .Where(other => Vector3.Distance(npc.transform.position, other.transform.position) <= 10f)
                .OrderBy(other => Vector3.Distance(npc.transform.position, other.transform.position));

            var target = nearbyNPCs.FirstOrDefault();
            if (target != null)
            {
                npc.SetTarget(target.transform);
                npc.SetState(NPCComponent.NPCState.Socializing);
            }
            else
            {
                // If no NPC found, move to a random point
                Vector3 randomPoint = GetRandomPointInRange(npc.transform.position, 10f);
                npc.SetTarget(randomPoint);
                npc.SetState(NPCComponent.NPCState.Moving);
            }
        }
        
        private Vector3 GetRandomPointInRange(Vector3 center, float range)
        {
            Vector2 random2D = Random.insideUnitCircle * range;
            Vector3 randomPoint = center + new Vector3(random2D.x, 0, random2D.y);
            
            // Get terrain height at random point
            if (terrainSystem != null)
            {
                randomPoint.y = terrainSystem.GetHeightAt(randomPoint);
            }
            
            return randomPoint;
        }
        
        public override void RegisterEntity(Entity entity)
        {
            if (entity.HasComponent<MLAgentComponent>())
            {
                base.RegisterEntity(entity);
            }
        }
    }
}
