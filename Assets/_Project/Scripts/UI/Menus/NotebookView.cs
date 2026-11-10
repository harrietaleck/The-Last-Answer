using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Investigation;
using TLA.UI.Components;
using TMPro;
using UnityEngine;

namespace TLA.UI.Menus
{
    /// <summary>
    /// INVESTIGATION NOTES page (inside the pause menu): case title, current objective, evidence
    /// collected and notebook entries. Everything comes from CaseSessionService; this view only
    /// displays it, and refreshes when it's shown or when something is added.
    /// </summary>
    public sealed class NotebookView : MonoBehaviour
    {
        [SerializeField] private TMP_Text caseTitle;
        [SerializeField] private TMP_Text objective;
        [SerializeField] private TMP_Text evidenceHeader;
        [SerializeField] private UIListEntry evidenceTemplate;
        [SerializeField] private Transform evidenceContainer;
        [SerializeField] private UIListEntry noteTemplate;
        [SerializeField] private Transform noteContainer;
        [SerializeField] private TMP_Text emptyNotes;

        private UIList<UIListEntry> _evidence;
        private UIList<UIListEntry> _notes;

        private void Awake()
        {
            _evidence = new UIList<UIListEntry>(evidenceTemplate, evidenceContainer);
            _notes = new UIList<UIListEntry>(noteTemplate, noteContainer);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EvidenceAddedEvent>(OnChanged);
            EventBus.Subscribe<NoteAddedEvent>(OnChanged);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EvidenceAddedEvent>(OnChanged);
            EventBus.Unsubscribe<NoteAddedEvent>(OnChanged);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnChanged);
        }

        private void OnChanged<T>(T evt) where T : struct, IGameEvent => Refresh();

        public void Refresh()
        {
            if (_evidence == null) Awake();
            _evidence.Clear();
            _notes.Clear();

            CaseSessionService session = Services.TryGet(out GameManager game) ? game.Case : null;
            if (session == null || !session.HasActiveCase)
            {
                caseTitle.text = "NO ACTIVE CASE";
                objective.text = "";
                evidenceHeader.text = "EVIDENCE";
                emptyNotes.gameObject.SetActive(true);
                return;
            }

            caseTitle.text = session.CurrentCase.DisplayName.ToUpperInvariant();
            objective.text = string.IsNullOrEmpty(session.Objective) ? "-" : session.Objective;
            evidenceHeader.text = $"EVIDENCE  ({session.Evidence.Count})";

            // Newest first: the most recent discovery is usually what the player wants to read.
            for (int i = session.Evidence.Count - 1; i >= 0; i--)
            {
                EvidenceRecord e = session.Evidence[i];
                _evidence.Add().Set(e.displayName, e.description, i == session.Evidence.Count - 1);
            }

            for (int i = session.Notes.Count - 1; i >= 0; i--)
            {
                NoteRecord n = session.Notes[i];
                _notes.Add().Set(string.IsNullOrEmpty(n.source) ? "" : n.source.ToUpperInvariant(), n.text);
            }

            emptyNotes.gameObject.SetActive(session.Evidence.Count == 0 && session.Notes.Count == 0);
        }
    }
}
