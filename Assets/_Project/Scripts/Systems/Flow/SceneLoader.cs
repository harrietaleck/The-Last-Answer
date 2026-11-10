using System;
using TLA.Core.Events;
using TLA.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TLA.Systems.Flow
{
    /// <summary>
    /// Loads and unloads CONTENT scenes on top of the always-loaded Bootstrap scene.
    ///
    /// HOW IT WORKS (additive loading):
    ///   Bootstrap (stays loaded: GameManager + services)
    ///     + one content scene at a time (MainMenu, HQ, a crime scene, Interrogation...)
    /// Loading a new content scene unloads the previous one first. Because the Bootstrap scene
    /// is never unloaded, the services in it survive every scene change. No DontDestroyOnLoad needed.
    ///
    /// Loading is asynchronous (the game keeps running while the scene streams in) and only one
    /// load can run at a time.
    /// </summary>
    public sealed class SceneLoader : IGameService
    {
        /// <summary>The scene that holds the GameManager. It is never unloaded.</summary>
        public const string BootstrapSceneName = "Bootstrap";

        /// <summary>True while a scene change is in progress.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>The content scene currently loaded (empty if only Bootstrap is loaded).</summary>
        public string CurrentSceneName { get; private set; } = string.Empty;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// Finds a content scene that is already loaded, which happens when you press Play in, say,
        /// the Sandbox scene in the Editor (Bootstrap is then added automatically, see BootstrapLoader).
        /// That scene becomes the current scene instead of loading the main menu.
        /// </summary>
        public bool TryAdoptLoadedScene(out Scene adoptedScene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name != BootstrapSceneName)
                {
                    CurrentSceneName = scene.name;
                    SceneManager.SetActiveScene(scene);
                    adoptedScene = scene;
                    return true;
                }
            }

            adoptedScene = default;
            return false;
        }

        /// <summary>
        /// Treats an already-loaded scene as the current content scene (the next load will unload it).
        /// For custom loading flows and automated tests that load a scene themselves.
        /// </summary>
        public void AdoptScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name == BootstrapSceneName) return;
            CurrentSceneName = scene.name;
            SceneManager.SetActiveScene(scene);
        }

        /// <summary>
        /// Unloads the current content scene (if any) and loads <paramref name="sceneName"/>.
        /// Returns true on success. Errors are logged rather than thrown, so callers can
        /// fire-and-forget: <c>_ = sceneLoader.LoadSceneAsync("HQ");</c>
        /// </summary>
        public async Awaitable<bool> LoadSceneAsync(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Ignoring request to load '{sceneName}': another scene is still loading.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName) || sceneName == BootstrapSceneName)
            {
                Debug.LogError($"[SceneLoader] '{sceneName}' is not a valid content scene name.");
                return false;
            }

            // A scene can only be loaded by name if it is in the Build Profile's scene list.
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' can't be loaded. " +
                               "Check the name and that it's in File > Build Profiles > Scene List.");
                return false;
            }

            IsLoading = true;
            EventBus.Publish(new SceneLoadStartedEvent(sceneName));

            try
            {
                if (!string.IsNullOrEmpty(CurrentSceneName))
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(CurrentSceneName);
                    if (unload != null) await unload;
                    CurrentSceneName = string.Empty;
                }

                AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (load == null)
                {
                    Debug.LogError($"[SceneLoader] Unity refused to load '{sceneName}'.");
                    return false;
                }

                await load;

                // The ACTIVE scene is where newly spawned objects go and whose lighting settings
                // are used, so it must be the content scene, not Bootstrap.
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
                CurrentSceneName = sceneName;

                EventBus.Publish(new SceneLoadedEvent(sceneName));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
