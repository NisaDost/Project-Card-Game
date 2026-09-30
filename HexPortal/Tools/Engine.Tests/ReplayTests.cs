using System;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // Determinism (seed + commands -> same hash) and legality: every legal command applies, every other
    // command is rejected without touching the state.
    public class ReplayTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static readonly UnitClass[] Classes =
            { UnitClass.Guardian, UnitClass.Rider, UnitClass.Archer, UnitClass.Mage, UnitClass.Healer };
        static readonly Biome[] Biomes = { Biome.Forest, Biome.Desert, Biome.Snow };

        /// <summary>A generated map, one unit of each class per side in the home rows, mirrored, plus a rock pair.
        /// Full pools, dealt hands (D-03, D-04), an open Market (D-05), and A's first turn start (T-04).</summary>
        static GameState NewGame(ulong seed)
        {
            var b = new TestBoard(MapGenerator.Generate(seed)).FullPools();
            b.Rock(new Hex(-2, 1)).Rock(new Hex(2, -1));
            var homeA = new[] { Hex.FromDoubled(2, 8), Hex.FromDoubled(4, 8), Hex.FromDoubled(8, 8), Hex.FromDoubled(10, 8), Hex.FromDoubled(5, 7) };
            for (int i = 0; i < Classes.Length; i++)
            {
                var posA = homeA[i];
                b.Unit(Classes[i], A, posA, Biomes[i % 3]);
                b.Unit(Classes[i], B, posA.Mirror(), Biomes[i % 3]);
            }
            var s = b.Build();
            Pools.Deal(s);
            Pools.OpenMarket(s, new List<GameEvent>());
            Turn.StartTurn(s, new List<GameEvent>());
            return s;
        }

        static ICommand Pick(GameState s, int index)
        {
            var legal = Engine.GetLegalCommands(s, s.ActivePlayer);
            return legal[index % legal.Count];
        }

        /// <summary>A random legal command: a kind first (so card plays with many targets do not crowd out attacks),
        /// then one of that kind. EndTurn is picked less often so units fight.</summary>
        static ICommand Choose(List<ICommand> legal, Rng rng)
        {
            var groups = legal.GroupBy(x => x.GetType()).Select(g => g.ToList()).ToList();
            var nonEnd = groups.Where(g => !(g[0] is EndTurnCommand)).ToList();
            if (nonEnd.Count == 0 || (groups.Count > nonEnd.Count && rng.NextInt(6) == 0)) return legal[legal.Count - 1];
            var group = nonEnd[rng.NextInt(nonEnd.Count)];
            return group[rng.NextInt(group.Count)];
        }

        [Test]
        public void Replay_SameSeedAndCommandsGiveSameHash()
        {
            int[] script =
            {
                3, 7, 1, 0, 12, 5, 9, 2, 44, 6, 8, 1, 13, 21, 4, 0, 17, 3, 5, 11, 2, 29, 7, 1, 0, 9, 14, 6,
            };
            const ulong seed = 42;

            // First run: pick commands by script index and record them.
            var s1 = NewGame(seed);
            var commands = new List<ICommand>();
            foreach (int i in script)
            {
                if (s1.Winner.HasValue) break;
                var c = Pick(s1, i);
                commands.Add(c);
                Engine.Apply(s1, c);
            }
            Assert.That(commands.Count, Is.GreaterThanOrEqualTo(20));
            Assert.That(commands.Count(c => !(c is EndTurnCommand)), Is.GreaterThan(5));

            // Replay the recorded command list on a fresh state.
            var s2 = NewGame(seed);
            foreach (var c in commands) Engine.Apply(s2, c);
            Assert.That(StateHash.Compute(s2), Is.EqualTo(StateHash.Compute(s1)));

            // A different command somewhere changes the hash.
            var s3 = NewGame(seed);
            var other = Engine.GetLegalCommands(s3, s3.ActivePlayer).First(x => !x.Equals(commands[0]) && !(x is EndTurnCommand));
            Engine.Apply(s3, other);
            for (int k = 1; k < commands.Count; k++)
            {
                var legal = Engine.GetLegalCommands(s3, s3.ActivePlayer);
                if (legal.Count == 0) break;
                Engine.Apply(s3, legal.Contains(commands[k]) ? commands[k] : legal[legal.Count - 1]);
            }
            Assert.That(StateHash.Compute(s3), Is.Not.EqualTo(StateHash.Compute(s1)));
        }

        [Test]
        public void Replay_CardMatchIsDeterministic()
        {
            // Both players' commands (incl. pre-picks, draws, deploys, cards) recorded, then replayed.
            const ulong seed = 11;
            var rng = new Rng(3);
            var s1 = NewGame(seed);
            var commands = new List<ICommand>();
            for (int step = 0; step < 300 && !s1.Winner.HasValue; step++)
            {
                var waiting = Engine.GetLegalCommands(s1, s1.ActivePlayer.Opponent());
                var c = rng.NextInt(5) == 0 ? waiting[rng.NextInt(waiting.Count)] : Choose(Engine.GetLegalCommands(s1, s1.ActivePlayer), rng);
                commands.Add(c);
                Engine.Apply(s1, c);
            }
            Assert.That(commands.OfType<PlayCardCommand>().Count(), Is.GreaterThan(5));
            Assert.That(commands.OfType<DrawCommand>().Count(), Is.GreaterThan(0));
            var s2 = NewGame(seed);
            foreach (var c in commands) Engine.Apply(s2, c);
            Assert.That(StateHash.Compute(s2), Is.EqualTo(StateHash.Compute(s1)));
            Assert.That(StateHash.Compute(NewGame(seed)), Is.Not.EqualTo(StateHash.Compute(NewGame(seed + 1))));
        }

        readonly Dictionary<Type, int> kinds = new Dictionary<Type, int>();
        readonly HashSet<string> playedCards = new HashSet<string>();

        void Count(ICommand c) => kinds[c.GetType()] = kinds.TryGetValue(c.GetType(), out int n) ? n + 1 : 1;

        [Test]
        public void Legal_AllLegalCommandsApplyWithoutError()
        {
            foreach (ulong seed in new ulong[] { 1, 2, 3, 7, 99 })
            {
                var rng = new Rng(seed);
                var s = NewGame(seed);
                int steps = 0, games = 1, attacks = 0;
                while (steps < 250)
                {
                    if (s.Winner.HasValue)
                    {
                        s = NewGame(seed + (ulong)(1000 * games++));
                        continue;
                    }
                    // T-11: the waiting player may only pre-pick.
                    var inactive = s.ActivePlayer.Opponent();
                    var waiting = Engine.GetLegalCommands(s, inactive);
                    Assert.That(waiting.All(x => x is PrePickCommand), Is.True);
                    Assert.That(waiting, Does.Contain(new PrePickCommand(inactive, PrePickCommand.None)));
                    if (rng.NextInt(5) == 0)
                    {
                        var pre = waiting[rng.NextInt(waiting.Count)];
                        Count(pre);
                        Assert.DoesNotThrow(() => Engine.Apply(s, pre), "seed " + seed + " step " + steps + ": " + pre);
                    }
                    var legal = Engine.GetLegalCommands(s, s.ActivePlayer);
                    Assert.That(legal, Is.Not.Empty);
                    // T-04: a pending draw means draw options only; otherwise EndTurn closes the list.
                    if (s.IsDrawPending(s.ActivePlayer)) Assert.That(legal.All(x => x is DrawCommand), Is.True);
                    else Assert.That(legal.Last(), Is.InstanceOf<EndTurnCommand>());
                    var c = Choose(legal, rng);
                    if (c is AttackCommand) attacks++;
                    Count(c);
                    if (c is PlayCardCommand pc) playedCards.Add(s.FindInHand(pc.Player, pc.CardId).DefId);
                    Assert.DoesNotThrow(() => Engine.Apply(s, c), "seed " + seed + " step " + steps + ": " + c);
                    AssertInvariants(s);
                    steps++;
                }
                Assert.That(attacks, Is.GreaterThan(0), "seed " + seed);
            }
            foreach (var kind in new[] { typeof(MoveCommand), typeof(AttackCommand), typeof(OverwatchCommand), typeof(DeployCommand),
                         typeof(PlayCardCommand), typeof(DrawCommand), typeof(PrePickCommand), typeof(EndTurnCommand) })
                Assert.That(kinds.ContainsKey(kind), Is.True, kind.Name + " never played");
            Assert.That(playedCards.Count, Is.GreaterThanOrEqualTo(10), "support cards played: " + string.Join(",", playedCards));
        }

        static void AssertInvariants(GameState s)
        {
            var cells = new HashSet<Hex>();
            int lastId = 0;
            foreach (var u in s.Units)
            {
                Assert.That(u.Id, Is.GreaterThan(lastId));
                lastId = u.Id;
                Assert.That(u.Health, Is.InRange(1, u.Def.Health));
                Assert.That(Board.IsOnBoard(u.Pos), Is.True);
                Assert.That(cells.Add(u.Pos), Is.True, "two units on " + u.Pos);
                Assert.That(s.Map.Get(u.Pos).Marker, Is.Not.EqualTo(Marker.Rock));
                Assert.That(u.Pos, Is.Not.EqualTo(s.GetTower(A).Pos).And.Not.EqualTo(s.GetTower(B).Pos));
                if (u.Buff != null) Assert.That(u.Buff.Def.Pool, Is.EqualTo(SupportPool.Buff));
                if (u.Debuff != null) Assert.That(u.Debuff.Def.Pool, Is.EqualTo(SupportPool.DebuffTrap));
            }
            Assert.That(s.GetEnergy(s.ActivePlayer), Is.InRange(0, Catalog.EnergyPerTurn));

            // Cards: every instance is in exactly one place; Mana never negative; C-31 trap limit.
            var ids = new HashSet<int>();
            foreach (var p in new[] { A, B })
            {
                Assert.That(s.GetMana(p), Is.GreaterThanOrEqualTo(0));
                Assert.That(Traps.ActiveCount(s, p), Is.LessThanOrEqualTo(Catalog.MaxActiveTraps));
                foreach (var c in s.GetHand(p)) Assert.That(ids.Add(c.Id), Is.True);
            }
            foreach (var slot in new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap })
            {
                foreach (var c in s.GetPool(slot)) Assert.That(ids.Add(c.Id) && c.Pool == slot, Is.True);
                var m = s.GetMarket(slot);
                if (m != null) Assert.That(ids.Add(m.Id) && m.Pool == slot, Is.True);
            }
            foreach (var t in s.Traps)
            {
                Assert.That(ids.Add(t.Card.Id), Is.True);
                Assert.That(t.Card.Support.Category, Is.EqualTo(CardCategory.Trap));
                Assert.That(s.Map.Get(t.Pos).Marker, Is.Not.EqualTo(Marker.Rock).And.Not.EqualTo(Marker.Portal));
            }
        }

        [Test]
        public void Legal_IllegalCommandsAreRejected()
        {
            int rejected = 0;
            foreach (ulong seed in new ulong[] { 5, 6, 8 })
            {
                var rng = new Rng(seed);
                var s = NewGame(seed);
                for (int step = 0; step < 150 && !s.Winner.HasValue; step++)
                {
                    // Random commands (any player, any unit/card id incl. dead/unknown, any cell) not in the legal set.
                    for (int k = 0; k < 20; k++)
                    {
                        var c = RandomCommand(s, rng);
                        if (Engine.GetLegalCommands(s, c.Player).Contains(c)) continue;
                        AssertRejected(s, c);
                        rejected++;
                    }
                    var waiting = Engine.GetLegalCommands(s, s.ActivePlayer.Opponent());
                    if (rng.NextInt(4) == 0) Engine.Apply(s, waiting[rng.NextInt(waiting.Count)]);
                    Engine.Apply(s, Choose(Engine.GetLegalCommands(s, s.ActivePlayer), rng));
                }
            }
            Assert.That(rejected, Is.GreaterThan(1500));

            // Named cases.
            var t = new TestBoard();
            int archer = t.Unit(UnitClass.Archer, A, new Hex(0, 0));
            int guardian = t.Unit(UnitClass.Guardian, A, new Hex(-3, 0));
            int enemy = t.Unit(UnitClass.Guardian, B, new Hex(1, 0));
            t.Unit(UnitClass.Guardian, B, new Hex(1, 2)); // off the Archer's lines
            var st = t.Build();
            AssertRejected(st, new AttackCommand(A, archer, new Hex(1, 2)));           // out of range
            AssertRejected(st, new AttackCommand(A, archer, new Hex(3, -3)));          // non-visible cell
            AssertRejected(st, new MoveCommand(A, archer, new Hex(3, -3)));            // non-visible / too far
            AssertRejected(st, new AttackCommand(A, archer, new Hex(0, 1)));           // empty cell
            AssertRejected(st, new MoveCommand(A, enemy, new Hex(2, 0)));              // other player's unit
            AssertRejected(st, new MoveCommand(B, enemy, new Hex(2, 0)));              // not your turn
            AssertRejected(st, new EndTurnCommand(B));                                 // not your turn
            AssertRejected(st, new MoveCommand(A, 999, new Hex(2, 0)));                // unknown unit
            AssertRejected(st, new MoveCommand(A, archer, new Hex(0, 0)));             // stay in place
            AssertRejected(st, new DrawCommand(A, DrawCommand.Blind));                 // no draw pending
            Engine.Apply(st, new MoveCommand(A, guardian, new Hex(-2, 0)));
            AssertRejected(st, new MoveCommand(A, guardian, new Hex(-1, 0)));          // second action
            st.SetEnergy(A, 0);
            AssertRejected(st, new AttackCommand(A, archer, new Hex(1, 0)));           // 0 Energy
            AssertRejected(st, new OverwatchCommand(A, archer));                       // 0 Energy
            Assert.Throws<ArgumentNullException>(() => Engine.Apply(st, null));

            var w = new TestBoard().Tower(B, new Hex(2, -2), 1);
            int wa = w.Unit(UnitClass.Archer, A, new Hex(-1, -2));
            w.Market("C-10");
            var ws = w.Build();
            Engine.Apply(ws, new AttackCommand(A, wa, new Hex(2, -2)));
            Assert.That(ws.Winner, Is.EqualTo(A));
            AssertRejected(ws, new EndTurnCommand(A));                                 // after game over
            AssertRejected(ws, new PrePickCommand(B, (int)CardPool.Buff));             // after game over
            Assert.That(Engine.GetLegalCommands(ws, B), Is.Empty);
        }

        static void AssertRejected(GameState s, ICommand c)
        {
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, c), c.ToString());
            Assert.That(StateHash.Compute(s), Is.EqualTo(before), c.ToString());
        }

        static ICommand RandomCommand(GameState s, Rng rng)
        {
            var player = rng.NextInt(2) == 0 ? A : B;
            int unitId = rng.NextInt(0, s.NextUnitId + 1);
            var cell = Board.Cells[rng.NextInt(Board.Cells.Count)];
            // Card ids: mostly real ones from a hand, sometimes any id (pool, Market, unknown).
            var cards = s.GetHand(A).Concat(s.GetHand(B)).Select(c => c.Id).ToList();
            int cardId = cards.Count > 0 && rng.NextInt(4) != 0 ? cards[rng.NextInt(cards.Count)] : rng.NextInt(0, s.NextCardId + 1);
            int slot = rng.NextInt(-2, 3);
            var dest = Board.Cells[rng.NextInt(Board.Cells.Count)];
            switch (rng.NextInt(10))
            {
                case 0: return new MoveCommand(player, unitId, cell);
                case 1: return new AttackCommand(player, unitId, cell);
                case 2: return new OverwatchCommand(player, unitId);
                case 3: return new DrawCommand(player, slot);
                case 4: return new PrePickCommand(player, slot);
                case 5: return new DeployCommand(player, cardId, cell);
                case 6: return new PlayCardCommand(player, cardId, cell);
                case 7: return new PlayCardCommand(player, cardId, cell, rng.NextInt(-1, 7));
                case 8: return new PlayCardCommand(player, cardId, cell, dest);
                default: return new EndTurnCommand(player);
            }
        }

        [Test]
        public void Replay_StateHashCoversEveryField()
        {
            GameState Make()
            {
                var b = new TestBoard().FullPools();
                b.Unit(UnitClass.Archer, A, new Hex(0, 0));
                b.Hand(A, "C-10");
                b.Hand(A, "C-12");
                b.Market("C-13");
                b.Trap(A, "C-20", new Hex(1, 1));
                b.Effect(1, "C-10");
                return b.Build();
            }
            var baseHash = StateHash.Compute(Make());
            Assert.That(StateHash.Compute(Make()), Is.EqualTo(baseHash));
            var changes = new List<Action<GameState>>
            {
                s => s.Round = 2,
                s => s.ActivePlayer = B,
                s => s.SetEnergy(A, 1),
                s => s.SetEnergy(B, 1),
                s => s.Winner = A,
                s => s.SetTowerShotAvailable(A, false),
                s => s.SetTowerShotAvailable(B, false),
                s => s.GetTower(A).Health = 9,
                s => s.GetTower(B).Pos = new Hex(1, -4),
                s => s.GetUnit(1).Pos = new Hex(1, 0),
                s => s.GetUnit(1).Health = 2,
                s => s.GetUnit(1).ActedThisTurn = true,
                s => s.GetUnit(1).MovedThisTurn = true,
                s => s.GetUnit(1).MovedLastOwnTurn = true,
                s => s.GetUnit(1).OnOverwatch = true,
                s => s.Map.Set(new Hex(1, 1), new Tile(Biome.Snow, Marker.None)),
                s => s.Map.Set(new Hex(1, 1), new Tile(Biome.Forest, Marker.Rock)),
                s => s.AddUnit(B, UnitClass.Mage, Biome.Snow, new Hex(3, -3)),
                s => s.SetMana(A, 2),
                s => s.SetMana(B, 1),
                s => s.SetDrawPending(A, true),
                s => s.SetDrawPending(B, true),
                s => s.SetPrePick(B, DrawCommand.Blind, 0),
                s => s.SetPrePick(B, PrePickCommand.None, 5),
                s => s.HandList(A).RemoveAt(0),
                s => s.HandList(A).Reverse(),
                s => s.HandList(B).Add(s.PoolList(CardPool.Buff)[0]),
                s => s.PoolList(CardPool.Buff).Reverse(),
                s => s.PoolList(CardPool.Character).RemoveAt(3),
                s => s.PoolList(CardPool.DebuffTrap).Reverse(),
                s => s.SetMarket(CardPool.Buff, null),
                s => s.SetMarket(CardPool.Character, s.PoolList(CardPool.Character)[0]),
                s => s.TrapList.Clear(),
                s => s.TrapList.Add(new Trap(B, new Hex(1, 1), s.PoolList(CardPool.DebuffTrap)[0])),
                s => s.GetUnit(1).Buff = null,
                s => s.GetUnit(1).Buff.TurnsLeft = 1,
                s => s.GetUnit(1).Buff = new ActiveEffect(Catalog.SupportCards.First(c => c.Id == "C-11")),
                s => s.GetUnit(1).Debuff = new ActiveEffect(Catalog.SupportCards.First(c => c.Id == "C-19")),
                s => s.GetUnit(1).MoveBonus = 2,
                s => s.Rng.NextULong(),
                s => s.NewCard(Catalog.SupportCards[0]),
            };
            foreach (var change in changes)
            {
                var s = Make();
                change(s);
                Assert.That(StateHash.Compute(s), Is.Not.EqualTo(baseHash));
            }
        }
    }
}
