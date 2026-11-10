using System.Collections;
using NUnit.Framework;
using TLA.Core.Services;
using TLA.Gameplay.Cinematics;
using TLA.Gameplay.Interaction;
using TLA.Gameplay.World;
using TLA.Systems;
using TLA.Systems.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    /// <summary>
    /// Walks through the real location scenes: station → evidence room → station → interrogation,
    /// checking the player arrives at the right spawn each time, then sits down at the interview table.
    /// </summary>
    public class LocationFlowTests
    {
        private const float LoadTimeout = 15f;

        [UnityTest]
        public IEnumerator Doors_MoveThePlayerBetweenLocations_AtTheRightSpawns()
        {
            yield return WaitForGameManager();
            GameManager game = Services.Get<GameManager>();

            // Load the hub ourselves (so the test runner's own scene is never unloaded), then hand it to the SceneLoader.
            yield return SceneManager.LoadSceneAsync("PoliceStation", LoadSceneMode.Additive);
            game.Scenes.AdoptScene(SceneManager.GetSceneByName("PoliceStation"));
            yield return null;
            AssertPlayerAtSpawn("Entrance");

            UseDoorTo("EvidenceRoom");
            yield return WaitForScene(game, "EvidenceRoom");
            AssertPlayerAtSpawn("FromHub");
            Assert.AreEqual(GameState.Investigating, game.State.Current);

            UseDoorTo("PoliceStation");
            yield return WaitForScene(game, "PoliceStation");
            AssertPlayerAtSpawn("FromEvidence");

            UseDoorTo("InterrogationRoom");
            yield return WaitForScene(game, "InterrogationRoom");
            AssertPlayerAtSpawn("FromHub");

            // Sit at the interview table: cinematic camera takes over, then stand up again.
            var seat = Object.FindFirstObjectByType<InterviewSeat>();
            var director = Object.FindFirstObjectByType<CinematicShotDirector>();
            var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
            var playerCamera = interactor.GetComponentInChildren<Camera>();

            seat.Interact(interactor);
            yield return null;
            Assert.IsTrue(seat.IsSeated);
            Assert.AreEqual(GameState.Interrogating, game.State.Current);
            Assert.IsTrue(director.IsActive);
            Assert.IsFalse(playerCamera.enabled, "first-person camera should be off while seated");
            Assert.GreaterOrEqual(director.Shots.Count, 5, "the room should offer several cinematic angles");

            string first = director.CurrentShot.ShotId;
            director.NextShot();
            Assert.AreNotEqual(first, director.CurrentShot.ShotId);

            seat.StandUp();
            yield return null;
            Assert.IsFalse(seat.IsSeated);
            Assert.AreEqual(GameState.Investigating, game.State.Current);
            Assert.IsTrue(playerCamera.enabled);

            // Leave the test with only Bootstrap + the runner scene loaded.
            yield return SceneManager.UnloadSceneAsync("InterrogationRoom");
        }

        private static void UseDoorTo(string targetScene)
        {
            var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
            foreach (SceneDoor door in Object.FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
            {
                if (door.TargetScene != targetScene) continue;
                Assert.IsTrue(door.CanInteract, $"door to {targetScene} should be usable");
                door.Interact(interactor);
                return;
            }
            Assert.Fail($"No door to {targetScene} in the current scene");
        }

        private static void AssertPlayerAtSpawn(string spawnId)
        {
            Transform player = Object.FindFirstObjectByType<PlayerInteractor>().transform;
            PlayerSpawnPoint spawn = null;
            foreach (PlayerSpawnPoint s in Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None))
                if (s.SpawnId == spawnId && s.gameObject.scene == player.gameObject.scene) spawn = s;

            Assert.IsNotNull(spawn, $"spawn '{spawnId}' not found");
            Vector3 a = player.position, b = spawn.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)), 0.1f, $"player should be at spawn '{spawnId}'");
            Assert.AreEqual(spawn.transform.eulerAngles.y, player.eulerAngles.y, 1f, $"player should face the spawn's direction");
        }

        private static IEnumerator WaitForGameManager()
        {
            float end = Time.realtimeSinceStartup + LoadTimeout;
            while (!Services.TryGet(out GameManager _) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(Services.TryGet(out GameManager _), "Bootstrap/GameManager didn't start");
        }

        private static IEnumerator WaitForScene(GameManager game, string sceneName)
        {
            float end = Time.realtimeSinceStartup + LoadTimeout;
            while ((game.Scenes.IsLoading || game.Scenes.CurrentSceneName != sceneName) && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(sceneName, game.Scenes.CurrentSceneName, $"'{sceneName}' didn't load");
            yield return null; // let the player's Start() place it at the spawn
            yield return null;
        }
    }
}
