using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§3.2 movement: U-07, U-09, U-10, U-12, U-43. One BFS rule for all units.</summary>
    public static class Movement
    {
        /// <summary>U-07, U-10, B-25: on the board, no unit, no tower, no rock, not Void. The Portal counts as empty.</summary>
        public static bool IsEmpty(GameState state, Hex h) =>
            state.Map.IsPassable(h)
            && state.UnitAt(h) == null && state.TowerAt(h) == null;

        /// <summary>Legal destinations, in BFS order. Uses the mover's current Visible set (U-12).</summary>
        public static List<Hex> Destinations(GameState state, Unit unit) =>
            Destinations(state, unit, Visibility.VisibleCells(state, unit.Owner));

        /// <summary>C-19: a rooted unit has none. C-14: Wind Step adds to Move this turn.</summary>
        internal static List<Hex> Destinations(GameState state, Unit unit, ISet<Hex> visible) =>
            Destinations(state, unit, visible, out _);

        /// <summary>The same, with the path length to each destination (T-05 v3.4 approach choice).</summary>
        internal static List<Hex> Destinations(GameState state, Unit unit, ISet<Hex> visible, out List<int> pathSteps)
        {
            if (unit.IsRooted) { pathSteps = new List<int>(); return new List<Hex>(); }
            Func<Hex, bool> canStop, canPass;
            if (unit.Class == UnitClass.Flyer)
            {
                // U-43: over Mountain, River, Lava and units (not towers, not Void); stops on River too, never on a Mountain.
                canPass = h => visible.Contains(h) && state.Map.IsPlayable(h) && state.TowerAt(h) == null;
                canStop = h => canPass(h) && state.UnitAt(h) == null && state.Map.Get(h).Marker != Marker.Mountain;
            }
            else
            {
                canStop = h => visible.Contains(h) && IsEmpty(state, h);
                // U-09: the Rider passes through units and towers of either side, never rock or Void. It still stops only on empty cells.
                // U-43: every ground unit may pass through a cell held only by a Flyer (of either side), never stop there.
                canPass = unit.Class == UnitClass.Rider
                    ? h => visible.Contains(h) && state.Map.IsPassable(h)
                    : h => visible.Contains(h) && state.Map.IsPassable(h) && state.TowerAt(h) == null
                           && (state.UnitAt(h) == null || state.UnitAt(h).Class == UnitClass.Flyer);
            }
            return HexSearch.Reachable(unit.Pos, unit.Def.Move + unit.MoveBonus, canPass, canStop, out pathSteps);
        }
    }
}
