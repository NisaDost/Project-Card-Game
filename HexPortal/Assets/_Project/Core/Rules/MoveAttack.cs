using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>T-05 (v3.4): the move-attack approach cell is chosen automatically. Shared by the engine, the legal list,
    /// GreedyAi (through the legal list) and Preview, so all of them agree.</summary>
    public static class MoveAttack
    {
        /// <summary>T-05 (v3.4), U-12, V-11: among the cells the unit can move to now (on the actor's Visible information) from
        /// which <paramref name="target"/> is a legal target, the one with the shortest path from the start cell; ties go to
        /// a cell of the unit's own biome, then to the lowest Board.Cells index as the actor sees the board (B's view is the
        /// 180° mirror, B-05, so both seats break ties the same way). Null if the target cannot be hit with a move-attack.</summary>
        internal static Hex? ApproachFor(GameState state, Unit unit, Hex target, ISet<Hex> visible)
        {
            var dests = Movement.Destinations(state, unit, visible, out var steps);
            Hex? best = null;
            int bestSteps = 0, bestBiome = 0, bestIndex = 0;
            for (int i = 0; i < dests.Count; i++)
            {
                var h = dests[i];
                if (!Combat.LegalTargetsFrom(state, unit, h, visible).Contains(target)) continue;
                int biome = state.Map.Get(h).Biome == unit.Biome ? 0 : 1;
                int index = Board.IndexOf(unit.Owner == PlayerId.A ? h : h.Mirror());
                if (best == null || steps[i] < bestSteps
                    || steps[i] == bestSteps && (biome < bestBiome || biome == bestBiome && index < bestIndex))
                {
                    best = h; bestSteps = steps[i]; bestBiome = biome; bestIndex = index;
                }
            }
            return best;
        }

        /// <summary>T-05 (v3.4): the same, on the mover's current Visible set.</summary>
        public static Hex? ApproachFor(GameState state, Unit unit, Hex target) =>
            ApproachFor(state, unit, target, Visibility.VisibleCells(state, unit.Owner));

        /// <summary>T-05 (v3.4): every target the unit can hit with a move-attack, with its approach, targets in Board.Cells order.</summary>
        internal static List<KeyValuePair<Hex, Hex>> Options(GameState state, Unit unit, ISet<Hex> visible)
        {
            var targets = new HashSet<Hex>();
            foreach (var h in Movement.Destinations(state, unit, visible))
                foreach (var t in Combat.LegalTargetsFrom(state, unit, h, visible)) targets.Add(t);
            var result = new List<KeyValuePair<Hex, Hex>>();
            foreach (var t in Board.Cells)
                if (targets.Contains(t)) result.Add(new KeyValuePair<Hex, Hex>(t, ApproachFor(state, unit, t, visible).Value));
            return result;
        }
    }
}
