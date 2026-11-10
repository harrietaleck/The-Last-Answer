using System.Collections.Generic;
using System.Text;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using TLA.Systems;
using TLA.Systems.Cases;
using TLA.Systems.Investigation;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Investigation
{
    /// <summary>
    /// CASE NOTEBOOK (press J, or Notebook in the pause menu). Pages:
    ///   CASE          overview, crime, victim, suspects, witnesses, status and decisions
    ///   OBJECTIVES    active and completed objectives (later ones appear as the case unfolds)
    ///   TIMELINE      the moments of the case, revealed as their facts are confirmed
    ///   STATEMENTS    what suspects claimed, their words, status. Select one and COMPARE it with a
    ///                 piece of evidence: the ContradictionChecker answers (contradiction / not proven
    ///                 yet / no contradiction). Found contradictions can then be put to the suspect.
    ///   FACTS         confirmed facts with source, reliability, related evidence and statements
    ///   OBSERVATIONS  the player's own notes (type and ADD) plus automatic case notes
    /// Left column: investigation progress. Everything is read from the investigation services;
    /// this screen never decides anything itself.
    /// </summary>
    public sealed class NotebookScreen : ModalScreen
    {
        private enum Page { Case = 0, Objectives = 1, Timeline = 2, Statements = 3, Facts = 4, Observations = 5 }

        [Header("Left")]
        [SerializeField] private TMP_Text caseLabel;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private UIOptionStepper pageStepper;
        [SerializeField] private Button itemTemplate;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private TMP_Text emptyLabel;

        [Header("Detail")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailBody;

        [Header("Compare (statements)")]
        [SerializeField] private GameObject compareBlock;
        [SerializeField] private Button compareTemplate;
        [SerializeField] private Transform compareContainer;
        [SerializeField] private TMP_Text compareResult;

        [Header("Observations")]
        [SerializeField] private GameObject observationRow;
        [SerializeField] private TMP_InputField observationInput;
        [SerializeField] private Button addObservationButton;

        [SerializeField] private Button backButton;
        [SerializeField] private Button closeCaseButton;

        private static readonly string[] PageNames = { "CASE", "OBJECTIVES", "TIMELINE", "STATEMENTS", "FACTS", "OBSERVATIONS" };
        private const string Red = "#c8433a", Amber = "#d39a3a", Grey = "#7a7d86", Light = "#d8d8dc";

        private UIList<Button> _items;
        private UIList<Button> _compareButtons;
        private Page _page;
        private SuspectStatement _selectedStatement;
        private CaseFact _selectedFact;
        private string _selectedNote;
        private ContradictionResult _lastResult;
        private string _selectedKey; // Case / Objectives / Timeline pages

        protected override void Awake()
        {
            base.Awake();
            _items = new UIList<Button>(itemTemplate, itemContainer);
            _compareButtons = new UIList<Button>(compareTemplate, compareContainer);
            backButton.onClick.AddListener(Close);
            if (closeCaseButton != null) closeCaseButton.onClick.AddListener(() => EventBus.Publish(new ConclusionRequestedEvent()));
            addObservationButton.onClick.AddListener(AddObservation);
            observationInput.onSubmit.AddListener(_ => AddObservation());
            observationInput.onSelect.AddListener(_ => EventBus.Publish(new TextInputFocusChangedEvent(true)));
            observationInput.onDeselect.AddListener(_ => EventBus.Publish(new TextInputFocusChangedEvent(false)));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<NotebookRequestedEvent>(OnRequested);
            EventBus.Subscribe<InvestigationChangedEvent>(OnChanged);
            EventBus.Subscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
            EventBus.Subscribe<CaseProgressChangedEvent>(OnProgressChanged);
        }

        protected override void OnDisable()
        {
            EventBus.Unsubscribe<NotebookRequestedEvent>(OnRequested);
            EventBus.Unsubscribe<InvestigationChangedEvent>(OnChanged);
            EventBus.Unsubscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
            EventBus.Unsubscribe<CaseProgressChangedEvent>(OnProgressChanged);
            base.OnDisable();
        }

        private static GameManager Game => Services.TryGet(out GameManager g) ? g : null;

        private void OnRequested(NotebookRequestedEvent evt)
        {
            if (IsVisible)
            {
                if (UIOverlay.OpenCount <= 1) Close(); // J again closes it
                return;
            }
            GameManager game = Game;
            if (game == null || game.Investigation == null || !game.Case.HasActiveCase) return;
            if (!OpenModal()) return;

            _lastResult = null;
            pageStepper.Bind("PAGE", PageNames, (int)_page, i =>
            {
                _page = (Page)i;
                _lastResult = null;
                _selectedKey = null;
                Refresh();
            });
            Refresh();
        }

        private void OnChanged(InvestigationChangedEvent evt)
        {
            if (IsVisible) Refresh();
        }

        private void OnEvidenceChanged(EvidenceStateChangedEvent evt)
        {
            if (IsVisible) Refresh();
        }

        private void OnProgressChanged(CaseProgressChangedEvent evt)
        {
            if (IsVisible) Refresh();
        }

        protected override void OnHidden()
        {
            EventBus.Publish(new TextInputFocusChangedEvent(false));
        }

        // ------------------------------------------------------------------ refresh

        private void Refresh()
        {
            GameManager game = Game;
            if (game == null || game.Investigation == null) return;

            caseLabel.text = game.Case.CurrentCase != null ? game.Case.CurrentCase.DisplayName.ToUpperInvariant() : "";
            progressText.text = ProgressText(game.Investigation.GetProgress());

            _items.Clear();
            compareBlock.SetActive(_page == Page.Statements);
            observationRow.SetActive(_page == Page.Observations);

            if (closeCaseButton != null) closeCaseButton.gameObject.SetActive(game.Progress != null && game.Progress.CanConclude);

            switch (_page)
            {
                case Page.Case: BuildCase(game); break;
                case Page.Objectives: BuildObjectives(game); break;
                case Page.Timeline: BuildTimeline(game); break;
                case Page.Statements: BuildStatements(game); break;
                case Page.Facts: BuildFacts(game); break;
                default: BuildObservations(game); break;
            }
        }

        private static string ProgressText(InvestigationProgress p)
        {
            var sb = new StringBuilder();
            sb.Append($"<size=150%><color={Light}>{p.Percent}%</color></size>  <color={Grey}>INVESTIGATED</color>\n\n");
            sb.Append($"Evidence collected<pos=52%><color={Light}>{p.EvidenceCollected} / {p.EvidenceTotal}</color>\n");
            sb.Append($"Facts confirmed<pos=52%><color={Light}>{p.FactsConfirmed} / {p.FactsTotal}</color>\n");
            sb.Append($"Statements heard<pos=52%><color={Light}>{p.StatementsRecorded}</color>\n");
            sb.Append($"Contradictions<pos=52%><color={Light}>{p.ContradictionsExposed} / {p.ContradictionsTotal} exposed</color>");
            if (p.ContradictionsFound > p.ContradictionsExposed) sb.Append($"  <color={Amber}>{p.ContradictionsFound - p.ContradictionsExposed} to confront</color>");
            if (p.FailedChallenges > 0) sb.Append($"\nFailed challenges<pos=52%><color={Red}>{p.FailedChallenges}</color>");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ case file

        private void BuildCase(GameManager game)
        {
            CaseDefinition c = game.Case.CurrentCase;
            var entries = new List<KeyValuePair<string, string>> // (key, list label)
            {
                new KeyValuePair<string, string>("overview", "Overview"),
                new KeyValuePair<string, string>("crime", "The crime"),
                new KeyValuePair<string, string>("victim", "Victim: " + c.victim.name)
            };
            foreach (var s in c.suspects) if (s != null) entries.Add(new KeyValuePair<string, string>("person:" + s.suspectId, "Suspect: " + s.displayName));
            foreach (var w in c.witnesses) if (w != null) entries.Add(new KeyValuePair<string, string>("witness:" + w.suspectId, "Witness: " + w.displayName));
            if (game.Progress != null && game.Progress.Decisions.Count > 0) entries.Add(new KeyValuePair<string, string>("decisions", "Decisions"));

            if (_selectedKey == null || !entries.Exists(e => e.Key == _selectedKey)) _selectedKey = "overview";
            foreach (var entry in entries)
            {
                var e = entry;
                AddItem(e.Key == _selectedKey ? $"<b>{e.Value}</b>" : e.Value, () =>
                {
                    _selectedKey = e.Key;
                    Refresh();
                }, e.Key == _selectedKey);
            }
            SetEmpty(false, "");
            detailRoot.SetActive(true);

            var body = new StringBuilder();
            if (_selectedKey == "overview")
            {
                detailLabel.text = c.DisplayName.ToUpperInvariant();
                detailTitle.text = c.title;
                body.Append(c.summary).Append("\n\n");
                if (game.Progress != null)
                {
                    CaseProgressSummary p = game.Progress.GetSummary();
                    body.Append($"<color={Grey}>STATUS</color><pos=24%>{StatusLabel(p.Status)}\n");
                    body.Append($"<color={Grey}>OBJECTIVES</color><pos=24%>{p.ObjectivesCompleted} / {p.ObjectivesRequired}   <color={Grey}>(optional {p.OptionalCompleted} / {p.OptionalTotal})</color>\n");
                    body.Append($"<color={Grey}>EVIDENCE</color><pos=24%>{p.EvidenceDiscovered} / {p.EvidenceTotal}\n");
                    body.Append($"<color={Grey}>QUESTIONED</color><pos=24%>{p.SuspectsQuestioned} / {p.SuspectsTotal} suspects\n");
                    body.Append($"<color={Grey}>CONTRADICTIONS</color><pos=24%>{p.ContradictionsExposed} exposed\n\n");
                    body.Append(game.Progress.CanConclude
                        ? $"<color={Amber}>You can close the case when you're ready (CLOSE THE CASE). The decision is final.</color>\n\n"
                        : "");
                }
                body.Append($"<color={Grey}>BRIEFING</color>\n").Append(c.briefing);
            }
            else if (_selectedKey == "crime")
            {
                detailLabel.text = "THE CRIME";
                detailTitle.text = c.crime.crimeType;
                Line(body, "DATE", c.crime.date);
                Line(body, "TIME OF DEATH", c.crime.timeOfDeath);
                Line(body, "LOCATION", c.crime.location);
                Line(body, "CAUSE OF DEATH", c.crime.causeOfDeath);
                Line(body, "WEAPON", c.crime.weapon);
                Line(body, "DISCOVERED BY", c.crime.discoveredBy);
                if (!string.IsNullOrEmpty(c.crime.details)) body.Append('\n').Append(c.crime.details);
            }
            else if (_selectedKey == "victim")
            {
                detailLabel.text = "VICTIM";
                detailTitle.text = c.victim.name;
                body.Append($"{c.victim.age}  ·  {c.victim.occupation}\n\n{c.victim.background}");
            }
            else if (_selectedKey == "decisions")
            {
                detailLabel.text = "IMPORTANT DECISIONS";
                detailTitle.text = "Decisions";
                foreach (DecisionRecord d in game.Progress.Decisions) body.Append($"• {d.label}\n");
            }
            else
            {
                bool witness = _selectedKey.StartsWith("witness:");
                string id = _selectedKey.Substring(_selectedKey.IndexOf(':') + 1);
                SuspectProfile person = (witness ? c.witnesses : c.suspects).Find(x => x != null && x.suspectId == id);
                detailLabel.text = witness ? "WITNESS" : $"SUSPECT  ·  {person?.status.ToUpperInvariant()}";
                detailTitle.text = person?.displayName;
                if (person != null)
                {
                    body.Append($"{person.age}  ·  {person.occupation}\n<color={Grey}>{person.relationToCase}</color>\n\n{person.summary}\n\n");
                    if (game.Progress != null && !witness)
                        body.Append(game.Progress.WasQuestioned(person) ? $"<color={Red}>•</color> Questioned" : $"<color={Grey}>• Not questioned</color>");
                    int heard = game.Investigation.GetRecorded(person.suspectId).Count;
                    if (heard > 0) body.Append($"\n<color={Red}>•</color> {heard} statement{(heard == 1 ? "" : "s")} recorded (see STATEMENTS)");
                }
            }
            detailBody.text = body.ToString().TrimEnd();
        }

        private static void Line(StringBuilder sb, string label, string value)
        {
            if (!string.IsNullOrEmpty(value)) sb.Append($"<color={Grey}>{label}</color>\n{value}\n\n");
        }

        private static string StatusLabel(InvestigationStatus s)
        {
            switch (s)
            {
                case InvestigationStatus.ReadyToConclude: return $"<color={Amber}>READY TO CLOSE</color>";
                case InvestigationStatus.Concluded: return $"<color={Red}>CLOSED</color>";
                case InvestigationStatus.Investigating: return "INVESTIGATING";
                default: return "NOT STARTED";
            }
        }

        // ------------------------------------------------------------------ objectives

        private void BuildObjectives(GameManager game)
        {
            CaseDefinition c = game.Case.CurrentCase;
            var shown = new List<ObjectiveDefinition>();
            int hidden = 0;
            foreach (ObjectiveDefinition o in c.objectives)
            {
                if (o == null) continue;
                if (game.Progress.IsObjectiveCompleted(o.objectiveId) || game.Progress.IsObjectiveActive(o)) shown.Add(o);
                else hidden++;
            }
            ObjectiveDefinition current = game.Progress.CurrentObjective;
            if (_selectedKey == null || !shown.Exists(o => o.objectiveId == _selectedKey)) _selectedKey = current?.objectiveId ?? (shown.Count > 0 ? shown[0].objectiveId : null);

            foreach (ObjectiveDefinition objective in shown)
            {
                ObjectiveDefinition o = objective;
                bool done = game.Progress.IsObjectiveCompleted(o.objectiveId);
                string tag = done ? $"<color={Grey}>DONE</color>" : o == current ? $"<color={Red}>CURRENT</color>" : o.optional ? $"<color={Amber}>OPTIONAL</color>" : $"<color={Light}>OPEN</color>";
                string title = done ? $"<color={Grey}><s>{o.title}</s></color>" : o.title;
                if (o.objectiveId == _selectedKey) title = $"<b>{title}</b>";
                AddItem($"{title}\n<size=70%>{tag}</size>", () =>
                {
                    _selectedKey = o.objectiveId;
                    Refresh();
                }, o.objectiveId == _selectedKey);
            }
            if (hidden > 0)
            {
                Button b = _items.Add();
                b.GetComponentInChildren<TMP_Text>().text = $"<color={Grey}><i>{hidden} more objective{(hidden == 1 ? "" : "s")} will appear as the case unfolds</i></color>";
                b.onClick.RemoveAllListeners();
            }
            SetEmpty(shown.Count == 0 && hidden == 0, "This case has no objectives.");

            ObjectiveDefinition sel = c.FindObjective(_selectedKey);
            detailRoot.SetActive(sel != null);
            if (sel == null) return;
            bool selDone = game.Progress.IsObjectiveCompleted(sel.objectiveId);
            detailLabel.text = selDone ? "OBJECTIVE  ·  COMPLETE" : sel.optional ? "OBJECTIVE  ·  OPTIONAL" : "OBJECTIVE";
            detailTitle.text = sel.title;
            detailBody.text = sel.description + (selDone ? $"\n\n<color={Red}>•</color> Completed." : "");
        }

        // ------------------------------------------------------------------ timeline

        private void BuildTimeline(GameManager game)
        {
            CaseDefinition c = game.Case.CurrentCase;
            var known = new List<TimelineEntry>();
            int unknown = 0;
            foreach (TimelineEntry t in c.timeline)
            {
                if (t == null) continue;
                if (t.revealedBy == null || game.Evidence.IsFactEstablished(t.revealedBy)) known.Add(t); else unknown++;
            }
            string Key(TimelineEntry t) => t.time + "|" + t.description;
            if (_selectedKey == null || !known.Exists(t => Key(t) == _selectedKey)) _selectedKey = known.Count > 0 ? Key(known[0]) : null;

            foreach (TimelineEntry entry in known)
            {
                TimelineEntry t = entry;
                string time = t.isKey ? $"<color={Red}>{t.time}</color>" : $"<color={Light}>{t.time}</color>";
                string text = Key(t) == _selectedKey ? $"<b>{t.description}</b>" : t.description;
                AddItem($"{time}   <size=90%>{text}</size>", () =>
                {
                    _selectedKey = Key(t);
                    Refresh();
                }, Key(t) == _selectedKey);
            }
            if (unknown > 0)
            {
                Button b = _items.Add();
                b.GetComponentInChildren<TMP_Text>().text = $"<color={Grey}><i>{unknown} moment{(unknown == 1 ? "" : "s")} still unknown</i></color>";
                b.onClick.RemoveAllListeners();
            }
            SetEmpty(known.Count == 0 && unknown == 0, "This case has no timeline.");

            TimelineEntry sel = known.Find(t => Key(t) == _selectedKey);
            detailRoot.SetActive(sel != null);
            if (sel == null) return;
            detailLabel.text = sel.isKey ? "TIMELINE  ·  KEY MOMENT" : "TIMELINE";
            detailTitle.text = sel.time;
            detailBody.text = sel.description + "\n\n" + (sel.revealedBy == null
                ? $"<color={Grey}>Known from the start.</color>"
                : $"<color={Grey}>From:</color> {sel.revealedBy.title} <color={Grey}>({sel.revealedBy.source})</color>");
        }

        // ------------------------------------------------------------------ statements

        private void BuildStatements(GameManager game)
        {
            List<SuspectStatement> recorded = game.Investigation.GetRecorded();
            if (_selectedStatement == null || !recorded.Contains(_selectedStatement))
                _selectedStatement = recorded.Count > 0 ? recorded[0] : null;

            foreach (SuspectStatement s in recorded)
            {
                StatementState st = game.Investigation.GetState(s);
                string who = s.suspect != null ? s.suspect.displayName : "Unknown";
                string name = s == _selectedStatement ? $"<b>{who}</b>" : who;
                AddItem($"{name}  {StatusTag(st.status)}\n<size=80%><color=#a9abb3>\"{s.claim}\"</color></size>", () =>
                {
                    _selectedStatement = s;
                    _lastResult = null;
                    Refresh();
                }, s == _selectedStatement);
            }

            SetEmpty(recorded.Count == 0, "No statements yet. Question the suspects; what they claim is recorded here.");
            ShowStatementDetail(game, _selectedStatement);
        }

        private static string StatusTag(StatementStatus status)
        {
            switch (status)
            {
                case StatementStatus.Exposed: return $"<size=70%><color={Red}>EXPOSED</color></size>";
                case StatementStatus.ContradictionFound: return $"<size=70%><color={Amber}>CONTRADICTION FOUND</color></size>";
                default: return $"<size=70%><color={Grey}>UNRESOLVED</color></size>";
            }
        }

        private void ShowStatementDetail(GameManager game, SuspectStatement s)
        {
            detailRoot.SetActive(s != null);
            _compareButtons.Clear();
            if (s == null)
            {
                compareBlock.SetActive(false);
                return;
            }

            StatementState st = game.Investigation.GetState(s);
            detailLabel.text = $"STATEMENT  ·  {(s.suspect != null ? s.suspect.displayName.ToUpperInvariant() : "")}";
            detailTitle.text = $"\"{s.claim}\"";

            var body = new StringBuilder();
            body.Append($"<color={Grey}>SOURCE</color><pos=24%>{s.source}\n");
            body.Append($"<color={Grey}>STATUS</color><pos=24%>{StatusTag(st.status)}\n\n");
            if (st.quotes.Count > 0)
            {
                body.Append($"<color={Grey}>WHAT THEY SAID</color>\n");
                foreach (string q in st.quotes) body.Append($"<i>\"{q}\"</i>\n");
                body.Append('\n');
            }
            if (st.status != StatementStatus.Unverified && !string.IsNullOrEmpty(st.contradictedWith))
            {
                EvidenceDefinition e = game.Evidence.Find(st.contradictedWith);
                ContradictionRule rule = ContradictionChecker.FindRule(s, e);
                body.Append($"<color={Red}>CONTRADICTED BY</color>   {e?.displayName}\n{rule?.explanation}\n");
                if (st.status == StatementStatus.ContradictionFound)
                    body.Append($"<color={Amber}>Confront {(s.suspect != null ? s.suspect.ShortName : "the suspect")} with it in an interview (CHALLENGE STATEMENT).</color>\n");
                body.Append('\n');
            }

            var facts = new StringBuilder();
            foreach (CaseFact f in game.Case.CurrentCase.facts)
                if (f != null && f.relatedStatements != null && System.Array.IndexOf(f.relatedStatements, s) >= 0 && game.Evidence.IsFactEstablished(f))
                    facts.Append($"<color={Red}>•</color> {f.title}\n");
            if (facts.Length > 0) body.Append($"<color={Grey}>RELATED FACTS</color>\n").Append(facts);

            detailBody.text = body.ToString().TrimEnd();

            // Compare with any collected evidence.
            bool canCompare = st.status != StatementStatus.Exposed;
            compareBlock.SetActive(canCompare);
            if (!canCompare) return;
            foreach (EvidenceDefinition e in game.Evidence.GetCollected())
            {
                Button b = _compareButtons.Add();
                b.GetComponentInChildren<TMP_Text>().text = e.displayName;
                if (b.TryGetComponent(out UIButtonStyle compareStyle))
                    compareStyle.SetMarked(_lastResult != null && _lastResult.Statement == s && _lastResult.Evidence == e);
                b.onClick.RemoveAllListeners();
                EvidenceDefinition evidence = e;
                b.onClick.AddListener(() =>
                {
                    _lastResult = game.Investigation.Compare(s, evidence);
                    Refresh();
                });
            }
            compareResult.text = _lastResult != null && _lastResult.Statement == s ? ResultText(_lastResult) : "<color=#7a7d86>Pick a piece of evidence to compare it with this statement.</color>";
        }

        private static string ResultText(ContradictionResult r)
        {
            string color = r.IsValid ? Red : r.Outcome == ContradictionOutcome.Unproven ? Amber : Grey;
            return $"<color={color}><b>{r.Title}</b></color>  {r.Explanation}";
        }

        // ------------------------------------------------------------------ facts

        private void BuildFacts(GameManager game)
        {
            var confirmed = new List<CaseFact>();
            int unconfirmed = 0;
            foreach (CaseFact f in game.Case.CurrentCase.facts)
            {
                if (f == null) continue;
                if (game.Evidence.IsFactEstablished(f)) confirmed.Add(f); else unconfirmed++;
            }
            if (_selectedFact == null || !confirmed.Contains(_selectedFact)) _selectedFact = confirmed.Count > 0 ? confirmed[confirmed.Count - 1] : null;

            for (int i = confirmed.Count - 1; i >= 0; i--)
            {
                CaseFact f = confirmed[i];
                string title = f == _selectedFact ? $"<b>{f.title}</b>" : f.title;
                string key = f.isKeyFact ? $"<color={Red}>KEY</color>  " : "";
                AddItem($"{title}\n<size=70%>{key}<color={Grey}>RELIABILITY {f.ReliabilityLabel}</color></size>", () =>
                {
                    _selectedFact = f;
                    Refresh();
                }, f == _selectedFact);
            }
            if (unconfirmed > 0)
            {
                Button b = _items.Add();
                b.GetComponentInChildren<TMP_Text>().text = $"<color={Grey}><i>{unconfirmed} more fact{(unconfirmed == 1 ? "" : "s")} not confirmed yet</i></color>";
                b.onClick.RemoveAllListeners();
            }

            SetEmpty(confirmed.Count == 0 && unconfirmed == 0, "This case has no facts to confirm.");
            ShowFactDetail(game, _selectedFact);
        }

        private void ShowFactDetail(GameManager game, CaseFact f)
        {
            detailRoot.SetActive(f != null);
            if (f == null) return;

            detailLabel.text = f.isKeyFact ? "CONFIRMED FACT  ·  KEY" : "CONFIRMED FACT";
            detailTitle.text = f.title;
            var body = new StringBuilder();
            body.Append(f.description).Append("\n\n");
            body.Append($"<color={Grey}>ID</color><pos=24%>{f.factId}\n");
            body.Append($"<color={Grey}>SOURCE</color><pos=24%>{(string.IsNullOrEmpty(f.source) ? "-" : f.source)}\n");
            body.Append($"<color={Grey}>RELIABILITY</color><pos=24%>{f.ReliabilityLabel}\n\n");

            var evidence = new StringBuilder();
            if (f.relatedEvidence != null)
                foreach (EvidenceDefinition e in f.relatedEvidence)
                    if (e != null) evidence.Append(game.Evidence.IsCollected(e) ? $"• {e.displayName}\n" : $"<color={Grey}>• (evidence not collected yet)</color>\n");
            if (evidence.Length > 0) body.Append($"<color={Grey}>RELATED EVIDENCE</color>\n").Append(evidence).Append('\n');

            var statements = new StringBuilder();
            if (f.relatedStatements != null)
                foreach (SuspectStatement s in f.relatedStatements)
                    if (s != null && game.Investigation.IsStatementRecorded(s))
                        statements.Append($"• {(s.suspect != null ? s.suspect.ShortName : "")}: \"{s.claim}\"  {StatusTag(game.Investigation.GetState(s).status)}\n");
            if (statements.Length > 0) body.Append($"<color={Grey}>RELATED STATEMENTS</color>\n").Append(statements);

            detailBody.text = body.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------ observations

        private void BuildObservations(GameManager game)
        {
            var entries = new List<KeyValuePair<string, string>>(); // (source, text), newest first
            IReadOnlyList<ObservationRecord> mine = game.Investigation.Observations;
            for (int i = mine.Count - 1; i >= 0; i--) entries.Add(new KeyValuePair<string, string>("MY OBSERVATION", mine[i].text));
            for (int i = game.Case.Notes.Count - 1; i >= 0; i--)
                entries.Add(new KeyValuePair<string, string>(string.IsNullOrEmpty(game.Case.Notes[i].source) ? "CASE NOTE" : game.Case.Notes[i].source.ToUpperInvariant(), game.Case.Notes[i].text));

            bool found = false;
            foreach (var e in entries) if (e.Value == _selectedNote) found = true;
            if (!found) _selectedNote = entries.Count > 0 ? entries[0].Value : null;

            string selectedSource = "";
            foreach (var entry in entries)
            {
                var e = entry;
                bool selected = e.Value == _selectedNote;
                if (selected) selectedSource = e.Key;
                string color = e.Key == "MY OBSERVATION" ? Amber : Grey;
                string text = e.Value.Length > 90 ? e.Value.Substring(0, 87) + "..." : e.Value;
                AddItem($"<size=70%><color={color}>{e.Key}</color></size>\n{(selected ? "<b>" + text + "</b>" : text)}", () =>
                {
                    _selectedNote = e.Value;
                    Refresh();
                }, selected);
            }

            SetEmpty(entries.Count == 0, "Nothing recorded yet. Type an observation below and press ADD.");
            detailRoot.SetActive(_selectedNote != null);
            detailLabel.text = selectedSource;
            detailTitle.text = selectedSource == "MY OBSERVATION" ? "Observation" : "Note";
            detailBody.text = _selectedNote ?? "";
        }

        private void AddObservation()
        {
            GameManager game = Game;
            if (game?.Investigation == null || string.IsNullOrWhiteSpace(observationInput.text)) return;
            ObservationRecord added = game.Investigation.AddObservation(observationInput.text);
            observationInput.text = "";
            if (added != null) _selectedNote = added.text;
            Refresh();
        }

        // ------------------------------------------------------------------ helpers

        private void AddItem(string text, System.Action onClick, bool marked = false)
        {
            Button b = _items.Add();
            b.GetComponentInChildren<TMP_Text>().text = text;
            if (b.TryGetComponent(out UIButtonStyle style)) style.SetMarked(marked);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => onClick());
        }

        private void SetEmpty(bool empty, string message)
        {
            emptyLabel.gameObject.SetActive(empty);
            emptyLabel.text = message;
        }
    }
}
