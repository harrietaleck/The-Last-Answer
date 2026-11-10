using System.IO;
using TLA.UI.Components;
using TLA.UI.Dialogue;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static TLA.EditorTools.UI.UIKit;

namespace TLA.EditorTools.UI
{
    /// <summary>
    /// Generates the reusable UI component prefabs in Assets/_Project/Prefabs/UI/Components/.
    /// Screens are built from INSTANCES of these, so restyling a prefab (e.g. UI_MenuButton)
    /// restyles every button in the game. Use them when you add new screens by hand too.
    /// </summary>
    internal static class UIComponentLibrary
    {
        public const string Folder = "Assets/_Project/Prefabs/UI/Components";

        public static GameObject MenuButton, ActionButton, ListButton, SliderRow, OptionStepper, ListEntry, Toast, DialogueEntry;

        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            MenuButton = Save(BuildMenuButton(), "UI_MenuButton");
            ActionButton = Save(BuildActionButton(), "UI_ActionButton");
            ListButton = Save(BuildListButton(), "UI_ListButton");
            SliderRow = Save(BuildSliderRow(), "UI_SliderRow");
            OptionStepper = Save(BuildOptionStepper(), "UI_OptionStepper");
            ListEntry = Save(BuildListEntry(), "UI_ListEntry");
            Toast = Save(BuildToast(), "UI_Toast");
            DialogueEntry = Save(BuildDialogueEntry(), "UI_DialogueEntry");
        }

        /// <summary>Places an instance of a component prefab (stays linked to the prefab).</summary>
        public static GameObject Spawn(GameObject prefab, Transform parent, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (name != null) go.name = name;
            return go;
        }

        /// <summary>Instance of a button prefab with its label text set.</summary>
        public static Button SpawnButton(GameObject prefab, Transform parent, string label)
        {
            GameObject go = Spawn(prefab, parent, "Button_" + label.Replace(" ", ""));
            go.GetComponentInChildren<TMP_Text>().text = label;
            return go.GetComponent<Button>();
        }

        // ------------------------------------------------------------------ builders

        /// <summary>Large text-only menu button with a red accent bar that appears on hover/selection.</summary>
        private static GameObject BuildMenuButton()
        {
            RectTransform root = Rect("UI_MenuButton", null);
            root.sizeDelta = new Vector2(460, 58);
            Image bg = Image(root, new Color(1, 1, 1, 0.0f), true); // invisible hit area
            Layout(root, 460, 58);

            RectTransform bar = Rect("AccentBar", root);
            Place(bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(3, 30));
            Image barImage = Image(bar, Theme.accent);

            TextMeshProUGUI label = Text(root, "Label", "BUTTON", 26, Theme.textSecondary, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Theme.labelSpacing - 1, false);
            Stretch(label.rectTransform, 22, 0, 0, 0);

            AddButton(root, bg, label, barImage);
            return root.gameObject;
        }

        /// <summary>Compact boxed button (ASK, BACK, RESET…): charcoal box, label turns white, red underline.</summary>
        private static GameObject BuildActionButton()
        {
            RectTransform root = Rect("UI_ActionButton", null);
            root.sizeDelta = new Vector2(220, 52);
            Image bg = Image(root, Theme.panelRaised, true);
            Layout(root, 220, 52);

            RectTransform bar = Rect("AccentBar", root);
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, 2);
            bar.anchoredPosition = Vector2.zero;
            Image barImage = Image(bar, Theme.accent);

            TextMeshProUGUI label = Text(root, "Label", "ACTION", 17, Theme.textSecondary, TextAlignmentOptions.Center, FontStyles.Normal, 4, false);
            Stretch(label.rectTransform, 12, 12, 0, 0);

