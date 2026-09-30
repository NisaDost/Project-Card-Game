using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§3.2 movement: U-07, U-09, U-10, U-12. One BFS rule for all units.</summary>
    public static class Movement
    {
        /// <summary>U-07, U-10: on the board, no unit, no tower, no rock. The Portal counts as empty.</summary>
        public static bool IsEmpty(GameState state, Hex h) =>
            Board.IsOnBoard(h) && state.Map.Get(h).Marker != Marker.Rock
            && state.UnitAt(h) == null && state.TowerAt(h) == null;

        /// <summary>Legal destinations, in BFS order. Uses the mover's current Visible set (U-12).</summary>
        public static List<Hex> Destinations(GameState state, Unit unit) =>
            Destinations(state, unit, Visibility.VisibleCells(state, unit.Owner));

        /// <summary>C-19: a rooted unit has none. C-14: Wind Step adds to Move this turn.</summary>
        internal static List<Hex> Destinations(GameState state, Unit unit, ISet<Hex> visible)
        {
            if (unit.IsRooted) return new List<Hex>();
            Func<Hex, bool> canStop = h => visible.Contains(h) && IsEmpty(state, h);
            // U-09: the Rider passes through units and towers of either side, never rock. It still stops only on empty cells.
            Func<Hex, bool> canPass = unit.Class == UnitClass.Rider
                ? h => visible.Contains(h) && state.Map.Get(h).Marker != Marker.Rock
                : canStop;
            return HexSearch.Reachable(unit.Pos, unit.Def.Move + unit.MoveBonus, canPass, canStop);
        }
    }
}
