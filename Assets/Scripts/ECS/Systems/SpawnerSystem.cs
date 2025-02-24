using UnityEngine;
using ECS.Core;
using ECS.Components;
using System.Collections.Generic;

namespace ECS.Systems
{
    public class SpawnerSystem : SystemBase
    {
        [Header("Spawn Settings")]
        [SerializeField] private int initialNPCCount = 10;
        [SerializeField] private float spawnRadius = 20f;
        [SerializeField] private GameObject npcPrefab; // Optional: can be used for visual representation

        [Header("NPC Generation")]
        [SerializeField] private Vector2 speedRange = new Vector2(3f, 7f);
        [SerializeField] private Vector2 rotationSpeedRange = new Vector2(90f, 180f);
        [SerializeField] private Vector2 interactionRangeRange = new Vector2(1f, 3f);

        private ResourceSystem resourceSystem;
        private List<string> possibleNames = new List<string>
        {
            "Alex", "Blake", "Charlie", "Dana", "Eden", "Frankie", "Gray", "Harper",
            "Indigo", "Jordan", "Kennedy", "Logan", "Morgan", "Noah", "Parker", "Quinn",
            "Riley", "Sage", "Taylor", "Unity", "Val", "Winter", "Xen", "Yuri", "Zen"
        };

        protected override bool CheckDependencies()
        {
            return IsSystemReady<ResourceSystem>();
        }

        protected override void Initialize()
        {
            resourceSystem = GetSystem<ResourceSystem>();
            
            if (resourceSystem != null)
            {
                // Spawn initial NPCs
                for (int i = 0; i < initialNPCCount; i++)
                {
                    SpawnNPC();
                }

                Debug.Log($"Spawned {initialNPCCount} NPCs");
                base.Initialize();
            }
            else
            {
                Debug.LogError("SpawnerSystem initialization failed: ResourceSystem not found");
                enabled = false;
            }
        }

        public NPCComponent SpawnNPC()
        {
            // Create GameObject (either from prefab or new)
            GameObject npcObject;
            if (npcPrefab != null)
            {
                npcObject = GameObject.Instantiate(npcPrefab);
            }
            else
            {
                npcObject = new GameObject("NPC");
                // Add basic visual representation
                var renderer = npcObject.AddComponent<MeshRenderer>();
                var filter = npcObject.AddComponent<MeshFilter>();
                filter.mesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
            }

            // Set random position
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            npcObject.transform.position = new Vector3(randomCircle.x, 0, randomCircle.y);

            // Add required components
            var entity = npcObject.AddComponent<Entity>();
            var npc = npcObject.AddComponent<NPCComponent>();

            // Configure NPC
            npc.npcName = GetRandomName();
            
            // Set movement properties
            npc.moveSpeed = Random.Range(speedRange.x, speedRange.y);
            npc.rotationSpeed = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y);
            npc.interactionRange = Random.Range(interactionRangeRange.x, interactionRangeRange.y);

            // Randomize personality
            npc.personality.sociability = Random.value;
            npc.personality.bravery = Random.value;
            npc.personality.curiosity = Random.value;
            npc.personality.diligence = Random.value;

            // Initialize needs with random values
            npc.needs.hunger = Random.Range(0f, 0.3f);
            npc.needs.thirst = Random.Range(0f, 0.3f);
            npc.needs.energy = Random.Range(0f, 0.3f);
            npc.needs.social = Random.Range(0f, 0.3f);

            return npc;
        }

        private string GetRandomName()
        {
            if (possibleNames.Count == 0)
            {
                return "NPC_" + Random.Range(1000, 9999);
            }

            int index = Random.Range(0, possibleNames.Count);
            string name = possibleNames[index];
            possibleNames.RemoveAt(index); // Ensure unique names
            return name;
        }

        // Helper method to spawn an NPC at a specific position
        public NPCComponent SpawnNPCAt(Vector3 position)
        {
            var npc = SpawnNPC();
            npc.transform.position = position;
            return npc;
        }

        // Helper method to spawn a group of NPCs
        public void SpawnNPCGroup(int count, Vector3 centerPosition, float groupRadius)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * groupRadius;
                Vector3 position = centerPosition + new Vector3(randomOffset.x, 0, randomOffset.y);
                SpawnNPCAt(position);
            }
        }
    }
}
