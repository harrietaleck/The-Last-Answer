using TLA.Core.Services;
using TLA.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// Main menu buttons. Only calls the GameManager. The menu itself has no game logic.
    ///   New Game → GameConfig's New Game scene (the police station hub)
    ///   Continue → loads save slot 0 (greyed out when there's no save)
    ///   Quit     → closes the game (stops Play Mode in the Editor)
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const int ContinueSlot = 0;

        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button quitButton;

        [Tooltip("Optional label showing the build version.")]
        [SerializeField] private TMP_Text versionLabel;

        private void OnEnable()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        }

        private void OnDisable()
        {
            if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
        }

        private void Start()
        {
            if (versionLabel != null) versionLabel.text = "v" + Application.version + "  ·  prototype";
            RefreshContinue();
        }

        private void RefreshContinue()
        {
            if (continueButton == null) return;
            bool hasSave = Services.TryGet(out GameManager game) && game.Saves.HasSave(ContinueSlot);
            continueButton.interactable = hasSave;

            // Dim the label too, so a disabled Continue clearly reads as unavailable.
            var label = continueButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.alpha = hasSave ? 1f : 0.3f;
        }

        private void OnNewGame()
        {
            if (Services.TryGet(out GameManager game)) game.StartNewGame();
        }

        private void OnContinue()
        {
            if (Services.TryGet(out GameManager game)) game.LoadGame(ContinueSlot);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
