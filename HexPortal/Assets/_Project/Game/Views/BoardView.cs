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

        readonly Renderer[] tiles = new Renderer[59];
        readonly GameObject[] clouds = new GameObject[59];
        readonly GameObject[][] markers = new GameObject[59][];
        readonly Renderer[] highlights = new Renderer[59];
        readonly Color[] highlightColor = new Color[59];
        readonly bool[] eventCell = new bool[59];
        readonly float[] flashUntil = new float[59];
        bool anyEvent;

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
                tiles[i] = tile.GetComponent<Renderer>();

                var cloud = Gfx.Primitive(PrimitiveType.Sphere, "Cloud", tile.transform.parent, pos + new Vector3(0f, TileHeight + 0.1f, 0f),
                    new Vector3(1.8f, 0.5f, 1.8f));
                Gfx.SetColor(cloud.GetComponent<Renderer>(), new Color(0.16f, 0.17f, 0.24f));
                clouds[i] = cloud;

                var top = pos + new Vector3(0f, TileHeight, 0f);
                var rune = Gfx.Hex("Rune", transform, top, 0.25f, 0.6f, false);
                Gfx.SetColor(rune.GetComponent<Renderer>(), new Color(0.55f, 0.25f, 0.85f));
                var well = Gfx.Hex("Wellspring", transform, top, 0.55f, 0.06f, false);
                Gfx.SetColor(well.GetComponent<Renderer>(), new Color(0.1f, 0.85f, 0.95f));
                var portal = Gfx.Primitive(PrimitiveType.Cylinder, "Portal", transform, top + new Vector3(0f, 0.04f, 0f), new Vector3(1.3f, 0.04f, 1.3f));
                Gfx.SetColor(portal.GetComponent<Renderer>(), new Color(0.95f, 0.3f, 0.9f));
                var rock = Gfx.Hex("Rock", transform, top, 0.75f, 0.9f, false);
                Gfx.SetColor(rock.GetComponent<Renderer>(), new Color(0.35f, 0.33f, 0.32f));
                markers[i] = new[] { null, rune, well, portal, rock }; // index = (int)Marker

                var hl = Gfx.Hex("Highlight", transform, pos + new Vector3(0f, TileHeight + 0.01f, 0f), 0.7f, 0.08f, true);
                highlights[i] = hl.GetComponent<Renderer>();
                hl.SetActive(false);
            }
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

        public void Flash(Hex h)
        {
            int i = Board.IndexOf(h);
            if (i >= 0) flashUntil[i] = Time.time + 0.6f;
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
            float pulse = 0.35f + 0.3f * Mathf.Sin(Time.time * 5f);
            for (int i = 0; i < highlights.Length; i++)
            {
                bool flash = flashUntil[i] > Time.time;
                if (flash)
                {
                    highlights[i].gameObject.SetActive(true);
                    Gfx.SetColor(highlights[i], Gfx.FlashHl);
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
        }
    }
}
