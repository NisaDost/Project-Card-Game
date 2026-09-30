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

        /// <summary>A generated map, one unit of each class per side in the home rows, mirrored, plus a rock pair.</summary>
        static GameState NewGame(ulong seed)
        {
            var b = new TestBoard(MapGenerator.Generate(seed));
            b.Rock(new Hex(-2, 1)).Rock(new Hex(2, -1));
            var homeA = new[] { Hex.FromDoubled(2, 8), Hex.FromDoubled(4, 8), Hex.FromDoubled(8, 8), Hex.FromDoubled(10, 8), Hex.FromDoubled(5, 7) };
            for (int i = 0; i < Classes.Length; i++)
            {
                var posA = homeA[i];
                b.Unit(Classes[i], A, posA, Biomes[i % 3]);
                b.Unit(Classes[i], B, posA.Mirror(), Biomes[i % 3]);
            }
            return b.Build();
        }

        static ICommand Pick(GameState s, int index)
        {
            var legal = Engine.GetLegalCommands(s, s.ActivePlayer);
            return legal[index % legal.Count];
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
            Assert.That(commands.OfType<EndTurnCommand>().Count(), Is.GreaterThan(0));
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
                    var inactive = s.ActivePlayer == A ? B : A;
                    Assert.That(Engine.GetLegalCommands(s, inactive), Is.Empty);
                    var legal = Engine.GetLegalCommands(s, s.ActivePlayer);
                    Assert.That(legal, Is.Not.Empty);
                    Assert.That(legal.Last(), Is.InstanceOf<EndTurnCommand>());
                    // Bias away from EndTurn so units actually fight.
                    var c = legal.Count > 1 && rng.NextInt(4) != 0 ? legal[rng.NextInt(legal.Count - 1)] : legal[legal.Count - 1];
                    if (c is AttackCommand) attacks++;
                    Assert.DoesNotThrow(() => Engine.Apply(s, c), "seed " + seed + " step " + steps + ": " + c);
                    AssertInvariants(s);
                    steps++;
                }
                Assert.That(attacks, Is.GreaterThan(0), "seed " + seed);
            }
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
            }
            Assert.That(s.GetEnergy(s.ActivePlayer), Is.InRange(0, Catalog.EnergyPerTurn));
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
                    // Random commands (any player, any unit id incl. dead/unknown, any cell) not in the legal set.
                    for (int k = 0; k < 20; k++)
                    {
                        var c = RandomCommand(s, rng);
                        if (Engine.GetLegalCommands(s, c.Player).Contains(c)) continue;
                        AssertRejected(s, c);
                        rejected++;
                    }
                    var legal = Engine.GetLegalCommands(s, s.ActivePlayer);
                    var pick = legal.Count > 1 && rng.NextInt(4) != 0 ? legal[rng.NextInt(legal.Count - 1)] : legal[legal.Count - 1];
                    Engine.Apply(s, pick);
                }
            }
            Assert.That(rejected, Is.GreaterThan(1000));

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
            Engine.Apply(st, new MoveCommand(A, guardian, new Hex(-2, 0)));
            AssertRejected(st, new MoveCommand(A, guardian, new Hex(-1, 0)));          // second action
            st.SetEnergy(A, 0);
            AssertRejected(st, new AttackCommand(A, archer, new Hex(1, 0)));           // 0 Energy
            AssertRejected(st, new OverwatchCommand(A, archer));                       // 0 Energy
            Assert.Throws<ArgumentNullException>(() => Engine.Apply(st, null));

            var w = new TestBoard().Tower(B, new Hex(2, -2), 1);
            int wa = w.Unit(UnitClass.Archer, A, new Hex(-1, -2));
            var ws = w.Build();
            Engine.Apply(ws, new AttackCommand(A, wa, new Hex(2, -2)));
            Assert.That(ws.Winner, Is.EqualTo(A));
            AssertRejected(ws, new EndTurnCommand(A));                                 // after game over
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
            switch (rng.NextInt(4))
            {
                case 0: return new MoveCommand(player, unitId, cell);
                case 1: return new AttackCommand(player, unitId, cell);
                case 2: return new OverwatchCommand(player, unitId);
                default: return new EndTurnCommand(player);
            }
        }

        [Test]
        public void Replay_StateHashCoversEveryField()
        {
            GameState Make()
            {
                var b = new TestBoard();
                b.Unit(UnitClass.Archer, A, new Hex(0, 0));
                return b.Build();
            }
            var baseHash = StateHash.Compute(Make());
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
