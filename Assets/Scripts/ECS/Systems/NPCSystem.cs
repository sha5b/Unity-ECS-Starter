using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;
using System.Linq;

namespace ECS.Systems
{
    public class NPCSystem : SystemBase
    {
        [Header("NPC Settings")]
        [SerializeField] private float needCheckInterval = 1f;
        [SerializeField] private float socialInteractionRange = 5f;
        [SerializeField] private float needThreshold = 0.7f; // When needs become urgent
        [SerializeField] private float resourceInteractionRange = 1f; // How close NPC needs to be to interact

        private float nextNeedCheck;
        private TimeSystem timeSystem;
        private ResourceSystem resourceSystem;
        private TerrainSystem terrainSystem;

        protected override bool CheckDependencies()
        {
            return IsSystemReady<TimeSystem>() && IsSystemReady<ResourceSystem>() && IsSystemReady<TerrainSystem>();
        }

        protected override void Initialize()
        {
            timeSystem = GetSystem<TimeSystem>();
            resourceSystem = GetSystem<ResourceSystem>();
            terrainSystem = GetSystem<TerrainSystem>();
            
            if (timeSystem != null && resourceSystem != null && terrainSystem != null)
            {
                nextNeedCheck = Time.time + needCheckInterval;
                base.Initialize();
            }
            else
            {
                Debug.LogError("NPCSystem initialization failed: Required systems not found");
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

            // Update needs and check for actions periodically
            if (currentTime >= nextNeedCheck)
            {
                nextNeedCheck = currentTime + needCheckInterval;
                UpdateAllNPCNeeds();
            }

            // Update NPC movement and actions every frame
            foreach (var entity in registeredEntities)
            {
                var npc = entity.GetComponent<NPCComponent>();
                if (npc != null)
                {
                    UpdateNPCBehavior(npc);
                }
            }
        }

        private void UpdateAllNPCNeeds()
        {
            foreach (var entity in registeredEntities)
            {
                var npc = entity.GetComponent<NPCComponent>();
                if (npc != null)
                {
                    // Update needs based on time and current biome
                    UpdateNeedsBasedOnBiome(npc);

                    // Check if any needs require attention
                    CheckAndAddressNeeds(npc);
                }
            }
        }

        private void UpdateNeedsBasedOnBiome(NPCComponent npc)
        {
            var biome = terrainSystem.GetBiomeAt(npc.transform.position);
            float deltaTime = needCheckInterval;

            // Base need changes
            npc.needs.Update(deltaTime);

            // Biome-specific modifiers
            switch (biome)
            {
                case TerrainDataComponent.BiomeType.Desert:
                    npc.needs.thirst += deltaTime * 0.2f; // Faster thirst in desert
                    npc.needs.energy += deltaTime * 0.1f; // Tire faster in desert
                    break;

                case TerrainDataComponent.BiomeType.Forest:
                    npc.needs.hunger -= deltaTime * 0.05f; // Easier to find food in forest
                    break;

                case TerrainDataComponent.BiomeType.Mountains:
                    npc.needs.energy += deltaTime * 0.15f; // Tire faster in mountains
                    break;
            }
        }

        private void CheckAndAddressNeeds(NPCComponent npc)
        {
            Debug.Log($"[NPCSystem] Checking needs for {npc.gameObject.name}. Current State: {npc.currentState}");
            if (npc.currentState == NPCComponent.NPCState.Idle)
            {
                Debug.Log($"[NPCSystem] {npc.gameObject.name} Needs - Hunger: {npc.needs.hunger:F2}, Thirst: {npc.needs.thirst:F2}, Energy: {npc.needs.energy:F2}, Social: {npc.needs.social:F2} (Threshold: {needThreshold:F2}, Social Threshold: {needThreshold * npc.personality.sociability:F2})");
                if (npc.needs.hunger > needThreshold)
                {
                    Debug.Log($"[NPCSystem] {npc.gameObject.name} is hungry. Finding food...");
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.Food);
                }
                else if (npc.needs.thirst > needThreshold)
                {
                    Debug.Log($"[NPCSystem] {npc.gameObject.name} is thirsty. Finding water...");
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.Water);
                }
                else if (npc.needs.energy > needThreshold)
                {
                    Debug.Log($"[NPCSystem] {npc.gameObject.name} is tired. Finding rest area...");
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.RestArea);
                }
                else if (npc.needs.social > needThreshold * npc.personality.sociability)
                {
                    Debug.Log($"[NPCSystem] {npc.gameObject.name} is lonely. Finding friend...");
                    FindAndSetSocialTarget(npc);
                }
                else 
                {
                    float curiosityCheck = Random.value;
                    float curiosityThreshold = npc.personality.curiosity * 0.1f;
                    Debug.Log($"[NPCSystem] {npc.gameObject.name} Curiosity Check: RandomValue={curiosityCheck:F2}, Threshold={curiosityThreshold:F2} (Curiosity={npc.personality.curiosity:F2})");
                    if (curiosityCheck < curiosityThreshold)
                    {
                        Debug.Log($"[NPCSystem] {npc.gameObject.name} is curious. Moving randomly...");
                        Vector3 randomPoint = GetRandomPointInRange(npc.transform.position, 10f);
                        npc.SetTarget(randomPoint);
                        npc.SetState(NPCComponent.NPCState.Moving);
                    }
                    else
                    {
                        Debug.Log($"[NPCSystem] {npc.gameObject.name} has no urgent needs and is not curious right now. Staying Idle.");
                    }
                }
            }
        }

        private void UpdateNPCBehavior(NPCComponent npc)
        {
            switch (npc.currentState)
            {
                case NPCComponent.NPCState.Moving:
                    UpdateMovement(npc);
                    break;

                case NPCComponent.NPCState.Interacting:
                    UpdateInteraction(npc);
                    break;

                case NPCComponent.NPCState.Socializing:
                    UpdateSocializing(npc);
                    break;

                case NPCComponent.NPCState.Eating:
                case NPCComponent.NPCState.Drinking:
                case NPCComponent.NPCState.Resting:
                    UpdateResourceConsumption(npc);
                    break;
            }
        }

        private void UpdateMovement(NPCComponent npc)
        {
            if (npc.HasReachedTarget())
            {
                HandleTargetReached(npc);
            }
            else
            {
                Vector3 targetPos = npc.GetTargetPosition();
                Vector3 currentPos = npc.transform.position;
                
                // Get terrain height at target position
                float targetHeight = terrainSystem.GetHeightAt(targetPos);
                targetPos.y = targetHeight;

                // Calculate direction considering terrain height
                Vector3 direction = (targetPos - currentPos).normalized;
                
                // Rotate towards target
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                npc.transform.rotation = Quaternion.RotateTowards(
                    npc.transform.rotation,
                    targetRotation,
                    npc.rotationSpeed * Time.deltaTime
                );

                // Move forward and adjust height
                Vector3 newPosition = currentPos + npc.transform.forward * npc.moveSpeed * Time.deltaTime;
                float calculatedHeight = terrainSystem.GetHeightAt(newPosition);
                Debug.Log($"[NPCSystem] NPC {npc.gameObject.name}: Moving from {currentPos} towards {targetPos}. NewPosXZ=({newPosition.x}, {newPosition.z}), CalculatedHeight={calculatedHeight}");
                newPosition.y = calculatedHeight;
                npc.transform.position = newPosition;
            }
        }

        private void UpdateInteraction(NPCComponent npc)
        {
            // Generic interaction handling
            // Specific interactions are handled in UpdateResourceConsumption and UpdateSocializing
        }

        private void UpdateResourceConsumption(NPCComponent npc)
        {
            if (resourceSystem == null) return;

            var nearbyResources = resourceSystem.GetResourcesInRange(
                GetResourceTypeForState(npc.currentState),
                npc.transform.position,
                resourceInteractionRange
            );

            var resource = nearbyResources.FirstOrDefault();
            if (resource != null && resource.CanBeUsed())
            {
                float consumed = resource.Consume(Time.deltaTime);

                switch (npc.currentState)
                {
                    case NPCComponent.NPCState.Eating:
                        npc.needs.hunger = Mathf.Max(0, npc.needs.hunger - consumed * 0.1f);
                        if (npc.needs.hunger <= 0.1f)
                        {
                            npc.SetState(NPCComponent.NPCState.Idle);
                            resource.StopUsing();
                        }
                        break;

                    case NPCComponent.NPCState.Drinking:
                        npc.needs.thirst = Mathf.Max(0, npc.needs.thirst - consumed * 0.1f);
                        if (npc.needs.thirst <= 0.1f)
                        {
                            npc.SetState(NPCComponent.NPCState.Idle);
                            resource.StopUsing();
                        }
                        break;

                    case NPCComponent.NPCState.Resting:
                        npc.needs.energy = Mathf.Max(0, npc.needs.energy - consumed * 0.1f);
                        if (npc.needs.energy <= 0.1f)
                        {
                            npc.SetState(NPCComponent.NPCState.Idle);
                            resource.StopUsing();
                        }
                        break;
                }
            }
            else
            {
                npc.SetState(NPCComponent.NPCState.Idle);
            }
        }

        private void UpdateSocializing(NPCComponent npc)
        {
            var nearbyNPCs = registeredEntities
                .Where(e => e.GetComponent<NPCComponent>() != null && e != npc.GetEntity())
                .Select(e => e.GetComponent<NPCComponent>())
                .Where(other => Vector3.Distance(npc.transform.position, other.transform.position) <= socialInteractionRange);

            var socialPartner = nearbyNPCs.FirstOrDefault();
            if (socialPartner != null)
            {
                float socialRate = Time.deltaTime * 0.1f;
                npc.needs.social = Mathf.Max(0, npc.needs.social - socialRate);
                socialPartner.needs.social = Mathf.Max(0, socialPartner.needs.social - socialRate);

                if (npc.needs.social <= 0.1f)
                {
                    npc.SetState(NPCComponent.NPCState.Idle);
                }
            }
            else
            {
                npc.SetState(NPCComponent.NPCState.Idle);
            }
        }

        private void HandleTargetReached(NPCComponent npc)
        {
            if (resourceSystem == null)
            {
                npc.SetState(NPCComponent.NPCState.Idle);
                return;
            }

            var nearbyResources = resourceSystem.GetResourcesInRange(
                ResourceComponent.ResourceType.Food,
                npc.transform.position,
                resourceInteractionRange
            ).Concat(
                resourceSystem.GetResourcesInRange(
                    ResourceComponent.ResourceType.Water,
                    npc.transform.position,
                    resourceInteractionRange
                )
            ).Concat(
                resourceSystem.GetResourcesInRange(
                    ResourceComponent.ResourceType.RestArea,
                    npc.transform.position,
                    resourceInteractionRange
                )
            );

            var resource = nearbyResources.FirstOrDefault();
            if (resource != null && resource.CanBeUsed())
            {
                resource.StartUsing();
                switch (resource.type)
                {
                    case ResourceComponent.ResourceType.Food:
                        npc.SetState(NPCComponent.NPCState.Eating);
                        break;
                    case ResourceComponent.ResourceType.Water:
                        npc.SetState(NPCComponent.NPCState.Drinking);
                        break;
                    case ResourceComponent.ResourceType.RestArea:
                        npc.SetState(NPCComponent.NPCState.Resting);
                        break;
                }
            }
            else
            {
                npc.SetState(NPCComponent.NPCState.Idle);
            }
        }

        private void FindAndSetResourceTarget(NPCComponent npc, ResourceComponent.ResourceType resourceType)
        {
            if (resourceSystem == null) return;

            var resource = resourceSystem.FindBestResource(resourceType, npc.transform.position);
            
            if (resource != null && resource.CanBeUsed())
            {
                Debug.Log($"[NPCSystem] {npc.gameObject.name} found resource {resource.name} of type {resourceType} at {resource.transform.position}. Setting state to Moving.");
                npc.SetTarget(resource.transform);
                npc.SetState(NPCComponent.NPCState.Moving);
            }
            else
            {
                Debug.Log($"[NPCSystem] {npc.gameObject.name} could not find usable resource of type {resourceType}. Moving randomly instead.");
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
                .Where(other => Vector3.Distance(npc.transform.position, other.transform.position) <= socialInteractionRange)
                .OrderBy(other => Vector3.Distance(npc.transform.position, other.transform.position));

            var target = nearbyNPCs.FirstOrDefault();
            if (target != null)
            {
                Debug.Log($"[NPCSystem] {npc.gameObject.name} found social target {target.gameObject.name} at {target.transform.position}. Setting state to Socializing.");
                npc.SetTarget(target.transform);
                npc.SetState(NPCComponent.NPCState.Socializing);
            }
            else
            {
                 Debug.Log($"[NPCSystem] {npc.gameObject.name} could not find a social target nearby.");
            }
        }

        private ResourceComponent.ResourceType GetResourceTypeForState(NPCComponent.NPCState state)
        {
            switch (state)
            {
                case NPCComponent.NPCState.Eating:
                    return ResourceComponent.ResourceType.Food;
                case NPCComponent.NPCState.Drinking:
                    return ResourceComponent.ResourceType.Water;
                case NPCComponent.NPCState.Resting:
                    return ResourceComponent.ResourceType.RestArea;
                default:
                    return ResourceComponent.ResourceType.Food;
            }
        }

        private Vector3 GetRandomPointInRange(Vector3 center, float range)
        {
            Vector2 random2D = Random.insideUnitCircle * range;
            Vector3 randomPoint = center + new Vector3(random2D.x, 0, random2D.y);
            
            // Get terrain height at random point
            randomPoint.y = terrainSystem.GetHeightAt(randomPoint);
            
            return randomPoint;
        }

        public override void RegisterEntity(Entity entity)
        {
            if (entity.HasComponent<NPCComponent>())
            {
                base.RegisterEntity(entity);
            }
        }
    }
}
