using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TLA.UI.Components
{
    /// <summary>
    /// Small stack of notifications ("EVIDENCE ADDED · Crime scene photographs"). Each fades in,
    /// stays a few seconds and fades out. At most <see cref="maxVisible"/> show at once; extras wait
    /// in a queue. Generic: anything can call <see cref="Show"/>.
    /// </summary>
    public sealed class NotificationFeed : MonoBehaviour
    {
        [SerializeField] private NotificationToast toastTemplate;
        [SerializeField] private int maxVisible = 3;
        [SerializeField] private float displaySeconds = 3.5f;

        private readonly Queue<(string title, string message)> _queue = new Queue<(string, string)>();
        private readonly List<NotificationToast> _pool = new List<NotificationToast>();

        private void Awake()
        {
            if (toastTemplate != null) toastTemplate.gameObject.SetActive(false);
        }

        public void Show(string title, string message)
        {
            _queue.Enqueue((title, message));
            TryDequeue();
        }

        private void Update()
        {
            TryDequeue();
        }

        private void TryDequeue()
        {
            while (_queue.Count > 0 && ActiveCount() < maxVisible)
            {
                (string title, string message) = _queue.Dequeue();
                NotificationToast toast = GetFreeToast();
                toast.transform.SetAsFirstSibling(); // newest on top
                toast.Play(title, message, displaySeconds);
            }
        }

        private int ActiveCount()
        {
            int n = 0;
            foreach (NotificationToast t in _pool) if (t.IsPlaying) n++;
            return n;
        }

        private NotificationToast GetFreeToast()
        {
            foreach (NotificationToast t in _pool) if (!t.IsPlaying) return t;
            NotificationToast created = Instantiate(toastTemplate, toastTemplate.transform.parent);
            _pool.Add(created);
            return created;
        }
    }
}
