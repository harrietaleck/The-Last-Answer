using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TLA.UI.Components
{
    /// <summary>
    /// The game's standard button look: secondary-grey label that turns white on hover/selection,
    /// with a thin red accent bar on the left. Deliberately subtle (colour only, no bouncing).
    /// Works with mouse (hover) and keyboard/gamepad (selection). Disabled buttons are muted.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Graphic accentBar;
        [SerializeField] private Color normalColor = new Color32(0x9A, 0x9F, 0xA6, 0xFF);
        [SerializeField] private Color activeColor = new Color32(0xEE, 0xEE, 0xEF, 0xFF);
        [SerializeField] private Color disabledColor = new Color32(0x4A, 0x4E, 0x54, 0xFF);
        [SerializeField] private float transitionSpeed = 14f;

        private Button _button;
        private bool _hovered;
        private bool _selected;
        private float _active; // 0..1

        private void Awake()
        {
            _button = GetComponent<Button>();
            Apply(true);
        }

        private void OnEnable()
        {
            _hovered = _selected = false;
            _active = 0f;
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) => _hovered = false;
        public void OnSelect(BaseEventData eventData) => _selected = true;
        public void OnDeselect(BaseEventData eventData) => _selected = false;

        private void Apply(bool instant)
        {
            bool interactable = _button != null && _button.IsInteractable();
            float target = interactable && (_hovered || _selected) ? 1f : 0f;
            _active = instant ? target : Mathf.MoveTowards(_active, target, Time.unscaledDeltaTime * transitionSpeed);

            if (label != null)
                label.color = interactable ? Color.Lerp(normalColor, activeColor, _active) : disabledColor;

            if (accentBar != null)
            {
                Color c = accentBar.color;
                c.a = _active;
                accentBar.color = c;
            }
        }
    }
}
