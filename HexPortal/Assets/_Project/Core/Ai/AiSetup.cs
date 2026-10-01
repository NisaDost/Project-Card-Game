using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>AI-04 setup from the PlayerView: quests and passive by a simple feasibility score (AiWeights), the tower
    /// in the back row near the center column, the Guardian next to the tower (front side), a Spike Trap (C-20) in front
    /// of the tower if one is in hand, then every other character spread out. Never uses SetupTimeoutCommand.</summary>
    internal static class AiSetup
    {
        internal static int QuestScore(PlayerView view, string questId) =>
            AiWeights.Quest(questId) + (questId == "Q-17" ? TrapCards(view) * AiWeights.QuestTrapperPerTrapCard : 0);

        static int PassiveScore(PlayerView view, string id) =>
            AiWeights.Passive(id) + (id == "P-02" ? TrapCards(view) * AiWeights.PassiveTrapMasterPerTrapCard : 0);

        static int TrapCards(PlayerView view)
        {
            int n = 0;
            foreach (var c in view.Hand)
                if (!c.IsCharacter && c.Support.Category == CardCategory.Trap) n++;
            return n;
        }

        internal static ICommand Choose(PlayerView view, IReadOnlyList<ICommand> legal, Rng rng)
        {
            ICommand best = null;
            int bestScore = int.MinValue;
            switch (view.SetupStep)
            {
                case SetupStep.Quests:
                    foreach (var c in legal)
                        if (c is ChooseQuestsCommand q)
                        {
                            int score = 0;
                            foreach (var id in q.QuestIds) score += QuestScore(view, id);
                            if (score > bestScore) { best = c; bestScore = score; }
                        }
                    break;
                case SetupStep.Passive:
                    foreach (var c in legal)
                        if (c is ChoosePassiveCommand d && PassiveScore(view, d.PassiveId) > bestScore)
                        {
                            best = c;
                            bestScore = PassiveScore(view, d.PassiveId);
                        }
                    break;
                case SetupStep.Tower:
                    var cells = new List<ICommand>();
                    foreach (var c in legal)
                        if (c is PlaceTowerCommand t && IsBackCenter(t.Cell, view.Viewer)) cells.Add(c);
                    if (cells.Count > 0) best = cells[rng.NextInt(cells.Count)];
                    break;
                case SetupStep.Placement:
                    best = Placement(view, legal, rng);
                    break;
            }
            return best ?? legal[0];
        }

        static bool IsBackCenter(Hex h, PlayerId p)
        {
            h.ToDoubled(out int x, out int y);
            return y == BackRow(p) && Math.Abs(x - 6) <= AiWeights.TowerColumnSpread;
        }

        static int BackRow(PlayerId p) => p == PlayerId.A ? Catalog.RowLengths.Count - 1 : 0;

        static ICommand Placement(PlayerView view, IReadOnlyList<ICommand> legal, Rng rng)
        {
            var tower = view.OwnTowerPos.Value;
            int towerToPortal = Hex.Distance(tower, Board.Portal);
            bool hasGuardian = false;
            foreach (var u in view.OwnUnits)
                if (u.Class == UnitClass.Guardian) hasGuardian = true;
            var trapCells = new HashSet<Hex>();
            foreach (var t in view.OwnTraps) trapCells.Add(t.Pos);

            // 1. The Guardian next to the tower, on the side of the Portal.
            if (!hasGuardian)
            {
                ICommand g = null;
                foreach (var c in legal)
                    if (c is PlaceUnitCommand pu && IsClass(view, pu.CardId, UnitClass.Guardian) && Hex.Distance(pu.Cell, tower) == 1
                        && !trapCells.Contains(pu.Cell)
                        && (g == null || Hex.Distance(pu.Cell, Board.Portal) < Hex.Distance(((PlaceUnitCommand)g).Cell, Board.Portal)))
                        g = c;
                if (g != null) return g;
            }
            // 2. A Spike Trap in front of the tower.
            if (view.OwnTraps.Count == 0)
            {
                ICommand trap = null;
                foreach (var c in legal)
                    if (c is PlaceTrapCommand pt && DefOf(view, pt.CardId) == "C-20" && Hex.Distance(pt.Cell, tower) == 1
                        && Hex.Distance(pt.Cell, Board.Portal) < towerToPortal && !Occupied(view, pt.Cell)
                        && (trap == null || Hex.Distance(pt.Cell, Board.Portal) < Hex.Distance(((PlaceTrapCommand)trap).Cell, Board.Portal)))
                        trap = c;
                if (trap != null) return trap;
            }
            // 3. Every other character, spread: the largest smallest distance to the other non-Guardian units, then the
            //    largest sum of (capped) distances to them, then far from the tower, then the front row; ties at random.
            int card = 0;
            foreach (var c in view.Hand)
                if (c.IsCharacter) { card = c.Id; break; }
            if (card != 0)
            {
                var best = new List<ICommand>();
                int bestScore = int.MinValue;
                foreach (var c in legal)
                {
                    if (!(c is PlaceUnitCommand pu) || pu.CardId != card || trapCells.Contains(pu.Cell)) continue;
                    int spread = AiWeights.SpreadCap, sum = 0;
                    foreach (var u in view.OwnUnits)
                    {
                        if (u.Class == UnitClass.Guardian) continue; // the Guardian guards the tower; the others spread
                        int d = Math.Min(AiWeights.SpreadCap, Hex.Distance(pu.Cell, u.Pos));
                        spread = Math.Min(spread, d);
                        sum += d;
                    }
                    int fromTower = Math.Min(AiWeights.SpreadCap, Hex.Distance(pu.Cell, tower));
                    pu.Cell.ToDoubled(out _, out int y);
                    int score = spread * 10000 + sum * 100 + fromTower * 10 + (y != BackRow(view.Viewer) ? 1 : 0);
                    if (score > bestScore)
                    {
                        best.Clear();
                        bestScore = score;
                    }
                    if (score == bestScore) best.Add(c);
                }
                if (best.Count > 0) return best[rng.NextInt(best.Count)];
            }
            foreach (var c in legal)
                if (c is FinishSetupCommand) return c;
            return null;
        }

        static bool Occupied(PlayerView view, Hex h)
        {
            foreach (var u in view.OwnUnits)
                if (u.Pos == h) return true;
            return false;
        }

        static bool IsClass(PlayerView view, int cardId, UnitClass cls)
        {
            foreach (var c in view.Hand)
                if (c.Id == cardId) return c.IsCharacter && c.Character.Class == cls;
            return false;
        }

        static string DefOf(PlayerView view, int cardId)
        {
            foreach (var c in view.Hand)
                if (c.Id == cardId) return c.DefId;
            return null;
        }
    }
}
