using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems;
using TLA.Systems.Cases;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Investigation
{
    /// <summary>
    /// CLOSE THE CASE: choose one of the case's conclusions (e.g. who to charge), then confirm.
    /// The decision is final; CaseProgressService picks the ending from the evidence gathered.
    /// Opened from the notebook (CLOSE THE CASE) once the case's conclusion conditions are met.
    /// </summary>
    public sealed class ConclusionScreen : ModalScreen
    {
        [SerializeField] private TMP_Text caseLabel;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private Transform optionContainer;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button backButton;

        private UIList<Button> _options;
        private ConclusionDefinition _selected;
        private bool _armed; // second press confirms

        protected override void Awake()
        {
            base.Awake();
            _options = new UIList<Button>(optionTemplate, optionContainer);
            confirmButton.onClick.AddListener(OnConfirm);
            backButton.onClick.AddListener(Close);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<ConclusionRequestedEvent>(OnRequested);
        }

        protected override void OnDisable()
        {
            EventBus.Unsubscribe<ConclusionRequestedEvent>(OnRequested);
            base.OnDisable();
        }

        private static GameManager Game => Services.TryGet(out GameManager g) ? g : null;

        private void OnRequested(ConclusionRequestedEvent evt)
        {
            GameManager game = Game;
            if (game?.Progress == null || !game.Progress.CanConclude || IsVisible) return;
            if (!OpenModal()) return;
            _selected = null;
            _armed = false;
            Refresh();
        }

        private void Refresh()
        {
            CaseDefinition c = Game.Case.CurrentCase;
            caseLabel.text = c.DisplayName.ToUpperInvariant();
            _options.Clear();
            foreach (ConclusionDefinition conclusion in c.conclusions)
            {
                if (conclusion == null) continue;
                ConclusionDefinition con = conclusion;
                Button b = _options.Add();
                b.GetComponentInChildren<TMP_Text>().text = con == _selected ? $"<b><color=#c8433a>{con.title}</color></b>" : con.title;
                if (b.TryGetComponent(out UIButtonStyle style)) style.SetMarked(con == _selected);
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() =>
                {
                    _selected = con;
                    _armed = false;
                    Refresh();
                });
            }

            if (_selected == null)
            {
                detailTitle.text = "Choose your conclusion";
                detailBody.text = "Review the notebook before you decide: your objectives, the facts you've confirmed and the statements you've exposed. Once you close the case, there's no going back.";
            }
            else
            {
                detailTitle.text = _selected.title;
                detailBody.text = _selected.description;
            }
            confirmButton.interactable = _selected != null;
            confirmLabel.text = _armed ? "CONFIRM: THIS IS FINAL" : "CLOSE THE CASE";
        }

        private void OnConfirm()
        {
            if (_selected == null) return;
            if (!_armed)
            {
                _armed = true; // ask once more: the decision can't be undone
                Refresh();
                return;
            }
            GameManager game = Game;
            ConclusionDefinition chosen = _selected;
            Close();
            game?.Progress?.Conclude(chosen.conclusionId); // EndingScreen takes it from here
        }
    }
}
