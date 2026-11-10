using System;

namespace TLA.Systems.Dialogue
{
    /// <summary>One line of an interview, as stored in the conversation history.</summary>
    [Serializable]
    public sealed class DialogueMessage
    {
        public DialogueSpeaker Speaker;
        public string SpeakerName;
        public string Text;

        /// <summary>Topic this line was about (null for small talk, narration, unknown questions).</summary>
        public string TopicId;

        /// <summary>Order in the conversation (0, 1, 2…).</summary>
        public int Index;

        public DialogueMessage(DialogueSpeaker speaker, string speakerName, string text, string topicId = null)
        {
            Speaker = speaker;
            SpeakerName = speakerName;
            Text = text;
            TopicId = topicId;
        }

        public override string ToString() => Speaker == DialogueSpeaker.System ? $"({Text})" : $"{SpeakerName}: {Text}";
    }
}
