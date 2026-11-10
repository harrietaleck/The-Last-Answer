using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TLA.EditorTools.Environment
{
    /// <summary>
    /// Small toolkit for building placeholder ("blockout") environments from Unity's primitive
    /// shapes: boxes, cylinders, walls with door/window openings, lights and 3D text labels.
    /// Everything is created under a parent transform so scenes stay organised.
    /// </summary>
    internal sealed class Blockout
    {
        /// <summary>A hole in a wall: centre distance from the wall's start, width, and bottom/top heights.</summary>
        public readonly struct Opening
        {
            public readonly float Center, Width, Bottom, Top;

            public Opening(float center, float width, float bottom, float top)
            {
                Center = center; Width = width; Bottom = bottom; Top = top;
            }

            public static Opening Door(float center, float width = 1.0f, float height = 2.15f) => new Opening(center, width, 0f, height);
            public static Opening Window(float center, float width, float sill, float top) => new Opening(center, width, sill, top);
        }

        /// <summary>
        /// Builders specify light intensities on a simple 0-5 "feel" scale (1 = normal room light).
        /// URP lights fall off with the inverse square of distance, so they need much higher values
        /// than the old Built-in pipeline; this converts. Tune final values on the lights in each scene.
        /// </summary>
        public const float UrpIntensityScale = 8f;

        private readonly Transform _root;

        public Blockout(Transform root)
        {
            _root = root;
        }

        public Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            return go.transform;
        }

        /// <summary>Axis-aligned box. Decorative props should pass collider:false.</summary>
        public GameObject Box(string name, Vector3 center, Vector3 size, Material material, Transform parent = null,
            bool collider = true, bool isStatic = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Setup(go, name, center, size, material, parent, collider, isStatic);
            return go;
        }

        public GameObject RotatedBox(string name, Vector3 center, Vector3 size, Vector3 euler, Material material,
            Transform parent = null, bool collider = true)
        {
            GameObject go = Box(name, center, size, material, parent, collider);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        /// <summary>Upright cylinder with the given radius and height, centred at <paramref name="center"/>.</summary>
        public GameObject Cylinder(string name, Vector3 center, float radius, float height, Material material,
            Transform parent = null, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Unity's cylinder is 2 units tall and 1 unit wide.
            Setup(go, name, center, new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, parent, collider, true);
            return go;
        }

        /// <summary>A thin cylinder between two points (cables, evidence string, table legs).</summary>
        public GameObject Rod(string name, Vector3 from, Vector3 to, float radius, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Vector3 mid = (from + to) * 0.5f;
            float length = Vector3.Distance(from, to);
            Setup(go, name, mid, new Vector3(radius * 2f, length * 0.5f, radius * 2f), material, parent, false, true);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
            return go;
        }

        /// <summary>One-sided flat quad (visible from the side its front faces). Never has a collider.</summary>
        public GameObject Quad(string name, Vector3 center, Vector2 size, Quaternion rotation, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Setup(go, name, center, new Vector3(size.x, size.y, 1f), material, parent, false, true);
            go.transform.localRotation = rotation;
            return go;
        }

        /// <summary>
        /// A straight wall from <paramref name="start"/> to <paramref name="end"/> (floor level) with optional
        /// door/window openings. Pieces are placed around each opening.
        /// </summary>
        public Transform Wall(string name, Vector3 start, Vector3 end, float height, float thickness, Material material,
            params Opening[] openings)
        {
            Transform group = Group(name);
            Vector3 dir = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, dir);

            var sorted = new List<Opening>(openings);
            sorted.Sort((a, b) => a.Center.CompareTo(b.Center));

            float cursor = 0f;
            int piece = 0;
            foreach (Opening o in sorted)
            {
                float left = o.Center - o.Width * 0.5f;
                float right = o.Center + o.Width * 0.5f;
                Segment(cursor, left, 0f, height);                       // solid wall up to the opening
                if (o.Bottom > 0f) Segment(left, right, 0f, o.Bottom);   // below a window
                if (o.Top < height) Segment(left, right, o.Top, height); // above a door/window
                cursor = right;
            }
            Segment(cursor, length, 0f, height);
            return group;

            void Segment(float a, float b, float y0, float y1)
            {
                if (b - a < 0.001f || y1 - y0 < 0.001f) return;
                Vector3 center = start + dir * ((a + b) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
                GameObject go = Box($"{name}_{piece++}", center, new Vector3(b - a, y1 - y0, thickness), material, group);
                go.transform.localRotation = rotation;
            }
        }

        public Light PointLight(string name, Vector3 position, Color color, float intensity, float range,
            bool shadows = false, Transform parent = null)
        {
            Light light = CreateLight(name, position, LightType.Point, color, intensity, range, shadows, parent);
            return light;
        }

        public Light SpotLight(string name, Vector3 position, Vector3 euler, Color color, float intensity, float range,
            float angle, bool shadows = true, Transform parent = null)
        {
            Light light = CreateLight(name, position, LightType.Spot, color, intensity, range, shadows, parent);
            light.transform.localRotation = Quaternion.Euler(euler);
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.45f; // soft edge
            return light;
        }

        /// <summary>Ceiling-mounted fluorescent panel: an emissive fixture plus a point light just below it.</summary>
        public Light CeilingPanel(string name, Vector3 position, float ceilingY, Color color, float intensity, float range,
            bool shadows = false, Transform parent = null)
        {
            Transform group = Group(name, parent);
            Box("Housing", new Vector3(position.x, ceilingY - 0.02f, position.z), new Vector3(1.24f, 0.04f, 0.34f), EnvMaterials.Trim, group, false);
            Box("Diffuser", new Vector3(position.x, ceilingY - 0.045f, position.z), new Vector3(1.18f, 0.012f, 0.28f), EnvMaterials.LightCool, group, false);
            return PointLight("Light", new Vector3(position.x, ceilingY - 0.25f, position.z), color, intensity, range, shadows, group);
        }

        /// <summary>World-space 3D text label (signs, file labels, photo captions).</summary>
        public TextMeshPro Label(string name, Vector3 position, Quaternion rotation, string text, float fontSize,
            Color color, Transform parent = null, float width = 2f, float height = 0.5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            return tmp;
        }

        /// <summary>Simple office chair: seat, back and a central post. Faces +Z unless rotated.</summary>
        public Transform Chair(string name, Vector3 position, float yaw, Material seatMaterial, Transform parent = null)
        {
            Transform chair = Group(name, parent);
            chair.localPosition = position;
            chair.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.46f, 0.06f, 0.44f), seatMaterial, chair);
            Box("Back", new Vector3(0f, 0.78f, -0.2f), new Vector3(0.44f, 0.5f, 0.05f), seatMaterial, chair);
            Box("Post", new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.44f, 0.05f), EnvMaterials.Trim, chair, false);
            Box("Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.04f, 0.5f), EnvMaterials.Trim, chair, false);
            return chair;
        }

        /// <summary>Table/desk with a top and four legs. Size = top footprint and height.</summary>
        public Transform Table(string name, Vector3 position, Vector3 size, Material topMaterial, Material legMaterial,
            Transform parent = null, float yaw = 0f)
        {
            Transform table = Group(name, parent);
            table.localPosition = position;
            table.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float t = 0.04f;
            Box("Top", new Vector3(0f, size.y - t * 0.5f, 0f), new Vector3(size.x, t, size.z), topMaterial, table);
            float lx = size.x * 0.5f - 0.05f, lz = size.z * 0.5f - 0.05f, lh = size.y - t;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                Box($"Leg_{i}", new Vector3(lx * sx, lh * 0.5f, lz * sz), new Vector3(0.05f, lh, 0.05f), legMaterial, table, false);
            }
            return table;
        }

        /// <summary>Filing cabinet with drawer fronts.</summary>
        public Transform FilingCabinet(string name, Vector3 position, float yaw, int drawers = 4, Transform parent = null)
        {
            Transform cab = Group(name, parent);
            cab.localPosition = position;
            cab.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float h = drawers * 0.33f;
            Box("Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(0.47f, h, 0.62f), EnvMaterials.MetalCabinet, cab);
            for (int i = 0; i < drawers; i++)
            {
                float y = 0.33f * i + 0.165f;
                Box($"Drawer_{i}", new Vector3(0f, y, 0.315f), new Vector3(0.42f, 0.29f, 0.02f), EnvMaterials.MetalCabinet, cab, false);
                Box($"Handle_{i}", new Vector3(0f, y + 0.08f, 0.33f), new Vector3(0.12f, 0.02f, 0.02f), EnvMaterials.Trim, cab, false);
            }
            return cab;
        }

        /// <summary>A desktop computer monitor with a softly glowing screen. Faces +Z unless rotated.</summary>
        public Transform Monitor(string name, Vector3 position, float yaw, Transform parent = null)
        {
            Transform mon = Group(name, parent);
            mon.localPosition = position;
            mon.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Stand", new Vector3(0f, 0.12f, -0.03f), new Vector3(0.05f, 0.24f, 0.05f), EnvMaterials.Trim, mon, false);
            Box("Foot", new Vector3(0f, 0.01f, -0.03f), new Vector3(0.22f, 0.02f, 0.16f), EnvMaterials.Trim, mon, false);
            Box("Bezel", new Vector3(0f, 0.36f, 0f), new Vector3(0.56f, 0.34f, 0.03f), EnvMaterials.Trim, mon, false);
            Box("Screen", new Vector3(0f, 0.36f, 0.016f), new Vector3(0.52f, 0.3f, 0.005f), EnvMaterials.Screen, mon, false);
            return mon;
        }

        private void Setup(GameObject go, string name, Vector3 center, Vector3 scale, Material material, Transform parent,
            bool collider, bool isStatic)
        {
            go.name = name;
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        private Light CreateLight(string name, Vector3 position, LightType type, Color color, float intensity, float range,
            bool shadows, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity * UrpIntensityScale;
            light.range = range;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.85f;
            if (!go.TryGetComponent<UniversalAdditionalLightData>(out _)) go.AddComponent<UniversalAdditionalLightData>();
            return light;
        }
    }
}
