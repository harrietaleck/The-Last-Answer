using System.Threading;
using System.Threading.Tasks;

namespace TLA.Systems.Dialogue
{
    /// <summary>Why a provider couldn't answer. Used for logs, fallback decisions and tests.</summary>
    public enum AIFailureKind
    {
        None = 0,
        NotConfigured,   // Online mode chosen but no backend URL (or an unsafe one)
        Offline,         // no internet / backend unreachable
        Timeout,         // no reply in time
        ServerError,     // backend answered with an error status (4xx/5xx)
        RateLimited,     // backend said "too many requests" (429)
        InvalidResponse, // reply wasn't valid JSON, didn't match the schema, or failed validation
        Busy,            // a request for this provider is already running (duplicate prevented)
        Paused,          // too many failures recently: not trying online for a while
        Cancelled,       // the interview ended or the turn was cancelled
        Unknown
    }

    /// <summary>What a provider sends back: the suspect's words, and optionally a stage direction.</summary>
    public sealed class AIResponse
    {
        public bool Success;

        /// <summary>The suspect's spoken line.</summary>
        public string Text;

        /// <summary>Optional narration shown before the line, e.g. "Marlowe's jaw tightens." (may be empty).</summary>
        public string Narration;

        /// <summary>Why it failed (for logs and the on-screen error).</summary>
        public string Error;

        public AIFailureKind FailureKind;

        public static AIResponse Ok(string text, string narration = null) => new AIResponse { Success = true, Text = text, Narration = narration };

        public static AIResponse Fail(string error, AIFailureKind kind = AIFailureKind.Unknown) =>
            new AIResponse { Success = false, Error = error, FailureKind = kind };
    }

    /// <summary>
    /// Anything that can phrase a suspect's reply. The dialogue system has already decided WHAT the
    /// suspect does (see <see cref="DialogueContext.Plan"/>); a provider only decides the WORDS.
    /// A provider can't change case state: it returns text, nothing else, and DialogueSession
    /// applies only the state changes the C# rules planned.
    ///
    /// Implementations:
    ///   MockAIProvider   offline, authored lines (works immediately, no internet, no model)
    ///   OnlineAIProvider sends a filtered context to OUR backend, which talks to an AI service
    /// Chosen through <see cref="AIProviderFactory"/>; the UI and rules never change.
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>Short id for logs and settings, e.g. "mock", "online".</summary>
        string ProviderId { get; }

        Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken cancellationToken);
    }
}
