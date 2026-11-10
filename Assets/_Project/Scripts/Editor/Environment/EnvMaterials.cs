using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// The placeholder material library: flat-colour URP Lit materials, no textures and nothing
    /// downloaded. All saved in Assets/_Project/Art/Materials/Environment/ so you can tweak them
    /// in the Inspector, or later swap in textured materials with the same names.
    ///
    /// Palette idea: desaturated cool greys and blues for walls, warm woods and paper for
    /// "human" objects, and RED used only for accents (signs, recording lights, evidence string).
    /// </summary>
    internal static class EnvMaterials
    {
        public const string Folder = "Assets/_Project/Art/Materials/Environment";
        private const string LitShader = "Universal Render Pipeline/Lit";

        // Surfaces
        public static Material ConcreteFloor => Get("M_ConcreteFloor", new Color(0.16f, 0.165f, 0.17f), 0.3f);
        public static Material Linoleum => Get("M_Linoleum", new Color(0.2f, 0.215f, 0.225f), 0.5f);
        public static Material WallPaint => Get("M_WallPaint", new Color(0.4f, 0.42f, 0.43f), 0.12f);
        public static Material WallLower => Get("M_WallLower", new Color(0.17f, 0.2f, 0.23f), 0.25f);
        public static Material InterrogationWall => Get("M_InterrogationWall", new Color(0.33f, 0.34f, 0.33f), 0.08f);
        public static Material AcousticPanel => Get("M_AcousticPanel", new Color(0.11f, 0.12f, 0.13f), 0.02f);
        public static Material Ceiling => Get("M_Ceiling", new Color(0.26f, 0.26f, 0.27f), 0.05f);
        public static Material Trim => Get("M_Trim", new Color(0.05f, 0.05f, 0.055f), 0.4f);

        // Furniture
        public static Material SteelTable => Get("M_SteelTable", new Color(0.42f, 0.43f, 0.45f), 0.55f, 0.85f);
        public static Material ChairPlastic => Get("M_ChairPlastic", new Color(0.08f, 0.08f, 0.09f), 0.35f);
        public static Material DarkWood => Get("M_DarkWood", new Color(0.24f, 0.15f, 0.09f), 0.4f);
        public static Material DeskLaminate => Get("M_DeskLaminate", new Color(0.32f, 0.3f, 0.27f), 0.3f);
        public static Material MetalCabinet => Get("M_MetalCabinet", new Color(0.28f, 0.31f, 0.3f), 0.45f, 0.6f);
        public static Material DoorSteel => Get("M_DoorSteel", new Color(0.2f, 0.23f, 0.26f), 0.35f, 0.3f);
        public static Material Cardboard => Get("M_Cardboard", new Color(0.5f, 0.38f, 0.25f), 0.05f);
        public static Material Fabric => Get("M_Fabric", new Color(0.13f, 0.14f, 0.16f), 0.05f);

        // Paperwork
        public static Material Cork => Get("M_Cork", new Color(0.5f, 0.36f, 0.22f), 0.03f);
        public static Material Paper => Get("M_Paper", new Color(0.82f, 0.81f, 0.77f), 0.1f);
        public static Material PaperAged => Get("M_PaperAged", new Color(0.74f, 0.69f, 0.58f), 0.08f);
        public static Material Manila => Get("M_ManilaFolder", new Color(0.74f, 0.62f, 0.38f), 0.12f);
        public static Material PhotoCool => Get("M_PhotoCool", new Color(0.2f, 0.23f, 0.27f), 0.7f);
        public static Material PhotoWarm => Get("M_PhotoWarm", new Color(0.38f, 0.32f, 0.26f), 0.7f);
        public static Material PhotoBorder => Get("M_PhotoBorder", new Color(0.86f, 0.86f, 0.84f), 0.4f);
        public static Material Whiteboard => Get("M_Whiteboard", new Color(0.78f, 0.8f, 0.8f), 0.75f);

        // Accents and lights (emission > 1 makes bloom pick them up)
        public static Material RedAccent => Get("M_RedAccent", new Color(0.5f, 0.03f, 0.03f), 0.4f, 0f, new Color(1f, 0.05f, 0.04f) * 2.5f);
        public static Material RedString => Get("M_RedString", new Color(0.55f, 0.04f, 0.04f), 0.3f, 0f, new Color(0.6f, 0.02f, 0.02f) * 0.6f);
        public static Material LightCool => Get("M_LightPanelCool", Color.white, 0.5f, 0f, new Color(0.85f, 0.92f, 1f) * 2.2f);
        public static Material LightWarm => Get("M_LightBulbWarm", Color.white, 0.5f, 0f, new Color(1f, 0.82f, 0.6f) * 4f);
        public static Material Screen => Get("M_ScreenGlow", new Color(0.02f, 0.03f, 0.05f), 0.9f, 0f, new Color(0.18f, 0.32f, 0.5f) * 1.1f);
        public static Material NightWindow => Get("M_NightWindow", new Color(0.01f, 0.015f, 0.03f), 0.9f, 0f, new Color(0.05f, 0.09f, 0.16f) * 0.9f);
        public static Material Blinds => Get("M_Blinds", new Color(0.55f, 0.55f, 0.53f), 0.3f);

        // Glass: the interrogation side of the one-way window is a dark mirror; the observation side is tinted glass.
        public static Material OneWayMirror => Get("M_OneWayMirror", new Color(0.015f, 0.017f, 0.02f), 0.96f, 1f);
        public static Material GlassTint => GetTransparent("M_GlassTint", new Color(0.12f, 0.14f, 0.16f, 0.22f), 0.95f);

        // Placeholder people
        public static Material Mannequin => Get("M_Mannequin", new Color(0.34f, 0.32f, 0.3f), 0.2f);
        public static Material Clothing => Get("M_Clothing", new Color(0.1f, 0.11f, 0.13f), 0.1f);

        public static Material Get(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetTransparent(string name, Color color, float smoothness)
        {
            Material material = Get(name, color, smoothness);
            material.SetFloat("_Surface", 1f);          // Transparent
            material.SetFloat("_Blend", 0f);            // Alpha blend
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            // No specular highlights: otherwise every nearby light shows up as a bright blob on the pane.
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Directory.CreateDirectory(Folder);
            Shader shader = Shader.Find(LitShader);
            if (shader == null) throw new System.InvalidOperationException("URP Lit shader not found. Is URP installed and active?");

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
