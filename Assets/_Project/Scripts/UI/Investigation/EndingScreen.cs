using System.Text;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems;
using TLA.Systems.Cases;
using TLA.Systems.Flow;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Investigation
{
    /// <summary>
    /// ENDING: shown when the case is closed. Outcome (SOLVED / PARTIALLY SOLVED / FAILED / UNSOLVED),
    /// the ending's title and epilogue, the player's conclusion and a summary of the investigation.
    /// Moves the game into the Ending state; RETURN TO MAIN MENU leaves it.
    /// </summary>
    public sealed class EndingScreen : UIScreen
    {
        [SerializeField] private TMP_Text outcomeLabel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text conclusionLine;
        [SerializeField] private TMP_Text epilogue;
        [SerializeField] private TMP_Text stats;
        [SerializeField] private Button mainMenuButton;

        protected override void Awake()
        {
            base.Awake();
            mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CaseConcludedEvent>(OnConcluded);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CaseConcludedEvent>(OnConcluded);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        /// <summary>A save of a closed case shows its ending again once its scene has loaded.</summary>
        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            if (IsVisible || !Services.TryGet(out GameManager game) || game.Progress == null) return;
            if (evt.SceneName == game.Config.Game.Scenes.MainMenuScene) return;
            CaseConcludedEvent? stored = game.Progress.StoredConclusion();
            if (stored.HasValue) StartCoroutine(ShowNextFrame(stored.Value));
        }

        private System.Collections.IEnumerator ShowNextFrame(CaseConcludedEvent evt)
        {
            yield return null; // after the scene has applied its own game state
            if (!IsVisible) OnConcluded(evt);
        }

        private void OnConcluded(CaseConcludedEvent evt)
        {
            // Into the Ending state (through Deciding, as the state machine requires).
            if (Services.TryGet(out GameStateManager state))
            {
                if (state.IsPaused) state.Resume();
                if (state.Current != GameState.Deciding) state.TryChangeState(GameState.Deciding);
                state.TryChangeState(GameState.Ending);
            }

            EndingDefinition e = evt.Ending;
            outcomeLabel.text = e == null ? "CASE CLOSED" : OutcomeText(e.outcome);
            outcomeLabel.color = e == null ? Color.white : OutcomeColor(e.outcome);
            title.text = e != null ? e.title : evt.Conclusion.title;
            conclusionLine.text = $"Your conclusion: {evt.Conclusion.title}";
            epilogue.text = e != null ? e.epilogue : "";
            stats.text = Stats(evt.Summary);
            Show();
            StopAllCoroutines();
            StartCoroutine(Reveal());
        }

        /// <summary>A slow, quiet reveal: outcome, then title, then the epilogue, then the summary and button.</summary>
        private System.Collections.IEnumerator Reveal()
        {
            var steps = new Graphic[][]
            {
                new Graphic[] { outcomeLabel },
                new Graphic[] { title },
                new Graphic[] { conclusionLine, epilogue },
                new Graphic[] { stats, mainMenuButton != null ? mainMenuButton.targetGraphic : null, mainMenuButton != null ? mainMenuButton.GetComponentInChildren<TMP_Text>() : null }
            };
            // Everything else in the column (the red rule, the "INVESTIGATION" label…) joins a step too.
            var known = new System.Collections.Generic.HashSet<Graphic>();
            foreach (Graphic[] step in steps) foreach (Graphic g in step) if (g != null) known.Add(g);
            var withTitle = new System.Collections.Generic.List<Graphic>(steps[1]);
            var withStats = new System.Collections.Generic.List<Graphic>(steps[3]);
            foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
            {
                if (known.Contains(g) || g.name == "Scrim" || g.transform == transform) continue;
                if (g.name == "Rule") withTitle.Add(g); else withStats.Add(g);
            }
            steps[1] = withTitle.ToArray();
            steps[3] = withStats.ToArray();

            foreach (Graphic[] step in steps) foreach (Graphic g in step) if (g != null) g.canvasRenderer.SetAlpha(0f);
            mainMenuButton.interactable = false;

            yield return new WaitForSecondsRealtime(0.6f);
            float[] delays = { 0.9f, 1.1f, 1.6f, 0f };
            for (int i = 0; i < steps.Length; i++)
            {
                foreach (Graphic g in steps[i]) if (g != null) g.CrossFadeAlpha(1f, 1.2f, true);
                if (delays[i] > 0f) yield return new WaitForSecondsRealtime(delays[i]);
            }
            mainMenuButton.interactable = true;
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (IsVisible && evt.Current == GameState.MainMenu) Hide();
        }

        private void OnMainMenu()
        {
            if (Services.TryGet(out GameManager game)) game.GoToMainMenu();
        }

        private static string Stats(CaseProgressSummary s)
        {
            var sb = new StringBuilder();
            sb.Append($"Objectives<pos=36%>{s.ObjectivesCompleted} / {s.ObjectivesRequired}   <color=#7a7d86>optional {s.OptionalCompleted} / {s.OptionalTotal}</color>\n");
            sb.Append($"Evidence collected<pos=36%>{s.EvidenceDiscovered} / {s.EvidenceTotal}\n");
            sb.Append($"Facts confirmed<pos=36%>{s.FactsConfirmed} / {s.FactsTotal}\n");
            sb.Append($"Suspects questioned<pos=36%>{s.SuspectsQuestioned} / {s.SuspectsTotal}\n");
            sb.Append($"Contradictions exposed<pos=36%>{s.ContradictionsExposed} / {s.ContradictionsTotal}\n");
            sb.Append($"Failed challenges<pos=36%>{s.FailedChallenges}");
            return sb.ToString();
        }

        private static string OutcomeText(EndingOutcome o)
        {
            switch (o)
            {
                case EndingOutcome.Solved: return "CASE SOLVED";
                case EndingOutcome.PartiallySolved: return "PARTIALLY SOLVED";
                case EndingOutcome.Failed: return "CASE FAILED";
                default: return "UNSOLVED";
            }
        }

        private static Color OutcomeColor(EndingOutcome o)
        {
            switch (o)
            {
                case EndingOutcome.Solved: return new Color32(0xC8, 0x43, 0x3A, 0xFF);
                case EndingOutcome.PartiallySolved: return new Color32(0xD3, 0x9A, 0x3A, 0xFF);
                default: return new Color32(0x9A, 0x9F, 0xA6, 0xFF);
            }
        }
    }
}
