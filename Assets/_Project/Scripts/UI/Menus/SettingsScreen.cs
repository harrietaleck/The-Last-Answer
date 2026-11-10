using System.Collections.Generic;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Config;
using TLA.Systems;
using TLA.Systems.Config;
using TLA.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// SETTINGS (one global instance in Bootstrap, opened from the main menu or the pause menu).
    ///   CONTROLS  Mouse sensitivity · Invert Y
    ///   AUDIO     Master volume
    ///   DISPLAY   Display mode · Resolution · Quality · V-Sync · Field of view · Brightness
    ///
    /// Changes apply immediately (so the player sees the effect) and are written to settings.json
    /// when the screen closes. All values live in ConfigService.User; this screen only edits them.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        [SerializeField] private UISliderRow mouseSensitivity;
        [SerializeField] private UIOptionStepper invertY;
        [SerializeField] private UISliderRow masterVolume;
        [SerializeField] private UIOptionStepper displayMode;
        [SerializeField] private UIOptionStepper resolution;
        [SerializeField] private UIOptionStepper quality;
        [SerializeField] private UIOptionStepper vSync;
        [SerializeField] private UISliderRow fieldOfView;
        [SerializeField] private UISliderRow brightness;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        private static readonly string[] OffOn = { "OFF", "ON" };
        private static readonly string[] DisplayModeNames = { "FULLSCREEN", "BORDERLESS", "WINDOWED" };
        private static readonly int[] DisplayModeValues = { (int)FullScreenMode.ExclusiveFullScreen, (int)FullScreenMode.FullScreenWindow, (int)FullScreenMode.Windowed };

        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        private ConfigService _config;

        protected override void Awake()
        {
            base.Awake();
            resetButton.onClick.AddListener(OnReset);
            backButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SettingsRequestedEvent>(OnRequested);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SettingsRequestedEvent>(OnRequested);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDestroy()
        {
            resetButton.onClick.RemoveListener(OnReset);
            backButton.onClick.RemoveListener(Close);
        }

        private void OnRequested(SettingsRequestedEvent evt) => Show();

        // e.g. Esc resumes the game while Settings is open from the pause menu: close too.
        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (IsVisible) Close();
        }

        protected override void OnShown()
        {
            if (!Services.TryGet(out _config)) return;
            BindAll();
        }

        public void Close()
        {
            if (!IsVisible) return;
            _config?.SaveUserSettings(); // write to disk once, when leaving the screen
            Hide();
            EventBus.Publish(new SettingsClosedEvent());
        }

        private void BindAll()
        {
            UserSettings u = _config.User;

            mouseSensitivity.Bind("MOUSE SENSITIVITY", 0.1f, 3f, u.mouseSensitivity, v => v.ToString("0.00") + "×", v => Change(s => s.mouseSensitivity = v));
            invertY.Bind("INVERT LOOK", OffOn, u.invertY ? 1 : 0, i => Change(s => s.invertY = i == 1));
            masterVolume.Bind("MASTER VOLUME", 0f, 1f, u.masterVolume, v => Mathf.RoundToInt(v * 100f) + "%", v => Change(s => s.masterVolume = v));

            int modeIndex = System.Array.IndexOf(DisplayModeValues, u.displayMode >= 0 ? u.displayMode : (int)Screen.fullScreenMode);
            displayMode.Bind("DISPLAY MODE", DisplayModeNames, Mathf.Max(0, modeIndex), i => Change(s => s.displayMode = DisplayModeValues[i]));

            BuildResolutions(out List<string> resNames, out int resIndex);
            resolution.Bind("RESOLUTION", resNames, resIndex, i => Change(s =>
            {
                s.resolutionWidth = _resolutions[i].x;
                s.resolutionHeight = _resolutions[i].y;
            }));

            string[] qualityNames = QualitySettings.names;
            for (int i = 0; i < qualityNames.Length; i++) qualityNames[i] = qualityNames[i].ToUpperInvariant();
            quality.Bind("GRAPHICS QUALITY", qualityNames, u.qualityLevel >= 0 ? u.qualityLevel : QualitySettings.GetQualityLevel(), i => Change(s => s.qualityLevel = i));

            vSync.Bind("V-SYNC", OffOn, u.vSync ? 1 : 0, i => Change(s => s.vSync = i == 1));
            fieldOfView.Bind("FIELD OF VIEW", UserSettings.MinFieldOfView, UserSettings.MaxFieldOfView, u.fieldOfView, v => Mathf.RoundToInt(v) + "°", v => Change(s => s.fieldOfView = v), true);
            brightness.Bind("BRIGHTNESS", -1f, 1f, u.brightness, v => (v >= 0 ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%", v => Change(s => s.brightness = v));
        }

        private void BuildResolutions(out List<string> names, out int currentIndex)
        {
            _resolutions.Clear();
            names = new List<string>();
            foreach (Resolution r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!_resolutions.Contains(size)) _resolutions.Add(size);
            }
            if (_resolutions.Count == 0) _resolutions.Add(new Vector2Int(Screen.width, Screen.height));

            int w = _config.User.resolutionWidth > 0 ? _config.User.resolutionWidth : Screen.currentResolution.width;
            int h = _config.User.resolutionHeight > 0 ? _config.User.resolutionHeight : Screen.currentResolution.height;
            currentIndex = _resolutions.Count - 1; // default: largest
            for (int i = 0; i < _resolutions.Count; i++)
            {
                names.Add($"{_resolutions[i].x} × {_resolutions[i].y}");
                if (_resolutions[i].x == w && _resolutions[i].y == h) currentIndex = i;
            }
        }

        /// <summary>Edits the settings, then applies and broadcasts them (without writing the file yet).</summary>
        private void Change(System.Action<UserSettings> edit)
        {
            if (_config == null) return;
            edit(_config.User);
            _config.PreviewUserSettings();
        }

        private void OnReset()
        {
            if (_config == null) return;
            _config.ResetUserSettings();
            BindAll();
        }
    }
}
