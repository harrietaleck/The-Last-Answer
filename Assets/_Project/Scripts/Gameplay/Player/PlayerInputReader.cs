using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TLA.Gameplay.Player
{
    /// <summary>
    /// The ONLY player script that talks to the Input System. Other scripts ask it for simple
    /// values ("which way is the player moving?", "was Interact pressed this frame?").
    ///
    /// Bindings live in Assets/_Project/Data/Settings/PlayerControls.inputactions:
    ///   "Player" map:    Move (WASD/arrows), Look (mouse), Run (Shift), Crouch (C / Ctrl), Interact (E)
    ///   "Interview" map: NextShot (Tab), Leave (E), used while seated at the interview table
    ///   "Global" map:    Pause (Esc), always active, so you can un-pause
    /// Double-click that asset to change keys; no code changes needed.
    ///
    /// It follows the game state:
    ///   Investigating -> Player map on, cursor locked and hidden
    ///   Interrogating -> Interview map on, cursor free (for dialogue UI later)
    ///   anything else -> only Global, cursor free
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("The PlayerControls input actions asset.")]
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap _playerMap;
        private InputActionMap _interviewMap;
        private InputActionMap _globalMap;
        private InputAction _move;
        private InputAction _look;
        private InputAction _run;
        private InputAction _crouch;
        private InputAction _interact;
        private InputAction _nextShot;
        private InputAction _leave;
        private InputAction _pause;
        private bool _ready;

        /// <summary>True while the player is allowed to move, look and interact (Investigating).</summary>
        public bool GameplayInputEnabled { get; private set; }

        /// <summary>True while seated at the interview table (Interrogating).</summary>
        public bool InterviewInputEnabled { get; private set; }

        /// <summary>WASD as a vector: x = strafe (-1 left, +1 right), y = forward/back. Zero when input is disabled.</summary>
        public Vector2 Move => GameplayInputEnabled ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;

        /// <summary>Mouse movement this frame in pixels. Zero when input is disabled.</summary>
        public Vector2 Look => GameplayInputEnabled ? _look.ReadValue<Vector2>() : Vector2.zero;

        public bool RunHeld => GameplayInputEnabled && _run.IsPressed();

        public bool CrouchHeld => GameplayInputEnabled && _crouch.IsPressed();

        public bool CrouchPressedThisFrame => GameplayInputEnabled && _crouch.WasPressedThisFrame();

        public bool InteractPressedThisFrame => GameplayInputEnabled && _interact.WasPressedThisFrame();

        public bool NextShotPressedThisFrame => InterviewInputEnabled && _nextShot.WasPressedThisFrame();

        public bool LeavePressedThisFrame => InterviewInputEnabled && _leave.WasPressedThisFrame();

        /// <summary>Display name of the Interact key, e.g. "E". Used for on-screen prompts, so rebinding updates them too.</summary>
        public string InteractKeyName => _ready ? _interact.GetBindingDisplayString() : "E";

        public string NextShotKeyName => _ready ? _nextShot.GetBindingDisplayString() : "Tab";

        public string LeaveKeyName => _ready ? _leave.GetBindingDisplayString() : "E";

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PlayerInput] No InputActionAsset assigned. Assign Data/Settings/PlayerControls.", this);
                enabled = false;
                return;
            }

            // Work on a private copy, so enabling/disabling maps never changes the shared project asset.
            inputActions = Instantiate(inputActions);

            _playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            _interviewMap = inputActions.FindActionMap("Interview", throwIfNotFound: true);
            _globalMap = inputActions.FindActionMap("Global", throwIfNotFound: true);
            _move = _playerMap.FindAction("Move", true);
            _look = _playerMap.FindAction("Look", true);
            _run = _playerMap.FindAction("Run", true);
            _crouch = _playerMap.FindAction("Crouch", true);
            _interact = _playerMap.FindAction("Interact", true);
            _nextShot = _interviewMap.FindAction("NextShot", true);
            _leave = _interviewMap.FindAction("Leave", true);
            _pause = _globalMap.FindAction("Pause", true);
            _ready = true;
        }

        private void OnEnable()
        {
            if (!_ready) return;

            _globalMap.Enable();
            _pause.performed += OnPausePerformed;
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            // If the game systems aren't running yet (e.g. the scene is still booting), assume
            // Investigating. The correct state arrives with the next GameStateChangedEvent.
            ApplyState(Services.TryGet(out GameStateManager state) ? state.Current : GameState.Investigating);
        }

        private void OnDisable()
        {
            if (!_ready) return;

            _pause.performed -= OnPausePerformed;
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            SetMaps(gameplay: false, interview: false);
            _globalMap.Disable();
        }

        private void OnDestroy()
        {
            if (_ready) Destroy(inputActions); // free our private copy
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Alt-tabbing releases the cursor; lock it again when the game window regains focus.
            if (hasFocus && GameplayInputEnabled) ApplyCursor(true);
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            ApplyState(evt.Current);
        }

        private void ApplyState(GameState state)
        {
            SetMaps(gameplay: state == GameState.Investigating, interview: state == GameState.Interrogating);
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            if (Services.TryGet(out GameStateManager state))
            {
                if (state.IsPaused) state.Resume();
                else state.Pause(); // refused (with a warning) in states that can't pause, e.g. MainMenu
            }
            else
            {
                SetMaps(!GameplayInputEnabled, false); // no game systems: just toggle the mouse
            }
        }

        private void SetMaps(bool gameplay, bool interview)
        {
            GameplayInputEnabled = gameplay;
            InterviewInputEnabled = interview;

            if (gameplay) _playerMap.Enable(); else _playerMap.Disable();
            if (interview) _interviewMap.Enable(); else _interviewMap.Disable();

            ApplyCursor(gameplay);
        }

        private static void ApplyCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
