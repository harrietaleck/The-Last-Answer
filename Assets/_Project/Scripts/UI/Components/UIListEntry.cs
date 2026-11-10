using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Components
{
    /// <summary>
    /// A reusable two-line list row (title + body) used for evidence, notes and case lists.
    /// Optional red marker on the left for "new" or "selected" items.
    /// </summary>
    public sealed class UIListEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Graphic marker;

        public void Set(string titleText, string bodyText, bool highlighted = false)
        {
            if (title != null)
            {
                title.text = titleText;
                title.gameObject.SetActive(!string.IsNullOrEmpty(titleText));
            }

            if (body != null)
            {
                body.text = bodyText;
                body.gameObject.SetActive(!string.IsNullOrEmpty(bodyText));
            }

            if (marker != null) marker.enabled = highlighted;
        }
    }
}
