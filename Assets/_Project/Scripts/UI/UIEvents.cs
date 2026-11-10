using TLA.Core.Events;

namespace TLA.UI
{
    // Events used only inside the UI layer, so screens in different scenes can talk
    // (e.g. the main menu, in its own scene, opens the global Settings screen in Bootstrap).

    /// <summary>Open the Settings screen.</summary>
    public readonly struct SettingsRequestedEvent : IGameEvent
    {
    }

    /// <summary>The Settings screen was closed (the screen that opened it can take focus back).</summary>
    public readonly struct SettingsClosedEvent : IGameEvent
    {
    }
}
