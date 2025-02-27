using UnityEngine;

namespace ECS.Components
{
    /// <summary>
    /// Helper component to set up ML-Agent components on an NPC.
    /// Attach this to any NPC GameObject to enable ML-Agent functionality.
    /// </summary>
    [RequireComponent(typeof(NPCComponent))]
    public class MLAgentSetup : MonoBehaviour
    {
        [Header("ML-Agent Configuration")]
        [Tooltip("Whether to use the ML-Agent for decision making")]
        public bool enableAgent = true;
        
        [Tooltip("Decision making interval in seconds")]
        public float decisionInterval = 0.5f;
        
        [Tooltip("Add randomness to decisions based on curiosity")]
        public bool useRandomness = true;
        
        private void Awake()
        {
            // Add required ML-Agent components
            SetupMLAgentComponents();
        }
        
        private void SetupMLAgentComponents()
        {
            // Add MLAgentComponent
            var mlAgentComponent = gameObject.AddComponent<MLAgentComponent>();
            mlAgentComponent.isActive = enableAgent;
            
            // Add NPCAgent
            var npcAgent = gameObject.AddComponent<NPCAgent>();
            
            // Configure NPCAgent
            if (npcAgent != null)
            {
                // Set decision interval using reflection since it's private
                var field = npcAgent.GetType().GetField("decisionInterval", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (field != null)
                {
                    field.SetValue(npcAgent, decisionInterval);
                }
            }
            
            Debug.Log($"[MLAgentSetup] ML-Agent components set up for {gameObject.name}");
        }
    }
}
