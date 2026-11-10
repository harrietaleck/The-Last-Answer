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
    /// - Provider: chosen by <see cref="AIProviderFactory"/> from the player's AI mode setting.
    ///   Online failures fall back to the mock automatically.
    /// - History persists while the game runs: leave the room and come back, and the conversation
    ///   is still there (cleared when a new case starts).
    /// - Notable answers are written to the investigation notebook so they can be reviewed later.
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        [Tooltip("Fake thinking time range for the mock provider, in milliseconds.")]
        [SerializeField] private Vector2Int mockThinkingMs = new Vector2Int(700, 1500);

        [Tooltip("Give up on a reply after this many seconds (online provider); the mock then answers.")]
        [SerializeField] private float timeoutSeconds = 10f;

        // Sessions survive scene changes for the current run (keyed by suspect id).
        private static readonly Dictionary<string, DialogueSession> Sessions = new Dictionary<string, DialogueSession>();

        private DialogueSession _session;
        private CancellationTokenSource _cts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sessions.Clear();

        private void OnEnable()
        {
            EventBus.Subscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Subscribe<InterviewEndedEvent>(OnInterviewEnded);
            EventBus.Subscribe<PlayerQuestionSubmittedEvent>(OnQuestion);
            EventBus.Subscribe<PlayerPresentedEvidenceEvent>(OnEvidence);
            EventBus.Subscribe<CaseStartedEvent>(OnCaseStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Unsubscribe<InterviewEndedEvent>(OnInterviewEnded);
            EventBus.Unsubscribe<PlayerQuestionSubmittedEvent>(OnQuestion);
            EventBus.Unsubscribe<PlayerPresentedEvidenceEvent>(OnEvidence);
            EventBus.Unsubscribe<CaseStartedEvent>(OnCaseStarted);
            Cancel();
        }

        /// <summary>The active session (for tests and debugging).</summary>
        public DialogueSession CurrentSession => _session;

        private void OnCaseStarted(CaseStartedEvent evt) => Sessions.Clear();

        private void OnInterviewStarted(InterviewStartedEvent evt)
        {
            if (evt.Suspect == null)
            {
                EventBus.Publish(new DialogueErrorEvent("No suspect is assigned to this interview."));
                return;
            }

            if (!Sessions.TryGetValue(evt.Suspect.suspectId, out _session))
            {
                _session = new DialogueSession(evt.Suspect, CreateProvider(), new MockAIProvider(mockThinkingMs.x, mockThinkingMs.y));
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
            RunTurn(ct => _session.PresentEvidenceAsync(evt.EvidenceId, evt.EvidenceName, Discovered(), ct));
        }

        /// <summary>Runs one turn: shows the player's line and the thinking indicator, then the reply or an error.</summary>
        private async void RunTurn(Func<CancellationToken, Task<DialogueTurn>> turnFactory)
        {
            if (_session.IsBusy)
            {
                EventBus.Publish(new DialogueErrorEvent($"Wait for {_session.Suspect.ShortName} to answer."));
                return;
            }

            DialogueSession session = _session;
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

            if (turn.UsedFallback)
                Debug.LogWarning("[Dialogue] Online provider failed; the offline mock answered instead.");

            for (int i = shown; i < session.History.Count; i++) Publish(session.History[i]);
            RecordInNotebook(session, turn);
            PublishSuggestions();
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

        private IAIProvider CreateProvider()
        {
            AIProviderMode mode = AIProviderMode.Mock;
            string url = "";
            if (Services.TryGet(out ConfigService config))
            {
                mode = config.User.aiMode;
                url = config.Game.AI.BackendBaseUrl;
                timeoutSeconds = config.Game.AI.RequestTimeoutSeconds;
            }
            return AIProviderFactory.Create(mode, url, mockThinkingMs.x, mockThinkingMs.y);
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
