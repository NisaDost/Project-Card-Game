using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD T-07 deploy, U-27 "deployed" trigger, C-32 deploy onto a trap, U-29 (v2.6) trap-then-shots order.
    // B's tower at (2,-2); A's Archer at (0,-1) gives A a zone that reaches (1,-1), which is distance 1 from the tower.
    public class DeployTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static readonly Hex TowerB = new Hex(2, -2);
        static readonly Hex InRange = new Hex(1, -1);
        static Hex H(int q, int r) => new Hex(q, r);
        static int HealthOf(UnitClass c) => Catalog.Units.Single(u => u.Class == c).Health;

        static TestBoard Scene()
        {
            var b = new TestBoard().Tower(B, TowerB).Mana(A, 6);
            b.Unit(UnitClass.Archer, A, H(0, -1)); // not a Guardian: no cover for deployed units
            return b;
        }

        [Test]
        public void T07_DeployPaysManaNoEnergy_UnitCannotActThisTurn()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(-1, 2));
            int card = b.Hand(A, "U-02-Snow");
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, H(0, 2)));

            var u = s.UnitAt(H(0, 2));
            Assert.That((u.Owner, u.Class, u.Biome, u.Health), Is.EqualTo((A, UnitClass.Rider, Biome.Snow, HealthOf(UnitClass.Rider))));
            Assert.That(s.GetMana(A), Is.EqualTo(3 - Catalog.Units.Single(d => d.Class == UnitClass.Rider).Cost));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetHand(A), Is.Empty);
            var dep = ev.OfType<UnitDeployed>().Single();
            Assert.That((dep.UnitId, dep.CardId, dep.Cell), Is.EqualTo((u.Id, card, H(0, 2))));
            Assert.That(u.ActedThisTurn, Is.True);
            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal.OfType<MoveCommand>().Any(c => c.UnitId == u.Id), Is.False);
            Assert.That(legal.OfType<OverwatchCommand>().Any(c => c.UnitId == u.Id), Is.False);

            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveCommand>().Any(c => c.UnitId == u.Id), Is.True);
        }

        [Test]
        public void T07_OnlyEmptyNonPortalZoneCells()
        {
            var b = new TestBoard().Mana(A, 6).Rock(H(-1, 1));
            b.Unit(UnitClass.Guardian, A, H(0, 1));   // next to the Portal (0,0)
            b.Unit(UnitClass.Archer, B, H(1, 0));
            int card = b.Hand(A, "U-03-Forest");
            var s = b.Build();
            var cells = Engine.GetLegalCommands(s, A).OfType<DeployCommand>().Select(c => c.Cell).ToList();
            var zone = ControlZone.Cells(s, A);
            var expected = Board.Cells.Where(h => zone.Contains(h) && h != Board.Portal && s.UnitAt(h) == null
                                                  && s.TowerAt(h) == null && h != H(-1, 1)).ToList();
            Assert.That(cells, Is.EquivalentTo(expected));
            foreach (var bad in new[] { Board.Portal, H(0, 1), H(1, 0), H(-1, 1), TestBoard.DefaultTowerA, H(2, -2) })
                ControlZoneTests.AssertRejected(s, new DeployCommand(A, card, bad));
        }

        [Test]
        public void T07_NeedsEnoughManaAndACharacterCardInHand()
        {
            var b = new TestBoard().Mana(A, 2);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int guardian = b.Hand(A, "U-01-Forest"); // cost 3
            int archer = b.Hand(A, "U-03-Forest");   // cost 2
            int rage = b.Hand(A, "C-10");
            var s = b.Build();
            var legal = Engine.GetLegalCommands(s, A).OfType<DeployCommand>().ToList();
            Assert.That(legal.Select(c => c.CardId).Distinct(), Is.EqualTo(new[] { archer }));
            ControlZoneTests.AssertRejected(s, new DeployCommand(A, guardian, H(1, 1)));
            ControlZoneTests.AssertRejected(s, new DeployCommand(A, rage, H(1, 1)));
            ControlZoneTests.AssertRejected(s, new DeployCommand(A, 999, H(1, 1)));
            ControlZoneTests.AssertRejected(s, new DeployCommand(B, archer, H(1, 1)));
        }

        [Test]
        public void U27_DeployInTowerRangeTriggersTowerShot()
        {
            var b = Scene();
            int card = b.Hand(A, "U-01-Desert");
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, InRange));
            var u = s.UnitAt(InRange);
            Assert.That(ev.OfType<TowerShot>().Single().TargetUnitId, Is.EqualTo(u.Id));
            Assert.That(u.Health, Is.EqualTo(HealthOf(UnitClass.Guardian) - Catalog.Tower.Attack));
            Assert.That(s.IsTowerShotAvailable(B), Is.False);
        }

        [Test]
        public void U29_DeployOntoSpikeTrap_TrapFirstThenTowerShot()
        {
            var b = Scene();
            int card = b.Hand(A, "U-01-Desert");
            b.Trap(B, "C-20", InRange);
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, InRange));
            var u = s.UnitAt(InRange);
            Assert.That(u.Health, Is.EqualTo(HealthOf(UnitClass.Guardian) - 3 - Catalog.Tower.Attack));
            var trig = ev.OfType<TrapTriggered>().Single();
            var shot = ev.OfType<TowerShot>().Single();
            Assert.That(ev.IndexOf(ev.OfType<UnitDeployed>().Single()), Is.LessThan(ev.IndexOf(trig)));
            Assert.That(ev.IndexOf(trig), Is.LessThan(ev.IndexOf(shot)));
            Assert.That(ev.OfType<DamageDealt>().Select(d => d.Kind), Is.EqualTo(new[] { DamageKind.Trap, DamageKind.TowerShot }));
            Assert.That(s.Traps, Is.Empty);
        }

        [Test]
        public void U29_DeployOntoSpikeTrapAndDie_NoTowerShot()
        {
            var b = Scene();
            int card = b.Hand(A, "U-03-Desert"); // 3 Health
            b.Trap(B, "C-20", InRange);
            int drawn = b.PoolCard("C-13");
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, InRange));
            Assert.That(s.UnitAt(InRange), Is.Null);
            Assert.That(ev.OfType<UnitDied>().Single().Killer, Is.EqualTo(B));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(s.IsTowerShotAvailable(B), Is.True);
            Assert.That(s.GetHand(A).Select(c => c.Id), Is.EqualTo(new[] { drawn })); // U-23
        }

        [Test]
        public void U29_DeployOntoMirrorTrap_MovedOutOfRange_NoTowerShot()
        {
            var b = Scene();
            int card = b.Hand(A, "U-01-Desert");
            b.Trap(B, "C-21", InRange);
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, InRange));
            var u = s.GetUnit(ev.OfType<UnitDeployed>().Single().UnitId);
            Assert.That(Board.IsHomeZone(u.Pos, A), Is.True);
            Assert.That(ev.OfType<UnitTeleported>().Single().To, Is.EqualTo(u.Pos));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(s.IsTowerShotAvailable(B), Is.True);
            Assert.That(u.Health, Is.EqualTo(HealthOf(UnitClass.Guardian)));
        }

        [Test]
        public void C32_DeployOntoOwnTrapDoesNotTrigger()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int card = b.Hand(A, "U-01-Forest");
            b.Trap(A, "C-20", H(1, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, H(1, 1)));
            Assert.That(ev.OfType<TrapTriggered>(), Is.Empty);
            Assert.That(s.Traps.Count, Is.EqualTo(1));
            Assert.That(s.UnitAt(H(1, 1)).Health, Is.EqualTo(HealthOf(UnitClass.Guardian)));
        }

        [Test]
        public void T07_DeployedUnitWidensTheControlZone()
        {
            var b = new TestBoard().Mana(A, 6);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int c1 = b.Hand(A, "U-03-Forest");
            int c2 = b.Hand(A, "U-03-Snow");
            var s = b.Build();
            Assert.That(ControlZone.Cells(s, A).Contains(H(2, 0)), Is.False);
            Engine.Apply(s, new DeployCommand(A, c1, H(1, 1)));
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new DeployCommand(A, c2, H(2, 0))));
        }
    }
}
