using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class ResourceComponent : ComponentBase
    {
        public enum ResourceType
        {
            Food,
            Water,
            RestArea,
            WorkArea
        }

        [System.Serializable]
        public class ResourceProperties
        {
            public float quantity = 100f;           // How much resource is available
            public float replenishRate = 1f;        // Units per second when replenishing
            public float consumptionRate = 10f;     // Units per second when being consumed
            public float qualityMultiplier = 1f;    // How effective the resource is (1 = normal)
            public bool isInfinite = false;         // Whether the resource depletes
            public bool autoReplenish = true;       // Whether it automatically replenishes
            public float replenishDelay = 60f;      // Time to wait before replenishing
        }

        [Header("Resource Settings")]
        public ResourceType type;
        public ResourceProperties properties = new ResourceProperties();

        [Header("Status")]
        public bool isBeingUsed = false;
        public bool isDepleted = false;
        private float lastUsedTime;
        private float currentQuantity;

        protected override void Awake()
        {
            base.Awake();
            currentQuantity = properties.quantity;
        }

        public bool CanBeUsed()
        {
            return !isDepleted && (!isBeingUsed || type == ResourceType.RestArea);
        }

        public void StartUsing()
        {
            isBeingUsed = true;
            lastUsedTime = Time.time;
        }

        public void StopUsing()
        {
            isBeingUsed = false;
            lastUsedTime = Time.time;
        }

        public float Consume(float deltaTime)
        {
            if (properties.isInfinite)
            {
                return properties.consumptionRate * deltaTime * properties.qualityMultiplier;
            }

            float amount = Mathf.Min(
                properties.consumptionRate * deltaTime,
                currentQuantity
            ) * properties.qualityMultiplier;

            currentQuantity -= amount;
            
            if (currentQuantity <= 0)
            {
                currentQuantity = 0;
                isDepleted = true;
            }

            return amount;
        }

        public void Replenish(float deltaTime)
        {
            if (isDepleted || !properties.autoReplenish || properties.isInfinite)
                return;

            // Only replenish after delay
            if (Time.time - lastUsedTime < properties.replenishDelay)
                return;

            currentQuantity += properties.replenishRate * deltaTime;
            
            if (currentQuantity >= properties.quantity)
            {
                currentQuantity = properties.quantity;
                isDepleted = false;
            }
        }

        public float GetQuantityPercentage()
        {
            return properties.isInfinite ? 1f : currentQuantity / properties.quantity;
        }

        // Visual feedback
        private void OnDrawGizmos()
        {
            // Draw different colored spheres based on resource type
            switch (type)
            {
                case ResourceType.Food:
                    Gizmos.color = Color.green;
                    break;
                case ResourceType.Water:
                    Gizmos.color = Color.blue;
                    break;
                case ResourceType.RestArea:
                    Gizmos.color = Color.yellow;
                    break;
                case ResourceType.WorkArea:
                    Gizmos.color = Color.gray;
                    break;
            }

            // Fade color if depleted
            if (isDepleted)
            {
                Color c = Gizmos.color;
                c.a = 0.3f;
                Gizmos.color = c;
            }

            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}
