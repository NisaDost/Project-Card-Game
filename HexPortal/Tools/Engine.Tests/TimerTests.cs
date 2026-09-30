using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // T-09 and W-04 (v2.8: a timeout resolves the pending draw by pre-pick or blind draw; the counter counts only
    // consecutive automatic turn ends). Game results (W-02 now, W-01/W-03 in M4b).
    public class TimerTests
    {
        static Hex H(int q, int r) => new Hex(q, r);

        static void AssertRejected(GameState s, ICommand c)
        {
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, c), c.ToString());
            Assert.That(StateHash.Compute(s), Is.EqualTo(before), c.ToString());
        }

        [Test]
        public void T09_TimeoutEndsTheTurnAndCounts()
        {
            var s = new TestBoard().Build();
            var ev = Engine.Apply(s, new TurnTimeoutCommand(A));
            Assert.That(s.ActivePlayer, Is.EqualTo(B));
            Assert.That(s.GetConsecutiveTimeouts(A), Is.EqualTo(1));
            var t = ev.OfType<TurnTimedOut>().Single();
            Assert.That((t.Player, t.Count), Is.EqualTo((A, 1)));
            Assert.That(ev.IndexOf(t), Is.LessThan(ev.IndexOf(ev.OfType<TurnStarted>().Single())));
            foreach (var p in Players) Assert.That(EventFilter.For(ev, p).OfType<TurnTimedOut>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void T09_OnlyTheActivePlayerWhilePlaying()
        {
            var s = new TestBoard().Build();
            AssertRejected(s, new TurnTimeoutCommand(B));
            var setup = Match.Create(41);
            AssertRejected(setup, new TurnTimeoutCommand(A));
            Assert.That(Engine.GetLegalCommands(s, A).OfType<TurnTimeoutCommand>(), Is.Empty); // sent by the clock, never offered
        }

        [Test]
        public void T09_NormalEndTurnResetsTheCounter()
        {
            var s = new TestBoard().Build();
            Engine.Apply(s, new TurnTimeoutCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Engine.Apply(s, new TurnTimeoutCommand(A));
            Assert.That(s.GetConsecutiveTimeouts(A), Is.EqualTo(2));
            Engine.Apply(s, new TurnTimeoutCommand(B));
            Assert.That(s.GetConsecutiveTimeouts(B), Is.EqualTo(1));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetConsecutiveTimeouts(A), Is.EqualTo(0));
            Assert.That(s.GetConsecutiveTimeouts(B), Is.EqualTo(1)); // B's own counter is untouched by A
        }

        [Test]
        public void W04_ThirdConsecutiveTimeoutLoses()
        {
            var s = new TestBoard().Build();
            Engine.Apply(s, new TurnTimeoutCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Engine.Apply(s, new TurnTimeoutCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            var ev = Engine.Apply(s, new TurnTimeoutCommand(A));
            Assert.That(s.IsOver, Is.True);
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Over));
            Assert.That((s.Winner, s.Result.Reason, s.Result.IsDraw), Is.EqualTo(((PlayerId?)B, WinReason.Timeout, false)));
            Assert.That(ev.Last(), Is.InstanceOf<GameOver>());
            Assert.That(((GameOver)ev.Last()).Result, Is.SameAs(s.Result));
            Assert.That(ev.OfType<TurnStarted>(), Is.Empty); // the game ends before B's turn starts
            Assert.That(Engine.GetLegalCommands(s, A), Is.Empty);
            AssertRejected(s, new EndTurnCommand(B));
        }

        [Test]
        public void T09_TimeoutWithPendingDrawDrawsBlind()
        {
            var b = new TestBoard();
            b.PoolCard("C-10");
            b.Market("C-12");
            var s = b.Build();
            s.SetDrawPending(A, true);
            var ev = Engine.Apply(s, new TurnTimeoutCommand(A));
            var drawn = ev.OfType<CardDrawn>().Single();
            Assert.That((drawn.Player, drawn.Source), Is.EqualTo((A, DrawSource.Blind)));
            Assert.That(s.GetHand(A).Single().DefId, Is.EqualTo("C-10"));
            Assert.That(s.IsDrawPending(A), Is.False);
            Assert.That(s.ActivePlayer, Is.EqualTo(B));
        }

        [Test]
        public void T09_TimeoutWithPendingDrawAndEmptyPools_TakesTheFirstMarketCard()
        {
            // Interim (T-09): blind drawing is impossible, so the first non-empty Market slot is taken.
            var b = new TestBoard();
            int market = b.Market("C-16");
            var s = b.Build();
            s.SetDrawPending(A, true);
            var ev = Engine.Apply(s, new TurnTimeoutCommand(A));
            Assert.That(ev.OfType<CardDrawn>().Single().CardId, Is.EqualTo(market));
            Assert.That(s.GetHand(A).Single().Id, Is.EqualTo(market));
        }

        [Test]
        public void T09_TimeoutWithoutPendingDrawDrawsNothing()
        {
            var b = new TestBoard();
            b.PoolCard("C-10");
            var s = b.Build();
            var ev = Engine.Apply(s, new TurnTimeoutCommand(A));
            Assert.That(ev.OfType<CardDrawn>().Where(c => c.Player == A), Is.Empty);
            Assert.That(s.GetHand(A), Is.Empty);
        }

        [Test]
        public void W02_ResultCarriesWinnerAndReason()
        {
            var b = new TestBoard().Tower(B, H(2, -2), 2);
            int a = b.Unit(UnitClass.Archer, A, H(0, -2));
            var s = b.Build();
            Assert.That((s.Result, s.Phase), Is.EqualTo(((GameResult)null, GamePhase.Playing)));
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(2, -2)));
            Assert.That((s.Result.Winner, s.Result.Reason, s.Result.Criterion), Is.EqualTo(((PlayerId?)A, WinReason.Tower, 0)));
            Assert.That(ev.OfType<GameOver>().Single().Result, Is.SameAs(s.Result));
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Over));
        }
    }
}
