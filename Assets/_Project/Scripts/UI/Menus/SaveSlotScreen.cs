using System;
using System.Collections.Generic;
using System.Globalization;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Flow;
using TLA.Systems.Save;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// SAVE / LOAD INVESTIGATION (one global screen in Bootstrap).
    ///   Save mode (pause menu): the manual slots; choosing an occupied slot asks to confirm the overwrite.
    ///   Load mode (main menu or pause menu): the autosave plus the manual slots, newest info first in each row:
    ///     case, objective, evidence, location, when saved, time played. Damaged saves are shown as such
    ///     and can't be loaded; a save restored from its backup says so.
    /// Delete asks to confirm too. Everything goes through GameManager / SaveService.
    /// </summary>
    public sealed class SaveSlotScreen : UIScreen
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button slotTemplate;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Button deleteButton;
        [SerializeField] private TMP_Text deleteLabel;
        [SerializeField] private Button backButton;

        private UIList<Button> _rows;
        private bool _saveMode;
        private int? _selected;
        private bool _confirmAction;
        private bool _confirmDelete;
        private bool _counted; // pushed onto UIOverlay (opened over the pause menu)
        private readonly List<SaveSlotInfo> _slots = new List<SaveSlotInfo>();

        protected override void Awake()
        {
            base.Awake();
            _rows = new UIList<Button>(slotTemplate, slotContainer);
            actionButton.onClick.AddListener(OnAction);
            deleteButton.onClick.AddListener(OnDelete);
            backButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SaveSlotsRequestedEvent>(OnRequested);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SaveSlotsRequestedEvent>(OnRequested);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            Release();
        }

        private static GameManager Game => Services.TryGet(out GameManager g) ? g : null;

        private void OnRequested(SaveSlotsRequestedEvent evt)
        {
            GameManager game = Game;
            if (game == null) return;
            if (evt.Save && !game.CanSave(out string reason))
            {
                EventBus.Publish(new TLA.Systems.Investigation.NotificationEvent("CAN'T SAVE NOW", reason));
                return;
            }

            _saveMode = evt.Save;
            _selected = null;
            _confirmAction = _confirmDelete = false;
            statusLabel.text = "";
            if (!_counted && Services.TryGet(out GameStateManager state) && state.IsPaused)
            {
                UIOverlay.Push(); // hide the pause menu behind us
                _counted = true;
            }
            Refresh();
            Show();
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            // Resumed, or a load changed the scene: this screen's job is done.
            if (IsVisible && evt.Current != GameState.Paused && evt.Current != GameState.MainMenu) Close();
            if (IsVisible && evt.Previous == GameState.MainMenu && evt.Current != GameState.MainMenu) Close();
        }

        public void Close()
        {
            if (!IsVisible) return;
            Hide();
            Release();
            EventBus.Publish(new SaveSlotsClosedEvent());
        }

        private void Release()
        {
            if (!_counted) return;
            _counted = false;
            UIOverlay.Pop();
        }

        // ------------------------------------------------------------------ list

        private void Refresh()
        {
            GameManager game = Game;
            titleLabel.text = _saveMode ? "SAVE" : "LOAD";
            title.text = _saveMode ? "SAVE INVESTIGATION" : "LOAD INVESTIGATION";

            _slots.Clear();
            foreach (SaveSlotInfo info in game.Saves.GetAllSlots())
                if (!(_saveMode && info.IsAutosave)) _slots.Add(info); // the autosave is written by the game only

            _rows.Clear();
            foreach (SaveSlotInfo slotInfo in _slots)
            {
                SaveSlotInfo info = slotInfo;
                Button row = _rows.Add();
                row.GetComponentInChildren<TMP_Text>().text = RowText(info, info.Slot == _selected);
                row.interactable = _saveMode || info.IsValid;
                if (row.TryGetComponent(out UIButtonStyle style)) style.SetMarked(info.Slot == _selected);
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() =>
                {
                    _selected = info.Slot;
                    _confirmAction = _confirmDelete = false;
                    statusLabel.text = "";
                    Refresh();
                });
            }

            SaveSlotInfo selected = _slots.Find(s => s.Slot == _selected);
            bool canAct = selected != null && (_saveMode || selected.IsValid);
            actionButton.interactable = canAct;
            actionLabel.text = ActionText(selected);
            deleteButton.gameObject.SetActive(selected != null && selected.Exists);
            deleteLabel.text = _confirmDelete ? "DELETE? CAN'T BE UNDONE" : "DELETE";
        }

        private string ActionText(SaveSlotInfo selected)
        {
            if (_saveMode)
            {
                if (selected == null) return "CHOOSE A SLOT";
                return selected.Exists && _confirmAction ? "OVERWRITE? CONFIRM" : selected.Exists ? "OVERWRITE" : "SAVE HERE";
            }
            if (selected == null) return "CHOOSE A SAVE";
            bool inGame = Services.TryGet(out GameStateManager state) && state.Current != GameState.MainMenu;
            return inGame && _confirmAction ? "LOAD? UNSAVED PROGRESS IS LOST" : "LOAD";
        }

        private static string RowText(SaveSlotInfo info, bool selected)
        {
            string name = info.IsAutosave ? "AUTOSAVE" : $"SLOT {info.Slot + 1}";
            name = selected ? $"<color=#c8433a><b>{name}</b></color>" : $"<b>{name}</b>";
            if (!info.Exists) return $"{name}   <color=#7a7d86>Empty</color>";
            if (!info.IsValid) return $"{name}   <color=#c8433a>Damaged save, can't be loaded</color>\n<size=75%><color=#7a7d86>{info.Problem}</color></size>";

            SaveSummary s = info.Summary;
            string when = info.SavedAtUtc == default ? "" : info.SavedAtUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
            string played = s.playTimeSeconds > 0 ? $"   ·   played {FormatTime(s.playTimeSeconds)}" : "";
            string backup = info.UsesBackup ? "   <color=#d39a3a>(restored from backup)</color>" : "";
            string caseLine = string.IsNullOrEmpty(s.caseTitle) ? "Investigation" : s.caseTitle;
            string details = $"{(string.IsNullOrEmpty(s.objective) ? "" : s.objective + "   ·   ")}Evidence {s.evidenceCollected} / {s.evidenceTotal}" +
                             $"{(string.IsNullOrEmpty(s.locationName) ? "" : "   ·   " + s.locationName)}{(string.IsNullOrEmpty(s.status) ? "" : "   ·   " + s.status)}";
            return $"{name}   {caseLine}{backup}\n<size=80%><color=#a9abb3>{details}</color></size>\n<size=75%><color=#7a7d86>Saved {when}{played}</color></size>";
        }

        private static string FormatTime(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : $"{Math.Max(1, t.Minutes)} min";
        }

        // ------------------------------------------------------------------ actions

        private void OnAction()
        {
            GameManager game = Game;
            SaveSlotInfo selected = _slots.Find(s => s.Slot == _selected);
            if (game == null || selected == null) return;

            if (_saveMode)
            {
                if (selected.Exists && !_confirmAction)
                {
                    _confirmAction = true; // overwriting needs a second press
                    Refresh();
                    return;
                }
                bool ok = game.SaveGame(selected.Slot);
                _confirmAction = false;
                statusLabel.text = ok ? $"Saved to {SaveService.SlotName(selected.Slot)}." : "<color=#c8433a>Couldn't save. Your previous save is untouched.</color>";
                Refresh();
                return;
            }

            bool inGame = Services.TryGet(out GameStateManager state) && state.Current != GameState.MainMenu;
            if (inGame && !_confirmAction)
            {
                _confirmAction = true; // loading over an investigation in progress needs a second press
                Refresh();
                return;
            }
            _confirmAction = false;
            if (game.LoadGame(selected.Slot))
            {
                Close();
                return;
            }
            statusLabel.text = "<color=#c8433a>That save couldn't be loaded. Nothing has changed.</color>";
            Refresh();
        }

        private void OnDelete()
        {
            GameManager game = Game;
            SaveSlotInfo selected = _slots.Find(s => s.Slot == _selected);
            if (game == null || selected == null || !selected.Exists) return;
            if (!_confirmDelete)
            {
                _confirmDelete = true;
                Refresh();
                return;
            }
            game.Saves.Delete(selected.Slot);
            _confirmDelete = false;
            statusLabel.text = $"Deleted {SaveService.SlotName(selected.Slot)}.";
            Refresh();
        }
    }
}
