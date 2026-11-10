using UnityEngine;
using UnityEngine.EventSystems;

namespace TLA.UI.Components
{
    /// <summary>
    /// Base for any full screen or panel that can be shown and hidden (menus, settings, dialogue).
    /// Uses a CanvasGroup for a short fade and to block or allow clicks. Override OnShown/OnHidden
    /// to refresh content.
    ///
    /// Fades use unscaled time, so screens still work while the game is paused (timeScale 0).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [Tooltip("Hidden when the scene starts.")]
        [SerializeField] private bool startHidden = true;

        [Tooltip("Selected when shown, so keyboard/gamepad navigation works immediately.")]
        [SerializeField] private GameObject firstSelected;

        [SerializeField] private float fadeDuration = 0.14f;

        private CanvasGroup _group;
        private float _targetAlpha;

        public bool IsVisible { get; private set; }

        protected CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        protected virtual void Awake()
        {
            if (startHidden) SetInstant(false);
            else SetInstant(true);
        }

        protected virtual void Update()
        {
            if (Mathf.Approximately(Group.alpha, _targetAlpha)) return;
            float step = fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / fadeDuration;
            Group.alpha = Mathf.MoveTowards(Group.alpha, _targetAlpha, step);
        }

        public void Show()
        {
            if (IsVisible) return;
            IsVisible = true;
            _targetAlpha = 1f;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            OnShown();
            SelectFirst();
        }

        public void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;
            _targetAlpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            OnHidden();
        }

        public void SetVisible(bool visible)
        {
            if (visible) Show(); else Hide();
        }

        /// <summary>Show/hide without fading (used at startup).</summary>
        public void SetInstant(bool visible)
        {
            IsVisible = visible;
            _targetAlpha = visible ? 1f : 0f;
            Group.alpha = _targetAlpha;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
        }

        protected void SelectFirst()
        {
            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        protected virtual void OnShown() { }

        protected virtual void OnHidden() { }
    }
}
