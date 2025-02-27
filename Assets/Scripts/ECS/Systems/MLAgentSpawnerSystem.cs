using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;

namespace ECS.Systems
{
    /// <summary>
    /// System that automatically adds ML-Agent components to all NPCs in the scene.
    /// </summary>
    public class MLAgentSpawnerSystem : SystemBase
    {
        [Header("ML-Agent Configuration")]
        [Tooltip("Whether to use the ML-Agent for decision making")]
        public bool enableAgent = true;
        
        [Tooltip("Decision making interval in seconds")]
        public float decisionInterval = 0.5f;
        
        [Tooltip("Add randomness to decisions based on curiosity")]
        public bool useRandomness = true;
        
        protected override bool CheckDependencies()
        {
            return IsSystemReady<NPCSystem>();
        }
        
        protected override void Initialize()
        {
            if (CheckDependencies())
            {
                base.Initialize();
                
                // Find all NPCs in the scene and add ML-Agent components to them
                AddMLAgentComponentsToAllNPCs();
            }
            else
            {
                Debug.LogError("MLAgentSpawnerSystem initialization failed: NPCSystem not found");
                enabled = false;
            }
        }
        
        private void AddMLAgentComponentsToAllNPCs()
        {
            // Find all NPCs in the scene
            var npcComponents = FindObjectsOfType<NPCComponent>();
            int count = 0;
            
            foreach (var npc in npcComponents)
            {
                // Check if the NPC already has ML-Agent components
                if (npc.GetComponent<MLAgentComponent>() == null)
                {
                    // Add MLAgentComponent
                    var mlAgentComponent = npc.gameObject.AddComponent<MLAgentComponent>();
                    mlAgentComponent.isActive = enableAgent;
                    
                    // Add NPCAgent
                    var npcAgent = npc.gameObject.AddComponent<NPCAgent>();
                    
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
                    
                    count++;
                }
            }
            
            Debug.Log($"[MLAgentSpawnerSystem] Added ML-Agent components to {count} NPCs");
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
            
            // Check for new NPCs periodically and add ML-Agent components to them
            var npcComponents = FindObjectsOfType<NPCComponent>();
            int count = 0;
            
            foreach (var npc in npcComponents)
            {
                // Check if the NPC already has ML-Agent components
                if (npc.GetComponent<MLAgentComponent>() == null)
                {
                    // Add MLAgentComponent
                    var mlAgentComponent = npc.gameObject.AddComponent<MLAgentComponent>();
                    mlAgentComponent.isActive = enableAgent;
                    
                    // Add NPCAgent
                    var npcAgent = npc.gameObject.AddComponent<NPCAgent>();
                    
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
                    
                    count++;
                }
            }
            
            if (count > 0)
            {
                Debug.Log($"[MLAgentSpawnerSystem] Added ML-Agent components to {count} new NPCs");
            }
        }
    }
}
