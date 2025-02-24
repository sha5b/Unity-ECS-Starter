using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class NPCComponent : ComponentBase
    {
        [System.Serializable]
        public class Needs
        {
            public float hunger;
            public float thirst;
            public float energy;
            public float social;

            public void Update(float deltaTime)
            {
                // Needs naturally increase over time
                hunger += deltaTime * 0.1f;
                thirst += deltaTime * 0.15f;
                energy += deltaTime * 0.05f;
                social += deltaTime * 0.08f;

                // Clamp values
                hunger = Mathf.Clamp01(hunger);
                thirst = Mathf.Clamp01(thirst);
                energy = Mathf.Clamp01(energy);
                social = Mathf.Clamp01(social);
            }
        }

        [System.Serializable]
        public class Personality
        {
            public float sociability = 0.5f;    // How much they seek social interaction
            public float bravery = 0.5f;        // Willingness to take risks
            public float curiosity = 0.5f;      // Tendency to explore
            public float diligence = 0.5f;      // Work ethic and task focus
        }

        public enum NPCState
        {
            Idle,
            Moving,
            Interacting,
            Working,
            Resting,
            Eating,
            Drinking,
            Socializing
        }

        [Header("NPC Settings")]
        public string npcName;
        public Needs needs = new Needs();
        public Personality personality = new Personality();
        public NPCState currentState = NPCState.Idle;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 120f;
        public float interactionRange = 2f;

        private Vector3 targetPosition;
        private Transform targetEntity;

        public void SetTarget(Vector3 position)
        {
            targetPosition = position;
            targetEntity = null;
            currentState = NPCState.Moving;
        }

        public void SetTarget(Transform entity)
        {
            targetEntity = entity;
            currentState = NPCState.Moving;
        }

        public Vector3 GetTargetPosition()
        {
            return targetEntity != null ? targetEntity.position : targetPosition;
        }

        public bool HasReachedTarget()
        {
            Vector3 target = GetTargetPosition();
            return Vector3.Distance(transform.position, target) < 0.1f;
        }

        public void SetState(NPCState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnStateChanged(newState);
            }
        }

        protected virtual void OnStateChanged(NPCState newState)
        {
            Debug.Log($"{npcName} changed state to {newState}");
        }
    }
}
