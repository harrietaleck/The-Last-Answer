using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLA.Data.Cases;
using TLA.Systems.Dialogue;
using TLA.Systems.Dialogue.Online;
using UnityEditor;

namespace TLA.Tests
{
    /// <summary>
    /// The online provider against a fake network: what it sends, how it validates replies, and
    /// how every failure (timeout, offline, server error, bad JSON, invented details, duplicates)
    /// ends with the offline mock answering and no change to case state beyond the rules' plan.
    /// </summary>
    public class OnlineAIProviderTests
    {
        private const string MarlowePath = "Assets/_Project/Data/Cases/Case0147/SUS_Marlowe.asset";

        private SuspectProfile _marlowe;
        private FakeTransport _net;
        private double _now;
        private OnlineAIProvider _online;

        [SetUp]
        public void SetUp()
        {
            _marlowe = AssetDatabase.LoadAssetAtPath<SuspectProfile>(MarlowePath);
            Assert.IsNotNull(_marlowe, "Marlowe profile missing.");
            _net = new FakeTransport();
            _now = 0;
            _online = new OnlineAIProvider(new OnlineProviderOptions
            {
                BackendBaseUrl = "https://dialogue.example.test",
                MaxReplyCharacters = 120,
                MaxNarrationCharacters = 60,
                FailuresBeforePause = 2,
                PauseSeconds = 60
            }, _net, () => _now);
        }

        private DialogueSession NewSession() => new DialogueSession(_marlowe, _online, new MockAIProvider(0, 0));

        private static DialogueTurn Ask(DialogueSession s, string q, ICollection<string> discovered = null) =>
            s.AskAsync(q, null, discovered ?? new List<string>()).GetAwaiter().GetResult();

        // ------------------------------------------------------------------ success path

        [Test]
        public void ValidReply_IsShown_AndSentToOurBackendOnly()
        {
            _net.Reply = req => Ok(req, "Home. I told you. All evening.", "He folds his arms.");
            DialogueTurn t = Ask(NewSession(), "Where were you last night?");

            Assert.IsTrue(t.Success);
            Assert.IsFalse(t.UsedFallback);
            Assert.AreEqual("Home. I told you. All evening.", t.Responses.Last().Text);
            Assert.AreEqual("https://dialogue.example.test/v1/dialogue", _net.LastUrl);
        }

        [Test]
        public void Request_ContainsWhatTheTurnNeeds_ButNoHiddenTruth()
        {
            _net.Reply = req => Ok(req, (string)req["allowed"]["baseline"]);
            DialogueSession s = NewSession();
            Ask(s, "Where were you last night?");

            JObject req = _net.LastRequest;
            KnowledgeTopic whereabouts = _marlowe.knowledge.FindTopic((string)req["turn"]["topic"]["id"]);
            Assert.AreEqual("Lie", (string)req["turn"]["topic"]["stance"]);
            Assert.AreEqual("Where were you last night?", (string)req["turn"]["playerInput"]);
            Assert.AreEqual(_marlowe.displayName, (string)req["suspect"]["name"]);
            Assert.AreEqual("Defensive", (string)req["suspect"]["temperament"]);
            Assert.IsNotEmpty((string)req["allowed"]["baseline"]);

            string json = req.ToString();
            Assert.IsNotEmpty(whereabouts.truth, "test needs an authored truth");
            Assert.IsFalse(json.Contains(whereabouts.truth), "the hidden truth must never be sent");
            if (!string.IsNullOrEmpty(whereabouts.admissionAnswer))
                Assert.IsFalse(json.Contains(whereabouts.admissionAnswer), "admissions are sent only once the rules unlock them");
            Assert.IsFalse(json.ToLowerInvariant().Contains("apikey") || json.ToLowerInvariant().Contains("api_key"), "no credentials in requests");
        }

        [Test]
        public void ExtraFieldsInReply_CannotChangeCaseState()
        {
            // A malicious or confused backend tries to award evidence, end the case and expose a lie.
            _net.Reply = req => JsonConvert.SerializeObject(new
            {
                schemaVersion = 1, requestId = (string)req["requestId"], text = "Fine. I was at home.",
                evidence = "EVD_Weapon", ending = "Guilty", contradicted = new[] { "TOP_Whereabouts" }, composure = 0
            });
            DialogueSession s = NewSession();
            DialogueTurn t = Ask(s, "Where were you last night?");

            Assert.IsTrue(t.Success);
            Assert.AreEqual(ResponseKind.TopicAnswer, t.Plan.Kind);
            Assert.IsFalse(s.IsContradicted("TOP_Whereabouts"));
            Assert.AreEqual(0, s.PresentedEvidence.Count);
            Assert.AreEqual(_marlowe.baseComposure + t.Plan.ComposureDelta, s.Composure, "composure follows the C# plan only");
        }

