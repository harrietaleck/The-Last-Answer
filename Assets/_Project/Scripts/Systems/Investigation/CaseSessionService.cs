using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems.Save;
using UnityEngine;

namespace TLA.Systems.Investigation
{
    /// <summary>One discovered piece of evidence, as the player sees it.</summary>
    [Serializable]
    public sealed class EvidenceRecord
    {
        public string id;
        public string displayName;
        public string description;
    }

    /// <summary>One line in the investigation notebook.</summary>
    [Serializable]
    public sealed class NoteRecord
    {
        public string text;
        public string source; // e.g. "Evidence Room", "Interview: D. Marlowe"
    }

    /// <summary>
    /// The state of the investigation in progress: which case, the current objective, the evidence
    /// found so far and the notebook. It's what the HUD, notebook and evidence notifications show.
    ///
    /// Gameplay calls it (e.g. picking up evidence → <see cref="AddEvidence"/>); the UI only listens
    /// to its events and reads its properties. It is saved with the game (<see cref="ISaveable"/>).
    ///
    /// NOTE: this tracks what the PLAYER has found. The case's hidden truth (facts, contradictions,
    /// endings) belongs to the investigation systems added in a later stage, which will call this.
    /// </summary>
    public sealed class CaseSessionService : IGameService, ISaveable
    {
        private readonly CaseCatalog _catalog;
        private readonly List<EvidenceRecord> _evidence = new List<EvidenceRecord>();
        private readonly List<NoteRecord> _notes = new List<NoteRecord>();

        public CaseSessionService(CaseCatalog catalog)
        {
            _catalog = catalog;
        }

        public CaseDefinition CurrentCase { get; private set; }
        public string Objective { get; private set; } = string.Empty;
        public IReadOnlyList<EvidenceRecord> Evidence => _evidence;
        public IReadOnlyList<NoteRecord> Notes => _notes;
        public bool HasActiveCase => CurrentCase != null;

        public string SaveKey => "caseSession";

        public void Initialize() { }

        public void Shutdown() { }

        /// <summary>Starts a fresh investigation of <paramref name="caseDefinition"/> (clears previous progress).</summary>
        public void StartCase(CaseDefinition caseDefinition)
        {
            if (caseDefinition == null) throw new ArgumentNullException(nameof(caseDefinition));

            CurrentCase = caseDefinition;
            _evidence.Clear();
            _notes.Clear();
            EventBus.Publish(new CaseStartedEvent(CurrentCase));
            SetObjective(caseDefinition.initialObjective);
        }

        public void SetObjective(string objective)
        {
            objective ??= string.Empty;
            if (objective == Objective) return;
            Objective = objective;
            EventBus.Publish(new ObjectiveChangedEvent(Objective));
        }

        /// <summary>Records evidence. Returns false (and does nothing) if it was already found.</summary>
        public bool AddEvidence(string id, string displayName, string description = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Evidence needs an id.", nameof(id));
            if (HasEvidence(id)) return false;

            var record = new EvidenceRecord { id = id, displayName = displayName, description = description ?? string.Empty };
            _evidence.Add(record);
            EventBus.Publish(new EvidenceAddedEvent(record, _evidence.Count));
            return true;
        }

        public bool HasEvidence(string id)
        {
            foreach (EvidenceRecord e in _evidence)
                if (e.id == id) return true;
            return false;
        }

        public void AddNote(string text, string source = "")
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var note = new NoteRecord { text = text, source = source ?? string.Empty };
            _notes.Add(note);
            EventBus.Publish(new NoteAddedEvent(note));
        }

        // ------------------------------------------------------------------ saving

        private sealed class SaveState
        {
            public string caseId;
            public string objective;
            public List<EvidenceRecord> evidence = new List<EvidenceRecord>();
            public List<NoteRecord> notes = new List<NoteRecord>();
        }

        public JToken CaptureState()
        {
            return JToken.FromObject(new SaveState
            {
                caseId = CurrentCase != null ? CurrentCase.caseId : null,
                objective = Objective,
                evidence = new List<EvidenceRecord>(_evidence),
                notes = new List<NoteRecord>(_notes)
            });
        }

        public void RestoreState(JToken state)
        {
            var data = state.ToObject<SaveState>();
            if (data == null) return;

            CaseDefinition restored = _catalog != null && data.caseId != null ? _catalog.Find(data.caseId) : null;
            if (data.caseId != null && restored == null)
                Debug.LogWarning($"[CaseSession] Saved case '{data.caseId}' is not in the case catalog.");

            CurrentCase = restored;
            _evidence.Clear();
            _notes.Clear();
            if (data.evidence != null) _evidence.AddRange(data.evidence);
            if (data.notes != null) _notes.AddRange(data.notes);
            Objective = data.objective ?? string.Empty;

            if (CurrentCase != null) EventBus.Publish(new CaseStartedEvent(CurrentCase));
            EventBus.Publish(new ObjectiveChangedEvent(Objective));
        }
    }
}
