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
        static readonly Marker[] MarkerKinds = (Marker[])System.Enum.GetValues(typeof(Marker));
        readonly GameObject[] traps = new GameObject[Board.Cells.Count];
        readonly Renderer[] rings = new Renderer[Board.Cells.Count]; // UX-16 rune/wellspring ring on the tile edge
        static Color RuneColor => ArtCatalog.MarkerOf(Marker.RuneStone).Color;
        static Color WellColor => ArtCatalog.MarkerOf(Marker.Wellspring).Color;
        static Color HeldGlow => ArtCatalog.HeldGlow;
        int portalIndex = -1;
        bool portalOpen;
        Color portalColor;
        /// <summary>UX-13: cells whose rune/wellspring holds an own unit (glowing), for tests.</summary>
        public int HeldCount { get; private set; }
        /// <summary>UX-16: rune/wellspring rings drawn (tests).</summary>
        public int RingsShown { get; private set; }
        /// <summary>UX-14: own traps drawn (never the opponent's: PlayerView only lists own traps).</summary>
        public int TrapsShown { get; private set; }
        public bool PortalOpenShown => portalOpen;
        readonly Renderer[] highlights = new Renderer[Board.Cells.Count];
        readonly Color[] highlightColor = new Color[Board.Cells.Count];
        readonly bool[] eventCell = new bool[Board.Cells.Count];
        readonly float[] flashUntil = new float[Board.Cells.Count];
        readonly Color[] flashColor = new Color[Board.Cells.Count];
        readonly bool[] voidCell = new bool[Board.Cells.Count];
        readonly bool[] playable = new bool[Board.Cells.Count];
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
                Gfx.SetColor(cloud.GetComponent<Renderer>(), ArtCatalog.FogColor);
                clouds[i] = cloud;

                var top = pos + new Vector3(0f, TileHeight, 0f);
                // A-03 marker props from the theme (ArtCatalog), one per marker kind; index = (int)Marker.
                var props = new GameObject[MarkerKinds.Length];
                for (int k = 0; k < MarkerKinds.Length; k++)
                {
                    var art = ArtCatalog.MarkerOf(MarkerKinds[k]);
                    if (art.Mesh == null) continue;
                    var prop = Prop(MarkerKinds[k].ToString(), top, art.Mesh(), MarkerKinds[k] == Marker.Portal ? 0 : i);
                    Gfx.SetColor(prop.GetComponent<Renderer>(), art.Color);
                    props[k] = prop;
                }
                markers[i] = props;
                if (cells[i].Q == 0 && cells[i].R == 0) portalIndex = i;
                var ring = Prop("MarkerRing", top + new Vector3(0f, 0.01f, 0f), LowPoly.MarkerRing, 0);
                ring.transform.localRotation = Quaternion.identity;
                ring.SetActive(false);
                rings[i] = ring.GetComponent<Renderer>();
                var trap = Prop("OwnTrap", top, LowPoly.Trap, i);
                Gfx.SetColor(trap.GetComponent<Renderer>(), ArtCatalog.OwnTrapColor);
                trap.SetActive(false);
                traps[i] = trap;

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
            int heldCount = 0, ringCount = 0;
            // B-25: Void comes from the public PlayableCells (v4.0 fog: Cells no longer mark Void, they read Hidden).
            for (int i = 0; i < playable.Length; i++) playable[i] = false;
            foreach (var h in view.PlayableCells) { int pi = Board.IndexOf(h); if (pi >= 0) playable[pi] = true; }
            for (int i = 0; i < view.Cells.Count; i++)
            {
                var c = view.Cells[i];
                bool known = c.TerrainKnown;
                // B-25: Void cells are not part of the board: no tile, cloud, marker, trap or highlight.
                bool isVoid = !playable[i] || (known && c.Tile.Marker == Marker.Void);
                voidCell[i] = isVoid;
                tiles[i].gameObject.SetActive(!isVoid);
                if (isVoid)
                {
                    clouds[i].SetActive(false);
                    for (int k = 1; k < markers[i].Length; k++) if (markers[i][k] != null) markers[i][k].SetActive(false);
                    rings[i].gameObject.SetActive(false);
                    traps[i].SetActive(false);
                    continue;
                }
                // Template rocks have no biome: tint the tile like the rock prop instead of the "no biome" colour.
                var color = !known ? Gfx.Unknown
                    : c.Tile.Biome == HexPortal.Core.Data.Biome.None && c.Tile.Marker == Marker.Rock ? Color.Lerp(ArtCatalog.MarkerOf(Marker.Rock).Color, Color.white, 0.25f)
                    : Gfx.BiomeColor(c.Tile.Biome);
                if (c.Visibility == CellVisibility.Explored) color = Gfx.Faded(color);
                Gfx.SetColor(tiles[i], color);
                clouds[i].SetActive(c.Visibility == CellVisibility.Hidden);
                var m = known ? c.Tile.Marker : Marker.None;
                for (int k = 1; k < markers[i].Length; k++) if (markers[i][k] != null) markers[i][k].SetActive((int)m == k);
                // UX-13: a rune stone or wellspring held by an own unit glows (tile and marker).
                bool held = (m == Marker.RuneStone || m == Marker.Wellspring) && c.Unit != null && !c.Unit.IsGhost && c.Unit.Owner == view.Viewer;
                if (held) { Gfx.SetColor(tiles[i], Color.Lerp(color, HeldGlow, 0.5f)); heldCount++; }
                if (m == Marker.RuneStone) Gfx.SetColor(markers[i][(int)Marker.RuneStone].GetComponent<Renderer>(), held ? HeldGlow : RuneColor);
                if (m == Marker.Wellspring) Gfx.SetColor(markers[i][(int)Marker.Wellspring].GetComponent<Renderer>(), held ? HeldGlow : WellColor);
                bool special = m == Marker.RuneStone || m == Marker.Wellspring;
                rings[i].gameObject.SetActive(special);
                if (special) ringCount++;
                if (special) Gfx.SetColor(rings[i], held ? HeldGlow : m == Marker.RuneStone ? RuneColor : WellColor);
                traps[i].SetActive(false);
            }
            HeldCount = heldCount;
            RingsShown = ringCount;
            int trapsShown = 0;
            foreach (var t in view.OwnTraps) // UX-14, C-33: own traps only
            {
                int i = Board.IndexOf(t.Pos);
                if (i >= 0 && !voidCell[i]) { traps[i].SetActive(true); trapsShown++; }
            }
            TrapsShown = trapsShown;
            System.Array.Clear(eventCell, 0, eventCell.Length);
            anyEvent = false;
            if (view.AnnouncedEvent != null)
                foreach (var h in view.AnnouncedEventCells)
                {
                    int i = Board.IndexOf(h);
                    if (i >= 0 && !voidCell[i]) { eventCell[i] = true; anyEvent = true; }
                }
            ApplyHighlights();
        }

        /// <summary>The highlight colour of a cell (clear when none); tests read the UX-02 highlights through it.</summary>
        public Color HighlightAt(Hex h)
        {
            int i = Board.IndexOf(h);
            return i < 0 ? Color.clear : highlightColor[i];
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
            if (i < 0 || voidCell[i]) return;
            if (highlightColor[i].a <= 0f) HighlightCount++;
            highlightColor[i] = c;
            ApplyHighlights();
        }

        public void Flash(Hex h) => Flash(h, Gfx.FlashHl);

        /// <summary>A short coloured flash on a cell (trap, shot, death highlights; UX-10).</summary>
        public void Flash(Hex h, Color color)
        {
            int i = Board.IndexOf(h);
            if (i < 0 || voidCell[i]) return;
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

        /// <summary>UX-14: the Portal opened for a player (W-01 is public): the portal ring glows and pulses in that
        /// player's colour. Purely visual.</summary>
        public void SetPortalOpen(bool open, Color c)
        {
            portalOpen = open;
            portalColor = c;
            if (portalIndex < 0) return;
            var p = markers[portalIndex][(int)Marker.Portal];
            if (!open) { p.transform.localScale = Vector3.one; Gfx.SetColor(p.GetComponent<Renderer>(), ArtCatalog.MarkerOf(Marker.Portal).Color); }
        }

        void Update()
        {
            if (portalOpen && portalIndex >= 0)
            {
                var p = markers[portalIndex][(int)Marker.Portal];
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                float s = 1.15f + 0.15f * k;
                p.transform.localScale = new Vector3(s, s, s);
                Gfx.SetColor(p.GetComponent<Renderer>(), Color.Lerp(portalColor, Color.white, 0.5f * k));
            }
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
