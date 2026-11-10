using UnityEngine;

namespace TLA.Gameplay.World
{
    /// <summary>
    /// A place where the player can appear when a scene loads, e.g. "FromEvidenceRoom" just
    /// outside the evidence room door in the hub.
    ///
    /// The blue arrow gizmo shows where the player will stand and face. Mark ONE spawn per scene
    /// as Default: it's used when starting a new game, loading a save, or when no specific
    /// spawn was requested.
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Unique name within this scene, referenced by scene doors (e.g. \"FromHub\").")]
        [SerializeField] private string spawnId = "Default";

        [Tooltip("Used when no specific spawn was requested.")]
        [SerializeField] private bool isDefault;

        public string SpawnId => spawnId;
        public bool IsDefault => isDefault;

        private void OnDrawGizmos()
        {
            Gizmos.color = isDefault ? new Color(0.3f, 0.8f, 1f) : new Color(0.3f, 0.5f, 1f);
            Vector3 feet = transform.position;
            Gizmos.DrawWireSphere(feet + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(feet, feet + Vector3.up * 1.8f);
            Gizmos.DrawLine(feet + Vector3.up * 1.6f, feet + Vector3.up * 1.6f + transform.forward * 0.8f);
        }
    }
}
