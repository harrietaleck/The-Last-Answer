using UnityEngine;

namespace TLA.Gameplay.Cinematics
{
    /// <summary>
    /// A saved camera angle, e.g. "Suspect close-up" or "Over the investigator's shoulder".
    /// Position and rotate this object like a camera. Select it to see its view cone in the
    /// Scene view; to frame it, align the Scene view and use GameObject → Align With View.
    ///
    /// <see cref="CinematicShotDirector"/> cuts between shots. Later, the dialogue system will
    /// pick shots by <see cref="ShotId"/> (e.g. a close-up when the suspect lies).
    /// </summary>
    public sealed class CinematicShot : MonoBehaviour
    {
        [Tooltip("Name used by code to request this shot, e.g. \"SuspectCloseUp\".")]
        [SerializeField] private string shotId = "Wide";

        [Tooltip("Vertical field of view. Lower = more zoomed in. ~60 wide, ~35 medium, ~22 close-up.")]
        [Range(10f, 90f)] [SerializeField] private float fieldOfView = 40f;

        [Tooltip("Slow forward drift (metres per second) for a subtle 'living camera' feel. 0 = locked off.")]
        [Range(0f, 0.1f)] [SerializeField] private float pushInSpeed = 0.015f;

        public string ShotId => shotId;
        public float FieldOfView => fieldOfView;
        public float PushInSpeed => pushInSpeed;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 3f, 0.1f, 16f / 9f);
        }
    }
}
