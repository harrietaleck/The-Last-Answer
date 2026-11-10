using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TLA.Gameplay.Player
{
    /// <summary>
    /// The ONLY player script that talks to the Input System. Other player scripts ask it for
    /// simple values ("which way is the player moving?", "was Interact pressed this frame?").
    ///
    /// Bindings live in Assets/_Project/Data/Settings/PlayerControls.inputactions:
    ///   "Player" map: Move (WASD/arrows), Look (mouse), Run (Shift), Crouch (C / Ctrl), Interact (E)
    ///   "Global" map: Pause (Esc), always active, so you can un-pause.
    /// Double-click that asset to change keys; no code changes needed.
    ///
    /// It also follows the game state: the Player map and a locked, hidden cursor are only active
    /// while the state is Investigating. Pausing or opening a dialogue frees the mouse automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string GlobalMapName = "Global";

        [Tooltip("The PlayerControls input actions asset.")]
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap _playerMap;
        private InputActionMap _globalMap;
        private InputAction _move;
        private InputAction _look;
        private InputAction _run;
        private InputAction _crouch;
        private InputAction _interact;
        private InputAction _pause;
        private bool _ready;

        /// <summary>True while the player is allowed to move, look and interact.</summary>
        public bool GameplayInputEnabled { get; private set; }

        /// <summary>WASD as a vector: x = strafe (-1 left, +1 right), y = forward/back. Zero when input is disabled.</summary>
        public Vector2 Move => GameplayInputEnabled ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;

        /// <summary>Mouse movement this frame in pixels. Zero when input is disabled.</summary>
        public Vector2 Look => GameplayInputEnabled ? _look.ReadValue<Vector2>() : Vector2.zero;

        public bool RunHeld => GameplayInputEnabled && _run.IsPressed();

        public bool CrouchHeld => GameplayInputEnabled && _crouch.IsPressed();

        public bool CrouchPressedThisFrame => GameplayInputEnabled && _crouch.WasPressedThisFrame();

        public bool InteractPressedThisFrame => GameplayInputEnabled && _interact.WasPressedThisFrame();

        /// <summary>Display name of the Interact key, e.g. "E". Used for on-screen prompts, so rebinding updates them too.</summary>
        public string InteractKeyName => _ready ? _interact.GetBindingDisplayString() : "E";

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

            _playerMap = inputActions.FindActionMap(PlayerMapName, throwIfNotFound: true);
            _globalMap = inputActions.FindActionMap(GlobalMapName, throwIfNotFound: true);
            _move = _playerMap.FindAction("Move", true);
            _look = _playerMap.FindAction("Look", true);
            _run = _playerMap.FindAction("Run", true);
            _crouch = _playerMap.FindAction("Crouch", true);
            _interact = _playerMap.FindAction("Interact", true);
            _pause = _globalMap.FindAction("Pause", true);
            _ready = true;
        }

        private void OnEnable()
        {
            if (!_ready) return;

            _globalMap.Enable();
            _pause.performed += OnPausePerformed;
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            // If the game systems aren't running yet (e.g. the scene is still booting), allow
            // gameplay. The correct state arrives with the next GameStateChangedEvent.
            bool investigating = !Services.TryGet(out GameStateManager state) || state.Current == GameState.Investigating;
            SetGameplayInput(investigating);
        }

        private void OnDisable()
        {
            if (!_ready) return;

            _pause.performed -= OnPausePerformed;
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            SetGameplayInput(false);
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
            SetGameplayInput(evt.Current == GameState.Investigating);
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
                SetGameplayInput(!GameplayInputEnabled); // no game systems: just toggle the mouse
            }
        }

        private void SetGameplayInput(bool enable)
        {
            GameplayInputEnabled = enable;
            if (enable) _playerMap.Enable();
            else _playerMap.Disable();
            ApplyCursor(enable);
        }

        private static void ApplyCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
