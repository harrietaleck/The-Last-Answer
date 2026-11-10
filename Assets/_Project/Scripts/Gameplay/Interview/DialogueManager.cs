using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Data.Config;
using TLA.Systems;
using TLA.Systems.Config;
using TLA.Systems.Dialogue;
using TLA.Systems.Investigation;
using UnityEngine;

namespace TLA.Gameplay.Interview
{
    /// <summary>
    /// Runs interviews in a scene. It connects the UI events (questions, evidence, end interview)
    /// to a <see cref="DialogueSession"/> per suspect, and publishes the results back
    /// (lines, "thinking", suggestions, errors). It contains no dialogue rules itself.
    ///
    /// - Provider: chosen by <see cref="AIProviderFactory"/> from the player's AI mode setting,
    ///   re-checked before every turn (switching Mock/Online in Settings applies immediately).
    ///   Online failures fall back to the mock automatically, with a one-off notification.
    /// - History persists while the game runs: leave the room and come back, and the conversation
    ///   is still there (cleared when a new case starts).
    /// - Notable answers are written to the investigation notebook so they can be reviewed later.
    /// - Statements the suspect makes are recorded with the InvestigationService. Challenges (statement +
    ///   evidence) are judged by its ContradictionChecker BEFORE the suspect answers; the outcome is
    ///   applied only after a reply succeeds. The dialogue provider never decides a contradiction.
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        [Tooltip("Fake thinking time range for the mock provider, in milliseconds.")]
        [SerializeField] private Vector2Int mockThinkingMs = new Vector2Int(700, 1500);

        [Tooltip("Safety net: cancel a turn after this many seconds if the provider never returns. Online requests also have their own timeout (GameConfig → AI).")]
        [SerializeField] private float timeoutSeconds = 13f;

        // Fallback store when the game systems aren't running (e.g. a test scene). Normally interviews
        // live in the InterviewService, which also saves them with the game.
        private static readonly Dictionary<string, DialogueSession> Sessions = new Dictionary<string, DialogueSession>();

        // One provider shared by all interviews, so the online provider's "paused after failures" state carries over.
        private static IAIProvider _provider;
        private static string _providerKey;
        private static bool _fallbackNotified;

        private DialogueSession _session;
        private CancellationTokenSource _cts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sessions.Clear();
            _provider = null;
            _providerKey = null;
            _fallbackNotified = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Subscribe<InterviewEndedEvent>(OnInterviewEnded);
            EventBus.Subscribe<PlayerQuestionSubmittedEvent>(OnQuestion);
            EventBus.Subscribe<PlayerPresentedEvidenceEvent>(OnEvidence);
            EventBus.Subscribe<PlayerChallengedStatementEvent>(OnChallenge);
            EventBus.Subscribe<CaseStartedEvent>(OnCaseStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Unsubscribe<InterviewEndedEvent>(OnInterviewEnded);
            EventBus.Unsubscribe<PlayerQuestionSubmittedEvent>(OnQuestion);
            EventBus.Unsubscribe<PlayerPresentedEvidenceEvent>(OnEvidence);
            EventBus.Unsubscribe<PlayerChallengedStatementEvent>(OnChallenge);
            EventBus.Unsubscribe<CaseStartedEvent>(OnCaseStarted);
            Cancel();
        }

        /// <summary>The active session (for tests and debugging).</summary>
        public DialogueSession CurrentSession => _session;

        private void OnCaseStarted(CaseStartedEvent evt) => Sessions.Clear(); // the InterviewService resets itself per case

        private void OnInterviewStarted(InterviewStartedEvent evt)
        {
            if (evt.Suspect == null)
            {
                EventBus.Publish(new DialogueErrorEvent("No suspect is assigned to this interview."));
                return;
            }

            DialogueSession Create() => new DialogueSession(evt.Suspect, CurrentProvider(), new MockAIProvider(mockThinkingMs.x, mockThinkingMs.y))
            {
                // Contradictions are decided by the investigation system (see OnEvidence / OnChallenge).
                DetectContradictionsOnPresent = !Services.TryGet(out GameManager g) || g.Investigation == null
            };

            if (Services.TryGet(out InterviewService interviews))
            {
                _session = interviews.GetOrCreate(evt.Suspect, Create); // saved interviews resume here
            }
            else if (!Sessions.TryGetValue(evt.Suspect.suspectId, out _session))
            {
                _session = Create();
                Sessions[evt.Suspect.suspectId] = _session;
            }

            // Wait a frame so the dialogue screen has opened and cleared itself before we fill it.
            StartCoroutine(StartNextFrame());
        }

        private IEnumerator StartNextFrame()
        {
            yield return null;
            if (_session == null) yield break;

            if (_session.HasStarted)
            {
                // Coming back: show the previous conversation so it can be reviewed.
                EventBus.Publish(new DialogueLineEvent(DialogueSpeaker.System, "", "— Earlier in this interview —"));
                foreach (DialogueMessage m in _session.History)
                    EventBus.Publish(new DialogueLineEvent(m.Speaker, m.SpeakerName, m.Text));
                PublishSuggestions();
            }
            else
            {
                RunTurn(ct => _session.BeginAsync(ct));
            }
        }

