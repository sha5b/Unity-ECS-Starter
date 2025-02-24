using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class ChunkStateComponent : ComponentBase
    {
        public enum LoadState
        {
            Unloaded,
            Loading,
            Active,
            UnloadPending
        }

        public enum EdgeDirection
        {
            North,
            South,
            East,
            West
        }

        [Header("Chunk Info")]
        public Vector2Int chunkCoord; // Position in chunk coordinates
        public int size = 32; // Size of the chunk in vertices
        public float scale = 1f; // World scale of the chunk

        [Header("State")]
        public LoadState currentState = LoadState.Unloaded;
        public int currentLOD = 0; // Level of detail (0 = highest)
        public float lastUpdateTime; // Time of last update

        [Header("Neighbors")] // Used for seamless LOD transitions
        public ChunkStateComponent northNeighbor;
        public ChunkStateComponent southNeighbor;
        public ChunkStateComponent eastNeighbor;
        public ChunkStateComponent westNeighbor;

        // Edge height data for smooth transitions
        private float[] northEdgeHeights;
        private float[] southEdgeHeights;
        private float[] eastEdgeHeights;
        private float[] westEdgeHeights;

        public Vector3 WorldPosition => new Vector3(chunkCoord.x * (size - 1) * scale, 0, chunkCoord.y * (size - 1) * scale);
        public Bounds WorldBounds => new Bounds(WorldPosition + new Vector3((size - 1) * scale * 0.5f, 0, (size - 1) * scale * 0.5f), 
                                              new Vector3((size - 1) * scale, 1000f, (size - 1) * scale));

        private TerrainDataComponent terrainData;

        public override void Initialize()
        {
            base.Initialize();
            terrainData = GetComponent<TerrainDataComponent>();
            if (terrainData == null)
            {
                terrainData = gameObject.AddComponent<TerrainDataComponent>();
            }
            terrainData.InitializeArrays(size);

            // Initialize edge height arrays
            northEdgeHeights = new float[size];
            southEdgeHeights = new float[size];
            eastEdgeHeights = new float[size];
            westEdgeHeights = new float[size];
            
            // Position the chunk in world space
            transform.position = WorldPosition;
        }

        public void UpdateState(Vector3 viewerPosition)
        {
            // Calculate distance to viewer
            float distance = Vector3.Distance(WorldPosition, viewerPosition);
            
            // Update LOD based on distance
            int newLOD = CalculateLOD(distance);
            if (newLOD != currentLOD)
            {
                currentLOD = newLOD;
                // Signal terrain system that LOD changed
                Debug.Log($"[ChunkState:{gameObject.name}] LOD changed to {currentLOD} at distance {distance:F1}");
            }

            // Update load state
            if (distance > size * scale * 3f && currentState == LoadState.Active)
            {
                currentState = LoadState.UnloadPending;
                Debug.Log($"[ChunkState:{gameObject.name}] Marked for unload at distance {distance:F1}");
            }
            else if (distance <= size * scale * 3f && currentState == LoadState.Unloaded)
            {
                currentState = LoadState.Loading;
                Debug.Log($"[ChunkState:{gameObject.name}] Marked for loading at distance {distance:F1}");
            }

            // Update edge heights from neighbors
            UpdateEdgeHeights();

            lastUpdateTime = Time.time;
        }

        private void UpdateEdgeHeights()
        {
            if (terrainData == null) return;

            // Get edge heights from neighbors
            if (northNeighbor != null && northNeighbor.terrainData != null)
            {
                float[] neighborEdge = northNeighbor.terrainData.GetEdge(EdgeDirection.South);
                if (neighborEdge != null) northEdgeHeights = neighborEdge;
            }

            if (southNeighbor != null && southNeighbor.terrainData != null)
            {
                float[] neighborEdge = southNeighbor.terrainData.GetEdge(EdgeDirection.North);
                if (neighborEdge != null) southEdgeHeights = neighborEdge;
            }

            if (eastNeighbor != null && eastNeighbor.terrainData != null)
            {
                float[] neighborEdge = eastNeighbor.terrainData.GetEdge(EdgeDirection.West);
                if (neighborEdge != null) eastEdgeHeights = neighborEdge;
            }

            if (westNeighbor != null && westNeighbor.terrainData != null)
            {
                float[] neighborEdge = westNeighbor.terrainData.GetEdge(EdgeDirection.East);
                if (neighborEdge != null) westEdgeHeights = neighborEdge;
            }

            // Update terrain data with new edge heights
            terrainData.SetEdgeData(northEdgeHeights, southEdgeHeights, eastEdgeHeights, westEdgeHeights);
        }

        private int CalculateLOD(float distance)
        {
            // Simple LOD calculation based on distance
            if (distance < size * scale) return 0; // Highest detail
            if (distance < size * scale * 2f) return 1;
            if (distance < size * scale * 4f) return 2;
            return 3; // Lowest detail
        }

        public void SetNeighbors(ChunkStateComponent north, ChunkStateComponent south, 
                               ChunkStateComponent east, ChunkStateComponent west)
        {
            northNeighbor = north;
            southNeighbor = south;
            eastNeighbor = east;
            westNeighbor = west;

            // Update edge heights when neighbors change
            UpdateEdgeHeights();

            Debug.Log($"[ChunkState:{gameObject.name}] Set neighbors N:{north?.gameObject.name} S:{south?.gameObject.name} E:{east?.gameObject.name} W:{west?.gameObject.name}");
        }

        public bool NeedsUpdate(float updateInterval = 0.5f)
        {
            return Time.time - lastUpdateTime > updateInterval;
        }

        public bool IsVisible(Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, WorldBounds);
        }
    }
}
