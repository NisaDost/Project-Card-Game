using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>The fixed 59-cell board (B-01…B-06, B-21). Data is never transformed per player (B-05).</summary>
    public static class Board
    {
        public static readonly Hex Portal = new Hex(0, 0); // B-02

        /// <summary>All cells, ordered by doubled y, then x.</summary>
        public static readonly IReadOnlyList<Hex> Cells;
        static readonly Dictionary<Hex, int> index = new Dictionary<Hex, int>();

        static Board()
        {
            var cells = new List<Hex>();
            for (int y = 0; y < Catalog.RowLengths.Count; y++)
                for (int i = 0; i < Catalog.RowLengths[y]; i++)
                {
                    var h = Hex.FromDoubled(y % 2 + 2 * i, y);
                    index.Add(h, cells.Count);
                    cells.Add(h);
                }
            Cells = cells;
        }

        public static bool IsOnBoard(Hex h) => index.ContainsKey(h);

        /// <summary>Position of h in Cells, or -1 if off the board.</summary>
        public static int IndexOf(Hex h) => index.TryGetValue(h, out int i) ? i : -1;

        /// <summary>1-based GDD row (row 1 = top, player B's edge).</summary>
        public static int GddRow(Hex h)
        {
            h.ToDoubled(out _, out int y);
            return y + 1;
        }

        /// <summary>1-based position within the GDD row, left to right.</summary>
        public static int GddCol(Hex h)
        {
            h.ToDoubled(out int x, out int y);
            return (x - y % 2) / 2 + 1;
        }

        /// <summary>B-03: A = bottom HomeZoneRows rows, B = top HomeZoneRows rows.</summary>
        public static bool IsHomeZone(Hex h, PlayerId player)
        {
            if (!IsOnBoard(h)) return false;
            h.ToDoubled(out _, out int y);
            return player == PlayerId.B
                ? y < Catalog.HomeZoneRows
                : y >= Catalog.RowLengths.Count - Catalog.HomeZoneRows;
        }

        /// <summary>B-21: rows 1–4 plus the 3 cells of row 5 left of the Portal (29 cells).</summary>
        public static bool IsInUpperHalf(Hex h)
        {
            if (!IsOnBoard(h)) return false;
            h.ToDoubled(out int x, out int y);
            return y <= 3 || (y == 4 && x < 6);
        }

        /// <summary>B-06: B's half is the upper half, A's half is its mirror. The Portal is in neither.</summary>
        public static bool IsInHalf(Hex h, PlayerId player) =>
            player == PlayerId.B ? IsInUpperHalf(h) : IsInUpperHalf(h.Mirror());
    }
}
