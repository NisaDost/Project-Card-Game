using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>The fixed 169-cell hexagonal board of radius 7 (B-01…B-06, B-21; v3.3). A map template may mark cells Void (B-25,
    /// GameMap.IsPlayable). Data is never transformed per
    /// player (B-05).</summary>
    public static class Board
    {
        public static readonly Hex Portal = new Hex(0, 0); // B-02

        /// <summary>All cells: max(|q|,|r|,|q+r|) ≤ BoardRadius, ordered by r (top row first), then q.</summary>
        public static readonly IReadOnlyList<Hex> Cells;
        static readonly Dictionary<Hex, int> index = new Dictionary<Hex, int>();

        static Board()
        {
            const int n = Catalog.BoardRadius;
            var cells = new List<Hex>();
            for (int r = -n; r <= n; r++)
                for (int q = Math.Max(-n, -n - r); q <= Math.Min(n, n - r); q++)
                {
                    var h = new Hex(q, r);
                    index.Add(h, cells.Count);
                    cells.Add(h);
                }
            Cells = cells;
        }

        public static bool IsOnBoard(Hex h) => index.ContainsKey(h);

        /// <summary>Position of h in Cells, or -1 if off the board.</summary>
        public static int IndexOf(Hex h) => index.TryGetValue(h, out int i) ? i : -1;

        /// <summary>Adds every on-board cell within <paramref name="radius"/> of center to set. Walks only the hex ball, in
        /// Board.Cells order (r, then q), so the cells are added in the same order as a filtered scan of Cells.</summary>
        public static void AddWithin(HashSet<Hex> set, Hex center, int radius)
        {
            for (int dr = -radius; dr <= radius; dr++)
                for (int dq = Math.Max(-radius, -radius - dr); dq <= Math.Min(radius, radius - dr); dq++)
                {
                    var c = new Hex(center.Q + dq, center.R + dr);
                    if (index.ContainsKey(c)) set.Add(c);
                }
        }

        /// <summary>AddWithin restricted to the map's playable cells (B-25: Void never counts for sight or the Control Zone).</summary>
        public static void AddWithin(HashSet<Hex> set, Hex center, int radius, GameMap map)
        {
            for (int dr = -radius; dr <= radius; dr++)
                for (int dq = Math.Max(-radius, -radius - dr); dq <= Math.Min(radius, radius - dr); dq++)
                {
                    var c = new Hex(center.Q + dq, center.R + dr);
                    if (map.IsPlayable(c)) set.Add(c);
                }
        }

        /// <summary>The on-board cells within <paramref name="radius"/> of center, as a list in Board.Cells order.</summary>
        public static List<Hex> CellsWithin(Hex center, int radius)
        {
            var list = new List<Hex>();
            for (int dr = -radius; dr <= radius; dr++)
                for (int dq = Math.Max(-radius, -radius - dr); dq <= Math.Min(radius, radius - dr); dq++)
                {
                    var c = new Hex(center.Q + dq, center.R + dr);
                    if (index.ContainsKey(c)) list.Add(c);
                }
            return list;
        }

        /// <summary>1-based GDD row (row 1 = top, player B's edge): r + BoardRadius + 1.</summary>
        public static int GddRow(Hex h) => h.R + Catalog.BoardRadius + 1;

        /// <summary>1-based position within the GDD row, left to right (increasing q).</summary>
        public static int GddCol(Hex h) => h.Q - Math.Max(-Catalog.BoardRadius, -Catalog.BoardRadius - h.R) + 1;

        /// <summary>B-03: B = the top HomeZoneRows rows (r ≤ -6), A = the bottom HomeZoneRows rows (r ≥ 6).</summary>
        public static bool IsHomeZone(Hex h, PlayerId player)
        {
            if (!IsOnBoard(h)) return false;
            int edge = Catalog.BoardRadius - Catalog.HomeZoneRows + 1;
            return player == PlayerId.B ? h.R <= -edge : h.R >= edge;
        }

        /// <summary>B-21: rows 1–7 plus the 7 cells of row 8 left of the Portal (r &lt; 0, or r = 0 and q &lt; 0; 84 cells).</summary>
        public static bool IsInUpperHalf(Hex h) => IsOnBoard(h) && (h.R < 0 || (h.R == 0 && h.Q < 0));

        /// <summary>B-06: B's half is the upper half, A's half is its mirror. The Portal is in neither.</summary>
        public static bool IsInHalf(Hex h, PlayerId player) =>
            player == PlayerId.B ? IsInUpperHalf(h) : IsInUpperHalf(h.Mirror());
    }
}
