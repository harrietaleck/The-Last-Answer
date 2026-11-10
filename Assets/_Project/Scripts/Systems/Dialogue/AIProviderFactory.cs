using TLA.Data.Config;

namespace TLA.Systems.Dialogue
{
    /// <summary>
    /// The single place that chooses a provider. Gameplay and UI never construct providers themselves.
    /// Mock is the default and the fallback. Online is used only when the player chose it AND this
    /// build has a usable backend address; otherwise the game quietly stays on Mock.
    /// </summary>
    public static class AIProviderFactory
    {
        public static IAIProvider Create(AIProviderMode mode, AIConfig config, int mockMinDelayMs = 600, int mockMaxDelayMs = 1400)
        {
            if (mode == AIProviderMode.Online && config != null)
            {
                var online = new OnlineAIProvider(OptionsFrom(config));
                if (online.IsConfigured) return online;
            }
            return new MockAIProvider(mockMinDelayMs, mockMaxDelayMs);
        }

        /// <summary>True if Online mode can work in this build (a valid backend address is set).</summary>
        public static bool IsOnlineAvailable(AIConfig config, out string problem)
        {
            problem = null;
            if (config == null)
            {
                problem = "No AI configuration.";
                return false;
            }
            return Online.BackendUrl.ToEndpoint(config.ActiveBackendUrl, out problem) != null;
        }

        public static OnlineProviderOptions OptionsFrom(AIConfig config) => new OnlineProviderOptions
        {
            BackendBaseUrl = config.ActiveBackendUrl,
            TimeoutSeconds = config.RequestTimeoutSeconds,
            MaxReplyCharacters = config.MaxReplyCharacters,
            MaxNarrationCharacters = config.MaxNarrationCharacters,
            HistoryLines = config.HistoryLinesSent,
            FailuresBeforePause = config.FailuresBeforePause,
            PauseSeconds = config.PauseAfterFailuresSeconds
        };
    }
}
