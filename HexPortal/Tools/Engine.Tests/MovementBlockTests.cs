using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD U-07: every class except the Rider (U-09) never passes through a unit (either side), a tower (either side) or
    // rock, in Movement.Destinations, the move-attack options (T-05) and Preview. On a hex grid the only 2-step path to
    // P + 2d runs through P + d, so blocking P + d must cut P + 2d (and anything only reachable from there).
    public class MovementBlockTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static Hex Step(Hex p, Hex d, int n) => new Hex(p.Q + d.Q * n, p.R + d.R * n);

        public enum Blocker { None, OwnUnit, EnemyUnit, OwnTower, EnemyTower, Rock }

        static readonly UnitClass[] Walkers = { UnitClass.Guardian, UnitClass.Archer, UnitClass.Mage, UnitClass.Healer };

        static void Place(TestBoard b, Blocker k, Hex h)
        {
            switch (k)
            {
                case Blocker.OwnUnit: b.Unit(UnitClass.Guardian, A, h); break;
                case Blocker.EnemyUnit: b.Unit(UnitClass.Guardian, B, h); break;
                case Blocker.OwnTower: b.Tower(A, h); break;
                case Blocker.EnemyTower: b.Tower(B, h); break;
                case Blocker.Rock: b.Rock(h); break;
            }
        }

        // Effective Move 2 for every class (Move + Wind Step-style bonus), so P + 2d is in reach only through P + d.
        static void MoveTwo(GameState s, int id)
        {
            var u = s.GetUnit(id);
            u.MoveBonus = 2 - u.Def.Move;
        }

        [Test]
        public void U07_BlockedCorridorShortensTheReach_EveryClassButRider(
            [ValueSource(nameof(Walkers))] UnitClass cls,
            [Values(Blocker.OwnUnit, Blocker.EnemyUnit, Blocker.OwnTower, Blocker.EnemyTower, Blocker.Rock)] Blocker k)
        {
            var p = H(0, 2);
            var d = Hex.Directions[0];
            foreach (var blocked in new[] { false, true })
            {
                var b = new TestBoard();
                int id = b.Unit(cls, A, p);
                if (blocked) Place(b, k, Step(p, d, 1));
                var s = b.Build();
                MoveTwo(s, id);
                var dests = Movement.Destinations(s, s.GetUnit(id));
                var legal = Engine.GetLegalCommands(s, A).OfType<MoveCommand>().Where(m => m.UnitId == id).Select(m => m.Dest);
                var preview = Preview.Ranges(PlayerView.For(s, A), id).MoveCells;
                if (!blocked)
                {
                    Assert.That(dests, Does.Contain(Step(p, d, 2)), "open corridor");
                    continue;
                }
                Assert.That(dests, Does.Not.Contain(Step(p, d, 1)).And.Not.Contain(Step(p, d, 2)), cls + " through " + k);
                Assert.That(legal, Does.Not.Contain(Step(p, d, 2)));
                Assert.That(preview, Does.Not.Contain(Step(p, d, 2)));
            }
        }

        [Test]
        public void U09_RiderPassesUnitsAndTowersInTheCorridor_NotRock(
            [Values(Blocker.OwnUnit, Blocker.EnemyUnit, Blocker.OwnTower, Blocker.EnemyTower, Blocker.Rock)] Blocker k)
        {
            var p = H(0, 2);
            var d = Hex.Directions[0];
            var b = new TestBoard();
            int id = b.Unit(UnitClass.Rider, A, p);
            Place(b, k, Step(p, d, 1));
            var s = b.Build();
            MoveTwo(s, id);
            var dests = Movement.Destinations(s, s.GetUnit(id));
            Assert.That(dests, Does.Not.Contain(Step(p, d, 1)));
            if (k == Blocker.Rock) Assert.That(dests, Does.Not.Contain(Step(p, d, 2)));
            else Assert.That(dests, Does.Contain(Step(p, d, 2)));
        }

        // The target sits where only a stop on P + 2d brings it into range: adjacent classes at P + 3d, the Mage (1–2) at
        // P + 4d, the Archer (straight line 1–3) at P + 5d. A spotter Guardian keeps the target Visible (V-07).
        static int TargetSteps(UnitClass cls) => cls == UnitClass.Archer ? 5 : cls == UnitClass.Mage ? 4 : 3;

        [Test]
        public void U07_T05_MoveAttackNeverApproachesThroughABlockedPath(
            [ValueSource(nameof(Walkers))] UnitClass cls,
            [Values(Blocker.OwnUnit, Blocker.EnemyUnit, Blocker.Rock)] Blocker k)
        {
            var p = H(-3, 2);
            var d = Hex.Directions[0];
            var target = Step(p, d, TargetSteps(cls));
            foreach (var blocked in new[] { false, true })
            {
                var b = new TestBoard().Energy(A, Catalog.MoveAttackEnergy);
                int id = b.Unit(cls, A, p);
                b.Unit(UnitClass.Guardian, B, target);
                b.Unit(UnitClass.Guardian, A, new Hex(target.Q, target.R - 2)); // spotter, off the line
                if (blocked) Place(b, k, Step(p, d, 1));
                var s = b.Build();
                MoveTwo(s, id);
                Assert.That(Visibility.VisibleCells(s, A), Does.Contain(target));
                var legal = Engine.GetLegalCommands(s, A).OfType<MoveAttackCommand>().Where(m => m.UnitId == id).ToList();
                var preview = Preview.MoveAttackTargets(PlayerView.For(s, A), id).Select(o => o.Target);
                var approach = MoveAttack.ApproachFor(s, s.GetUnit(id), target);
                if (!blocked)
                {
                    Assert.That(legal.Select(m => m.Target), Does.Contain(target), "open path");
                    Assert.That(approach, Is.EqualTo((Hex?)Step(p, d, 2)));
                    continue;
                }
                Assert.That(legal.Select(m => m.Target), Does.Not.Contain(target), cls + " through " + k);
                Assert.That(legal.Select(m => m.Approach), Does.Not.Contain(Step(p, d, 2)));
                Assert.That(preview, Does.Not.Contain(target));
                Assert.That(approach, Is.Null);
                ControlZoneTests.AssertRejected(s, new MoveAttackCommand(A, id, Step(p, d, 2), target));
            }
        }
    }
}
