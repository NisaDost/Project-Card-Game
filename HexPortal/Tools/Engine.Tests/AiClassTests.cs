using System.Diagnostics;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // AI-02 (v4.0 phase 3): the new classes (§3.4), the Bearer escort (W-01) and AI-05 decision time.
    public class AiClassTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static ICommand Choose(GameState s, PlayerId p) =>
            GreedyAi.Choose(PlayerView.For(s, p), Engine.GetLegalCommands(s, p), AiLevel.Normal, new Rng(1));

        static TestBoard PortalOpen(TestBoard tb, PlayerId p) =>
            tb.Quests(p, "Q-10", "Q-11", "Q-12").QuestStatus(p, "Q-10", QuestStatus.Completed).QuestStatus(p, "Q-11", QuestStatus.Completed);

        [Test]
        public void AI02_U40_GathererStepsOntoAFreeRune()
        {
            var tb = new TestBoard().Rune(H(0, 2)).Energy(A, 1);
            int g = tb.Unit(UnitClass.Gatherer, A, H(0, 3));
            var s = tb.Build();
            Assert.That(Choose(s, A), Is.EqualTo(new MoveCommand(A, g, H(0, 2))));
        }

        [Test]
        public void AI02_U41_BesiegerHitsTheTowerRatherThanAUnit()
        {
            var tb = new TestBoard().Energy(A, 1);
            int bs = tb.Unit(UnitClass.Besieger, A, H(2, -5));   // next to B's tower (3,-6)
            tb.Unit(UnitClass.Archer, B, H(1, -5));
            var s = tb.Build();
            Assert.That(Choose(s, A), Is.EqualTo(new AttackCommand(A, bs, TestBoard.DefaultTowerB)));
        }

        [Test]
        public void AI02_U42_AssassinTakesTheLowHealthKill()
        {
            var tb = new TestBoard().Energy(A, 1);
            int a = tb.Unit(UnitClass.Assassin, A, H(0, 0));
            tb.Unit(UnitClass.Guardian, B, H(2, 0));
            tb.Unit(UnitClass.Healer, B, H(-2, 0), health: 30);
            var s = tb.Build();
            var c = Choose(s, A);
            Assert.That(c is AttackCommand at && at.Target == H(-2, 0) || c is MoveAttackCommand m && m.Target == H(-2, 0), Is.True, c.ToString());
        }

        [Test]
        public void AI02_U43_FlyerScoutsWhenNothingElseToDo()
        {
            var tb = new TestBoard().Energy(A, 1).Mana(A, 0);
            int f = tb.Unit(UnitClass.Flyer, A, H(0, 3));
            var s = tb.Build();
            var c = Choose(s, A);
            Assert.That(c is MoveCommand m && m.UnitId == f, Is.True, c.ToString());
        }

        [Test]
        public void AI02_W01_EscortStepsOffTheBearersPath()
        {
            var tb = PortalOpen(new TestBoard().Energy(A, 1).Mana(A, 0), A);
            int bearer = tb.Unit(UnitClass.PortalBearer, A, H(0, 4));
            int g = tb.Unit(UnitClass.Guardian, A, H(0, 2)); // on the straight way (0,3) (0,2) (0,1) (0,0)
            var s = tb.Build();
            s.GetUnit(bearer).ActedThisTurn = true;
            ExploreAll(s, A);
            var c = Choose(s, A) as MoveCommand;
            Assert.That(c, Is.Not.Null);
            Assert.That(c.UnitId, Is.EqualTo(g));
            Assert.That(new[] { H(0, 1), H(0, 3) }, Does.Not.Contain(c.Dest));
        }

        [Test]
        public void AI02_W01_HitsTheEnemyBearerHeadingForItsOpenPortal()
        {
            var tb = PortalOpen(new TestBoard().Energy(A, 1).Mana(A, 0), B);
            int archer = tb.Unit(UnitClass.Archer, A, H(0, 1));
            tb.Unit(UnitClass.Guardian, A, Board.Portal); // already blocking (W-01)
            tb.Unit(UnitClass.PortalBearer, B, H(0, -2));
            tb.Unit(UnitClass.Mage, B, H(3, -2));
            var s = tb.Build();
            Assert.That(Choose(s, A), Is.EqualTo(new AttackCommand(A, archer, H(0, -2))));
        }

        /// <summary>AI-05: a crowded worst case (every class on both sides, a full hand and Mana) stays under the limit.</summary>
        [Test]
        public void AI05_CrowdedBoardDecisionTakesUnderOneSecond()
        {
            var tb = new TestBoard().FullPools().Mana(A, 6).Energy(A, 5);
            var classes = Catalog.Units.Select(u => u.Class).ToArray();
            for (int i = 0; i < classes.Length; i++)
            {
                tb.Unit(classes[i], A, H(-4 + i, 2));
                tb.Unit(classes[i], B, H(-4 + i, -1));
            }
            foreach (var id in new[] { "C-10", "C-15", "C-18", "C-20", "U-42-Forest", "U-43-Snow" }) tb.Hand(A, id);
            var s = tb.Build();
            ExploreAll(s, A);
            Choose(s, A); // warm-up (JIT)
            var sw = Stopwatch.StartNew();
            Choose(s, A);
            sw.Stop();
            Assert.That(sw.Elapsed.TotalMilliseconds, Is.LessThan(Catalog.AiMaxSecondsPerMove * 1000.0));
        }

        static void ExploreAll(GameState s, PlayerId p)
        {
            var fog = s.GetFog(p);
            foreach (var h in Board.Cells)
                if (fog.Get(h) == CellVisibility.Hidden) fog.Set(h, CellVisibility.Explored, new LastSeen(s.Map.Get(h)));
        }
    }
}
