using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §12 win conditions W-01 (Portal, v2.10: open at the turn end of the wait) and W-03 (round limit, v2.8 W2). W-02 and W-04: DefenseTests/TimerTests.
    public class WinTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        static TestBoard TwoQuestsDone(TestBoard b, PlayerId p) =>
            b.Quests(p, "Q-12", "Q-13", "Q-17").QuestStatus(p, "Q-12", QuestStatus.Completed).QuestStatus(p, "Q-13", QuestStatus.Completed);

        // ---------- W-01 ----------

        [Test]
        public void W01_UnitOnThePortalAtTurnEndAndStillThereAtTheNextTurnStartWins()
        {
            var b = TwoQuestsDone(new TestBoard().Round(3), A);
            b.Unit(UnitClass.Guardian, A, Board.Portal);
            var s = b.Build();
            var ev = End(s);
            Assert.That(s.IsOver, Is.False);
            ev = End(s); // A's turn start, T-04 step 3
            Assert.That(s.IsOver, Is.True);
            Assert.That((s.Result.Winner, s.Result.Reason), Is.EqualTo(((PlayerId?)A, WinReason.Portal)));
            foreach (var p in new[] { A, B })
                Assert.That(EventFilter.For(ev, p).OfType<GameOver>().Single().Winner, Is.EqualTo(A));
            Assert.That(ev.Last(), Is.InstanceOf<GameOver>());
            Assert.That(s.IsDrawPending(A), Is.False);
            Assert.That(Engine.GetLegalCommands(s, A), Is.Empty);
        }

        [Test]
        public void W01_WorksForB()
        {
            var b = TwoQuestsDone(new TestBoard().Round(3).Active(B), B);
            b.Unit(UnitClass.Archer, B, Board.Portal);
            var s = b.Build();
            End(s);
            End(s);
            Assert.That((s.Result.Winner, s.Result.Reason), Is.EqualTo(((PlayerId?)B, WinReason.Portal)));
        }

        [Test]
        public void W01_ASecondQuestCompletedAtTheSameTurnEndCounts()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-11", "Q-19").QuestStatus(A, "Q-11", QuestStatus.Completed);
            b.Unit(UnitClass.Guardian, A, Board.Portal);
            b.Unit(UnitClass.Archer, A, H(0, -3));
            b.Unit(UnitClass.Archer, A, H(-1, -3));
            var s = b.Build();
            Assert.That(End(s).OfType<QuestCompleted>().Single().QuestId, Is.EqualTo("Q-19"));
            End(s);
            Assert.That((s.Result.Winner, s.Result.Reason), Is.EqualTo(((PlayerId?)A, WinReason.Portal)));
        }

        [Test]
        public void W01_APortalOpenedLaterByARoundEndQuestDoesNotValidateTheWait()
        {
            // v2.10: the Portal must be open at the end of the turn the unit stands on it.
            var b = new TestBoard().Round(6).Quests(A, "Q-12", "Q-16").QuestStatus(A, "Q-12", QuestStatus.Completed);
            b.Unit(UnitClass.Guardian, A, Board.Portal);
            var s = b.Build();
            End(s); // A's turn end: 1 quest, closed
            var ev = End(s); // end of round 6: Q-16 completes -> open, then A's turn start
            Assert.That(ev.OfType<QuestCompleted>().Single().QuestId, Is.EqualTo("Q-16"));
            Assert.That(s.CompletedQuestCount(A), Is.EqualTo(2));
            Assert.That(s.IsOver, Is.False);
            End(s); // A's turn end: open now, the same unit waits
            End(s);
            Assert.That((s.Result.Winner, s.Result.Reason), Is.EqualTo(((PlayerId?)A, WinReason.Portal)));
        }

        [Test]
        public void W01_OneQuestIsNotEnough()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12", "Q-13").QuestStatus(A, "Q-12", QuestStatus.Completed);
            b.Unit(UnitClass.Guardian, A, Board.Portal);
            var s = b.Build();
            End(s);
            End(s);
            Assert.That(s.IsOver, Is.False);
        }

        [Test]
        public void W01_APoisonDeathAtTurnStartPreventsTheWin()
        {
            var b = TwoQuestsDone(new TestBoard().Round(3), A);
            int u = b.Unit(UnitClass.Guardian, A, Board.Portal, health: 1);
            b.Effect(u, "C-17");
            var s = b.Build();
            End(s);
            var ev = End(s);
            Assert.That(ev.OfType<UnitDied>().Single().UnitId, Is.EqualTo(u));
            Assert.That(s.IsOver, Is.False);
        }

        [Test]
        public void W01_TheUnitMustStillBeThere()
        {
            var b = TwoQuestsDone(new TestBoard().Round(3), A);
            int u = b.Unit(UnitClass.Guardian, A, Board.Portal);
            b.Unit(UnitClass.Archer, B, H(0, -1));
            int push = b.Hand(B, "C-18");
            var s = b.Build();
            End(s);
            Engine.Apply(s, new PlayCardCommand(B, push, Board.Portal, 0)); // direction 0 = (1,0)
            Assert.That(s.GetUnit(u).Pos, Is.Not.EqualTo(Board.Portal));
            End(s);
            Assert.That(s.IsOver, Is.False);
        }

        [Test]
        public void W01_ReachingThePortalDuringTheTurnIsNotEnough_ItMustBeThereAtTheTurnEnd()
        {
            var b = TwoQuestsDone(new TestBoard().Round(3).Active(B), A);
            b.Unit(UnitClass.Guardian, A, Board.Portal); // placed during B's turn: A's last turn end did not see it there
            var s = b.Build();
            End(s); // A's turn start
            Assert.That(s.IsOver, Is.False);
        }

        // ---------- W-03 ----------

        static GameState RoundFifteen(int towerA, int towerB)
        {
            var b = new TestBoard().Round(Catalog.RoundLimit).Tower(A, TestBoard.DefaultTowerA, towerA).Tower(B, TestBoard.DefaultTowerB, towerB);
            return b.Build();
        }

        [Test]
        public void W03_Criterion1_HigherTowerHealthAtTheEndOfRoundFifteen()
        {
            var s = RoundFifteen(8, 9);
            End(s); // A's turn end in round 15: not yet
            Assert.That(s.IsOver, Is.False);
            var ev = End(s);
            Assert.That((s.Result.Winner, s.Result.Reason, s.Result.Criterion), Is.EqualTo(((PlayerId?)B, WinReason.RoundLimit, 1)));
            Assert.That(ev.Last(), Is.InstanceOf<GameOver>());
            Assert.That(s.Round, Is.EqualTo(Catalog.RoundLimit));
            foreach (var p in new[] { A, B }) Assert.That(EventFilter.For(ev, p).OfType<GameOver>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void W03_Criterion2_MoreCompletedQuests()
        {
            var b = new TestBoard().Round(Catalog.RoundLimit).Active(B).Quests(A, "Q-12").QuestStatus(A, "Q-12", QuestStatus.Completed)
                .Quests(B, "Q-12", "Q-13");
            b.Unit(UnitClass.Guardian, B, H(0, -2)); // more unit Health does not matter here
            var s = b.Build();
            End(s);
            Assert.That((s.Result.Winner, s.Result.Reason, s.Result.Criterion), Is.EqualTo(((PlayerId?)A, WinReason.RoundLimit, 2)));
        }

        [Test]
        public void W03_Criterion3_HigherTotalUnitHealthOnTheBoard()
        {
            var b = new TestBoard().Round(Catalog.RoundLimit).Active(B);
            b.Unit(UnitClass.Guardian, A, H(0, 2), health: 3);
            b.Unit(UnitClass.Archer, A, H(1, 2), health: 3);
            b.Unit(UnitClass.Guardian, B, H(0, -2), health: 5);
            var s = b.Build();
            End(s);
            Assert.That((s.Result.Winner, s.Result.Reason, s.Result.Criterion), Is.EqualTo(((PlayerId?)A, WinReason.RoundLimit, 3)));
        }

        [Test]
        public void W03_Criterion4_AllEqualIsADraw()
        {
            var b = new TestBoard().Round(Catalog.RoundLimit).Active(B);
            b.Unit(UnitClass.Guardian, A, H(0, 2), health: 4);
            b.Unit(UnitClass.Guardian, B, H(0, -2), health: 4);
            var s = b.Build();
            var ev = End(s);
            Assert.That(s.Result.IsDraw, Is.True);
            Assert.That((s.Result.Winner, s.Result.Reason, s.Result.Criterion), Is.EqualTo(((PlayerId?)null, WinReason.RoundLimit, 4)));
            Assert.That(ev.OfType<GameOver>().Single().Winner, Is.Null);
        }
    }
}
