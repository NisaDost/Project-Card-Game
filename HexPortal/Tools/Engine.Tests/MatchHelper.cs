using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Tests
{
    /// <summary>What Scramble may change. Default = everything hidden from the viewer (the static view/legal check).
    /// The event checks turn parts off where a command legitimately depends on them; every such case is commented
    /// at the call site.</summary>
    public sealed class ScrambleSpec
    {
        /// <summary>Swap opponent hand cards with same-pool pool cards, shuffle the pools and replace the Rng. Off: replace
        /// the opponent's hand card identities in place (same ids, same pools), pools and Rng untouched.</summary>
        public bool Full = true;
        public bool Quests = true;
        public bool Money = true;       // opponent Mana / Energy
        public bool PrePick = true;
        public bool MoveUnits = true;   // hidden enemy units
        public bool UnitState = true;   // Health, buff, debuff of hidden enemy units
        public bool Overwatch = true;   // overwatch flag of hidden enemy units
        public bool MoveTower = true;
        public bool MoveTraps = true;
        /// <summary>Enemy units left completely untouched.</summary>
        public Func<Unit, bool> KeepUnit = u => false;
        /// <summary>Cells no hidden enemy unit may be moved into.</summary>
        public Func<Hex, bool> NoEntry = h => false;
        /// <summary>Opponent hand cards whose identity is kept.</summary>
        public HashSet<int> KeepCards = new HashSet<int>();
    }

    /// <summary>Shared helpers for the M4 match tests: scripted setup, random play, canonical serializers, scrambling.</summary>
    public static class MatchHelper
    {
        public static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        public static readonly PlayerId[] Players = { PlayerId.A, PlayerId.B };

        /// <summary>D-03 + D-04: the size of a freshly dealt hand.</summary>
        public static int DealtHandSize =>
            Catalog.Units.Count * Catalog.DealCharactersPerClass + Catalog.DealCommonSupport + Catalog.DealRareSupport;

        /// <summary>First legal choice at every setup step; places <paramref name="units"/> characters, no traps.</summary>
        public static List<GameEvent> SetupPlayer(GameState s, PlayerId p, int units = 3, bool finish = true)
        {
            var events = new List<GameEvent>();
            events.AddRange(Engine.Apply(s, Engine.GetLegalCommands(s, p).OfType<ChooseQuestsCommand>().First()));
            events.AddRange(Engine.Apply(s, Engine.GetLegalCommands(s, p).OfType<ChoosePassiveCommand>().First()));
            events.AddRange(Engine.Apply(s, Engine.GetLegalCommands(s, p).OfType<PlaceTowerCommand>().First()));
            for (int i = 0; i < units; i++)
                events.AddRange(Engine.Apply(s, Engine.GetLegalCommands(s, p).OfType<PlaceUnitCommand>().First()));
            if (finish) events.AddRange(Engine.Apply(s, new FinishSetupCommand(p)));
            return events;
        }

        public static GameState StartedMatch(ulong seed, int units = 3)
        {
            var s = Match.Create(seed);
            SetupPlayer(s, A, units);
            SetupPlayer(s, B, units);
            return s;
        }

        /// <summary>A random legal command: a command type first, then one of that type. EndTurn/Finish less often.</summary>
        public static ICommand Choose(List<ICommand> legal, Rng rng)
        {
            var groups = legal.GroupBy(x => x.GetType()).Select(g => g.ToList()).ToList();
            var rare = new[] { typeof(EndTurnCommand), typeof(FinishSetupCommand) };
            var common = groups.Where(g => !rare.Contains(g[0].GetType())).ToList();
            if (common.Count == 0 || (groups.Count > common.Count && rng.NextInt(5) == 0))
                return groups.First(g => rare.Contains(g[0].GetType()))[0];
            var group = common[rng.NextInt(common.Count)];
            return group[rng.NextInt(group.Count)];
        }

        /// <summary>The next command of a random game: setup commands of either player (sometimes a setup timeout),
        /// else the waiting player's pre-pick (sometimes), a turn timeout (rarely) or the active player's choice.</summary>
        public static ICommand NextRandom(GameState s, Rng rng)
        {
            if (s.Phase == GamePhase.Setup)
            {
                var open = Players.Where(p => !s.IsSetupFinished(p)).ToList();
                var p = open[rng.NextInt(open.Count)];
                if (rng.NextInt(10) == 0) return new SetupTimeoutCommand(p);
                return Choose(Engine.GetLegalCommands(s, p), rng);
            }
            var waiting = s.ActivePlayer.Opponent();
            if (rng.NextInt(6) == 0)
            {
                var pre = Engine.GetLegalCommands(s, waiting);
                return pre[rng.NextInt(pre.Count)];
            }
            if (rng.NextInt(40) == 0) return new TurnTimeoutCommand(s.ActivePlayer);
            return Choose(Engine.GetLegalCommands(s, s.ActivePlayer), rng);
        }

        // ---------- Canonical serializers ----------

        public static string Serialize(PlayerView v)
        {
            var sb = new StringBuilder();
            sb.Append("viewer=").Append(v.Viewer).Append(" phase=").Append(v.Phase).Append(" round=").Append(v.Round)
              .Append(" turn=").Append(v.TurnIndex).Append(" active=").Append(v.ActivePlayer)
              .Append(" result=").Append(v.Result == null ? "-" : v.Result.Winner + "/" + v.Result.Reason + "/" + v.Result.Criterion).Append('\n');
            foreach (var c in v.Cells)
            {
                sb.Append(c.Cell).Append(':').Append(c.Visibility);
                if (c.TerrainKnown) sb.Append(' ').Append(c.Tile);
                if (c.Unit != null) sb.Append(" U[").Append(Unit(c.Unit)).Append(']');
                if (c.HasTower) sb.Append(" T[").Append(c.TowerOwner).Append(' ').Append(c.TowerHealth).Append(c.TowerIsGhost ? " ghost" : "").Append(']');
                sb.Append('\n');
            }
            sb.Append("own=").Append(string.Join(";", v.OwnUnits.Select(Unit))).Append('\n');
            sb.Append("enemy=").Append(string.Join(";", v.EnemyUnits.Select(Unit))).Append('\n');
            sb.Append("ownTower=").Append(v.OwnTowerPos).Append(' ').Append(v.OwnTowerHealth).Append(' ').Append(v.OwnTowerShotAvailable)
              .Append(' ').Append(v.OwnTowerRevealedUntilTurn).Append('\n');
            sb.Append("enemyTower=").Append(v.EnemyTowerPos).Append(' ').Append(v.EnemyTowerHealth).Append(' ').Append(v.EnemyTowerShotAvailable).Append('\n');
            sb.Append("hand=").Append(string.Join(",", v.Hand.Select(c => c.Id + ":" + c.DefId))).Append('\n');
            sb.Append("mana=").Append(v.Mana).Append(" energy=").Append(v.Energy).Append(" pending=").Append(v.DrawPending)
              .Append(" prepick=").Append(v.PrePickSlot).Append('/').Append(v.PrePickCardId).Append('\n');
            sb.Append("setup=").Append(v.SetupStep).Append(" oppFinished=").Append(v.OpponentSetupFinished).Append('\n');
            sb.Append("quests=").Append(string.Join(",", v.QuestOffer)).Append(" chosen=").Append(string.Join(",", v.QuestChoices)).Append('\n');
            sb.Append("passives=").Append(string.Join(",", v.PassiveOffer)).Append(" chosen=").Append(v.PassiveChoice).Append('\n');
            sb.Append("traps=").Append(string.Join(",", v.OwnTraps.Select(t => t.CardId + ":" + t.DefId + "@" + t.Pos))).Append('\n');
            sb.Append("oppHand=").Append(v.OpponentHandCount)
              .Append(" market=").Append(string.Join(",", v.Market.Select(c => c == null ? "-" : c.Id + ":" + c.DefId)))
              .Append(" pools=").Append(string.Join(",", v.PoolCounts))
              .Append(" timeouts=").Append(v.OwnConsecutiveTimeouts).Append('/').Append(v.OpponentConsecutiveTimeouts).Append('\n');
            return sb.ToString();
        }

        static string Unit(UnitView u) =>
            u.Id + " " + u.Owner + " " + u.Class + " " + u.Biome + " " + u.Pos + " hp" + u.Health + (u.IsGhost ? " ghost" : "")
            + " b=" + u.BuffId + "/" + u.BuffTurnsLeft + " d=" + u.DebuffId + "/" + u.DebuffTurnsLeft + " ow=" + u.OnOverwatch
            + " f=" + u.ActedThisTurn + u.MovedThisTurn + u.MovedLastOwnTurn + u.MoveBonus + " rv=" + u.RevealedUntilTurn;

        /// <summary>Type name and every public instance field / property (declared on the event), in name order.</summary>
        public static string Serialize(IEnumerable<GameEvent> events) => string.Join("\n", events.Select(Serialize));

        public static string Serialize(GameEvent e)
        {
            var t = e.GetType();
            var parts = new List<string>();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name))
                parts.Add(f.Name + "=" + Value(f.GetValue(e)));
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(p => p.Name))
                parts.Add(p.Name + "=" + Value(p.GetValue(e)));
            return t.Name + "(" + string.Join(" ", parts) + ")";
        }

        static string Value(object o)
        {
            if (o == null) return "null";
            if (o is GameResult r) return r.Winner + "/" + r.Reason + "/" + r.Criterion;
            if (o is string s) return s;
            if (o is System.Collections.IEnumerable list) return "[" + string.Join(",", list.Cast<object>().Select(Value)) + "]";
            return o.ToString();
        }

        /// <summary>Unit ids an event refers to (UnitId, SourceUnitId, TargetUnitId fields; positive values only).</summary>
        public static IEnumerable<int> UnitIds(GameEvent e)
        {
            foreach (var name in new[] { "UnitId", "SourceUnitId", "TargetUnitId" })
            {
                var f = e.GetType().GetField(name);
                if (f != null && f.FieldType == typeof(int) && (int)f.GetValue(e) > 0) yield return (int)f.GetValue(e);
            }
        }

        // ---------- Scrambling data hidden from a player ----------

        /// <summary>Everything hidden from p (Leak test B, view and legal lists).</summary>
        public static GameState Scramble(GameState original, PlayerId p, Rng rng) => Scramble(original, p, rng, new ScrambleSpec());

        /// <summary>Clone the state and change only what <paramref name="p"/> may not know, as allowed by the spec:
        /// opponent hand contents, quest/passive offers and choices, pool order and Rng, opponent Mana/Energy/pre-pick,
        /// action flags of enemy units, and position, Health, effects and overwatch of enemy units (and the enemy tower)
        /// on cells not Visible to p, enemy untriggered traps. Scrambled positions stay legal: empty, not rock, not Visible
        /// to p, not on p's traps; during setup inside the opponent's home zone (units, tower) or half (traps).</summary>
        public static GameState Scramble(GameState original, PlayerId p, Rng rng, ScrambleSpec spec)
        {
            var s = original.Clone();
            var o = p.Opponent();
            var visible = Visibility.VisibleCells(s, p);
            bool setup = s.Phase == GamePhase.Setup;

            var hand = s.HandList(o);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (spec.KeepCards.Contains(c.Id)) continue;
                if (spec.Full)
                {
                    // Swap with a random card of the same pool (pool counts stay the same).
                    var pool = s.PoolList(c.Pool);
                    if (pool.Count == 0) continue;
                    int j = rng.NextInt(pool.Count);
                    hand[i] = pool[j];
                    pool[j] = c;
                }
                else if (c.IsCharacter)
                    hand[i] = new CardInstance(c.Id, Catalog.CharacterCards[rng.NextInt(Catalog.CharacterCards.Count)], null);
                else
                {
                    var defs = Catalog.SupportCards.Where(d => d.Pool == c.Support.Pool).ToList();
                    hand[i] = new CardInstance(c.Id, null, defs[rng.NextInt(defs.Count)]);
                }
            }
            if (spec.Full)
            {
                foreach (var pool in new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }) rng.Shuffle(s.PoolList(pool));
                s.Rng = new Rng(rng.NextULong());
            }

            if (spec.Quests)
            {
                var quests = Catalog.Quests.ToList();
                rng.Shuffle(quests);
                var qOffer = quests.Take(Catalog.QuestOffer).ToList();
                s.SetQuestOffer(o, qOffer);
                if (s.GetQuestChoices(o).Count > 0) s.SetQuestChoices(o, qOffer.Take(Catalog.QuestPick).ToList());
                var passives = Catalog.Passives.ToList();
                rng.Shuffle(passives);
                var pOffer = passives.Take(Catalog.PassiveOffer).ToList();
                s.SetPassiveOffer(o, pOffer);
                if (s.GetPassiveChoice(o) != null) s.SetPassiveChoice(o, pOffer[rng.NextInt(pOffer.Count)]);
            }
            if (spec.Money)
            {
                s.SetMana(o, rng.NextInt(0, 8));
                s.SetEnergy(o, rng.NextInt(0, Catalog.EnergyPerTurn + 1));
            }
            if (spec.PrePick && !setup)
            {
                var pre = Pools.PrePickOptions(s);
                int slot = pre[rng.NextInt(pre.Count)];
                s.SetPrePick(o, slot, slot >= 0 && s.GetMarket((CardPool)slot) != null ? s.GetMarket((CardPool)slot).Id : 0);
            }

            bool Free(Hex h) =>
                Movement.IsEmpty(s, h) && !visible.Contains(h) && !s.Traps.Any(t => t.Owner == p && t.Pos == h)
                && (!setup || Board.IsHomeZone(h, o)) && !spec.NoEntry(h);

            foreach (var u in s.Units)
            {
                if (u.Owner != o || spec.KeepUnit(u)) continue;
                u.ActedThisTurn = rng.NextInt(2) == 0;
                u.MovedThisTurn = rng.NextInt(2) == 0;
                u.MovedLastOwnTurn = rng.NextInt(2) == 0;
                u.MoveBonus = rng.NextInt(3);
                if (visible.Contains(u.Pos)) continue;
                if (spec.MoveUnits)
                {
                    var cells = Board.Cells.Where(Free).ToList();
                    if (cells.Count > 0) u.Pos = cells[rng.NextInt(cells.Count)];
                }
                if (spec.UnitState)
                {
                    u.Health = rng.NextInt(1, u.Def.Health + 1);
                    u.Buff = rng.NextInt(2) == 0 ? null : new ActiveEffect(Catalog.SupportCards.First(c => c.Id == "C-10"));
                    u.Debuff = rng.NextInt(2) == 0 ? null : new ActiveEffect(Catalog.SupportCards.First(c => c.Id == "C-16"));
                }
                if (spec.Overwatch) u.OnOverwatch = rng.NextInt(2) == 0;
            }
            var tower = s.GetTower(o);
            if (spec.MoveTower && tower.IsPlaced && !visible.Contains(tower.Pos))
            {
                var cells = Board.Cells.Where(h => Free(h) && Board.IsHomeZone(h, o)).ToList();
                if (cells.Count > 0) tower.Pos = cells[rng.NextInt(cells.Count)];
            }

            // Enemy traps: new cells (not rock/Portal, no unit or tower, not a second own trap on one cell).
            var traps = s.TrapList;
            for (int i = 0; spec.MoveTraps && i < traps.Count; i++)
            {
                var t = traps[i];
                if (t.Owner != o) continue;
                var cells = Board.Cells.Where(h => h != Board.Portal && Movement.IsEmpty(s, h)
                    && (!setup || Board.IsInHalf(h, o)) && !traps.Any(x => x.Owner == o && x.Pos == h)).ToList();
                if (cells.Count > 0) traps[i] = new Trap(o, cells[rng.NextInt(cells.Count)], t.Card);
            }
            return s;
        }
    }
}