        private void OnInterviewEnded(InterviewEndedEvent evt)
        {
            Cancel();
            EventBus.Publish(new SuspectThinkingEvent(false));
            _session = null;
        }

        private void OnQuestion(PlayerQuestionSubmittedEvent evt)
        {
            if (_session == null) return;
            RunTurn(ct => _session.AskAsync(evt.Text, evt.SuggestionId, Discovered(), ct));
        }

        private void OnEvidence(PlayerPresentedEvidenceEvent evt)
        {
            if (_session == null) return;

            // Showing evidence that VALIDLY contradicts something this suspect said counts as challenging it.
            if (Services.TryGet(out GameManager game) && game.Investigation != null)
            {
                var evidence = game.Evidence.Find(evt.EvidenceId);
                ContradictionResult contradiction = evidence != null ? game.Investigation.FindValidContradiction(_session.Suspect.suspectId, evidence) : null;
                if (contradiction != null)
                {
                    RunChallenge(contradiction);
                    return;
                }
            }
            RunTurn(ct => _session.PresentEvidenceAsync(evt.EvidenceId, evt.EvidenceName, Discovered(), ct));
        }

        /// <summary>The player picked a statement and a piece of evidence: the checker decides, then the suspect answers.</summary>
        private void OnChallenge(PlayerChallengedStatementEvent evt)
        {
            if (_session == null || !Services.TryGet(out GameManager game) || game.Investigation == null) return;

            var statement = game.Investigation.FindStatement(evt.StatementId);
            var evidence = game.Evidence.Find(evt.EvidenceId);
            ContradictionResult result = game.Investigation.Evaluate(statement, evidence);
            switch (result.Outcome)
            {
                case ContradictionOutcome.Valid:
                case ContradictionOutcome.Unproven:
                case ContradictionOutcome.Invalid:
                    RunChallenge(result);
                    break;
                default:
                    EventBus.Publish(new DialogueErrorEvent(result.Explanation)); // not heard yet, not collected, already exposed
                    break;
            }
        }

        private void RunChallenge(ContradictionResult result)
        {
            ChallengeVerdict verdict = result.Outcome == ContradictionOutcome.Valid ? ChallengeVerdict.Valid
                : result.Outcome == ContradictionOutcome.Unproven ? ChallengeVerdict.Unproven
                : ChallengeVerdict.Invalid;

            RunTurn(ct => _session.ChallengeAsync(result.Statement.topicId, result.Statement.claim, result.Evidence.evidenceId,
                    result.Evidence.displayName, verdict, Discovered(), ct),
                turn =>
                {
                    // Applied only once the suspect has answered (a failed reply changes nothing).
                    if (Services.TryGet(out GameManager game) && game.Investigation != null) game.Investigation.ApplyChallenge(result);
                });
        }

        /// <summary>Runs one turn: shows the player's line and the thinking indicator, then the reply or an error.</summary>
        private async void RunTurn(Func<CancellationToken, Task<DialogueTurn>> turnFactory, Action<DialogueTurn> onSuccess = null)
        {
            if (_session.IsBusy)
            {
                EventBus.Publish(new DialogueErrorEvent($"Wait for {_session.Suspect.ShortName} to answer."));
                return;
            }

            DialogueSession session = _session;
            session.Provider = CurrentProvider();
            if (Services.TryGet(out GameManager game) && game.Case.HasActiveCase) session.Case = game.Case.CurrentCase;
            Cancel();
            _cts = new CancellationTokenSource();
            _cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            CancellationToken token = _cts.Token;

            int historyBefore = session.History.Count;
            Task<DialogueTurn> task = turnFactory(token);

            // Show the player's line right away (it's added synchronously before the provider is awaited).
            for (int i = historyBefore; i < session.History.Count; i++) Publish(session.History[i]);
            int shown = session.History.Count;

            if (!task.IsCompleted) EventBus.Publish(new SuspectThinkingEvent(true));

            DialogueTurn turn;
            try
            {
                turn = await task;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                turn = DialogueTurn.Failed("Something went wrong. Try again.");
            }

            if (session != _session) return; // interview ended while waiting
            EventBus.Publish(new SuspectThinkingEvent(false));

            if (!turn.Success)
            {
                EventBus.Publish(new DialogueErrorEvent(turn.Error));
                return;
            }

            ReportProvider(session, turn);

            for (int i = shown; i < session.History.Count; i++) Publish(session.History[i]);
            RecordInNotebook(session, turn);
            Services.TryGet(out GameManager g);
            if (!string.IsNullOrEmpty(turn.Plan?.EvidenceId) && g != null)
                g.Evidence.MarkPresented(turn.Plan.EvidenceId, session.Suspect.suspectId);
            RecordStatements(session, turn);
            EventBus.Publish(new SuspectComposureChangedEvent(session.Composure)); // tension audio follows composure
            if (turn.PlayerMessage != null && g != null && g.Progress != null) g.Progress.MarkQuestioned(session.Suspect); // asked or showed something
            onSuccess?.Invoke(turn);
            PublishSuggestions();
        }