            AddButton(root, bg, label, barImage);
            return root.gameObject;
        }

        /// <summary>Full-width row button with wrapping text (suggested questions, case list).</summary>
        private static GameObject BuildListButton()
        {
            RectTransform root = Rect("UI_ListButton", null);
            Image bg = Image(root, new Color(Theme.panelRaised.r, Theme.panelRaised.g, Theme.panelRaised.b, 0.85f), true);
            VLayout(root, 0, new RectOffset(20, 16, 12, 12));
            Layout(root, -1, -1, 1, -1, 46);

            RectTransform bar = Rect("AccentBar", root);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(0, 1);
            bar.pivot = new Vector2(0, 0.5f);
            bar.sizeDelta = new Vector2(3, 0);
            bar.anchoredPosition = Vector2.zero;
            Image barImage = Image(bar, Theme.accent);

            TextMeshProUGUI label = Text(root, "Label", "Question?", 18, Theme.textSecondary, TextAlignmentOptions.MidlineLeft);
            AddButton(root, bg, label, barImage);
            return root.gameObject;
        }

        private static void AddButton(RectTransform root, Image target, TMP_Text label, Image bar)
        {
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None; // UIButtonStyle handles the look
            var style = root.gameObject.AddComponent<UIButtonStyle>();
            SetField(style, "label", label);
            SetField(style, "accentBar", bar);
            SetColor(style, "normalColor", Theme.textSecondary);
            SetColor(style, "activeColor", Theme.textPrimary);
            SetColor(style, "disabledColor", Theme.textMuted);
        }

        private static GameObject BuildSliderRow()
        {
            RectTransform root = Rect("UI_SliderRow", null);
            HLayout(root, 24, new RectOffset(0, 0, 0, 0));
            Layout(root, -1, 52, 1);

            TextMeshProUGUI label = Text(root, "Label", "SETTING", 17, Theme.textSecondary, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 4, false);
            Layout(label, -1, 52, 1);

            // Slider: thin track, red fill, small white handle.
            RectTransform sliderRt = Rect("Slider", root);
            Layout(sliderRt, 340, 52);
            var slider = sliderRt.gameObject.AddComponent<Slider>();

            RectTransform track = Rect("Track", sliderRt);
            track.anchorMin = new Vector2(0, 0.5f);
            track.anchorMax = new Vector2(1, 0.5f);
            track.sizeDelta = new Vector2(0, 4);
            Image(track, Theme.divider);

            RectTransform fillArea = Rect("FillArea", sliderRt);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 4);
            RectTransform fill = Stretch(Rect("Fill", fillArea));
            Image(fill, Theme.accent);

            RectTransform handleArea = Stretch(Rect("HandleArea", sliderRt), 8, 8, 0, 0);
            RectTransform handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(14, 22);
            Image handleImage = Image(handle, Theme.textPrimary, true);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            ColorBlock colors = slider.colors;
            colors.normalColor = Theme.textSecondary;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = Theme.accent;
            slider.colors = colors;

            TextMeshProUGUI value = Text(root, "Value", "100%", 17, Theme.textPrimary, TextAlignmentOptions.MidlineRight, FontStyles.Normal, 2, false);
            Layout(value, 96, 52);

            var row = root.gameObject.AddComponent<UISliderRow>();
            SetField(row, "label", label);
            SetField(row, "slider", slider);
            SetField(row, "valueText", value);
            return root.gameObject;
        }

        private static GameObject BuildOptionStepper()
        {
            RectTransform root = Rect("UI_OptionStepper", null);
            HLayout(root, 8);
            Layout(root, -1, 52, 1);

            TextMeshProUGUI label = Text(root, "Label", "SETTING", 17, Theme.textSecondary, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 4, false);
            Layout(label, -1, 52, 1);

            Button prev = ArrowButton(root, "Previous", "<");
            TextMeshProUGUI value = Text(root, "Value", "VALUE", 17, Theme.textPrimary, TextAlignmentOptions.Center, FontStyles.Normal, 3, false);
            Layout(value, 300, 52);
            Button next = ArrowButton(root, "Next", ">");

            var stepper = root.gameObject.AddComponent<UIOptionStepper>();
            SetField(stepper, "label", label);
            SetField(stepper, "valueText", value);
            SetField(stepper, "previousButton", prev);
            SetField(stepper, "nextButton", next);
            return root.gameObject;
        }

        private static Button ArrowButton(Transform parent, string name, string glyph)
        {
            RectTransform rt = Rect(name, parent);
            Layout(rt, 40, 40);
            Image bg = Image(rt, Theme.panelRaised, true);
            TextMeshProUGUI text = Text(rt, "Glyph", glyph, 20, Theme.textSecondary, TextAlignmentOptions.Center, FontStyles.Normal, 0, false);
            Stretch(text.rectTransform);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            colors.pressedColor = new Color(2.5f, 0.7f, 0.7f);
            colors.selectedColor = new Color(1.6f, 1.6f, 1.6f);
            colors.colorMultiplier = 2f;
            button.colors = colors;
            return button;
        }

        private static GameObject BuildListEntry()
        {
            RectTransform root = Rect("UI_ListEntry", null);
            VLayout(root, 4, new RectOffset(18, 8, 10, 12));
            Layout(root, -1, -1, 1);

            RectTransform marker = Rect("Marker", root);
            marker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            marker.anchorMin = new Vector2(0, 0);
            marker.anchorMax = new Vector2(0, 1);
            marker.pivot = new Vector2(0, 0.5f);
            marker.sizeDelta = new Vector2(3, -14);
            Image markerImage = Image(marker, Theme.accent);

            TextMeshProUGUI title = Text(root, "Title", "TITLE", 17, Theme.textPrimary, TextAlignmentOptions.TopLeft, FontStyles.Normal, 2);
            TextMeshProUGUI body = Text(root, "Body", "Body text", 17, Theme.textSecondary);

            var entry = root.gameObject.AddComponent<UIListEntry>();
            SetField(entry, "title", title);
            SetField(entry, "body", body);
            SetField(entry, "marker", markerImage);
            return root.gameObject;
        }

        private static GameObject BuildToast()
        {
            RectTransform root = Rect("UI_Toast", null);
            Image(root, Theme.panel);
            VLayout(root, 4, new RectOffset(22, 18, 14, 16));
            Layout(root, 420, -1);
            root.gameObject.AddComponent<CanvasGroup>();

            RectTransform bar = Rect("AccentBar", root);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(0, 1);
            bar.pivot = new Vector2(0, 0.5f);
            bar.sizeDelta = new Vector2(3, 0);
            Image(bar, Theme.accent);

            TextMeshProUGUI title = Text(root, "Title", "EVIDENCE ADDED", Theme.labelSize, Theme.accent, TextAlignmentOptions.TopLeft, FontStyles.Normal, Theme.labelSpacing, false);
            TextMeshProUGUI message = Text(root, "Message", "Item name", 19, Theme.textPrimary);

            var toast = root.gameObject.AddComponent<NotificationToast>();
            SetField(toast, "title", title);
            SetField(toast, "message", message);
            return root.gameObject;
        }

        private static GameObject BuildDialogueEntry()
        {
            RectTransform root = Rect("UI_DialogueEntry", null);
            VLayout(root, 4, new RectOffset(0, 8, 0, 14));
            Layout(root, -1, -1, 1);

            TextMeshProUGUI speaker = Text(root, "Speaker", "NAME", Theme.labelSize, Theme.accent, TextAlignmentOptions.TopLeft, FontStyles.Normal, Theme.labelSpacing, false);
            TextMeshProUGUI body = Text(root, "Body", "Line of dialogue.", 20, Theme.textPrimary);
            body.lineSpacing = 6f;

            var entry = root.gameObject.AddComponent<DialogueEntryView>();
            SetField(entry, "speaker", speaker);
            SetField(entry, "body", body);
            SetColor(entry, "suspectNameColor", Theme.accent);
            SetColor(entry, "investigatorNameColor", Theme.textSecondary);
            SetColor(entry, "primaryText", Theme.textPrimary);
            SetColor(entry, "mutedText", Theme.textMuted);
            return root.gameObject;
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Save(GameObject root, string name)
        {
            string path = $"{Folder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[UIBuilder] {target.GetType().Name} has no field '{field}'"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[UIBuilder] {target.GetType().Name} has no field '{field}'"); return; }
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
