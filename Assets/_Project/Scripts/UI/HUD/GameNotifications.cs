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
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EvidenceAddedEvent>(OnEvidence);
            EventBus.Unsubscribe<NoteAddedEvent>(OnNote);
            EventBus.Unsubscribe<GameSavedEvent>(OnSaved);
            EventBus.Unsubscribe<NotificationEvent>(OnNotification);
        }

        private void OnEvidence(EvidenceAddedEvent evt) => feed.Show("EVIDENCE ADDED", evt.Evidence.displayName);
        private void OnNote(NoteAddedEvent evt) => feed.Show("NOTEBOOK UPDATED", "Press J to review your notes");
        private void OnSaved(GameSavedEvent evt) => feed.Show("INVESTIGATION SAVED", "");
        private void OnNotification(NotificationEvent evt) => feed.Show(evt.Title, evt.Message);
    }
}
