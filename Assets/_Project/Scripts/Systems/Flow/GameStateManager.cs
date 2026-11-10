using System.Collections.Generic;
using TLA.Core.Events;
using TLA.Core.Services;
using UnityEngine;

namespace TLA.Systems.Flow
{
    /// <summary>
    /// Keeps track of the current <see cref="GameState"/> and enforces which changes are legal.
    ///
    /// WHY a rules table: it stops whole classes of bugs, e.g. jumping from the main menu straight
    /// to an ending, or opening an interrogation while the game is paused. An illegal change is
    /// refused with a warning instead of silently breaking the game.
    ///
    /// Every successful change publishes a <see cref="GameStateChangedEvent"/>.
    /// </summary>
    public sealed class GameStateManager : IGameService
    {
        // For each state, the states it may change to. Paused is handled separately (see CanChangeTo).
        private static readonly Dictionary<GameState, GameState[]> AllowedTransitions =
            new Dictionary<GameState, GameState[]>
            {
                [GameState.Boot] = new[] { GameState.MainMenu },
                // From the menu the player can start a new game or continue a save at any point of a case.
                [GameState.MainMenu] = new[]
                {
                    GameState.Briefing, GameState.Investigating, GameState.Interrogating, GameState.Deciding
                },
                [GameState.Briefing] = new[] { GameState.Investigating, GameState.Paused, GameState.MainMenu },
                [GameState.Investigating] = new[]
                {
                    GameState.Briefing, GameState.Interrogating, GameState.Deciding, GameState.Paused, GameState.MainMenu
                },
                [GameState.Interrogating] = new[]
                {
                    GameState.Investigating, GameState.Deciding, GameState.Paused, GameState.MainMenu
                },
                [GameState.Deciding] = new[]
                {
                    GameState.Investigating, GameState.Ending, GameState.Paused, GameState.MainMenu
                },
                // After an ending: back to the menu, or straight into the next case's briefing.
                [GameState.Ending] = new[] { GameState.MainMenu, GameState.Briefing },
            };

        public GameState Current { get; private set; } = GameState.Boot;

        /// <summary>The state before the most recent change. Used by <see cref="Resume"/>.</summary>
        public GameState Previous { get; private set; } = GameState.Boot;

        public bool IsPaused => Current == GameState.Paused;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        public bool CanChangeTo(GameState next)
        {
            if (next == Current) return false;

            // While paused you can only resume (back to where you were) or quit to the menu.
            if (Current == GameState.Paused)
                return next == Previous || next == GameState.MainMenu;

            return AllowedTransitions.TryGetValue(Current, out GameState[] allowed)
                   && System.Array.IndexOf(allowed, next) >= 0;
        }

        /// <summary>Changes state if the rules allow it. Returns false (and logs a warning) otherwise.</summary>
        public bool TryChangeState(GameState next)
        {
            if (next == Current) return false;

            if (!CanChangeTo(next))
            {
                Debug.LogWarning($"[GameState] Change {Current} -> {next} is not allowed.");
                return false;
            }

            SetState(next);
            return true;
        }

        /// <summary>
        /// Changes state WITHOUT checking the rules. Reserved for the GameManager when it has
        /// authoritative information, e.g. you pressed Play directly in a gameplay scene in the
        /// Editor, so the game jumps from Boot straight to Investigating. Gameplay code should use
        /// <see cref="TryChangeState"/>.
        /// </summary>
        public void ForceState(GameState next)
        {
            if (next == Current) return;
            SetState(next);
        }

        public bool Pause()
        {
            return TryChangeState(GameState.Paused);
        }

        public bool Resume()
        {
            return IsPaused && TryChangeState(Previous);
        }

        private void SetState(GameState next)
        {
            Previous = Current;
            Current = next;
            Debug.Log($"[GameState] {Previous} -> {Current}");
            EventBus.Publish(new GameStateChangedEvent(Previous, Current));
        }
    }
}
