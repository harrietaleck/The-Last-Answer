using TLA.Core.Events;

namespace TLA.Systems
{
    // Small events that let UI and gameplay coordinate without referencing each other.

    /// <summary>
    /// FROM THE UI: a text box gained or lost keyboard focus. While typing, gameplay keys
    /// (E, Tab, WASD...) must be ignored so letters don't trigger actions.
    /// </summary>
    public readonly struct TextInputFocusChangedEvent : IGameEvent
    {
        public readonly bool IsTyping;
        public TextInputFocusChangedEvent(bool isTyping) { IsTyping = isTyping; }
    }

    /// <summary>FROM INPUT / MENUS: open (or close) the case notebook (J).</summary>
    public readonly struct NotebookRequestedEvent : IGameEvent
    {
    }
}

namespace TLA.Systems
{
    /// <summary>FROM INPUT / MENUS: open (or close) the evidence inventory (I).</summary>
    public readonly struct EvidenceInventoryRequestedEvent : IGameEvent
    {
    }

    /// <summary>
    /// FROM GAMEPLAY / UI: show one piece of evidence in the inspect view (read the document, look at
    /// the photo). <see cref="FromWorld"/> = picked up in the scene (offers Collect).
    /// </summary>
    public readonly struct EvidenceInspectRequestedEvent : IGameEvent
    {
        public readonly TLA.Data.Evidence.EvidenceDefinition Evidence;
        public readonly bool FromWorld;
        public EvidenceInspectRequestedEvent(TLA.Data.Evidence.EvidenceDefinition evidence, bool fromWorld) { Evidence = evidence; FromWorld = fromWorld; }
    }
}

namespace TLA.Systems
{
    /// <summary>FROM THE UI: open the "close the case" screen (choose a conclusion).</summary>
    public readonly struct ConclusionRequestedEvent : IGameEvent
    {
    }
}
