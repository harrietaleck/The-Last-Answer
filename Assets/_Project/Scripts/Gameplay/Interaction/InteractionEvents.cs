using TLA.Core.Events;

namespace TLA.Gameplay.Interaction
{
    /// <summary>
    /// Raised when the object the player is looking at changes (or its prompt changes, e.g. a door
    /// going from "Open" to "Close"). The HUD listens to this to show or hide the prompt.
    /// </summary>
    public readonly struct InteractionFocusChangedEvent : IGameEvent
    {
        /// <summary>True if the player is looking at something they can interact with.</summary>
        public readonly bool HasTarget;

        /// <summary>Full prompt text including the key, e.g. "[E] Open". Empty when there's no target.</summary>
        public readonly string PromptText;

        public InteractionFocusChangedEvent(bool hasTarget, string promptText)
        {
            HasTarget = hasTarget;
            PromptText = promptText;
        }
    }
}
