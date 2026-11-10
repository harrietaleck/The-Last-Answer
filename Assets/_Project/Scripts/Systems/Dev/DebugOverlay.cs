using TLA.Core.Services;
using TLA.Systems.Flow;
using UnityEngine;

namespace TLA.Systems.Dev
{
    /// <summary>
    /// Developer-only panel for testing the foundation systems before real UI exists.
    /// Press F1 in Play Mode to show/hide it. While walking around the mouse is locked, so press
    /// Esc (pause) to get the cursor back and click its buttons.
    ///
    /// It uses Unity's built-in immediate-mode GUI (OnGUI) on purpose: it needs no UI package,
    /// no prefabs and no setup. The code is compiled ONLY in the Editor and in Development
    /// builds, so it can never appear in a release build.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int WindowId = 7341;

        private bool _visible;   // starts hidden: the mouse is locked during play, press F1 to show
        private bool _allowed = true;
        private Rect _windowRect = new Rect(12, 12, 320, 10);
        private string _sceneToLoad = "Sandbox";
        private string _lastAction = "-";

        private void Start()
        {
            if (Services.TryGet(out GameManager game))
                _allowed = game.Config.Game.DebugOptions.ShowDebugOverlay;
        }

        private void OnGUI()
        {
            if (!_allowed) return;

            // IMGUI key handling works with both the old Input Manager and the new Input System.
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.F1)
            {
                _visible = !_visible;
                current.Use();
            }

            if (!_visible) return;
            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "The Last Answer - Dev (F1)");
        }

        private void DrawWindow(int id)
        {
            if (!Services.TryGet(out GameManager game))
            {
                GUILayout.Label("GameManager not running.");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label($"State: {game.State.Current}   (previous: {game.State.Previous})");
            GUILayout.Label($"Scene: {(string.IsNullOrEmpty(game.Scenes.CurrentSceneName) ? "-" : game.Scenes.CurrentSceneName)}" +
                            (game.Scenes.IsLoading ? "   (loading...)" : ""));
            GUILayout.Label($"AI mode: {game.Config.User.aiMode}   Volume: {game.Config.User.masterVolume:0.00}");
            GUILayout.Label($"Last action: {_lastAction}");

            GUILayout.Space(6);
            GUILayout.Label("Scenes");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Main Menu")) game.GoToMainMenu();
            if (GUILayout.Button("New Game")) game.StartNewGame();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _sceneToLoad = GUILayout.TextField(_sceneToLoad, GUILayout.Width(180));
            if (GUILayout.Button("Load")) game.LoadScene(_sceneToLoad);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Game state");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(game.State.IsPaused ? "Resume" : "Pause"))
                _lastAction = (game.State.IsPaused ? game.State.Resume() : game.State.Pause()) ? "pause toggled" : "pause refused";
            if (GUILayout.Button("Interrogate"))
                _lastAction = game.State.TryChangeState(GameState.Interrogating) ? "-> Interrogating" : "refused (see Console)";
            if (GUILayout.Button("Investigate"))
                _lastAction = game.State.TryChangeState(GameState.Investigating) ? "-> Investigating" : "refused (see Console)";
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Save slot 0");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) _lastAction = game.SaveGame(0) ? "saved slot 0" : "save failed";
            GUI.enabled = game.Saves.HasSave(0);
            if (GUILayout.Button("Load")) _lastAction = game.LoadGame(0) ? "loaded slot 0" : "load failed";
            if (GUILayout.Button("Delete")) { game.Saves.Delete(0); _lastAction = "deleted slot 0"; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Settings");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Volume 50%/100%"))
            {
                game.Config.User.masterVolume = game.Config.User.masterVolume > 0.75f ? 0.5f : 1f;
                game.Config.SaveUserSettings();
                _lastAction = "settings saved";
            }
            if (GUILayout.Button("Open save folder")) Application.OpenURL("file://" + Application.persistentDataPath);
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
#endif
    }
}
