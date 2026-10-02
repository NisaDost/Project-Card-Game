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

        internal static ICommand Choose(PlayerView view, IReadOnlyList<ICommand> legal, AiLevel level, Rng rng)
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
                    SeatOrder(cells, view.Viewer);
                    if (cells.Count > 0) best = cells[rng.NextInt(cells.Count)];
                    break;
                case SetupStep.Placement:
                    best = Placement(view, legal, level, rng);
                    break;
            }
            return best ?? legal[0];
        }

        static bool IsBackCenter(Hex h, PlayerId p)
        {
            // 2q + r is the doubled-width column offset from the board's centre line (0 = centre).
            return h.R == BackRow(p) && Math.Abs(2 * h.Q + h.R) <= AiWeights.TowerColumnSpread;
        }

        /// <summary>Axial r of p's back (edge) row.</summary>
        static int BackRow(PlayerId p) => p == PlayerId.A ? Catalog.BoardRadius : -Catalog.BoardRadius;

        static ICommand Placement(PlayerView view, IReadOnlyList<ICommand> legal, AiLevel level, Rng rng)
        {
            var tower = view.OwnTowerPos.Value;
            int towerToPortal = Hex.Distance(tower, Board.Portal);
            bool hasGuardian = false;
            foreach (var u in view.OwnUnits)
                if (u.Class == UnitClass.Guardian) hasGuardian = true;
            var trapCells = new HashSet<Hex>();
            foreach (var t in view.OwnTraps) trapCells.Add(t.Pos);

            // S-05 (v3.0), PM S3: the pieces still to place come from a combination that fits the setup budget.
            var plan = Plan(view, legal, level, rng);
            if (plan.Count == 0)
            {
                foreach (var c in legal)
                    if (c is FinishSetupCommand) return c;
            }
            bool planGuardian = false, planSpike = false;
            int card = 0;
            foreach (var c in plan)
            {
                if (c.IsCharacter && c.Character.Class == UnitClass.Guardian) planGuardian = true;
                else if (!c.IsCharacter) planSpike = true;
                else if (card == 0) card = c.Id;
            }

            // 1. The Guardian next to the tower, on the side of the Portal.
            if (!hasGuardian && planGuardian)
            {
                ICommand g = null;
                int gKey = int.MaxValue;
                foreach (var c in legal)
                    if (c is PlaceUnitCommand pu && IsClass(view, pu.CardId, UnitClass.Guardian) && Hex.Distance(pu.Cell, tower) == 1
                        && !trapCells.Contains(pu.Cell) && FrontKey(view.Viewer, pu.Cell) < gKey)
                    {
                        g = c;
                        gKey = FrontKey(view.Viewer, pu.Cell);
                    }
                if (g != null) return g;
            }
            // 2. A Spike Trap in front of the tower.
            if (view.OwnTraps.Count == 0 && planSpike)
            {
                ICommand trap = null;
                int tKey = int.MaxValue;
                foreach (var c in legal)
                    if (c is PlaceTrapCommand pt && DefOf(view, pt.CardId) == "C-20" && Hex.Distance(pt.Cell, tower) == 1
                        && Hex.Distance(pt.Cell, Board.Portal) < towerToPortal && !Occupied(view, pt.Cell)
                        && FrontKey(view.Viewer, pt.Cell) < tKey)
                    {
                        trap = c;
                        tKey = FrontKey(view.Viewer, pt.Cell);
                    }
                if (trap != null) return trap;
            }
            // 3. Every other character, spread: the largest smallest distance to the other non-Guardian units, then the
            //    largest sum of (capped) distances to them, then far from the tower, then the front row; ties at random.
            // A planned Guardian without a free cell next to the tower is spread like the others.
            if (card == 0)
                foreach (var c in plan)
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
                    int score = spread * 10000 + sum * 100 + fromTower * 10 + (pu.Cell.R != BackRow(view.Viewer) ? 1 : 0);
                    if (score > bestScore)
                    {
                        best.Clear();
                        bestScore = score;
                    }
                    if (score == bestScore) best.Add(c);
                }
                SeatOrder(best, view.Viewer);
                if (best.Count > 0) return best[rng.NextInt(best.Count)];
            }
            foreach (var c in legal)
                if (c is FinishSetupCommand) return c;
            return null;
        }

        /// <summary>PM S3: the cards still to place. Candidates: the character cards in hand and one Spike Trap (C-20) if no trap
        /// is placed yet and one can be placed now. Every subset that fits the setup Mana left, non-empty while no character
        /// stands (S-05: at least one). Normal: random among the AiWeights.SetupTopCombos best by AiWeights.SetupValue (ties
        /// by the subset's bit order); Easy: any fitting subset. Re-planned each step, so the result always fits.</summary>
        internal static List<CardInstance> Plan(PlayerView view, IReadOnlyList<ICommand> legal, AiLevel level, Rng rng)
        {
            var cand = new List<CardInstance>();
            foreach (var c in view.Hand)
                if (c.IsCharacter) cand.Add(c);
            if (view.OwnTraps.Count == 0)
                foreach (var c in view.Hand)
                    if (!c.IsCharacter && c.DefId == "C-20" && HasTrapCommand(legal, c.Id)) { cand.Add(c); break; }
            bool needOne = view.OwnUnits.Count == 0;
            var options = new List<(int mask, int score)>();
            for (int mask = 0; mask < 1 << cand.Count; mask++)
            {
                int cost = 0, score = 0;
                bool hasChar = false;
                for (int i = 0; i < cand.Count; i++)
                    if ((mask & 1 << i) != 0)
                    {
                        var c = cand[i];
                        cost += c.IsCharacter ? Unit.DefOf(c.Character.Class).Cost : c.Support.Cost;
                        score += c.IsCharacter ? AiWeights.SetupValue(c.Character.Class) : AiWeights.SetupSpikeValue;
                        hasChar |= c.IsCharacter;
                    }
                if (cost > view.SetupMana || (needOne && !hasChar)) continue;
                options.Add((mask, score));
            }
            var chosen = new List<CardInstance>();
            if (options.Count == 0) return chosen;
            int pick;
            if (level == AiLevel.Easy) pick = options[rng.NextInt(options.Count)].mask;
            else
            {
                options.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.mask.CompareTo(y.mask));
                pick = options[rng.NextInt(Math.Min(AiWeights.SetupTopCombos, options.Count))].mask;
            }
            for (int i = 0; i < cand.Count; i++)
                if ((pick & 1 << i) != 0) chosen.Add(cand[i]);
            return chosen;
        }

        static bool HasTrapCommand(IReadOnlyList<ICommand> legal, int cardId)
        {
            foreach (var c in legal)
                if (c is PlaceTrapCommand t && t.CardId == cardId) return true;
            return false;
        }

        // Placement commands sorted by their cell in seat order, so a random pick among ties is seat-symmetric.
        static void SeatOrder(List<ICommand> cmds, PlayerId seat) =>
            cmds.Sort((x, y) => GreedyAi.SeatIndex(seat, CellOf(x)).CompareTo(GreedyAi.SeatIndex(seat, CellOf(y))));

        static Hex CellOf(ICommand c) =>
            c is PlaceUnitCommand u ? u.Cell : c is PlaceTowerCommand t ? t.Cell : c is PlaceTrapCommand p ? p.Cell : default(Hex);

        // Closer to the Portal first; ties in seat order (GreedyAi.SeatIndex), so both seats place alike.
        static int FrontKey(PlayerId seat, Hex h) => Hex.Distance(h, Board.Portal) * 1000 + GreedyAi.SeatIndex(seat, h);

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
