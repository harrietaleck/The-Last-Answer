using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TLA.Data.Cases;
using TLA.Systems.Dialogue;
using UnityEditor;
using UnityEngine;

namespace TLA.Tests
{
    /// <summary>
    /// Drives a real interview with Elliot Marlowe (the authored knowledge asset) through the whole
    /// pipeline: intent matching → rules → context → MockAIProvider. Checks the behaviours the design
    /// requires: varied answers, lies, evasion, follow-ups, pressure, evidence, no confessions,
    /// no invented knowledge, validation and provider fallback.
    /// </summary>
    public class DialogueSystemTests
    {
        private const string MarlowePath = "Assets/_Project/Data/Cases/Case0147/SUS_Marlowe.asset";

        private SuspectProfile _marlowe;
        private DialogueSession _session;
        private List<string> _discovered;

        [SetUp]
        public void SetUp()
        {
            _marlowe = AssetDatabase.LoadAssetAtPath<SuspectProfile>(MarlowePath);
            Assert.IsNotNull(_marlowe, "Marlowe profile missing; run The Last Answer → Environment → Rebuild All Locations.");
            Assert.IsNotNull(_marlowe.knowledge, "Marlowe has no knowledge asset.");
            _session = new DialogueSession(_marlowe, new MockAIProvider(0, 0));
            _discovered = new List<string>();
        }

        private DialogueTurn Ask(string question, string suggestionId = null) =>
            _session.AskAsync(question, suggestionId, _discovered).GetAwaiter().GetResult();

        private DialogueTurn Present(string id, string name) =>
            _session.PresentEvidenceAsync(id, name, _discovered).GetAwaiter().GetResult();

        private static string Said(DialogueTurn t) => t.Responses.Last().Text;

        private KnowledgeTopic Topic(string id) => _marlowe.knowledge.FindTopic(id);

        // ------------------------------------------------------------------ validation

        [Test]
        public void EmptyOrBlankQuestion_IsRejected_WithMessage()
        {
            foreach (string blank in new[] { "", "   ", null })
            {
                DialogueTurn t = Ask(blank);
                Assert.IsFalse(t.Success);
                StringAssert.Contains("Type a question", t.Error);
            }
            Assert.AreEqual(0, _session.History.Count, "rejected input must not enter the history");
        }

        // ------------------------------------------------------------------ variety and topics

        [Test]
        public void DifferentQuestions_GetDifferentInCharacterAnswers()
        {
            string[] questions =
            {
                "Where were you last night?",
                "How did you get along with Daniel?",
                "Who had keys to the building?",
                "Why didn't you answer your phone?",
                "What can you tell me about Nora Hayes?",
                "How did he die?",
                "What's your favourite colour?",
            };

            var answers = new List<string>();
            foreach (string q in questions)
            {
                DialogueTurn t = Ask(q);
                Assert.IsTrue(t.Success, $"'{q}' failed: {t.Error}");
                answers.Add(Said(t));
            }

            Assert.AreEqual(answers.Count, answers.Distinct().Count(), "every question should get its own answer:\n" + string.Join("\n", answers));
        }

        [Test]
        public void Whereabouts_IsALie_AndAskingAgainGivesNewWording_ThenImpatience()
        {
            KnowledgeTopic t = Topic("TOP_Whereabouts");
            Assert.AreEqual(TopicStance.Lie, t.stance);

            string first = Said(Ask("Where were you on the night of the 14th?"));
            string second = Said(Ask("Where were you that evening?"));
            string third = Said(Ask("Tell me again where you were."));
            string fourth = Said(Ask("Where were you?"));

            Assert.AreEqual(t.answers[0], first);
            Assert.AreEqual(t.answers[1], second);
            Assert.AreEqual(t.answers[2], third);
            StringAssert.Contains("already", fourth.ToLowerInvariant(), "after the written answers run out the suspect gets impatient");
            Assert.Less(_session.Composure, _marlowe.baseComposure, "repeating questions should wear the suspect down");
        }

        [Test]
        public void FollowUp_RefersToThePreviousTopic()
        {
            Ask("How was your relationship with Daniel Reyes?");
            DialogueTurn followUp = Ask("Why?");

            Assert.AreEqual(IntentKind.FollowUp, followUp.Intent.Kind);
            Assert.AreEqual("TOP_Relationship", followUp.Plan.Topic.topicId);
            Assert.AreEqual(Topic("TOP_Relationship").followUpAnswer, Said(followUp));
        }

