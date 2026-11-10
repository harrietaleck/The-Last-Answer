using TLA.Gameplay.Environment;
using TLA.Systems.Flow;
using UnityEngine;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// POLICE STATION HUB (16 x 12 m, night shift). Three zones:
    ///   Lobby (south)    front desk, benches, precinct sign, EXIT door → main menu
    ///   Bullpen (west)   four detective desks, filing cabinets, whiteboard, night windows with blinds
    ///   Corridor (east)  doors to the Interrogation Room (red "in session" lamp) and the Evidence Room
    /// </summary>
    internal static class PoliceStationBuilder
    {
        public const string SceneName = "PoliceStation";
        public const string ScenePath = LocationBuilder.LocationsFolder + "/" + SceneName + ".unity";

        private const float H = 3.2f;   // ceiling height
        private const float T = 0.2f;   // wall thickness

        public static void Build()
        {
            Blockout b = SceneKit.NewScene(ScenePath, out var scene);
            SceneKit.Atmosphere(new Color(0.09f, 0.1f, 0.12f), new Color(0.02f, 0.025f, 0.03f), 0.02f);
            SceneKit.PostProcessing(RenderPipelineSetup.StationProfilePath);
            SceneKit.Context(GameState.Investigating);
            SceneKit.RoomTone(0.07f, 50f, 0.3f);

            Transform shell = b.Group("Shell");
            b.Box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(16.4f, 0.1f, 12.4f), EnvMaterials.Linoleum, shell);
            b.Box("Ceiling", new Vector3(0f, H + 0.05f, 0f), new Vector3(16.4f, 0.1f, 12.4f), EnvMaterials.Ceiling, shell);

            // Outer walls (openings measured from each wall's start point).
            b.Wall("Wall_South", new Vector3(-8f, 0f, -6f), new Vector3(8f, 0f, -6f), H, T, EnvMaterials.WallPaint,
                Blockout.Opening.Door(8f, 1.0f));
            b.Wall("Wall_North", new Vector3(-8f, 0f, 6f), new Vector3(8f, 0f, 6f), H, T, EnvMaterials.WallPaint,
                Blockout.Opening.Window(2f, 1.6f, 1.0f, 2.3f), Blockout.Opening.Window(4.5f, 1.6f, 1.0f, 2.3f),
                Blockout.Opening.Window(7f, 1.6f, 1.0f, 2.3f), Blockout.Opening.Door(13.5f, 1.0f));
            b.Wall("Wall_West", new Vector3(-8f, 0f, -6f), new Vector3(-8f, 0f, 6f), H, T, EnvMaterials.WallPaint);
            b.Wall("Wall_East", new Vector3(8f, 0f, -6f), new Vector3(8f, 0f, 6f), H, T, EnvMaterials.WallPaint,
                Blockout.Opening.Door(7.5f, 1.0f));

            // Interior partitions: lobby/bullpen (with an open archway) and bullpen/corridor.
            b.Wall("Partition_Lobby", new Vector3(-8f, 0f, -2f), new Vector3(3f, 0f, -2f), H, T, EnvMaterials.WallPaint,
                Blockout.Opening.Door(8f, 1.8f, 2.4f));
            b.Wall("Partition_Corridor", new Vector3(3f, 0f, -2f), new Vector3(3f, 0f, 6f), H, T, EnvMaterials.WallPaint,
                Blockout.Opening.Door(4f, 1.4f, 2.4f));

            BuildLobby(b);
            BuildBullpen(b);
            BuildCorridor(b);

            Transform spawns = b.Group("SpawnPoints");
            SceneKit.Spawn(spawns, "Entrance", new Vector3(0f, 0f, -5.1f), 0f, true);
            SceneKit.Spawn(spawns, "FromInterrogation", new Vector3(5.5f, 0f, 4.9f), 180f, false);
            SceneKit.Spawn(spawns, "FromEvidence", new Vector3(6.9f, 0f, 1.5f), 270f, false);
            SceneKit.Player(new Vector3(0f, 0f, -5.1f), 0f);

            SceneKit.Save(scene);
        }

        private static void BuildLobby(Blockout b)
        {
            Transform lobby = b.Group("Lobby");

            // Front desk.
            b.Box("FrontDesk_Counter", new Vector3(-3f, 0.55f, -3.7f), new Vector3(3f, 1.1f, 0.45f), EnvMaterials.DarkWood, lobby);
            b.Box("FrontDesk_Top", new Vector3(-3f, 1.12f, -3.6f), new Vector3(3.1f, 0.04f, 0.7f), EnvMaterials.DeskLaminate, lobby);
            b.Box("FrontDesk_Return", new Vector3(-4.35f, 0.55f, -3.1f), new Vector3(0.45f, 1.1f, 1.2f), EnvMaterials.DarkWood, lobby);
            b.Box("FrontDesk_Work", new Vector3(-3f, 0.74f, -3.1f), new Vector3(2.4f, 0.04f, 0.6f), EnvMaterials.DeskLaminate, lobby, false);
            b.Monitor("FrontDesk_Monitor", new Vector3(-2.6f, 0.76f, -3.1f), 0f, lobby); // faces the receptionist's chair (north)
            b.Chair("FrontDesk_Chair", new Vector3(-2.8f, 0f, -2.55f), 180f, EnvMaterials.ChairPlastic, lobby);
            MainMenuBuilder.DeskLamp(b, lobby, new Vector3(-3.8f, 0.76f, -3.0f), new Vector3(-3.2f, 0.76f, -3.2f), 1.6f);

            // Waiting benches.
            for (int i = 0; i < 2; i++)
            {
                float z = -4.6f + i * 1.6f;
                b.Box($"Bench_{i}_Seat", new Vector3(-7.5f, 0.44f, z), new Vector3(0.5f, 0.06f, 1.4f), EnvMaterials.ChairPlastic, lobby);
                b.Box($"Bench_{i}_Back", new Vector3(-7.78f, 0.75f, z), new Vector3(0.06f, 0.55f, 1.4f), EnvMaterials.ChairPlastic, lobby, false);
                b.Box($"Bench_{i}_Leg", new Vector3(-7.5f, 0.21f, z), new Vector3(0.4f, 0.42f, 0.08f), EnvMaterials.Trim, lobby, false);
            }

            // Precinct sign on the partition, facing the entrance (fictional department name).
            b.Box("PrecinctSign_Plate", new Vector3(-4f, 2.35f, -2.12f), new Vector3(3.2f, 0.62f, 0.03f), EnvMaterials.Trim, lobby, false);
            var sign = b.Label("PrecinctSign_Text", new Vector3(-4f, 2.45f, -2.14f), Quaternion.identity, "METRO POLICE DEPARTMENT", 2.2f, new Color(0.82f, 0.83f, 0.85f), lobby, 3f, 0.3f);
            sign.characterSpacing = 8f;
            b.Label("PrecinctSign_Sub", new Vector3(-4f, 2.18f, -2.14f), Quaternion.identity, "14TH PRECINCT  ·  CRIMINAL INVESTIGATIONS", 1.1f, new Color(0.6f, 0.62f, 0.66f), lobby, 3f, 0.2f).characterSpacing = 6f;

            // EXIT sign over the front door (red accent) + front door back to the menu.
            b.Box("ExitSign", new Vector3(0f, 2.48f, -5.86f), new Vector3(0.46f, 0.16f, 0.05f), EnvMaterials.RedAccent, lobby, false);
            b.Label("ExitSign_Text", new Vector3(0f, 2.48f, -5.83f), Quaternion.Euler(0f, 180f, 0f), "EXIT", 1.2f, new Color(0.08f, 0f, 0f), lobby, 0.46f, 0.16f).characterSpacing = 10f;
            b.PointLight("ExitSign_Glow", new Vector3(0f, 2.3f, -5.6f), new Color(1f, 0.12f, 0.08f), 0.6f, 2.5f, false, lobby);
            SceneKit.SceneDoorAt(b, "FrontDoor", new Vector3(0f, 0f, -6f), Vector3.forward, "Leave station", "MainMenu", "", "", lobby);

            b.CeilingPanel("Ceiling_Lobby_W", new Vector3(-4.5f, 0f, -4f), H, new Color(0.85f, 0.9f, 1f), 1.0f, 6.5f, false, lobby);
            b.CeilingPanel("Ceiling_Lobby_C", new Vector3(0.5f, 0f, -4f), H, new Color(0.85f, 0.9f, 1f), 1.1f, 6.5f, false, lobby);
            b.CeilingPanel("Ceiling_Lobby_E", new Vector3(5.5f, 0f, -4f), H, new Color(0.85f, 0.9f, 1f), 0.9f, 6f, false, lobby);
        }

        private static void BuildBullpen(Blockout b)
        {
            Transform pen = b.Group("Bullpen");

            Vector2[] desks = { new Vector2(-5.5f, 0.5f), new Vector2(-5.5f, 3.6f), new Vector2(-1.5f, 0.5f), new Vector2(-1.5f, 3.6f) };
            for (int i = 0; i < desks.Length; i++)
            {
                var p = new Vector3(desks[i].x, 0f, desks[i].y);
                b.Table($"Desk_{i}", p, new Vector3(1.5f, 0.75f, 0.75f), EnvMaterials.DeskLaminate, EnvMaterials.Trim, pen);
                b.Monitor($"Desk_{i}_Monitor", p + new Vector3(0.25f, 0.75f, 0.15f), 180f, pen);
                b.Chair($"Desk_{i}_Chair", p + new Vector3(0.1f, 0f, -0.7f), 10f * (i - 1.5f), EnvMaterials.ChairPlastic, pen);
                b.Box($"Desk_{i}_Papers", p + new Vector3(-0.4f, 0.765f, -0.05f), new Vector3(0.3f, 0.03f, 0.22f), EnvMaterials.Paper, pen, false);
                b.Box($"Desk_{i}_Folder", p + new Vector3(-0.38f, 0.785f, -0.02f), new Vector3(0.26f, 0.012f, 0.32f), EnvMaterials.Manila, pen, false).transform.localRotation = Quaternion.Euler(0f, 8f * i, 0f);
            }

            for (int i = 0; i < 4; i++)
                b.FilingCabinet($"Cabinet_{i}", new Vector3(-7.62f, 0f, -1.3f + i * 0.5f), 90f, 4, pen);

            // Whiteboard on the west wall.
            b.Box("Whiteboard", new Vector3(-7.88f, 1.55f, 3.6f), new Vector3(0.03f, 1.1f, 2.0f), EnvMaterials.Whiteboard, pen, false);
            b.Box("Whiteboard_Tray", new Vector3(-7.84f, 0.98f, 3.6f), new Vector3(0.06f, 0.03f, 2.0f), EnvMaterials.Trim, pen, false);
            b.Label("Whiteboard_Text", new Vector3(-7.86f, 1.85f, 3.6f), Quaternion.Euler(0f, -90f, 0f), "ACTIVE CASES", 1.6f, new Color(0.12f, 0.14f, 0.2f), pen, 1.8f, 0.3f);
            b.Label("Whiteboard_Case", new Vector3(-7.86f, 1.55f, 3.6f), Quaternion.Euler(0f, -90f, 0f), "#0147  -  OPEN  -  INTERVIEWS PENDING", 0.9f, new Color(0.6f, 0.06f, 0.05f), pen, 1.8f, 0.2f);

            // Night windows with blinds (the bluish glow reads as "city at night").
            float[] windowX = { -6f, -3.5f, -1f };
            foreach (float x in windowX)
            {
                b.Quad($"Window_{x}", new Vector3(x, 1.65f, 6.08f), new Vector2(1.6f, 1.3f), Quaternion.identity, EnvMaterials.NightWindow, pen);
                for (int s = 0; s < 10; s++)
                    b.RotatedBox($"Blind_{x}_{s}", new Vector3(x, 1.08f + s * 0.125f, 5.93f), new Vector3(1.6f, 0.05f, 0.01f), new Vector3(30f, 0f, 0f), EnvMaterials.Blinds, pen, false);
            }
            b.PointLight("Moonlight_Fill", new Vector3(-3.5f, 2.0f, 5.2f), new Color(0.35f, 0.45f, 0.75f), 0.45f, 5f, false, pen);

            b.Cylinder("WaterCooler", new Vector3(2.4f, 0.55f, 5.5f), 0.16f, 1.1f, EnvMaterials.Paper, pen, true);
            b.CeilingPanel("Ceiling_Bullpen_W", new Vector3(-5.5f, 0f, 2f), H, new Color(0.85f, 0.9f, 1f), 1.1f, 7f, true, pen);
            b.CeilingPanel("Ceiling_Bullpen_E", new Vector3(-1.5f, 0f, 2f), H, new Color(0.85f, 0.9f, 1f), 1.0f, 7f, false, pen);
        }

        private static void BuildCorridor(Blockout b)
        {
            Transform corridor = b.Group("Corridor");

            SceneKit.SceneDoorAt(b, "Door_Interrogation", new Vector3(5.5f, 0f, 6f), Vector3.back, "Enter interrogation",
                InterrogationRoomBuilder.SceneName, "FromHub", "INTERROGATION", corridor);
            // Red "in session" lamp: the signature red accent of the station.
            b.Box("InSession_Lamp", new Vector3(5.5f, 2.72f, 5.86f), new Vector3(0.34f, 0.09f, 0.05f), EnvMaterials.RedAccent, corridor, false);
            b.PointLight("InSession_Glow", new Vector3(5.5f, 2.6f, 5.55f), new Color(1f, 0.1f, 0.06f), 0.8f, 2.6f, false, corridor);

            SceneKit.SceneDoorAt(b, "Door_Evidence", new Vector3(8f, 0f, 1.5f), Vector3.left, "Enter evidence room",
                EvidenceRoomBuilder.SceneName, "FromHub", "EVIDENCE", corridor);

            b.Cylinder("FireExtinguisher", new Vector3(7.8f, 0.35f, -0.6f), 0.08f, 0.5f, EnvMaterials.Get("M_RedPaint", new Color(0.42f, 0.04f, 0.04f), 0.55f), corridor);

            Light flicker = b.CeilingPanel("Ceiling_Corridor", new Vector3(5.5f, 0f, 3f), H, new Color(0.85f, 0.9f, 1f), 0.9f, 6f, false, corridor);
            var flick = flicker.gameObject.AddComponent<FlickeringLight>();
            SceneKit.Set(flick, "fixtureRenderer", flicker.transform.parent.Find("Diffuser").GetComponent<Renderer>());
        }
    }
}
