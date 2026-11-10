using TLA.Core.Events;
using TLA.Systems;
using TLA.Systems.Investigation;
using TLA.UI.Components;
using UnityEngine;

namespace TLA.UI.HUD
{
    /// <summary>
    /// Turns game events into on-screen notifications: new evidence, notebook entries, saves and
    /// any generic <see cref="NotificationEvent"/>. Keeping this translation in one place means the
    /// feed itself stays a generic, reusable component.
    /// </summary>
    public sealed class GameNotifications : MonoBehaviour
    {
        [SerializeField] private NotificationFeed feed;

        private void OnEnable()
        {
            EventBus.Subscribe<EvidenceAddedEvent>(OnEvidence);
            EventBus.Subscribe<NoteAddedEvent>(OnNote);
            EventBus.Subscribe<GameSavedEvent>(OnSaved);
            EventBus.Subscribe<NotificationEvent>(OnNotification);
            EventBus.Subscribe<FactEstablishedEvent>(OnFact);
            EventBus.Subscribe<EvidenceConnectedEvent>(OnConnected);
            EventBus.Subscribe<TLA.Systems.Cases.ObjectiveCompletedEvent>(OnObjective);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EvidenceAddedEvent>(OnEvidence);
            EventBus.Unsubscribe<NoteAddedEvent>(OnNote);
            EventBus.Unsubscribe<GameSavedEvent>(OnSaved);
            EventBus.Unsubscribe<NotificationEvent>(OnNotification);
            EventBus.Unsubscribe<FactEstablishedEvent>(OnFact);
            EventBus.Unsubscribe<EvidenceConnectedEvent>(OnConnected);
            EventBus.Unsubscribe<TLA.Systems.Cases.ObjectiveCompletedEvent>(OnObjective);
        }

        private void OnEvidence(EvidenceAddedEvent evt) => feed.Show("EVIDENCE ADDED", evt.Evidence.displayName + "  ·  Press I to review");
        private void OnNote(NoteAddedEvent evt) => feed.Show("NOTEBOOK UPDATED", "Press J to review your notes");
        private void OnSaved(GameSavedEvent evt) => feed.Show(evt.Slot == TLA.Systems.Save.SaveService.AutoSlot ? "AUTOSAVED" : "INVESTIGATION SAVED",
            evt.Slot == TLA.Systems.Save.SaveService.AutoSlot ? "" : $"Slot {evt.Slot + 1}");
        private void OnNotification(NotificationEvent evt) => feed.Show(evt.Title, evt.Message);
        private void OnObjective(TLA.Systems.Cases.ObjectiveCompletedEvent evt) => feed.Show("OBJECTIVE COMPLETE", evt.Objective.title);
        private void OnFact(FactEstablishedEvent evt) => feed.Show("NEW FACT", evt.Fact.title);
        private void OnConnected(EvidenceConnectedEvent evt) => feed.Show("CONNECTION MADE", $"{evt.First.displayName} + {evt.Second.displayName}");
    }
}
