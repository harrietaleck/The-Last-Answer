using TLA.Core.Events;
using TLA.Core.Services;
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
    /// INSPECT VIEW: looking at one piece of evidence up close.
    ///   Documents  paper pages (◀ ▶ to turn)        Digital  a screen with the log / footage notes
    ///   Photograph the image (or a placeholder frame) and its caption
    ///   Object / Forensic  image + description
    /// Buttons: COLLECT (when found in the world), EXAMINE (reveals the examination notes and
    /// establishes facts, through EvidenceService), CLOSE.
    /// Opened by an EvidencePickup in the scene or from the evidence inventory.
    /// </summary>
    public sealed class EvidenceInspectScreen : ModalScreen
    {
        [Header("Header")]
        [SerializeField] private TMP_Text categoryLabel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text location;

        [Header("Content")]
        [SerializeField] private Image contentPanel;
        [SerializeField] private TMP_Text contentText;
        [SerializeField] private GameObject photoFrame;
        [SerializeField] private Image photo;
        [SerializeField] private TMP_Text photoPlaceholder;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private GameObject pageControls;
        [SerializeField] private Button previousPage;
        [SerializeField] private Button nextPage;
        [SerializeField] private TMP_Text pageIndicator;

        [Header("Examination")]
        [SerializeField] private TMP_Text examinationText;

        [Header("Buttons")]
        [SerializeField] private Button collectButton;
        [SerializeField] private TMP_Text collectLabel;
        [SerializeField] private Button examineButton;
        [SerializeField] private Button closeButton;

        private static readonly Color Paper = new Color(0.86f, 0.83f, 0.76f);
        private static readonly Color Ink = new Color(0.13f, 0.12f, 0.11f);
        private static readonly Color ScreenBg = new Color(0.04f, 0.07f, 0.08f);
        private static readonly Color ScreenText = new Color(0.55f, 0.85f, 0.8f);
        private static readonly Color Frame = new Color(0.1f, 0.1f, 0.11f);

        private EvidenceDefinition _evidence;
        private bool _fromWorld;
        private int _page;

        protected override void Awake()
        {
            base.Awake();
            previousPage.onClick.AddListener(() => TurnPage(-1));
            nextPage.onClick.AddListener(() => TurnPage(1));
            collectButton.onClick.AddListener(OnCollect);
            examineButton.onClick.AddListener(OnExamine);
            closeButton.onClick.AddListener(Close);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<EvidenceInspectRequestedEvent>(OnRequested);
            EventBus.Subscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
        }

        protected override void OnDisable()
        {
            EventBus.Unsubscribe<EvidenceInspectRequestedEvent>(OnRequested);
            EventBus.Unsubscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
            base.OnDisable();
        }

        private static EvidenceService Service => Services.TryGet(out GameManager game) ? game.Evidence : null;

        private void OnRequested(EvidenceInspectRequestedEvent evt)
        {
            if (evt.Evidence == null) return;
            _evidence = evt.Evidence;
            _fromWorld = evt.FromWorld;
            _page = 0;
            if (!OpenModal()) return;
            Refresh();
        }

        private void OnEvidenceChanged(EvidenceStateChangedEvent evt)
        {
            if (IsVisible) Refresh();
        }

        private void TurnPage(int delta)
        {
            if (_evidence?.pages == null || _evidence.pages.Length == 0) return;
            int before = _page;
            _page = Mathf.Clamp(_page + delta, 0, _evidence.pages.Length - 1);
            if (_page != before) UISound.Play(TLA.Data.Audio.SoundCue.PageTurn);
            Refresh();
        }

        private void OnCollect()
        {
            EvidenceService service = Service;
            if (service == null || _evidence == null) return;
            if (service.Collect(_evidence) == CollectResult.Collected && _evidence.collectable && _fromWorld)
                Close(); // the object is gone from the scene; nothing left to look at here
            else
                Refresh();
        }

        private void OnExamine()
        {
            EvidenceService service = Service;
            if (service == null || _evidence == null) return;
            service.Discover(_evidence);
            service.Examine(_evidence); // facts announce themselves (FactEstablishedEvent → notification)
            Refresh();
        }

        private void Refresh()
        {
            EvidenceService service = Service;
            if (_evidence == null || service == null) return;
            EvidenceState state = service.GetState(_evidence.evidenceId);

            categoryLabel.text = state.collected ? $"{_evidence.CategoryLabel}  ·  IN EVIDENCE" : _evidence.CategoryLabel;
            title.text = _evidence.displayName;
            location.text = string.IsNullOrEmpty(_evidence.locationFound) ? "" : "Found: " + _evidence.locationFound;

            bool hasPages = _evidence.pages != null && _evidence.pages.Length > 0;
            bool visual = _evidence.category == EvidenceCategory.Photograph || _evidence.category == EvidenceCategory.Object || _evidence.category == EvidenceCategory.Forensic;
            bool paper = _evidence.category == EvidenceCategory.Document;

            // Text content: pages for documents/digital, otherwise the description (+ any extra page).
            string text = hasPages ? _evidence.pages[Mathf.Clamp(_page, 0, _evidence.pages.Length - 1)] : _evidence.description;
            contentText.text = text;
            contentPanel.color = paper ? Paper : (_evidence.category == EvidenceCategory.Digital ? ScreenBg : Frame);
            contentText.color = paper ? Ink : (_evidence.category == EvidenceCategory.Digital ? ScreenText : new Color(0.82f, 0.82f, 0.84f));

            // Image (photographs, objects): the sprite, or a labelled placeholder frame.
            photoFrame.SetActive(visual);
            photo.gameObject.SetActive(visual && _evidence.image != null);
            if (_evidence.image != null) photo.sprite = _evidence.image;
            photoPlaceholder.gameObject.SetActive(visual && _evidence.image == null);
            photoPlaceholder.text = visual && _evidence.image == null ? $"[ {_evidence.CategoryLabel} ]" : "";
            caption.text = _evidence.caption;
            caption.gameObject.SetActive(!string.IsNullOrEmpty(_evidence.caption));

            int pageCount = hasPages ? _evidence.pages.Length : 0;
            pageControls.SetActive(pageCount > 1);
            pageIndicator.text = pageCount > 1 ? $"PAGE {_page + 1} / {pageCount}" : "";
            previousPage.interactable = _page > 0;
            nextPage.interactable = _page < pageCount - 1;

            // Examination: hidden until examined.
            if (state.examined)
                examinationText.text = string.IsNullOrEmpty(_evidence.examinationNotes) ? "Nothing more to learn from this." : _evidence.examinationNotes;
            else
                examinationText.text = "<i>Not examined yet. Examine it to look for details.</i>";

            // Buttons.
            bool canCollect = !state.collected && service.IsAvailable(_evidence);
            collectButton.gameObject.SetActive(canCollect);
            collectLabel.text = _evidence.collectable ? "COLLECT" : "LOG AS EVIDENCE";
            examineButton.gameObject.SetActive(!state.examined);
        }
    }
}
