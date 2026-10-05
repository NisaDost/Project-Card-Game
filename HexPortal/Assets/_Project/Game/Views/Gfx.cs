using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>Shared graybox meshes, materials and palette (A-02). Created once.
    /// All meshes are procedural and every object is MeshFilter + MeshRenderer only: no GameObject.CreatePrimitive and
    /// no colliders, because the Physics module is stripped from player builds (CreatePrimitive adds a collider there).</summary>
    public static class Gfx
    {
        public enum Shape { Cube, Sphere, Capsule, Cylinder }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static Material opaque, transparent;
        static Mesh hexPrism, cube, sphere, capsule, cylinder;
        static MaterialPropertyBlock block;

        public static readonly Color Forest = new Color(0.30f, 0.70f, 0.30f);
        public static readonly Color Desert = new Color(0.95f, 0.60f, 0.20f);
        public static readonly Color Snow = new Color(0.70f, 0.88f, 1.00f);
        public static readonly Color NoBiome = new Color(0.75f, 0.45f, 0.90f);
        public static readonly Color Unknown = new Color(0.10f, 0.11f, 0.16f);
        public static readonly Color OwnerA = new Color(0.20f, 0.45f, 1.00f);
        public static readonly Color OwnerB = new Color(0.95f, 0.20f, 0.25f);
        public static readonly Color MoveHl = new Color(0.05f, 0.25f, 1.00f, 0.85f);
        public static readonly Color AttackHl = new Color(1.00f, 0.05f, 0.05f, 0.85f);
        public static readonly Color MoveAttackHl = new Color(0.85f, 0.30f, 0.00f, 1.00f);  // T-05 v3.2 move-attack target: burnt orange, darker than Desert tiles
        public static readonly Color MoveAttackOffHl = new Color(0.85f, 0.30f, 0.00f, 0.45f); // reachable but not affordable now
        public static readonly Color PreviewMoveHl = new Color(0.05f, 0.25f, 1.00f, 0.45f); // UX-10 preview (no command)
        public static readonly Color PreviewAttackHl = new Color(1.00f, 0.05f, 0.05f, 0.45f);
        public static readonly Color TargetHl = new Color(1.00f, 0.95f, 0.10f, 0.8f);
        public static readonly Color ZoneHl = new Color(0.55f, 0.95f, 1.00f, 0.35f);
        public static readonly Color EventHl = new Color(1.00f, 1.00f, 1.00f, 0.55f); // white pulse: orange is the move-attack colour
        public static readonly Color FlashHl = new Color(1.00f, 1.00f, 1.00f, 0.8f);
        public static readonly Color ShotHl = new Color(1.00f, 0.80f, 0.10f, 0.9f);
        public static readonly Color TrapHl = new Color(0.80f, 0.30f, 1.00f, 0.9f);
        public static readonly Color DeathHl = new Color(1.00f, 0.10f, 0.10f, 0.9f);

        public static Color BiomeColor(Biome b) =>
            b == Biome.Forest ? Forest : b == Biome.Desert ? Desert : b == Biome.Snow ? Snow : NoBiome;

        public static Color OwnerColor(PlayerId p) => p == PlayerId.A ? OwnerA : OwnerB;

        /// <summary>UX-09: Explored = desaturated and darker.</summary>
        public static Color Faded(Color c)
        {
            float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return Color.Lerp(new Color(g, g, g), c, 0.25f) * 0.6f;
        }

        // Material assets in Resources keep the URP Lit shader and its transparent variant in Player builds
        // (Shader.Find and runtime keywords alone may be stripped). The code fallback covers a missing asset.
        public static Material Opaque => opaque != null ? opaque : opaque = Load("HexPortalOpaque", false);
        public static Material Transparent => transparent != null ? transparent : transparent = Load("HexPortalGhost", true);

        static Material Load(string resource, bool transparentSurface)
        {
            var asset = Resources.Load<Material>(resource);
            if (asset != null) return new Material(asset);
            Debug.LogWarning("[Gfx] Missing Resources/" + resource + ".mat; building the material in code.");
            return MakeMaterial(transparentSurface);
        }

        static Material MakeMaterial(bool transparentSurface)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader);
            m.SetFloat("_Smoothness", 0.2f);
            if (transparentSurface)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return m;
        }

        public static void SetColor(Renderer r, Color c)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
        }

        /// <summary>Pointy-top hexagonal prism, outer radius 1, height 1 (y 0..1), flat normals.</summary>
        public static Mesh HexPrism => hexPrism != null ? hexPrism : hexPrism = Prism("HexPrism", 6, -30f, 0f, 1f, 1f);

        /// <summary>Unit-size meshes matching Unity's primitives (centred on the origin, size 1; cylinder height 2 and
        /// capsule height 2 like the built-in ones) so existing scales keep their look.</summary>
        public static Mesh MeshOf(Shape s)
        {
            switch (s)
            {
                case Shape.Cube: return cube != null ? cube : cube = Prism("Cube", 4, 45f, -0.5f, 0.5f, Mathf.Sqrt(0.5f));
                case Shape.Cylinder: return cylinder != null ? cylinder : cylinder = Prism("Cylinder", 20, 0f, -1f, 1f, 0.5f);
                case Shape.Capsule: return capsule != null ? capsule : capsule = Capsule();
                default: return sphere != null ? sphere : sphere = Capsule(0f, "Sphere");
            }
        }

        /// <summary>Regular n-gon prism with flat side normals, between y0 and y1.</summary>
        static Mesh Prism(string name, int sides, float startDeg, float y0, float y1, float radius)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            var c = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.Deg2Rad * (360f / sides * i + startDeg);
                c[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            var up = new Vector3(0f, y1, 0f);
            var down = new Vector3(0f, y0, 0f);
            int top = v.Count;
            v.Add(up); n.Add(Vector3.up);
            for (int i = 0; i < sides; i++) { v.Add(c[i] + up); n.Add(Vector3.up); }
            for (int i = 0; i < sides; i++) { t.Add(top); t.Add(top + 1 + (i + 1) % sides); t.Add(top + 1 + i); }
            int bottom = v.Count;
            v.Add(down); n.Add(Vector3.down);
            for (int i = 0; i < sides; i++) { v.Add(c[i] + down); n.Add(Vector3.down); }
            for (int i = 0; i < sides; i++) { t.Add(bottom); t.Add(bottom + 1 + i); t.Add(bottom + 1 + (i + 1) % sides); }
            for (int i = 0; i < sides; i++)
            {
                var a = c[i]; var b = c[(i + 1) % sides];
                var normal = ((a + b) * 0.5f).normalized;
                int s = v.Count;
                v.Add(a + down); v.Add(b + down); v.Add(b + up); v.Add(a + up);
                for (int k = 0; k < 4; k++) n.Add(normal);
                t.Add(s); t.Add(s + 2); t.Add(s + 1);
                t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
            return Build(name, v, n, t);
        }

        /// <summary>UV sphere of radius 0.5, stretched by a straight middle part of the given half length
        /// (0 = sphere, 0.5 = Unity capsule of height 2).</summary>
        static Mesh Capsule(float halfMiddle = 0.5f, string name = "Capsule")
        {
            const int Seg = 16, Rings = 12;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            for (int r = 0; r <= Rings; r++)
            {
                float phi = Mathf.PI * r / Rings;
                float y = Mathf.Cos(phi) * 0.5f;
                float ring = Mathf.Sin(phi) * 0.5f;
                float shift = r <= Rings / 2 ? halfMiddle : -halfMiddle;
                for (int s = 0; s <= Seg; s++)
                {
                    float th = 2f * Mathf.PI * s / Seg;
                    var dir = new Vector3(Mathf.Cos(th) * ring, y, Mathf.Sin(th) * ring);
                    v.Add(dir + new Vector3(0f, shift, 0f));
                    n.Add(dir.normalized);
                }
            }
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Seg; s++)
                {
                    int a = r * (Seg + 1) + s, b = a + Seg + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(b); t.Add(a + 1); t.Add(b + 1);
                }
            return Build(name, v, n, t);
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public static GameObject Hex(string name, Transform parent, Vector3 pos, float radius, float height, bool transparentSurface)
        {
            var go = Make(name, parent, pos, new Vector3(radius, height, radius), HexPrism);
            go.GetComponent<MeshRenderer>().sharedMaterial = transparentSurface ? Transparent : Opaque;
            return go;
        }

        public static GameObject Primitive(Shape shape, string name, Transform parent, Vector3 pos, Vector3 scale) =>
            Make(name, parent, pos, scale, MeshOf(shape));

        static GameObject Make(string name, Transform parent, Vector3 pos, Vector3 scale, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Opaque;
            return go;
        }
    }
}
