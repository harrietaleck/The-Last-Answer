using TLA.Core.Events;
using TLA.Systems.Flow;

namespace TLA.Systems
{
    // Events raised by the foundation systems. Subscribe with EventBus.Subscribe<T>(handler).
    // Each one is a small read-only struct describing something that has already happened.

    /// <summary>Raised after the game state changes, e.g. MainMenu -> Investigating.</summary>
    public readonly struct GameStateChangedEvent : IGameEvent
    {
        public readonly GameState Previous;
        public readonly GameState Current;

        public GameStateChangedEvent(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>Raised when a scene starts loading (show a loading screen, block input).</summary>
    public readonly struct SceneLoadStartedEvent : IGameEvent
    {
        public readonly string SceneName;

        public SceneLoadStartedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    /// <summary>Raised when a scene has finished loading and become the active scene.</summary>
    public readonly struct SceneLoadedEvent : IGameEvent
    {
        public readonly string SceneName;

        public SceneLoadedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    /// <summary>Raised after a save file has been written successfully.</summary>
    public readonly struct GameSavedEvent : IGameEvent
    {
        public readonly int Slot;

        public GameSavedEvent(int slot)
        {
            Slot = slot;
        }
    }

    /// <summary>Raised after a save file has been read and its data handed back to the systems.</summary>
    public readonly struct GameLoadedEvent : IGameEvent
    {
        public readonly int Slot;

        public GameLoadedEvent(int slot)
        {
            Slot = slot;
        }
    }

    /// <summary>Raised after the player's settings were changed and saved.</summary>
    public readonly struct UserSettingsChangedEvent : IGameEvent
    {
    }
}
