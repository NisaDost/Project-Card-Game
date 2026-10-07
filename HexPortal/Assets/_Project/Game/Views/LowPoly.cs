using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>A-01…A-03 procedural low-poly meshes: flat-shaded (every triangle has its own vertices and normal).
    /// Each mesh is built once on first use and shared by every renderer (static cache).</summary>
    public static class LowPoly
    {
        sealed class Builder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<int> t = new List<int>();
            /// <summary>Centre of the convex part being built: triangles are wound to face away from it (back-face culling).</summary>
            public Vector3 Centre;

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) return;
                normal.Normalize();
                if (Vector3.Dot(normal, (a + b + c) / 3f - Centre) < 0f) { var tmp = b; b = c; c = tmp; normal = -normal; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(normal); n.Add(normal); n.Add(normal);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

            /// <summary>Frustum prism (r1 = 0 gives a cone) around the y axis, between y0 and y1.</summary>
            public void Prism(int sides, float r0, float r1, float y0, float y1, Vector3 at, float startDeg = 0f, bool caps = true)
            {
                Centre = at + Vector3.up * ((y0 + y1) * 0.5f);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = Mathf.Deg2Rad * (startDeg + 360f / sides * i), a1 = Mathf.Deg2Rad * (startDeg + 360f / sides * (i + 1));
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 b0 = at + d0 * r0 + Vector3.up * y0, b1 = at + d1 * r0 + Vector3.up * y0;
                    Vector3 u0 = at + d0 * r1 + Vector3.up * y1, u1 = at + d1 * r1 + Vector3.up * y1;
                    Quad(b0, u0, u1, b1);
                    if (caps)
                    {
                        if (r1 > 0f) Tri(at + Vector3.up * y1, u1, u0);
                        Tri(at + Vector3.up * y0, b0, b1);
                    }
                }
            }

            /// <summary>Low-poly ball (lat-long, few segments) with per-axis radius.</summary>
            public void Ball(Vector3 c, Vector3 r, int seg = 6, int rings = 4)
            {
                Centre = c;
                for (int i = 0; i < rings; i++)
                {
                    float p0 = Mathf.PI * i / rings, p1 = Mathf.PI * (i + 1) / rings;
                    for (int s = 0; s < seg; s++)
                    {
                        float a0 = 2f * Mathf.PI * s / seg, a1 = 2f * Mathf.PI * (s + 1) / seg;
                        var q00 = P(c, r, p0, a0); var q01 = P(c, r, p0, a1);
                        var q10 = P(c, r, p1, a0); var q11 = P(c, r, p1, a1);
                        if (i == 0) Tri(q00, q11, q10);
                        else if (i == rings - 1) Tri(q00, q01, q10);
                        else Quad(q00, q01, q11, q10);
                    }
                }
            }

            static Vector3 P(Vector3 c, Vector3 r, float phi, float th) =>
                c + new Vector3(Mathf.Sin(phi) * Mathf.Cos(th) * r.x, Mathf.Cos(phi) * r.y, Mathf.Sin(phi) * Mathf.Sin(th) * r.z);

            /// <summary>Box with the given centre, size and rotation.</summary>
            public void Box(Vector3 c, Vector3 size, Quaternion rot)
            {
                Centre = c;
                var h = size * 0.5f;
                Vector3 X(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Vector3 a = X(-1, -1, -1), b = X(1, -1, -1), cc = X(1, 1, -1), d = X(-1, 1, -1);
                Vector3 e = X(-1, -1, 1), f = X(1, -1, 1), g = X(1, 1, 1), k = X(-1, 1, 1);
                Quad(a, d, cc, b); Quad(f, g, k, e); Quad(e, k, d, a); Quad(b, cc, g, f); Quad(d, k, g, cc); Quad(a, b, f, e);
            }

            public void Box(Vector3 c, Vector3 size) => Box(c, size, Quaternion.identity);

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

        static Mesh Cached(int key, System.Func<Mesh> make)
        {
            if (!cache.TryGetValue(key, out var m) || m == null) cache[key] = m = make();
            return m;
        }

        /// <summary>Unit body for a class (feet at y = 0, facing +z). kind 100 = tower.</summary>
        public static Mesh Body(int kind) => Cached(kind, () => MakeBody(kind));

        public static Mesh Tile => Cached(200, MakeTile);
        public static Mesh Cloud => Cached(201, MakeCloud);
        public static Mesh Rune => Cached(202, MakeRune);
        public static Mesh Wellspring => Cached(203, MakeWell);
        public static Mesh Portal => Cached(204, MakePortal);
        public static Mesh Rock => Cached(205, MakeRock);
        public static Mesh Trap => Cached(206, MakeTrap);

        static Mesh MakeBody(int kind)
        {
            var b = new Builder();
            switch (kind)
            {
                case (int)UnitClass.Guardian:
                    b.Prism(6, 0.4f, 0.34f, 0.1f, 0.8f, Vector3.zero, 30f);            // stocky body
                    b.Ball(new Vector3(0f, 0.98f, 0f), new Vector3(0.22f, 0.2f, 0.22f));
                    b.Prism(6, 0.24f, 0.1f, 1.02f, 1.22f, Vector3.zero, 30f);           // helmet
                    b.Box(new Vector3(0f, 0.5f, 0.42f), new Vector3(0.62f, 0.72f, 0.1f)); // shield slab
                    b.Box(new Vector3(0f, 0.5f, 0.48f), new Vector3(0.14f, 0.5f, 0.06f)); // shield boss
                    break;
                case (int)UnitClass.Rider:
                    b.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.32f, 0.85f));   // horse body
                    for (int i = 0; i < 4; i++)
                        b.Box(new Vector3(i % 2 == 0 ? -0.12f : 0.12f, 0.17f, i < 2 ? -0.3f : 0.3f), new Vector3(0.1f, 0.34f, 0.1f));
                    b.Box(new Vector3(0f, 0.75f, 0.46f), new Vector3(0.2f, 0.4f, 0.18f), Quaternion.Euler(-30f, 0f, 0f)); // neck
                    b.Box(new Vector3(0f, 0.92f, 0.6f), new Vector3(0.18f, 0.16f, 0.3f));                             // horse head
                    b.Prism(5, 0.16f, 0.12f, 0.62f, 1.05f, new Vector3(0f, 0f, -0.05f));                              // rider
                    b.Ball(new Vector3(0f, 1.17f, -0.05f), new Vector3(0.13f, 0.13f, 0.13f));
                    b.Box(new Vector3(0.18f, 1.0f, 0.2f), new Vector3(0.05f, 0.05f, 0.7f), Quaternion.Euler(-15f, 0f, 0f)); // lance
                    break;
                case (int)UnitClass.Archer:
                    b.Prism(5, 0.22f, 0.15f, 0f, 0.85f, Vector3.zero);
                    b.Ball(new Vector3(0f, 0.98f, 0f), new Vector3(0.15f, 0.15f, 0.15f));
                    b.Prism(5, 0.18f, 0f, 0.98f, 1.3f, Vector3.zero);                   // hood
                    for (int i = 0; i < 5; i++)                                           // bow arc
                    {
                        float a = Mathf.Lerp(-60f, 60f, i / 4f) * Mathf.Deg2Rad;
                        var p = new Vector3(0.32f, 0.65f + Mathf.Sin(a) * 0.42f, 0.12f + Mathf.Cos(a) * 0.18f);
                        b.Box(p, new Vector3(0.05f, 0.2f, 0.05f), Quaternion.Euler(-Mathf.Rad2Deg * a, 0f, 0f));
                    }
                    b.Box(new Vector3(0.32f, 0.65f, 0.12f), new Vector3(0.02f, 0.78f, 0.02f)); // string
                    break;
                case (int)UnitClass.Mage:
                    b.Prism(8, 0.42f, 0.1f, 0f, 0.95f, Vector3.zero, 22.5f);            // robe cone
                    b.Ball(new Vector3(0f, 1.03f, 0f), new Vector3(0.16f, 0.16f, 0.16f));
                    b.Prism(6, 0.26f, 0f, 1.1f, 1.55f, Vector3.zero);                   // hat
                    b.Box(new Vector3(0.36f, 0.6f, 0.1f), new Vector3(0.05f, 1.1f, 0.05f)); // staff
                    b.Ball(new Vector3(0.36f, 1.22f, 0.1f), new Vector3(0.13f, 0.13f, 0.13f), 5, 3); // orb
                    break;
                case 100: // tower
                    b.Prism(6, 0.62f, 0.5f, 0f, 1.45f, Vector3.zero, 30f);
                    b.Prism(6, 0.62f, 0.62f, 1.45f, 1.6f, Vector3.zero, 30f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Deg2Rad * (60f * i);
                        b.Box(new Vector3(Mathf.Cos(a) * 0.52f, 1.72f, Mathf.Sin(a) * 0.52f), new Vector3(0.2f, 0.24f, 0.2f), Quaternion.Euler(0f, -60f * i, 0f));
                    }
                    b.Prism(6, 0.4f, 0f, 1.6f, 2.15f, Vector3.zero, 30f);                // roof
                    b.Box(new Vector3(0f, 0.3f, 0.6f), new Vector3(0.26f, 0.5f, 0.06f)); // door
                    break;
                default: // Healer
                    b.Ball(new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.4f, 0.36f), 7, 5); // rounded body
                    b.Ball(new Vector3(0f, 0.92f, 0f), new Vector3(0.17f, 0.17f, 0.17f));
                    b.Box(new Vector3(0f, 0.5f, 0.36f), new Vector3(0.1f, 0.34f, 0.06f));     // cross
                    b.Box(new Vector3(0f, 0.54f, 0.36f), new Vector3(0.3f, 0.1f, 0.06f));
                    for (int i = 0; i < 8; i++)                                               // halo
                    {
                        float a = Mathf.Deg2Rad * (45f * i);
                        b.Box(new Vector3(Mathf.Cos(a) * 0.2f, 1.18f, Mathf.Sin(a) * 0.2f), new Vector3(0.1f, 0.03f, 0.04f), Quaternion.Euler(0f, -45f * i + 90f, 0f));
                    }
                    break;
            }
            return b.Build("Body" + kind);
        }

        /// <summary>Pointy-top hex tile, outer radius 1, height 1 (y 0..1) with a bevelled top rim.</summary>
        static Mesh MakeTile()
        {
            var b = new Builder();
            b.Prism(6, 1f, 1f, 0f, 0.82f, Vector3.zero, -30f, false);
            b.Prism(6, 1f, 0.86f, 0.82f, 1f, Vector3.zero, -30f, false);
            b.Centre = new Vector3(0f, 0.5f, 0f);
            for (int i = 0; i < 6; i++) // top cap
            {
                float a0 = Mathf.Deg2Rad * (-30f + 60f * i), a1 = Mathf.Deg2Rad * (30f + 60f * i);
                b.Tri(Vector3.up, new Vector3(Mathf.Cos(a1) * 0.86f, 1f, Mathf.Sin(a1) * 0.86f), new Vector3(Mathf.Cos(a0) * 0.86f, 1f, Mathf.Sin(a0) * 0.86f));
            }
            return b.Build("Tile");
        }

        static Mesh MakeCloud()
        {
            var b = new Builder();
            b.Ball(new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.38f, 0.6f), 7, 4);
            b.Ball(new Vector3(0.45f, 0.15f, 0.2f), new Vector3(0.42f, 0.3f, 0.42f), 6, 3);
            b.Ball(new Vector3(-0.4f, 0.17f, 0.28f), new Vector3(0.4f, 0.28f, 0.4f), 6, 3);
            b.Ball(new Vector3(0.05f, 0.14f, -0.5f), new Vector3(0.44f, 0.28f, 0.4f), 6, 3);
            b.Ball(new Vector3(-0.35f, 0.12f, -0.3f), new Vector3(0.36f, 0.24f, 0.36f), 5, 3);
            return b.Build("Cloud");
        }

        static Mesh MakeRune()
        {
            var b = new Builder();
            b.Prism(5, 0.3f, 0.32f, 0f, 0.1f, Vector3.zero);
            b.Prism(4, 0.18f, 0.12f, 0.1f, 0.62f, Vector3.zero, 45f);
            b.Prism(4, 0.12f, 0f, 0.62f, 0.8f, Vector3.zero, 45f);
            return b.Build("Rune");
        }

        static Mesh MakeWell()
        {
            var b = new Builder();
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Deg2Rad * (45f * i);
                b.Box(new Vector3(Mathf.Cos(a) * 0.45f, 0.06f, Mathf.Sin(a) * 0.45f), new Vector3(0.36f, 0.12f, 0.14f), Quaternion.Euler(0f, -45f * i + 90f, 0f));
            }
            b.Prism(8, 0.4f, 0.4f, 0f, 0.05f, Vector3.zero, 22.5f);
            b.Ball(new Vector3(0f, 0.18f, 0f), new Vector3(0.1f, 0.14f, 0.1f), 5, 3);
            return b.Build("Wellspring");
        }

        static Mesh MakePortal()
        {
            var b = new Builder();
            b.Prism(8, 0.75f, 0.7f, 0f, 0.06f, Vector3.zero, 22.5f);
            for (int i = 0; i < 10; i++) // standing ring
            {
                float a0 = Mathf.PI * 2f * i / 10f;
                var p = new Vector3(Mathf.Cos(a0) * 0.55f, 0.65f + Mathf.Sin(a0) * 0.55f, 0f);
                b.Box(p, new Vector3(0.36f, 0.1f, 0.12f), Quaternion.Euler(0f, 0f, Mathf.Rad2Deg * a0 + 90f));
            }
            return b.Build("Portal");
        }

        /// <summary>UX-14 own trap: a flat plate with small spikes.</summary>
        static Mesh MakeTrap()
        {
            var b = new Builder();
            b.Prism(6, 0.45f, 0.42f, 0f, 0.05f, Vector3.zero, 30f);
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i);
                b.Prism(4, 0.07f, 0f, 0.05f, 0.3f, new Vector3(Mathf.Cos(a) * 0.25f, 0f, Mathf.Sin(a) * 0.25f), 45f);
            }
            b.Prism(4, 0.08f, 0f, 0.05f, 0.36f, Vector3.zero, 45f);
            return b.Build("Trap");
        }

        static Mesh MakeRock()
        {
            var b = new Builder();
            b.Prism(5, 0.7f, 0.45f, 0f, 0.5f, Vector3.zero, 10f);
            b.Prism(5, 0.45f, 0.18f, 0.5f, 0.85f, new Vector3(0.05f, 0f, -0.05f), 40f);
            b.Prism(4, 0.3f, 0.1f, 0f, 0.45f, new Vector3(0.55f, 0f, 0.3f), 20f);
            return b.Build("Rock");
        }
    }
}
