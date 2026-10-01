using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>Shared graybox meshes, materials and palette (A-02). Created once.</summary>
    public static class Gfx
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static Material opaque, transparent;
        static Mesh hexPrism;
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
        public static readonly Color TargetHl = new Color(1.00f, 0.95f, 0.10f, 0.8f);
        public static readonly Color ZoneHl = new Color(0.55f, 0.95f, 1.00f, 0.35f);
        public static readonly Color EventHl = new Color(1.00f, 0.55f, 0.10f, 0.6f);
        public static readonly Color FlashHl = new Color(1.00f, 1.00f, 1.00f, 0.8f);

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
        public static Mesh HexPrism
        {
            get
            {
                if (hexPrism != null) return hexPrism;
                var v = new System.Collections.Generic.List<Vector3>();
                var n = new System.Collections.Generic.List<Vector3>();
                var t = new System.Collections.Generic.List<int>();
                var c = new Vector3[6];
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Deg2Rad * (60f * i - 30f);
                    c[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                }
                int top = v.Count;
                v.Add(Vector3.up); n.Add(Vector3.up);
                for (int i = 0; i < 6; i++) { v.Add(c[i] + Vector3.up); n.Add(Vector3.up); }
                for (int i = 0; i < 6; i++) { t.Add(top); t.Add(top + 1 + (i + 1) % 6); t.Add(top + 1 + i); }
                for (int i = 0; i < 6; i++)
                {
                    var a = c[i]; var b = c[(i + 1) % 6];
                    var normal = ((a + b) * 0.5f).normalized;
                    int s = v.Count;
                    v.Add(a); v.Add(b); v.Add(b + Vector3.up); v.Add(a + Vector3.up);
                    for (int k = 0; k < 4; k++) n.Add(normal);
                    t.Add(s); t.Add(s + 2); t.Add(s + 1);
                    t.Add(s); t.Add(s + 3); t.Add(s + 2);
                }
                hexPrism = new Mesh { name = "HexPrism" };
                hexPrism.SetVertices(v);
                hexPrism.SetNormals(n);
                hexPrism.SetTriangles(t, 0);
                hexPrism.RecalculateBounds();
                return hexPrism;
            }
        }

        public static GameObject Hex(string name, Transform parent, Vector3 pos, float radius, float height, bool transparentSurface)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = HexPrism;
            go.AddComponent<MeshRenderer>().sharedMaterial = transparentSurface ? Transparent : Opaque;
            return go;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Opaque;
            return go;
        }
    }
}
