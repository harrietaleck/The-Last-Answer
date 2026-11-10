using System.Collections.Generic;
using TLA.Data.Config;
using TLA.Gameplay.World;
using TLA.UI.Transitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// Builds (or rebuilds) every location of the game in one go:
    ///   Main Menu · Police Station hub · Interrogation Room · Evidence Room
    /// plus the supporting setup: URP, the Player prefab, the Bootstrap screen fader, the Build
    /// Profile scene list and GameConfig's New Game scene.
    ///
    /// Menu: The Last Answer → Environment → Rebuild All Locations.
    /// WARNING: rebuilding OVERWRITES those four scenes. Once you start decorating a scene by hand,
    /// stop using the rebuild for it (or copy the scene first). Treat this as a blockout generator.
    /// </summary>
    internal static class LocationBuilder
    {
        public const string LocationsFolder = "Assets/_Project/Scenes/Locations";
        private const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string SandboxPath = "Assets/_Project/Scenes/Sandbox/Sandbox.unity";
        private const string GameConfigPath = "Assets/_Project/Data/Settings/GameConfig.asset";

        [MenuItem("The Last Answer/Environment/Rebuild All Locations (overwrites scenes)", priority = 2)]
        private static void RebuildAllFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog("Rebuild all locations?",
                "This regenerates MainMenu, PoliceStation, InterrogationRoom and EvidenceRoom from code.\n\n" +
                "Any manual changes in those scenes will be LOST.", "Rebuild", "Cancel");
            if (confirmed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildAll();
        }

        /// <summary>Builds everything without asking (used by the menu after confirming, and by automation).</summary>
        public static void BuildAll()
        {
            UI.CaseDataBuilder.EnsureCaseData();
            RenderPipelineSetup.Run();
            GameObject gameUi = UI.UIScreensBuilder.BuildPrefabs();
            UpdatePlayerPrefab();

            MainMenuBuilder.Build();
            PoliceStationBuilder.Build();
            InterrogationRoomBuilder.Build();
            EvidenceRoomBuilder.Build();

            UpdateBootstrap(gameUi);
            UpdateBuildSettings();
            UpdateGameConfig();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(PoliceStationBuilder.ScenePath);
            Debug.Log("[Environment] All locations built.");
        }

        [MenuItem("The Last Answer/UI/Rebuild UI (prefabs, main menu, Bootstrap UI)", priority = 20)]
        private static void RebuildUIFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog("Rebuild the UI?",
                "This regenerates the UI component prefabs, UI_GameRoot, the MainMenu scene and the Bootstrap UI.\n\n" +
                "Manual changes to those will be LOST. Location scenes are not touched.", "Rebuild", "Cancel");
            if (confirmed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildUI();
        }

        /// <summary>Rebuilds only the UI (no 3D locations except the main menu backdrop).</summary>
        public static void BuildUI()
        {
            UI.CaseDataBuilder.EnsureCaseData();
            GameObject gameUi = UI.UIScreensBuilder.BuildPrefabs();
            UpdatePlayerPrefab();
            MainMenuBuilder.Build();
            UpdateBootstrap(gameUi);
            UpdateGameConfig();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(BootstrapPath);
            Debug.Log("[UI] UI rebuilt.");
        }

        /// <summary>Player prefab: spawn-point placement + post-processing on its camera.</summary>
        private static void UpdatePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SceneKit.PlayerPrefabPath);
            try
            {
                if (root.GetComponent<PlayerSpawnPlacement>() == null) root.AddComponent<PlayerSpawnPlacement>();

                // The HUD is now global (UI_GameRoot in Bootstrap), so the prefab's own HUD canvas goes.
                Transform oldHud = root.transform.Find("HUD");
                if (oldHud != null) Object.DestroyImmediate(oldHud.gameObject);
                Camera cam = root.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    SceneKit.ConfigureCamera(cam);
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                }
                PrefabUtility.SaveAsPrefabAsset(root, SceneKit.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Bootstrap (always loaded) gets everything shared by all scenes: the screen fader, the game UI
        /// (HUD, dialogue, pause, settings), the single EventSystem and the brightness control.
        /// </summary>
        private static void UpdateBootstrap(GameObject gameUiPrefab)
        {
            var scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);

            foreach (GameObject rootObject in scene.GetRootGameObjects())
                if (rootObject.name == "UI_GameRoot" || rootObject.name == "EventSystem" || rootObject.name == "DisplayBrightness")
                    Object.DestroyImmediate(rootObject); // replaced below with fresh copies

            PrefabUtility.InstantiatePrefab(gameUiPrefab, scene);

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            new GameObject("DisplayBrightness").AddComponent<TLA.Gameplay.Environment.DisplayBrightness>();

            if (Object.FindFirstObjectByType<ScreenFader>() == null)
            {
                var canvasGo = new GameObject("ScreenFader", typeof(RectTransform));
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // above every other UI
                canvasGo.AddComponent<CanvasGroup>();
                canvasGo.AddComponent<ScreenFader>();

                var black = new GameObject("Black", typeof(RectTransform));
                black.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)black.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var image = black.AddComponent<Image>();
                image.color = Color.black;
                image.raycastTarget = false;
            }
            SceneKit.Save(scene);
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapPath, true),   // must stay first
                new EditorBuildSettingsScene(MainMenuBuilder.ScenePath, true),
                new EditorBuildSettingsScene(PoliceStationBuilder.ScenePath, true),
                new EditorBuildSettingsScene(InterrogationRoomBuilder.ScenePath, true),
                new EditorBuildSettingsScene(EvidenceRoomBuilder.ScenePath, true),
                new EditorBuildSettingsScene(SandboxPath, true),      // dev test room
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void UpdateGameConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            var so = new SerializedObject(config);
            so.FindProperty("scenes.newGameScene").stringValue = PoliceStationBuilder.SceneName;
            so.FindProperty("caseCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TLA.Data.Cases.CaseCatalog>(UI.CaseDataBuilder.CatalogPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }
    }
}
