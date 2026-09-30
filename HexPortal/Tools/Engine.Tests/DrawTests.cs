using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD T-04 step 4 (mandatory first draw), T-11 (pre-pick), D-07 (hand limit), U-23 (death draw).
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
            int rage = b.Hand(B, "C-13");
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.True);
            Assert.That(Engine.GetLegalCommands(s, B),
                Is.EqualTo(new ICommand[] { new DrawCommand(B, Buff), new DrawCommand(B, DrawCommand.Blind) }));
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
            Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[] { new EndTurnCommand(B) }));
        }

        [Test]
        public void T11_OnlyTheWaitingPlayerPrePicks_AndOnlyThat()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, B, H(1, -1));
            b.Market("C-10");
            b.PoolCard("C-16");
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[]
            {
                new PrePickCommand(B, Buff), new PrePickCommand(B, DrawCommand.Blind), new PrePickCommand(B, PrePickCommand.None),
            }));
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PrePickCommand>(), Is.Empty);
            ControlZoneTests.AssertRejected(s, new PrePickCommand(A, Buff));        // the active player
            ControlZoneTests.AssertRejected(s, new PrePickCommand(B, Char));        // empty slot
            ControlZoneTests.AssertRejected(s, new MoveCommand(B, g, H(1, 0)));     // not B's turn
            Engine.Apply(s, new PrePickCommand(B, Buff));
            Assert.That(s.GetPrePickSlot(B), Is.EqualTo(Buff));
            Assert.That(s.GetPrePickSlot(A), Is.EqualTo(PrePickCommand.None));
        }

        [Test]
        public void T11_PrePickIsAppliedAtTurnStart()
        {
            var b = new TestBoard();
            int m = b.Market("C-10");
            b.PoolCard("C-16");
            var s = b.Build();
            Engine.Apply(s, new PrePickCommand(B, Buff));
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.False);
            Assert.That(s.GetHand(B).Single().Id, Is.EqualTo(m));
            Assert.That(ev.OfType<CardDrawn>().Single().Player, Is.EqualTo(B));
            Assert.That(s.GetPrePickSlot(B), Is.EqualTo(PrePickCommand.None)); // cleared once applied

            var b2 = new TestBoard();
            b2.Market("C-10");
            int pooled = b2.PoolCard("C-16");
            var s2 = b2.Build();
            Engine.Apply(s2, new PrePickCommand(B, DrawCommand.Blind));
            Engine.Apply(s2, new EndTurnCommand(A));
            Assert.That(s2.GetHand(B).Single().Id, Is.EqualTo(pooled));
            Assert.That(s2.IsDrawPending(B), Is.False);
        }

        [Test]
        public void T11_PrePickCanBeChangedUntilTheTurnEnds()
        {
            var b = new TestBoard();
            b.Market("C-10");
            b.PoolCard("C-16");
            var s = b.Build();
            Engine.Apply(s, new PrePickCommand(B, Buff));
            Engine.Apply(s, new PrePickCommand(B, DrawCommand.Blind));
            Engine.Apply(s, new PrePickCommand(B, PrePickCommand.None));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.True); // no pre-pick left: B chooses now
            Assert.That(s.GetHand(B), Is.Empty);
        }

        [Test]
        public void T11_PrePickedMarketCardChanged_PlayerChoosesAgain()
        {
            var b = new TestBoard();
            b.Market("C-10");
            b.PoolCard("C-12");  // refills the Buff slot with a different card
            b.PoolCard("C-16");
            var s = b.Build();
            Turn.StartTurn(s, new List<GameEvent>()); // A must draw
            Assert.That(s.IsDrawPending(A), Is.True);
            Engine.Apply(s, new PrePickCommand(B, Buff)); // B picks while A has not drawn yet
            Engine.Apply(s, new DrawCommand(A, Buff));  // A takes that very card
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.IsDrawPending(B), Is.True);
            Assert.That(s.GetHand(B), Is.Empty);
            Assert.That(s.GetPrePickSlot(B), Is.EqualTo(PrePickCommand.None));
            var legal = Engine.GetLegalCommands(s, B);
            Assert.That(legal.All(c => c is DrawCommand), Is.True);
            Assert.That(legal, Is.EqualTo(new ICommand[] { new DrawCommand(B, Buff), new DrawCommand(B, DrawCommand.Blind) }));
        }

        [Test]
        public void T11_PrePickedSlotEmptied_PlayerChoosesAgain()
        {
            var b = new TestBoard();
            b.Market("C-10");
            b.PoolCard("C-16"); // the Buff pool is empty: the slot stays empty after A's take
            var s = b.Build();
            Turn.StartTurn(s, new List<GameEvent>());
            Engine.Apply(s, new PrePickCommand(B, Buff));
            Engine.Apply(s, new DrawCommand(A, Buff));
            Assert.That(s.GetMarket(CardPool.Buff), Is.Null);
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[] { new DrawCommand(B, DrawCommand.Blind) }));
        }

        [Test]
        public void T11_PrePicksAreStoredPerPlayer()
        {
            var b = new TestBoard();
            int m = b.Market("C-10");
            b.PoolCard("C-16");
            b.PoolCard("C-17");
            var s = b.Build();
            Engine.Apply(s, new PrePickCommand(B, Buff));
            Assert.That((s.GetPrePickSlot(A), s.GetPrePickSlot(B)), Is.EqualTo((PrePickCommand.None, Buff)));
            Assert.That(s.GetPrePickCardId(B), Is.EqualTo(m));
            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new PrePickCommand(A, DrawCommand.Blind));
            Assert.That((s.GetPrePickSlot(A), s.GetPrePickSlot(B)), Is.EqualTo((DrawCommand.Blind, PrePickCommand.None)));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetHand(A).Count, Is.EqualTo(1));
            Assert.That(s.IsDrawPending(A), Is.False);
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