        // ------------------------------------------------------------------ failures → mock fallback

        [TestCase(TransportStatus.Offline, 0, AIFailureKind.Offline)]
        [TestCase(TransportStatus.Timeout, 0, AIFailureKind.Timeout)]
        [TestCase(TransportStatus.HttpError, 500, AIFailureKind.ServerError)]
        [TestCase(TransportStatus.HttpError, 503, AIFailureKind.ServerError)]
        [TestCase(TransportStatus.HttpError, 429, AIFailureKind.RateLimited)]
        public void NetworkFailures_FallBackToMock(TransportStatus status, int code, AIFailureKind expected)
        {
            _net.Status = status;
            _net.Code = code;
            DialogueTurn t = Ask(NewSession(), "Where were you last night?");

            Assert.IsTrue(t.Success, "the mock must answer");
            Assert.IsTrue(t.UsedFallback);
            Assert.AreEqual(expected, t.ProviderFailure);
            Assert.IsNotEmpty(t.Responses.Last().Text);
        }

        [TestCase("{\"text\": \"broken", TestName = "InvalidReply_TruncatedJson")]
        [TestCase("[\"not\", \"an object\"]", TestName = "InvalidReply_NotAnObject")]
        [TestCase("", TestName = "InvalidReply_EmptyBody")]
        [TestCase("<html>502 Bad Gateway</html>", TestName = "InvalidReply_Html")]
        public void InvalidReplies_FallBackToMock(string body)
        {
            _net.Reply = _ => body;
            DialogueTurn t = Ask(NewSession(), "Where were you last night?");
            Assert.IsTrue(t.Success);
            Assert.IsTrue(t.UsedFallback);
            Assert.AreEqual(AIFailureKind.InvalidResponse, t.ProviderFailure);
        }

        [Test]
        public void Reply_WithWrongRequestIdOrSchema_IsRejected()
        {
            _net.Reply = req => JsonConvert.SerializeObject(new { schemaVersion = 1, requestId = "someone-else", text = "Home." });
            Assert.AreEqual(AIFailureKind.InvalidResponse, Ask(NewSession(), "Where were you?").ProviderFailure);

            _net.Reply = req => JsonConvert.SerializeObject(new { schemaVersion = 9, requestId = (string)req["requestId"], text = "Home." });
            Assert.AreEqual(AIFailureKind.InvalidResponse, Ask(NewSession(), "Where were you?").ProviderFailure);
        }

        [TestCase("I was at the Riverside Hotel with Gregory.", TestName = "InventedDetail_NewName")]
        [TestCase("I left at 11:45, I swear.", TestName = "InventedDetail_NewTime")]
        [TestCase("As an AI, I cannot pretend to be a suspect.", TestName = "OutOfCharacter_Disclaimer")]
        public void Replies_ThatInventOrBreakCharacter_AreRejected(string text)
        {
            _net.Reply = req => Ok(req, text);
            DialogueTurn t = Ask(NewSession(), "Where were you last night?");
            Assert.IsTrue(t.UsedFallback, text);
            Assert.AreNotEqual(text, t.Responses.Last().Text);
        }

        [Test]
        public void Names_AlreadyInTheConversation_AreAllowed()
        {
            _net.Reply = req => Ok(req, "Daniel and I argued, yes. Everyone argues.");
            DialogueTurn t = Ask(NewSession(), "Did you argue with Daniel?");
            Assert.IsFalse(t.UsedFallback, t.ProviderError);
        }

        [Test]
        public void LongReply_IsCutAtASentence_AndMarkupStripped()
        {
            string longText = "<size=300>Home.</size> I was home. " + new string('x', 200);
            _net.Reply = req => Ok(req, longText);
            DialogueTurn t = Ask(NewSession(), "Where were you last night?");
            Assert.IsFalse(t.UsedFallback, t.ProviderError);
            Assert.AreEqual("Home. I was home.", t.Responses.Last().Text);
        }

        [Test]
        public void DuplicateRequest_WhileOneIsRunning_IsRefused()
        {
            var pending = new TaskCompletionSource<TransportResult>();
            _net.Pending = pending.Task;
            var context = new DialogueContext { Suspect = _marlowe, Knowledge = _marlowe.knowledge, Plan = new ResponsePlan(), PlayerInput = "Hello" };

            Task<AIResponse> first = _online.GenerateResponseAsync(context, CancellationToken.None);
            AIResponse second = _online.GenerateResponseAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(first.IsCompleted);
            Assert.AreEqual(AIFailureKind.Busy, second.FailureKind);
            Assert.AreEqual(1, _net.Calls, "only one request reaches the network");
            pending.SetResult(new TransportResult(TransportStatus.Offline));
        }

