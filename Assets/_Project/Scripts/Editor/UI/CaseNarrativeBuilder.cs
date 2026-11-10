using System.Collections.Generic;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using UnityEditor;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Fills in the narrative and progression of Case 0147 ("Silence on Harlow Street"), ONLY WHERE
    /// EMPTY: summary, crime, victim, witnesses, timeline, objectives, conclusions, endings and the
    /// writer's solution notes. It also aligns Marlowe's knowledge with the solution (his "I don't
    /// know how he died" is a lie he keeps up). Everything is consistent with the evidence, facts and
    /// statements created by EvidenceDataBuilder and InvestigationDataBuilder. Fictional content.
    /// </summary>
    internal static class CaseNarrativeBuilder
    {
        public static void EnsureNarrativeData()
        {
            var c = AssetDatabase.LoadAssetAtPath<CaseDefinition>(EvidenceDataBuilder.CasePath);
            if (c == null) return;
            SuspectProfile marlowe = Person("SUS_Marlowe"), hayes = Person("SUS_Hayes"), kim = Person("SUS_Kim");
            if (marlowe == null) return;

            EvidenceDefinition caseFile = Ev("EVD_CaseFile"), photos = Ev("EVD_ScenePhotos"), cctv = Ev("EVD_CCTV");
            CaseFact doorCode = Fact("FACT_DoorCodeUsed"), entry = Fact("FACT_EntryAt2140"), knewCode = Fact("FACT_EntrantKnewCode");
            CaseFact atOffice = Fact("FACT_MarloweAtOffice"), knewSale = Fact("FACT_MarloweKnewSale"), motive = Fact("FACT_MarloweMotive");
            CaseFact visitor = Fact("FACT_VictimHadVisitor"), welcome = Fact("FACT_VisitorWasWelcome");
            SuspectStatement home = Statement("STM_Marlowe_Home"), finances = Statement("STM_Marlowe_Finances");

            if (string.IsNullOrEmpty(c.summary))
                c.summary = "Architect Daniel Reyes was found dead in his office on Harlow Street. No forced entry, no alarm. Three people could get in, and one of them is lying.";

            if (string.IsNullOrEmpty(c.crime.causeOfDeath))
                c.crime = new CrimeInfo
                {
                    crimeType = "Homicide",
                    date = "Night of the 14th",
                    timeOfDeath = "21:30 - 22:30 (pathologist's estimate)",
                    location = "Office of Reyes & Marlowe Architects, 3rd floor, 18 Harlow Street",
                    causeOfDeath = "Blunt-force injury to the head",
                    weapon = "Not recovered",
                    discoveredBy = "Nora Hayes, office manager, at 23:50. She called it in at 23:52.",
                    details = "No sign of forced entry and no struggle in the outer office. The building alarm never went off."
                };

            if (string.IsNullOrEmpty(c.victim.name))
                c.victim = new VictimInfo
                {
                    name = "Daniel Reyes",
                    age = 46,
                    occupation = "Architect, co-owner of Reyes & Marlowe Architects",
                    background = "Founded the practice with Elliot Marlowe eighteen years ago. Known for working late and keeping his business close. Divorced, no children."
                };

            if (c.witnesses.Count == 0 && hayes != null) c.witnesses.Add(hayes); // found the body (and had a key, so also a suspect)

            if (c.timeline.Count == 0)
                c.timeline.AddRange(new[]
                {
                    T("12th", "Marlowe learns from Calder Holdings' lawyer that Reyes is selling his share of the firm.", knewSale, true),
                    T("17:58", "Nora Hayes leaves the office (key card).", doorCode),
                    T("18:40", "Elliot Marlowe leaves the office (key card).", doorCode),
                    T("19:10", "Reyes, working late, arms the building alarm.", doorCode),
                    T("21:38", "The master code opens the lobby door and disarms the alarm. No key card is used.", doorCode, true),
                    T("21:40", "A figure in a long dark coat walks into the lobby and straight to the stairs.", entry, true),
                    T("21:40", "Marlowe arrives at the office to see Reyes, by his own admission.", atOffice, true),
                    T("23:47", "Hayes's key card opens the lobby door.", doorCode),
                    T("23:50", "Hayes finds Reyes dead at his desk.", null),
                    T("23:52", "Hayes calls the police.", null),
                    T("00:06", "The first officer arrives at the scene.", null)
                });

            if (c.objectives.Count == 0)
            {
                c.objectives.AddRange(new[]
                {
                    O("OBJ_ReviewCaseFile", "Review the case file", "Read case file 0147 on the investigation desk in the Evidence Room.", false, null,
                        Collected(caseFile)),
                    O("OBJ_SearchEvidenceRoom", "Search the Evidence Room", "Study the scene photographs on the evidence board and the CCTV archive on the laptop.", false, new[] { "OBJ_ReviewCaseFile" },
                        Collected(photos), Collected(cctv)),
                    O("OBJ_QuestionMarlowe", "Question Elliot Marlowe", "Marlowe is waiting in Interview Room 1. Hear his side of the night.", false, new[] { "OBJ_ReviewCaseFile" },
                        new CaseCondition { type = CaseConditionType.SuspectQuestioned, suspect = marlowe }),
                    O("OBJ_TraceEntry", "Find out who came in at 21:40", "Someone walked into the lobby at 21:40. Work out how they got in, and what that says about who they were.", false, new[] { "OBJ_SearchEvidenceRoom" },
                        FactC(knewCode)),
                    O("OBJ_BreakAlibi", "Break Marlowe's alibi", "He told officers he was at home all evening. Prove otherwise, then confront him with it.", false, new[] { "OBJ_QuestionMarlowe", "OBJ_TraceEntry" },
                        new CaseCondition { type = CaseConditionType.StatementExposed, statement = home }),
                    O("OBJ_FindMotive", "Find a motive", "Why would anyone want Daniel Reyes dead? The firm's paperwork may say.", false, new[] { "OBJ_ReviewCaseFile" },
                        FactC(motive)),
                    O("OBJ_TheVisitor", "Work out who Reyes was expecting", "The scene photos show two cups on the side table. Find out what they mean.", true, new[] { "OBJ_SearchEvidenceRoom" },
                        FactC(welcome)),
                    O("OBJ_SaleAdmission", "Make Marlowe admit he knew about the sale", "He says the firm's finances were fine. Show him why that isn't true.", true, new[] { "OBJ_QuestionMarlowe", "OBJ_FindMotive" },
                        new CaseCondition { type = CaseConditionType.StatementExposed, statement = finances }),
                    O("OBJ_CloseCase", "Close the case", "When you're ready, open the notebook (J) and choose CLOSE THE CASE. The decision is final.", false, new[] { "OBJ_QuestionMarlowe" },
                        new CaseCondition { type = CaseConditionType.DecisionMade, id = "DEC_CloseCase" })
                });
                c.conclusionUnlockedBy = new[] { new CaseCondition { type = CaseConditionType.ObjectiveCompleted, id = "OBJ_QuestionMarlowe" } };
                c.conclusionDecisionId = "DEC_CloseCase";
            }

            if (c.conclusions.Count == 0)
            {
                c.conclusions.AddRange(new[]
                {
                    Con("CON_ChargeMarlowe", "Charge Elliot Marlowe with murder", "Reyes's partner of eighteen years. Make sure you can prove he was there, and why.", marlowe),
                    Con("CON_ChargeKim", "Charge Victor Kim with murder", "The junior architect Reyes fired two weeks ago, whose key card was never cancelled.", kim),
                    Con("CON_ChargeHayes", "Charge Nora Hayes with murder", "The office manager who found the body and called it in.", hayes),
                    Con("CON_NoCharge", "Charge no one", "Not enough evidence to charge anyone. Marlowe goes home and the case stays open.", null)
                });
            }

            if (c.endings.Count == 0)
            {
                c.endings.AddRange(new[]
                {
                    E("END_Solved", "The Last Answer", EndingOutcome.Solved, "CON_ChargeMarlowe",
                        "Marlowe's solicitor arrives talking about a misunderstanding between partners. Then the file is laid out in order: the master code at 21:38, the figure on camera at 21:40, the cup rinsed and wiped clean, the sale he'd known about since the 12th.\n\nHe stops talking. Prosecutors charge Elliot Marlowe with the murder of Daniel Reyes. He never confesses. He doesn't have to.",
                        new CaseCondition { type = CaseConditionType.StatementExposed, statement = home }, FactC(motive), FactC(visitor)),
                    E("END_WeakCase", "A Thin File", EndingOutcome.PartiallySolved, "CON_ChargeMarlowe",
                        "Marlowe is charged. He lied about the night and he was there, and the file proves that much. But without a motive on record, or anything to show what happened in that room, his defence calls it a late visit between old partners that ended in an argument.\n\nThe trial is months away, and the case is weaker than it should be.",
                        new CaseCondition { type = CaseConditionType.StatementExposed, statement = home }),
                    E("END_Collapsed", "Released by Morning", EndingOutcome.Failed, "CON_ChargeMarlowe",
                        "Marlowe's alibi was never broken. His solicitor has him out within hours: nothing places him on Harlow Street that night. The charge is dropped before breakfast, and Marlowe now knows exactly what you have, and what you don't."),
                    E("END_WrongKim", "The Wrong Man", EndingOutcome.Failed, "CON_ChargeKim",
                        "Victor Kim had a grudge and an old key card, and that's all. The access log shows his card was never used that night, and nothing puts him near Harlow Street. The charge collapses.\n\nWhoever was in that office at 21:40 has had time to get rid of everything that mattered."),
                    E("END_WrongHayes", "The Wrong Witness", EndingOutcome.Failed, "CON_ChargeHayes",
                        "Nora Hayes's card opened the lobby door at 23:47, three minutes before she found the body and called it in. The pathologist puts the death between 21:30 and 22:30.\n\nThe charge doesn't survive the first hearing, and the real killer is still out there."),
                    E("END_Unsolved", "An Open File", EndingOutcome.Unsolved, "CON_NoCharge",
                        "No charge is brought. The file on Daniel Reyes goes back into the cabinet in the Evidence Room, still open.\n\nSomewhere in the city, someone knows exactly what happened at 18 Harlow Street, and is starting to believe they got away with it.")
                });
            }

            if (string.IsNullOrEmpty(c.solutionNotes))
                c.solutionNotes =
                    "THE TRUTH (writers only). Elliot Marlowe killed Daniel Reyes.\n" +
                    "On the 12th Marlowe learned from Calder Holdings' lawyer that Reyes was selling his 60% share, which would put Marlowe's position 'under review'. " +
                    "On the 14th Marlowe left work at 18:40 by key card. At 21:38 he came back and used the master code instead of his card, so his card wouldn't show; " +
                    "this disarmed the alarm Reyes had set at 19:10. The lobby camera caught him at 21:40 in a long dark coat, carrying the draft agreement in a flat folder. " +
                    "Reyes made them both coffee. They argued about the sale; Marlowe struck him with a heavy object from the desk, rinsed and wiped his own cup, took the weapon " +
                    "and left by the stairs around 22:15 (exits by the stairs aren't logged). He ignored the calls on his phone that night.\n" +
                    "Under questioning he admits only the visit and the argument ('He was alive when I walked out'), never the killing. " +
                    "Hayes is innocent: she left at 17:58 and came back at 23:47 for her laptop, after the time of death. Kim is innocent: his card was never used.";

            AlignMarloweKnowledge();

            // Objectives now drive the HUD objective; the case file's old one-off objective is no longer needed.
            if (caseFile != null && caseFile.objectiveOnCollect == "Interview Elliot Marlowe in Interview Room 1.")
            {
                caseFile.objectiveOnCollect = "";
                EditorUtility.SetDirty(caseFile);
            }

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Marlowe is the killer: "I don't know how he died" is a lie he maintains, not a true unknown.</summary>
        private static void AlignMarloweKnowledge()
        {
            var k = AssetDatabase.LoadAssetAtPath<SuspectKnowledge>(CaseDataBuilder.MarloweKnowledgePath);
            if (k == null) return;
            KnowledgeTopic death = k.FindTopic("TOP_Death");
            if (death != null && death.stance == TopicStance.Unknown)
            {
                death.stance = TopicStance.Lie; // same "I don't know" answers, now as a lie
                death.truth = "He killed Reyes with a heavy object from the desk and took it with him.";
            }
            KnowledgeTopic where = k.FindTopic("TOP_Whereabouts");
            if (where != null && where.truth == "Went to the office at 21:40 to confront Reyes; left around 22:15.")
                where.truth = "Came back at 21:38 using the master code (not his card), argued with Reyes about the sale and killed him; wiped his cup and left by the stairs around 22:15.";
            EditorUtility.SetDirty(k);
        }

        // ------------------------------------------------------------------ helpers

        private static SuspectProfile Person(string id) => AssetDatabase.LoadAssetAtPath<SuspectProfile>($"{CaseDataBuilder.Folder}/Case0147/{id}.asset");
        private static EvidenceDefinition Ev(string id) => AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(EvidenceDataBuilder.EvidencePath(id));
        private static CaseFact Fact(string id) => AssetDatabase.LoadAssetAtPath<CaseFact>($"{EvidenceDataBuilder.FactFolder}/{id}.asset");
        private static SuspectStatement Statement(string id) => AssetDatabase.LoadAssetAtPath<SuspectStatement>(InvestigationDataBuilder.StatementPath(id));

        private static TimelineEntry T(string time, string text, CaseFact revealedBy, bool key = false) =>
            new TimelineEntry { time = time, description = text, revealedBy = revealedBy, isKey = key };

        private static CaseCondition Collected(EvidenceDefinition e) => new CaseCondition { type = CaseConditionType.EvidenceCollected, evidence = e };
        private static CaseCondition FactC(CaseFact f) => new CaseCondition { type = CaseConditionType.FactConfirmed, fact = f };

        private static ObjectiveDefinition O(string id, string title, string description, bool optional, string[] prerequisites, params CaseCondition[] completion) =>
            new ObjectiveDefinition
            {
                objectiveId = id, title = title, description = description, optional = optional,
                prerequisites = prerequisites ?? new string[0], completion = completion
            };

        private static ConclusionDefinition Con(string id, string title, string description, SuspectProfile accused) =>
            new ConclusionDefinition { conclusionId = id, title = title, description = description, accused = accused };

        private static EndingDefinition E(string id, string title, EndingOutcome outcome, string conclusionId, string epilogue, params CaseCondition[] conditions) =>
            new EndingDefinition { endingId = id, title = title, outcome = outcome, conclusionId = conclusionId, epilogue = epilogue, conditions = conditions };
    }
}
