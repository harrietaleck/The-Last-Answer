using System;
using System.Text;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// CASE FILES: a list of every case (from the CaseCatalog) on the left and the selected
    /// case's file on the right (number, title, location, status, briefing, persons of interest).
    /// "Open Investigation" starts that case. Locked cases show as "Coming soon".
    /// </summary>
    public sealed class CaseFilesScreen : UIScreen
    {
        [SerializeField] private Button caseButtonTemplate;
        [SerializeField] private Transform caseListContainer;

        [SerializeField] private TMP_Text caseNumber;
        [SerializeField] private TMP_Text caseTitle;
        [SerializeField] private TMP_Text caseMeta;
        [SerializeField] private TMP_Text caseBriefing;
        [SerializeField] private TMP_Text caseSuspects;
        [SerializeField] private Button openButton;
        [SerializeField] private Button backButton;

        private UIList<Button> _list;
        private CaseDefinition _selected;

        /// <summary>Raised when the player leaves this screen (the main menu shows itself again).</summary>
        public event Action Closed;

        protected override void Awake()
        {
            base.Awake();
            _list = new UIList<Button>(caseButtonTemplate, caseListContainer);
            openButton.onClick.AddListener(OnOpen);
            backButton.onClick.AddListener(OnBack);
        }

        private void OnDestroy()
        {
            openButton.onClick.RemoveListener(OnOpen);
            backButton.onClick.RemoveListener(OnBack);
        }

        protected override void OnShown()
        {
            BuildList();
        }

        private void BuildList()
        {
            _list.Clear();
            CaseCatalog catalog = Services.TryGet(out GameManager game) ? game.Config.Game.CaseCatalog : null;
            if (catalog == null) return;

            CaseDefinition first = null;
            foreach (CaseDefinition c in catalog.cases)
            {
                if (c == null) continue;
                Button row = _list.Add();
                row.GetComponentInChildren<TMP_Text>().text = c.isAvailable
                    ? $"CASE {c.caseNumber}   {c.title.ToUpperInvariant()}"
                    : $"CASE {c.caseNumber}   CLASSIFIED";
                CaseDefinition captured = c;
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => Select(captured));
                first ??= c;
            }

            Select(first);
        }

        private void Select(CaseDefinition c)
        {
            _selected = c;
            if (c == null) return;

            caseNumber.text = $"CASE FILE  ·  {c.caseNumber}";
            caseTitle.text = c.isAvailable ? c.title : "Classified";
            caseMeta.text = c.isAvailable ? $"{c.location.ToUpperInvariant()}     STATUS: OPEN" : "STATUS: COMING SOON";
            caseBriefing.text = c.isAvailable ? c.briefing : "This file has not been released yet.";

            var sb = new StringBuilder();
            if (c.isAvailable && c.suspects.Count > 0)
            {
                sb.Append("PERSONS OF INTEREST\n");
                foreach (SuspectProfile s in c.suspects)
                    if (s != null) sb.Append($"<color=#EEEEEF>{s.displayName}</color>  ·  {s.relationToCase}\n");
            }
            caseSuspects.text = sb.ToString();
            openButton.interactable = c.isAvailable;
        }

        private void OnOpen()
        {
            if (_selected != null && _selected.isAvailable && Services.TryGet(out GameManager game))
                game.StartNewGame(_selected);
        }

        private void OnBack()
        {
            Hide();
            Closed?.Invoke();
        }
    }
}
