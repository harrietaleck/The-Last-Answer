using System;
using Newtonsoft.Json;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Config;
using TLA.Systems.Save;
using UnityEngine;

namespace TLA.Systems.Config
{
    /// <summary>
    /// One place to read configuration from:
    ///  - <see cref="Game"/>: developer settings from the GameConfig asset (read-only at runtime).
    ///  - <see cref="User"/>: the player's own settings (volume, sensitivity, AI mode), saved to disk.
    ///
    /// To change a player setting:
    /// <code>
    ///   var config = Services.Get&lt;ConfigService&gt;();
    ///   config.User.masterVolume = 0.5f;
    ///   config.SaveUserSettings();   // applies, writes to disk and raises UserSettingsChangedEvent
    /// </code>
    /// </summary>
    public sealed class ConfigService : IGameService
    {
        private readonly ISaveStorage _storage;

        public ConfigService(GameConfig gameConfig, ISaveStorage storage)
        {
            Game = gameConfig != null ? gameConfig : throw new ArgumentNullException(nameof(gameConfig));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public GameConfig Game { get; }

        public UserSettings User { get; private set; }

        public void Initialize()
        {
            LoadUserSettings();
        }

        public void Shutdown()
        {
        }

        /// <summary>Loads settings from disk, or creates defaults on first launch or if the file is unreadable.</summary>
        public void LoadUserSettings()
        {
            string key = Game.Save.SettingsFileName;
            UserSettings loaded = null;

            if (_storage.TryRead(key, out string json))
            {
                try
                {
                    loaded = JsonConvert.DeserializeObject<UserSettings>(json);
                }
                catch (JsonException exception)
                {
                    Debug.LogWarning($"[Config] Settings file is unreadable, using defaults: {exception.Message}");
                }
            }

            User = loaded ?? UserSettings.CreateDefault(Game);
            User.Sanitize();
            ApplyUserSettings();
        }

        /// <summary>Applies the current settings, saves them and notifies listeners.</summary>
        public void SaveUserSettings()
        {
            User.Sanitize();
            ApplyUserSettings();

            try
            {
                _storage.Write(Game.Save.SettingsFileName, JsonConvert.SerializeObject(User, Formatting.Indented));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Config] Could not save settings: {exception.Message}");
            }

            EventBus.Publish(new UserSettingsChangedEvent());
        }

        public void ResetUserSettings()
        {
            User = UserSettings.CreateDefault(Game);
            SaveUserSettings();
        }

        // Settings that map directly onto engine values are applied here.
        // Others (mouse sensitivity, AI mode) are read by their systems when needed.
        private void ApplyUserSettings()
        {
            AudioListener.volume = User.masterVolume;
        }
    }
}