        [Test]
        public void SuggestedQuestion_MapsDirectlyToItsTopic()
        {
            KnowledgeTopic keys = Topic("TOP_Keys");
            DialogueTurn t = Ask(keys.suggestedQuestion, keys.topicId);
            Assert.AreEqual("TOP_Keys", t.Plan.Topic.topicId);
            Assert.AreEqual(keys.answers[0], Said(t));
        }

        [Test]
        public void Greeting_IsSmallTalk_EvenWithKeywordInside()
        {
            Assert.AreEqual(IntentKind.Greeting, Ask("Good evening.").Intent.Kind); // "evening" must not mean "whereabouts"
        }

        [Test]
        public void HowDidDanielDie_IsTheDeathTopic_NotTheRelationship()
        {
            Assert.AreEqual("TOP_Death", Ask("How did Daniel die?").Plan.Topic.topicId);
            Assert.AreEqual("TOP_Relationship", Ask("How did you get along with Daniel?").Plan.Topic.topicId);
        }

        [Test]
        public void StatementAfterAdmission_IsTreatedAsFollowUp()
        {
            _discovered.Add("EVD_CCTV");
            Present("EVD_CCTV", "CCTV still, 21:40");
            DialogueTurn t = Ask("So you were there.");
            Assert.AreEqual(IntentKind.FollowUp, t.Intent.Kind);
            Assert.AreEqual(ResponseKind.TopicAdmitted, t.Plan.Kind);
        }

        // ------------------------------------------------------------------ knowledge limits

        [Test]
        public void UnknownTopic_SuspectDoesNotRevealAnything()
        {
            DialogueTurn t = Ask("How was he killed? What was the weapon?");
            Assert.AreEqual("TOP_Death", t.Plan.Topic.topicId);
            Assert.AreEqual(TopicStance.Unknown, t.Plan.Topic.stance);
            StringAssert.Contains("don't know", Said(t));
        }

        [Test]
        public void UnrelatedQuestion_IsDeflectedInCharacter()
        {
            DialogueTurn a = Ask("What's the capital of France?");
            DialogueTurn b = Ask("Do you like football?");
            Assert.AreEqual(ResponseKind.Unknown, a.Plan.Kind);
            CollectionAssert.Contains(_marlowe.knowledge.unknownQuestion, Said(a));
            Assert.AreNotEqual(Said(a), Said(b), "deflections should vary");
        }

        [Test]
        public void EvidenceQuestion_IsNotSuggested_UntilEvidenceDiscovered()
        {
            Assert.IsFalse(_session.GetSuggestions(_discovered, 20).Any(s => s.Id == "TOP_CCTV"));
            _discovered.Add("EVD_CCTV");
            Assert.IsTrue(_session.GetSuggestions(_discovered, 20).Any(s => s.Id == "TOP_CCTV"));
        }

        // ------------------------------------------------------------------ never confess

        [Test]
        public void Accusations_AreAlwaysDenied_EvenWhenRattled()
        {
            string admission = Topic("TOP_Whereabouts").admissionAnswer;
            for (int i = 0; i < 10; i++)
            {
                DialogueTurn t = Ask("You killed him. Admit it, you did it!");
                Assert.IsTrue(t.Success);
                Assert.AreEqual(ResponseKind.Accusation, t.Plan.Kind);
                Assert.AreNotEqual(admission, Said(t));
            }
            Assert.AreEqual(ComposureLevel.Rattled, _session.Level, "repeated accusations rattle him...");
            // ...but he still never confesses:
            Assert.AreEqual(ResponseKind.ConfessionRefusal, Ask("Just confess.").Plan.Kind);
        }

        [Test]
        public void Pressure_ProducesACrackedAnswer_NotAConfession()
        {
            for (int i = 0; i < 5; i++) Ask("You're lying to me."); // wear composure down
            DialogueTurn t = Ask("How was your relationship with Daniel?");
            Assert.AreEqual(ResponseKind.TopicPressured, t.Plan.Kind);
            Assert.AreEqual(Topic("TOP_Relationship").pressuredAnswer, Said(t));
        }

        // ------------------------------------------------------------------ evidence

        [Test]
        public void PresentingCCTV_ExposesTheLie_AndHeSticksToTheAdmission()
        {
            _discovered.Add("EVD_CCTV");
            Ask("Where were you that evening?"); // the lie first

            DialogueTurn shown = Present("EVD_CCTV", "CCTV still, 21:40");
            Assert.IsTrue(shown.ExposedContradiction);
            Assert.AreEqual(Topic("TOP_Whereabouts").admissionAnswer, Said(shown));

            // Both related lies are now exposed.
            Assert.AreEqual(ResponseKind.TopicAdmitted, Ask("So where were you really?").Plan.Kind);
            Assert.AreEqual(ResponseKind.TopicAdmitted, Ask("Was that you on the camera?").Plan.Kind);

            // Showing it again doesn't break anything further.
            Assert.AreEqual(ResponseKind.EvidenceRepeated, Present("EVD_CCTV", "CCTV still, 21:40").Plan.Kind);
        }

