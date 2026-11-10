using System.Threading;
using System.Threading.Tasks;

namespace TLA.Systems.Dialogue
{
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

        public static AIResponse Ok(string text, string narration = null) => new AIResponse { Success = true, Text = text, Narration = narration };
        public static AIResponse Fail(string error) => new AIResponse { Success = false, Error = error };
    }

    /// <summary>
    /// Anything that can phrase a suspect's reply. The dialogue system has already decided WHAT the
    /// suspect does (see <see cref="DialogueContext.Plan"/>); a provider only decides the WORDS.
    ///
    /// Implementations:
    ///   MockAIProvider   offline, authored lines (works immediately, no internet, no model)
    ///   OnlineAIProvider sends the context to OUR backend, which talks to an AI service (later stage)
    /// Swap them through <see cref="AIProviderFactory"/>; the UI and rules never change.
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>Short id for logs and settings, e.g. "mock", "online".</summary>
        string ProviderId { get; }

        Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken cancellationToken);
    }
}
