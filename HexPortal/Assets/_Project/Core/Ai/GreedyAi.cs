using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>AI-03.</summary>
    public enum AiLevel { Easy, Normal }

    /// <summary>One scored legal command (tests and tools). Score = evaluation after the command minus before it, in the
    /// belief state; EndTurn = 0.</summary>
    internal sealed class AiCandidate
    {
        public ICommand Command;
        public int Score;
        /// <summary>An attack that kills an enemy unit (AI-03 guard).</summary>
        public bool Lethal;
        /// <summary>A move onto the Portal while it is open for the AI (W-01, AI-03 guard).</summary>
        public bool PortalStep;
        public int Index;
    }

    /// <summary>
    /// §13 greedy AI. AI-01: it sees only the PlayerView and the legal list the caller got from Engine.GetLegalCommands;
    /// it simulates on a BeliefState built from the view. Its Rng is its own (never the match Rng).
    /// AI-02: every legal command is applied once to a copy of the belief state and scored (AiWeights); the turn ends
    /// when nothing scores above ending it. AI-03: Normal takes the best; Easy picks uniformly among the best
    /// Catalog.AiEasyTopMoves. Neither ends the turn while a lethal attack or a step onto the open Portal is available.
    /// AI-04: setup (AiSetup). AI-06: the turn-start draw and the pre-pick by expected card value.
    /// AI-05: one simulation per legal command (one ply). Never sends clock commands (it only returns legal-list members).
    /// </summary>
    public static class GreedyAi
    {
        /// <summary>A member of <paramref name="legal"/> (which must be the player's GetLegalCommands, non-empty).</summary>
        public static ICommand Choose(PlayerView view, IReadOnlyList<ICommand> legal, AiLevel level, Rng aiRng)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (legal == null || legal.Count == 0) throw new ArgumentException("No legal command to choose from.", nameof(legal));
            if (aiRng == null) throw new ArgumentNullException(nameof(aiRng));
            if (view.Phase == GamePhase.Setup) return AiSetup.Choose(view, legal, aiRng);
            if (legal[0] is PrePickCommand || legal[0] is DrawCommand) return ChooseDraw(view, legal);

            var ranked = Rank(view, legal);
            bool guard = false;
            foreach (var c in ranked) guard |= c.Lethal || c.PortalStep;
            var pool = new List<AiCandidate>();
            foreach (var c in ranked)
                if (!(guard && c.Command is EndTurnCommand)) pool.Add(c);
            if (level == AiLevel.Normal) return pool[0].Command;
            return pool[aiRng.NextInt(Math.Min(Catalog.AiEasyTopMoves, pool.Count))].Command;
        }

        /// <summary>Every legal command scored, best first. Ties: EndTurn first (nothing beats it), then legal-list order.</summary>
        internal static List<AiCandidate> Rank(PlayerView view, IReadOnlyList<ICommand> legal)
        {
            var belief = BeliefState.From(view);
            var ctx = new Context(view, belief.Map);
            int before = Eval(belief, ctx);
            var list = new List<AiCandidate>();
            for (int i = 0; i < legal.Count; i++)
            {
                var c = legal[i];
                var cand = new AiCandidate { Command = c, Index = i };
                list.Add(cand);
                if (c is EndTurnCommand) continue;
                var sim = belief.Clone();
                try
                {
                    Engine.Apply(sim, c);
                }
                catch (IllegalCommandException)
                {
                    cand.Score = int.MinValue / 2; // the belief reproduces the legal list (AiTests), so never expected
                    continue;
                }
                cand.Score = Eval(sim, ctx) - before;
                if (c is AttackCommand a)
                {
                    var target = belief.UnitAt(a.Target);
                    cand.Lethal = target != null && target.Owner == ctx.Opp && sim.GetUnit(target.Id) == null;
                }
                cand.PortalStep = c is MoveCommand m && m.Dest == Board.Portal && ctx.PortalOpen;
            }
            list.Sort((x, y) =>
            {
                if (x.Score != y.Score) return y.Score.CompareTo(x.Score);
                bool ex = x.Command is EndTurnCommand, ey = y.Command is EndTurnCommand;
                if (ex != ey) return ex ? -1 : 1;
                return x.Index.CompareTo(y.Index);
            });
            return list;
        }

        // ---------- Draw and pre-pick (T-04 step 4, T-11, AI-06) ----------

        /// <summary>The draw option with the highest card value; Blind = the expected value over the public pool counts.
        /// Pre-pick: never None unless it is the only option. Ties: legal-list order.</summary>
        static ICommand ChooseDraw(PlayerView view, IReadOnlyList<ICommand> legal)
        {
            ICommand best = null;
            int bestValue = int.MinValue;
            foreach (var c in legal)
            {
                int slot = c is DrawCommand d ? d.Slot : ((PrePickCommand)c).Slot;
                if (slot == PrePickCommand.None) continue;
                int value = slot == DrawCommand.Blind ? BlindValue(view) : CardValue(view, view.Market[slot]);
                if (value > bestValue)
                {
                    best = c;
                    bestValue = value;
                }
            }
            return best ?? legal[0];
        }

        static int CardValue(PlayerView view, CardInstance card)
        {
            if (card == null) return int.MinValue / 2;
            return card.IsCharacter ? CharacterValue(view) : SupportValue(view, card.Support);
        }

        static int CharacterValue(PlayerView view)
        {
            int units = view.OwnUnits.Count;
            foreach (var c in view.Hand)
                if (c.IsCharacter) units++;
            return AiWeights.CharacterCard + (units < AiWeights.ShortUnits ? AiWeights.CharacterCardShort : 0);
        }

        static int SupportValue(PlayerView view, SupportCardDef d) =>
            AiWeights.SupportCard(d.Id) + (d.Category == CardCategory.Trap && HasActiveQuest(view, "Q-17") ? AiWeights.TrapCardTrapper : 0);

        /// <summary>D-06: uniform over the pooled cards; each pool's mix is assumed to be its Catalog mix (order and exact
        /// contents are hidden, V-09).</summary>
        static int BlindValue(PlayerView view)
        {
            long sum = 0, count = 0;
            for (int pool = 0; pool < view.PoolCounts.Count; pool++)
            {
                int n = view.PoolCounts[pool];
                if (n == 0) continue;
                long v = 0, copies = 0;
                if ((CardPool)pool == CardPool.Character)
                {
                    v = CharacterValue(view);
                    copies = 1;
                }
                else
                    foreach (var d in Catalog.SupportCards)
                        if ((d.Pool == SupportPool.Buff) == ((CardPool)pool == CardPool.Buff))
                        {
                            v += (long)SupportValue(view, d) * d.Copies;
                            copies += d.Copies;
                        }
                sum += n * v / copies;
                count += n;
            }
            return count == 0 ? int.MinValue / 2 : (int)(sum / count);
        }

        static bool HasActiveQuest(PlayerView view, string id)
        {
            for (int i = 0; i < view.QuestChoices.Count; i++)
                if (view.QuestChoices[i] == id && view.QuestStatuses[i] == QuestStatus.Active) return true;
            return false;
        }

        // ---------- Evaluation (AI-02) ----------

        /// <summary>What the evaluation needs from the view, computed once per decision.</summary>
        sealed class Context
        {
            public readonly PlayerId Me, Opp;
            public readonly bool[] Hidden = new bool[Board.Cells.Count];
            public readonly HashSet<int> Ghosts = new HashSet<int>();
            public readonly HashSet<string> Quests = new HashSet<string>();
            public readonly int Completed;
            public readonly bool PortalOpen, EnemyPortalOpen;
            public readonly List<Hex> Runes = new List<Hex>(), Wellsprings = new List<Hex>(), EnemyHome = new List<Hex>();

            public Context(PlayerView view, GameMap map)
            {
                Me = view.Viewer;
                Opp = Me.Opponent();
                foreach (var c in view.Cells)
                {
                    Hidden[Board.IndexOf(c.Cell)] = c.Visibility == CellVisibility.Hidden;
                    if (c.Unit != null && c.Unit.IsGhost) Ghosts.Add(c.Unit.Id);
                }
                for (int i = 0; i < view.QuestChoices.Count; i++)
                {
                    if (view.QuestStatuses[i] == QuestStatus.Active) Quests.Add(view.QuestChoices[i]);
                    else if (view.QuestStatuses[i] == QuestStatus.Completed) Completed++;
                }
                PortalOpen = Completed >= Catalog.PortalQuestsRequired;
                EnemyPortalOpen = view.OpponentCompletedQuests.Count >= Catalog.PortalQuestsRequired;
                foreach (var h in Board.Cells)
                {
                    var m = map.Get(h).Marker;
                    if (m == Marker.RuneStone) Runes.Add(h);
                    else if (m == Marker.Wellspring) Wellsprings.Add(h);
                    if (Board.IsHomeZone(h, Opp)) EnemyHome.Add(h);
                }
            }
        }

        static int Eval(GameState s, Context ctx)
        {
            var me = ctx.Me;
            if (s.IsOver) return s.Winner == me ? AiWeights.Win : s.Winner.HasValue ? -AiWeights.Win : 0;
            var progress = s.GetProgress(me);
            int score = 0;
            var own = new List<Unit>();
            var enemies = new List<Unit>();
            foreach (var u in s.Units)
            {
                if (u.Owner == me) own.Add(u);
                else enemies.Add(u);
                score += (u.Owner == me ? 1 : -1) * UnitValue(s, u);
            }

            // Kills (the belief starts at 0, so these are the kills of this command) and towers.
            score += progress.Kills * (AiWeights.Kill + (ctx.Quests.Contains("Q-12") ? AiWeights.QuestKill : 0));
            var ownTower = s.GetTower(me);
            var enemyTower = s.GetTower(ctx.Opp);
            score += AiWeights.OwnTowerHealth * ownTower.Health - AiWeights.EnemyTowerHealth * enemyTower.Health;
            if (ctx.Quests.Contains("Q-13")) score += AiWeights.QuestTowerDamage * progress.TowerDamage;

            score += QuestScore(s, ctx, own, progress);

            // W-01: the Portal.
            int lead = int.MaxValue;
            foreach (var u in own)
            {
                int d = Hex.Distance(u.Pos, Board.Portal);
                score -= AiWeights.PortalDistanceAll * d;
                lead = Math.Min(lead, d);
            }
            if (lead != int.MaxValue)
                score -= lead * (ctx.PortalOpen ? AiWeights.PortalLeadOpen : ctx.Completed > 0 ? AiWeights.PortalLeadOneQuest : AiWeights.PortalLead);
            var onPortal = s.UnitAt(Board.Portal);
            if (onPortal != null && onPortal.Owner == me && ctx.PortalOpen) score += AiWeights.PortalHoldOpen;
            if (onPortal != null && onPortal.Owner != me && ctx.EnemyPortalOpen) score -= AiWeights.EnemyOnOpenPortal;

            // Threat to the own tower.
            if (ownTower.IsPlaced)
                foreach (var e in enemies)
                {
                    int d = Hex.Distance(e.Pos, ownTower.Pos);
                    if (d <= AiWeights.TowerThreatRange) score -= AiWeights.TowerThreat * (AiWeights.TowerThreatRange + 1 - d);
                }

            // Position, exploration, efficiency.
            var visible = Visibility.VisibleCells(s, me);
            foreach (var h in visible)
                if (ctx.Hidden[Board.IndexOf(h)]) score += AiWeights.Explore;
            foreach (var u in own)
            {
                var tile = s.Map.Get(u.Pos);
                if (tile.Biome == u.Biome) score += AiWeights.Biome;
                if (tile.Marker == Marker.Wellspring) score += AiWeights.Wellspring;
            }
            score -= AiWeights.ManaSpent * s.GetMana(me) + AiWeights.EnergySpent * s.GetEnergy(me);
            score += AiWeights.CardInHand * s.GetHand(me).Count;

            // Overwatch value and exposure (known enemies only; ghosts at a reduced share: they may have moved).
            foreach (var u in own)
            {
                int risk = 0, watch = 0;
                foreach (var e in enemies)
                {
                    int d = Hex.Distance(e.Pos, u.Pos);
                    if (u.OnOverwatch && d <= e.Def.Move + u.Def.MaxRange) watch++;
                    if (d <= e.Def.Move + e.Def.MaxRange)
                    {
                        int r = Combat.AttackDamage(s, e, false) * AiWeights.RiskPerDamage;
                        risk += ctx.Ghosts.Contains(e.Id) ? r * AiWeights.GhostRiskPercent / 100 : r;
                    }
                }
                score -= risk;
                if (u.OnOverwatch) score += AiWeights.Overwatch + AiWeights.OverwatchPerThreat * watch;
                if (enemyTower.IsPlaced)
                {
                    int d = Hex.Distance(u.Pos, enemyTower.Pos);
                    if (d >= Catalog.Tower.MinRange && d <= Catalog.Tower.MaxRange) score -= AiWeights.EnemyTowerRange;
                }
                // Caution (interim reading of "next to Hidden cells"): never-seen cells just beyond the unit's sight.
                foreach (var h in Board.Cells)
                    if (ctx.Hidden[Board.IndexOf(h)] && !visible.Contains(h) && Hex.Distance(h, u.Pos) == u.Def.Sight + 1)
                        score -= AiWeights.HiddenNeighbour;
            }
            return score;
        }

        static int UnitValue(GameState s, Unit u)
        {
            int atk = u.Def.Attack;
            if (u.Buff != null && u.Buff.Kind == EffectKind.AttackBonus) atk += u.Buff.Def.Amount;
            if (u.Debuff != null && u.Debuff.Kind == EffectKind.AttackPenalty) atk -= u.Debuff.Def.Amount;
            int v = AiWeights.UnitBase + AiWeights.HealthPoint * u.Health + AiWeights.AttackPoint * Math.Max(0, atk);
            if (u.Buff != null && u.Buff.Kind == EffectKind.BlockNextDamage) v += AiWeights.Shield;
            if (u.IsRooted) v -= AiWeights.Rooted;
            if (u.Debuff != null && u.Debuff.Kind == EffectKind.Poison) v -= AiWeights.PoisonPerTurn * u.Debuff.TurnsLeft;
            return v;
        }

        /// <summary>Progress on the own Active quests (§8).</summary>
        static int QuestScore(GameState s, Context ctx, List<Unit> own, PlayerProgress progress)
        {
            int score = 0;
            var q = ctx.Quests;
            if (q.Contains("Q-10") || q.Contains("Q-11"))
            {
                int held = 0;
                foreach (var h in ctx.Runes)
                {
                    var u = s.UnitAt(h);
                    if (u != null && u.Owner == ctx.Me) held++;
                }
                if (q.Contains("Q-10") && held > 0) score += AiWeights.QuestRuneHeld;
                if (q.Contains("Q-11")) score += held * AiWeights.QuestRunePerStone + (held >= 2 ? AiWeights.QuestRuneBoth : 0);
                if (held < (q.Contains("Q-11") ? 2 : 1)) score -= AiWeights.QuestApproach * Approach(s, ctx, own, ctx.Runes);
            }
            if (q.Contains("Q-15"))
            {
                int amount = Quest("Q-15").Amount, n = 0;
                foreach (var u in own)
                    if (s.Map.Get(u.Pos).Biome == u.Biome) n++;
                score += Math.Min(n, amount) * AiWeights.QuestBiomeUnit + (n >= amount ? AiWeights.QuestBiomeAll : 0);
            }
            if (q.Contains("Q-17"))
            {
                score += AiWeights.QuestTrapSprung * progress.TrapsSprung;
                foreach (var t in s.Traps)
                    if (t.Owner == ctx.Me) score += AiWeights.QuestTrap;
            }
            if (q.Contains("Q-18"))
            {
                bool on = false;
                foreach (var h in ctx.Wellsprings)
                {
                    var u = s.UnitAt(h);
                    on |= u != null && u.Owner == ctx.Me;
                }
                score += on ? AiWeights.QuestWellspring : -AiWeights.QuestApproach * Approach(s, ctx, own, ctx.Wellsprings);
            }
            if (q.Contains("Q-19"))
            {
                int amount = Quest("Q-19").Amount, n = 0;
                foreach (var u in own)
                    if (Board.IsHomeZone(u.Pos, ctx.Opp)) n++;
                score += Math.Min(n, amount) * AiWeights.QuestDeepRaidUnit;
                if (n < amount) score -= AiWeights.QuestApproach * Approach(s, ctx, own, ctx.EnemyHome);
            }
            return score;
        }

        /// <summary>Steps from the closest own unit to the closest target cell not held by an own unit (0 if none).</summary>
        static int Approach(GameState s, Context ctx, List<Unit> own, List<Hex> targets)
        {
            int best = int.MaxValue;
            foreach (var t in targets)
            {
                var on = s.UnitAt(t);
                if (on != null && on.Owner == ctx.Me) continue;
                foreach (var u in own) best = Math.Min(best, Hex.Distance(u.Pos, t));
            }
            return best == int.MaxValue ? 0 : best;
        }

        static QuestDef Quest(string id)
        {
            foreach (var d in Catalog.Quests)
                if (d.Id == id) return d;
            throw new KeyNotFoundException(id);
        }
    }
}
