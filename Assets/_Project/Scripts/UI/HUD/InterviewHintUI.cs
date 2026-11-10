using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Gameplay.Player;
using TLA.Systems;
using TLA.Systems.Flow;
using TMPro;
using UnityEngine;

namespace TLA.UI.HUD
{
    /// <summary>
    /// Small hint shown only while seated at the interview table ("[Tab] Change camera   [E] Stand up").
    /// Key names come from the input bindings, so they stay correct if keys are rebound.
    /// Placeholder until the dialogue UI arrives.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class InterviewHintUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            Show(Services.TryGet(out GameStateManager state) && state.Current == GameState.Interrogating);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            Show(evt.Current == GameState.Interrogating);
        }

        private void Show(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
            if (!visible || label == null) return;

            var input = FindFirstObjectByType<PlayerInputReader>();
            string next = input != null ? input.NextShotKeyName : "Tab";
            string leave = input != null ? input.LeaveKeyName : "E";
            label.text = $"[{next}] Change camera      [{leave}] Stand up";
        }
    }
}
