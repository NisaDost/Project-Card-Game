using System.Collections.Generic;
using HexPortal.Core;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>Tiles, markers, fog (UX-09) and cell highlights, drawn only from a PlayerView.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float TileHeight = 0.3f;
        const float TileRadius = 0.95f;

        readonly Renderer[] tiles = new Renderer[Board.Cells.Count];
        readonly GameObject[] clouds = new GameObject[Board.Cells.Count];
        readonly GameObject[][] markers = new GameObject[Board.Cells.Count][];
        readonly Renderer[] highlights = new Renderer[Board.Cells.Count];
        readonly Color[] highlightColor = new Color[Board.Cells.Count];
        readonly bool[] eventCell = new bool[Board.Cells.Count];
        readonly float[] flashUntil = new float[Board.Cells.Count];
        readonly Color[] flashColor = new Color[Board.Cells.Count];
        bool anyEvent, flashing;

        public int HighlightCount { get; private set; }

        public static BoardView Create(Transform parent)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BoardView>();
            v.Build();
            return v;
        }

        void Build()
        {
            var cells = Board.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                var pos = HexLayout.ToWorld(cells[i]);
                var tile = Gfx.Hex("Tile " + cells[i], transform, pos, TileRadius, TileHeight, false);
                tile.GetComponent<MeshFilter>().sharedMesh = LowPoly.Tile; // A-03 bevelled low-poly tile
                // Subtle deterministic height variation per cell (visual only; units still stand on TileHeight).
                uint hsh = (uint)(cells[i].Q * 73856093) ^ (uint)(cells[i].R * 19349663);
                tile.transform.localScale = new Vector3(TileRadius, TileHeight * (0.88f + (hsh % 7) * 0.02f), TileRadius);
                tiles[i] = tile.GetComponent<Renderer>();

                var cloud = Gfx.Primitive(Gfx.Shape.Sphere, "Cloud", tile.transform.parent, pos + new Vector3(0f, TileHeight + 0.1f, 0f),
                    new Vector3(1.25f, 0.9f, 1.25f));
                cloud.GetComponent<MeshFilter>().sharedMesh = LowPoly.Cloud; // UX-09 low-poly fog puffs
                cloud.transform.localRotation = Quaternion.Euler(0f, (i * 47) % 360, 0f);
                Gfx.SetColor(cloud.GetComponent<Renderer>(), new Color(0.16f, 0.17f, 0.24f));
                clouds[i] = cloud;

                var top = pos + new Vector3(0f, TileHeight, 0f);
                var rune = Prop("Rune", top, LowPoly.Rune, i);
                Gfx.SetColor(rune.GetComponent<Renderer>(), new Color(0.55f, 0.25f, 0.85f));
                var well = Prop("Wellspring", top, LowPoly.Wellspring, i);
                Gfx.SetColor(well.GetComponent<Renderer>(), new Color(0.1f, 0.85f, 0.95f));
                var portal = Prop("Portal", top, LowPoly.Portal, 0);
                Gfx.SetColor(portal.GetComponent<Renderer>(), new Color(0.95f, 0.3f, 0.9f));
                var rock = Prop("Rock", top, LowPoly.Rock, i);
                Gfx.SetColor(rock.GetComponent<Renderer>(), new Color(0.35f, 0.33f, 0.32f));
                markers[i] = new[] { null, rune, well, portal, rock }; // index = (int)Marker

                var hl = Gfx.Hex("Highlight", transform, pos + new Vector3(0f, TileHeight + 0.01f, 0f), 0.7f, 0.08f, true);
                highlights[i] = hl.GetComponent<Renderer>();
                hl.SetActive(false);
            }
        }

        GameObject Prop(string name, Vector3 at, Mesh mesh, int seed)
        {
            var go = Gfx.Hex(name, transform, at, 1f, 1f, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.localRotation = Quaternion.Euler(0f, (seed * 61) % 360, 0f);
            return go;
        }

        public void Render(PlayerView view)
        {
            for (int i = 0; i < view.Cells.Count; i++)
            {
                var c = view.Cells[i];
                bool known = c.TerrainKnown;
                var color = known ? Gfx.BiomeColor(c.Tile.Biome) : Gfx.Unknown;
                if (c.Visibility == CellVisibility.Explored) color = Gfx.Faded(color);
                Gfx.SetColor(tiles[i], color);
                clouds[i].SetActive(c.Visibility == CellVisibility.Hidden);
                var m = known ? c.Tile.Marker : Marker.None;
                for (int k = 1; k < markers[i].Length; k++) markers[i][k].SetActive((int)m == k);
            }
            System.Array.Clear(eventCell, 0, eventCell.Length);
            anyEvent = false;
            if (view.AnnouncedEvent != null)
                foreach (var h in view.AnnouncedEventCells)
                {
                    int i = Board.IndexOf(h);
                    if (i >= 0) { eventCell[i] = true; anyEvent = true; }
                }
            ApplyHighlights();
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < highlightColor.Length; i++) highlightColor[i] = Color.clear;
            HighlightCount = 0;
            ApplyHighlights();
        }

        public void Highlight(Hex h, Color c)
        {
            int i = Board.IndexOf(h);
            if (i < 0) return;
            if (highlightColor[i].a <= 0f) HighlightCount++;
            highlightColor[i] = c;
            ApplyHighlights();
        }

        public void Flash(Hex h) => Flash(h, Gfx.FlashHl);

        /// <summary>A short coloured flash on a cell (trap, shot, death highlights; UX-10).</summary>
        public void Flash(Hex h, Color color)
        {
            int i = Board.IndexOf(h);
            if (i < 0) return;
            flashUntil[i] = Time.time + 0.9f;
            flashColor[i] = color;
            flashing = true;
        }

        void ApplyHighlights()
        {
            for (int i = 0; i < highlights.Length; i++)
            {
                bool on = highlightColor[i].a > 0f || eventCell[i];
                highlights[i].gameObject.SetActive(on);
                if (on) Gfx.SetColor(highlights[i], highlightColor[i].a > 0f ? highlightColor[i] : Gfx.EventHl);
            }
        }

        void Update()
        {
            // UX-06 pulse for announced event cells, and short flashes for traps/shots. No allocations.
            if (!anyEvent && !flashing) return; // nothing pulsing or flashing
            bool stillFlashing = false;
            float pulse = 0.35f + 0.3f * Mathf.Sin(Time.time * 5f);
            for (int i = 0; i < highlights.Length; i++)
            {
                bool flash = flashUntil[i] > Time.time;
                if (flash)
                {
                    stillFlashing = true;
                    highlights[i].gameObject.SetActive(true);
                    var fc = flashColor[i];
                    fc.a *= 0.55f + 0.45f * Mathf.Sin(Time.time * 18f);
                    Gfx.SetColor(highlights[i], fc);
                }
                else if (flashUntil[i] > 0f)
                {
                    flashUntil[i] = 0f;
                    ApplyHighlights();
                }
                else if (anyEvent && eventCell[i] && highlightColor[i].a <= 0f)
                {
                    var c = Gfx.EventHl;
                    c.a = pulse;
                    Gfx.SetColor(highlights[i], c);
                }
            }
            flashing = stillFlashing;
        }
    }
}