        [Test]
        public void RepeatedFailures_PauseOnline_ThenRetryLater()
        {
            _net.Status = TransportStatus.Offline;
            DialogueSession s = NewSession();
            Ask(s, "Where were you?");
            Ask(s, "Who had keys?");
            Assert.AreEqual(2, _net.Calls);
            Assert.IsTrue(_online.IsPaused);

            DialogueTurn paused = Ask(s, "What about money?");
            Assert.AreEqual(AIFailureKind.Paused, paused.ProviderFailure);
            Assert.IsTrue(paused.Success, "the mock still answers instantly");
            Assert.AreEqual(2, _net.Calls, "no network call while paused");

            _now += 61;
            _net.Status = TransportStatus.Success;
            _net.Reply = req => Ok(req, (string)req["allowed"]["baseline"]);
            DialogueTurn back = Ask(s, "Tell me about Victor Kim.");
            Assert.IsFalse(back.UsedFallback, back.ProviderError);
            Assert.AreEqual(3, _net.Calls);
        }

        [Test]
        public void SessionCanSwitchProvider_BetweenTurns()
        {
            DialogueSession s = new DialogueSession(_marlowe, new MockAIProvider(0, 0), new MockAIProvider(0, 0));
            Ask(s, "Where were you?");
            Assert.AreEqual(0, _net.Calls);

            _net.Reply = req => Ok(req, (string)req["allowed"]["baseline"]);
            s.Provider = _online;
            Assert.IsFalse(Ask(s, "Who had keys?").UsedFallback);
            Assert.AreEqual(1, _net.Calls);
        }

        // ------------------------------------------------------------------ configuration safety

        [TestCase("", false, TestName = "Url_Empty_Rejected")]
        [TestCase("not a url", false, TestName = "Url_Garbage_Rejected")]
        [TestCase("http://dialogue.example.com", false, TestName = "Url_PlainHttpRemote_Rejected")]
        [TestCase("https://user:secret@dialogue.example.com", false, TestName = "Url_WithCredentials_Rejected")]
        [TestCase("ftp://dialogue.example.com", false, TestName = "Url_OtherScheme_Rejected")]
        [TestCase("https://dialogue.example.com", true, TestName = "Url_Https_Accepted")]
        [TestCase("http://localhost:8787", true, TestName = "Url_LocalhostHttp_Accepted")]
        [TestCase("http://127.0.0.1:8787/", true, TestName = "Url_LoopbackHttp_Accepted")]
        public void BackendUrl_Rules(string url, bool accepted)
        {
            string endpoint = BackendUrl.ToEndpoint(url, out string problem);
            Assert.AreEqual(accepted, endpoint != null, problem);
        }

        [Test]
        public void UnconfiguredOnline_FailsAsNotConfigured_AndMockAnswers()
        {
            var unconfigured = new OnlineAIProvider(new OnlineProviderOptions { BackendBaseUrl = "" }, _net);
            var s = new DialogueSession(_marlowe, unconfigured, new MockAIProvider(0, 0));
            DialogueTurn t = Ask(s, "Where were you?");
            Assert.IsTrue(t.Success);
            Assert.AreEqual(AIFailureKind.NotConfigured, t.ProviderFailure);
            Assert.AreEqual(0, _net.Calls);
        }

        // ------------------------------------------------------------------ fakes

        private static string Ok(JObject req, string text, string narration = "") =>
            JsonConvert.SerializeObject(new { schemaVersion = 1, requestId = (string)req["requestId"], text, narration });

        private sealed class FakeTransport : IDialogueTransport
        {
            public TransportStatus Status = TransportStatus.Success;
            public int Code = 200;
            public Func<JObject, string> Reply = req => Ok(req, (string)req["allowed"]["baseline"]);
            public Task<TransportResult> Pending;
            public int Calls;
            public string LastUrl;
            public JObject LastRequest;

            public Task<TransportResult> PostJsonAsync(string url, string json, float timeoutSeconds, CancellationToken ct)
            {
                Calls++;
                LastUrl = url;
                LastRequest = JObject.Parse(json);
                if (Pending != null) return Pending;
                if (Status != TransportStatus.Success) return Task.FromResult(new TransportResult(Status, Code, null, Status.ToString()));
                return Task.FromResult(new TransportResult(TransportStatus.Success, 200, Reply(LastRequest)));
            }
        }
    }
}
