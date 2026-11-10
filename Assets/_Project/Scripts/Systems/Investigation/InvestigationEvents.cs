using TLA.Core.Events;
using TLA.Data.Cases;

namespace TLA.Systems.Investigation
{
    // Events raised by CaseSessionService. The HUD, notebook and notifications listen to these;
    // they never call the investigation code directly.

    /// <summary>A case was started (new investigation) or restored from a save.</summary>
    public readonly struct CaseStartedEvent : IGameEvent
    {
        public readonly CaseDefinition Case;
        public CaseStartedEvent(CaseDefinition caseDefinition) { Case = caseDefinition; }
    }

    /// <summary>The current objective text changed.</summary>
    public readonly struct ObjectiveChangedEvent : IGameEvent
    {
        public readonly string Objective;
        public ObjectiveChangedEvent(string objective) { Objective = objective; }
    }

    /// <summary>A NEW piece of evidence was added (not raised for duplicates).</summary>
    public readonly struct EvidenceAddedEvent : IGameEvent
    {
        public readonly EvidenceRecord Evidence;
        public readonly int TotalFound;
        public EvidenceAddedEvent(EvidenceRecord evidence, int totalFound) { Evidence = evidence; TotalFound = totalFound; }
    }

    /// <summary>A note was added to the investigation notebook.</summary>
    public readonly struct NoteAddedEvent : IGameEvent
    {
        public readonly NoteRecord Note;
        public NoteAddedEvent(NoteRecord note) { Note = note; }
    }

    /// <summary>Generic on-screen notification (e.g. "Game saved"). Anything may publish this.</summary>
    public readonly struct NotificationEvent : IGameEvent
    {
        public readonly string Title;
        public readonly string Message;
        public NotificationEvent(string title, string message) { Title = title; Message = message; }
    }
}

namespace TLA.Systems.Investigation
{
    /// <summary>What changed about a piece of evidence.</summary>
    public enum EvidenceChange
    {
        Discovered,
        Collected,
        Examined,
        Presented,
        Connected,
        Reset // new case or save loaded: everything may have changed
    }

    /// <summary>Raised by EvidenceService whenever an item's state changes (UI and world pickups refresh on it).</summary>
    public readonly struct EvidenceStateChangedEvent : IGameEvent
    {
        public readonly string EvidenceId;
        public readonly EvidenceChange Change;
        public EvidenceStateChangedEvent(string evidenceId, EvidenceChange change) { EvidenceId = evidenceId; Change = change; }
    }

    /// <summary>A case fact was established (by examining or connecting evidence).</summary>
    public readonly struct FactEstablishedEvent : IGameEvent
    {
        public readonly TLA.Data.Evidence.CaseFact Fact;
        public FactEstablishedEvent(TLA.Data.Evidence.CaseFact fact) { Fact = fact; }
    }

    /// <summary>The player connected two related pieces of evidence.</summary>
    public readonly struct EvidenceConnectedEvent : IGameEvent
    {
        public readonly TLA.Data.Evidence.EvidenceDefinition First;
        public readonly TLA.Data.Evidence.EvidenceDefinition Second;
        public readonly TLA.Data.Evidence.EvidenceConnection Connection;
        public EvidenceConnectedEvent(TLA.Data.Evidence.EvidenceDefinition first, TLA.Data.Evidence.EvidenceDefinition second, TLA.Data.Evidence.EvidenceConnection connection)
        {
            First = first;
            Second = second;
            Connection = connection;
        }
    }
}

namespace TLA.Systems.Investigation
{
    /// <summary>A suspect statement was recorded in the notebook (or a new quote was added to it).</summary>
    public readonly struct StatementRecordedEvent : IGameEvent
    {
        public readonly TLA.Data.Investigation.SuspectStatement Statement;
        public readonly bool FirstTime;
        public StatementRecordedEvent(TLA.Data.Investigation.SuspectStatement statement, bool firstTime) { Statement = statement; FirstTime = firstTime; }
    }

    /// <summary>
    /// The contradiction checker judged a statement against a piece of evidence (in the notebook or
    /// in an interview). The feedback banner and notebook show it.
    /// </summary>
    public readonly struct ContradictionEvaluatedEvent : IGameEvent
    {
        public readonly ContradictionResult Result;
        public readonly bool InInterview;
        public ContradictionEvaluatedEvent(ContradictionResult result, bool inInterview) { Result = result; InInterview = inInterview; }
    }

    /// <summary>The player recorded an observation in the notebook.</summary>
    public readonly struct ObservationAddedEvent : IGameEvent
    {
        public readonly ObservationRecord Observation;
        public ObservationAddedEvent(ObservationRecord observation) { Observation = observation; }
    }

    /// <summary>Anything that changes investigation progress (statements, contradictions, observations).</summary>
    public readonly struct InvestigationChangedEvent : IGameEvent
    {
    }
}
