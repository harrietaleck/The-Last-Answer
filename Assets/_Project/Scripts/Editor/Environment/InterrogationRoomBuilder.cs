using TLA.Gameplay.Cinematics;
using TLA.Gameplay.Environment;
using TLA.Systems.Flow;
using UnityEngine;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// INTERROGATION ROOM. Layout (top view, north = up):
    ///
    ///   z=3 ┌───────────────────────┬─────────┐
    ///       │  INTERVIEW ROOM        ║ OBSERV- │   ║ = one-way window (mirror on the interview side)
    ///       │   suspect ●            ║ ATION   │
    ///       │   [ table ]  pendant   ║ (dark,  │
    ///       │   investigator ●       ║  red)   │
    ///   z=-2├──door─────────────────┴──door───┤
    ///       │ corridor        → station door  │
    ///   z=-4└─────────────────────────────────┘
    ///       x=-4                 x=1.5       x=4
    ///
    /// Six cinematic shots are placed for interviews (see CinematicShot markers under InterviewCameraRig).
    /// </summary>
    internal static class InterrogationRoomBuilder
    {
        public const string SceneName = "InterrogationRoom";
        public const string ScenePath = LocationBuilder.LocationsFolder + "/" + SceneName + ".unity";

        private const float H = 3.0f;
        private const float T = 0.2f;

        // Key positions (world space), shared by the furniture, markers and camera shots.
        private static readonly Vector3 TableCenter = new Vector3(-1.25f, 0f, 0.5f);
        private static readonly Vector3 InvestigatorSeat = new Vector3(-1.25f, 0f, -0.35f);
        private static readonly Vector3 SuspectSeat = new Vector3(-1.25f, 0f, 1.35f);
        private static readonly Vector3 SuspectHead = new Vector3(-1.25f, 1.28f, 1.38f);

        public static void Build()
        {
            Blockout b = SceneKit.NewScene(ScenePath, out var scene);
            SceneKit.Atmosphere(new Color(0.036f, 0.04f, 0.048f), new Color(0.008f, 0.01f, 0.012f), 0.03f);
            SceneKit.PostProcessing(RenderPipelineSetup.InterrogationProfilePath);
            SceneKit.Context(GameState.Investigating);
            SceneKit.RoomTone(0.09f, 50f, 0.45f);

            Transform shell = b.Group("Shell");
            b.Box("Floor", new Vector3(0f, -0.05f, -0.5f), new Vector3(8.4f, 0.1f, 7.4f), EnvMaterials.ConcreteFloor, shell);
            b.Box("Ceiling", new Vector3(0f, H + 0.05f, -0.5f), new Vector3(8.4f, 0.1f, 7.4f), EnvMaterials.Ceiling, shell);
            b.Wall("Wall_South", new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), H, T, EnvMaterials.InterrogationWall);
            b.Wall("Wall_North", new Vector3(-4f, 0f, 3f), new Vector3(4f, 0f, 3f), H, T, EnvMaterials.InterrogationWall);
            b.Wall("Wall_West", new Vector3(-4f, 0f, -4f), new Vector3(-4f, 0f, 3f), H, T, EnvMaterials.InterrogationWall,
                Blockout.Opening.Door(1f, 1.0f));
            b.Wall("Wall_East", new Vector3(4f, 0f, -4f), new Vector3(4f, 0f, 3f), H, T, EnvMaterials.InterrogationWall);
            b.Wall("Wall_Corridor", new Vector3(-4f, 0f, -2f), new Vector3(4f, 0f, -2f), H, T, EnvMaterials.InterrogationWall,
                Blockout.Opening.Door(1f, 1.0f), Blockout.Opening.Door(6.75f, 1.0f));
            b.Wall("Wall_Window", new Vector3(1.5f, 0f, -2f), new Vector3(1.5f, 0f, 3f), H, T, EnvMaterials.InterrogationWall,
                Blockout.Opening.Window(2.5f, 2.2f, 0.95f, 2.05f));

            BuildInterviewRoom(b);
            BuildOneWayWindow(b);
            BuildObservationRoom(b);
            BuildCorridor(b);
            CinematicShotDirector director = BuildCameraRig(b);
            BuildInterviewSeat(b, director);

            // Runs the interview: questions → rules → AI provider (mock offline, online later) → dialogue screen.
            new GameObject("DialogueManager").AddComponent<TLA.Gameplay.Interview.DialogueManager>();

            Transform spawns = b.Group("SpawnPoints");
            SceneKit.Spawn(spawns, "FromHub", new Vector3(-3.3f, 0f, -3f), 90f, true);
            SceneKit.Player(new Vector3(-3.3f, 0f, -3f), 90f);

            SceneKit.Save(scene);
        }

        private static void BuildInterviewRoom(Blockout b)
        {
            Transform room = b.Group("InterviewRoom");

            // Acoustic panels: dark squares that read instantly as "interview room".
            for (int i = 0; i < 3; i++)
                b.Box($"AcousticPanel_N{i}", new Vector3(-3.1f + i * 1.25f, 1.6f, 2.87f), new Vector3(1.0f, 1.2f, 0.05f), EnvMaterials.AcousticPanel, room, false);
            for (int i = 0; i < 2; i++)
                b.Box($"AcousticPanel_W{i}", new Vector3(-3.87f, 1.6f, -0.6f + i * 1.8f), new Vector3(0.05f, 1.2f, 1.0f), EnvMaterials.AcousticPanel, room, false);

            // Steel table with a restraint bar, and two chairs.
            b.Table("InterviewTable", TableCenter, new Vector3(1.6f, 0.76f, 0.9f), EnvMaterials.SteelTable, EnvMaterials.SteelTable, room);
            b.Box("RestraintBar", TableCenter + new Vector3(0f, 0.78f, 0.3f), new Vector3(0.3f, 0.03f, 0.03f), EnvMaterials.SteelTable, room, false);
            b.Box("Folder_OnTable", TableCenter + new Vector3(0.35f, 0.765f, -0.22f), new Vector3(0.24f, 0.012f, 0.32f), EnvMaterials.Manila, room, false);
            b.Cylinder("Cup_OnTable", TableCenter + new Vector3(-0.45f, 0.815f, 0.18f), 0.035f, 0.1f, EnvMaterials.Paper, room);
            b.Chair("Chair_Investigator", InvestigatorSeat, 0f, EnvMaterials.ChairPlastic, room);
            b.Chair("Chair_Suspect", SuspectSeat, 180f, EnvMaterials.ChairPlastic, room);

            // Story positions used by future dialogue/camera systems.
            Marker(room, "InvestigatorPosition", InvestigatorSeat + Vector3.up * 1.2f, 0f);
            Marker(room, "SuspectPosition", SuspectSeat + Vector3.up * 1.2f, 180f);
            BuildSuspectPlaceholder(b, room);

            // Overhead pendant: the single hard key light. Pool of light on the table, dark edges.
            Vector3 lampPos = TableCenter + new Vector3(0f, 2.05f, 0f);
            b.Rod("Pendant_Cable", new Vector3(lampPos.x, H, lampPos.z), lampPos + Vector3.up * 0.1f, 0.006f, EnvMaterials.Trim, room);
            b.Cylinder("Pendant_Shade", lampPos, 0.24f, 0.18f, EnvMaterials.Trim, room);
            b.Cylinder("Pendant_Bulb", lampPos - Vector3.up * 0.1f, 0.09f, 0.03f, EnvMaterials.LightWarm, room);
            b.SpotLight("Pendant_Light", lampPos - Vector3.up * 0.12f, new Vector3(90f, 0f, 0f), new Color(1f, 0.85f, 0.66f), 4f, 4.5f, 85f, true, room);
            b.PointLight("Fill_Cool", new Vector3(-3f, 2.6f, -1.4f), new Color(0.55f, 0.65f, 0.85f), 0.45f, 7f, false, room);

            // Faces must stay readable: fake the light bouncing up off the lit table onto the suspect,
            // plus a faint cool rim behind them to separate the silhouette from the dark wall.
            b.PointLight("Bounce_Table", SuspectSeat + new Vector3(0f, 0.95f, -0.45f), new Color(1f, 0.82f, 0.62f), 0.25f, 1.3f, false, room);
            b.PointLight("Rim_Suspect", SuspectSeat + new Vector3(0.7f, 1.7f, 0.8f), new Color(0.5f, 0.6f, 0.85f), 0.35f, 2.2f, false, room);

            // Corner security camera with a red recording light.
            Transform cam = b.Group("SecurityCamera", room);
            cam.position = new Vector3(-3.8f, 2.78f, 2.8f);
            SceneKit.LookAt(cam, TableCenter + Vector3.up * 0.8f);
            b.Box("Body", Vector3.zero, new Vector3(0.12f, 0.1f, 0.24f), EnvMaterials.Trim, cam, false);
            b.Box("RecLED", new Vector3(0.04f, 0.035f, 0.121f), new Vector3(0.015f, 0.015f, 0.005f), EnvMaterials.RedAccent, cam, false);
            b.PointLight("RecGlow", cam.position + cam.forward * 0.2f, new Color(1f, 0.08f, 0.05f), 0.06f, 0.8f, false, room);

            // Realtime reflection probe (updates once at load) so the one-way mirror reflects the room.
            var probeGo = new GameObject("ReflectionProbe_Interview");
            probeGo.transform.SetParent(room, false);
            probeGo.transform.position = TableCenter + Vector3.up * 1.5f;
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.size = new Vector3(5.6f, 3f, 5f);
            probe.boxProjection = true;
        }

        private static void BuildSuspectPlaceholder(Blockout b, Transform parent)
        {
            // A seated mannequin made of primitives, facing the investigator (local +Z = towards the table).
            Transform s = b.Group("Suspect_Placeholder", parent);
            s.position = SuspectSeat;
            s.rotation = Quaternion.Euler(0f, 180f, 0f);

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Torso";
            torso.transform.SetParent(s, false);
            torso.transform.localPosition = new Vector3(0f, 0.84f, -0.06f);
            torso.transform.localScale = new Vector3(0.4f, 0.32f, 0.26f);
            torso.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Clothing;
            Object.DestroyImmediate(torso.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(s, false);
            head.transform.localPosition = new Vector3(0f, 1.28f, -0.03f);
            head.transform.localScale = new Vector3(0.2f, 0.24f, 0.22f);
            head.GetComponent<Renderer>().sharedMaterial = EnvMaterials.Mannequin;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            // Limb end points are in the figure's local space (Blockout places children relative to their parent).
            for (int side = -1; side <= 1; side += 2)
            {
                b.Rod($"UpperArm_{side}", new Vector3(0.19f * side, 1.06f, -0.04f), new Vector3(0.22f * side, 0.82f, 0.2f), 0.045f, EnvMaterials.Clothing, s);
                b.Rod($"Forearm_{side}", new Vector3(0.22f * side, 0.82f, 0.2f), new Vector3(0.12f * side, 0.8f, 0.45f), 0.04f, EnvMaterials.Clothing, s);
                b.Rod($"Hand_{side}", new Vector3(0.12f * side, 0.8f, 0.45f), new Vector3(0.09f * side, 0.79f, 0.53f), 0.035f, EnvMaterials.Mannequin, s);
                b.Rod($"Thigh_{side}", new Vector3(0.1f * side, 0.52f, -0.02f), new Vector3(0.11f * side, 0.52f, 0.4f), 0.065f, EnvMaterials.Clothing, s);
                b.Rod($"Shin_{side}", new Vector3(0.11f * side, 0.52f, 0.4f), new Vector3(0.11f * side, 0.06f, 0.44f), 0.055f, EnvMaterials.Clothing, s);
            }

            // One collider for the whole figure so the player can't walk through it.
            var capsule = s.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.75f, 0.1f);
            capsule.height = 1.5f;
            capsule.radius = 0.3f;
        }

        private static void BuildOneWayWindow(Blockout b)
        {
            Transform win = b.Group("OneWayWindow");
            var center = new Vector3(1.5f, 1.5f, 0.5f);
            var size = new Vector2(2.2f, 1.1f);

            // Interview side: a single-sided dark mirror facing west. From the observation side it is
            // invisible (back faces aren't drawn), so observers see straight through: a free one-way glass.
            b.Quad("Mirror_InterviewSide", center + Vector3.left * 0.06f, size, Quaternion.Euler(0f, 90f, 0f), EnvMaterials.OneWayMirror, win);
            b.Quad("Glass_ObservationSide", center + Vector3.right * 0.06f, size, Quaternion.Euler(0f, -90f, 0f), EnvMaterials.GlassTint, win);

            foreach (float side in new[] { -1f, 1f })
            {
                float x = 1.5f + side * 0.105f;
                b.Box($"Frame_Top_{side}", new Vector3(x, 2.07f, 0.5f), new Vector3(0.03f, 0.05f, 2.3f), EnvMaterials.Trim, win, false);
                b.Box($"Frame_Bottom_{side}", new Vector3(x, 0.93f, 0.5f), new Vector3(0.03f, 0.05f, 2.3f), EnvMaterials.Trim, win, false);
                b.Box($"Frame_L_{side}", new Vector3(x, 1.5f, -0.625f), new Vector3(0.03f, 1.2f, 0.05f), EnvMaterials.Trim, win, false);
                b.Box($"Frame_R_{side}", new Vector3(x, 1.5f, 1.625f), new Vector3(0.03f, 1.2f, 0.05f), EnvMaterials.Trim, win, false);
            }

            // Invisible glass collider.
            var blocker = new GameObject("Glass_Collider");
            blocker.transform.SetParent(win, false);
            blocker.transform.position = center;
            blocker.AddComponent<BoxCollider>().size = new Vector3(0.12f, size.y, size.x);
        }

        private static void BuildObservationRoom(Blockout b)
        {
            Transform obs = b.Group("ObservationRoom");
            b.Table("Desk_Observation", new Vector3(2.0f, 0f, 0.5f), new Vector3(0.6f, 0.76f, 2.1f), EnvMaterials.DeskLaminate, EnvMaterials.Trim, obs);
            // Monitor at the south end of the desk so it never blocks the view through the glass.
            b.Monitor("Monitor_Feed", new Vector3(2.05f, 0.76f, -0.25f), 90f, obs);
            b.Box("Speaker", new Vector3(2.0f, 0.86f, 1.35f), new Vector3(0.14f, 0.2f, 0.12f), EnvMaterials.Trim, obs, false);
            b.Box("Notepad", new Vector3(2.05f, 0.765f, 0.4f), new Vector3(0.21f, 0.01f, 0.29f), EnvMaterials.PaperAged, obs, false);
            b.Chair("Chair_Observer", new Vector3(2.75f, 0f, 0.4f), -90f, EnvMaterials.ChairPlastic, obs);

            // Dark room, so the interview room reads clearly through the glass; red accent strip.
            b.Box("RedStrip", new Vector3(3.88f, 2.72f, 0.5f), new Vector3(0.03f, 0.03f, 2.6f), EnvMaterials.RedAccent, obs, false);
            b.PointLight("Accent_Red", new Vector3(3.5f, 2.4f, 0.5f), new Color(0.9f, 0.12f, 0.1f), 0.55f, 4f, false, obs);
            b.PointLight("Monitor_Glow", new Vector3(2.4f, 1.1f, -0.25f), new Color(0.3f, 0.5f, 0.9f), 0.3f, 2f, false, obs);
        }

        private static void BuildCorridor(Blockout b)
        {
            Transform corridor = b.Group("Corridor");

            SceneKit.SceneDoorAt(b, "Door_ToStation", new Vector3(-4f, 0f, -3f), Vector3.right, "Back to the main office",
                PoliceStationBuilder.SceneName, "FromInterrogation", "MAIN OFFICE", corridor);
            SceneKit.HingedDoor(b, "Door_Interview", new Vector3(-3f, 0f, -2f), Vector3.back, 1.0f, corridor);
            SceneKit.HingedDoor(b, "Door_Observation", new Vector3(2.75f, 0f, -2f), Vector3.back, 1.0f, corridor);

            // Room signs on the corridor side (readers look north, +Z).
            RoomSign(b, corridor, new Vector3(-3f, 2.45f, -2.12f), "INTERVIEW 1");
            RoomSign(b, corridor, new Vector3(2.75f, 2.45f, -2.12f), "OBSERVATION");
            b.Box("InSession_Lamp", new Vector3(-3f, 2.72f, -2.12f), new Vector3(0.3f, 0.08f, 0.04f), EnvMaterials.RedAccent, corridor, false);
            b.PointLight("InSession_Glow", new Vector3(-3f, 2.6f, -2.5f), new Color(1f, 0.1f, 0.06f), 0.5f, 2.2f, false, corridor);

            b.Box("Bench_Seat", new Vector3(0.6f, 0.44f, -3.7f), new Vector3(1.6f, 0.06f, 0.42f), EnvMaterials.ChairPlastic, corridor);
            b.Box("Bench_Leg", new Vector3(0.6f, 0.21f, -3.7f), new Vector3(0.08f, 0.42f, 0.34f), EnvMaterials.Trim, corridor, false);

            Light corridorLight = b.CeilingPanel("Ceiling_Corridor", new Vector3(-0.5f, 0f, -3f), H, new Color(0.85f, 0.9f, 1f), 0.8f, 6f, false, corridor);
            var flick = corridorLight.gameObject.AddComponent<FlickeringLight>();
            SceneKit.Set(flick, "fixtureRenderer", corridorLight.transform.parent.Find("Diffuser").GetComponent<Renderer>());
            SceneKit.Set(flick, "averageInterval", 12f);
        }

        private static void RoomSign(Blockout b, Transform parent, Vector3 pos, string text)
        {
            b.Box(text + "_Plate", pos + new Vector3(0f, 0f, 0.012f), new Vector3(0.9f, 0.16f, 0.02f), EnvMaterials.Trim, parent, false);
            b.Label(text + "_Text", pos, Quaternion.identity, text, 0.95f, new Color(0.82f, 0.83f, 0.85f), parent, 0.88f, 0.16f).characterSpacing = 6f;
        }

        private static CinematicShotDirector BuildCameraRig(Blockout b)
        {
            Transform rig = b.Group("InterviewCameraRig");
            var director = rig.gameObject.AddComponent<CinematicShotDirector>();

            var camGo = new GameObject("CinematicCamera");
            camGo.transform.SetParent(rig, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 40f;
            cam.enabled = false;
            SceneKit.ConfigureCamera(cam);
            SceneKit.Set(director, "cinematicCamera", cam);

            Transform shots = b.Group("Shots", rig);
            Vector3 table = TableCenter + Vector3.up * 0.9f;
            // Order = Tab cycling order. Each shot: id, position, look target, field of view, push-in speed.
            Shot(director, shots, "Establishing", new Vector3(-3.6f, 2.45f, -1.7f), table, 56f, 0.02f);
            Shot(director, shots, "OverShoulder_Investigator", new Vector3(-0.9f, 1.32f, -0.9f), SuspectHead, 34f, 0.012f);
            Shot(director, shots, "Suspect_CloseUp", new Vector3(-1.2f, 1.26f, 0.05f), SuspectHead, 28f, 0.008f);
            Shot(director, shots, "Profile_TwoShot", new Vector3(-3.55f, 1.15f, 0.5f), table, 36f, 0.015f);
            Shot(director, shots, "Through_OneWayGlass", new Vector3(3.3f, 1.5f, 1.25f), table + Vector3.up * 0.1f, 30f, 0.01f);
            Shot(director, shots, "TableTop_Evidence", TableCenter + new Vector3(0f, 2.3f, -0.15f), TableCenter + new Vector3(0f, 0.76f, -0.05f), 42f, 0f);
            return director;
        }

        private static void Shot(CinematicShotDirector director, Transform parent, string id, Vector3 position, Vector3 target, float fov, float push)
        {
            var go = new GameObject("Shot_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            SceneKit.LookAt(go.transform, target);
            var shot = go.AddComponent<CinematicShot>();
            SceneKit.Set(shot, "shotId", id);
            SceneKit.Set(shot, "fieldOfView", fov);
            SceneKit.Set(shot, "pushInSpeed", push);
            SceneKit.AddToList(director, "shots", shot);
        }

        private static void BuildInterviewSeat(Blockout b, CinematicShotDirector director)
        {
            GameObject chair = GameObject.Find("Chair_Investigator");
            var seat = chair.AddComponent<InterviewSeat>();
            SceneKit.Set(seat, "prompt", "Sit down");
            SceneKit.Set(seat, "director", director);
            SceneKit.Set(seat, "suspect", UnityEditor.AssetDatabase.LoadAssetAtPath<TLA.Data.Cases.SuspectProfile>(UI.CaseDataBuilder.MarlowePath));

            var stand = new GameObject("InterviewSeat_StandPoint");
            stand.transform.SetParent(chair.transform.parent, false);
            stand.transform.SetPositionAndRotation(new Vector3(-2.2f, 0f, -0.9f), Quaternion.Euler(0f, 35f, 0f));
            SceneKit.Set(seat, "standPoint", stand.transform);
        }

        private static void Marker(Transform parent, string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
