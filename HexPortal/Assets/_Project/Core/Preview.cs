using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>T-05 (v3.2): one target a unit can hit with a move-attack, and the approach cells it can use (BFS order).</summary>
    public sealed class MoveAttackOption
    {
        public Hex Target { get; internal set; }
        public IReadOnlyList<Hex> Approaches { get; internal set; }
    }

    /// <summary>UX-10: an own unit's ranges, all in Board.Cells order except MoveCells (BFS order, like the move list).</summary>
    public sealed class RangePreview
    {
        /// <summary>U-07…U-12: where the unit can move (from what the viewer sees now).</summary>
        public IReadOnlyList<Hex> MoveCells { get; internal set; }
        /// <summary>Cells in attack range from where it stands.</summary>
        public IReadOnlyList<Hex> AttackCells { get; internal set; }
        /// <summary>Cells in attack range from where it stands or from any move cell (the move-attack reach).</summary>
        public IReadOnlyList<Hex> MoveAttackCells { get; internal set; }
    }

    /// <summary>Client previews computed by Core from a PlayerView only (through BeliefState, like the AI, AI-01): hidden
    /// enemy units, traps, the opponent's hand and anything else outside the view never change them. They ignore Energy and
    /// whether the unit already acted (they answer "where could it", e.g. on the opponent's turn, UX-10).</summary>
    public static class Preview
    {
        /// <summary>T-05 (v3.2): the targets the viewer's unit can hit with a move-attack, each with its single automatic approach (v3.4).
        /// Targets in Board.Cells order. Empty if the unit is not the viewer's.</summary>
        public static List<MoveAttackOption> MoveAttackTargets(PlayerView view, int unitId)
        {
            var result = new List<MoveAttackOption>();
            var belief = BeliefState.From(view);
            var u = belief.GetUnit(unitId);
            if (u == null || u.Owner != view.Viewer) return result;
            var visible = Visibility.VisibleCells(belief, view.Viewer);
            foreach (var o in MoveAttack.Options(belief, u, visible)) // T-05 (v3.4): the one automatic approach
                result.Add(new MoveAttackOption { Target = o.Key, Approaches = new List<Hex> { o.Value } });
            return result;
        }

        /// <summary>UX-10: the viewer's own unit's move cells and attack ranges; null if the unit is not the viewer's.</summary>
        public static RangePreview Ranges(PlayerView view, int unitId)
        {
            var belief = BeliefState.From(view);
            var u = belief.GetUnit(unitId);
            if (u == null || u.Owner != view.Viewer) return null;
            var moves = Movement.Destinations(belief, u, Visibility.VisibleCells(belief, view.Viewer));
            var attack = new List<Hex>();
            var reach = new List<Hex>();
            foreach (var h in Board.Cells)
            {
                bool here = Combat.InRange(u.Def, u.Pos, h);
                if (here) attack.Add(h);
                bool any = here;
                foreach (var m in moves)
                    if (!any && Combat.InRange(u.Def, m, h)) any = true;
                if (any) reach.Add(h);
            }
            return new RangePreview { MoveCells = moves, AttackCells = attack, MoveAttackCells = reach };
        }
    }
}
