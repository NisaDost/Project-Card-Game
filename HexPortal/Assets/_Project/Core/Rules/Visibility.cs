using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§11, M2 part: the currently Visible set. Explored memory, last-seen, V-08 and PlayerView are M4.</summary>
    public static class Visibility
    {
        /// <summary>V-02: cells within Sight of any own unit or the own tower (nothing blocks sight), plus the Portal (V-06).</summary>
        public static HashSet<Hex> VisibleCells(GameState state, PlayerId player)
        {
            var visible = new HashSet<Hex> { Board.Portal };
            AddWithin(visible, state.GetTower(player).Pos, Catalog.Tower.Sight);
            foreach (var u in state.Units)
                if (u.Owner == player) AddWithin(visible, u.Pos, u.Def.Sight);
            return visible;
        }

        static void AddWithin(HashSet<Hex> set, Hex center, int sight)
        {
            foreach (var c in Board.Cells)
                if (Hex.Distance(c, center) <= sight) set.Add(c);
        }
    }
}
