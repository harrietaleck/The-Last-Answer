using TLA.Core.Services;
using TLA.Gameplay.Interaction;
using TLA.Systems;
using UnityEngine;

namespace TLA.Gameplay.Investigation
{
    /// <summary>
    /// Data-driven interactable for placeholder investigation content: press E to record a piece of
    /// evidence, add a notebook entry and/or change the objective, all through CaseSessionService.
    /// The HUD counter, evidence notification and notebook update automatically (they listen to
    /// its events).
    ///
    /// The real evidence system (later stage) will replace this with Evidence definitions, but it
    /// will talk to the same CaseSessionService, so the UI won't need to change.
    /// </summary>
    public sealed class InvestigationTrigger : Interactable
    {
        [Header("Evidence (leave Id empty for none)")]
        [SerializeField] private string evidenceId = "";
        [SerializeField] private string evidenceName = "";
        [TextArea(2, 4)] [SerializeField] private string evidenceDescription = "";

        [Header("Notebook (empty = none)")]
        [TextArea(2, 4)] [SerializeField] private string note = "";
        [SerializeField] private string noteSource = "";

        [Header("Objective (empty = unchanged)")]
        [SerializeField] private string newObjective = "";

        [Tooltip("Stop offering the interaction after the first use.")]
        [SerializeField] private bool singleUse = true;

        private bool _used;

        public override bool CanInteract => base.CanInteract && !(singleUse && _used);

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (!Services.TryGet(out GameManager game))
            {
                Debug.LogWarning("[InvestigationTrigger] Game systems aren't running.", this);
                return;
            }

            _used = true;
            if (!string.IsNullOrWhiteSpace(evidenceId)) game.Case.AddEvidence(evidenceId, evidenceName, evidenceDescription);
            if (!string.IsNullOrWhiteSpace(note)) game.Case.AddNote(note, noteSource);
            if (!string.IsNullOrWhiteSpace(newObjective)) game.Case.SetObjective(newObjective);
        }

        private void Start()
        {
            // Evidence already found (e.g. after loading a save) shouldn't be offered again.
            if (singleUse && !string.IsNullOrWhiteSpace(evidenceId) && Services.TryGet(out GameManager game) && game.Case.HasEvidence(evidenceId))
                _used = true;
        }
    }
}
