using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD U-31 Dinlenme (v4.0): at the owner's turn start (T-04 step 2, with U-05), each own unit that took no damage
    // during the opponent's last turn gains RestHealAmount, capped by U-24. Towers never rest. Before the opponent's first
    // turn nothing could damage it, so A rests in round 1 too (PM). A Shield-blocked hit is not damage (C-12, U-20).
    public class RestTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void U31_RestCatalogValueIsTen() => Assert.That(Catalog.RestHealAmount, Is.EqualTo(10));

        [Test]
        public void U31_UndamagedUnitRestsAtOwnTurnStart_TowerDoesNot()
        {
            var b = new TestBoard().Active(B).Tower(A, TestBoard.DefaultTowerA, 100);
            int g = b.Unit(UnitClass.Guardian, A, H(-2, 4), health: 50);
            int enemy = b.Unit(UnitClass.Archer, B, H(2, -4), health: 30);
            var s = b.Build();
            var ev = Engine.Apply(s, new EndTurnCommand(B)); // A's turn starts
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(50 + Catalog.RestHealAmount));
            Assert.That(s.GetUnit(enemy).Health, Is.EqualTo(30)); // not B's turn start
            Assert.That(s.GetTower(A).Health, Is.EqualTo(100));
            var heal = ev.OfType<UnitHealed>().Single();
            Assert.That((heal.UnitId, heal.Amount), Is.EqualTo((g, Catalog.RestHealAmount)));
        }

        [Test]
        public void U31_UnitDamagedDuringOpponentTurn_DoesNotRest_ThenRestsAgain()
        {
            var b = new TestBoard();
            int archer = b.Unit(UnitClass.Archer, A, H(0, 2));
            int victim = b.Unit(UnitClass.Guardian, B, H(0, 0), health: 100);
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, archer, H(0, 0)));
            int hit = s.GetUnit(victim).Health;
            Assert.That(hit, Is.LessThan(100));
            var ev = Engine.Apply(s, new EndTurnCommand(A)); // B's turn starts
            Assert.That(s.GetUnit(victim).Health, Is.EqualTo(hit));
            Assert.That(ev.OfType<UnitHealed>().Where(h => h.UnitId == victim), Is.Empty);
            Engine.Apply(s, new EndTurnCommand(B));
            Engine.Apply(s, new EndTurnCommand(A)); // B's next turn start: A did nothing this time
            Assert.That(s.GetUnit(victim).Health, Is.EqualTo(hit + Catalog.RestHealAmount));
        }

        [Test]
        public void U31_C12_ShieldBlockedHitIsNotDamage_UnitStillRests()
        {
            var b = new TestBoard();
            int archer = b.Unit(UnitClass.Archer, A, H(0, 2));
            int victim = b.Unit(UnitClass.Guardian, B, H(0, 0), health: 100);
            b.Effect(victim, "C-12");
            var s = b.Build();
            var hit = Engine.Apply(s, new AttackCommand(A, archer, H(0, 0)));
            Assert.That(hit.OfType<ShieldBlocked>(), Is.Not.Empty);
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(victim).Health, Is.EqualTo(100 + Catalog.RestHealAmount));
        }

        [Test]
        public void U31_U24_RestIsCappedAtStartingHealth()
        {
            var b = new TestBoard().Active(B);
            int g = b.Unit(UnitClass.Guardian, A, H(-2, 4));
            int full = Unit.DefOf(UnitClass.Guardian).Health;
            int near = b.Unit(UnitClass.Archer, A, H(-1, 4), health: Unit.DefOf(UnitClass.Archer).Health - 4);
            var s = b.Build();
            var ev = Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(full));
            Assert.That(s.GetUnit(near).Health, Is.EqualTo(Unit.DefOf(UnitClass.Archer).Health));
            Assert.That(ev.OfType<UnitHealed>().Select(h => (h.UnitId, h.Amount)), Is.EqualTo(new[] { (near, 4) }));
        }

        [Test]
        public void U31_U05_RestAndHealerStack_InTheHealStep()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Healer, A, H(-2, 4), health: 50);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 4), health: 50);
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(50 + Catalog.HealerHealAmount + Catalog.RestHealAmount));
        }

        [Test]
        public void U31_C17_PoisonOnTheOwnTurnStartDoesNotStopTheNextRest()
        {
            var b = new TestBoard().Active(B);
            int g = b.Unit(UnitClass.Guardian, A, H(-2, 4), health: 100);
            b.Effect(g, "C-17", 2);
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(B)); // A: rest, then poison
            int poison = Catalog.SupportCards.First(c => c.Id == "C-17").Amount;
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(100 + Catalog.RestHealAmount - poison));
            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new EndTurnCommand(B)); // the poison hit A's unit on A's own turn: it still rests
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(100 + 2 * (Catalog.RestHealAmount - poison)));
        }

        [Test]
        public void U31_RoundOne_BothSeatsRest()
        {
            var b = new TestBoard(); // round 1, A active
            int ga = b.Unit(UnitClass.Guardian, A, H(-2, 4), health: 50);
            int gb = b.Unit(UnitClass.Guardian, B, H(2, -4), health: 50);
            var s = b.Build();
            Turn.StartTurn(s, new List<GameEvent>()); // A's first turn start: B has had no turn yet
            Assert.That(s.GetUnit(ga).Health, Is.EqualTo(50 + Catalog.RestHealAmount));
            if (s.IsDrawPending(A)) Engine.Apply(s, Engine.GetLegalCommands(s, A)[0]);
            Engine.Apply(s, new EndTurnCommand(A)); // B, round 1: A's turn happened, nothing hit B
            Assert.That(s.GetUnit(gb).Health, Is.EqualTo(50 + Catalog.RestHealAmount));
        }

        [Test]
        public void U31_V05_RestEventOfAHiddenUnitIsFilteredLikeAHeal()
        {
            var b = new TestBoard();
            b.Unit(UnitClass.Guardian, A, H(-2, 5));
            int hidden = b.Unit(UnitClass.Guardian, B, H(2, -5), health: 50);
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A), Does.Not.Contain(H(2, -5)));
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(EventFilter.For(ev, B).OfType<UnitHealed>().Single().UnitId, Is.EqualTo(hidden));
            Assert.That(EventFilter.For(ev, A).OfType<UnitHealed>(), Is.Empty);
        }
    }
}