        [Test]
        public void IrrelevantAndRelevantEvidence_GetDifferentReactions()
        {
            Assert.AreEqual(ResponseKind.EvidenceReaction, Present("EVD_ScenePhotos", "Crime scene photographs").Plan.Kind);
            Assert.AreEqual(ResponseKind.EvidenceIrrelevant, Present("EVD_Unrelated", "A parking ticket").Plan.Kind);
        }

        // ------------------------------------------------------------------ context + providers

        [Test]
        public void Provider_ReceivesFullContext()
        {
            var spy = new SpyProvider();
            var session = new DialogueSession(_marlowe, spy);
            session.AskAsync("Where were you last night?", null, _discovered).GetAwaiter().GetResult();
            session.PresentEvidenceAsync("EVD_ScenePhotos", "Crime scene photographs", _discovered).GetAwaiter().GetResult();
            session.AskAsync("Why?", null, _discovered).GetAwaiter().GetResult();

            DialogueContext c = spy.Last;
            Assert.AreSame(_marlowe, c.Suspect);
            Assert.AreEqual("Why?", c.PlayerInput);
            Assert.AreEqual(IntentKind.FollowUp, c.Intent.Kind);
            Assert.IsNotNull(c.Plan);
            CollectionAssert.Contains(c.PresentedEvidence.ToList(), "EVD_ScenePhotos");
            Assert.GreaterOrEqual(c.History.Count, 5, "previous conversation is available to the provider");
        }

        [Test]
        public void FailingProvider_FallsBackToMock()
        {
            var session = new DialogueSession(_marlowe, new FailingProvider(), new MockAIProvider(0, 0));
            DialogueTurn t = session.AskAsync("Where were you last night?", null, _discovered).GetAwaiter().GetResult();
            Assert.IsTrue(t.Success);
            Assert.IsTrue(t.UsedFallback);
        }

        [Test]
        public void FailingProvider_WithoutFallback_ReturnsPlayerFacingError()
        {
            var session = new DialogueSession(_marlowe, new FailingProvider());
            DialogueTurn t = session.AskAsync("Where were you last night?", null, _discovered).GetAwaiter().GetResult();
            Assert.IsFalse(t.Success);
            StringAssert.Contains("didn't respond", t.Error);
            Assert.AreEqual(0, session.TimesAsked("TOP_Whereabouts"), "state must not change when no reply arrives");
        }

        [Test]
        public void OnlineProvider_WithoutBackend_FailsCleanly()
        {
            var online = new OnlineAIProvider("");
            AIResponse r = online.GenerateResponseAsync(new DialogueContext { Plan = new ResponsePlan() }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(r.Success);
        }

        // ------------------------------------------------------------------ sample transcript

        [Test]
        public void SampleInterview_Transcript()
        {
            _discovered.AddRange(new[] { "EVD_CaseFile", "EVD_CCTV" });
            _session.BeginAsync().GetAwaiter().GetResult();
            string[] script =
            {
                "Good evening.", "Where were you on the night of the 14th?", "Can anyone confirm that?", "Why?",
                "Who had keys to the office?", "Tell me about Victor Kim.", "Did the firm have money problems?",
                "How did Daniel die?", "You're lying.", "You killed him, didn't you?"
            };
            foreach (string q in script) Assert.IsTrue(Ask(q).Success, q);
            Assert.IsTrue(Present("EVD_CCTV", "CCTV still, 21:40").ExposedContradiction);
            Ask("So you were there.");

            var sb = new StringBuilder("=== Interview transcript: Elliot Marlowe ===\n");
            foreach (DialogueMessage m in _session.History) sb.AppendLine(m.ToString());
            sb.AppendLine($"(final composure: {_session.Composure})");
            Debug.Log(sb.ToString());
            Assert.Greater(_session.History.Count, 20);
        }

        // ------------------------------------------------------------------ fakes

        private sealed class SpyProvider : IAIProvider
        {
            public DialogueContext Last;
            public string ProviderId => "spy";
            public Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken ct)
            {
                Last = context;
                return Task.FromResult(MockAIProvider.Compose(context));
            }
        }

        private sealed class FailingProvider : IAIProvider
        {
            public string ProviderId => "failing";
            public Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken ct) =>
                Task.FromResult(AIResponse.Fail("network down"));
        }
    }
}
