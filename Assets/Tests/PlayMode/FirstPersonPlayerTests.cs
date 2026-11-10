#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TLA.Core.Services;
using TLA.Gameplay.Interaction;
using TLA.Gameplay.Player;
using TLA.Systems.Flow;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    /// <summary>
    /// Drives the real Player prefab with SIMULATED keyboard/mouse input (InputTestFixture),
    /// so movement, crouching, looking and interaction are verified without anyone at the keys.
    /// Each test builds a tiny world: a floor, the player, and whatever the test needs.
    /// </summary>
    public class FirstPersonPlayerTests : InputTestFixture
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        private Keyboard _keyboard;
        private Mouse _mouse;
        private GameObject _floor;
        private GameObject _player;
        private FirstPersonController _controller;
        private PlayerInteractor _interactor;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        /// <summary>
        /// Builds the test world. Each test calls this FIRST: Unity runs [UnitySetUp] before the
        /// InputTestFixture's [SetUp], which resets the Input System, so the player must be
        /// created inside the test itself.
        /// </summary>
        private IEnumerator SpawnWorld()
        {
            // Let the auto-loaded Bootstrap scene finish starting, then make sure we're "investigating".
            yield return null;
            if (Services.TryGet(out GameStateManager state)) state.ForceState(GameState.Investigating);

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "TestFloor";
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(40f, 1f, 40f);

            // The floor must exist in the physics world BEFORE the player appears, otherwise the
            // player falls into it on the first frame and gets pushed up onto a wrong height.
            Physics.SyncTransforms();
            yield return WaitForPhysics();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player prefab not found at " + PlayerPrefabPath);
            _player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            _controller = _player.GetComponent<FirstPersonController>();
            _interactor = _player.GetComponent<PlayerInteractor>();

            yield return WaitForPhysics(); // settle onto the floor

            Assert.AreEqual(0f, _player.transform.position.y, 0.1f, "player should be standing on the floor");

            var reader = _player.GetComponent<PlayerInputReader>();
            string stateName = Services.TryGet(out GameStateManager current) ? current.Current.ToString() : "no services";
            Assert.IsTrue(reader.GameplayInputEnabled,
                $"Gameplay input should be enabled (state: {stateName}, timeScale: {Time.timeScale}, reader enabled: {reader.enabled})");
        }

        [UnityTearDown]
        public IEnumerator DestroyWorld()
        {
            Object.Destroy(_player);
            Object.Destroy(_floor);
            foreach (var extra in Object.FindObjectsByType<TestMarker>(FindObjectsSortMode.None))
                Object.Destroy(extra.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingW_MovesPlayerForward()
        {
            yield return SpawnWorld();
            float startZ = _player.transform.position.z;

            Press(_keyboard.wKey);
            yield return WaitSeconds(0.5f);
            Release(_keyboard.wKey);

            Assert.Greater(_player.transform.position.z - startZ, 0.5f, "W should move the player forward");
            Assert.AreEqual(0f, _player.transform.position.x, 0.05f, "W shouldn't move sideways");
        }

        [UnityTest]
        public IEnumerator HoldingShift_RunsFasterThanWalking()
        {
            yield return SpawnWorld();
            Press(_keyboard.wKey);
            yield return WaitSeconds(0.5f);
            float walkSpeed = _controller.HorizontalSpeed;

            Press(_keyboard.leftShiftKey);
            yield return WaitSeconds(0.5f);
            float runSpeed = _controller.HorizontalSpeed;
            Release(_keyboard.leftShiftKey);
            Release(_keyboard.wKey);

            Assert.IsTrue(_controller.IsRunning || runSpeed > walkSpeed, "Shift should enable running");
            Assert.AreEqual(_controller.Settings.walkSpeed, walkSpeed, 0.1f);
            Assert.AreEqual(_controller.Settings.runSpeed, runSpeed, 0.1f);
        }

        [UnityTest]
        public IEnumerator ReleasingKeys_StopsSmoothly()
        {
            yield return SpawnWorld();
            Press(_keyboard.wKey);
            yield return WaitSeconds(0.5f);
            Release(_keyboard.wKey);
            yield return null;

            Assert.Greater(_controller.HorizontalSpeed, 0f, "should decelerate, not stop instantly");
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(0f, _controller.HorizontalSpeed, 0.01f, "should come to rest");
        }

        [UnityTest]
        public IEnumerator CrouchKey_TogglesCrouchAndLowersCamera()
        {
            yield return SpawnWorld();
            Transform pivot = _player.transform.Find("CameraPivot");
            float standingEye = pivot.localPosition.y;

            PressAndRelease(_keyboard.cKey);
            yield return WaitSeconds(0.4f);
            Assert.IsTrue(_controller.IsCrouching);
            Assert.Less(pivot.localPosition.y, standingEye - 0.5f, "camera should drop when crouching");

            PressAndRelease(_keyboard.cKey);
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(_controller.IsCrouching);
            Assert.AreEqual(standingEye, pivot.localPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator CannotStandUp_UnderLowCeiling()
        {
            yield return SpawnWorld();
            PressAndRelease(_keyboard.cKey);
            yield return WaitSeconds(0.4f);

            // Put a low ceiling (underside at 1.3 m) right above the crouched player.
            var ceiling = CreateBox("LowCeiling", new Vector3(0f, 1.5f, 0f), new Vector3(3f, 0.4f, 3f));
            yield return null;

            PressAndRelease(_keyboard.cKey); // try to stand
            yield return WaitSeconds(0.4f);
            Assert.IsTrue(_controller.IsCrouching, "player must stay crouched under the ceiling");

            Object.Destroy(ceiling);
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(_controller.IsCrouching, "player should stand once there's room");
        }

        [UnityTest]
        public IEnumerator MouseMovement_TurnsAndPitchesCamera()
        {
            yield return SpawnWorld();
            float sensitivity = _controller.Settings.lookSensitivity;

            Set(_mouse.delta, new Vector2(100f, 0f));
            yield return null;
            Set(_mouse.delta, Vector2.zero);
            yield return null;

            float yaw = Mathf.DeltaAngle(0f, _player.transform.eulerAngles.y);
            Assert.AreEqual(100f * sensitivity, yaw, 0.5f, "horizontal mouse should turn the body");

            Set(_mouse.delta, new Vector2(0f, 10000f)); // huge upward flick
            yield return null;
            Set(_mouse.delta, Vector2.zero);
            yield return null;

            float pitch = Mathf.DeltaAngle(0f, _player.transform.Find("CameraPivot").localEulerAngles.x);
            Assert.AreEqual(-_controller.Settings.maxLookAngle, pitch, 0.5f, "looking up must be clamped");
        }

        [UnityTest]
        public IEnumerator LookingAtInteractable_ShowsTarget_AndEInteracts()
        {
            yield return SpawnWorld();
            var target = CreateTestInteractable(new Vector3(0f, 1.6f, 1.5f));
            yield return WaitForPhysics();

            Assert.AreSame(target, _interactor.Current, "should focus the object in front of the camera. " + DescribeView());

            PressAndRelease(_keyboard.eKey);
            yield return WaitForPhysics();

            Assert.AreEqual(1, target.InteractionCount, "E should interact exactly once");
        }

        [UnityTest]
        public IEnumerator LookingDownAtTableObject_IsNotBlockedByOwnBody()
        {
            yield return SpawnWorld();
            // Regression test: looking down, the ray used to hit the player's own capsule first.
            var target = CreateTestInteractable(new Vector3(0f, 0.8f, 1.2f));
            _player.transform.Find("CameraPivot").localRotation = Quaternion.Euler(37f, 0f, 0f);
            yield return WaitForPhysics();

            Assert.AreSame(target, _interactor.Current, "should focus the object below eye level. " + DescribeView());
        }

        [UnityTest]
        public IEnumerator InteractableOutOfRange_IsIgnored()
        {
            yield return SpawnWorld();
            float beyond = _controller.Settings.interactionDistance + 1.5f;
            var target = CreateTestInteractable(new Vector3(0f, 1.6f, beyond));
            yield return WaitForPhysics();

            Assert.IsNull(_interactor.Current);
            PressAndRelease(_keyboard.eKey);
            yield return WaitForPhysics();
            Assert.AreEqual(0, target.InteractionCount);
        }

        [UnityTest]
        public IEnumerator WallBetween_BlocksInteraction()
        {
            yield return SpawnWorld();
            CreateBox("Wall", new Vector3(0f, 1.5f, 0.9f), new Vector3(3f, 3f, 0.2f));
            var target = CreateTestInteractable(new Vector3(0f, 1.6f, 1.6f));
            yield return WaitForPhysics();

            Assert.IsNull(_interactor.Current, "can't interact through a wall");
            Assert.AreEqual(0, target.InteractionCount);
        }

        [UnityTest]
        public IEnumerator Door_OpensAndCloses_WithPromptChange()
        {
            yield return SpawnWorld();
            // Door hinge 1.2 m in front, panel extends to the right (+X) and covers the view centre.
            var hinge = new GameObject("TestDoor");
            hinge.AddComponent<TestMarker>();
            hinge.transform.position = new Vector3(-0.5f, 0f, 1.2f);
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.transform.SetParent(hinge.transform, false);
            panel.transform.localPosition = new Vector3(0.5f, 1.1f, 0f);
            panel.transform.localScale = new Vector3(1f, 2.2f, 0.06f);
            var door = hinge.AddComponent<DoorInteractable>();
            yield return WaitForPhysics();

            Assert.AreSame(door, _interactor.Current);
            Assert.AreEqual("Open", door.Prompt);

            PressAndRelease(_keyboard.eKey);
            yield return WaitSeconds(0.8f);
            Assert.IsTrue(door.IsOpen);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, hinge.transform.rotation), 80f, "door should swing open");

            // The door must swing AWAY from the player (player is on the -Z side, so the panel goes to +Z).
            Assert.Greater(panel.transform.position.z, hinge.transform.position.z, "door should open away from the player");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Explains what the camera is looking at, for clearer failure messages.</summary>
        private string DescribeView()
        {
            Transform cam = _player.GetComponentInChildren<Camera>().transform;
            string hit = Physics.Raycast(cam.position, cam.forward, out RaycastHit info, 10f)
                ? $"{info.collider.name} at {info.distance:0.00} m"
                : "nothing";
            return $"Camera at {cam.position}, looking {cam.forward}, ray hits {hit}; " +
                   $"input enabled: {_player.GetComponent<PlayerInputReader>().GameplayInputEnabled}";
        }

        private TestInteractable CreateTestInteractable(Vector3 position)
        {
            var go = CreateBox("TestInteractable", position, new Vector3(0.4f, 0.4f, 0.4f));
            return go.AddComponent<TestInteractable>();
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.AddComponent<TestMarker>();
            Physics.SyncTransforms(); // make the new collider visible to raycasts immediately
            return go;
        }

        /// <summary>
        /// New colliders only become visible to raycasts after a physics step (every 0.02 s),
        /// so wait a little longer than that.
        /// </summary>
        private static IEnumerator WaitForPhysics()
        {
            yield return WaitSeconds(0.1f);
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        /// <summary>Tags objects created by a test so TearDown can remove them.</summary>
        private sealed class TestMarker : MonoBehaviour
        {
        }
    }
}
#endif
