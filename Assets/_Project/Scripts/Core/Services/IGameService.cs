namespace TLA.Core.Services
{
    /// <summary>
    /// A long-lived game system created by the GameManager (e.g. scene loading, saving, game state).
    ///
    /// Services are plain C# classes, not MonoBehaviours, wherever possible. That makes them
    /// easy to unit test and keeps them independent of scenes.
    ///
    /// Lifecycle (controlled by the GameManager):
    ///   1. constructor  - receive dependencies
    ///   2. Initialize() - called once, in registration order, after ALL services are created
    ///   3. Shutdown()   - called once, in REVERSE order, when the game closes
    /// </summary>
    public interface IGameService
    {
        void Initialize();

        void Shutdown();
    }
}
