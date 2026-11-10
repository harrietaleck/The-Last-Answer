using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Components
{
    /// <summary>
    /// Settings row: "LABEL ──────●── 75%". Call <see cref="Bind"/> with the range, the current value,
    /// how to format it and what to do when it changes. Reusable for any numeric setting.
    /// </summary>
    public sealed class UISliderRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text valueText;

        private Func<float, string> _format;
        private Action<float> _onChanged;

        public Slider Slider => slider;

        public void Bind(string title, float min, float max, float value, Func<float, string> format, Action<float> onChanged, bool wholeNumbers = false)
        {
            _format = format;
            _onChanged = null; // don't fire while initialising
            if (label != null) label.text = title;
            slider.wholeNumbers = wholeNumbers;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            UpdateValueText(value);
            _onChanged = onChanged;
        }

        private void Awake()
        {
            slider.onValueChanged.AddListener(OnSliderChanged);
            slider.onValueChanged.AddListener(_ => UISound.Play(TLA.Data.Audio.SoundCue.UISlider)); // rate-limited by its cooldown
        }

        private void OnDestroy()
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        private void OnSliderChanged(float value)
        {
            UpdateValueText(value);
            _onChanged?.Invoke(value);
        }

        private void UpdateValueText(float value)
        {
            if (valueText != null) valueText.text = _format != null ? _format(value) : value.ToString("0.00");
        }
    }
}
