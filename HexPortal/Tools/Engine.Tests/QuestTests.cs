using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §8 quests: Q-01…Q-05, Q-10…Q-19 (v2.8: "hold" = same stone/wellspring, the unit may change; Q-12 counts every
    // kill source; "round end" = end of B's turn). Scenes start in round 3 where several turns are played, so no map
    // event (announced at the start of round 5 at the earliest) changes the board.
    public class QuestTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        static QuestStatus Status(GameState s, PlayerId p, string id) => s.GetQuestStatus(p, id);

        static IEnumerable<string> Completed(IEnumerable<GameEvent> ev, PlayerId p) =>
            ev.OfType<QuestCompleted>().Where(q => q.Player == p).Select(q => q.QuestId);

        // ---------- Q-01…Q-05 ----------

        [Test]
        public void Q02_QuestsAreCheckedAtTheEndOfTheOwnersTurn()
        {
            var b = new TestBoard().Round(3).Active(B).Quests(A, "Q-19");
            b.Unit(UnitClass.Archer, A, H(0, -3)); // B's home zone
            b.Unit(UnitClass.Archer, A, H(-1, -3));
            var s = b.Build();
            var ev = End(s); // B's turn end: A's quests are not checked
            Assert.That(Completed(ev, A), Is.Empty);
            Assert.That(Status(s, A, "Q-19"), Is.EqualTo(QuestStatus.Active));
            ev = End(s); // A's turn end
            Assert.That(Completed(ev, A), Is.EqualTo(new[] { "Q-19" }));
            Assert.That(Status(s, A, "Q-19"), Is.EqualTo(QuestStatus.Completed));
        }

        [Test]
        public void Q01_Q03_QuestsAreHiddenUntilCompleted_ThenPublicAndPermanent()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-19", "Q-11", "Q-15");
            int u1 = b.Unit(UnitClass.Archer, A, H(0, -3));
            b.Unit(UnitClass.Archer, A, H(-1, -3));
            var s = b.Build();
            var viewB = PlayerView.For(s, B);
            Assert.That(viewB.OpponentCompletedQuests, Is.Empty);
            Assert.That(viewB.OpponentFailedQuests, Is.Empty);
            Assert.That(PlayerView.For(s, A).QuestStatuses, Is.EqualTo(new[] { QuestStatus.Active, QuestStatus.Active, QuestStatus.Active }));

            var ev = End(s);
            var seenByB = EventFilter.For(ev, B).OfType<QuestCompleted>().Single();
            Assert.That((seenByB.Player, seenByB.QuestId), Is.EqualTo((A, "Q-19")));
            Assert.That(PlayerView.For(s, B).OpponentCompletedQuests, Is.EqualTo(new[] { "Q-19" }));
            Assert.That(PlayerView.For(s, A).QuestStatuses, Is.EqualTo(new[] { QuestStatus.Completed, QuestStatus.Active, QuestStatus.Active }));

            // Q-03: it stays completed after the condition is gone.
            End(s);
            Engine.Apply(s, new MoveCommand(A, u1, H(0, -2)));
            ev = End(s);
            Assert.That(Completed(ev, A), Is.Empty);
            Assert.That(Status(s, A, "Q-19"), Is.EqualTo(QuestStatus.Completed));
            Assert.That(s.CompletedQuestCount(A), Is.EqualTo(1));
        }

        // ---------- Q-10, Q-05 ----------

        [Test]
        public void Q10_HoldingTheSameRuneStoneAtTwoOwnTurnEndsCompletes()
        {
            var b = new TestBoard().Round(3).Rune(H(1, -1)).Quests(A, "Q-10");
            b.Unit(UnitClass.Guardian, A, H(1, -1));
            var s = b.Build();
            Assert.That(Completed(End(s), A), Is.Empty); // first turn end
            End(s);
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-10" }));
        }

        [Test]
        public void Q10_TheUnitOnTheStoneMayChange()
        {
            var b = new TestBoard().Round(3).Rune(H(1, -1)).Quests(A, "Q-10");
            int x = b.Unit(UnitClass.Archer, A, H(1, -1));
            int y = b.Unit(UnitClass.Archer, A, H(1, 0));
            var s = b.Build();
            End(s);
            End(s);
            Engine.Apply(s, new MoveCommand(A, x, H(2, -1)));
            Engine.Apply(s, new MoveCommand(A, y, H(1, -1)));
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-10" }));
        }

        [Test]
        public void Q10_TwoDifferentStonesDoNotCount_AndAGapRestarts()
        {
            var b = new TestBoard().Round(3).Rune(H(1, -1)).Rune(H(-1, 1)).Quests(A, "Q-10");
            int x = b.Unit(UnitClass.Archer, A, H(1, -1));
            var s = b.Build();
            End(s);
            End(s);
            Engine.Apply(s, new MoveCommand(A, x, H(-1, 1))); // the other stone
            Assert.That(Completed(End(s), A), Is.Empty);
            End(s);
            Engine.Apply(s, new MoveCommand(A, x, H(-1, 2))); // off every stone: the streak breaks
            Assert.That(Completed(End(s), A), Is.Empty);
            End(s);
            Engine.Apply(s, new MoveCommand(A, x, H(-1, 1)));
            Assert.That(Completed(End(s), A), Is.Empty); // one turn end on it again
            End(s);
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-10" }));
        }

        // ---------- Q-11 ----------

        [Test]
        public void Q11_UnitsOnTwoDifferentRuneStonesAtTurnEnd()
        {
            var b = new TestBoard().Round(3).Rune(H(1, -1)).Rune(H(-1, 1)).Rune(H(2, -2))
                .Quests(A, "Q-11").Quests(B, "Q-11");
            b.Unit(UnitClass.Guardian, A, H(1, -1));
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            b.Unit(UnitClass.Guardian, B, H(2, -2));
            b.Unit(UnitClass.Guardian, B, H(3, -2));
            var s = b.Build();
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-11" }));
            Assert.That(Completed(End(s), B), Is.Empty); // only one stone
            Assert.That(Status(s, B, "Q-11"), Is.EqualTo(QuestStatus.Active));
        }

        // ---------- Q-12 ----------

        [Test]
        public void Q12_TwoEnemyKillsFromAttackAndTrap()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12");
            int archer = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1), health: 1);
            int runner = b.Unit(UnitClass.Archer, B, H(2, -2));
            b.Trap(A, "C-20", H(1, -2));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, archer, H(0, -1)));
            Assert.That(ev.OfType<UnitDied>().Single().Killer, Is.EqualTo(A));
            Assert.That(s.GetProgress(A).Kills, Is.EqualTo(1));
            Assert.That(Completed(End(s), A), Is.Empty);
            ev = Engine.Apply(s, new MoveCommand(B, runner, H(1, -2))); // spike trap kills it
            Assert.That(ev.OfType<UnitDied>().Single().Killer, Is.EqualTo(A));
            Assert.That(Completed(End(s), A), Is.Empty); // B's turn end
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-12" }));
        }

        [Test]
        public void Q12_PoisonTowerAndOverwatchKillsCount_OwnLossesDoNot()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12");
            int poisoned = b.Unit(UnitClass.Healer, B, H(3, -3), health: 1);
            b.Effect(poisoned, "C-17");
            int toTower = b.Unit(UnitClass.Archer, B, H(0, 1), health: 2);
            int watcher = b.Unit(UnitClass.Archer, A, H(2, 0));
            b.U(watcher).OnOverwatch = true;
            int toWatch = b.Unit(UnitClass.Guardian, B, H(3, -1), health: 1);
            var s = b.Build();

            var ev = End(s); // B's turn start: poison
            Assert.That(ev.OfType<UnitDied>().Single(d => d.UnitId == poisoned).Killer, Is.EqualTo(A));
            ev = Engine.Apply(s, new MoveCommand(B, toTower, H(-1, 2))); // A's tower shoots
            Assert.That(ev.OfType<UnitDied>().Single().Killer, Is.EqualTo(A));
            ev = Engine.Apply(s, new MoveCommand(B, toWatch, H(2, -1))); // A's overwatch
            Assert.That(ev.OfType<UnitDied>().Single().Killer, Is.EqualTo(A));
            Assert.That(s.GetProgress(A).Kills, Is.EqualTo(3));
            Assert.That(s.GetProgress(B).Kills, Is.EqualTo(0));
            End(s);
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-12" }));
        }

        // ---------- Q-13 ----------

        [Test]
        public void Q13_FiveDamageToTheEnemyTowerInTotal_SplashCounts()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-13");
            int archer = b.Unit(UnitClass.Archer, A, H(2, -2));
            int mage = b.Unit(UnitClass.Mage, A, H(1, -2));
            b.Unit(UnitClass.Archer, B, H(2, -3)); // next to B's tower (2,-4)
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, archer, TestBoard.DefaultTowerB)); // 2
            Engine.Apply(s, new AttackCommand(A, mage, H(2, -3)));                 // splash 1 on the tower
            Assert.That(s.GetProgress(A).TowerDamage, Is.EqualTo(3));
            Assert.That(Completed(End(s), A), Is.Empty);
            End(s);
            Engine.Apply(s, new AttackCommand(A, archer, TestBoard.DefaultTowerB));
            Assert.That(s.GetProgress(A).TowerDamage, Is.EqualTo(5));
            Assert.That(s.GetTower(B).Health, Is.EqualTo(Catalog.Tower.Health - 5));
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-13" }));
        }

        // ---------- Q-14, Q-04 ----------

        [Test]
        public void Q14_JudgedAtTheEndOfRoundEight_CompleteOrFail_Public()
        {
            var b = new TestBoard().Round(8).Quests(A, "Q-14").Quests(B, "Q-14")
                .Tower(A, TestBoard.DefaultTowerA, 7).Tower(B, TestBoard.DefaultTowerB, 6);
            var s = b.Build();
            var ev = End(s); // A's turn end in round 8: not yet
            Assert.That(ev.OfType<QuestCompleted>().Concat<GameEvent>(ev.OfType<QuestFailed>()), Is.Empty);
            ev = End(s);     // B's turn end = end of round 8
            foreach (var p in new[] { A, B })
            {
                var seen = EventFilter.For(ev, p);
                Assert.That(seen.OfType<QuestCompleted>().Select(q => (q.Player, q.QuestId)), Is.EqualTo(new[] { (A, "Q-14") }));
                Assert.That(seen.OfType<QuestFailed>().Select(q => (q.Player, q.QuestId)), Is.EqualTo(new[] { (B, "Q-14") }));
            }
            Assert.That(Status(s, A, "Q-14"), Is.EqualTo(QuestStatus.Completed));
            Assert.That(Status(s, B, "Q-14"), Is.EqualTo(QuestStatus.Failed));
            Assert.That(PlayerView.For(s, A).OpponentFailedQuests, Is.EqualTo(new[] { "Q-14" }));
        }

        [Test]
        public void Q14_TowerAtSevenInRoundSevenIsNotJudged()
        {
            var s = new TestBoard().Round(7).Active(B).Quests(A, "Q-14").Tower(A, TestBoard.DefaultTowerA, 7).Build();
            var ev = End(s);
            Assert.That(ev.OfType<QuestCompleted>(), Is.Empty);
            Assert.That(Status(s, A, "Q-14"), Is.EqualTo(QuestStatus.Active));
        }

        // ---------- Q-15 ----------

        [Test]
        public void Q15_ThreeUnitsOnTheirOwnBiome_PortalHasNone_RuneKeepsItsBiome()
        {
            var b = new TestBoard(Biome.Forest).Round(3).Rune(H(1, 1)).Quests(A, "Q-15");
            b.Unit(UnitClass.Guardian, A, H(1, 1), Biome.Forest);   // rune stone on Forest
            b.Unit(UnitClass.Guardian, A, H(2, 1), Biome.Forest);
            int onPortal = b.Unit(UnitClass.Guardian, A, Board.Portal, Biome.Forest);
            b.Unit(UnitClass.Guardian, A, H(-1, 2)); // Desert unit on Forest: no
            var s = b.Build();
            Assert.That(Completed(End(s), A), Is.Empty);
            End(s);
            Engine.Apply(s, new MoveCommand(A, onPortal, H(-1, 1)));
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-15" }));
        }

        // ---------- Q-16 ----------

        [Test]
        public void Q16_NoLossUntilTheEndOfRoundSixCompletesForBoth()
        {
            var b = new TestBoard().Round(6).Active(B).Quests(A, "Q-16").Quests(B, "Q-16");
            b.Unit(UnitClass.Guardian, A, H(0, 2));
            b.Unit(UnitClass.Guardian, B, H(0, -2));
            var s = b.Build();
            var ev = End(s);
            Assert.That(ev.OfType<QuestCompleted>().Select(q => q.Player), Is.EquivalentTo(new[] { A, B }));
            Assert.That(Status(s, A, "Q-16"), Is.EqualTo(QuestStatus.Completed));
            Assert.That(Status(s, B, "Q-16"), Is.EqualTo(QuestStatus.Completed));
        }

        [Test]
        public void Q16_Q04_AnOwnDeathFailsAtTheOwnersNextTurnEnd_PubliclyAndForGood()
        {
            var b = new TestBoard().Round(4).Active(B).Quests(A, "Q-16");
            b.Unit(UnitClass.Guardian, A, H(0, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            // v2.10: nothing is shown at the death (hidden flag).
            Assert.That(ev.OfType<QuestFailed>(), Is.Empty);
            Assert.That(Status(s, A, "Q-16"), Is.EqualTo(QuestStatus.Active));
            Assert.That(s.GetProgress(A).UnitsLost, Is.EqualTo(1));
            Assert.That(PlayerView.For(s, B).OpponentFailedQuests, Is.Empty);
            Assert.That(End(s).OfType<QuestFailed>(), Is.Empty); // B's turn end (round 4): not A's
            ev = End(s);                                          // A's turn end
            foreach (var p in new[] { A, B })
                Assert.That(EventFilter.For(ev, p).OfType<QuestFailed>().Select(q => (q.Player, q.QuestId)), Is.EqualTo(new[] { (A, "Q-16") }));
            Assert.That(Status(s, A, "Q-16"), Is.EqualTo(QuestStatus.Failed));
            var all = new List<GameEvent>();
            while (s.Round <= 6) all.AddRange(End(s));
            Assert.That(all.OfType<QuestCompleted>().Concat<GameEvent>(all.OfType<QuestFailed>()), Is.Empty);
            Assert.That(Status(s, A, "Q-16"), Is.EqualTo(QuestStatus.Failed));
        }

        [Test]
        public void Q16_ADeathInBsTurnOfRoundSixFailsAtThatRoundEnd_WhichComesFirst()
        {
            var b = new TestBoard().Round(6).Active(B).Quests(A, "Q-16");
            b.Unit(UnitClass.Guardian, A, H(0, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            var s = b.Build();
            Assert.That(Engine.Apply(s, new AttackCommand(B, archer, H(0, 1))).OfType<QuestFailed>(), Is.Empty);
            var ev = End(s); // end of round 6 comes before A's next turn end
            Assert.That(ev.OfType<QuestFailed>().Select(q => (q.Player, q.QuestId)), Is.EqualTo(new[] { (A, "Q-16") }));
            Assert.That(ev.OfType<QuestCompleted>(), Is.Empty);
        }

        [Test]
        public void Q16_BsOwnLossFailsAtBsTurnEnd()
        {
            var b = new TestBoard().Round(4).Quests(B, "Q-16");
            b.Unit(UnitClass.Guardian, B, H(0, -1), health: 1);
            int archer = b.Unit(UnitClass.Archer, A, H(0, 1));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, archer, H(0, -1)));
            Assert.That(End(s).OfType<QuestFailed>(), Is.Empty); // A's turn end
            Assert.That(End(s).OfType<QuestFailed>().Single().Player, Is.EqualTo(B));
        }

        // ---------- T-08 order ----------

        [Test]
        public void T08_TimersCountDownBeforeQuestsAreChecked()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-19");
            int x = b.Unit(UnitClass.Archer, A, H(0, -3));
            b.Unit(UnitClass.Archer, A, H(-1, -3));
            b.Effect(x, "C-10", 1); // expires at this turn end (C-04)
            var s = b.Build();
            var ev = End(s);
            int expired = ev.FindIndex(e => e is EffectExpired);
            int done = ev.FindIndex(e => e is QuestCompleted);
            Assert.That(expired, Is.GreaterThanOrEqualTo(0));
            Assert.That(done, Is.GreaterThan(expired));
            Assert.That(ev.FindIndex(e => e is TurnStarted), Is.GreaterThan(done));
        }

        // ---------- Q-17 ----------

        [Test]
        public void Q17_OwnTrapTriggeredOnAnEnemy_CompletesAtTheOwnersTurnEnd()
        {
            var b = new TestBoard().Round(3).Active(B).Quests(A, "Q-17");
            int runner = b.Unit(UnitClass.Guardian, B, H(2, -2));
            b.Trap(A, "C-21", H(1, -2)); // Mirror: no damage, still a trigger
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, runner, H(1, -2)));
            Assert.That(ev.OfType<TrapTriggered>().Count(), Is.EqualTo(1));
            Assert.That(Completed(End(s), A), Is.Empty); // B's turn end
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-17" }));
        }

        // ---------- Q-18 ----------

        [Test]
        public void Q18_HoldTheSameWellspringForTwoOwnTurnEnds()
        {
            var b = new TestBoard().Round(3).Wellspring(H(1, -1)).Wellspring(H(-1, 1)).Quests(A, "Q-18");
            int x = b.Unit(UnitClass.Archer, A, H(1, -1));
            var s = b.Build();
            End(s);
            End(s);
            Engine.Apply(s, new MoveCommand(A, x, H(-1, 1))); // another wellspring
            Assert.That(Completed(End(s), A), Is.Empty);
            End(s);
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-18" }));
        }

        // ---------- Q-19 ----------

        [Test]
        public void Q19_TwoUnitsInTheEnemyHomeZone_OneIsNotEnough()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-19");
            b.Unit(UnitClass.Archer, A, H(0, -3));
            int second = b.Unit(UnitClass.Archer, A, H(0, -2));
            var s = b.Build();
            Assert.That(Completed(End(s), A), Is.Empty);
            End(s);
            Engine.Apply(s, new MoveCommand(A, second, H(1, -3)));
            Assert.That(Completed(End(s), A), Is.EqualTo(new[] { "Q-19" }));
        }
    }
}
