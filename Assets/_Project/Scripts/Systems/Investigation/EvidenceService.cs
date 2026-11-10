using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Systems.Save;

namespace TLA.Systems.Investigation
{
    /// <summary>The player's progress with one piece of evidence.</summary>
    [Serializable]
    public sealed class EvidenceState
    {
        public string id;
        public bool discovered;
        public bool collected;
        public bool examined;

        /// <summary>Suspect ids it has been shown to.</summary>
        public List<string> presentedTo = new List<string>();

        public bool WasPresented => presentedTo.Count > 0;
    }

    public enum CollectResult
    {
        Collected,
        AlreadyCollected,
        NotAvailable,   // discovery conditions not met yet
        Unknown         // not part of the current case
    }

    public enum ConnectResult
    {
        Connected,
        AlreadyConnected,
        NotRelated,
        NotCollected,   // both items must be in the inventory
        SameItem,
        Unknown
    }

    /// <summary>Result of examining an item: whether it was new, and which facts it established.</summary>
    public sealed class ExamineResult
    {
        public bool Success;
        public bool FirstTime;
        public string Error;
        public readonly List<CaseFact> NewFacts = new List<CaseFact>();
    }

    /// <summary>A connection the player has made, as shown in the evidence screen.</summary>
    public readonly struct MadeConnection
    {
        public readonly EvidenceDefinition Other;
        public readonly EvidenceConnection Connection;
        public MadeConnection(EvidenceDefinition other, EvidenceConnection connection) { Other = other; Connection = connection; }
    }

    /// <summary>
    /// The single owner of evidence progress: what has been discovered, collected, examined and
    /// presented, which facts are established and which connections the player has made.
    ///
    /// Every rule lives here in C#: discovery conditions, what counts as a valid connection and
    /// which facts a step establishes. The UI and world objects only ask it and listen to its events;
    /// AI dialogue never touches it (it only reads which items were presented).
    ///
    /// Collected items are also recorded in <see cref="CaseSessionService"/>, which the HUD counter,
    /// notebook, notifications and the interview evidence picker already use.
    /// </summary>
    public sealed class EvidenceService : IGameService, ISaveable, ICaseScopedService
    {
        private readonly CaseSessionService _session;
        private readonly Dictionary<string, EvidenceState> _states = new Dictionary<string, EvidenceState>();
        private readonly HashSet<string> _facts = new HashSet<string>();
        private readonly HashSet<string> _connections = new HashSet<string>(); // "idA|idB", ids sorted

        public EvidenceService(CaseSessionService session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public string SaveKey => "evidence";

        public void Initialize() { }

        public void Shutdown() { }

        private CaseDefinition Case => _session.CurrentCase;

        /// <summary>All evidence in the current case (empty without a case).</summary>
        public IReadOnlyList<EvidenceDefinition> Definitions =>
            Case != null && Case.evidence != null ? Case.evidence : (IReadOnlyList<EvidenceDefinition>)Array.Empty<EvidenceDefinition>();

        public IReadOnlyCollection<string> EstablishedFactIds => _facts;

        /// <summary>Clears all progress; call when a new investigation starts.</summary>
        public void BeginCase()
        {
            _states.Clear();
            _facts.Clear();
            _connections.Clear();
            EventBus.Publish(new EvidenceStateChangedEvent(null, EvidenceChange.Reset));
        }

        // ------------------------------------------------------------------ queries

        public EvidenceDefinition Find(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId)) return null;
            foreach (EvidenceDefinition d in Definitions)
                if (d != null && d.evidenceId == evidenceId) return d;
            return null;
        }

        public CaseFact FindFact(string factId)
        {
            if (Case?.facts == null || string.IsNullOrEmpty(factId)) return null;
            foreach (CaseFact f in Case.facts)
                if (f != null && f.factId == factId) return f;
            return null;
        }

        /// <summary>Progress for an item (a blank state if untouched). Never null.</summary>
        public EvidenceState GetState(string evidenceId)
        {
            if (evidenceId == null) return new EvidenceState();
            if (_states.TryGetValue(evidenceId, out EvidenceState s)) return s;
            // Recorded in the case session but no state here (e.g. a save from before this system): it was collected.
            if (_session.HasEvidence(evidenceId)) return _states[evidenceId] = new EvidenceState { id = evidenceId, discovered = true, collected = true };
            return new EvidenceState { id = evidenceId };
        }

