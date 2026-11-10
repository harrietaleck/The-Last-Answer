using System;
using System.IO;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using UnityEditor;
using UnityEngine;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Investigation test content for Case 0147, created or filled ONLY IF MISSING:
    ///   - source / reliability / related evidence / related statements for the existing facts
    ///   - two facts confirmed by confronting Marlowe (FACT_MarloweAtOffice, FACT_MarloweKnewSale)
    ///   - five statements by Marlowe (Data/Cases/Case0147/Statements) with their contradiction rules
    ///
    /// Scenarios they demonstrate:
    ///   VALID       "I was at home" vs the CCTV still, once the CCTV and access log are connected
    ///               (before that: NOT PROVEN, the face is hidden)
    ///   INVALID     "I was at home" vs the coffee cups (or any unrelated evidence): the challenge fails
    ///   UNRESOLVED  "My phone was off": nothing in the case contradicts it, so it stays open
    /// All names and events are fictional.
    /// </summary>
    internal static class InvestigationDataBuilder
    {
        public const string StatementFolder = CaseDataBuilder.Folder + "/Case0147/Statements";

        public static string StatementPath(string id) => $"{StatementFolder}/{id}.asset";

        public static void EnsureInvestigationData()
        {
            var c = AssetDatabase.LoadAssetAtPath<CaseDefinition>(EvidenceDataBuilder.CasePath);
            var marlowe = AssetDatabase.LoadAssetAtPath<SuspectProfile>(CaseDataBuilder.MarlowePath);
            if (c == null || marlowe == null) return;
            Directory.CreateDirectory(StatementFolder);

            EvidenceDefinition caseFile = Ev("EVD_CaseFile"), photos = Ev("EVD_ScenePhotos"), cctv = Ev("EVD_CCTV");
            EvidenceDefinition log = Ev("EVD_KeycardLog"), cups = Ev("EVD_CoffeeCups"), contract = Ev("EVD_SaleContract");

            CaseFact noForced = Fact("FACT_NoForcedEntry"), found = Fact("FACT_FoundAt2350"), keyholders = Fact("FACT_ThreeKeyholders");
            CaseFact entry = Fact("FACT_EntryAt2140"), code = Fact("FACT_DoorCodeUsed"), visitor = Fact("FACT_VictimHadVisitor");
            CaseFact sale = Fact("FACT_SecretSale"), knewCode = Fact("FACT_EntrantKnewCode"), welcome = Fact("FACT_VisitorWasWelcome");
            CaseFact motive = Fact("FACT_MarloweMotive");

            // ---- Facts confirmed by confronting Marlowe
            CaseFact atOffice = NewFact("FACT_MarloweAtOffice", "Marlowe was at the office that night",
                "Marlowe went to the office at about 21:40, despite telling officers he was at home all evening.", "Marlowe, confronted with the evidence", FactReliability.High);
            CaseFact knewSale = NewFact("FACT_MarloweKnewSale", "Marlowe knew about the sale",
                "Marlowe knew Reyes was selling his share to Calder Holdings before the night of the murder.", "Marlowe, confronted with the sale agreement", FactReliability.High);
            AddIfMissing(c, atOffice);
            AddIfMissing(c, knewSale);

            // ---- Statements
            SuspectStatement home = Statement("STM_Marlowe_Home", out bool newHome);
            SuspectStatement notOnCamera = Statement("STM_Marlowe_NotOnCamera", out bool newCam);
            SuspectStatement finances = Statement("STM_Marlowe_Finances", out bool newFin);
            SuspectStatement phone = Statement("STM_Marlowe_Phone", out bool newPhone);
            SuspectStatement kimCard = Statement("STM_Marlowe_KimCard", out bool newKim);

            const string hiddenFace = "The figure's face is hidden. On its own the footage could show anyone. Work out who could have got in at 21:38.";
            const string codeExplanation = "The master code was used at 21:38 and the figure walked in two minutes later. Only the partners and the office manager know that code, and Hayes's card shows she wasn't there until 23:47. Reyes was already inside.";

            if (newHome)
                Fill(home, marlowe, "TOP_Whereabouts", "I was at home, alone, all evening. I never left the house.", "Statement to officers", true,
                    Rule(cctv, codeExplanation, hiddenFace, atOffice, knewCode),
                    Rule(log, codeExplanation, "The log shows the master code was used, not who used it. Compare it with what else happened at 21:40.", atOffice, knewCode));

            if (newCam)
                Fill(notOnCamera, marlowe, "TOP_CCTV", "The person on the camera at 21:40 wasn't me.", "Interview", false,
                    Rule(cctv, codeExplanation, hiddenFace, atOffice, knewCode),
                    Rule(log, codeExplanation, "The log alone doesn't put anyone on camera. Connect it with the footage first.", atOffice, knewCode));

            if (newFin)
                Fill(finances, marlowe, "TOP_Money", "The firm's finances are fine. Nothing worth discussing.", "Interview", false,
                    Rule(contract, "A confidential draft shows Reyes selling his majority share to Calder Holdings, with Marlowe's position \"under review\". The firm was being sold out from under him: the finances were not \"fine\".",
                        "It's a draft agreement. Read it properly before you put it in front of him.", knewSale, sale));

            if (newPhone)
                Fill(phone, marlowe, "TOP_Phone", "My phone was switched off. I'd gone to bed.", "Interview", false);

            if (newKim)
                Fill(kimCard, marlowe, "TOP_Keys", "Victor Kim's key card was never deactivated.", "Interview", false);

            foreach (SuspectStatement s in new[] { home, notOnCamera, finances, phone, kimCard })
            {
                EditorUtility.SetDirty(s);
                if (!c.statements.Contains(s)) c.statements.Add(s);
            }

            // ---- Fact metadata (only facts without a source yet)
            Meta(noForced, "Crime scene photographs", FactReliability.High, new[] { photos, caseFile });
            Meta(found, "Case file 0147 (incident report)", FactReliability.High, new[] { caseFile });
            Meta(keyholders, "Case file 0147", FactReliability.High, new[] { caseFile }, kimCard);
            Meta(entry, "Lobby CCTV", FactReliability.High, new[] { cctv }, home, notOnCamera);
            Meta(code, "Building access log", FactReliability.High, new[] { log }, home);
            Meta(visitor, "Lab note on the coffee cups", FactReliability.High, new[] { cups, photos });
            Meta(sale, "Draft sale agreement (unsigned)", FactReliability.Medium, new[] { contract }, finances);
            Meta(knewCode, "CCTV and access log, compared", FactReliability.Medium, new[] { cctv, log }, home, notOnCamera);
            Meta(welcome, "Scene photos and coffee cups, compared", FactReliability.Medium, new[] { photos, cups });
            Meta(motive, "Sale agreement and case file, compared", FactReliability.Medium, new[] { contract, caseFile }, finances);
            Meta(atOffice, null, FactReliability.High, new[] { cctv, log }, home, notOnCamera);
            Meta(knewSale, null, FactReliability.High, new[] { contract }, finances);

            // ---- Marlowe: shown the CCTV before it proves anything, he shrugs it off.
            var knowledge = AssetDatabase.LoadAssetAtPath<SuspectKnowledge>(CaseDataBuilder.MarloweKnowledgePath);
            if (knowledge != null && knowledge.FindReaction("EVD_CCTV") == null)
            {
                knowledge.evidenceReactions.Add(new EvidenceReaction { evidenceId = "EVD_CCTV", reaction = "Someone in a dark coat. Half this city owns a dark coat.", composureImpact = 4 });
                EditorUtility.SetDirty(knowledge);
            }

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ helpers

        private static EvidenceDefinition Ev(string id) => AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(EvidenceDataBuilder.EvidencePath(id));

        private static CaseFact Fact(string id) => AssetDatabase.LoadAssetAtPath<CaseFact>($"{EvidenceDataBuilder.FactFolder}/{id}.asset");

        private static CaseFact NewFact(string id, string title, string description, string source, FactReliability reliability)
        {
            CaseFact f = Fact(id);
            if (f != null) return f;
            f = ScriptableObject.CreateInstance<CaseFact>();
            f.factId = id;
            f.title = title;
            f.description = description;
            f.source = source;
            f.reliability = reliability;
            f.isKeyFact = true;
            AssetDatabase.CreateAsset(f, $"{EvidenceDataBuilder.FactFolder}/{id}.asset");
            return f;
        }

        private static void AddIfMissing(CaseDefinition c, CaseFact f)
        {
            if (f != null && !c.facts.Contains(f)) c.facts.Add(f);
        }

        private static SuspectStatement Statement(string id, out bool created)
        {
            var s = AssetDatabase.LoadAssetAtPath<SuspectStatement>(StatementPath(id));
            created = s == null;
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<SuspectStatement>();
            s.statementId = id;
            AssetDatabase.CreateAsset(s, StatementPath(id));
            return s;
        }

        private static void Fill(SuspectStatement s, SuspectProfile suspect, string topic, string claim, string source, bool fromStart, params ContradictionRule[] rules)
        {
            s.suspect = suspect;
            s.topicId = topic;
            s.claim = claim;
            s.source = source;
            s.recordedFromStart = fromStart;
            s.contradictions = rules ?? Array.Empty<ContradictionRule>();
        }

        private static ContradictionRule Rule(EvidenceDefinition evidence, string explanation, string unproven, CaseFact confirms, params CaseFact[] requires) =>
            new ContradictionRule
            {
                evidence = evidence,
                explanation = explanation,
                unprovenHint = unproven,
                confirmsFact = confirms,
                requiresConfirmed = requires ?? Array.Empty<CaseFact>()
            };

        private static void Meta(CaseFact f, string source, FactReliability reliability, EvidenceDefinition[] evidence, params SuspectStatement[] statements)
        {
            if (f == null) return;
            bool changed = false;
            if (string.IsNullOrEmpty(f.source) && source != null)
            {
                f.source = source;
                f.reliability = reliability;
                changed = true;
            }
            if ((f.relatedEvidence == null || f.relatedEvidence.Length == 0) && evidence != null)
            {
                f.relatedEvidence = evidence;
                changed = true;
            }
            if ((f.relatedStatements == null || f.relatedStatements.Length == 0) && statements != null && statements.Length > 0)
            {
                f.relatedStatements = statements;
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(f);
        }
    }
}
