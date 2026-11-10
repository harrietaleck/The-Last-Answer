using TLA.Gameplay.Environment;
using TLA.Systems.Flow;
using TLA.UI.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// MAIN MENU: a small, moody vignette behind the title. A detective's desk under one warm lamp,
    /// an out-of-focus evidence board with red string behind it, a night window, and a slow
    /// drifting camera. The menu UI sits on the left over a dark gradient panel.
    /// </summary>
    internal static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        public static void Build()
        {
            Blockout b = SceneKit.NewScene(ScenePath, out var scene);
            Transform env = b.Group("Set");

            SceneKit.Atmosphere(new Color(0.04f, 0.044f, 0.056f), new Color(0.01f, 0.012f, 0.016f), 0.06f);
            SceneKit.PostProcessing(RenderPipelineSetup.MenuProfilePath);
            SceneKit.Context(GameState.MainMenu);
            SceneKit.RoomTone(0.05f, 0f, 0f);

            // Room shell (only what the camera sees).
            b.Box("Floor", new Vector3(0f, -0.05f, 0.5f), new Vector3(7f, 0.1f, 6f), EnvMaterials.DarkWood, env);
            b.Box("BackWall", new Vector3(0f, 1.6f, 2.6f), new Vector3(7f, 3.2f, 0.2f), EnvMaterials.WallLower, env);
            b.Box("LeftWall", new Vector3(-2.2f, 1.6f, 0.5f), new Vector3(0.2f, 3.2f, 6f), EnvMaterials.WallLower, env);
            b.Quad("NightWindow", new Vector3(-2.09f, 1.7f, 1.2f), new Vector2(1.2f, 1.4f), Quaternion.Euler(0f, 90f, 0f), EnvMaterials.NightWindow, env);
            for (int i = 0; i < 12; i++)
                b.RotatedBox($"Blind_{i}", new Vector3(-2.06f, 1.05f + i * 0.115f, 1.2f), new Vector3(0.01f, 0.05f, 1.22f), new Vector3(0f, 0f, 25f), EnvMaterials.Blinds, env, false);

            // Desk with case file, photos, mug and a lamp.
            b.Table("Desk", new Vector3(0f, 0f, 0.35f), new Vector3(1.6f, 0.76f, 0.8f), EnvMaterials.DarkWood, EnvMaterials.Trim, env);
            b.Box("CaseFolder", new Vector3(-0.05f, 0.772f, 0.25f), new Vector3(0.32f, 0.012f, 0.24f), EnvMaterials.Manila, env, false);
            b.Box("CaseFolder_Paper", new Vector3(-0.03f, 0.779f, 0.24f), new Vector3(0.29f, 0.003f, 0.21f), EnvMaterials.Paper, env, false);
            var title = b.Label("CaseFolder_Label", new Vector3(-0.03f, 0.782f, 0.3f), Quaternion.Euler(90f, 0f, 0f), "CASE 0147", 0.25f, new Color(0.15f, 0.12f, 0.1f), env, 0.25f, 0.05f);
            title.fontStyle = FontStyles.Bold;
            Photo(b, env, new Vector3(0.3f, 0.772f, 0.12f), 12f, EnvMaterials.PhotoCool);
            Photo(b, env, new Vector3(0.42f, 0.774f, 0.3f), -18f, EnvMaterials.PhotoWarm);
            b.Cylinder("Mug", new Vector3(-0.5f, 0.82f, 0.5f), 0.04f, 0.1f, EnvMaterials.Paper, env);
            DeskLamp(b, env, new Vector3(-0.55f, 0.76f, 0.55f), new Vector3(-0.05f, 0.76f, 0.25f), 1.1f);

            // Evidence board behind (soft focus from depth of field).
            b.Box("Board", new Vector3(0.3f, 1.75f, 2.47f), new Vector3(2.2f, 1.2f, 0.04f), EnvMaterials.Cork, env, false);
            Vector3[] pins =
            {
                new Vector3(-0.5f, 2.05f, 2.44f), new Vector3(0.25f, 2.1f, 2.44f), new Vector3(0.95f, 1.95f, 2.44f),
                new Vector3(-0.3f, 1.45f, 2.44f), new Vector3(0.6f, 1.4f, 2.44f)
            };
            for (int i = 0; i < pins.Length; i++)
            {
                b.Box($"BoardPhoto_{i}", pins[i] + new Vector3(0f, -0.1f, 0.01f), new Vector3(0.2f, 0.15f, 0.005f), i % 2 == 0 ? EnvMaterials.PhotoBorder : EnvMaterials.Paper, env, false);
                b.Box($"Pin_{i}", pins[i] + new Vector3(0f, 0f, -0.005f), new Vector3(0.02f, 0.02f, 0.02f), EnvMaterials.RedAccent, env, false);
            }
            b.Rod("String_0", pins[0], pins[1], 0.003f, EnvMaterials.RedString, env);
            b.Rod("String_1", pins[1], pins[2], 0.003f, EnvMaterials.RedString, env);
            b.Rod("String_2", pins[0], pins[3], 0.003f, EnvMaterials.RedString, env);
            b.Rod("String_3", pins[1], pins[4], 0.003f, EnvMaterials.RedString, env);

            // Lighting: warm lamp key, cold window fill, subtle red rim from behind.
            b.PointLight("Fill_Window", new Vector3(-1.6f, 1.8f, 1.2f), new Color(0.45f, 0.55f, 0.85f), 0.6f, 4f, false, env);
            b.PointLight("Accent_Red", new Vector3(1.6f, 0.7f, 2.0f), new Color(0.9f, 0.1f, 0.08f), 1.2f, 3.5f, false, env);
            b.SpotLight("Board_Spot", new Vector3(0.3f, 3.0f, 1.6f), new Vector3(60f, 0f, 0f), new Color(1f, 0.92f, 0.8f), 1.4f, 3.5f, 60f, false, env);

            // Camera with slow drift.
            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0.35f, 1.22f, -1.05f);
            // Aim left of the desk so the set sits in the right of the frame, leaving the left for the menu UI.
            SceneKit.LookAt(camGo.transform, new Vector3(-0.75f, 0.95f, 0.45f));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraDrift>();
            SceneKit.ConfigureCamera(cam);

            BuildUI();
            SceneKit.Save(scene);
        }

        private static void BuildUI()
        {
            var canvasGo = new GameObject("MainMenuUI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dark panel on the left for readability.
            var panel = UI("Panel", canvasGo.transform, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.55f);
            panelImage.raycastTarget = false;

            Text(canvasGo.transform, "Title", "THE LAST ANSWER", 76f, new Vector2(140f, 190f), new Vector2(1000f, 110f), new Color(0.93f, 0.93f, 0.94f), 8f, FontStyles.Bold);
            var line = UI("TitleRule", canvasGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 128f), new Vector2(120f, 4f), new Vector2(0f, 0.5f));
            var lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = new Color(0.85f, 0.08f, 0.06f);
            lineImage.raycastTarget = false;
            Text(canvasGo.transform, "Tagline", "Every case ends with one question.", 26f, new Vector2(140f, 92f), new Vector2(900f, 40f), new Color(0.68f, 0.7f, 0.73f), 1f, FontStyles.Italic);

            Button newGame = MenuButton(canvasGo.transform, "Button_NewGame", "NEW GAME", -10f);
            Button cont = MenuButton(canvasGo.transform, "Button_Continue", "CONTINUE", -80f);
            Button quit = MenuButton(canvasGo.transform, "Button_Quit", "QUIT", -150f);

            var version = Text(canvasGo.transform, "Version", "v0.1", 16f, new Vector2(140f, -470f), new Vector2(600f, 30f), new Color(0.45f, 0.47f, 0.5f), 2f, FontStyles.Normal);

            var controller = canvasGo.AddComponent<MainMenuController>();
            SceneKit.Set(controller, "newGameButton", newGame);
            SceneKit.Set(controller, "continueButton", cont);
            SceneKit.Set(controller, "quitButton", quit);
            SceneKit.Set(controller, "versionLabel", version);

            // Event system so the buttons respond to mouse/keyboard (Input System UI module).
            var es = new GameObject("EventSystem");
            var eventSystem = es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            eventSystem.firstSelectedGameObject = newGame.gameObject;
        }

        private static Button MenuButton(Transform parent, string name, string label, float y)
        {
            RectTransform rt = UI(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(128f, y), new Vector2(360f, 56f), new Vector2(0f, 0.5f));
            var image = rt.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rt.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(0.85f, 0.1f, 0.08f, 0.25f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = Text(rt, "Label", label, 30f, new Vector2(12f, 0f), new Vector2(340f, 56f), new Color(0.9f, 0.9f, 0.92f), 5f, FontStyles.Normal);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return button;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box,
            Color color, float spacing, FontStyles style)
        {
            RectTransform rt = UI(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pos, box, new Vector2(0f, 0.5f));
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.characterSpacing = spacing;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform UI(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Photo(Blockout b, Transform parent, Vector3 pos, float yaw, Material image)
        {
            b.RotatedBox("Photo_Border", pos, new Vector3(0.11f, 0.004f, 0.15f), new Vector3(0f, yaw, 0f), EnvMaterials.PhotoBorder, parent, false);
            b.RotatedBox("Photo_Image", pos + Vector3.up * 0.0025f, new Vector3(0.095f, 0.002f, 0.12f), new Vector3(0f, yaw, 0f), image, parent, false);
        }

        /// <summary>Desk lamp at <paramref name="basePos"/> aiming a warm spot at <paramref name="target"/>.</summary>
        public static void DeskLamp(Blockout b, Transform parent, Vector3 basePos, Vector3 target, float intensity)
        {
            Transform lamp = b.Group("DeskLamp", parent);
            b.Cylinder("Base", basePos + Vector3.up * 0.01f, 0.08f, 0.02f, EnvMaterials.Trim, lamp);
            Vector3 head = basePos + new Vector3(0f, 0.42f, 0f) + (target - basePos).normalized * 0.12f;
            b.Rod("Arm", basePos + Vector3.up * 0.02f, head, 0.01f, EnvMaterials.Trim, lamp);
            GameObject shade = b.Cylinder("Shade", head, 0.075f, 0.12f, EnvMaterials.Trim, lamp);
            shade.transform.rotation = Quaternion.FromToRotation(Vector3.up, -(target - head).normalized);
            GameObject bulb = b.Cylinder("Bulb", head + (target - head).normalized * 0.03f, 0.05f, 0.01f, EnvMaterials.LightWarm, lamp);
            bulb.transform.rotation = shade.transform.rotation;

            Light spot = b.SpotLight("Light", head + (target - head).normalized * 0.07f, Vector3.zero, new Color(1f, 0.82f, 0.6f), intensity, 3f, 90f, true, lamp);
            SceneKit.LookAt(spot.transform, target);
        }
    }
}