        public bool IsCollected(EvidenceDefinition d) => d != null && GetState(d.evidenceId).collected;
        public bool IsExamined(EvidenceDefinition d) => d != null && GetState(d.evidenceId).examined;
        public bool IsFactEstablished(CaseFact f) => f != null && _facts.Contains(f.factId);

        /// <summary>Collected items in the order they were found (for the inventory).</summary>
        public List<EvidenceDefinition> GetCollected()
        {
            var result = new List<EvidenceDefinition>();
            foreach (EvidenceRecord r in _session.Evidence)
            {
                EvidenceDefinition d = Find(r.id);
                if (d != null && IsCollected(d)) result.Add(d);
            }
            return result;
        }

        /// <summary>True when the item's discovery conditions are met.</summary>
        public bool IsAvailable(EvidenceDefinition d)
        {
            if (d == null) return false;
            DiscoveryConditions c = d.discovery;
            if (c == null || c.IsEmpty) return true;
            if (c.requiresCollected != null)
                foreach (EvidenceDefinition e in c.requiresCollected)
                    if (e != null && !IsCollected(e)) return false;
            if (c.requiresExamined != null)
                foreach (EvidenceDefinition e in c.requiresExamined)
                    if (e != null && !IsExamined(e)) return false;
            if (c.requiresFacts != null)
                foreach (CaseFact f in c.requiresFacts)
                    if (f != null && !IsFactEstablished(f)) return false;
            return true;
        }

