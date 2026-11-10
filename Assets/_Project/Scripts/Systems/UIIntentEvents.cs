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

    /// <summary>FROM INPUT: the player pressed the Notebook key (J). The pause menu opens on the notes page.</summary>
    public readonly struct NotebookRequestedEvent : IGameEvent
    {
    }
}
