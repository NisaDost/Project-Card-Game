using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // M6 D2: the viewer's own quest progress in PlayerView (Q-*, V-09, V-11). The opponent's progress is never shown;
    // the Hunter counter shows only kills the viewer saw (PM), completion still uses the real count.
    public class QuestProgressViewTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        static QuestProgressView Q(GameState s, PlayerId p, string id) => PlayerView.For(s, p).QuestProgress.Single(q => q.Id == id);

        [Test]
        public void Q12_HunterCounterShowsOnlySeenKills_CompletionUsesTheRealCount()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12");
            int m = b.Unit(UnitClass.Mage, A, H(-1, 1));
            int archer = b.Unit(UnitClass.Archer, A, H(1, 1));
            b.Unit(UnitClass.Guardian, B, H(-1, -1));
            b.Unit(UnitClass.Healer, B, H(0, -2), health: 1);   // hidden from A, dies to splash
            b.Unit(UnitClass.Archer, B, H(1, -1), health: 1);   // visible to A
            var s = b.Build();

            Engine.Apply(s, new AttackCommand(A, m, H(-1, -1)));
            Assert.That(s.GetProgress(A).Kills, Is.EqualTo(1));
            Assert.That(s.GetProgress(A).SeenKills, Is.EqualTo(0));
            var hunter = Q(s, A, "Q-12");
            Assert.That((hunter.Status, hunter.Current, hunter.Target), Is.EqualTo((QuestStatus.Active, 0, 2)));

            Engine.Apply(s, new AttackCommand(A, archer, H(1, -1)));
            Assert.That(s.GetProgress(A).SeenKills, Is.EqualTo(1));
            Assert.That(Q(s, A, "Q-12").Current, Is.EqualTo(1));

            End(s); // real count 2: completed (public), the counter still shows only the seen kill
            hunter = Q(s, A, "Q-12");
            Assert.That((hunter.Status, hunter.Current), Is.EqualTo((QuestStatus.Completed, 1)));
        }

        [Test]
        public void Q12_SeenKillsSurviveCloneAndChangeTheStateHash()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12");
            int archer = b.Unit(UnitClass.Archer, A, H(1, 1));
            b.Unit(UnitClass.Archer, B, H(1, -1), health: 1);
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, archer, H(1, -1)));
            var c = s.Clone();
            Assert.That(c.GetProgress(A).SeenKills, Is.EqualTo(1));
            Assert.That(StateHash.Compute(c), Is.EqualTo(StateHash.Compute(s)));
            c.GetProgress(A).SeenKills = 0;
            Assert.That(StateHash.Compute(c), Is.Not.EqualTo(StateHash.Compute(s)));
        }

        [Test]
        public void Q13_SiegeShowsTheTowerDamageDealtSoFar()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-13");
            int archer = b.Unit(UnitClass.Archer, A, H(2, -2));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, archer, TestBoard.DefaultTowerB));
            var siege = Q(s, A, "Q-13");
            Assert.That((siege.Current, siege.Target, siege.Round), Is.EqualTo((2, 5, 0)));
        }

        [Test]
        public void Q10_Q18_HoldQuestsShowTheStreakAndTheHeldCell()
        {
            var b = new TestBoard().Round(3).Rune(H(1, -1)).Wellspring(H(-1, 1)).Quests(A, "Q-10", "Q-18");
            b.Unit(UnitClass.Guardian, A, H(1, -1));
            var s = b.Build();
            var rune = Q(s, A, "Q-10");
            Assert.That((rune.Current, rune.Target, rune.Cell), Is.EqualTo((0, Catalog.QuestHoldTurns, (Hex?)null)));
            End(s);
            rune = Q(s, A, "Q-10");
            Assert.That((rune.Current, rune.Cell), Is.EqualTo((1, (Hex?)H(1, -1))));
            var well = Q(s, A, "Q-18");
            Assert.That((well.Current, well.Cell), Is.EqualTo((0, (Hex?)null)));
        }

        [Test]
        public void Q11_Q15_Q19_ShowTheCountAtThisMoment()
        {
            var b = new TestBoard().Round(3).Rune(H(1, -1)).Quests(A, "Q-11", "Q-15", "Q-19");
            b.Unit(UnitClass.Guardian, A, H(1, -1), Biome.Forest);   // on a rune, own biome (board is Forest)
            b.Unit(UnitClass.Archer, A, H(0, -3), Biome.Forest);     // B's home zone, own biome
            b.Unit(UnitClass.Healer, A, H(0, 1), Biome.Desert);      // off biome
            var s = b.Build();
            Assert.That(Q(s, A, "Q-11").Current, Is.EqualTo(1));
            Assert.That(Q(s, A, "Q-15").Current, Is.EqualTo(2));
            Assert.That(Q(s, A, "Q-19").Current, Is.EqualTo(1));
            Assert.That(Q(s, A, "Q-19").Target, Is.EqualTo(2));
        }

        [Test]
        public void Q14_ShowsTheTowerHealthAndTheRoundDeadline()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-14").Tower(A, TestBoard.DefaultTowerA, 8);
            var s = b.Build();
            var q = Q(s, A, "Q-14");
            Assert.That((q.Current, q.Target, q.Round), Is.EqualTo((8, 7, 8)));
        }

        [Test]
        public void Q16_TheOwnerSeesTheirOwnLossBeforeTheFailureIsAnnounced()
        {
            var b = new TestBoard().Round(3).Active(B).Quests(A, "Q-16");
            b.Unit(UnitClass.Archer, A, H(1, -1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(1, -3));
            var s = b.Build();
            var q = Q(s, A, "Q-16");
            Assert.That((q.Current, q.Round, q.Status), Is.EqualTo((0, 6, QuestStatus.Active)));
            Engine.Apply(s, new AttackCommand(B, archer, H(1, -1)));
            q = Q(s, A, "Q-16");
            Assert.That((q.Current, q.Status), Is.EqualTo((1, QuestStatus.Active))); // the failure is announced later (v2.10)
        }

        [Test]
        public void V09_QuestProgressListsOnlyTheViewersOwnQuests()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12", "Q-13", "Q-17").Quests(B, "Q-10", "Q-14", "Q-19");
            var s = b.Build();
            Assert.That(PlayerView.For(s, A).QuestProgress.Select(q => q.Id), Is.EqualTo(new[] { "Q-12", "Q-13", "Q-17" }));
            Assert.That(PlayerView.For(s, B).QuestProgress.Select(q => q.Id), Is.EqualTo(new[] { "Q-10", "Q-14", "Q-19" }));
        }
    }
}
