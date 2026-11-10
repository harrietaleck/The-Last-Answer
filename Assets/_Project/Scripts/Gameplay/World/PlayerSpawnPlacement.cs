using TLA.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TLA.Gameplay.World
{
    /// <summary>
    /// On the Player prefab. When the scene starts, moves the player to the requested
    /// <see cref="PlayerSpawnPoint"/> (set by the door you came through), or to the scene's
    /// default spawn. If the scene has no spawn points, the player stays where it was placed.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class PlayerSpawnPlacement : MonoBehaviour
    {
        private void Start()
        {
            string requested = SpawnRequest.Consume();
            PlayerSpawnPoint target = FindSpawn(requested);
            if (target == null) return;

            GetComponent<FirstPersonController>().Teleport(target.transform.position, target.transform.eulerAngles.y);
        }

        private PlayerSpawnPoint FindSpawn(string requestedId)
        {
            PlayerSpawnPoint fallback = null;
            Scene myScene = gameObject.scene;

            foreach (PlayerSpawnPoint point in FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None))
            {
                if (point.gameObject.scene != myScene) continue; // only spawns in the player's own scene

                if (!string.IsNullOrEmpty(requestedId) && point.SpawnId == requestedId) return point;
                // Fallback: the default spawn if there is one, otherwise the first spawn found.
                if (fallback == null || (point.IsDefault && !fallback.IsDefault)) fallback = point;
            }

            if (!string.IsNullOrEmpty(requestedId))
                Debug.LogWarning($"[Spawn] No spawn point '{requestedId}' in scene '{myScene.name}'. Using the default.");

            return fallback;
        }
    }
}
