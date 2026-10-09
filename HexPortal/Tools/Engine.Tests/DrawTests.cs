using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD T-04 step 4 (mandatory first draw), D-07 (hand limit), U-23 (death draw).
    public class DrawTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static readonly int Buff = (int)CardPool.Buff, Char = (int)CardPool.Character, Debuff = (int)CardPool.DebuffTrap;
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void T04_DrawIsMandatoryAndComesFirst()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, B, H(1, -1));
            b.Market("C-10");
            b.PoolCard("C-16");
            b.PoolCard("C-16"); // D-05 (v4.0): the turn-start refresh puts one C-16 in the Market, one stays for a blind draw
            int rage = b.Hand(B, "C-13");
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.True);
            Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[]
            {
                new DrawCommand(B, Buff), new DrawCommand(B, Debuff), new DrawCommand(B, DrawCommand.Blind), new RefreshMarketCommand(B),
            }));
            ControlZoneTests.AssertRejected(s, new MoveCommand(B, g, H(1, 0)));
            ControlZoneTests.AssertRejected(s, new EndTurnCommand(B));
            ControlZoneTests.AssertRejected(s, new PlayCardCommand(B, rage, H(1, -1)));
            ControlZoneTests.AssertRejected(s, new DrawCommand(B, Char)); // empty slot

            Engine.Apply(s, new DrawCommand(B, DrawCommand.Blind));
            Assert.That(s.IsDrawPending(B), Is.False);
            var legal = Engine.GetLegalCommands(s, B);
            Assert.That(legal.OfType<DrawCommand>(), Is.Empty);
            Assert.That(legal, Does.Contain(new MoveCommand(B, g, H(1, 0))));
            ControlZoneTests.AssertRejected(s, new DrawCommand(B, Buff)); // only one draw per turn
        }

        [Test]
        public void T04_PlayerADrawsInRoundOneToo()
        {
            var b = new TestBoard().FullPools();
            var s = b.Build();
            Pools.OpenMarket(s, new List<GameEvent>());
            var ev = new List<GameEvent>();
            Turn.StartTurn(s, ev); // how the setup phase (M4) starts A's first turn
            Assert.That((s.Round, s.ActivePlayer), Is.EqualTo((1, A)));
            Assert.That(s.GetMana(A), Is.EqualTo(1));
            Assert.That(s.IsDrawPending(A), Is.True);
            Assert.That(Engine.GetLegalCommands(s, A), Is.EqualTo(new ICommand[]
            {
                new DrawCommand(A, Char), new DrawCommand(A, Buff), new DrawCommand(A, Debuff), new DrawCommand(A, DrawCommand.Blind),
                new RefreshMarketCommand(A), // D-05 (v4.0): A has 1 Mana
            }));
            Engine.Apply(s, new DrawCommand(A, Char));
            Assert.That(s.GetHand(A).Single().IsCharacter, Is.True);
        }

        [Test]
        public void T04_D07_DrawSkippedWhenHandIsFull()
        {
            var b = new TestBoard();
            b.Market("C-10");
            for (int i = 0; i < Catalog.HandLimit; i++) b.Hand(B, "C-13");
            var s = b.Build();
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.False);
            Assert.That(ev.OfType<CardDrawn>(), Is.Empty);
            Assert.That(s.GetHand(B).Count, Is.EqualTo(Catalog.HandLimit));
            Assert.That(Engine.GetLegalCommands(s, B).Last(), Is.EqualTo(new EndTurnCommand(B)));

            var b2 = new TestBoard();
            b2.Market("C-10");
            for (int i = 0; i < Catalog.HandLimit - 1; i++) b2.Hand(B, "C-13");
            var s2 = b2.Build();
            Engine.Apply(s2, new EndTurnCommand(A));
            Assert.That(s2.IsDrawPending(B), Is.True);
        }

        [Test]
        public void T04_DrawSkippedWhenNothingToDraw()
        {
            var s = new TestBoard().Build();
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.False);
            // D-05 (v4.0): the paid refresh needs only Mana (B has 2).
            Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[] { new RefreshMarketCommand(B), new EndTurnCommand(B) }));
        }

        // T-11 KALDIRILDI (v4.0): the six pre-pick tests (waiting player's command, applied at turn start, changeable,
        // void when the Market card changed or the slot emptied, stored per player) were removed with the rule.

        [Test]
        public void T11_Removed_WaitingPlayerHasNoCommand_DrawStaysMandatoryAtTurnStart()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, B, H(1, -1));
            int m = b.Market("C-10");
            b.PoolCard("C-16");
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, B), Is.Empty);               // A is active: nothing for B
            ControlZoneTests.AssertRejected(s, new MoveCommand(B, g, H(1, 0)));  // not B's turn
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.True);                          // T-04 step 4: B chooses now
            Assert.That(s.GetHand(B), Is.Empty);
            // D-05 (v4.0): the turn-start refresh moved the pooled C-16 into the Market; C-10 came back to the Buff slot.
            Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[] { new DrawCommand(B, Buff), new DrawCommand(B, Debuff), new RefreshMarketCommand(B) }));
            Engine.Apply(s, new DrawCommand(B, Buff));
            Assert.That(s.GetHand(B).Single().Id, Is.EqualTo(m));
        }

        [Test]
        public void U23_KilledUnitsOwnerDrawsOneBlindCard()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            int victim = b.Unit(UnitClass.Healer, B, H(0, -1), health: 1);
            int m = b.Market("C-10");
            int pooled = b.PoolCard("C-16");
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(s.GetUnit(victim), Is.Null);
            var drawn = ev.OfType<CardDrawn>().Single();
            Assert.That((drawn.Player, drawn.CardId, drawn.Source), Is.EqualTo((B, pooled, DrawSource.Blind)));
            Assert.That(s.GetMarket(CardPool.Buff).Id, Is.EqualTo(m));
            Assert.That(s.GetHand(A), Is.Empty);
        }

        [Test]
        public void U23_NoDeathDrawWithFullHandOrEmptyPools()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Healer, B, H(0, -1), health: 1);
            for (int i = 0; i < Catalog.HandLimit; i++) b.Hand(B, "C-13");
            b.PoolCard("C-16");
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<CardDrawn>(), Is.Empty);
            Assert.That(s.GetHand(B).Count, Is.EqualTo(Catalog.HandLimit));
            Assert.That(s.GetPool(CardPool.DebuffTrap).Count, Is.EqualTo(1));

            var b2 = new TestBoard();
            int a2 = b2.Unit(UnitClass.Archer, A, H(0, 1));
            b2.Unit(UnitClass.Healer, B, H(0, -1), health: 1);
            b2.Market("C-10"); // Market cards are never blind-drawn
            var s2 = b2.Build();
            ev = Engine.Apply(s2, new AttackCommand(A, a2, H(0, -1)));
            Assert.That(ev.OfType<CardDrawn>(), Is.Empty);
            Assert.That(s2.GetHand(B), Is.Empty);
        }
    }
}
