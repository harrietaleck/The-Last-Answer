using TLA.Core.Events;
using TLA.Data.Player;
using TLA.Gameplay.Player;
using UnityEngine;

namespace TLA.Gameplay.Interaction
{
    /// <summary>
    /// Every frame, casts a ray from the centre of the camera to find the <see cref="IInteractable"/>
    /// the player is looking at, within PlayerMovementSettings.interactionDistance.
    /// Pressing Interact (E) uses it.
    ///
    /// Walls block the ray: you can't open a door through a wall. The ray ignores trigger colliders,
    /// so invisible trigger zones never block interaction. Interactables therefore need a normal
    /// (non-trigger) collider.
    ///
    /// Focus changes are announced with <see cref="InteractionFocusChangedEvent"/>, which the HUD
    /// uses to show "[E] Open". This script never touches the UI directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerMovementSettings settings;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Where the ray starts and which way it points. Usually the player's camera.")]
        [SerializeField] private Transform viewPoint;

        // Reused every frame to avoid creating garbage. 16 is plenty for one short ray.
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private IInteractable _current;
        private string _currentPromptText = string.Empty;

        /// <summary>The object currently looked at, or null.</summary>
        public IInteractable Current => _current;

        /// <summary>Distance to the current target's surface (valid while <see cref="Current"/> isn't null).</summary>
        public float CurrentDistance { get; private set; }

        private void Awake()
        {
            if (settings == null || input == null || viewPoint == null)
            {
                Debug.LogError("[Interactor] Settings, Input or View Point is not assigned.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            SetFocus(null);
        }

        private void Update()
        {
            // A destroyed object still "exists" as an interface reference; treat it as gone.
            if (_current is Object unityObject && unityObject == null) _current = null;

            IInteractable target = input.GameplayInputEnabled ? FindTarget() : null;

            if (!ReferenceEquals(target, _current))
                SetFocus(target);
            else if (_current != null && BuildPromptText(_current) != _currentPromptText)
                PublishFocus(); // same object, new prompt (e.g. door now says "Close")

            if (_current != null && input.InteractPressedThisFrame)
                _current.Interact(this);
        }

        private IInteractable FindTarget()
        {
            var ray = new Ray(viewPoint.position, viewPoint.forward);
            float distance = settings.interactionDistance;

            int count = settings.interactionRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, settings.interactionRadius, _hits, distance, settings.interactionLayers, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(ray, _hits, distance, settings.interactionLayers, QueryTriggerInteraction.Ignore);

            // Find the NEAREST hit that isn't part of the player. The camera sits inside the player's
            // own capsule, and looking down the ray can hit that capsule first, so it must be skipped.
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;
                if (nearest < 0 || _hits[i].distance < _hits[nearest].distance) nearest = i;
            }

            if (nearest < 0) return null;
            RaycastHit hit = _hits[nearest];

            // The nearest thing blocks everything behind it (walls block doors behind them).
            // The collider may be on a child (e.g. a door panel), so search upwards for the interactable.
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract) return null;

            CurrentDistance = hit.distance;
            return interactable;
        }

        private void SetFocus(IInteractable target)
        {
            if (_current is Interactable previous && previous != null) previous.OnFocusLost();

            _current = target;

            if (_current is Interactable next) next.OnFocusGained();

            PublishFocus();
        }

        private void PublishFocus()
        {
            _currentPromptText = _current != null ? BuildPromptText(_current) : string.Empty;
            EventBus.Publish(new InteractionFocusChangedEvent(_current != null, _currentPromptText));
        }

        private string BuildPromptText(IInteractable target)
        {
            return $"[{input.InteractKeyName}] {target.Prompt}";
        }

        private void OnDrawGizmosSelected()
        {
            // Shows the interaction reach in the Scene view when the player is selected.
            if (viewPoint == null || settings == null) return;
            Gizmos.color = _current != null ? Color.green : Color.yellow;
            Gizmos.DrawLine(viewPoint.position, viewPoint.position + viewPoint.forward * settings.interactionDistance);
        }
    }
}
