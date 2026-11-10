using System.Collections.Generic;
using UnityEngine;

namespace TLA.UI.Components
{
    /// <summary>
    /// Spawns copies of a row prefab under a container and reuses them (no destroy/re-create on
    /// every refresh). Used by the notebook, case list, suggested questions and dialogue history.
    /// </summary>
    public sealed class UIList<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _container;
        private readonly List<T> _items = new List<T>();
        private int _used;

        public UIList(T prefab, Transform container)
        {
            _prefab = prefab;
            _container = container;
            if (_prefab != null && _prefab.transform.IsChildOf(_container)) _prefab.gameObject.SetActive(false); // template stays hidden
        }

        public IReadOnlyList<T> Items => _items;
        public int Count => _used;

        /// <summary>Hides all rows (they're kept for reuse).</summary>
        public void Clear()
        {
            foreach (T item in _items) item.gameObject.SetActive(false);
            _used = 0;
        }

        /// <summary>Returns the next free row, creating one if needed, placed last in the container.</summary>
        public T Add()
        {
            T item;
            if (_used < _items.Count)
            {
                item = _items[_used];
            }
            else
            {
                item = Object.Instantiate(_prefab, _container);
                _items.Add(item);
            }

            _used++;
            item.gameObject.SetActive(true);
            item.transform.SetAsLastSibling();
            return item;
        }
    }
}
