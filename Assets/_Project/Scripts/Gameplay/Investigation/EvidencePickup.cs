using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Evidence;
using TLA.Gameplay.Interaction;
using TLA.Systems;
using TLA.Systems.Investigation;
using UnityEngine;

namespace TLA.Gameplay.Investigation
{
    /// <summary>
    /// Puts a piece of evidence in the world. Press E to open it in the inspect view (read the
    /// document, look at the photo), where the player can collect and examine it.
    ///
    /// All rules come from <see cref="EvidenceService"/> and the <see cref="EvidenceDefinition"/>:
    ///  - discovery conditions not met: hidden, or shows the locked prompt and hint
    ///  - collectable objects disappear once taken; fixed items (wall board, laptop) stay and can be re-read
    /// This component only reflects that state in the scene.
    /// </summary>
    public sealed class EvidencePickup : Interactable
    {
        [SerializeField] private EvidenceDefinition evidence;

        [Tooltip("Hide the object once a collectable item is taken. Untick when the evidence comes out of a container that stays (e.g. a filing cabinet).")]
        [SerializeField] private bool hideOnCollect = true;

        [Tooltip("Objects hidden once the evidence is collected (collectable items only). Empty = this object's renderers.")]
        [SerializeField] private GameObject[] hideWhenCollected = new GameObject[0];

        private Renderer[] _renderers;
        private Collider[] _colliders;

        public EvidenceDefinition Evidence => evidence;

        // The discovery sound comes from the inspect view opening (AudioManager), not the pickup itself.
        protected override TLA.Data.Audio.SoundCue InteractionSound => TLA.Data.Audio.SoundCue.None;

        private static bool TryGetService(out EvidenceService service)
        {
            service = Services.TryGet(out GameManager game) ? game.Evidence : null;
            return service != null;
        }

        private bool Available => TryGetService(out EvidenceService s) && s.IsAvailable(evidence);
        private bool Collected => TryGetService(out EvidenceService s) && s.IsCollected(evidence);

        public override string Prompt
        {
            get
            {
                if (evidence == null) return base.Prompt;
                if (!Available) return string.IsNullOrEmpty(evidence.discovery?.lockedPrompt) ? "Search" : evidence.discovery.lockedPrompt;
                return Collected ? $"View {evidence.displayName.ToLowerInvariant()}" : evidence.interactionPrompt;
            }
        }

        public override bool CanInteract
        {
            get
            {
                if (!base.CanInteract || evidence == null) return false;
                if (Collected && evidence.collectable) return false; // taken away
                if (!Available && evidence.discovery != null && evidence.discovery.hiddenUntilAvailable) return false;
                return true;
            }
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable() => EventBus.Subscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);

        private void OnDisable() => EventBus.Unsubscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);

        private void Start() => RefreshVisibility();

        private void OnEvidenceChanged(EvidenceStateChangedEvent evt) => RefreshVisibility();

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (evidence == null || !TryGetService(out EvidenceService service))
            {
                Debug.LogWarning("[EvidencePickup] No evidence assigned, or game systems aren't running.", this);
                return;
            }

            if (!service.IsAvailable(evidence))
            {
                EventBus.Publish(new NotificationEvent("NOTHING YET", evidence.discovery.lockedHint));
                return;
            }

            service.Discover(evidence);
            EventBus.Publish(new EvidenceInspectRequestedEvent(evidence, true));
        }

        /// <summary>Hidden until available (if set), and gone once a collectable item is taken.</summary>
        private void RefreshVisibility()
        {
            if (evidence == null || _renderers == null) return;
            bool hiddenLocked = evidence.discovery != null && evidence.discovery.hiddenUntilAvailable && !Available;
            bool taken = evidence.collectable && hideOnCollect && Collected;
            bool show = !hiddenLocked && !taken;

            if (hideWhenCollected != null && hideWhenCollected.Length > 0)
            {
                foreach (GameObject go in hideWhenCollected)
                    if (go != null) go.SetActive(show);
            }
            else
            {
                foreach (Renderer r in _renderers) if (r != null) r.enabled = show;
            }
            foreach (Collider c in _colliders) if (c != null) c.enabled = show;
        }
    }
}
