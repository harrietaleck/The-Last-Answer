using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TLA.Gameplay.Player;
using TLA.Gameplay.World;
using TLA.Systems;
using TLA.Systems.Flow;
using TLA.UI.Transitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TLA.Tests
{
    /// <summary>
    /// Checks every scene in the Build Profile for wiring mistakes that would only show up at
    /// runtime: missing SceneContext, missing/duplicate players or spawns, and scene doors that
    /// point at a scene or spawn point that doesn't exist. Run these after editing any location.
    /// </summary>
    public class SceneValidationTests
    {
        private const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";

        private SceneSetup[] _originalSetup;

        [OneTimeSetUp]
        public void RememberOpenScenes() => _originalSetup = EditorSceneManager.GetSceneManagerSetup();

        [OneTimeTearDown]
        public void RestoreOpenScenes()
        {
            if (_originalSetup != null && _originalSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_originalSetup);
        }

        private static IEnumerable<string> ContentScenePaths()
        {
            return EditorBuildSettings.scenes.Where(s => s.enabled && s.path != BootstrapPath).Select(s => s.path);
        }

        [Test]
        public void BuildProfile_StartsWithBootstrap_AndContainsAllLocations()
        {
            string[] paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            Assert.AreEqual("Bootstrap", paths[0], "Bootstrap must be the first scene");
            CollectionAssert.IsSubsetOf(new[] { "MainMenu", "PoliceStation", "InterrogationRoom", "EvidenceRoom" }, paths);
        }

        [Test]
        public void Bootstrap_HasGameManagerAndScreenFader()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Additive);
            try
            {
                Assert.AreEqual(1, Find<GameManager>(scene).Count, "exactly one GameManager");
                Assert.AreEqual(1, Find<ScreenFader>(scene).Count, "exactly one ScreenFader");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCaseSource(nameof(ContentScenePaths))]
        public void ContentScene_IsWiredCorrectly(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                List<SceneContext> contexts = Find<SceneContext>(scene);
                Assert.AreEqual(1, contexts.Count, $"{scene.name}: needs exactly one SceneContext");
                Assert.LessOrEqual(Find<AudioListener>(scene).Count(l => l.enabled), 1, $"{scene.name}: more than one AudioListener");
                Assert.IsTrue(Find<Volume>(scene).Any(v => v.isGlobal && v.sharedProfile != null), $"{scene.name}: needs a global post-processing Volume");

                if (contexts[0].StateOnLoad == GameState.MainMenu) return; // menus have no player

                Assert.AreEqual(1, Find<FirstPersonController>(scene).Count, $"{scene.name}: needs exactly one Player");
                List<PlayerSpawnPoint> spawns = Find<PlayerSpawnPoint>(scene);
                Assert.AreEqual(1, spawns.Count(s => s.IsDefault), $"{scene.name}: needs exactly one default spawn point");
                Assert.AreEqual(spawns.Count, spawns.Select(s => s.SpawnId).Distinct().Count(), $"{scene.name}: spawn ids must be unique");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EverySceneDoor_PointsAtARealSceneAndSpawn()
        {
            // First collect every scene's spawn ids, then check each door against them.
            var spawnsByScene = new Dictionary<string, HashSet<string>>();
            var doors = new List<(string scene, string name, string target, string spawn)>();

            foreach (string path in ContentScenePaths())
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    spawnsByScene[scene.name] = new HashSet<string>(Find<PlayerSpawnPoint>(scene).Select(s => s.SpawnId));
                    foreach (SceneDoor door in Find<SceneDoor>(scene))
                        doors.Add((scene.name, door.name, door.TargetScene, door.TargetSpawnId));
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Assert.IsNotEmpty(doors, "no scene doors found");
            foreach (var door in doors)
            {
                Assert.IsTrue(spawnsByScene.ContainsKey(door.target),
                    $"{door.scene}/{door.name}: target scene '{door.target}' is not in the Build Profile");
                if (!string.IsNullOrEmpty(door.spawn))
                    Assert.IsTrue(spawnsByScene[door.target].Contains(door.spawn),
                        $"{door.scene}/{door.name}: spawn '{door.spawn}' doesn't exist in '{door.target}'");
            }
        }

        private static List<T> Find<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));
            return results;
        }
    }
}