        /// <summary>Connections the player has made that involve <paramref name="d"/>.</summary>
        public List<MadeConnection> GetConnections(EvidenceDefinition d)
        {
            var result = new List<MadeConnection>();
            if (d == null) return result;
            foreach (EvidenceDefinition other in Definitions)
            {
                if (other == null || other == d || !_connections.Contains(PairKey(d, other))) continue;
                EvidenceConnection link = FindLink(d, other);
                if (link != null) result.Add(new MadeConnection(other, link));
            }
            return result;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>The player found the item in the world (opened it in the inspect view).</summary>
        public bool Discover(EvidenceDefinition d)
        {
            if (!IsInCase(d) || !IsAvailable(d)) return false;
            EvidenceState s = StateFor(d);
            if (s.discovered) return false;
            s.discovered = true;
            EventBus.Publish(new EvidenceStateChangedEvent(d.evidenceId, EvidenceChange.Discovered));
            return true;
        }

        /// <summary>Takes (or logs) the item into the inventory.</summary>
        public CollectResult Collect(EvidenceDefinition d)
        {
            if (!IsInCase(d)) return CollectResult.Unknown;
            if (!IsAvailable(d)) return CollectResult.NotAvailable;
            EvidenceState s = StateFor(d);
            if (s.collected) return CollectResult.AlreadyCollected;

            s.discovered = true;
            s.collected = true;
            _session.AddEvidence(d.evidenceId, d.displayName, d.description); // HUD, notification, notebook, interview picker
            if (!string.IsNullOrWhiteSpace(d.noteOnCollect)) _session.AddNote(d.noteOnCollect, d.displayName);
            if (!string.IsNullOrWhiteSpace(d.objectiveOnCollect)) _session.SetObjective(d.objectiveOnCollect);
            EventBus.Publish(new EvidenceStateChangedEvent(d.evidenceId, EvidenceChange.Collected));
            return CollectResult.Collected;
        }

        /// <summary>Looks closely: reveals the examination notes and establishes the item's facts.</summary>
        public ExamineResult Examine(EvidenceDefinition d)
        {
            var result = new ExamineResult();
            if (!IsInCase(d))
            {
                result.Error = "This isn't part of the case.";
                return result;
            }
            EvidenceState s = StateFor(d);
            if (!s.discovered)
            {
                result.Error = "Find it first.";
                return result;
            }

            result.Success = true;
            result.FirstTime = !s.examined;
            if (!s.examined)
            {
                s.examined = true;
                if (d.relatedFacts != null)
                    foreach (CaseFact f in d.relatedFacts)
                        if (Establish(f)) result.NewFacts.Add(f);
                EventBus.Publish(new EvidenceStateChangedEvent(d.evidenceId, EvidenceChange.Examined));
            }
            return result;
        }

        /// <summary>Records that the item was shown to a suspect (called by the interview system after a successful turn).</summary>
        public void MarkPresented(string evidenceId, string suspectId)
        {
            EvidenceDefinition d = Find(evidenceId);
            if (d == null || string.IsNullOrEmpty(suspectId)) return;
            EvidenceState s = StateFor(d);
            if (s.presentedTo.Contains(suspectId)) return;
            s.presentedTo.Add(suspectId);
            EventBus.Publish(new EvidenceStateChangedEvent(d.evidenceId, EvidenceChange.Presented));
        }

        /// <summary>Tries to connect two collected items. Only connections authored in the data succeed.</summary>
        public ConnectResult TryConnect(EvidenceDefinition a, EvidenceDefinition b, out EvidenceConnection connection)
        {
            connection = null;
            if (!IsInCase(a) || !IsInCase(b)) return ConnectResult.Unknown;
            if (a == b) return ConnectResult.SameItem;
            if (!IsCollected(a) || !IsCollected(b)) return ConnectResult.NotCollected;

            connection = FindLink(a, b);
            if (connection == null) return ConnectResult.NotRelated;
            if (!_connections.Add(PairKey(a, b))) return ConnectResult.AlreadyConnected;

            Establish(connection.establishesFact);
            EventBus.Publish(new EvidenceConnectedEvent(a, b, connection));
            EventBus.Publish(new EvidenceStateChangedEvent(a.evidenceId, EvidenceChange.Connected));
            EventBus.Publish(new EvidenceStateChangedEvent(b.evidenceId, EvidenceChange.Connected));
            return ConnectResult.Connected;
        }

        /// <summary>Confirms a fact from another game system (e.g. a contradiction exposed in an interview). False if already confirmed.</summary>
        public bool ConfirmFact(CaseFact fact) => Establish(fact);

        // ------------------------------------------------------------------ helpers

        private bool IsInCase(EvidenceDefinition d)
        {
            if (d == null) return false;
            foreach (EvidenceDefinition e in Definitions)
                if (e == d) return true;
            return false;
        }

        private EvidenceState StateFor(EvidenceDefinition d)
        {
            EvidenceState s = GetState(d.evidenceId);
            _states[d.evidenceId] = s;
            return s;
        }

        private bool Establish(CaseFact fact)
        {
            if (fact == null || !_facts.Add(fact.factId)) return false;
            EventBus.Publish(new FactEstablishedEvent(fact));
            return true;
        }

        /// <summary>The authored connection between two items (defined on either side), or null.</summary>
        public static EvidenceConnection FindLink(EvidenceDefinition a, EvidenceDefinition b)
        {
            if (a == null || b == null) return null;
            if (a.connections != null)
                foreach (EvidenceConnection c in a.connections)
                    if (c != null && c.other == b) return c;
            if (b.connections != null)
                foreach (EvidenceConnection c in b.connections)
                    if (c != null && c.other == a) return c;
            return null;
        }

        private static string PairKey(EvidenceDefinition a, EvidenceDefinition b) =>
            string.CompareOrdinal(a.evidenceId, b.evidenceId) < 0 ? a.evidenceId + "|" + b.evidenceId : b.evidenceId + "|" + a.evidenceId;

        // ------------------------------------------------------------------ saving

        private sealed class SaveState
        {
            public List<EvidenceState> items = new List<EvidenceState>();
            public List<string> facts = new List<string>();
            public List<string> connections = new List<string>();
        }

        public JToken CaptureState()
        {
            return JToken.FromObject(new SaveState
            {
                items = new List<EvidenceState>(_states.Values),
                facts = new List<string>(_facts),
                connections = new List<string>(_connections)
            });
        }

        public void RestoreState(JToken state)
        {
            var data = state?.ToObject<SaveState>();
            _states.Clear();
            _facts.Clear();
            _connections.Clear();
            if (data != null)
            {
                if (data.items != null)
                    foreach (EvidenceState s in data.items)
                        if (s != null && !string.IsNullOrEmpty(s.id))
                        {
                            s.presentedTo ??= new List<string>();
                            _states[s.id] = s;
                        }
                if (data.facts != null) _facts.UnionWith(data.facts);
                if (data.connections != null) _connections.UnionWith(data.connections);
            }

            EventBus.Publish(new EvidenceStateChangedEvent(null, EvidenceChange.Reset));
        }
    }
}
