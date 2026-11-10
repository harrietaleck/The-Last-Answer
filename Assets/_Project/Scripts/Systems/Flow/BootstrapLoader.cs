using UnityEngine;
using UnityEngine.SceneManagement;

namespace TLA.Systems.Flow
{
    /// <summary>
    /// Makes sure the Bootstrap scene (GameManager + services) is ALWAYS loaded, even when you
    /// press Play while a different scene is open in the Editor (e.g. testing the Sandbox scene).
    ///
    /// In a real build Bootstrap is scene 0 and is already loaded, so this does nothing there.
    /// </summary>
    internal static class BootstrapLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBootstrapLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == SceneLoader.BootstrapSceneName)
                    return; // already there
            }

            if (!Application.CanStreamedLevelBeLoaded(SceneLoader.BootstrapSceneName))
            {
                Debug.LogWarning($"[Bootstrap] '{SceneLoader.BootstrapSceneName}' scene is not in the Build Profile " +
                                 "scene list, so game systems won't start. Add it as the first scene.");
                return;
            }

            // Additive: keep the scene you were testing, add Bootstrap next to it.
            // The GameManager then "adopts" your scene instead of loading the main menu.
            SceneManager.LoadScene(SceneLoader.BootstrapSceneName, LoadSceneMode.Additive);
        }
    }
}
