using TMPro;
using UnityEngine;

namespace TLA.UI.Theme
{
    /// <summary>
    /// The look of the whole interface in one asset (Data/Settings/UITheme.asset): colours, font
    /// and text sizes. The UI builder reads it when generating screens, and runtime components
    /// (dialogue lines, notifications) read it when they style themselves.
    ///
    /// Direction: charcoal and black backgrounds, white type, restrained red accents.
    /// RULE: red marks attention only: the selected item, new evidence, the suspect's name.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "The Last Answer/UI/UI Theme", order = 30)]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Backgrounds")]
        public Color background = new Color32(0x0B, 0x0C, 0x0E, 0xFF);   // near black
        public Color panel = new Color32(0x14, 0x15, 0x18, 0xF2);        // charcoal
        public Color panelRaised = new Color32(0x1D, 0x1F, 0x23, 0xFF);  // inputs, list rows
        public Color scrim = new Color(0f, 0f, 0f, 0.72f);              // dims the game behind menus
        public Color divider = new Color32(0x2C, 0x2F, 0x34, 0xFF);

        [Header("Text")]
        public Color textPrimary = new Color32(0xEE, 0xEE, 0xEF, 0xFF);
        public Color textSecondary = new Color32(0x9A, 0x9F, 0xA6, 0xFF);
        public Color textMuted = new Color32(0x5C, 0x61, 0x68, 0xFF);

        [Header("Accent (use sparingly)")]
        public Color accent = new Color32(0xC4, 0x1E, 0x1A, 0xFF);       // investigative red
        public Color accentDim = new Color32(0x5A, 0x12, 0x10, 0xFF);

        [Header("Typography")]
        public TMP_FontAsset font;
        public float titleSize = 72f;
        public float headingSize = 34f;
        public float subheadingSize = 22f;
        public float bodySize = 22f;
        public float labelSize = 15f;       // small caps labels ("CURRENT OBJECTIVE")
        public float labelSpacing = 6f;     // letter spacing for labels/buttons

        [Header("Motion")]
        [Tooltip("Seconds for screens to fade in/out. Keep short: the direction asks for minimal animation.")]
        public float fadeDuration = 0.14f;
    }
}
