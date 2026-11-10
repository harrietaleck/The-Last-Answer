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
