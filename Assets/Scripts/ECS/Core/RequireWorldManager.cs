using UnityEngine;

namespace ECS.Core
{
    /// <summary>
    /// Add this attribute to any MonoBehaviour that requires a WorldManager in the scene.
    /// It will automatically create one if none exists.
    /// </summary>
    public class RequireWorldManager : MonoBehaviour
    {
        protected virtual void Awake()
        {
            var worldManager = Object.FindFirstObjectByType<WorldManager>();
            if (worldManager == null)
            {
                var go = new GameObject("World Manager");
                worldManager = go.AddComponent<WorldManager>();
                Debug.Log("Created WorldManager automatically");
            }
        }
    }
}
