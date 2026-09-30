using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §7 Mana: T-01…T-03, T-06, and C-01 (cards cost Mana only).
    public class ManaTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void T01_ManaIsRoundCappedAtSix_RefilledEachTurn()
        {
            var s = new TestBoard().Build();
            Assert.That(s.GetMana(A), Is.EqualTo(1));
            for (int round = 1; round <= 8; round++)
            {
                Assert.That(s.Round, Is.EqualTo(round));
                Assert.That(s.GetMana(A), Is.EqualTo(System.Math.Min(round, Catalog.ManaCap)), "A round " + round);
                Engine.Apply(s, new EndTurnCommand(A));
                int expectedB = System.Math.Min(round, Catalog.ManaCap) + (round == 1 ? Catalog.ManaFirstRoundBonusB : 0);
                Assert.That(s.GetMana(B), Is.EqualTo(expectedB), "B round " + round);
                Engine.Apply(s, new EndTurnCommand(B));
            }
            Assert.That(Catalog.ManaCap, Is.EqualTo(6));
        }

        [Test]
        public void T01_UnspentManaIsNotCarried()
        {
            var s = new TestBoard().Round(3).Mana(A, 3).Build();
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetMana(A), Is.EqualTo(4)); // not 3 + 4
        }

        [Test]
        public void T02_BStartsRoundOneWithTwoMana()
        {
            var s = new TestBoard().Build();
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetMana(B), Is.EqualTo(2));
            Assert.That(Mana.TurnStartMana(s, A), Is.EqualTo(1)); // the bonus is B's only
            Engine.Apply(s, new EndTurnCommand(B));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetMana(B), Is.EqualTo(2)); // round 2: no bonus
        }

        [Test]
        public void T03_EachHeldWellspringGivesOneMana_CanExceedCap()
        {
            var b = new TestBoard().Wellspring(H(-1, 2)).Wellspring(H(1, -2)).Wellspring(H(2, 0)).Round(8);
            b.Unit(UnitClass.Guardian, A, H(-1, 2));
            b.Unit(UnitClass.Guardian, A, H(1, -2));
            b.Unit(UnitClass.Guardian, B, H(2, 0)); // the enemy's Wellspring gives A nothing
            var s = b.Build();
            Assert.That(Mana.TurnStartMana(s, A), Is.EqualTo(Catalog.ManaCap + 2 * Catalog.WellspringManaBonus));
            Assert.That(Mana.TurnStartMana(s, B), Is.EqualTo(Catalog.ManaCap + 1));

            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetMana(A), Is.EqualTo(8));
        }

        [Test]
        public void T03_EmptyWellspringGivesNothing()
        {
            var b = new TestBoard().Wellspring(H(-1, 2)).Round(2);
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            Assert.That(Mana.TurnStartMana(b.Build(), A), Is.EqualTo(2));
        }

        [Test]
        public void T06_PlayCardsWhileManaLasts_NoEnergy()
        {
            var b = new TestBoard().Mana(A, 3);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1), health: 2);
            int c1 = b.Hand(A, "C-10");
            int c2 = b.Hand(A, "C-13");
            int c3 = b.Hand(A, "C-14");
            int c4 = b.Hand(A, "C-16");
            b.Unit(UnitClass.Archer, B, H(0, 1));
            var s = b.Build();

            Engine.Apply(s, new PlayCardCommand(A, c1, H(-1, 1)));
            Engine.Apply(s, new PlayCardCommand(A, c2, H(-1, 1)));
            Engine.Apply(s, new PlayCardCommand(A, c3, H(-1, 1)));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetUnit(g).ActedThisTurn, Is.False);
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PlayCardCommand>(), Is.Empty);
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new PlayCardCommand(A, c4, H(0, 1))));
            Assert.That(StateHash.Compute(s), Is.EqualTo(before));
        }

        [Test]
        public void C01_SupportCardCostsManaOnly_UnitCanStillAct()
        {
            var b = new TestBoard().Mana(A, 2);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int card = b.Hand(A, "C-11");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, card, H(-1, 1)));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetHand(A), Is.Empty);
            var played = ev.OfType<CardPlayed>().Single();
            Assert.That((played.CardId, played.DefId), Is.EqualTo((card, "C-11")));
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveCommand>().Any(m => m.UnitId == g), Is.True);
        }

        [Test]
        public void C01_CardWithoutEffectIsLegalAndCostsMana()
        {
            var b = new TestBoard().Mana(A, 2);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1)); // full Health
            int potion = b.Hand(A, "C-13");
            int wind = b.Hand(A, "C-14");
            var s = b.Build();
            Engine.Apply(s, new OverwatchCommand(A, g)); // the unit has used its action

            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal, Does.Contain(new PlayCardCommand(A, potion, H(-1, 1))));
            Assert.That(legal, Does.Contain(new PlayCardCommand(A, wind, H(-1, 1))));
            var ev = Engine.Apply(s, new PlayCardCommand(A, potion, H(-1, 1)));
            Assert.That(ev.OfType<UnitHealed>(), Is.Empty);
            Engine.Apply(s, new PlayCardCommand(A, wind, H(-1, 1)));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveCommand>(), Is.Empty);
        }

        [Test]
        public void T05_ActionsSpendEnergyNotMana()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, g, H(0, 1)));
            Assert.That(s.GetMana(A), Is.EqualTo(1));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn - 1));
        }
    }
}
