using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Components
{
    /// <summary>
    /// Settings row for a choice from a list: "LABEL      ‹  1920 × 1080  ›".
    /// Clearer and less fiddly than a dropdown for short lists (display mode, quality, On/Off).
    /// Left/right arrow keys also step through the options when the row is selected.
    /// </summary>
    public sealed class UIOptionStepper : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        private IReadOnlyList<string> _options = Array.Empty<string>();
        private Action<int> _onChanged;
        private int _index;

        public int Index => _index;

        public void Bind(string title, IReadOnlyList<string> options, int index, Action<int> onChanged)
        {
            if (label != null) label.text = title;
            _options = options ?? Array.Empty<string>();
            _index = _options.Count == 0 ? 0 : Mathf.Clamp(index, 0, _options.Count - 1);
            _onChanged = onChanged;
            Refresh();
        }

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
        }

        private void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
        }

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        private void Step(int delta)
        {
            if (_options.Count == 0) return;
            _index = (_index + delta + _options.Count) % _options.Count; // wraps around
            Refresh();
            _onChanged?.Invoke(_index);
        }

        private void Refresh()
        {
            if (valueText != null) valueText.text = _options.Count > 0 ? _options[_index] : "-";
        }
    }
}
