using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems.Investigation;
using TLA.Systems.Save;

namespace TLA.Systems.Dialogue
{
    /// <summary>
    /// Keeps every suspect's interview for the current case: the conversation history and the
    /// suspect's state (composure, topics asked, topics exposed, evidence shown). Saved with the
    /// game, so a loaded save resumes each interview exactly where it was.
    ///
    /// Sessions are created on demand by the DialogueManager (it supplies the providers). A loaded
    /// interview waits as a snapshot until that suspect is next interviewed, then is applied.
    /// </summary>
    public sealed class InterviewService : IGameService, ISaveable, ICaseScopedService
    {
        private readonly Dictionary<string, DialogueSession> _sessions = new Dictionary<string, DialogueSession>();
        private readonly Dictionary<string, DialogueSessionSnapshot> _pending = new Dictionary<string, DialogueSessionSnapshot>();

        public string SaveKey => "interviews";

        public void Initialize() { }

        public void Shutdown() { }

        public void BeginCase()
        {
            _sessions.Clear();
            _pending.Clear();
        }

        /// <summary>The existing interview with this suspect, or a new one from <paramref name="create"/> (with any saved state applied).</summary>
        public DialogueSession GetOrCreate(SuspectProfile suspect, Func<DialogueSession> create)
        {
            if (suspect == null) throw new ArgumentNullException(nameof(suspect));
            if (_sessions.TryGetValue(suspect.suspectId, out DialogueSession existing)) return existing;

            DialogueSession session = create();
            if (_pending.TryGetValue(suspect.suspectId, out DialogueSessionSnapshot snap))
            {
                session.RestoreSnapshot(snap);
                _pending.Remove(suspect.suspectId);
            }
            _sessions[suspect.suspectId] = session;
            return session;
        }

        /// <summary>True if this suspect has been interviewed (live or saved).</summary>
        public bool HasInterview(string suspectId) => _sessions.ContainsKey(suspectId) || _pending.ContainsKey(suspectId);

        /// <summary>Snapshot of one suspect's interview (live or saved), or null.</summary>
        public DialogueSessionSnapshot GetSnapshot(string suspectId)
        {
            if (_sessions.TryGetValue(suspectId, out DialogueSession s)) return s.CaptureSnapshot();
            return _pending.TryGetValue(suspectId, out DialogueSessionSnapshot snap) ? snap : null;
        }

        // ------------------------------------------------------------------ saving

        private sealed class SaveState
        {
            public List<DialogueSessionSnapshot> interviews = new List<DialogueSessionSnapshot>();
        }

        public JToken CaptureState()
        {
            var state = new SaveState();
            foreach (DialogueSession s in _sessions.Values) state.interviews.Add(s.CaptureSnapshot());
            foreach (var kv in _pending)
                if (!_sessions.ContainsKey(kv.Key)) state.interviews.Add(kv.Value);
            return JToken.FromObject(state);
        }

        public void RestoreState(JToken state)
        {
            var data = state?.ToObject<SaveState>();
            _sessions.Clear();
            _pending.Clear();
            if (data?.interviews == null) return;
            foreach (DialogueSessionSnapshot snap in data.interviews)
                if (snap != null && !string.IsNullOrEmpty(snap.suspectId)) _pending[snap.suspectId] = snap;
        }
    }
}
