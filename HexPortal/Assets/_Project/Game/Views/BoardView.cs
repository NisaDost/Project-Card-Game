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
        // Index = (int)Marker. Not Enum.GetValues: the Rock = Mountain alias would duplicate a value and shift the indices.
        static readonly int MarkerCount = MaxMarker() + 1;
        static int MaxMarker() { int m = 0; foreach (Marker x in System.Enum.GetValues(typeof(Marker))) m = Mathf.Max(m, (int)x); return m; }
        readonly GameObject[] torchProps = new GameObject[Board.Cells.Count];
        readonly GameObject[] torchRadius = new GameObject[Board.Cells.Count];
        readonly List<int> lavaCells = new List<int>();
        /// <summary>V-12: torches drawn (tests).</summary>
        public int TorchesShown { get; private set; }
        /// <summary>B-15/B-16: drawn river / lava tiles (tests, captures).</summary>
        public int RiversShown { get; private set; }
        public int LavasShown { get; private set; }
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
        readonly GameObject[] landingHints = new GameObject[Board.Cells.Count];
        /// <summary>U-42: cells shown as the Assassin's landing hint while it is selected (tests).</summary>
        public int LandingHintCount { get; private set; }
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
                var props = new GameObject[MarkerCount];
                for (int k = 0; k < MarkerCount; k++)
                {
                    if (!ArtCatalog.Markers.TryGetValue((Marker)k, out var art) || art.Mesh == null) continue;
                    var prop = Prop(((Marker)k).ToString(), top, art.Mesh(), (Marker)k == Marker.Portal ? 0 : i);
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
                var torch = Prop("Torch", top, LowPoly.Torch, 0);
                Gfx.SetColor(torch.GetComponent<Renderer>(), ArtCatalog.TorchColor);
                torch.SetActive(false);
                torchProps[i] = torch;
                var tr = Gfx.Hex("TorchRadius", transform, top + new Vector3(0f, 0.03f, 0f), 1f, 1f, true);
                tr.GetComponent<MeshFilter>().sharedMesh = LowPoly.TorchRadius;
                Gfx.SetColor(tr.GetComponent<Renderer>(), ArtCatalog.TorchRadiusColor);
                tr.SetActive(false);
                torchRadius[i] = tr;

                var hl = Gfx.Hex("Highlight", transform, pos + new Vector3(0f, TileHeight + 0.01f, 0f), 0.7f, 0.08f, true);
                highlights[i] = hl.GetComponent<Renderer>();
                var lh = Prop("LandingHint", top + new Vector3(0f, 0.05f, 0f), LowPoly.MarkerRing, 0); // U-42 landing hint: a white ring
                lh.transform.localRotation = Quaternion.identity;
                lh.transform.localScale = new Vector3(0.75f, 3f, 0.75f);
                Gfx.SetColor(lh.GetComponent<Renderer>(), Gfx.LandingHl);
                lh.SetActive(false);
                landingHints[i] = lh;
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
            int rivers = 0, lavas = 0;
            lavaCells.Clear();
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
                    torchProps[i].SetActive(false); torchRadius[i].SetActive(false);
                    continue;
                }
                // Mountains, rivers and lava have no biome: the marker theme gives the tile colour (B-14…B-16).
                var tm = known ? c.Tile.Marker : Marker.None;
                var tint = ArtCatalog.Markers.TryGetValue(tm, out var ta) ? ta.TileColor : Color.clear;
                var color = !known ? Gfx.Unknown : tint.a > 0f ? tint : Gfx.BiomeColor(c.Tile.Biome);
                if (tm == Marker.River) rivers++;
                if (tm == Marker.Lava) { lavas++; if (c.Visibility == CellVisibility.Visible) lavaCells.Add(i); }
                if (c.Visibility == CellVisibility.Explored) color = Gfx.Faded(color);
                Gfx.SetColor(tiles[i], color);
                clouds[i].SetActive(c.Visibility == CellVisibility.Hidden);
                var m = known ? c.Tile.Marker : Marker.None;
                for (int k = 1; k < markers[i].Length; k++) if (markers[i][k] != null) markers[i][k].SetActive((int)m == k);
                torchProps[i].SetActive(false); torchRadius[i].SetActive(false);
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
            RiversShown = rivers; LavasShown = lavas;
            int torchCount = 0;
            foreach (var t in view.Torches) // V-12: own torches and Visible enemy ones (PlayerView filters)
            {
                int i = Board.IndexOf(t.Pos);
                if (i < 0 || voidCell[i]) continue;
                torchProps[i].SetActive(true);
                Gfx.SetColor(torchProps[i].GetComponent<Renderer>(), Color.Lerp(ArtCatalog.TorchColor, Gfx.OwnerColor(t.Owner), 0.25f));
                torchRadius[i].SetActive(t.Owner == view.Viewer);
                torchCount++;
            }
            TorchesShown = torchCount;
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
        /// <summary>U-42: marks where the selected Assassin would land after a hit that does not kill (Preview.AssassinLanding).
        /// Cleared with the highlights.</summary>
        public void LandingHint(Hex h)
        {
            int i = Board.IndexOf(h);
            if (i < 0 || voidCell[i] || landingHints[i].activeSelf) return;
            landingHints[i].SetActive(true);
            LandingHintCount++;
        }

        public bool LandingHintAt(Hex h)
        {
            int i = Board.IndexOf(h);
            return i >= 0 && landingHints[i].activeSelf;
        }

        public Color HighlightAt(Hex h)
        {
            int i = Board.IndexOf(h);
            return i < 0 ? Color.clear : highlightColor[i];
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < highlightColor.Length; i++) highlightColor[i] = Color.clear;
            HighlightCount = 0;
            for (int i = 0; i < landingHints.Length; i++) if (landingHints[i] != null) landingHints[i].SetActive(false);
            LandingHintCount = 0;
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
            // B-16 lava glow (no allocations: a cached index list).
            if (lavaCells.Count > 0)
            {
                var lc = ArtCatalog.MarkerOf(Marker.Lava).TileColor;
                for (int j = 0; j < lavaCells.Count; j++)
                {
                    int i = lavaCells[j];
                    float g = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f + i);
                    Gfx.SetColor(tiles[i], Color.Lerp(lc, new Color(1f, 0.75f, 0.2f), 0.45f * g));
                }
            }
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
