using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TLA.UI.Components
{
    /// <summary>
    /// The game's standard button look. Hover (mouse) and selection (keyboard, gamepad, or the
    /// list row that is currently chosen) share one state: the label turns white, a red accent
    /// bar appears, and the plate warms. Disabled buttons stay muted.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Graphic accentBar;
        [SerializeField] private Graphic plate;
        [SerializeField] private Color normalColor = new Color32(0x9A, 0x9F, 0xA6, 0xFF);
        [SerializeField] private Color activeColor = new Color32(0xEE, 0xEE, 0xEF, 0xFF);
        [SerializeField] private Color disabledColor = new Color32(0x4A, 0x4E, 0x54, 0xFF);
        [SerializeField] private float transitionSpeed = 14f;

        private Button _button;
        private bool _hovered;
        private bool _selected;
        private bool _marked;
        private float _active;
        private Color _plateIdle;
        private Color _plateHot;
        private bool _hasPlate;
        private RectTransform _accentRect;
        private Vector2 _accentSize;

        [Tooltip("Sound when clicked (None = silent). Hovering plays a softer tick.")]
        [SerializeField] private TLA.Data.Audio.SoundCue clickSound = TLA.Data.Audio.SoundCue.UIClick;

        /// <summary>
        /// Keeps this row highlighted as the current choice (case file, evidence, save slot)
        /// even after the pointer leaves. Hover and keyboard focus still highlight on their own.
        /// </summary>
        public void SetMarked(bool marked)
        {
            _marked = marked;
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            ResolveReferences();
            PrepareHitTarget();
            _button.onClick.AddListener(PlayClick);
            CachePlate();
            Apply(true);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(PlayClick);
        }

        private void PlayClick() => UISound.Play(clickSound);

        private void OnEnable()
        {
            _hovered = false;
            EventSystem events = EventSystem.current;
            _selected = events != null && events.currentSelectedGameObject == gameObject;
            _active = IsHot ? 1f : 0f;
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            _hovered = true;
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != gameObject)
                events.SetSelectedGameObject(gameObject);
            UISound.Play(TLA.Data.Audio.SoundCue.UIHover);
        }

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        public void OnSelect(BaseEventData eventData)
        {
            bool already = _selected;
            _selected = true;
            if (!already && !_hovered && IsInteractable)
                UISound.Play(TLA.Data.Audio.SoundCue.UIHover);
        }

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        private bool IsInteractable => _button == null || _button.IsInteractable();

        private bool IsHot => IsInteractable && (_hovered || _selected || _marked);

        /// <summary>Lowest alpha that still produces a canvas mesh. 0 is omitted, so clicks miss the row.</summary>
        private const float MinHitAlpha = 1f / 255f;

        /// <summary>
        /// Menu buttons use an invisible plate. Unity omits a graphic whose alpha is exactly 0
        /// from the canvas (its depth stays -1), and the UI raycaster skips those graphics.
        /// The label only covers the glyphs, so a click on the rest of the row did nothing.
        /// A 1/255 plate is invisible and gives the whole row a click target.
        /// </summary>
        private void PrepareHitTarget()
        {
            if (plate != null)
            {
                plate.raycastTarget = true;
                plate.canvasRenderer.cullTransparentMesh = false;
                if (plate.color.a < MinHitAlpha)
                {
                    Color color = plate.color;
                    color.a = MinHitAlpha;
                    plate.color = color;
                }
            }
            if (label != null)
            {
                label.raycastTarget = true;
                label.canvasRenderer.cullTransparentMesh = false;
            }
        }

        private void ResolveReferences()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (accentBar == null)
            {
                Transform bar = transform.Find("AccentBar");
                if (bar != null) accentBar = bar.GetComponent<Graphic>();
            }
            if (accentBar != null)
            {
                _accentRect = accentBar.transform as RectTransform;
                if (_accentRect != null) _accentSize = _accentRect.sizeDelta;
            }
            if (plate == null) plate = GetComponent<Graphic>();
        }

        private void CachePlate()
        {
            _hasPlate = plate != null && plate != label;
            if (!_hasPlate) return;

            _plateIdle = plate.color;
            if (_plateIdle.a < 0.08f)
            {
                // Text-only menu buttons use an invisible hit area. Hover fills it so the row reads as selected.
                _plateHot = new Color(0.32f, 0.09f, 0.08f, 0.94f);
            }
            else
            {
                _plateHot = Color.Lerp(_plateIdle, new Color(0.55f, 0.16f, 0.14f, _plateIdle.a), 0.55f);
                _plateHot.a = Mathf.Max(_plateIdle.a, 0.92f);
            }
        }

        private void Apply(bool instant)
        {
            float target = IsHot ? 1f : 0f;
            _active = instant ? target : Mathf.MoveTowards(_active, target, Time.unscaledDeltaTime * transitionSpeed);

            if (label != null)
                label.color = IsInteractable ? Color.Lerp(normalColor, activeColor, _active) : disabledColor;

            if (accentBar != null)
            {
                Color c = new Color(0.77f, 0.12f, 0.10f, _active);
                accentBar.color = c;
            }

            if (_accentRect != null)
            {
                Vector2 size = _accentSize;
                size.x = Mathf.Lerp(0f, Mathf.Max(_accentSize.x, 4f), _active);
                _accentRect.sizeDelta = size;
            }

            if (_hasPlate)
                plate.color = Color.Lerp(_plateIdle, _plateHot, _active);
        }
    }
}
