using System;
using System.Collections.Generic;

namespace HexPortal.Core
{
    public static class HexSearch
    {
        /// <summary>
        /// BFS over edge neighbours on the board, up to <paramref name="steps"/> steps from <paramref name="start"/>.
        /// A step may enter a cell only if canPass(cell). A reached cell (not start) is returned only if canStop(cell).
        /// Output is in BFS order, neighbours in Hex.Directions order.
        /// </summary>
        public static List<Hex> Reachable(Hex start, int steps, Func<Hex, bool> canPass, Func<Hex, bool> canStop)
        {
            var result = new List<Hex>();
            var seen = new HashSet<Hex> { start };
            var frontier = new List<Hex> { start };
            for (int step = 0; step < steps && frontier.Count > 0; step++)
            {
                var next = new List<Hex>();
                foreach (var c in frontier)
                    for (int d = 0; d < 6; d++)
                    {
                        var n = c.Neighbor(d);
                        if (!Board.IsOnBoard(n) || seen.Contains(n) || !canPass(n)) continue;
                        seen.Add(n);
                        next.Add(n);
                        if (canStop(n)) result.Add(n);
                    }
                frontier = next;
            }
            return result;
        }
    }
}
