using TLA.Data.Evidence;
using TLA.EditorTools.UI;
using TLA.Gameplay.Investigation;
using TLA.Systems.Flow;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// EVIDENCE ROOM (7 x 6 m): where the player reviews the case.
    ///   North wall  main evidence board: suspect/victim photos, documents, index cards, red string
    ///   West wall   timeline whiteboard
    ///   Centre      investigation desk: case files, open folder, scattered photos, laptop, desk lamp
    ///   East wall   evidence shelving with labelled boxes, filing cabinets
    /// Evidence (EvidencePickup, data in Data/Cases/Case0147/Evidence): photos on the board, case file, laptop CCTV,
    /// sale agreement in the open folder, access log in the filing cabinet, coffee cups in an evidence tray.
    /// </summary>
    internal static class EvidenceRoomBuilder
    {
        public const string SceneName = "EvidenceRoom";
        public const string ScenePath = LocationBuilder.LocationsFolder + "/" + SceneName + ".unity";

        private const float H = 3.0f;
        private const float T = 0.2f;
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.14f);

        public static void Build()
        {
            Blockout b = SceneKit.NewScene(ScenePath, out var scene);
            SceneKit.Atmosphere(new Color(0.07f, 0.074f, 0.09f), new Color(0.015f, 0.017f, 0.02f), 0.025f);
            SceneKit.PostProcessing(RenderPipelineSetup.StationProfilePath);
            SceneKit.Context(GameState.Investigating);
            SceneKit.RoomTone(0.06f, 50f, 0.2f);

            Transform shell = b.Group("Shell");
            b.Box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(7.4f, 0.1f, 6.4f), EnvMaterials.Linoleum, shell);
            b.Box("Ceiling", new Vector3(0f, H + 0.05f, 0f), new Vector3(7.4f, 0.1f, 6.4f), EnvMaterials.Ceiling, shell);
            b.Wall("Wall_South", new Vector3(-3.5f, 0f, -3f), new Vector3(3.5f, 0f, -3f), H, T, EnvMaterials.WallPaint, Blockout.Opening.Door(1f, 1.0f));
            b.Wall("Wall_North", new Vector3(-3.5f, 0f, 3f), new Vector3(3.5f, 0f, 3f), H, T, EnvMaterials.WallPaint);
            b.Wall("Wall_West", new Vector3(-3.5f, 0f, -3f), new Vector3(-3.5f, 0f, 3f), H, T, EnvMaterials.WallPaint);
            b.Wall("Wall_East", new Vector3(3.5f, 0f, -3f), new Vector3(3.5f, 0f, 3f), H, T, EnvMaterials.WallPaint);

            BuildEvidenceBoard(b);
            BuildTimeline(b);
            BuildDesk(b);
            BuildShelving(b);
            BuildLighting(b);

            Transform exits = b.Group("Exits");
            SceneKit.SceneDoorAt(b, "Door_ToStation", new Vector3(-2.5f, 0f, -3f), Vector3.forward, "Back to the main office",
                PoliceStationBuilder.SceneName, "FromEvidence", "MAIN OFFICE", exits);

            Transform spawns = b.Group("SpawnPoints");
            SceneKit.Spawn(spawns, "FromHub", new Vector3(-2.5f, 0f, -2.2f), 0f, true);
            SceneKit.Player(new Vector3(-2.5f, 0f, -2.2f), 0f);

            SceneKit.Save(scene);
        }

        private static void BuildEvidenceBoard(Blockout b)
        {
            Transform board = b.Group("EvidenceBoard");
            const float faceZ = 2.86f;   // front surface of the cork (readers look north, +Z)
            var center = new Vector3(0f, 1.62f, faceZ + 0.02f);

            GameObject cork = b.Box("Cork", center, new Vector3(3.4f, 1.7f, 0.04f), EnvMaterials.Cork, board);
            b.Box("Frame_T", center + new Vector3(0f, 0.87f, -0.01f), new Vector3(3.5f, 0.04f, 0.06f), EnvMaterials.DarkWood, board, false);
            b.Box("Frame_B", center + new Vector3(0f, -0.87f, -0.01f), new Vector3(3.5f, 0.04f, 0.06f), EnvMaterials.DarkWood, board, false);
            b.Box("Frame_L", center + new Vector3(-1.73f, 0f, -0.01f), new Vector3(0.04f, 1.78f, 0.06f), EnvMaterials.DarkWood, board, false);
            b.Box("Frame_R", center + new Vector3(1.73f, 0f, -0.01f), new Vector3(0.04f, 1.78f, 0.06f), EnvMaterials.DarkWood, board, false);
            var header = b.Label("Header", new Vector3(0f, 2.32f, faceZ - 0.005f), Quaternion.identity, "CASE #0147   ·   OPEN", 1.6f, Ink, board, 3f, 0.25f);
            header.characterSpacing = 8f;
            header.fontStyle = FontStyles.Bold;

            // Pin positions (local x, y on the board face) for photos and documents.
            Vector2 victim = new Vector2(-1.3f, 0.35f), s1 = new Vector2(-0.45f, 0.42f), s2 = new Vector2(0.4f, 0.38f), s3 = new Vector2(1.25f, 0.42f);
            Vector2 scene = new Vector2(-1.1f, -0.42f), doc1 = new Vector2(-0.2f, -0.35f), doc2 = new Vector2(0.55f, -0.38f), card = new Vector2(1.3f, -0.3f);

            BoardPhoto(b, board, faceZ, victim, "VICTIM", EnvMaterials.PhotoWarm);
            BoardPhoto(b, board, faceZ, s1, "SUSPECT 1", EnvMaterials.PhotoCool);
            BoardPhoto(b, board, faceZ, s2, "SUSPECT 2", EnvMaterials.PhotoCool);
            BoardPhoto(b, board, faceZ, s3, "SUSPECT 3", EnvMaterials.PhotoCool);
            BoardPhoto(b, board, faceZ, scene, "SCENE", EnvMaterials.PhotoWarm);
            BoardDocument(b, board, faceZ, doc1, 4f, "STATEMENT");
            BoardDocument(b, board, faceZ, doc2, -3f, "AUTOPSY");
            BoardCard(b, board, faceZ, card, "21:40 ?");
            BoardCard(b, board, faceZ, new Vector2(0f, 0.02f), "ALIBI");
            BoardCard(b, board, faceZ, new Vector2(-0.85f, 0.0f), "MOTIVE");

            // Red string between pins: the classic "connections" read, and a subtle red accent.
            Vector2 alibi = new Vector2(0f, 0.02f), motive = new Vector2(-0.85f, 0f);
            Thread(b, board, faceZ, victim, s1);
            Thread(b, board, faceZ, victim, scene);
            Thread(b, board, faceZ, s1, motive);
            Thread(b, board, faceZ, s2, alibi);
            Thread(b, board, faceZ, alibi, doc1);
            Thread(b, board, faceZ, s3, card);
            Thread(b, board, faceZ, s2, doc2);

            AddPickup(cork, "EVD_ScenePhotos");
        }

        private static void BuildTimeline(Blockout b)
        {
            Transform tl = b.Group("TimelineBoard");
            const float faceX = -3.36f;      // readers look west (-X)
            Quaternion read = Quaternion.Euler(0f, -90f, 0f);

            b.Box("Whiteboard", new Vector3(faceX - 0.015f, 1.6f, 0.2f), new Vector3(0.03f, 1.2f, 2.4f), EnvMaterials.Whiteboard, tl, false);
            b.Box("Tray", new Vector3(faceX + 0.02f, 0.98f, 0.2f), new Vector3(0.06f, 0.03f, 2.4f), EnvMaterials.Trim, tl, false);
            b.Label("Title", new Vector3(faceX + 0.003f, 2.05f, 0.2f), read, "TIMELINE  -  NIGHT OF 14TH", 1.2f, Ink, tl, 2.2f, 0.2f).characterSpacing = 5f;
            b.Box("Line", new Vector3(faceX + 0.002f, 1.6f, 0.2f), new Vector3(0.004f, 0.012f, 2.1f), EnvMaterials.Trim, tl, false);

            string[] times = { "19:00", "20:30", "21:40", "22:15", "23:50" };
            for (int i = 0; i < times.Length; i++)
            {
                float z = -0.8f + i * 0.5f;  // a reader facing west has -Z on their left, so time runs -Z → +Z
                b.Box($"Tick_{i}", new Vector3(faceX + 0.002f, 1.6f, z), new Vector3(0.004f, 0.08f, 0.012f), EnvMaterials.Trim, tl, false);
                b.Label($"Time_{i}", new Vector3(faceX + 0.003f, 1.72f, z), read, times[i], 0.8f, Ink, tl, 0.4f, 0.1f);
                b.Box($"Note_{i}", new Vector3(faceX + 0.006f, 1.38f - (i % 2) * 0.12f, z), new Vector3(0.006f, 0.1f, 0.15f), EnvMaterials.PaperAged, tl, false);
            }
            b.Box("Marker_Red", new Vector3(faceX + 0.002f, 1.6f, 0.2f), new Vector3(0.006f, 0.05f, 0.05f), EnvMaterials.RedAccent, tl, false);
        }

        private static void BuildDesk(Blockout b)
        {
            Transform desk = b.Group("InvestigationDesk");
            var deskPos = new Vector3(0f, 0f, -0.2f);
            const float top = 0.76f;

            b.Table("Desk", deskPos, new Vector3(1.9f, top, 0.95f), EnvMaterials.DarkWood, EnvMaterials.Trim, desk);
            b.Chair("Chair", new Vector3(0f, 0f, -1.0f), 0f, EnvMaterials.ChairPlastic, desk);

            // Case files: a stack of manila folders, labelled.
            Transform stack = b.Group("CaseFiles", desk);
            for (int i = 0; i < 4; i++)
            {
                GameObject folder = b.Box($"Folder_{i}", new Vector3(-0.6f, top + 0.015f + i * 0.028f, -0.12f), new Vector3(0.24f, 0.026f, 0.32f), EnvMaterials.Manila, stack);
                folder.transform.localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? 3f : -4f) * (i + 1) * 0.5f, 0f);
            }
            var label = b.Label("CaseFiles_Label", new Vector3(-0.6f, top + 0.13f, -0.12f), Quaternion.Euler(90f, 0f, 0f), "CASE 0147", 0.25f, Ink, stack, 0.22f, 0.05f);
            label.fontStyle = FontStyles.Bold;
            AddPickup(stack.gameObject, "EVD_CaseFile");

            // Open folder with documents.
            b.RotatedBox("OpenFolder_L", new Vector3(-0.13f, top + 0.004f, -0.3f), new Vector3(0.24f, 0.006f, 0.32f), new Vector3(0f, -4f, 0f), EnvMaterials.Manila, desk, false);
            b.RotatedBox("OpenFolder_R", new Vector3(0.12f, top + 0.004f, -0.32f), new Vector3(0.24f, 0.006f, 0.32f), new Vector3(0f, 2f, 0f), EnvMaterials.Manila, desk, false);
            b.RotatedBox("Document_A", new Vector3(-0.13f, top + 0.009f, -0.3f), new Vector3(0.21f, 0.002f, 0.297f), new Vector3(0f, -4f, 0f), EnvMaterials.Paper, desk, false);
            Transform contract = b.Group("SaleAgreement", desk);
            b.RotatedBox("Document_B", new Vector3(0.12f, top + 0.009f, -0.32f), new Vector3(0.21f, 0.002f, 0.297f), new Vector3(0f, 2f, 0f), EnvMaterials.Paper, contract, true);
            for (int line = 0; line < 8; line++)
                b.RotatedBox($"Document_B_Line_{line}", new Vector3(0.12f, top + 0.0105f, -0.2f - line * 0.03f), new Vector3(0.16f, 0.0005f, 0.006f), new Vector3(0f, 2f, 0f), EnvMaterials.Trim, contract, false);
            AddPickup(contract.gameObject, "EVD_SaleContract");

            // Scattered photographs.
            DeskPhoto(b, desk, new Vector3(0.38f, top + 0.005f, -0.05f), 14f, EnvMaterials.PhotoCool);
            DeskPhoto(b, desk, new Vector3(0.5f, top + 0.007f, -0.25f), -22f, EnvMaterials.PhotoWarm);
            DeskPhoto(b, desk, new Vector3(0.27f, top + 0.009f, -0.38f), 5f, EnvMaterials.PhotoCool);

            // Laptop with glowing screen.
            Transform laptop = b.Group("Laptop", desk);
            laptop.position = new Vector3(0.55f, top, 0.15f);
            laptop.rotation = Quaternion.Euler(0f, -12f, 0f);
            b.Box("Base", new Vector3(0f, 0.01f, 0f), new Vector3(0.34f, 0.02f, 0.24f), EnvMaterials.Trim, laptop);
            Transform lid = b.Group("Lid", laptop);
            lid.localPosition = new Vector3(0f, 0.02f, 0.12f);
            lid.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            b.Box("Shell", new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.22f, 0.01f), EnvMaterials.Trim, lid, false);
            b.Box("Screen", new Vector3(0f, 0.11f, -0.006f), new Vector3(0.31f, 0.19f, 0.002f), EnvMaterials.Screen, lid, false);
            AddPickup(laptop.gameObject, "EVD_CCTV");

            b.Cylinder("Mug", new Vector3(-0.25f, top + 0.05f, 0.22f), 0.04f, 0.1f, EnvMaterials.Paper, desk);
            b.Box("Pen", new Vector3(-0.02f, top + 0.006f, -0.1f), new Vector3(0.14f, 0.01f, 0.01f), EnvMaterials.Trim, desk, false).transform.localRotation = Quaternion.Euler(0f, 30f, 0f);

            MainMenuBuilder.DeskLamp(b, desk, new Vector3(-0.78f, top, 0.3f), new Vector3(-0.15f, top, -0.25f), 2.6f);
        }

        private static void BuildShelving(Blockout b)
        {
            Transform shelf = b.Group("EvidenceShelving");
            const float x = 3.12f;   // shelf centre, against the east wall
            for (int i = 0; i < 3; i++)
                b.Box($"Upright_{i}", new Vector3(x, 1.0f, -2.4f + i * 1.15f), new Vector3(0.45f, 2.0f, 0.04f), EnvMaterials.MetalCabinet, shelf);
            for (int level = 0; level < 4; level++)
                b.Box($"Shelf_{level}", new Vector3(x, 0.15f + level * 0.55f, -1.25f), new Vector3(0.45f, 0.03f, 2.3f), EnvMaterials.MetalCabinet, shelf, false);

            char letter = 'A';
            for (int level = 0; level < 3; level++)
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    if ((level + slot) % 4 == 3) continue; // a few gaps look more natural
                    float z = -2.1f + slot * 0.56f;
                    float y = 0.165f + level * 0.55f + 0.14f;
                    b.Box($"Box_{letter}", new Vector3(x - 0.02f, y, z), new Vector3(0.38f, 0.28f, 0.42f), EnvMaterials.Cardboard, shelf, false);
                    b.Label($"Box_{letter}_Label", new Vector3(x - 0.215f, y + 0.02f, z), Quaternion.Euler(0f, 90f, 0f), $"0147-{letter}", 0.55f, Ink, shelf, 0.3f, 0.08f);
                    letter++;
                }
            }

            Transform cabinet = b.FilingCabinet("Cabinet_0", new Vector3(3.15f, 0f, 1.5f), -90f, 4, shelf);
            b.FilingCabinet("Cabinet_1", new Vector3(3.15f, 0f, 2.05f), -90f, 4, shelf);
            AddPickup(cabinet.gameObject, "EVD_KeycardLog", hideOnCollect: false); // the log comes out of the cabinet; the cabinet stays

            // Evidence tray 0147-C with the two coffee cups (in the gap on the top shelf, at eye level).
            Transform tray = b.Group("EvidenceTray_0147C", shelf);
            const float trayY = 0.15f + 2 * 0.55f + 0.015f;
            b.Box("Tray", new Vector3(x - 0.02f, trayY + 0.02f, -1.54f), new Vector3(0.34f, 0.04f, 0.4f), EnvMaterials.Cardboard, tray, true, false);
            b.Cylinder("Cup_C1", new Vector3(x - 0.04f, trayY + 0.085f, -1.62f), 0.04f, 0.09f, EnvMaterials.Paper, tray);
            b.Cylinder("Cup_C2", new Vector3(x + 0.02f, trayY + 0.085f, -1.45f), 0.04f, 0.09f, EnvMaterials.Paper, tray);
            b.Label("Tray_Label", new Vector3(x - 0.195f, trayY + 0.03f, -1.54f), Quaternion.Euler(0f, 90f, 0f), "0147-C", 0.55f, Ink, tray, 0.3f, 0.06f);
            AddPickup(tray.gameObject, "EVD_CoffeeCups");
        }

        private static void BuildLighting(Blockout b)
        {
            Transform lights = b.Group("Lighting");
            // Dim overhead fluorescents: the room is lit mostly by the lamp and board spots.
            b.CeilingPanel("Ceiling_W", new Vector3(-1.6f, 0f, -0.5f), H, new Color(0.8f, 0.88f, 1f), 0.6f, 6f, false, lights);
            b.CeilingPanel("Ceiling_E", new Vector3(1.6f, 0f, -0.5f), H, new Color(0.8f, 0.88f, 1f), 0.55f, 6f, false, lights);

            // Track spots washing the evidence board.
            Light spotL = b.SpotLight("BoardSpot_L", new Vector3(-0.9f, 2.9f, 1.7f), Vector3.zero, new Color(1f, 0.94f, 0.85f), 2.6f, 3.5f, 55f, true, lights);
            SceneKit.LookAt(spotL.transform, new Vector3(-0.8f, 1.6f, 2.86f));
            Light spotR = b.SpotLight("BoardSpot_R", new Vector3(0.9f, 2.9f, 1.7f), Vector3.zero, new Color(1f, 0.94f, 0.85f), 2.6f, 3.5f, 55f, false, lights);
            SceneKit.LookAt(spotR.transform, new Vector3(0.8f, 1.6f, 2.86f));
            b.Cylinder("TrackRail", new Vector3(0f, 2.98f, 1.7f), 0.015f, 0.02f, EnvMaterials.Trim, lights);
            b.Box("TrackBar", new Vector3(0f, 2.97f, 1.7f), new Vector3(2.2f, 0.03f, 0.04f), EnvMaterials.Trim, lights, false);

            // Red accent: a glow under the board and a "secure room" lamp over the door.
            b.PointLight("Accent_Board", new Vector3(0f, 0.9f, 2.55f), new Color(0.9f, 0.1f, 0.08f), 0.35f, 2f, false, lights);
            b.Box("SecureLamp", new Vector3(-2.5f, 2.62f, -2.88f), new Vector3(0.3f, 0.08f, 0.04f), EnvMaterials.RedAccent, lights, false);
            b.PointLight("SecureLamp_Glow", new Vector3(-2.5f, 2.5f, -2.6f), new Color(1f, 0.1f, 0.06f), 0.45f, 2f, false, lights);
        }

        private static void BoardPhoto(Blockout b, Transform board, float faceZ, Vector2 p, string caption, Material image)
        {
            var c = new Vector3(p.x, 1.62f + p.y, faceZ);
            b.Box("Photo_" + caption, c + new Vector3(0f, 0f, -0.004f), new Vector3(0.26f, 0.2f, 0.004f), EnvMaterials.PhotoBorder, board, false);
            b.Box("Photo_" + caption + "_Image", c + new Vector3(0f, 0.012f, -0.007f), new Vector3(0.22f, 0.15f, 0.002f), image, board, false);
            b.Label("Photo_" + caption + "_Caption", c + new Vector3(0f, -0.155f, -0.008f), Quaternion.identity, caption, 0.55f, Ink, board, 0.3f, 0.06f);
            Pin(b, board, faceZ, p + new Vector2(0f, 0.09f));
        }

        private static void BoardDocument(Blockout b, Transform board, float faceZ, Vector2 p, float tilt, string title)
        {
            var c = new Vector3(p.x, 1.62f + p.y, faceZ - 0.004f);
            GameObject doc = b.RotatedBox("Doc_" + title, c, new Vector3(0.21f, 0.297f, 0.003f), new Vector3(0f, 0f, tilt), EnvMaterials.Paper, board, false);
            b.Label("Doc_" + title + "_Title", new Vector3(0f, 0.11f, -0.6f), Quaternion.identity, title, 0.5f, Ink, doc.transform, 0.18f, 0.04f)
                .transform.localScale = new Vector3(1f / 0.21f, 1f / 0.297f, 1f);
            for (int line = 0; line < 6; line++)
                b.Box($"Line_{line}", new Vector3(0f, 0.05f - line * 0.08f, -0.6f), new Vector3(0.75f, 0.02f, 0.1f), EnvMaterials.Trim, doc.transform, false);
            Pin(b, board, faceZ, p + new Vector2(0f, 0.13f));
        }

        private static void BoardCard(Blockout b, Transform board, float faceZ, Vector2 p, string text)
        {
            var c = new Vector3(p.x, 1.62f + p.y, faceZ - 0.004f);
            b.Box("Card_" + text, c, new Vector3(0.15f, 0.1f, 0.003f), EnvMaterials.PaperAged, board, false);
            b.Label("Card_" + text + "_Text", c + new Vector3(0f, 0f, -0.003f), Quaternion.identity, text, 0.55f, new Color(0.45f, 0.05f, 0.04f), board, 0.14f, 0.06f);
            Pin(b, board, faceZ, p + new Vector2(0f, 0.04f));
        }

        private static void Pin(Blockout b, Transform board, float faceZ, Vector2 p)
        {
            b.Box("Pin", new Vector3(p.x, 1.62f + p.y, faceZ - 0.012f), new Vector3(0.018f, 0.018f, 0.018f), EnvMaterials.RedAccent, board, false);
        }

        private static void Thread(Blockout b, Transform board, float faceZ, Vector2 from, Vector2 to)
        {
            var a = new Vector3(from.x, 1.62f + from.y + 0.06f, faceZ - 0.015f);
            var c = new Vector3(to.x, 1.62f + to.y + 0.06f, faceZ - 0.015f);
            b.Rod("Thread", a, c, 0.0025f, EnvMaterials.RedString, board);
        }

        private static void DeskPhoto(Blockout b, Transform parent, Vector3 pos, float yaw, Material image)
        {
            b.RotatedBox("DeskPhoto", pos, new Vector3(0.11f, 0.003f, 0.15f), new Vector3(0f, yaw, 0f), EnvMaterials.PhotoBorder, parent, false);
            b.RotatedBox("DeskPhoto_Image", pos + Vector3.up * 0.002f, new Vector3(0.095f, 0.002f, 0.12f), new Vector3(0f, yaw, 0f), image, parent, false);
        }

        /// <summary>Makes <paramref name="target"/> an EvidencePickup for the authored evidence asset (Data/Cases/Case0147/Evidence).</summary>
        private static void AddPickup(GameObject target, string evidenceId, bool hideOnCollect = true)
        {
            var evidence = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(EvidenceDataBuilder.EvidencePath(evidenceId));
            if (evidence == null)
            {
                Debug.LogError($"[Environment] Evidence asset '{evidenceId}' is missing. Run The Last Answer → Environment → Rebuild All Locations.");
                return;
            }
            var pickup = target.AddComponent<EvidencePickup>();
            SceneKit.Set(pickup, "evidence", evidence);
            SceneKit.Set(pickup, "prompt", evidence.interactionPrompt);
            SceneKit.Set(pickup, "hideOnCollect", hideOnCollect);
        }
    }
}
