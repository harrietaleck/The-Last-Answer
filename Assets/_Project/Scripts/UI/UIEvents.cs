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

namespace TLA.UI
{
    /// <summary>A modal UI overlay (evidence screens) opened or closed. See <see cref="Components.UIOverlay"/>.</summary>
    public readonly struct UIOverlayChangedEvent : IGameEvent
    {
        public readonly int OpenCount;
        public UIOverlayChangedEvent(int openCount) { OpenCount = openCount; }
    }
}

namespace TLA.UI
{
    /// <summary>Open the save/load slot screen. <see cref="Save"/> = save mode, otherwise load mode.</summary>
    public readonly struct SaveSlotsRequestedEvent : IGameEvent
    {
        public readonly bool Save;
        public SaveSlotsRequestedEvent(bool save) { Save = save; }
    }

    /// <summary>The save/load slot screen closed (menus refresh their "Continue" info).</summary>
    public readonly struct SaveSlotsClosedEvent : IGameEvent
    {
    }
}
