using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// One-time project rendering setup:
    ///  - Linear colour space (correct, realistic lighting maths)
    ///  - a URP pipeline asset tuned for a dark, cinematic PC game (Forward+, soft shadows, HDR, MSAA)
    ///  - post-processing "looks" (Volume Profiles) for the station, the interrogation room and the menu
    ///  - converts any old Built-in "Standard" materials to URP Lit
    /// Safe to run again: it updates existing assets instead of duplicating them.
    /// </summary>
    internal static class RenderPipelineSetup
    {
        public const string SettingsFolder = "Assets/_Project/Settings/Rendering";
        public const string PipelinePath = SettingsFolder + "/URP_PC.asset";
        public const string RendererPath = SettingsFolder + "/URP_PC_Renderer.asset";
        public const string StationProfilePath = SettingsFolder + "/VP_Station.asset";
        public const string InterrogationProfilePath = SettingsFolder + "/VP_Interrogation.asset";
        public const string MenuProfilePath = SettingsFolder + "/VP_MainMenu.asset";

        [MenuItem("The Last Answer/Environment/Setup Render Pipeline (URP)", priority = 1)]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsFolder);

            PlayerSettings.colorSpace = ColorSpace.Linear;

            UniversalRenderPipelineAsset pipeline = CreateOrUpdatePipeline();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                // Quality levels may override the pipeline; make them all use ours.
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);

            CreateOrUpdateProfiles();
            ConvertStandardMaterials();

            AssetDatabase.SaveAssets();
            Debug.Log("[Environment] URP + Linear colour space set up.");
        }

        private static UniversalRenderPipelineAsset CreateOrUpdatePipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var rendererSo = new SerializedObject(renderer);
            SetInt(rendererSo, "m_RenderingMode", 2); // Forward+: many lights per object, ideal for interiors
            var postData = rendererSo.FindProperty("postProcessData");
            if (postData != null && postData.objectReferenceValue == null)
            {
                postData.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            }
            rendererSo.ApplyModifiedPropertiesWithoutUndo();

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var so = new SerializedObject(pipeline);
            SetBool(so, "m_SupportsHDR", true);
            SetInt(so, "m_MSAA", 4);
            SetFloat(so, "m_RenderScale", 1f);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1);      // per pixel
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetBool(so, "m_AdditionalLightShadowsSupported", true); // lamps cast shadows, not just the sun
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 4096); // room for a few shadowed lamps (a point light uses 6 maps)
            SetBool(so, "m_SoftShadowsSupported", true);
            SetFloat(so, "m_ShadowDistance", 30f);                 // interiors are small; short distance = sharper shadows
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetBool(so, "m_RequireDepthTexture", true);            // needed for depth of field
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        private static void CreateOrUpdateProfiles()
        {
            // Station: neutral, slightly cool, desaturated.
            BuildProfile(StationProfilePath, exposure: 0.15f, contrast: 12f, saturation: -18f, bloom: 0.35f,
                vignette: 0.28f, grain: 0.12f, depthOfField: false);

            // Interrogation: tighter vignette, harder contrast, more grain = pressure.
            BuildProfile(InterrogationProfilePath, exposure: 0.1f, contrast: 20f, saturation: -26f, bloom: 0.4f,
                vignette: 0.4f, grain: 0.2f, depthOfField: false);

            // Menu: dreamy background blur behind the title.
            BuildProfile(MenuProfilePath, exposure: 0.1f, contrast: 15f, saturation: -20f, bloom: 0.5f,
                vignette: 0.42f, grain: 0.15f, depthOfField: true);
        }

        private static void BuildProfile(string path, float exposure, float contrast, float saturation, float bloom,
            float vignette, float grain, bool depthOfField)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            // ACES tonemapping: filmic roll-off of bright highlights (lamps, red signs).
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            color.colorFilter.Override(new Color(0.96f, 0.98f, 1f));

            // Subtle thriller grade: cool shadows, warm highlights.
            var split = Get<SplitToning>(profile);
            split.shadows.Override(new Color(0.42f, 0.48f, 0.55f));
            split.highlights.Override(new Color(0.58f, 0.54f, 0.48f));
            split.balance.Override(-15f);

            var bloomFx = Get<Bloom>(profile);
            bloomFx.threshold.Override(1.0f);   // only emissive things brighter than white glow
            bloomFx.intensity.Override(bloom);
            bloomFx.scatter.Override(0.65f);

            var vig = Get<Vignette>(profile);
            vig.intensity.Override(vignette);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);

            var film = Get<FilmGrain>(profile);
            film.type.Override(FilmGrainLookup.Thin1);
            film.intensity.Override(grain);
            film.response.Override(0.8f);

            if (depthOfField)
            {
                var dof = Get<DepthOfField>(profile);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(1.6f);
                dof.gaussianEnd.Override(5f);
                dof.gaussianMaxRadius.Override(1f);
            }

            EditorUtility.SetDirty(profile);
        }

        /// <summary>Gets a Volume effect, adding it (as a sub-asset of the profile) if missing.</summary>
        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void ConvertStandardMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null || material.shader.name != "Standard") continue;

                Color color = material.HasProperty("_Color") ? material.color : Color.white;
                float gloss = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.3f;
                material.shader = lit;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", gloss);
                EditorUtility.SetDirty(material);
            }
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[Environment] URP setting '{name}' not found; skipped."); return; }
            p.intValue = value; // for enums this sets the underlying value (e.g. MSAA 4 = 4x), not the list index
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[Environment] URP setting '{name}' not found; skipped."); return; }
            p.floatValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[Environment] URP setting '{name}' not found; skipped."); return; }
            p.boolValue = value;
        }
    }
}
