using System.Collections.Generic;
using HexPortal.Core;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>UX-10 / T-12 plan arrows: client-only marks drawn on the opponent's turn. No commands, no state.
    /// A fixed pool of arrow objects is created up front; a mesh is rebuilt only when an arrow is added.</summary>
    public sealed class PlanArrows
    {
        public const int Max = 8;
        static readonly Color ArrowColor = new Color(1f, 1f, 1f, 0.9f);

        sealed class Arrow
        {
            public GameObject Go;
            public Mesh Mesh;
            public Hex From, To;
        }

        readonly Arrow[] pool = new Arrow[Max];
        readonly List<Arrow> active = new List<Arrow>(Max);
        readonly Vector3[] verts = new Vector3[7];
        static readonly int[] Tris = { 0, 1, 2, 0, 2, 3, 4, 5, 6 };

        public int Count => active.Count;

        public PlanArrows(Transform parent)
        {
            for (int i = 0; i < Max; i++)
            {
                var go = new GameObject("PlanArrow");
                go.transform.SetParent(parent, false);
                var mesh = new Mesh { name = "PlanArrow" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Gfx.Opaque;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Gfx.SetColor(r, ArrowColor);
                go.SetActive(false);
                pool[i] = new Arrow { Go = go, Mesh = mesh };
            }
        }

        public bool Has(Hex from, Hex to)
        {
            foreach (var a in active)
                if (a.From == from && a.To == to) return true;
            return false;
        }

        /// <summary>Adds an arrow; when the pool is full the oldest arrow is reused.</summary>
        public void Add(Hex from, Hex to)
        {
            if (from == to || Has(from, to)) return;
            Arrow a = null;
            foreach (var p in pool)
                if (!p.Go.activeSelf) { a = p; break; }
            if (a == null) { a = active[0]; active.RemoveAt(0); }
            a.From = from; a.To = to;
            Build(a);
            a.Go.SetActive(true);
            active.Add(a);
        }

        /// <summary>Removes the arrow that ends on the cell (a tap on its head). Returns false if none.</summary>
        public bool RemoveEndingAt(Hex cell)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].To == cell)
                {
                    active[i].Go.SetActive(false);
                    active.RemoveAt(i);
                    return true;
                }
            return false;
        }

        public void Clear()
        {
            foreach (var a in active) a.Go.SetActive(false);
            active.Clear();
        }

        void Build(Arrow a)
        {
            const float y = BoardView.TileHeight + 0.25f, half = 0.14f, headHalf = 0.38f, headLen = 0.6f;
            var p0 = HexLayout.ToWorld(a.From, y);
            var p1 = HexLayout.ToWorld(a.To, y);
            var d = p1 - p0; d.y = 0f;
            float len = d.magnitude;
            var dir = d / len;
            var side = new Vector3(-dir.z, 0f, dir.x);
            var s0 = p0 + dir * 0.3f;
            var neck = p1 - dir * Mathf.Min(headLen, len * 0.5f);
            verts[0] = s0 - side * half; verts[1] = s0 + side * half;
            verts[2] = neck + side * half; verts[3] = neck - side * half;
            verts[4] = neck - side * headHalf; verts[5] = neck + side * headHalf; verts[6] = p1;
            a.Mesh.Clear();
            a.Mesh.vertices = verts;
            a.Mesh.triangles = Tris;
            a.Mesh.RecalculateNormals();
            a.Mesh.RecalculateBounds();
        }
    }
}
