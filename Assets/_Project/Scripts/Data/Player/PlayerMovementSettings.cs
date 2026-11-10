using UnityEngine;

namespace TLA.Data.Player
{
    /// <summary>
    /// Every tunable number for the first-person player, in ONE asset:
    /// Assets/_Project/Data/Settings/PlayerMovementSettings.asset
    ///
    /// Select the asset and change values in the Inspector, even during Play Mode: changes apply
    /// immediately, and unlike component values they are KEPT when you stop playing.
    ///
    /// Defaults are tuned for a slow, deliberate investigation game, not a shooter.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "The Last Answer/Config/Player Movement Settings", order = 10)]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        public enum CrouchMode
        {
            /// <summary>Press once to crouch, again to stand.</summary>
            Toggle = 0,

            /// <summary>Crouch only while the key is held.</summary>
            Hold = 1
        }

        [Header("Movement speeds (metres per second)")]
        [Tooltip("Normal walking speed. ~1.4 m/s is a relaxed real-world walk.")]
        [Min(0f)] public float walkSpeed = 2.2f;

        [Tooltip("Speed while holding Run (Left Shift).")]
        [Min(0f)] public float runSpeed = 4.2f;

        [Tooltip("Speed while crouched.")]
        [Min(0f)] public float crouchSpeed = 1.2f;

        [Header("Smoothing")]
        [Tooltip("Speed gained per second (m/s²) when starting to move. Higher = snappier. 12 reaches walking speed in ~0.2 s.")]
        [Min(0.1f)] public float acceleration = 12f;

        [Tooltip("Speed lost per second (m/s²) after releasing the keys. Higher = less sliding.")]
        [Min(0.1f)] public float deceleration = 14f;

        [Tooltip("Downward acceleration (m/s²). Real gravity is 9.81; slightly more feels grounded.")]
        [Min(0f)] public float gravity = 15f;

        [Header("Body and camera height (metres)")]
        [Tooltip("Height of the player's collision capsule when standing.")]
        [Min(0.5f)] public float standingHeight = 1.8f;

        [Tooltip("Height of the collision capsule when crouched. Fits under ~1.2 m obstacles.")]
        [Min(0.5f)] public float crouchHeight = 1.1f;

        [Tooltip("Distance from the top of the capsule down to the eyes. Camera height = capsule height - this.")]
        [Min(0f)] public float eyeOffsetFromTop = 0.12f;

        [Tooltip("How quickly the body and camera move between standing and crouching.")]
        [Min(0.1f)] public float crouchTransitionSpeed = 8f;

        public CrouchMode crouchMode = CrouchMode.Toggle;

        [Header("Mouse look")]
        [Tooltip("Degrees the camera turns per pixel of mouse movement. The player's Mouse Sensitivity setting multiplies this.")]
        [Range(0.01f, 1f)] public float lookSensitivity = 0.12f;

        [Tooltip("Invert vertical mouse look.")]
        public bool invertY = false;

        [Tooltip("How far up/down the player can look, in degrees.")]
        [Range(30f, 89f)] public float maxLookAngle = 85f;

        [Header("Interaction")]
        [Tooltip("How far away (metres, from the eyes) the player can interact with objects.")]
        [Min(0.1f)] public float interactionDistance = 2.5f;

        [Tooltip("Radius of the interaction 'aim' sphere. A little above 0 makes small objects easier to target.")]
        [Range(0f, 0.3f)] public float interactionRadius = 0.05f;

        [Tooltip("Which physics layers can block or be targeted by the interaction ray.")]
        public LayerMask interactionLayers = ~0;

        /// <summary>Camera height above the player's feet for a given capsule height.</summary>
        public float EyeHeightFor(float capsuleHeight)
        {
            return Mathf.Max(0.1f, capsuleHeight - eyeOffsetFromTop);
        }

        private void OnValidate()
        {
            // Keep values consistent so the controller can't end up in an impossible state.
            crouchHeight = Mathf.Min(crouchHeight, standingHeight);
            runSpeed = Mathf.Max(runSpeed, walkSpeed);
        }
    }
}
