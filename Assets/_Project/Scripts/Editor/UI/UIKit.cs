using TLA.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Low-level helpers for building uGUI hierarchies from code: rect transforms, images, text,
    /// layout groups. All colours/fonts come from the <see cref="UITheme"/>.
    /// </summary>
    internal static class UIKit
    {
        public static UITheme Theme;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI layer
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Fills the parent, inset by the given margins (left, right, top, bottom).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors to a fraction of the parent (responsive regions), inset by margins.</summary>
        public static RectTransform Region(RectTransform rt, float minX, float minY, float maxX, float maxY, float inset = 0)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Fixed-size element anchored at a point of the parent (e.g. top-left corner).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Component target, Color color, bool raycast = false)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal, float spacing = 0f, bool wrap = true)
        {
            RectTransform rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Theme != null && Theme.font != null) tmp.font = Theme.font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.characterSpacing = spacing;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Small spaced capitals, e.g. "CURRENT OBJECTIVE".</summary>
        public static TextMeshProUGUI Label(Transform parent, string text, Color? color = null)
        {
            return Text(parent, "Label_" + text, text, Theme.labelSize, color ?? Theme.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, Theme.labelSpacing, false);
        }

        public static VerticalLayoutGroup VLayout(Component target, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool controlHeight = true, bool expandWidth = true)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = controlHeight;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup HLayout(Component target, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static LayoutElement Layout(Component target, float prefWidth = -1, float prefHeight = -1,
            float flexWidth = -1, float flexHeight = -1, float minHeight = -1, float minWidth = -1)
        {
            // TryGetComponent, not "GetComponent() ?? Add": Unity's fake-null objects break the ?? operator.
            if (!target.TryGetComponent(out LayoutElement le)) le = target.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = prefWidth;
            le.preferredHeight = prefHeight;
            le.flexibleWidth = flexWidth;
            le.flexibleHeight = flexHeight;
            le.minHeight = minHeight;
            le.minWidth = minWidth;
            return le;
        }

        /// <summary>1px horizontal rule.</summary>
        public static Image Divider(Transform parent, Color? color = null)
        {
            RectTransform rt = Rect("Divider", parent);
            Layout(rt, -1, 1, 1);
            return Image(rt, color ?? Theme.divider);
        }

        public static RectTransform Spacer(Transform parent, float height)
        {
            RectTransform rt = Rect("Spacer", parent);
            Layout(rt, -1, height);
            return rt;
        }

        /// <summary>Full-screen dark overlay that also blocks clicks to whatever is behind.</summary>
        public static Image Scrim(Transform parent, Color color)
        {
            RectTransform rt = Stretch(Rect("Scrim", parent));
            return Image(rt, color, true);
        }

        /// <summary>
        /// A vertical scroll list: Viewport (clips) → Content (vertical layout, grows with its rows).
        /// </summary>
        public static ScrollRect ScrollList(Transform parent, string name, float spacing, out RectTransform content)
        {
            RectTransform root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            Image(root, new Color(0, 0, 0, 0), true); // receives mouse-wheel events

            RectTransform viewport = Stretch(Rect("Viewport", root));
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VLayout(content, spacing);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        public static Canvas ScreenCanvas(string name, Transform parent, int sortingOrder)
        {
            RectTransform rt = Rect(name, parent);
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = rt.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // balances 16:9, 16:10, 21:9 and 4:3 screens
            rt.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
