namespace TLA.Systems.Flow
{
    /// <summary>
    /// The high-level "mode" the game is in. Exactly one is active at a time.
    /// Systems check (or listen for changes to) the state to decide what is allowed, e.g.
    /// the player can only walk around while Investigating.
    /// </summary>
    public enum GameState
    {
        /// <summary>Starting up: services are being created. Nothing is playable yet.</summary>
        Boot = 0,

        /// <summary>In the main menu.</summary>
        MainMenu = 1,

        /// <summary>Reading the case briefing at HQ.</summary>
        Briefing = 2,

        /// <summary>Free first-person exploration (HQ, crime scenes).</summary>
        Investigating = 3,

        /// <summary>Questioning a suspect in the interrogation room.</summary>
        Interrogating = 4,

        /// <summary>Making the final case decision / accusation.</summary>
        Deciding = 5,

        /// <summary>Watching the ending and debrief.</summary>
        Ending = 6,

        /// <summary>Game paused (time frozen). Resuming returns to the previous state.</summary>
        Paused = 7
    }
}
