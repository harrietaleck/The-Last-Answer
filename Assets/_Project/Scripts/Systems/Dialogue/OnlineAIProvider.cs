using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TLA.Systems.Dialogue.Online;

namespace TLA.Systems.Dialogue
{
    /// <summary>Settings for <see cref="OnlineAIProvider"/> (filled from GameConfig → AI).</summary>
    public sealed class OnlineProviderOptions
    {
        public string BackendBaseUrl = "";
        public float TimeoutSeconds = 10f;
        public int MaxReplyCharacters = 320;
        public int MaxNarrationCharacters = 120;
        public int HistoryLines = 12;
        public int FailuresBeforePause = 2;
        public float PauseSeconds = 60f;
    }

    /// <summary>
    /// Sends the dialogue context to OUR backend (never directly to an AI vendor), which holds the
    /// API key, chooses the AI service and model, and returns the suspect's words.
    ///
    /// Safety and reliability:
    ///  - The request carries only what this reply needs (see <see cref="DialogueRequestBuilder"/>):
    ///    no hidden truths, no unrevealed evidence, no other topics' answers.
    ///  - The reply is validated (<see cref="ResponseValidator"/>): schema, requestId, length,
    ///    markup, out-of-character text, invented names and numbers. Failing replies are discarded.
    ///  - Timeouts, no internet, server errors, rate limits and bad replies all return a failure,
    ///    and <see cref="DialogueSession"/> lets the offline mock answer instead.
    ///  - One request at a time: a second call while one is running fails as Busy (no duplicates).
    ///  - After repeated failures it pauses itself for a while, so the player isn't kept waiting
    ///    for a timeout on every question while offline.
    ///  - The reply can only contain words. Evidence, progress, contradictions and endings are
    ///    decided by the C# rules before the request is even sent.
    /// No API key, vendor or model name exists anywhere in the game.
    /// </summary>
    public sealed class OnlineAIProvider : IAIProvider
    {
        private readonly OnlineProviderOptions _options;
        private readonly IDialogueTransport _transport;
        private readonly Func<double> _clock;
        private readonly string _endpoint;
        private readonly string _configProblem;

        private int _inFlight;          // 0 or 1 (Interlocked)
        private int _consecutiveFailures;
        private double _pausedUntil;

        public OnlineAIProvider(OnlineProviderOptions options, IDialogueTransport transport = null, Func<double> clock = null)
        {
            _options = options ?? new OnlineProviderOptions();
            _transport = transport ?? new UnityWebRequestTransport();
            _clock = clock ?? (() => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
            _endpoint = BackendUrl.ToEndpoint(_options.BackendBaseUrl, out _configProblem);
        }

        /// <summary>Convenience constructor: default limits, real network.</summary>
        public OnlineAIProvider(string backendBaseUrl) : this(new OnlineProviderOptions { BackendBaseUrl = backendBaseUrl })
        {
        }

        public string ProviderId => "online";

        /// <summary>True if the backend address is usable (set, valid, https unless localhost).</summary>
        public bool IsConfigured => _endpoint != null;

        /// <summary>Why the address is unusable (null when configured).</summary>
        public string ConfigurationProblem => _configProblem;

        /// <summary>True while paused after repeated failures.</summary>
        public bool IsPaused => _clock() < _pausedUntil;

        /// <summary>The last failure, for logs and the debug overlay.</summary>
        public AIFailureKind LastFailure { get; private set; }

        public async Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken cancellationToken)
        {
            if (_endpoint == null) return AIResponse.Fail(_configProblem, AIFailureKind.NotConfigured);
            if (context == null) return AIResponse.Fail("No dialogue context.", AIFailureKind.Unknown);
            if (IsPaused) return AIResponse.Fail("Online dialogue paused after repeated failures.", AIFailureKind.Paused);
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
                return AIResponse.Fail("A reply is already being generated.", AIFailureKind.Busy);

            try
            {
                AIResponse response = await SendAsync(context, cancellationToken);
                Record(response);
                return response;
            }
            finally
            {
                Interlocked.Exchange(ref _inFlight, 0);
            }
        }

        private async Task<AIResponse> SendAsync(DialogueContext context, CancellationToken ct)
        {
            DialogueRequestDto request = DialogueRequestBuilder.Build(
                context, Guid.NewGuid().ToString("N"), _options.MaxReplyCharacters, _options.MaxNarrationCharacters, _options.HistoryLines);
            string json = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            TransportResult result;
            try
            {
                result = await _transport.PostJsonAsync(_endpoint, json, _options.TimeoutSeconds, ct);
            }
            catch (OperationCanceledException)
            {
                return AIResponse.Fail("Cancelled.", AIFailureKind.Cancelled);
            }
            catch (Exception e)
            {
                return AIResponse.Fail("Network error: " + e.Message, AIFailureKind.Offline);
            }

            if (ct.IsCancellationRequested && result.Status != TransportStatus.Success)
                return AIResponse.Fail("Cancelled.", AIFailureKind.Cancelled);

            switch (result.Status)
            {
                case TransportStatus.Success:
                    break;
                case TransportStatus.Offline:
                    return AIResponse.Fail("Can't reach the dialogue server: " + result.Error, AIFailureKind.Offline);
                case TransportStatus.Timeout:
                    return AIResponse.Fail("The dialogue server took too long to answer.", AIFailureKind.Timeout);
                case TransportStatus.Cancelled:
                    return AIResponse.Fail("Cancelled.", AIFailureKind.Cancelled);
                default:
                    if (result.HttpCode == 429) return AIResponse.Fail("The dialogue server is busy (rate limited).", AIFailureKind.RateLimited);
                    return AIResponse.Fail($"The dialogue server returned an error ({result.HttpCode}).", AIFailureKind.ServerError);
            }

            ValidationResult valid = ResponseValidator.Validate(result.Body, request, _options.MaxReplyCharacters, _options.MaxNarrationCharacters);
            if (!valid.IsValid) return AIResponse.Fail("Rejected reply: " + valid.Reason, AIFailureKind.InvalidResponse);

            return AIResponse.Ok(valid.Text, valid.Narration);
        }

        private void Record(AIResponse response)
        {
            if (response.Success)
            {
                _consecutiveFailures = 0;
                LastFailure = AIFailureKind.None;
                return;
            }

            LastFailure = response.FailureKind;
            // Cancelling (leaving the interview) isn't the server's fault.
            if (response.FailureKind == AIFailureKind.Cancelled) return;

            _consecutiveFailures++;
            if (_consecutiveFailures >= Math.Max(1, _options.FailuresBeforePause))
            {
                _pausedUntil = _clock() + _options.PauseSeconds;
                _consecutiveFailures = 0;
            }
        }
    }
}