        /// <summary>When the suspect answers a topic, the statements tied to it go into the notebook, with their words.</summary>
        private static void RecordStatements(DialogueSession session, DialogueTurn turn)
        {
            KnowledgeTopic topic = turn.Plan?.Topic;
            if (topic == null || turn.Responses.Count == 0 || !Services.TryGet(out GameManager game) || game.Investigation == null) return;
            ResponseKind k = turn.Plan.Kind;
            if (k != ResponseKind.TopicAnswer && k != ResponseKind.TopicPressured && k != ResponseKind.TopicRepeat && k != ResponseKind.TopicFollowUp) return;
            game.Investigation.RecordStatementsForTopic(session.Suspect.suspectId, topic.topicId, turn.Responses[turn.Responses.Count - 1].Text);
        }

        private void PublishSuggestions()
        {
            if (_session != null) EventBus.Publish(new SuggestedQuestionsEvent(_session.GetSuggestions(Discovered())));
        }

        private static void Publish(DialogueMessage m) => EventBus.Publish(new DialogueLineEvent(m.Speaker, m.SpeakerName, m.Text));

        /// <summary>Statements worth checking later go into the notebook: first answers and exposed lies.</summary>
        private static void RecordInNotebook(DialogueSession session, DialogueTurn turn)
        {
            if (!Services.TryGet(out GameManager game) || !game.Case.HasActiveCase) return;
            KnowledgeTopic topic = turn.Plan.Topic;
            if (topic == null || turn.Responses.Count == 0) return;

            string said = turn.Responses[turn.Responses.Count - 1].Text;
            string source = $"Interview: {session.Suspect.displayName}";

            if (turn.Plan.Kind == ResponseKind.EvidenceContradiction)
                game.Case.AddNote($"Confronted with {turn.Plan.EvidenceName}, {session.Suspect.ShortName} changed the story: \"{said}\"", source);
            else if (turn.Plan.Kind == ResponseKind.TopicPressured)
                game.Case.AddNote($"Under pressure about {topic.displayName.ToLowerInvariant()}: \"{said}\"", source);
            else if (turn.Plan.Kind == ResponseKind.TopicAnswer && session.TimesAsked(topic.topicId) == 1 && topic.stance != TopicStance.Unknown)
                game.Case.AddNote($"{topic.displayName}: \"{said}\"", source);
        }

        private static ICollection<string> Discovered()
        {
            var ids = new List<string>();
            if (Services.TryGet(out GameManager game))
                foreach (EvidenceRecord e in game.Case.Evidence) ids.Add(e.id);
            return ids;
        }

        /// <summary>The provider for the player's current AI mode (cached; rebuilt when the mode or config changes).</summary>
        private IAIProvider CurrentProvider()
        {
            AIProviderMode mode = AIProviderMode.Mock;
            AIConfig ai = null;
            if (Services.TryGet(out ConfigService config))
            {
                mode = config.User.aiMode;
                ai = config.Game.AI;
                timeoutSeconds = ai.RequestTimeoutSeconds + 3f;
            }

            string key = mode == AIProviderMode.Online && ai != null ? "online|" + ai.ActiveBackendUrl : "mock";
            if (_provider == null || key != _providerKey)
            {
                _provider = AIProviderFactory.Create(mode, ai, mockThinkingMs.x, mockThinkingMs.y);
                _providerKey = key;
                _fallbackNotified = false;
                if (mode == AIProviderMode.Online && _provider is MockAIProvider)
                {
                    AIProviderFactory.IsOnlineAvailable(ai, out string problem);
                    Debug.LogWarning($"[Dialogue] Online mode selected but unavailable ({problem}). Using offline dialogue.");
                }
                else
                {
                    Debug.Log($"[Dialogue] Dialogue provider: {_provider.ProviderId}");
                }
            }
            return _provider;
        }

        /// <summary>Logs online failures and tells the player once when offline dialogue takes over.</summary>
        private static void ReportProvider(DialogueSession session, DialogueTurn turn)
        {
            if (session.Provider is MockAIProvider) return;

            if (!turn.UsedFallback)
            {
                if (_fallbackNotified)
                    EventBus.Publish(new NotificationEvent("ONLINE DIALOGUE", "Connection restored."));
                _fallbackNotified = false;
                return;
            }

            if (turn.ProviderFailure != AIFailureKind.Paused)
                Debug.LogWarning($"[Dialogue] Online reply failed ({turn.ProviderFailure}: {turn.ProviderError}). The offline mock answered instead.");

            if (!_fallbackNotified && turn.ProviderFailure != AIFailureKind.Cancelled)
            {
                _fallbackNotified = true;
                EventBus.Publish(new NotificationEvent("ONLINE DIALOGUE UNAVAILABLE", "Using offline dialogue for now."));
            }
        }

        private void Cancel()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
