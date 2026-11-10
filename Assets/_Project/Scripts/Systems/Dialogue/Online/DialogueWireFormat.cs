using System;
using System.Collections.Generic;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue.Online
{
    // =====================================================================================
    // The JSON contract between the game and OUR backend (POST {backend}/v1/dialogue).
    // Field names are the JSON names. Documented in ONLINE_AI_GUIDE.md and implemented by the
    // reference backend in /Backend. Bump SchemaVersion when either side changes shape.
    // =====================================================================================

    /// <summary>Everything the backend receives for ONE reply. Built by <see cref="DialogueRequestBuilder"/>.</summary>
    [Serializable]
    public sealed class DialogueRequestDto
    {
        public int schemaVersion = DialogueRequestBuilder.SchemaVersion;

        /// <summary>Unique per request; the backend must echo it back so stale or mixed-up replies are rejected.</summary>
        public string requestId;

        public LimitsDto limits;
        public CaseDto @case;
        public SuspectDto suspect;
        public TurnDto turn;

        /// <summary>
        /// The ONLY case content the reply may express. The C# rules and the authored knowledge
        /// decide it; the AI rephrases it in the suspect's voice and reacts to the question.
        /// </summary>
        public AllowedDto allowed;

        public List<HistoryLineDto> history = new List<HistoryLineDto>();
    }

    [Serializable]
    public sealed class LimitsDto
    {
        public int maxReplyChars;
        public int maxNarrationChars;
    }

    /// <summary>Public case details: what the investigator already knows from the briefing.</summary>
    [Serializable]
    public sealed class CaseDto
    {
        public string title;
        public string location;
        public string briefing;
    }

    /// <summary>Profile and personality. Composure is the value AFTER this turn.</summary>
    [Serializable]
    public sealed class SuspectDto
    {
        public string name;
        public int age;
        public string occupation;
        public string relationToCase;
        public string background;
        public string temperament;
        public string speechStyle;
        public int composure;
        public string composureLevel;
    }

    /// <summary>The player's move and what the rules decided the suspect does about it.</summary>
    [Serializable]
    public sealed class TurnDto
    {
        public int number;

        /// <summary>The player's latest question (empty for greetings and evidence).</summary>
        public string playerInput;

        /// <summary>ResponseKind chosen by the rules, e.g. "TopicAnswer", "Accusation", "EvidenceContradiction".</summary>
        public string replyKind;

        public TopicDto topic;

        /// <summary>Evidence shown THIS turn (null if none).</summary>
        public EvidenceDto evidenceShown;

        /// <summary>Ids of all evidence shown so far in this interview.</summary>
        public List<string> evidencePresentedSoFar = new List<string>();
    }

    [Serializable]
    public sealed class TopicDto
    {
        public string id;
        public string name;

        /// <summary>Truthful, Partial, Lie, Deflect or Unknown: how the suspect handles this topic.</summary>
        public string stance;
    }

    [Serializable]
    public sealed class EvidenceDto
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class AllowedDto
    {
        /// <summary>The authored line for this turn. The reply must keep its meaning and add no new facts.</summary>
        public string baseline;

        /// <summary>Optional stage direction the offline version would show.</summary>
        public string baselineNarration;
    }

    [Serializable]
    public sealed class HistoryLineDto
    {
        /// <summary>"Investigator", "Suspect" or "System" (narration / evidence shown).</summary>
        public string speaker;
        public string text;
    }

    /// <summary>
    /// What the backend must return. Anything else in the JSON is ignored: the backend (and the
    /// AI behind it) can only supply words, never evidence, progress, contradictions or endings.
    /// </summary>
    [Serializable]
    public sealed class DialogueResponseDto
    {
        public int schemaVersion;
        public string requestId;

        /// <summary>The suspect's spoken line.</summary>
        public string text;

        /// <summary>Optional short stage direction, e.g. "He looks away." (may be empty).</summary>
        public string narration;
    }

    /// <summary>
    /// Builds the request from a <see cref="DialogueContext"/>, sending only what this reply needs.
    /// Deliberately NOT sent: the hidden truth of any topic, other topics' answers, evidence the
    /// player hasn't shown, admissions the rules haven't unlocked, other suspects' knowledge.
    /// The AI can't leak what it never receives.
    /// </summary>
    public static class DialogueRequestBuilder
    {
        public const int SchemaVersion = 1;
        private const int MaxBriefingChars = 600;

        public static DialogueRequestDto Build(DialogueContext c, string requestId, int maxReplyChars, int maxNarrationChars, int historyLines)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            ResponsePlan plan = c.Plan ?? new ResponsePlan();

            // The authored version of this turn (exactly what the offline mode would say).
            AIResponse baseline = MockAIProvider.Compose(c);

            var dto = new DialogueRequestDto
            {
                requestId = requestId,
                limits = new LimitsDto { maxReplyChars = maxReplyChars, maxNarrationChars = maxNarrationChars },
                @case = c.Case == null ? null : new CaseDto
                {
                    title = c.Case.title,
                    location = c.Case.location,
                    briefing = Trim(c.Case.briefing, MaxBriefingChars)
                },
                suspect = new SuspectDto
                {
                    name = c.SuspectName,
                    age = c.Suspect != null ? c.Suspect.age : 0,
                    occupation = c.Suspect?.occupation ?? "",
                    relationToCase = c.Suspect?.relationToCase ?? "",
                    background = c.Suspect?.summary ?? "",
                    temperament = c.Temperament.ToString(),
                    speechStyle = c.Suspect?.speechStyle ?? "",
                    composure = c.Composure,
                    composureLevel = c.Level.ToString()
                },
                turn = new TurnDto
                {
                    number = c.TurnNumber,
                    playerInput = c.PlayerInput ?? "",
                    replyKind = plan.Kind.ToString(),
                    topic = plan.Topic == null ? null : new TopicDto
                    {
                        id = plan.Topic.topicId,
                        name = plan.Topic.displayName,
                        stance = plan.Topic.stance.ToString()
                    },
                    evidenceShown = string.IsNullOrEmpty(plan.EvidenceId) ? null : new EvidenceDto { id = plan.EvidenceId, name = plan.EvidenceName }
                },
                allowed = new AllowedDto
                {
                    baseline = baseline?.Text ?? "",
                    baselineNarration = baseline?.Narration ?? ""
                }
            };

            if (c.PresentedEvidence != null)
                foreach (string id in c.PresentedEvidence) dto.turn.evidencePresentedSoFar.Add(id);

            if (c.History != null && historyLines > 0)
            {
                // The player's current line was already added to history by the session; it's in turn.playerInput instead.
                int end = c.History.Count;
                if (end > 0 && !string.IsNullOrEmpty(c.PlayerInput) && c.History[end - 1].Text == c.PlayerInput) end--;
                for (int i = Math.Max(0, end - historyLines); i < end; i++)
                    dto.history.Add(new HistoryLineDto { speaker = c.History[i].Speaker.ToString(), text = c.History[i].Text });
            }

            return dto;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
