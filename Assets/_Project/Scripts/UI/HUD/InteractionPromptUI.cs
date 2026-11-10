using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Gameplay.Interaction;
using TLA.Systems;
using TLA.Systems.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.HUD
{
    /// <summary>
    /// The small first-person HUD: a centre dot (crosshair) and an interaction prompt such as
    /// "[E] Open" that fades in when the player looks at something usable.
    ///
    /// It only LISTENS to events (<see cref="InteractionFocusChangedEvent"/>, <see cref="GameStateChangedEvent"/>)
    /// and never calls gameplay code, so the look can be redesigned freely.
    /// The whole HUD hides when the player isn't investigating (paused, in a menu, in dialogue).
    /// </summary>
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [Tooltip("CanvasGroup on the whole HUD (hidden outside the Investigating state).")]
        [SerializeField] private CanvasGroup hudGroup;

        [Tooltip("CanvasGroup on the prompt (faded in/out).")]
        [SerializeField] private CanvasGroup promptGroup;

        [SerializeField] private TMP_Text promptLabel;

        [Tooltip("Centre dot. Becomes brighter while looking at something usable.")]
        [SerializeField] private Graphic crosshair;

        [SerializeField] private Color crosshairIdle = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color crosshairActive = new Color(1f, 1f, 1f, 0.95f);

        [Tooltip("How fast the prompt fades in/out (alpha per second).")]
        [Min(0.1f)] [SerializeField] private float fadeSpeed = 10f;

        private float _promptTargetAlpha;

        private void OnEnable()
        {
            EventBus.Subscribe<InteractionFocusChangedEvent>(OnFocusChanged);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            bool investigating = !Services.TryGet(out GameStateManager state) || state.Current == GameState.Investigating;
            SetHudVisible(investigating);
            ShowPrompt(false, string.Empty);
            if (promptGroup != null) promptGroup.alpha = 0f;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionFocusChangedEvent>(OnFocusChanged);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void Update()
        {
            if (promptGroup == null) return;
            // Unscaled time: the fade still finishes while the game is paused.
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, _promptTargetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        }

        private void OnFocusChanged(InteractionFocusChangedEvent evt)
        {
            ShowPrompt(evt.HasTarget, evt.PromptText);
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            SetHudVisible(evt.Current == GameState.Investigating);
        }

        private void ShowPrompt(bool visible, string text)
        {
            if (visible && promptLabel != null) promptLabel.text = text;
            _promptTargetAlpha = visible ? 1f : 0f;
            if (crosshair != null) crosshair.color = visible ? crosshairActive : crosshairIdle;
        }

        private void SetHudVisible(bool visible)
        {
            if (hudGroup != null) hudGroup.alpha = visible ? 1f : 0f;
        }
    }
}
