using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>The fixed 127-cell hexagonal board of radius 6 (B-01…B-06, B-21; v3.0). Data is never transformed per
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

        /// <summary>1-based GDD row (row 1 = top, player B's edge): r + BoardRadius + 1.</summary>
        public static int GddRow(Hex h) => h.R + Catalog.BoardRadius + 1;

        /// <summary>1-based position within the GDD row, left to right (increasing q).</summary>
        public static int GddCol(Hex h) => h.Q - Math.Max(-Catalog.BoardRadius, -Catalog.BoardRadius - h.R) + 1;

        /// <summary>B-03: B = the top HomeZoneRows rows (r ≤ -5), A = the bottom HomeZoneRows rows (r ≥ 5).</summary>
        public static bool IsHomeZone(Hex h, PlayerId player)
        {
            if (!IsOnBoard(h)) return false;
            int edge = Catalog.BoardRadius - Catalog.HomeZoneRows + 1;
            return player == PlayerId.B ? h.R <= -edge : h.R >= edge;
        }

        /// <summary>B-21: rows 1–6 plus the 6 cells of row 7 left of the Portal (r &lt; 0, or r = 0 and q &lt; 0; 63 cells).</summary>
        public static bool IsInUpperHalf(Hex h) => IsOnBoard(h) && (h.R < 0 || (h.R == 0 && h.Q < 0));

        /// <summary>B-06: B's half is the upper half, A's half is its mirror. The Portal is in neither.</summary>
        public static bool IsInHalf(Hex h, PlayerId player) =>
            player == PlayerId.B ? IsInUpperHalf(h) : IsInUpperHalf(h.Mirror());
    }
}
