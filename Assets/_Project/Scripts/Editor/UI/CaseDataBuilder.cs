using System.IO;
using TLA.Data.Cases;
using UnityEditor;
using UnityEngine;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Creates the placeholder case content used by menus and the HUD, ONLY IF IT DOESN'T EXIST:
    ///   Data/Cases/CaseCatalog.asset
    ///   Data/Cases/Case0147/CASE_0147.asset + three suspect profiles
    ///   Data/Cases/Case0152/CASE_0152.asset (locked, to show "coming soon")
    /// Existing assets are never overwritten, so your own edits to the case text are safe.
    /// All names and events are fictional.
    /// </summary>
    internal static class CaseDataBuilder
    {
        public const string Folder = "Assets/_Project/Data/Cases";
        public const string CatalogPath = Folder + "/CaseCatalog.asset";
        public const string MarlowePath = Folder + "/Case0147/SUS_Marlowe.asset";

        public const string MarloweKnowledgePath = Folder + "/Case0147/SUS_Marlowe_Knowledge.asset";

        public static CaseCatalog EnsureCaseData()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CaseCatalog>(CatalogPath);
            if (catalog == null) catalog = CreateCaseData();
            EnsureSuspectPersonalities();
            return catalog;
        }

        /// <summary>
        /// Personality + knowledge for Case 0147's suspects. Only filled in when missing, so edits made
        /// in the Inspector are never overwritten. Marlowe (Interview Room 1) has full knowledge;
        /// Hayes and Kim have personalities only (their interviews come with later content).
        /// </summary>
        private static void EnsureSuspectPersonalities()
        {
            SetPersonality(Folder + "/Case0147/SUS_Hayes.asset", SuspectTemperament.Nervous, 45, "Fast, apologetic, over-explains. Fills silences.");
            SetPersonality(Folder + "/Case0147/SUS_Kim.asset", SuspectTemperament.Arrogant, 75, "Smug, sarcastic, quick to belittle. Treats the interview as a game.");

            var marlowe = AssetDatabase.LoadAssetAtPath<SuspectProfile>(MarlowePath);
            if (marlowe == null || marlowe.knowledge != null) return;

            SetPersonality(MarlowePath, SuspectTemperament.Defensive, 65,
                "Clipped, educated, irritated. Short sentences. Never apologises. Gets quieter, not louder, when cornered.");
            marlowe.knowledge = BuildMarloweKnowledge();
            EditorUtility.SetDirty(marlowe);
            AssetDatabase.SaveAssets();
        }

        private static void SetPersonality(string path, SuspectTemperament temperament, int composure, string style)
        {
            var s = AssetDatabase.LoadAssetAtPath<SuspectProfile>(path);
            if (s == null || !string.IsNullOrEmpty(s.speechStyle)) return; // already authored
            s.temperament = temperament;
            s.baseComposure = composure;
            s.speechStyle = style;
            EditorUtility.SetDirty(s);
        }

        private static SuspectKnowledge BuildMarloweKnowledge()
        {
            var k = ScriptableObject.CreateInstance<SuspectKnowledge>();

            // TRUTH (writer's reference): Marlowe went to the office at 21:40 to confront Reyes about selling
            // the firm behind his back. They argued; he says he left around 22:15 with Reyes alive.
            // He hides the visit and the sale. He does NOT know how Reyes died.

            k.topics.Add(Topic("TOP_Whereabouts", "Whereabouts", TopicStance.Lie, 1,
                "Where were you on the night of the 14th?",
                new[] { "where", "that evening", "the evening", "alibi", "whereabouts", "last night", "home", "were you", "doing that night", "the 14th" },
                new[]
                {
                    "I was at home. Alone. I already gave the officers a statement.",
                    "Home. I read, I had a drink, I went to bed early. It's not complicated.",
                    "At home, detective. I didn't leave the house."
                },
                followUp: "Because I was tired and I'm fifty-one. Is staying in suspicious now?",
                pressured: "I... might have gone out for a while. A drive. To clear my head. That's all.",
                contradictedBy: new[] { "EVD_CCTV" },
                admission: "Fine. I went to the office. About twenty to ten. Daniel and I argued and I left before half past. He was alive when I walked out.",
                truth: "Went to the office at 21:40 to confront Reyes; left around 22:15."));

            k.topics.Add(Topic("TOP_Alibi", "Alibi", TopicStance.Partial, 2,
                "Can anyone confirm you were at home?",
                new[] { "confirm", "witness", "anyone see", "can anyone", "verify", "who saw", "proof", "neighbour", "neighbor", "corroborate" },
                new[] { "No. I live alone. That's not a crime.", "My neighbours keep to themselves. So, no." },
                followUp: "Because nobody follows me around at night. Normal people don't have alibis.",
                pressured: "My car was out of the garage for an hour. A neighbour might have noticed. Happy?",
                requiresTopic: "TOP_Whereabouts",
                truth: "His car was seen leaving at about 21:25."));

            k.topics.Add(Topic("TOP_Relationship", "Relationship with Reyes", TopicStance.Partial, 3,
                "How was your relationship with Daniel Reyes?",
                new[] { "relationship", "partner", "partners", "daniel", "reyes", "get along", "friend", "friends", "victim", "colleague", "know him" },
                new[] { "We built that firm together. Eighteen years. He was my partner.", "Partners disagree. We always worked it out." },
                followUp: "Because that's what partners do. You wouldn't understand.",
                pressured: "He wanted to sell the firm. Behind my back. To people who'd have pushed me out within a year.",
                truth: "Reyes was secretly negotiating a sale that would have forced Marlowe out."));

            k.topics.Add(Topic("TOP_Money", "The firm's finances", TopicStance.Deflect, 4,
                "Was the firm in financial trouble?",
                new[] { "money", "sell", "sale", "selling", "buyer", "finances", "financial", "debt", "owe", "offer", "business", "struggling", "firm" },
                new[] { "Our finances are none of your business.", "The firm is fine. Ask our accountant if you're so interested." },
                pressured: "There was an offer on the table. Daniel wanted it. I didn't. That's all it was.",
                requiresTopic: "TOP_Relationship",
                truth: "An acquisition offer; Reyes wanted it, Marlowe opposed it."));

            k.topics.Add(Topic("TOP_Keys", "Access to the office", TopicStance.Truthful, 5,
                "Who had keys to the office?",
                new[] { "key", "keys", "keycard", "key card", "access", "get in", "entry", "alarm", "lock", "door", "building" },
                new[]
                {
                    "Daniel, me, Nora. And Victor still had a card. Daniel should have had it cancelled after he fired him.",
                    "Three of us had keys. Four, if Victor's card still works."
                },
                followUp: "Ask building management. Victor's card was never deactivated. I told Daniel twice.",
                truth: "Reyes, Marlowe, Hayes; Kim's card was never cancelled."));

            k.topics.Add(Topic("TOP_Phone", "Unanswered calls", TopicStance.Lie, 6,
                "Why didn't you answer your phone that night?",
                new[] { "phone", "call", "calls", "message", "messages", "text", "texts", "voicemail", "mobile", "answer" },
                new[] { "It was switched off. I switch it off when I go to bed.", "Off. Charging. I don't sleep with it next to me." },
                followUp: "Because I wanted a quiet night. That's all.",
                pressured: "I saw the calls. I didn't want to talk to anyone. Not that night.",
                truth: "He saw the calls and ignored them."));

            k.topics.Add(Topic("TOP_Kim", "Victor Kim", TopicStance.Truthful, 7,
                "Tell me about Victor Kim.",
                new[] { "victor", "kim", "fired", "junior", "dismissed", "sacked" },
                new[]
                {
                    "Talented, arrogant and bitter. Daniel fired him two weeks ago over who designed the Halden project.",
                    "Victor said Daniel would regret it. In front of the whole office."
                },
                followUp: "I'm telling you what he said. What he meant is your job.",
                requiresTopic: "TOP_Keys"));

            k.topics.Add(Topic("TOP_Hayes", "Nora Hayes", TopicStance.Truthful, 8,
                "What can you tell me about Nora Hayes?",
                new[] { "nora", "hayes", "office manager", "manager", "found him", "found the body" },
                new[] { "Nora runs the office. Reliable. Finding him must have been awful for her.", "She's been with us eight years. I trust her more than most." }));

            k.topics.Add(Topic("TOP_Death", "How Reyes died", TopicStance.Unknown, 9,
                "",
                new[] { "how did he die", "how did", "die", "killed", "how was he killed", "weapon", "cause of death", "died", "death", "wound", "blood", "murder", "murdered", "body" },
                new[]
                {
                    "I don't know. Nobody's told me how. I'm not sure I want to know.",
                    "I wasn't there when he was found. I don't know anything about how it happened."
                }));

            k.topics.Add(Topic("TOP_CCTV", "CCTV at 21:40", TopicStance.Lie, 1,
                "Someone entered the building at 21:40. Was that you?",
                new[] { "cctv", "camera", "cameras", "footage", "video", "recording", "21 40", "9 40", "lobby", "came in" },
                new[] { "People come and go from that building all evening. It wasn't me.", "Show me a face on that footage and then we'll talk." },
                followUp: "Because I told you. I was at home.",
                contradictedBy: new[] { "EVD_CCTV" },
                admission: "Alright. Yes. That was me. I went to talk to Daniel. That's all I did.",
                requiresEvidence: "EVD_CCTV"));

            k.evidenceReactions.Add(new EvidenceReaction { evidenceId = "EVD_ScenePhotos", reaction = "Don't. I don't need to see that. Put it away.", composureImpact = 8 });
            k.evidenceReactions.Add(new EvidenceReaction { evidenceId = "EVD_CaseFile", reaction = "So you've read the file. Then you know I wasn't the only one with a key.", composureImpact = 0 });

            k.greeting = "Detective. I've already given a statement. I assume this won't take long.";
            k.unknownQuestion = new[] { "I don't see what that has to do with Daniel.", "Ask me something relevant.", "I'm an architect, not a mind reader. What are you asking?" };
            k.accusationReplies = new[]
            {
                "I didn't kill Daniel. I want that on the record.",
                "You've got nothing, so you're guessing. Guess again.",
                "Eighteen years. You think I'd throw that away?"
            };
            k.repeatedQuestion = new[] { "I already told you.", "Asking twice won't get you a different answer." };
            k.irrelevantEvidence = new[] { "And? What am I supposed to say to that?", "That means nothing to me." };

            AssetDatabase.CreateAsset(k, MarloweKnowledgePath);
            return k;
        }

        private static KnowledgeTopic Topic(string id, string name, TopicStance stance, int priority, string suggestion, string[] keywords,
            string[] answers, string followUp = "", string pressured = "", string[] contradictedBy = null, string admission = "",
            string requiresTopic = "", string requiresEvidence = "", string truth = "")
        {
            return new KnowledgeTopic
            {
                topicId = id, displayName = name, stance = stance, priority = priority, suggestedQuestion = suggestion,
                keywords = keywords, answers = answers, followUpAnswer = followUp, pressuredAnswer = pressured,
                contradictedByEvidence = contradictedBy ?? System.Array.Empty<string>(), admissionAnswer = admission,
                requiresTopic = requiresTopic, requiresEvidence = requiresEvidence, truth = truth
            };
        }

        private static CaseCatalog CreateCaseData()
        {
            CaseCatalog catalog;

            Directory.CreateDirectory(Folder + "/Case0147");
            Directory.CreateDirectory(Folder + "/Case0152");

            SuspectProfile marlowe = Suspect("SUS_Marlowe", "Elliot Marlowe", 51, "Architect", "Business partner of the victim", "Person of interest",
                "Co-founded the practice with Reyes eighteen years ago. Says he spent the evening at home, alone.");
            SuspectProfile hayes = Suspect("SUS_Hayes", "Nora Hayes", 34, "Office manager", "Found the body", "Witness",
                "Called it in at 23:52. Says she came back to the office for her laptop.");
            SuspectProfile kim = Suspect("SUS_Kim", "Victor Kim", 29, "Junior architect", "Dismissed by the victim two weeks ago", "Person of interest",
                "Let go after a dispute over credit for a project. His key card was never deactivated.");

            var case0147 = ScriptableObject.CreateInstance<CaseDefinition>();
            case0147.caseId = "CASE_0147";
            case0147.caseNumber = "0147";
            case0147.title = "Silence on Harlow Street";
            case0147.location = "Harlow Street, Eastside";
            case0147.briefing =
                "Daniel Reyes, 46, co-owner of Reyes & Marlowe Architects, was found dead in his office at 23:50 on the night of the 14th.\n\n" +
                "The building's alarm was never triggered. Three people had a way in. One of them is lying.";
            case0147.initialObjective = "Review the case files in the Evidence Room.";
            case0147.startScene = "PoliceStation";
            case0147.isAvailable = true;
            case0147.evidenceTotal = 6;
            case0147.suspects.Add(marlowe);
            case0147.suspects.Add(hayes);
            case0147.suspects.Add(kim);
            AssetDatabase.CreateAsset(case0147, Folder + "/Case0147/CASE_0147.asset");

            var case0152 = ScriptableObject.CreateInstance<CaseDefinition>();
            case0152.caseId = "CASE_0152";
            case0152.caseNumber = "0152";
            case0152.title = "The Ferryman";
            case0152.location = "Port District";
            case0152.isAvailable = false;
            AssetDatabase.CreateAsset(case0152, Folder + "/Case0152/CASE_0152.asset");

            catalog = ScriptableObject.CreateInstance<CaseCatalog>();
            catalog.cases.Add(case0147);
            catalog.cases.Add(case0152);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static SuspectProfile Suspect(string id, string name, int age, string occupation, string relation, string status, string summary)
        {
            var s = ScriptableObject.CreateInstance<SuspectProfile>();
            s.suspectId = id;
            s.displayName = name;
            s.age = age;
            s.occupation = occupation;
            s.relationToCase = relation;
            s.status = status;
            s.summary = summary;
            AssetDatabase.CreateAsset(s, $"{Folder}/Case0147/{id}.asset");
            return s;
        }
    }
}
