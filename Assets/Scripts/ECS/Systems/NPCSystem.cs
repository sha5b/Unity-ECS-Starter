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

        protected override bool CheckDependencies()
        {
            return IsSystemReady<TimeSystem>() && IsSystemReady<ResourceSystem>();
        }

        protected override void Initialize()
        {
            timeSystem = GetSystem<TimeSystem>();
            resourceSystem = GetSystem<ResourceSystem>();
            
            if (timeSystem != null && resourceSystem != null)
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
                    // Update needs based on time
                    npc.needs.Update(needCheckInterval);

                    // Check if any needs require attention
                    CheckAndAddressNeeds(npc);
                }
            }
        }

        private void CheckAndAddressNeeds(NPCComponent npc)
        {
            // Only check needs if not already addressing one
            if (npc.currentState == NPCComponent.NPCState.Idle)
            {
                // Check needs in priority order
                if (npc.needs.hunger > needThreshold)
                {
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.Food);
                }
                else if (npc.needs.thirst > needThreshold)
                {
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.Water);
                }
                else if (npc.needs.energy > needThreshold)
                {
                    FindAndSetResourceTarget(npc, ResourceComponent.ResourceType.RestArea);
                }
                else if (npc.needs.social > needThreshold * npc.personality.sociability)
                {
                    FindAndSetSocialTarget(npc);
                }
                else if (Random.value < npc.personality.curiosity * 0.1f)
                {
                    // Random exploration
                    Vector3 randomPoint = GetRandomPointInRange(npc.transform.position, 10f);
                    npc.SetTarget(randomPoint);
                    npc.SetState(NPCComponent.NPCState.Moving);
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
                // Move towards target
                Vector3 targetPos = npc.GetTargetPosition();
                Vector3 direction = (targetPos - npc.transform.position).normalized;
                
                // Rotate towards target
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                npc.transform.rotation = Quaternion.RotateTowards(
                    npc.transform.rotation,
                    targetRotation,
                    npc.rotationSpeed * Time.deltaTime
                );

                // Move forward
                npc.transform.position += npc.transform.forward * npc.moveSpeed * Time.deltaTime;
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

            // Find the resource we're interacting with
            var nearbyResources = resourceSystem.GetResourcesInRange(
                GetResourceTypeForState(npc.currentState),
                npc.transform.position,
                resourceInteractionRange
            );

            var resource = nearbyResources.FirstOrDefault();
            if (resource != null && resource.CanBeUsed())
            {
                // Consume the resource
                float consumed = resource.Consume(Time.deltaTime);

                // Update NPC needs based on consumption
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
                // Lost access to resource
                npc.SetState(NPCComponent.NPCState.Idle);
            }
        }

        private void UpdateSocializing(NPCComponent npc)
        {
            // Find the NPC we're socializing with
            var nearbyNPCs = registeredEntities
                .Where(e => e.GetComponent<NPCComponent>() != null && e != npc.GetEntity())
                .Select(e => e.GetComponent<NPCComponent>())
                .Where(other => Vector3.Distance(npc.transform.position, other.transform.position) <= socialInteractionRange);

            var socialPartner = nearbyNPCs.FirstOrDefault();
            if (socialPartner != null)
            {
                // Update social needs
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
                // Lost social partner
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

            // Check what kind of target we reached
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
                // Start using the resource
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
                // No resource found, return to idle
                npc.SetState(NPCComponent.NPCState.Idle);
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
                // No resource found, explore randomly
                Vector3 randomPoint = GetRandomPointInRange(npc.transform.position, 10f);
                npc.SetTarget(randomPoint);
                npc.SetState(NPCComponent.NPCState.Moving);
            }
        }

        private void FindAndSetSocialTarget(NPCComponent npc)
        {
            // Find nearest NPC for social interaction
            var nearbyNPCs = registeredEntities
                .Where(e => e.GetComponent<NPCComponent>() != null && e != npc.GetEntity())
                .Select(e => e.GetComponent<NPCComponent>())
                .Where(other => Vector3.Distance(npc.transform.position, other.transform.position) <= socialInteractionRange)
                .OrderBy(other => Vector3.Distance(npc.transform.position, other.transform.position));

            var target = nearbyNPCs.FirstOrDefault();
            if (target != null)
            {
                npc.SetTarget(target.transform);
                npc.SetState(NPCComponent.NPCState.Socializing);
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
                    return ResourceComponent.ResourceType.Food; // Default case
            }
        }

        private Vector3 GetRandomPointInRange(Vector3 center, float range)
        {
            Vector2 random2D = Random.insideUnitCircle * range;
            return center + new Vector3(random2D.x, 0, random2D.y);
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
