using TLA.Gameplay.Environment;
using TLA.Gameplay.Interaction;
using TLA.Gameplay.World;
using TLA.Systems.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TLA.EditorTools.Environment
{
    /// <summary>Shared building blocks for every location scene (atmosphere, player, spawns, doors, audio).</summary>
    internal static class SceneKit
    {
        public const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        /// <summary>Creates an empty scene, saves it at <paramref name="path"/>, and returns a Blockout rooted at "Environment".</summary>
        public static Blockout NewScene(string path, out Scene scene)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            return new Blockout(new GameObject("Environment").transform);
        }

        public static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Interior atmosphere: no sky, a dark flat ambient (the "floor" brightness of shadows) and
        /// light fog for depth. Ambient + fog are saved per scene in Lighting settings.
        /// </summary>
        public static void Atmosphere(Color ambient, Color fog, float fogDensity)
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.6f;
        }

        /// <summary>Global post-processing volume using one of the shared profiles.</summary>
        public static void PostProcessing(string profilePath)
        {
            var go = new GameObject("PostProcessing (Global Volume)");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        }

        public static void Context(GameState state)
        {
            var go = new GameObject("SceneContext");
            var context = go.AddComponent<SceneContext>();
            Set(context, "stateOnLoad", (int)state);
        }

        public static void RoomTone(float volume, float humFrequency, float humLevel)
        {
            var go = new GameObject("Audio_RoomTone");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            var tone = go.AddComponent<ProceduralRoomTone>();
            Set(tone, "volume", volume);
            Set(tone, "humFrequency", humFrequency);
            Set(tone, "humLevel", humLevel);
        }

        public static GameObject Player(Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return player;
        }

        public static PlayerSpawnPoint Spawn(Transform parent, string id, Vector3 position, float yaw, bool isDefault)
        {
            var go = new GameObject("Spawn_" + id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var spawn = go.AddComponent<PlayerSpawnPoint>();
            Set(spawn, "spawnId", id);
            Set(spawn, "isDefault", isDefault);
            return spawn;
        }

        /// <summary>
        /// A door that loads another scene. <paramref name="inward"/> points INTO the room the
        /// player is standing in (the sign is placed on that side).
        /// </summary>
        public static SceneDoor SceneDoorAt(Blockout b, string name, Vector3 floorCenter, Vector3 inward, string prompt,
            string targetScene, string targetSpawn, string sign, Transform parent)
        {
            Transform group = b.Group(name, parent);
            group.position = floorCenter;
            group.rotation = Quaternion.LookRotation(inward);

            GameObject panel = b.Box("Panel", new Vector3(0f, 1.06f, 0f), new Vector3(0.96f, 2.12f, 0.06f), EnvMaterials.DoorSteel, group);
            b.Box("Handle", new Vector3(0.36f, 1.0f, 0.05f), new Vector3(0.12f, 0.025f, 0.04f), EnvMaterials.Trim, group, false);
            b.Box("Window", new Vector3(0f, 1.55f, 0.031f), new Vector3(0.22f, 0.4f, 0.005f), EnvMaterials.NightWindow, group, false);
            DoorFrame(b, group, 1.0f, 2.15f);

            var door = panel.AddComponent<SceneDoor>();
            Set(door, "prompt", prompt);
            Set(door, "targetScene", targetScene);
            Set(door, "targetSpawnId", targetSpawn);

            if (!string.IsNullOrEmpty(sign))
            {
                // Text faces the player standing on the inward side (TMP is readable looking along its +Z).
                // The door sits in the middle of a 0.2 m wall, so the sign goes just in front of the wall face (z > 0.1).
                b.Box("SignPlate", new Vector3(0f, 2.42f, 0.115f), new Vector3(1.1f, 0.2f, 0.02f), EnvMaterials.Trim, group, false);
                var label = b.Label("SignText", new Vector3(0f, 2.42f, 0.13f), Quaternion.Euler(0f, 180f, 0f), sign, 1.1f,
                    new Color(0.85f, 0.86f, 0.88f), group, 1.05f, 0.2f);
                label.characterSpacing = 6f;
            }
            return door;
        }

        /// <summary>Normal hinged door (stays in the same scene). Inward = direction the closed door faces.</summary>
        public static DoorInteractable HingedDoor(Blockout b, string name, Vector3 openingFloorCenter, Vector3 inward,
            float width, Transform parent, bool locked = false)
        {
            Quaternion rotation = Quaternion.LookRotation(inward);
            Vector3 right = rotation * Vector3.right;

            Transform frameGroup = b.Group(name + "_Frame", parent);
            frameGroup.SetPositionAndRotation(openingFloorCenter, rotation);
            DoorFrame(b, frameGroup, width, 2.15f);

            Transform hinge = b.Group(name, parent);
            hinge.SetPositionAndRotation(openingFloorCenter - right * (width * 0.5f - 0.02f), rotation);
            b.Box("Panel", new Vector3(width * 0.5f - 0.02f, 1.06f, 0f), new Vector3(width - 0.06f, 2.12f, 0.05f), EnvMaterials.DoorSteel, hinge, true, false);
            b.Box("Handle", new Vector3(width - 0.17f, 1.0f, 0.045f), new Vector3(0.12f, 0.025f, 0.04f), EnvMaterials.Trim, hinge, false, false);
            b.Box("Handle_Back", new Vector3(width - 0.17f, 1.0f, -0.045f), new Vector3(0.12f, 0.025f, 0.04f), EnvMaterials.Trim, hinge, false, false);

            var door = hinge.gameObject.AddComponent<DoorInteractable>();
            if (locked) Set(door, "locked", true);
            return door;
        }

        private static void DoorFrame(Blockout b, Transform group, float width, float height)
        {
            float t = 0.06f;
            b.Box("Frame_L", new Vector3(-width * 0.5f - t * 0.5f, height * 0.5f, 0f), new Vector3(t, height, 0.24f), EnvMaterials.Trim, group, false);
            b.Box("Frame_R", new Vector3(width * 0.5f + t * 0.5f, height * 0.5f, 0f), new Vector3(t, height, 0.24f), EnvMaterials.Trim, group, false);
            b.Box("Frame_T", new Vector3(0f, height + t * 0.5f, 0f), new Vector3(width + t * 2f, t, 0.24f), EnvMaterials.Trim, group, false);
        }

        /// <summary>Enables post-processing and anti-aliasing on a camera.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        /// <summary>Points a transform at a world position.</summary>
        public static void LookAt(Transform t, Vector3 target)
        {
            t.rotation = Quaternion.LookRotation(target - t.position, Vector3.up);
        }

        /// <summary>Sets a serialized (possibly private) field by name.</summary>
        public static void Set(Object target, string property, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[Environment] {target.GetType().Name} has no serialized field '{property}'.");
                return;
            }

            switch (value)
            {
                case string s: p.stringValue = s; break;
                case bool v: p.boolValue = v; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i;
                    break;
                case float f: p.floatValue = f; break;
                case Object o: p.objectReferenceValue = o; break;
                default: Debug.LogError($"[Environment] Unsupported value type for '{property}'."); break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Appends an object to a serialized list field.</summary>
        public static void AddToList(Object target, string listProperty, Object item)
        {
            var so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(listProperty);
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
