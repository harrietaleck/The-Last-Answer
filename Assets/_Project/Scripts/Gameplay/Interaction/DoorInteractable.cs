using UnityEngine;

namespace TLA.Gameplay.Interaction
{
    /// <summary>
    /// A hinged door that swings open/closed when used. It always opens AWAY from the player.
    ///
    /// SETUP:
    ///   Door                (this component; its position is the HINGE, rotated around its Y axis)
    ///   └── Panel           (the visible door mesh + a BoxCollider, offset along the Door's local +X from the hinge)
    /// The prompt switches between "Open" and "Close" automatically. Tick "Locked" for a door that
    /// shows "Locked" and won't open (later, a game system can unlock it with <see cref="SetLocked"/>).
    /// </summary>
    public sealed class DoorInteractable : Interactable
    {
        [Tooltip("How far the door swings open, in degrees.")]
        [Range(10f, 170f)] [SerializeField] private float openAngle = 95f;

        [Tooltip("Swing speed in degrees per second.")]
        [Min(10f)] [SerializeField] private float swingSpeed = 180f;

        [SerializeField] private bool startsOpen;
        [SerializeField] private bool locked;

        [SerializeField] private string openPrompt = "Open";
        [SerializeField] private string closePrompt = "Close";
        [SerializeField] private string lockedPrompt = "Locked";

        private Quaternion _closedRotation;
        private Quaternion _targetRotation;
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public bool IsLocked => locked;

        public override string Prompt => locked ? lockedPrompt : _isOpen ? closePrompt : openPrompt;

        private void Awake()
        {
            _closedRotation = transform.localRotation;
            _targetRotation = _closedRotation;

            if (startsOpen)
            {
                _isOpen = true;
                _targetRotation = _closedRotation * Quaternion.Euler(0f, openAngle, 0f);
                transform.localRotation = _targetRotation;
            }
        }

        private void Update()
        {
            if (transform.localRotation != _targetRotation)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, _targetRotation, swingSpeed * Time.deltaTime);
        }

        protected override TLA.Data.Audio.SoundCue InteractionSound => TLA.Data.Audio.SoundCue.None; // open/close sounds below

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (locked)
            {
                PlaySound(TLA.Data.Audio.SoundCue.Interact, transform.position); // a dull click: it won't open
                return;
            }
            PlaySound(_isOpen ? TLA.Data.Audio.SoundCue.DoorClose : TLA.Data.Audio.SoundCue.DoorOpen, transform.position);

            if (_isOpen)
            {
                _targetRotation = _closedRotation;
            }
            else
            {
                // Swing away from the player: which side of the door is the player on?
                Vector3 toPlayer = interactor.transform.position - transform.position;
                float side = Vector3.Dot(toPlayer, transform.parent != null
                    ? transform.parent.TransformDirection(_closedRotation * Vector3.forward)
                    : _closedRotation * Vector3.forward);
                // With the panel along local +X, a positive Y rotation swings it towards -Z (local back).
                float angle = side > 0f ? openAngle : -openAngle;
                _targetRotation = _closedRotation * Quaternion.Euler(0f, angle, 0f);
            }

            _isOpen = !_isOpen;
        }

        public void SetLocked(bool value)
        {
            locked = value;
        }
    }
}
