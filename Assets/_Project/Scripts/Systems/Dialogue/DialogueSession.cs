using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    /// <summary>Result of one turn (a question, an evidence presentation or the greeting).</summary>
    public sealed class DialogueTurn
    {
        public bool Success;
        /// <summary>Player-facing error (empty input, busy, no reply). Null on success.</summary>
        public string Error;
        public DialogueMessage PlayerMessage;
        /// <summary>Narration (optional) + the suspect's line, in display order.</summary>
        public readonly List<DialogueMessage> Responses = new List<DialogueMessage>();
        public ResponsePlan Plan;
        public DialogueIntent Intent;
        /// <summary>True when the main provider failed and the mock answered instead.</summary>
        public bool UsedFallback;
        /// <summary>True when this turn exposed a lie with evidence.</summary>
        public bool ExposedContradiction => Success && Plan != null && Plan.Kind == ResponseKind.EvidenceContradiction;

        public static DialogueTurn Failed(string error) => new DialogueTurn { Success = false, Error = error };
    }

    /// <summary>
    /// One suspect's interview: conversation history plus state (composure, topics asked, topics
    /// exposed, evidence shown). Orchestrates a turn: validate → match intent → plan with the rules →
    /// build context → ask the provider (fallback to mock on failure) → apply state → record history.
    ///
    /// Pure C# with no Unity scene dependencies, so it's unit-tested directly.
    /// </summary>
    public sealed class DialogueSession : ResponsePlanner.IState
    {
        public const int MaxInputLength = 200;

        private readonly IAIProvider _provider;
        private readonly IAIProvider _fallback;
        private readonly List<DialogueMessage> _history = new List<DialogueMessage>();
        private readonly Dictionary<string, int> _asked = new Dictionary<string, int>();
        private readonly HashSet<string> _contradicted = new HashSet<string>();
        private readonly HashSet<string> _presented = new HashSet<string>();
        private readonly Dictionary<string, int> _variants = new Dictionary<string, int>();
        private string _lastTopicId;
        private int _turn;

        public DialogueSession(SuspectProfile suspect, IAIProvider provider, IAIProvider fallback = null)
        {
            Suspect = suspect ?? throw new ArgumentNullException(nameof(suspect));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _fallback = fallback;
            Composure = suspect.baseComposure;
        }

        public SuspectProfile Suspect { get; }
        public SuspectKnowledge Knowledge => Suspect.knowledge;
        public IReadOnlyList<DialogueMessage> History => _history;
        public int Composure { get; private set; }
        public ComposureLevel Level => DialogueContext.LevelFor(Composure);
        public bool IsBusy { get; private set; }
        public bool HasStarted => _history.Count > 0;
        public IReadOnlyCollection<string> PresentedEvidence => _presented;

        public string InvestigatorName { get; set; } = "You";

        // ---- ResponsePlanner.IState
        public int TimesAsked(string topicId) => topicId != null && _asked.TryGetValue(topicId, out int n) ? n : 0;
        public bool IsContradicted(string topicId) => topicId != null && _contradicted.Contains(topicId);
        public bool WasPresented(string evidenceId) => _presented.Contains(evidenceId);
        public int NextVariant(string bank)
        {
            _variants.TryGetValue(bank, out int n);
            _variants[bank] = n + 1;
            return n;
        }

        /// <summary>The suspect's opening line (call once when the interview first starts).</summary>
        public Task<DialogueTurn> BeginAsync(CancellationToken ct = default)
        {
            var plan = new ResponsePlan { Kind = ResponseKind.Greeting };
            return RunAsync(null, new DialogueIntent(IntentKind.Greeting), plan, Array.Empty<string>(), ct);
        }

        /// <summary>The player asks something (typed text, or a suggestion's text + id).</summary>
        public Task<DialogueTurn> AskAsync(string input, string suggestionId, ICollection<string> discoveredEvidence, CancellationToken ct = default)
        {
            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0) return Task.FromResult(DialogueTurn.Failed("Type a question first."));
            if (text.Length > MaxInputLength) text = text.Substring(0, MaxInputLength);
            if (IsBusy) return Task.FromResult(DialogueTurn.Failed($"Wait for {Suspect.ShortName} to answer."));

            DialogueIntent intent = IntentMatcher.Match(text, suggestionId, Knowledge, _lastTopicId);
            ResponsePlan plan = ResponsePlanner.PlanQuestion(intent, this);
            var player = new DialogueMessage(DialogueSpeaker.Investigator, InvestigatorName, text, intent.Topic?.topicId);
            return RunAsync(player, intent, plan, discoveredEvidence, ct);
        }

        /// <summary>The player shows the suspect a piece of evidence they've discovered.</summary>
        public Task<DialogueTurn> PresentEvidenceAsync(string evidenceId, string evidenceName, ICollection<string> discoveredEvidence, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(evidenceId)) return Task.FromResult(DialogueTurn.Failed("Choose a piece of evidence."));
            if (IsBusy) return Task.FromResult(DialogueTurn.Failed($"Wait for {Suspect.ShortName} to answer."));

            ResponsePlan plan = ResponsePlanner.PlanEvidence(evidenceId, evidenceName, Knowledge, this);
            var player = new DialogueMessage(DialogueSpeaker.System, "", $"You show {Suspect.ShortName}: {evidenceName}.");
            return RunAsync(player, new DialogueIntent(IntentKind.Unknown, plan.Topic), plan, discoveredEvidence, ct);
        }

        public List<SuggestedQuestion> GetSuggestions(ICollection<string> discoveredEvidence, int max = SuggestionBuilder.DefaultMax) =>
            SuggestionBuilder.Build(Knowledge, this, discoveredEvidence, max);

        private async Task<DialogueTurn> RunAsync(DialogueMessage player, DialogueIntent intent, ResponsePlan plan,
            ICollection<string> discovered, CancellationToken ct)
        {
            IsBusy = true;
            try
            {
                int newComposure = Clamp(Composure + plan.ComposureDelta);
                var context = new DialogueContext
                {
                    Suspect = Suspect,
                    Knowledge = Knowledge,
                    PlayerInput = player?.Text ?? string.Empty,
                    Intent = intent,
                    Plan = plan,
                    History = _history,
                    PresentedEvidence = _presented,
                    DiscoveredEvidence = discovered as IReadOnlyCollection<string> ?? new List<string>(discovered ?? Array.Empty<string>()),
                    Composure = newComposure,
                    Level = DialogueContext.LevelFor(newComposure),
                    TurnNumber = _turn
                };

                // The player's line is part of the conversation even if the reply then fails.
                if (player != null) Add(player);

                AIResponse response = await TryProvider(_provider, context, ct);
                bool usedFallback = false;
                if ((response == null || !response.Success) && _fallback != null)
                {
                    // The fallback ignores the (possibly timed-out) token: the offline mock always gets its chance.
                    response = await TryProvider(_fallback, context, CancellationToken.None);
                    usedFallback = response != null && response.Success;
                }

                if (response == null || !response.Success || string.IsNullOrWhiteSpace(response.Text))
                    return new DialogueTurn { Success = false, Error = $"{Suspect.ShortName} didn't respond. Try asking again.", PlayerMessage = player, Plan = plan, Intent = intent };

                // Success: apply the rules' state changes, then record the reply.
                Composure = newComposure;
                if (plan.MarksTopicAsked && plan.Topic != null) _asked[plan.Topic.topicId] = TimesAsked(plan.Topic.topicId) + 1;
                if (plan.MarksTopicContradicted && plan.Topic != null)
                {
                    // One piece of evidence can expose several related lies (e.g. "I was home" AND "it wasn't me on camera").
                    _contradicted.Add(plan.Topic.topicId);
                    foreach (KnowledgeTopic t in Knowledge.topics)
                        if (t?.contradictedByEvidence != null && Array.IndexOf(t.contradictedByEvidence, plan.EvidenceId) >= 0)
                            _contradicted.Add(t.topicId);
                }
                if (plan.MarksEvidencePresented && !string.IsNullOrEmpty(plan.EvidenceId)) _presented.Add(plan.EvidenceId);
                if (plan.Topic != null) _lastTopicId = plan.Topic.topicId;
                _turn++;

                var turn = new DialogueTurn { Success = true, PlayerMessage = player, Plan = plan, Intent = intent, UsedFallback = usedFallback };
                if (!string.IsNullOrWhiteSpace(response.Narration))
                    turn.Responses.Add(Add(new DialogueMessage(DialogueSpeaker.System, "", response.Narration)));
                turn.Responses.Add(Add(new DialogueMessage(DialogueSpeaker.Suspect, Suspect.displayName, response.Text, plan.Topic?.topicId)));
                return turn;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static async Task<AIResponse> TryProvider(IAIProvider provider, DialogueContext context, CancellationToken ct)
        {
            try
            {
                return await provider.GenerateResponseAsync(context, ct);
            }
            catch (OperationCanceledException)
            {
                return AIResponse.Fail("Cancelled.");
            }
            catch (Exception e)
            {
                return AIResponse.Fail(e.Message);
            }
        }

        private DialogueMessage Add(DialogueMessage message)
        {
            message.Index = _history.Count;
            _history.Add(message);
            return message;
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }
}
