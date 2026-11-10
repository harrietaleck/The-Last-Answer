using System.Collections.Generic;
using System.Text;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Systems;
using TLA.Systems.Investigation;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Evidence
{
    /// <summary>
    /// EVIDENCE INVENTORY (press I, or Evidence in the pause menu).
    ///   Left   collected evidence, filterable by category, with status tags (NEW / EXAMINED / SHOWN)
    ///   Right  detail view: category, location found, description, status (collected, examined,
    ///          presented to whom), examination notes, established facts, connections made
    ///   Actions VIEW (opens the inspect view) · EXAMINE · CONNECT (pick a second item) · BACK
    /// All state comes from EvidenceService; this screen only displays it and forwards actions.
    /// </summary>
    public sealed class EvidenceScreen : ModalScreen
    {
        [Header("List")]
        [SerializeField] private UIOptionStepper filter;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Button itemTemplate;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private TMP_Text emptyLabel;

        [Header("Detail")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailCategory;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailLocation;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private TMP_Text detailStatus;
        [SerializeField] private TMP_Text detailExamination;
        [SerializeField] private TMP_Text detailFacts;
        [SerializeField] private TMP_Text detailConnections;
        [SerializeField] private TMP_Text connectHint;

        [Header("Buttons")]
        [SerializeField] private Button viewButton;
        [SerializeField] private Button examineButton;
        [SerializeField] private Button connectButton;
        [SerializeField] private TMP_Text connectLabel;
        [SerializeField] private Button backButton;

        private static readonly string[] FilterNames = { "ALL", "DOCUMENTS", "PHOTOGRAPHS", "OBJECTS", "DIGITAL", "FORENSIC" };

        private UIList<Button> _items;
        private readonly List<EvidenceDefinition> _shown = new List<EvidenceDefinition>();
        private EvidenceDefinition _selected;
        private EvidenceDefinition _connectFrom; // non-null while choosing the second item
        private int _filter;
        private string _resultMessage; // outcome of the last connection attempt

        protected override void Awake()
        {
            base.Awake();
            _items = new UIList<Button>(itemTemplate, itemContainer);
            viewButton.onClick.AddListener(OnView);
            examineButton.onClick.AddListener(OnExamine);
            connectButton.onClick.AddListener(OnConnect);
            backButton.onClick.AddListener(OnBack);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<EvidenceInventoryRequestedEvent>(OnRequested);
            EventBus.Subscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
        }

        protected override void OnDisable()
        {
            EventBus.Unsubscribe<EvidenceInventoryRequestedEvent>(OnRequested);
            EventBus.Unsubscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
            base.OnDisable();
        }

        private static EvidenceService Service => Services.TryGet(out GameManager game) ? game.Evidence : null;

        private void OnRequested(EvidenceInventoryRequestedEvent evt)
        {
            if (IsVisible)
            {
                if (UIOverlay.OpenCount <= 1) Close(); // I again closes it (not while the inspect view is on top)
                return;
            }
            EvidenceService service = Service;
            if (service == null || !Services.TryGet(out GameManager game) || !game.Case.HasActiveCase) return;
            if (!OpenModal()) return;

            _connectFrom = null;
            filter.Bind("SHOW", FilterNames, _filter, i =>
            {
                _filter = i;
                RefreshList();
            });
            RefreshAll();
        }

        private void OnEvidenceChanged(EvidenceStateChangedEvent evt)
        {
            if (IsVisible) RefreshAll();
        }

        private void OnBack()
        {
            if (_connectFrom != null)
            {
                _connectFrom = null; // cancel connecting first
                RefreshAll();
                return;
            }
            Close();
        }

        private void OnView()
        {
            if (_selected != null) EventBus.Publish(new EvidenceInspectRequestedEvent(_selected, false));
        }

        private void OnExamine()
        {
            EvidenceService service = Service;
            if (service == null || _selected == null) return;
            service.Examine(_selected);
            RefreshAll();
        }

        private void OnConnect()
        {
            if (_selected == null) return;
            _connectFrom = _connectFrom == null ? _selected : null; // toggle
            _resultMessage = null;
            RefreshAll();
        }

        private void OnItemClicked(EvidenceDefinition item)
        {
            if (_connectFrom != null && item != _connectFrom)
            {
                EvidenceDefinition first = _connectFrom;
                _connectFrom = null;
                ConnectResult result = Service.TryConnect(first, item, out EvidenceConnection connection);
                _selected = item;
                _resultMessage = ConnectMessage(result, first, item, connection);
                RefreshAll();
                return;
            }

            _selected = item;
            _resultMessage = null;
            RefreshAll();
        }

        private static string ConnectMessage(ConnectResult result, EvidenceDefinition a, EvidenceDefinition b, EvidenceConnection connection)
        {
            switch (result)
            {
                case ConnectResult.Connected: return $"<color=#d8d8dc>CONNECTED:</color> {connection.explanation}";
                case ConnectResult.AlreadyConnected: return "You've already connected these.";
                case ConnectResult.NotRelated: return $"{a.displayName} and {b.displayName} don't seem connected.";
                default: return "Those can't be connected.";
            }
        }

        // ------------------------------------------------------------------ refresh

        private void RefreshAll()
        {
            RefreshList();
            RefreshDetail();
        }

        private void RefreshList()
        {
            EvidenceService service = Service;
            _items.Clear();
            _shown.Clear();
            if (service == null) return;

            List<EvidenceDefinition> collected = service.GetCollected();
            foreach (EvidenceDefinition d in collected)
                if (MatchesFilter(d)) _shown.Add(d);

            if (_selected == null || !collected.Contains(_selected))
                _selected = collected.Count > 0 ? collected[collected.Count - 1] : null; // newest first
            if (_selected != null && !_shown.Contains(_selected) && _shown.Count > 0) _selected = _shown[_shown.Count - 1];

            // Newest at the top.
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                EvidenceDefinition d = _shown[i];
                EvidenceState s = service.GetState(d.evidenceId);
                Button b = _items.Add();
                var label = b.GetComponentInChildren<TMP_Text>();
                string tag = !s.examined ? "NEW" : s.WasPresented ? "SHOWN" : "EXAMINED";
                string tagColor = !s.examined ? "#c8433a" : "#7a7d86";
                bool selected = d == _selected;
                bool source = d == _connectFrom;
                string name = source ? $"<color=#c8433a>{d.displayName}</color>" : selected ? $"<b>{d.displayName}</b>" : d.displayName;
                label.text = $"{name}\n<size=70%><color=#7a7d86>{d.CategoryLabel}</color>   <color={tagColor}>{tag}</color></size>";
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => OnItemClicked(d));
                if (b.TryGetComponent(out UIButtonStyle style)) style.SetMarked(selected || source);
            }

            int total = Services.TryGet(out GameManager game) && game.Case.CurrentCase != null ? game.Case.CurrentCase.EvidenceTotal : 0;
            countLabel.text = total > 0 ? $"{collected.Count} / {total} COLLECTED" : $"{collected.Count} COLLECTED";
            emptyLabel.gameObject.SetActive(_shown.Count == 0);
            emptyLabel.text = collected.Count == 0 ? "No evidence collected yet. Search the scene and press E on anything that looks important." : "Nothing in this category.";
        }

        private bool MatchesFilter(EvidenceDefinition d)
        {
            switch (_filter)
            {
                case 1: return d.category == EvidenceCategory.Document;
                case 2: return d.category == EvidenceCategory.Photograph;
                case 3: return d.category == EvidenceCategory.Object;
                case 4: return d.category == EvidenceCategory.Digital;
                case 5: return d.category == EvidenceCategory.Forensic;
                default: return true;
            }
        }

        private void RefreshDetail()
        {
            EvidenceService service = Service;
            detailRoot.SetActive(_selected != null && service != null);
            if (_selected == null || service == null)
            {
                connectHint.gameObject.SetActive(false);
                return;
            }

            EvidenceDefinition d = _selected;
            EvidenceState s = service.GetState(d.evidenceId);

            detailCategory.text = $"{d.CategoryLabel}  ·  {d.evidenceId}";
            detailTitle.text = d.displayName;
            detailLocation.text = string.IsNullOrEmpty(d.locationFound) ? "" : "FOUND  " + d.locationFound;
            detailDescription.text = d.description;

            // Status lines.
            var status = new StringBuilder();
            status.Append(Tick(s.collected)).Append(d.collectable ? "Collected" : "Logged (left in place)").Append('\n');
            status.Append(Tick(s.examined)).Append(s.examined ? "Examined" : "Not examined").Append('\n');
            status.Append(Tick(s.WasPresented)).Append(s.WasPresented ? "Shown to " + SuspectNames(s.presentedTo) : "Not shown to a suspect");
            detailStatus.text = status.ToString();

            detailExamination.text = s.examined
                ? (string.IsNullOrEmpty(d.examinationNotes) ? "Nothing more to learn from this." : d.examinationNotes)
                : "<i>Examine this item to look for details.</i>";

            // Facts: established ones by name; others stay hidden ("?") so nothing is spoiled.
            var facts = new StringBuilder();
            int hidden = 0;
            if (d.relatedFacts != null)
                foreach (CaseFact f in d.relatedFacts)
                {
                    if (f == null) continue;
                    if (service.IsFactEstablished(f)) facts.Append("<color=#c8433a>•</color>  ").Append(f.title).Append(" <color=#7a7d86>- ").Append(f.description).Append("</color>\n");
                    else hidden++;
                }
            if (hidden > 0) facts.Append($"<color=#7a7d86>{hidden} more to establish (examine it)</color>");
            detailFacts.text = facts.Length == 0 ? "<color=#7a7d86>None.</color>" : facts.ToString().TrimEnd();

            // Connections made.
            var links = new StringBuilder();
            foreach (MadeConnection c in service.GetConnections(d))
                links.Append("<b>+ ").Append(c.Other.displayName).Append("</b>\n<color=#a9abb3>").Append(c.Connection.explanation).Append("</color>\n");
            detailConnections.text = links.Length == 0 ? "<color=#7a7d86>None yet. Use CONNECT to link it with another item.</color>" : links.ToString().TrimEnd();

            examineButton.gameObject.SetActive(!s.examined);
            connectLabel.text = _connectFrom != null ? "CANCEL" : "CONNECT";
            string hint = _connectFrom != null ? $"Choose another item to connect with <b>{_connectFrom.displayName}</b>." : _resultMessage;
            connectHint.text = hint ?? "";
            connectHint.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        }

        private static string Tick(bool on) => on ? "<color=#c8433a>•</color>  " : "<color=#4a4c55>•</color>  ";

        private static string SuspectNames(List<string> ids)
        {
            var names = new List<string>();
            CaseDefinition c = Services.TryGet(out GameManager game) ? game.Case.CurrentCase : null;
            foreach (string id in ids)
            {
                string name = id;
                if (c != null)
                    foreach (SuspectProfile p in c.suspects)
                        if (p != null && p.suspectId == id) name = p.displayName;
                names.Add(name);
            }
            return string.Join(", ", names);
        }

        protected override void OnHidden()
        {
            _connectFrom = null;
            _resultMessage = null;
            if (connectHint != null) connectHint.gameObject.SetActive(false);
        }
    }
}
