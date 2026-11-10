using System.Collections.Generic;
using TLA.Core.Events;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    // =====================================================================================
    // The CONTRACT between the dialogue system (gameplay) and the dialogue screen (UI).
    //
    //   Dialogue system ──publishes──▶ InterviewStarted / DialogueLine / SuggestedQuestions /
    //                                  SuspectThinking / InterviewEnded            ──▶ UI shows them
    //   UI ──publishes──▶ PlayerQuestionSubmitted / InterviewLeaveRequested  ──▶ dialogue system reacts
    //
    // Neither side references the other. The DialogueManager (Gameplay/Interview) plays the gameplay
    // side, using DialogueSession + an IAIProvider (mock now, online later).
    // =====================================================================================

    public enum DialogueSpeaker
    {
        Investigator = 0, // the player
        Suspect = 1,
        System = 2        // narration / stage directions, e.g. "Marlowe looks away."
    }

    /// <summary>A clickable suggested question.</summary>
    public readonly struct SuggestedQuestion
    {
        /// <summary>Stable id so the dialogue system knows which topic was chosen (e.g. "TOP_Whereabouts").</summary>
        public readonly string Id;
        public readonly string Text;
        public SuggestedQuestion(string id, string text) { Id = id; Text = text; }
    }

    /// <summary>An interview began: show the dialogue screen for this suspect.</summary>
    public readonly struct InterviewStartedEvent : IGameEvent
    {
        public readonly SuspectProfile Suspect;
        public InterviewStartedEvent(SuspectProfile suspect) { Suspect = suspect; }
    }

    /// <summary>The interview ended: hide the dialogue screen.</summary>
    public readonly struct InterviewEndedEvent : IGameEvent
    {
    }

    /// <summary>A line of dialogue to add to the conversation history.</summary>
    public readonly struct DialogueLineEvent : IGameEvent
    {
        public readonly DialogueSpeaker Speaker;
        public readonly string SpeakerName;
        public readonly string Text;
        public DialogueLineEvent(DialogueSpeaker speaker, string speakerName, string text)
        {
            Speaker = speaker; SpeakerName = speakerName; Text = text;
        }
    }

    /// <summary>Replace the suggested questions with this list (empty list = none).</summary>
    public readonly struct SuggestedQuestionsEvent : IGameEvent
    {
        public readonly IReadOnlyList<SuggestedQuestion> Questions;
        public SuggestedQuestionsEvent(IReadOnlyList<SuggestedQuestion> questions) { Questions = questions; }
    }

    /// <summary>The suspect is "thinking" (waiting for a reply). The UI shows an indicator and blocks input.</summary>
    public readonly struct SuspectThinkingEvent : IGameEvent
    {
        public readonly bool IsThinking;
        public SuspectThinkingEvent(bool isThinking) { IsThinking = isThinking; }
    }

    /// <summary>FROM THE UI: the player asked something (typed, or clicked a suggestion).</summary>
    public readonly struct PlayerQuestionSubmittedEvent : IGameEvent
    {
        public readonly string Text;
        /// <summary>Id of the clicked suggestion, or null when typed freely.</summary>
        public readonly string SuggestionId;
        public PlayerQuestionSubmittedEvent(string text, string suggestionId) { Text = text; SuggestionId = suggestionId; }
    }

    /// <summary>FROM THE UI: the player clicked "End interview".</summary>
    public readonly struct InterviewLeaveRequestedEvent : IGameEvent
    {
    }

    /// <summary>FROM THE UI: the player showed the suspect a piece of discovered evidence.</summary>
    public readonly struct PlayerPresentedEvidenceEvent : IGameEvent
    {
        public readonly string EvidenceId;
        public readonly string EvidenceName;
        public PlayerPresentedEvidenceEvent(string evidenceId, string evidenceName) { EvidenceId = evidenceId; EvidenceName = evidenceName; }
    }

    /// <summary>A player-facing problem to show under the input box (no reply, please wait, empty question…).</summary>
    public readonly struct DialogueErrorEvent : IGameEvent
    {
        public readonly string Message;
        public DialogueErrorEvent(string message) { Message = message; }
    }
}
