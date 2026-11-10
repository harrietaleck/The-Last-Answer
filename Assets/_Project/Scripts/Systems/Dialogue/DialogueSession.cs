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
        /// <summary>Why the main provider failed (None if it answered).</summary>
        public AIFailureKind ProviderFailure;
        /// <summary>The main provider's error message, for logs (null if it answered).</summary>
        public string ProviderError;
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

        private IAIProvider _provider;
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

        /// <summary>The case this interview belongs to (only its public details are ever sent online).</summary>
        public CaseDefinition Case { get; set; }

        /// <summary>The provider that phrases replies. Can be swapped between turns (e.g. the player switches AI mode).</summary>
        public IAIProvider Provider
        {
            get => _provider;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (IsBusy) throw new InvalidOperationException("Can't change provider during a turn.");
                _provider = value;
            }
        }

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

        /// <summary>
        /// False when the game's investigation system decides contradictions (it calls <see cref="ChallengeAsync"/>
        /// for those); true for stand-alone use, where a topic's contradictedByEvidence exposes it.
        /// </summary>
        public bool DetectContradictionsOnPresent { get; set; } = true;

        /// <summary>The player shows the suspect a piece of evidence they've discovered.</summary>
        public Task<DialogueTurn> PresentEvidenceAsync(string evidenceId, string evidenceName, ICollection<string> discoveredEvidence, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(evidenceId)) return Task.FromResult(DialogueTurn.Failed("Choose a piece of evidence."));
            if (IsBusy) return Task.FromResult(DialogueTurn.Failed($"Wait for {Suspect.ShortName} to answer."));

            ResponsePlan plan = ResponsePlanner.PlanEvidence(evidenceId, evidenceName, Knowledge, this, DetectContradictionsOnPresent);
            var player = new DialogueMessage(DialogueSpeaker.System, "", $"You show {Suspect.ShortName}: {evidenceName}.");
            return RunAsync(player, new DialogueIntent(IntentKind.Unknown, plan.Topic), plan, discoveredEvidence, ct);
        }

        /// <summary>
        /// The player challenges one of the suspect's statements (about <paramref name="topicId"/>) with evidence.
        /// <paramref name="verdict"/> comes from the investigation system's ContradictionChecker; the suspect's
        /// reply follows it (admission / evasion / brushed off), and the AI provider only chooses the words.
        /// </summary>
        public Task<DialogueTurn> ChallengeAsync(string topicId, string claim, string evidenceId, string evidenceName, ChallengeVerdict verdict,
            ICollection<string> discoveredEvidence, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(evidenceId)) return Task.FromResult(DialogueTurn.Failed("Choose a piece of evidence."));
            if (IsBusy) return Task.FromResult(DialogueTurn.Failed($"Wait for {Suspect.ShortName} to answer."));

            KnowledgeTopic topic = Knowledge != null ? Knowledge.FindTopic(topicId) : null;
            ResponsePlan plan = ResponsePlanner.PlanChallenge(topic, evidenceId, evidenceName, verdict, this);
            string said = string.IsNullOrWhiteSpace(claim) ? "" : $" \"{claim}\"";
            var player = new DialogueMessage(DialogueSpeaker.System, "", $"You challenge {Suspect.ShortName}'s statement{said} with: {evidenceName}.");
            return RunAsync(player, new DialogueIntent(IntentKind.Challenge, topic), plan, discoveredEvidence, ct);
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
                    Case = Case,
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
                AIFailureKind failure = response != null && !response.Success ? response.FailureKind : AIFailureKind.None;
                string providerError = response != null && !response.Success ? response.Error : null;
                if ((response == null || !response.Success) && _fallback != null && _fallback != _provider)
                {
                    // The fallback ignores the (possibly timed-out) token: the offline mock always gets its chance.
                    response = await TryProvider(_fallback, context, CancellationToken.None);
                    usedFallback = response != null && response.Success;
                }

                if (response == null || !response.Success || string.IsNullOrWhiteSpace(response.Text))
                    return new DialogueTurn { Success = false, Error = $"{Suspect.ShortName} didn't respond. Try asking again.", PlayerMessage = player, Plan = plan, Intent = intent, ProviderFailure = failure, ProviderError = providerError };

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

                var turn = new DialogueTurn { Success = true, PlayerMessage = player, Plan = plan, Intent = intent, UsedFallback = usedFallback, ProviderFailure = failure, ProviderError = providerError };
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
                return AIResponse.Fail("Cancelled.", AIFailureKind.Cancelled);
            }
            catch (Exception e)
            {
                return AIResponse.Fail(e.Message, AIFailureKind.Unknown);
            }
        }

        private DialogueMessage Add(DialogueMessage message)
        {
            message.Index = _history.Count;
            _history.Add(message);
            return message;
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > 100 ? 100 : value;

        // ------------------------------------------------------------------ saving

        /// <summary>Everything about this interview as plain data (for save files).</summary>
        public DialogueSessionSnapshot CaptureSnapshot()
        {
            var snap = new DialogueSessionSnapshot
            {
                suspectId = Suspect.suspectId,
                composure = Composure,
                turn = _turn,
                lastTopicId = _lastTopicId,
                asked = new Dictionary<string, int>(_asked),
                contradicted = new List<string>(_contradicted),
                presented = new List<string>(_presented),
                variants = new Dictionary<string, int>(_variants)
            };
            foreach (DialogueMessage m in _history)
                snap.history.Add(new DialogueSessionSnapshot.Line { speaker = m.Speaker, speakerName = m.SpeakerName, text = m.Text, topicId = m.TopicId });
            return snap;
        }

        /// <summary>Puts a saved interview back (history, composure, topics asked and exposed, evidence shown).</summary>
        public void RestoreSnapshot(DialogueSessionSnapshot snap)
        {
            if (snap == null) return;
            if (IsBusy) throw new InvalidOperationException("Can't restore an interview during a turn.");
            _history.Clear();
            if (snap.history != null)
                foreach (DialogueSessionSnapshot.Line l in snap.history)
                    if (l != null) Add(new DialogueMessage(l.speaker, l.speakerName ?? "", l.text ?? "", l.topicId));
            _asked.Clear();
            if (snap.asked != null) foreach (var kv in snap.asked) _asked[kv.Key] = Math.Max(0, kv.Value);
            _contradicted.Clear();
            if (snap.contradicted != null) _contradicted.UnionWith(snap.contradicted);
            _presented.Clear();
            if (snap.presented != null) _presented.UnionWith(snap.presented);
            _variants.Clear();
            if (snap.variants != null) foreach (var kv in snap.variants) _variants[kv.Key] = Math.Max(0, kv.Value);
            _lastTopicId = snap.lastTopicId;
            _turn = Math.Max(0, snap.turn);
            Composure = Clamp(snap.composure);
        }
    }

    /// <summary>A saved interview: plain data only.</summary>
    [Serializable]
    public sealed class DialogueSessionSnapshot
    {
        [Serializable]
        public sealed class Line
        {
            public DialogueSpeaker speaker;
            public string speakerName;
            public string text;
            public string topicId;
        }

        public string suspectId;
        public int composure;
        public int turn;
        public string lastTopicId;
        public List<Line> history = new List<Line>();
        public Dictionary<string, int> asked = new Dictionary<string, int>();
        public List<string> contradicted = new List<string>();
        public List<string> presented = new List<string>();
        public Dictionary<string, int> variants = new Dictionary<string, int>();
    }
}
