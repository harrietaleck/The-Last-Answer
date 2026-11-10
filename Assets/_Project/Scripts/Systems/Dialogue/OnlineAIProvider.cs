using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TLA.Data.Cases;
using UnityEngine.Networking;

namespace TLA.Systems.Dialogue
{
    /// <summary>
    /// LATER-STAGE provider: sends the dialogue context to OUR backend (never directly to an AI
    /// vendor), which holds the API key, picks the model and returns the suspect's words.
    ///
    /// What it sends is deliberately limited to what the rules allow: the decided reply KIND, the
    /// topic's stance and the authored lines for that topic (the only facts the AI may express),
    /// the suspect's personality and the recent conversation. The backend must answer with
    /// {"text": "...", "narration": "..."}. If anything fails, DialogueSession falls back to the mock.
    ///
    /// No API key or model name exists in the game. Configure the URL in GameConfig → AI.
    /// </summary>
    public sealed class OnlineAIProvider : IAIProvider
    {
        private const int MaxHistory = 12;
        private const int MaxReplyLength = 600;

        private readonly string _endpoint;

        public OnlineAIProvider(string backendBaseUrl)
        {
            _endpoint = string.IsNullOrWhiteSpace(backendBaseUrl) ? null : backendBaseUrl.TrimEnd('/') + "/v1/dialogue";
        }

        public string ProviderId => "online";

        public async Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken cancellationToken)
        {
            if (_endpoint == null) return AIResponse.Fail("No backend URL configured.");

            string body = JsonConvert.SerializeObject(BuildRequest(context));
            using var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 15
            };
            request.SetRequestHeader("Content-Type", "application/json");

            var done = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ => done.TrySetResult(true);
            using (cancellationToken.Register(() => { request.Abort(); done.TrySetCanceled(); }))
                await done.Task;

            if (request.result != UnityWebRequest.Result.Success)
                return AIResponse.Fail($"Backend error: {request.error}");

            try
            {
                var reply = JsonConvert.DeserializeObject<Reply>(request.downloadHandler.text);
                string text = Clean(reply?.text);
                return string.IsNullOrEmpty(text) ? AIResponse.Fail("Empty reply from backend.") : AIResponse.Ok(text, Clean(reply.narration));
            }
            catch (JsonException e)
            {
                return AIResponse.Fail("Unreadable reply: " + e.Message);
            }
        }

        /// <summary>Basic output safety: trim, cap length, drop AI disclaimers.</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = text.Trim();
            if (text.IndexOf("as an ai", StringComparison.OrdinalIgnoreCase) >= 0) return string.Empty;
            return text.Length > MaxReplyLength ? text.Substring(0, MaxReplyLength) : text;
        }

        private static object BuildRequest(DialogueContext c)
        {
            KnowledgeTopic topic = c.Plan.Topic;
            var history = new List<object>();
            int start = Math.Max(0, (c.History?.Count ?? 0) - MaxHistory);
            for (int i = start; i < (c.History?.Count ?? 0); i++)
                history.Add(new { speaker = c.History[i].Speaker.ToString(), text = c.History[i].Text });

            return new
            {
                suspect = new
                {
                    name = c.SuspectName,
                    temperament = c.Temperament.ToString(),
                    speechStyle = c.Suspect != null ? c.Suspect.speechStyle : "",
                    composure = c.Composure,
                    level = c.Level.ToString()
                },
                playerInput = c.PlayerInput,
                replyKind = c.Plan.Kind.ToString(),
                topic = topic == null ? null : new
                {
                    id = topic.topicId,
                    stance = topic.stance.ToString(),
                    // The ONLY facts the AI may express for this reply:
                    allowedLines = new[] { PickAllowed(c, topic) }
                },
                evidenceShown = c.Plan.EvidenceName,
                history,
                rules = new[]
                {
                    "Stay in character. Never confess. Never reveal facts not in allowedLines.",
                    "Never mention evidence that isn't in evidenceShown or history.",
                    "Express the reply kind and stance; you choose only the wording."
                }
            };
        }

        private static string PickAllowed(DialogueContext c, KnowledgeTopic topic)
        {
            switch (c.Plan.Kind)
            {
                case ResponseKind.TopicPressured: return topic.pressuredAnswer;
                case ResponseKind.TopicAdmitted:
                case ResponseKind.EvidenceContradiction: return topic.admissionAnswer;
                case ResponseKind.TopicFollowUp: return topic.followUpAnswer;
                default: return topic.answers != null && topic.answers.Length > 0 ? topic.answers[Math.Min(c.Plan.VariantIndex, topic.answers.Length - 1)] : "";
            }
        }

        private sealed class Reply
        {
            public string text;
            public string narration;
        }
    }

    /// <summary>The single place that chooses a provider. Gameplay and UI never construct providers themselves.</summary>
    public static class AIProviderFactory
    {
        public static IAIProvider Create(TLA.Data.Config.AIProviderMode mode, string backendBaseUrl, int mockMinDelayMs = 600, int mockMaxDelayMs = 1400)
        {
            if (mode == TLA.Data.Config.AIProviderMode.Online && !string.IsNullOrWhiteSpace(backendBaseUrl))
                return new OnlineAIProvider(backendBaseUrl);
            return new MockAIProvider(mockMinDelayMs, mockMaxDelayMs);
        }
    }
}
