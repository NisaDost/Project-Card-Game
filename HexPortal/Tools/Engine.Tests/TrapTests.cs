using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §4.3 traps: C-30…C-35, C-20, C-21, and U-29 (v2.6) trap-before-shots on arrival.
    public class TrapTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void C30_TrapGoesOnEmptyZoneCell_NotPortalTowerRockOrUnit()
        {
            var b = new TestBoard().Mana(A, 3).Rock(H(-1, 1)).Tower(B, H(1, 1));
            b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Archer, B, H(1, 0));
            int t1 = b.Hand(A, "C-20");
            int t2 = b.Hand(A, "C-21");
            var s = b.Build();
            var zone = ControlZone.Cells(s, A);
            var cells = Engine.GetLegalCommands(s, A).OfType<PlayCardCommand>().Where(c => c.CardId == t1).Select(c => c.Target);
            Assert.That(cells, Is.EquivalentTo(Board.Cells.Where(h => zone.Contains(h) && h != Board.Portal
                && s.UnitAt(h) == null && s.TowerAt(h) == null && s.Map.Get(h).Marker != Marker.Rock)));
            foreach (var bad in new[] { Board.Portal, H(1, 0), H(0, 1), H(1, 1), H(-1, 1), H(2, -1), TestBoard.DefaultTowerA })
                ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, t1, bad));

            var ev = Engine.Apply(s, new PlayCardCommand(A, t1, H(0, 2)));
            var placed = ev.OfType<TrapPlaced>().Single();
            Assert.That((placed.Owner, placed.CardId, placed.Cell), Is.EqualTo((A, t1, H(0, 2))));
            Assert.That(s.Traps.Single().Pos, Is.EqualTo(H(0, 2)));
            Assert.That(s.GetMana(A), Is.EqualTo(2));
            Assert.That(s.GetHand(A).Select(c => c.Id), Is.EqualTo(new[] { t2 }));
            // Interim (flagged): a cell that already holds one of your own traps is not offered again.
            ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, t2, H(0, 2)));
        }

        [Test]
        public void C31_AtMostTwoActiveTrapsPerPlayer()
        {
            var b = new TestBoard().Mana(A, 6).Tower(A, H(-4, 4));
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(2, 0));
            int t1 = b.Hand(A, "C-20");
            int t2 = b.Hand(A, "C-20");
            int t3 = b.Hand(A, "C-21");
            b.Trap(B, "C-20", H(-1, 2)); // the opponent's traps do not count
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, t1, H(1, 1)));
            Engine.Apply(s, new PlayCardCommand(A, t2, H(0, 2)));
            Assert.That(Traps.ActiveCount(s, A), Is.EqualTo(Catalog.MaxActiveTraps));
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PlayCardCommand>().Any(c => c.CardId == t3), Is.False);
            ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, t3, H(-1, 1)));

            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new MoveCommand(B, g, H(1, 1))); // uses up t1
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlayCardCommand(A, t3, H(-1, 1))));
        }

        [Test]
        public void C32_TriggersWhenEnemyStops_NotWhenPassingThrough()
        {
            // A's tower moved away so no tower shot muddies the damage.
            var b = new TestBoard().Tower(A, H(-4, 4)).Active(B);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int archer = b.Unit(UnitClass.Archer, B, H(2, 0));
            int guardian = b.Unit(UnitClass.Guardian, B, H(2, 1));
            int trap = b.Trap(A, "C-20", H(1, 1));
            var s = b.Build();

            // (2,0) -> (0,2) is 2 steps and the only way is through (1,1).
            var ev = Engine.Apply(s, new MoveCommand(B, archer, H(0, 2)));
            Assert.That(ev.OfType<TrapTriggered>(), Is.Empty);
            Assert.That(s.GetUnit(archer).Health, Is.EqualTo(3));
            Assert.That(s.Traps.Count, Is.EqualTo(1));

            ev = Engine.Apply(s, new MoveCommand(B, guardian, H(1, 1)));
            var t = ev.OfType<TrapTriggered>().Single();
            Assert.That((t.Owner, t.CardId, t.DefId, t.Cell, t.UnitId), Is.EqualTo((A, trap, "C-20", H(1, 1), guardian)));
            var dmg = ev.OfType<DamageDealt>().Single();
            Assert.That((dmg.Kind, dmg.SourcePlayer, dmg.Amount), Is.EqualTo((DamageKind.Trap, A, 3)));
            Assert.That(s.GetUnit(guardian).Health, Is.EqualTo(3));
            Assert.That(s.Traps, Is.Empty); // single use
        }

        [Test]
        public void C32_OwnUnitNeverTriggersOwnTrap()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Trap(A, "C-20", H(1, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, 1)));
            Assert.That(ev.OfType<TrapTriggered>(), Is.Empty);
            Assert.That(s.Traps.Count, Is.EqualTo(1));
        }

        [Test]
        public void C32_PushStopCellTriggers_PushedOverCellDoesNot()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Trap(A, "C-20", H(2, 0));
            b.Trap(A, "C-20", H(3, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(ev.OfType<TrapTriggered>().Single().Cell, Is.EqualTo(H(3, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(3));
            Assert.That(s.Traps.Single().Pos, Is.EqualTo(H(2, 0)));
        }

        [Test]
        public void C32_TeleportOntoEnemyTrapTriggers()
        {
            var b = new TestBoard().Mana(A, 3);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            int a = b.Unit(UnitClass.Guardian, A, H(-3, 0));
            b.Trap(B, "C-20", H(1, 1));
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlayCardCommand(A, tp, H(-3, 0), H(1, 1))));
            var ev = Engine.Apply(s, new PlayCardCommand(A, tp, H(-3, 0), H(1, 1)));
            Assert.That(ev.OfType<TrapTriggered>().Single().Owner, Is.EqualTo(B));
            Assert.That(s.GetUnit(a).Health, Is.EqualTo(3));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
        }

        [Test]
        public void C20_SpikeDamageIsBlockedByShield()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Effect(g, "C-12");
            b.Trap(B, "C-20", H(1, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, 1)));
            Assert.That(ev.OfType<TrapTriggered>().Count(), Is.EqualTo(1));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Kind, Is.EqualTo(DamageKind.Trap));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            Assert.That(s.GetUnit(g).Buff, Is.Null);
            Assert.That(s.Traps, Is.Empty);
        }

        [Test]
        public void C20_SpikeKill_VictimOwnerDraws_KillerIsTrapOwner()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 1)); // 3 Health
            b.Trap(B, "C-20", H(1, 1));
            int pooled = b.PoolCard("C-10");
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, a, H(1, 1)));
            Assert.That(s.GetUnit(a), Is.Null);
            var died = ev.OfType<UnitDied>().Single();
            Assert.That((died.Owner, died.Killer), Is.EqualTo((A, B)));
            Assert.That(s.GetHand(A).Single().Id, Is.EqualTo(pooled)); // U-23
            Assert.That(s.GetHand(B), Is.Empty);
        }

        [Test]
        public void C21_MirrorSendsUnitToRandomEmptyCellOfItsOwnHomeZone()
        {
            var seen = new HashSet<Hex>();
            for (ulong seed = 0; seed < 20; seed++)
            {
                Hex Run()
                {
                    var b = new TestBoard(seed: seed).Active(B);
                    b.Unit(UnitClass.Archer, B, H(-1, -3)); // B's home zone, occupied
                    int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
                    b.Trap(A, "C-21", H(1, 1));
                    var s = b.Build();
                    var ev = Engine.Apply(s, new MoveCommand(B, g, H(1, 1)));
                    var u = s.GetUnit(g);
                    Assert.That(Board.IsHomeZone(u.Pos, B), Is.True);
                    Assert.That(u.Pos, Is.Not.EqualTo(H(-1, -3)).And.Not.EqualTo(s.GetTower(B).Pos));
                    Assert.That(u.Health, Is.EqualTo(6));
                    var t = ev.OfType<UnitTeleported>().Single();
                    Assert.That((t.From, t.To), Is.EqualTo((H(1, 1), u.Pos)));
                    Assert.That(s.Traps, Is.Empty);
                    return u.Pos;
                }
                var first = Run();
                Assert.That(Run(), Is.EqualTo(first)); // deterministic per seed
                seen.Add(first);
            }
            Assert.That(seen.Count, Is.GreaterThan(1));
        }

        static TestBoard FillHomeA(TestBoard b, Hex keepEmpty)
        {
            foreach (var h in Board.Cells)
                if (Board.IsHomeZone(h, A) && h != TestBoard.DefaultTowerA && h != keepEmpty) b.Rock(h);
            return b;
        }

        [Test]
        public void C21_MirrorCanChainIntoAnotherTrap()
        {
            var keep = H(0, 3);
            var b = FillHomeA(new TestBoard(), keep);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Trap(B, "C-21", H(1, 1));
            b.Trap(B, "C-20", keep);
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, 1)));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(keep));
            Assert.That(ev.OfType<TrapTriggered>().Select(t => t.DefId), Is.EqualTo(new[] { "C-21", "C-20" }));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(3));
            Assert.That(s.Traps, Is.Empty);
        }

        // U-29 (v2.7): once the Mirror Trap moves the unit to another cell, its arrival triggers no shots at all.
        static void AssertMirrorMoveDrawsNoOverwatch(Hex keep, Hex mageCell)
        {
            var b = FillHomeA(new TestBoard(), keep);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            int ow = b.Unit(UnitClass.Mage, B, mageCell);
            b.U(ow).OnOverwatch = true;
            b.Trap(B, "C-21", H(1, 1));
            var s = b.Build();
            Assert.That(Hex.Distance(mageCell, keep), Is.InRange(1, 2), "the new cell is in the Mage's range");
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, 1)));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(keep));
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s.GetUnit(ow).OnOverwatch, Is.True);
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
        }

        [Test]
        public void U29_MirrorMovesUnitIntoOverwatchRange_NoShot()
        {
            Assert.That(Hex.Distance(H(-2, 2), H(1, 1)), Is.GreaterThan(2)); // the trap cell is out of range
            AssertMirrorMoveDrawsNoOverwatch(H(-2, 3), H(-2, 2));
        }

        [Test]
        public void U29_MirrorMovesUnitBetweenTwoInRangeCells_NoShot()
        {
            Assert.That(Hex.Distance(H(0, 2), H(1, 1)), Is.InRange(1, 2)); // the trap cell is in range too
            AssertMirrorMoveDrawsNoOverwatch(H(-1, 3), H(0, 2));
        }

        [Test]
        public void C21_MirrorWithNoEmptyHomeCellDoesNothing()
        {
            var b = FillHomeA(new TestBoard(), H(99, 99));
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Trap(B, "C-21", H(1, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, 1)));
            Assert.That(ev.OfType<TrapTriggered>().Count(), Is.EqualTo(1));
            Assert.That(ev.OfType<UnitTeleported>(), Is.Empty);
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(1, 1)));
            Assert.That(s.Traps, Is.Empty);
        }

        [Test]
        public void C33_HiddenEnemyTrapsDoNotChangeLegalCommands()
        {
            GameState Make(bool withTrap)
            {
                var b = new TestBoard().Mana(A, 6);
                b.Unit(UnitClass.Guardian, A, H(0, 1));
                b.Hand(A, "C-20");
                b.Hand(A, "U-03-Snow");
                if (withTrap) b.Trap(B, "C-20", H(1, 1));
                return b.Build();
            }
            var with = Engine.GetLegalCommands(Make(true), A);
            Assert.That(with, Is.EqualTo(Engine.GetLegalCommands(Make(false), A)));
            Assert.That(with.OfType<MoveCommand>().Any(c => c.Dest == H(1, 1)), Is.True);
            Assert.That(with.OfType<DeployCommand>().Any(c => c.Cell == H(1, 1)), Is.True);
            Assert.That(with.OfType<PlayCardCommand>().Any(c => c.Target == H(1, 1)), Is.True);
        }

        [Test]
        public void C34_BothPlayersMayTrapTheSameCell_EachFiresOnlyOnEnemies()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int mover = b.Unit(UnitClass.Guardian, A, H(2, 0));
            b.Trap(B, "C-20", H(1, 1));
            int mine = b.Hand(A, "C-20");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, mine, H(1, 1)));
            Assert.That(s.Traps.Count, Is.EqualTo(2));

            var ev = Engine.Apply(s, new MoveCommand(A, mover, H(1, 1)));
            Assert.That(ev.OfType<TrapTriggered>().Single().Owner, Is.EqualTo(B));
            Assert.That(s.Traps.Single().Owner, Is.EqualTo(A));
            Assert.That(s.GetUnit(mover).Health, Is.EqualTo(3));
        }

        [Test]
        public void C35_TrapOnCellThatBecomesRockIsDestroyed()
        {
            var b = new TestBoard();
            int ta = b.Trap(A, "C-20", H(1, 1));
            int tb = b.Trap(B, "C-21", H(1, 1));
            b.Trap(A, "C-20", H(2, 0));
            var s = b.Build();
            var ev = new List<GameEvent>();
            Traps.RemoveAt(s, H(1, 1), ev);
            Assert.That(s.Traps.Single().Pos, Is.EqualTo(H(2, 0)));
            Assert.That(ev.OfType<TrapRemoved>().Select(t => t.CardId), Is.EquivalentTo(new[] { ta, tb }));
        }

        [Test]
        public void U29_MoveOntoTrapInTowerRange_TrapThenTowerShot()
        {
            var b = new TestBoard().Tower(B, H(2, -2));
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            b.Trap(B, "C-20", H(0, -1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().Select(d => d.Kind), Is.EqualTo(new[] { DamageKind.Trap, DamageKind.TowerShot }));
            Assert.That(ev.IndexOf(ev.OfType<TrapTriggered>().Single()), Is.LessThan(ev.IndexOf(ev.OfType<TowerShot>().Single())));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - 3 - Catalog.Tower.Attack));
        }
    }
}
