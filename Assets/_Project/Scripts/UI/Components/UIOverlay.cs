using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Flow;
using UnityEngine;

namespace TLA.UI.Components
{
    /// <summary>
    /// Counts open modal overlays (evidence inventory, inspect view). While any is open, the pause
    /// menu stays hidden even though the game is paused behind them.
    /// </summary>
    public static class UIOverlay
    {
        public static int OpenCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OpenCount = 0;

        public static void Push()
        {
            OpenCount++;
            EventBus.Publish(new UIOverlayChangedEvent(OpenCount));
        }

        public static void Pop()
        {
            if (OpenCount == 0) return;
            OpenCount--;
            EventBus.Publish(new UIOverlayChangedEvent(OpenCount));
        }
    }

    /// <summary>
    /// A screen shown on top of gameplay that pauses the game while open: opened during
    /// investigation it pauses and resumes on close; opened from the pause menu it returns there.
    /// The game state machine stays the single source of truth (Esc resumes and closes it).
    /// </summary>
    public abstract class ModalScreen : UIScreen
    {
        private bool _resumeOnClose;
        private bool _counted;

        protected virtual void OnEnable() => EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            Release();
        }

        /// <summary>Shows the screen, pausing gameplay if needed. False if it can't open now (e.g. main menu).</summary>
        protected bool OpenModal()
        {
            if (IsVisible) return true;
            if (!Services.TryGet(out GameStateManager state)) return false;

            bool playing = state.Current == GameState.Investigating || state.Current == GameState.Interrogating;
            if (!playing && !state.IsPaused) return false;

            UIOverlay.Push();
            _counted = true;
            _resumeOnClose = playing;
            if (playing && !state.Pause())
            {
                Release();
                return false;
            }

            Show();
            UISound.Play(TLA.Data.Audio.SoundCue.UIOpen);
            return true;
        }

        public void Close()
        {
            if (!IsVisible) return;
            Hide();
            UISound.Play(TLA.Data.Audio.SoundCue.UIClose);
            bool resume = _resumeOnClose;
            _resumeOnClose = false;
            if (resume && Services.TryGet(out GameStateManager state) && state.IsPaused) state.Resume();
            Release();
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Esc / resume elsewhere: just close (the state already changed).
            if (IsVisible && evt.Current != GameState.Paused)
            {
                _resumeOnClose = false;
                Hide();
                Release();
            }
        }

        private void Release()
        {
            if (!_counted) return;
            _counted = false;
            UIOverlay.Pop();
        }
    }
}
