using System;
using System.IO;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using UnityEditor;
using UnityEngine;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Test evidence for Case 0147, created ONLY IF MISSING (existing assets are never overwritten):
    ///   Data/Cases/Case0147/Facts/FACT_*.asset      10 case facts
    ///   Data/Cases/Case0147/Evidence/EVD_*.asset    6 evidence items
    /// It also adds Marlowe's reactions to the new items (only reactions he doesn't have yet).
    ///
    /// Discovery chain: Case file → (examine) → Access log;  Scene photos → (examine) → Coffee cups;
    /// Case file (collect) → Sale agreement.  Connections: CCTV + Access log, Photos + Cups,
    /// Sale agreement + Case file. All names and events are fictional.
    /// </summary>
    internal static class EvidenceDataBuilder
    {
        public const string CasePath = CaseDataBuilder.Folder + "/Case0147/CASE_0147.asset";
        public const string EvidenceFolder = CaseDataBuilder.Folder + "/Case0147/Evidence";
        public const string FactFolder = CaseDataBuilder.Folder + "/Case0147/Facts";

        public static string EvidencePath(string id) => $"{EvidenceFolder}/{id}.asset";

        public static void EnsureEvidenceData()
        {
            var case0147 = AssetDatabase.LoadAssetAtPath<CaseDefinition>(CasePath);
            if (case0147 == null) return;
            Directory.CreateDirectory(EvidenceFolder);
            Directory.CreateDirectory(FactFolder);

            // ---- Facts
            CaseFact noForcedEntry = Fact("FACT_NoForcedEntry", "No forced entry", "The office door and lock were undamaged. Whoever came in had a key or the code.", true);
            CaseFact foundAt2350 = Fact("FACT_FoundAt2350", "Body found at 23:50", "Nora Hayes found Reyes at 23:50 and called it in two minutes later.", false);
            CaseFact keyholders = Fact("FACT_ThreeKeyholders", "Three keyholders", "Reyes, Marlowe and Hayes had keys. Victor Kim's old card was never cancelled.", false);
            CaseFact entryAt2140 = Fact("FACT_EntryAt2140", "Someone entered at 21:40", "The lobby camera shows a figure in a long dark coat entering at 21:40.", true);
            CaseFact codeUsed = Fact("FACT_DoorCodeUsed", "Master code used at 21:38", "At 21:38 the lobby door was opened, and the alarm disarmed, with the master code. No key card was used.", true);
            CaseFact visitor = Fact("FACT_VictimHadVisitor", "Reyes had a visitor", "Two cups were used in Reyes's office that evening. One was rinsed and wiped clean.", true);
            CaseFact sale = Fact("FACT_SecretSale", "Reyes was selling the firm", "A draft agreement shows Reyes planned to sell his share to Calder Holdings, and to keep it from Marlowe.", true);
            CaseFact knewCode = Fact("FACT_EntrantKnewCode", "The 21:40 visitor knew the code", "The figure on camera arrived two minutes after the master code was used. They knew the code, so they knew the firm.", true);
            CaseFact welcome = Fact("FACT_VisitorWasWelcome", "Reyes knew his visitor", "Reyes made his visitor coffee. Whoever came that night was someone he let in.", true);
            CaseFact motive = Fact("FACT_MarloweMotive", "Marlowe stood to lose the firm", "The sale would have handed control to Calder Holdings and put Marlowe's position under review.", true);

            // ---- Evidence (created first, linked afterwards so items can reference each other)
            EvidenceDefinition caseFile = Item("EVD_CaseFile", out bool newCaseFile);
            EvidenceDefinition photos = Item("EVD_ScenePhotos", out bool newPhotos);
            EvidenceDefinition cctv = Item("EVD_CCTV", out bool newCctv);
            EvidenceDefinition accessLog = Item("EVD_KeycardLog", out bool newLog);
            EvidenceDefinition cups = Item("EVD_CoffeeCups", out bool newCups);
            EvidenceDefinition contract = Item("EVD_SaleContract", out bool newContract);

            if (newCaseFile)
            {
                Fill(caseFile, "Case file 0147", EvidenceCategory.Document, "Evidence Room · investigation desk", "Read case file",
                    "The initial incident report: the victim, the time he was found, who could get into the building and the persons of interest.",
                    "The access section is underlined twice in pencil: \"Building access log requested.\" A copy should be in the filing cabinet by the shelves.",
                    new[] { foundAt2350, keyholders }, collectable: true);
                caseFile.pages = new[]
                {
                    "METROPOLITAN POLICE  ·  CASE 0147\nINCIDENT REPORT\n\nVictim: Daniel REYES, 46. Co-owner of Reyes & Marlowe Architects, 3rd floor, 18 Harlow Street.\n\nReported 23:52 on the 14th by Nora HAYES (office manager), who returned for her laptop and found the victim at his desk.\n\nFirst officer on scene 00:06. Victim pronounced dead at the scene.",
                    "SCENE\n\nOffice door closed but not locked. No damage to door, frame or lock.\nBuilding alarm armed at 19:10. It never triggered.\nNo sign of a struggle in the outer office.\n\nACCESS\n\nKey holders: D. Reyes, E. Marlowe, N. Hayes.\nCard issued to V. Kim (dismissed two weeks ago) not yet deactivated.\nBuilding access log requested.",
                    "PERSONS OF INTEREST\n\nElliot MARLOWE, 51  ·  business partner. States he was at home all evening.\nNora HAYES, 34  ·  office manager. Found the body.\nVictor KIM, 29  ·  former junior architect. Dismissed after a dispute with the victim.\n\nNEXT STEPS\nInterview Marlowe. Obtain the building access log. Review lobby CCTV."
                };
                caseFile.noteOnCollect = "Reyes was found at 23:50 and the alarm never went off. Whoever was there had a key: Marlowe, Hayes or Kim.";
                caseFile.objectiveOnCollect = "Interview Elliot Marlowe in Interview Room 1.";
            }

            if (newPhotos)
            {
                Fill(photos, "Crime scene photographs", EvidenceCategory.Photograph, "Evidence Room · evidence board", "Study crime scene photos",
                    "Scene photographs pinned to the evidence board: the office door, the victim's desk and the side table.",
                    "Up close, the lock and strike plate are clean: no scratches, no tool marks. On the side table by the window there are two cups. Only one has a saucer.",
                    new[] { noForcedEntry }, collectable: false);
                photos.caption = "Office of D. Reyes, 18 Harlow Street. Photographed 00:40 on the 15th.";
                photos.pages = new[] { "PHOTO 1  The office door, closed. Lock intact.\nPHOTO 2  The victim's desk. Papers pushed aside, desk lamp still on.\nPHOTO 3  A side table by the window. Two coffee cups." };
            }

            if (newCctv)
            {
                Fill(cctv, "CCTV still, 21:40", EvidenceCategory.Digital, "Evidence Room · laptop, CCTV archive", "Review CCTV footage",
                    "A frame from the lobby camera: someone entering the building at 21:40, face hidden.",
                    "Frame by frame: the figure is about 1.8 m tall and doesn't hesitate at the stairs. They have been here before.",
                    new[] { entryAt2140 }, collectable: false);
                cctv.pages = new[] { "HARLOW ST 18  ·  LOBBY CAM 02\n14th  ·  21:40:12\n\nA figure enters through the main door and walks straight to the stairs without looking at the reception desk.\n\nLong dark coat. Hood or hat. Something flat carried under the left arm.\n\nThe figure is not seen on this camera again before 23:50." };
                cctv.connections = new[] { Link(accessLog, "The master code was used at 21:38. Two minutes later the figure walks into the lobby. Whoever came at 21:40 knew the code: someone from the firm.", knewCode) };
            }

            if (newLog)
            {
                Fill(accessLog, "Building access log", EvidenceCategory.Document, "Evidence Room · filing cabinet", "Read access log",
                    "The building's door and alarm log for the night of the 14th.",
                    "Nobody used a key card between 18:40 and 23:47. At 21:38 someone entered the master code, which also disarmed the alarm. Only the partners and the office manager are given that code.",
                    new[] { codeUsed }, collectable: true);
                accessLog.pages = new[] { "18 HARLOW STREET  ·  ACCESS LOG  ·  14th\n\n17:58   Card 0112 (N. Hayes)       exit\n18:40   Card 0104 (E. Marlowe)     exit\n19:10   Alarm armed                D. Reyes\n21:38   MASTER CODE                lobby door, alarm disarmed\n23:47   Card 0112 (N. Hayes)       lobby door\n\nNo entries for card 0131 (V. Kim)." };
                accessLog.discovery = Locked("Search filing cabinet", "Drawers full of old case files. You don't know what you're looking for yet. Examine the case file first.", examined: caseFile);
            }

            if (newCups)
            {
                Fill(cups, "Two coffee cups", EvidenceCategory.Object, "Evidence Room · shelf, box 0147-C", "Inspect evidence box",
                    "Two cups bagged from the side table in the victim's office.",
                    "Lab note attached: cup C1 carries the victim's fingerprints. Cup C2 was rinsed and wiped. No prints at all. That takes care.",
                    new[] { visitor }, collectable: true);
                cups.caption = "Bagged from the side table in Reyes's office.";
                cups.pages = new[] { "ITEM 0147-C1  White cup with saucer. Coffee residue.\nITEM 0147-C2  White cup, no saucer. Rinsed." };
                cups.discovery = Locked("Check evidence boxes", "Rows of sealed evidence boxes. Study the crime scene photos first to know which one matters.", examined: photos);
            }

            if (newContract)
            {
                Fill(contract, "Draft sale agreement", EvidenceCategory.Document, "Evidence Room · open folder on the desk", "Read document",
                    "A confidential draft: Reyes planned to sell his majority share of the firm.",
                    "The margin note is in Reyes's handwriting: \"Tell E. after the 20th.\" He was keeping the sale from Marlowe. There's a coffee ring on the last page.",
                    new[] { sale }, collectable: true);
                contract.pages = new[]
                {
                    "HEADS OF TERMS  ·  DRAFT 3  ·  STRICTLY CONFIDENTIAL\n\nBetween: Daniel Reyes, holder of 60% of Reyes & Marlowe Architects Ltd\nand: Calder Holdings plc\n\nCalder Holdings to acquire Mr Reyes's entire holding. Completion planned for the end of the month.",
                    "4. MANAGEMENT\nOn completion, Calder Holdings will appoint the board. The position of the remaining partner (E. Marlowe) will be reviewed within 12 months.\n\nSigned  D. Reyes ..................  (unsigned)\nSigned for Calder Holdings ..................  (unsigned)\n\nIn the margin, handwritten: \"Tell E. after the 20th.\""
                };
                contract.discovery = Locked("Look through paperwork", "Loose paperwork from the firm. Read the case file first so you know what matters.", collected: caseFile);
                contract.connections = new[] { Link(caseFile, "The case file names Marlowe as Reyes's partner. If the sale went through, Calder Holdings would review his position. He stood to lose the firm he co-founded.", motive) };
            }

            if (newPhotos) photos.connections = new[] { Link(cups, "The photos put both cups on Reyes's side table. He made coffee for his visitor: someone he knew and let in.", welcome) };

            foreach (EvidenceDefinition e in new[] { caseFile, photos, cctv, accessLog, cups, contract }) EditorUtility.SetDirty(e);

            // ---- Register with the case (only if the case has no evidence list yet)
            if (case0147.evidence.Count == 0)
            {
                case0147.evidence.AddRange(new[] { caseFile, photos, cctv, accessLog, cups, contract });
                EditorUtility.SetDirty(case0147);
            }
            if (case0147.facts.Count == 0)
            {
                case0147.facts.AddRange(new[] { noForcedEntry, foundAt2350, keyholders, entryAt2140, codeUsed, visitor, sale, knewCode, welcome, motive });
                EditorUtility.SetDirty(case0147);
            }

            EnsureMarloweReactions();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Marlowe's reactions to the new items, and the sale agreement breaking his "finances" deflection.</summary>
        private static void EnsureMarloweReactions()
        {
            var knowledge = AssetDatabase.LoadAssetAtPath<SuspectKnowledge>(CaseDataBuilder.MarloweKnowledgePath);
            if (knowledge == null) return;

            AddReaction(knowledge, "EVD_KeycardLog", "The master code. Daniel, Nora and I had it. And probably every contractor who ever fixed the lifts.", 4);
            AddReaction(knowledge, "EVD_CoffeeCups", "Daniel drank coffee from morning to midnight. Two cups proves nothing.", 3);
            AddReaction(knowledge, "EVD_SaleContract", "Where did you get that? It's a draft. He never had my signature, and he never would have.", 10);

            KnowledgeTopic money = knowledge.FindTopic("TOP_Money");
            if (money != null && (money.contradictedByEvidence == null || money.contradictedByEvidence.Length == 0))
            {
                money.contradictedByEvidence = new[] { "EVD_SaleContract" };
                if (string.IsNullOrEmpty(money.admissionAnswer))
                    money.admissionAnswer = "Yes. Daniel was selling his share to Calder. I found out on the 12th, from their lawyer. Not from him.";
            }
            EditorUtility.SetDirty(knowledge);
        }

        private static void AddReaction(SuspectKnowledge k, string id, string line, int impact)
        {
            if (k.FindReaction(id) != null) return;
            k.evidenceReactions.Add(new EvidenceReaction { evidenceId = id, reaction = line, composureImpact = impact });
        }

        // ------------------------------------------------------------------ helpers

        private static CaseFact Fact(string id, string title, string statement, bool key)
        {
            string path = $"{FactFolder}/{id}.asset";
            var fact = AssetDatabase.LoadAssetAtPath<CaseFact>(path);
            if (fact != null) return fact;
            fact = ScriptableObject.CreateInstance<CaseFact>();
            fact.factId = id;
            fact.title = title;
            fact.description = statement;
            fact.isKeyFact = key;
            AssetDatabase.CreateAsset(fact, path);
            return fact;
        }

        private static EvidenceDefinition Item(string id, out bool created)
        {
            string path = EvidencePath(id);
            var item = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(path);
            created = item == null;
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<EvidenceDefinition>();
            item.evidenceId = id;
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        private static void Fill(EvidenceDefinition e, string name, EvidenceCategory category, string location, string prompt,
            string description, string examination, CaseFact[] facts, bool collectable)
        {
            e.displayName = name;
            e.category = category;
            e.locationFound = location;
            e.interactionPrompt = prompt;
            e.description = description;
            e.examinationNotes = examination;
            e.relatedFacts = facts;
            e.collectable = collectable;
        }

        private static EvidenceConnection Link(EvidenceDefinition other, string explanation, CaseFact fact) =>
            new EvidenceConnection { other = other, explanation = explanation, establishesFact = fact };

        private static DiscoveryConditions Locked(string prompt, string hint, EvidenceDefinition examined = null, EvidenceDefinition collected = null) =>
            new DiscoveryConditions
            {
                requiresExamined = examined != null ? new[] { examined } : Array.Empty<EvidenceDefinition>(),
                requiresCollected = collected != null ? new[] { collected } : Array.Empty<EvidenceDefinition>(),
                requiresFacts = Array.Empty<CaseFact>(),
                lockedPrompt = prompt,
                lockedHint = hint
            };
    }
}
