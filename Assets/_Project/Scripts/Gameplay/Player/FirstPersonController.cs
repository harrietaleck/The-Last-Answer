using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Player;
using TLA.Systems;
using TLA.Systems.Config;
using UnityEngine;

namespace TLA.Gameplay.Player
{
    /// <summary>
    /// First-person movement and mouse look, built on Unity's CharacterController.
    ///
    /// WHY CharacterController (not a Rigidbody): it gives precise, non-floaty movement with
    /// built-in collision, sliding along walls, walking up steps and slopes, with no physics
    /// surprises such as being pushed by objects. That suits a slow, deliberate investigation game.
    ///
    /// Features: walk, run (hold Shift), crouch (toggle or hold, with a ceiling check so you can't
    /// stand up inside a table), smooth acceleration, gravity. There's deliberately no jump:
    /// investigators don't need to platform, and it keeps players out of places a scene
    /// wasn't built for.
    ///
    /// HIERARCHY this script expects:
    ///   Player             (CharacterController, FirstPersonController, PlayerInputReader, PlayerInteractor)
    ///   └── CameraPivot    (moved up/down for crouching; rotated for looking up/down)
    ///       └── Main Camera
    /// The Player's position is at its FEET. Turning left/right rotates the whole Player.
    ///
    /// All numbers come from the PlayerMovementSettings asset.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonController : MonoBehaviour
    {
        // Small constant downward speed while grounded, so the controller stays snapped to
        // slopes and stairs instead of "floating" off them for a frame.
        private const float GroundedStickSpeed = -2f;

        [SerializeField] private PlayerMovementSettings settings;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Child transform at eye height that holds the camera.")]
        [SerializeField] private Transform cameraPivot;

        private readonly RaycastHit[] _headroomHits = new RaycastHit[8];

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private float _currentHeight;
        private bool _wantsCrouch;
        private float _userSensitivity = 1f;

        public PlayerMovementSettings Settings => settings;

        /// <summary>True while crouched or still standing up from a crouch.</summary>
        public bool IsCrouching { get; private set; }

        public bool IsRunning { get; private set; }

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>Current horizontal speed in m/s (useful later for footsteps / head-bob).</summary>
        public float HorizontalSpeed => _horizontalVelocity.magnitude;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (settings == null || input == null || cameraPivot == null)
            {
                Debug.LogError("[FirstPerson] Settings, Input or Camera Pivot is not assigned.", this);
                enabled = false;
                return;
            }

            _currentHeight = settings.standingHeight;
            ApplyHeight(_currentHeight);
            _pitch = NormalizeAngle(cameraPivot.localEulerAngles.x);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UserSettingsChangedEvent>(OnUserSettingsChanged);
            RefreshUserSensitivity();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UserSettingsChangedEvent>(OnUserSettingsChanged);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return; // paused (timeScale 0)

            UpdateLook();
            UpdateCrouch(deltaTime);
            UpdateMovement(deltaTime);
        }

        /// <summary>
        /// Instantly moves the player, e.g. to a spawn point. Use this instead of setting
        /// transform.position: the CharacterController would otherwise snap the player back.
        /// </summary>
        public void Teleport(Vector3 feetPosition, float yawDegrees)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, yawDegrees, 0f));
            _controller.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        // ------------------------------------------------------------------ look

        private void UpdateLook()
        {
            // Mouse delta is already "per frame", so it must NOT be multiplied by deltaTime.
            Vector2 delta = input.Look * (settings.lookSensitivity * _userSensitivity);
            if (delta == Vector2.zero) return;

            transform.Rotate(0f, delta.x, 0f, Space.Self);               // turn body left/right

            _pitch += settings.invertY ? delta.y : -delta.y;              // look up/down
            _pitch = Mathf.Clamp(_pitch, -settings.maxLookAngle, settings.maxLookAngle);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // ------------------------------------------------------------------ crouch

        private void UpdateCrouch(float deltaTime)
        {
            if (settings.crouchMode == PlayerMovementSettings.CrouchMode.Toggle)
            {
                if (input.CrouchPressedThisFrame) _wantsCrouch = !_wantsCrouch;
            }
            else
            {
                _wantsCrouch = input.CrouchHeld;
            }

            float targetHeight = _wantsCrouch ? settings.crouchHeight : settings.standingHeight;

            // Don't stand up into a ceiling or table: stay low until there's room.
            if (targetHeight > _currentHeight && !HasHeadroom(targetHeight))
                targetHeight = _currentHeight;

            if (!Mathf.Approximately(_currentHeight, targetHeight))
            {
                // Full stand<->crouch transition takes 1 / crouchTransitionSpeed seconds.
                float range = Mathf.Max(0.01f, settings.standingHeight - settings.crouchHeight);
                _currentHeight = Mathf.MoveTowards(_currentHeight, targetHeight,
                    range * settings.crouchTransitionSpeed * deltaTime);
                ApplyHeight(_currentHeight);
            }

            IsCrouching = _currentHeight < settings.standingHeight - 0.01f;
        }

        /// <summary>Checks whether the space above the head is free to grow to <paramref name="targetHeight"/>.</summary>
        private bool HasHeadroom(float targetHeight)
        {
            float radius = _controller.radius * 0.95f;
            Vector3 headCentre = transform.position + Vector3.up * (_currentHeight - _controller.radius);
            float distance = targetHeight - _currentHeight + _controller.skinWidth;

            int count = Physics.SphereCastNonAlloc(headCentre, radius, Vector3.up, _headroomHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            // Ignore the player's own colliders; anything else above the head blocks standing up.
            for (int i = 0; i < count; i++)
            {
                if (!_headroomHits[i].collider.transform.IsChildOf(transform)) return false;
            }

            return true;
        }

        /// <summary>Resizes the capsule (feet stay on the ground) and moves the camera to eye height.</summary>
        private void ApplyHeight(float height)
        {
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
            cameraPivot.localPosition = new Vector3(0f, settings.EyeHeightFor(height), 0f);
        }

        // ------------------------------------------------------------------ movement

        private void UpdateMovement(float deltaTime)
        {
            Vector2 move = input.Move;
            bool hasInput = move.sqrMagnitude > 0.0001f;

            IsRunning = hasInput && input.RunHeld && !IsCrouching;
            float speed = IsCrouching ? settings.crouchSpeed : IsRunning ? settings.runSpeed : settings.walkSpeed;

            // Direction relative to where the player faces.
            Vector3 desired = (transform.right * move.x + transform.forward * move.y) * speed;

            // Smoothly speed up / slow down instead of starting and stopping instantly.
            float rate = hasInput ? settings.acceleration : settings.deceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desired, rate * deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = GroundedStickSpeed;
            else
                _verticalVelocity -= settings.gravity * deltaTime;

            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags flags = _controller.Move(motion * deltaTime);

            // Walking into a wall shouldn't keep building up speed "into" it.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = _controller.velocity;
                _horizontalVelocity = Vector3.ClampMagnitude(new Vector3(actual.x, 0f, actual.z), _horizontalVelocity.magnitude);
            }
        }

        // ------------------------------------------------------------------ settings

        private void OnUserSettingsChanged(UserSettingsChangedEvent evt)
        {
            RefreshUserSensitivity();
        }

        /// <summary>Reads the player's Mouse Sensitivity option (multiplier on top of lookSensitivity).</summary>
        private void RefreshUserSensitivity()
        {
            _userSensitivity = Services.TryGet(out ConfigService config) ? config.User.mouseSensitivity : 1f;
        }

        private static float NormalizeAngle(float degrees)
        {
            return degrees > 180f ? degrees - 360f : degrees;
        }
    }
}
