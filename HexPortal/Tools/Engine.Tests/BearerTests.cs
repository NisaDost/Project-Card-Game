using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD v4.0 U-44 Portal Bearer and W-01 (only the Bearer uses the Portal).
    public class BearerTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static System.Collections.Generic.List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        // A has Q-11 done and Q-19 completes at its turn end (two units in B's home zone).
        static TestBoard OpensAtTurnEnd()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-11", "Q-19").QuestStatus(A, "Q-11", QuestStatus.Completed);
            b.Unit(UnitClass.Archer, A, H(0, -6));
            b.Unit(UnitClass.Archer, A, H(-1, -6));
            return b;
        }

        [Test]
        public void U44_StatsComeFromTheCatalog_NotACard()
        {
            var d = Catalog.PortalBearer;
            Assert.That((d.Id, d.Attack, d.Health, d.Move, d.Sight, d.Range, d.MaxRange), Is.EqualTo(("U-44", 10, 80, 2, 2, RangeKind.Adjacent, 1)));
            Assert.That(Unit.DefOf(UnitClass.PortalBearer), Is.SameAs(d));
            Assert.That(Catalog.Units.Any(u => u.Class == UnitClass.PortalBearer), Is.False);
            Assert.That(Catalog.CharacterCards.Any(c => c.Class == UnitClass.PortalBearer), Is.False);
        }

        [Test]
        public void U44_W01_TheBearerAppearsNextToTheTowerWhenThePortalOpens()
        {
            var s = OpensAtTurnEnd().Build();
            var ev = End(s);
            var spawned = ev.OfType<BearerSpawned>().Single();
            Assert.That(spawned.Owner, Is.EqualTo(A));
            // Free tower neighbours closest to the Portal: (-3,5) and (-2,5) at 5; tie broken by board order (seat view).
            Assert.That(spawned.Cell, Is.EqualTo(H(-3, 5)));
            var u = s.GetUnit(spawned.UnitId);
            Assert.That((u.Class, u.Owner, u.Health, u.Biome), Is.EqualTo((UnitClass.PortalBearer, A, 80, Biome.None)));
            Assert.That(PlayerView.For(s, A).OwnBearerId, Is.EqualTo(u.Id));
            Assert.That(PlayerView.For(s, A).PortalOpen, Is.True);
            Assert.That(PlayerView.For(s, B).OpponentPortalOpen, Is.True);
        }

        [Test]
        public void U44_ThePlacementIsSeatSymmetric()
        {
            var b = new TestBoard().Round(3).Active(B).Quests(B, "Q-11", "Q-19").QuestStatus(B, "Q-11", QuestStatus.Completed);
            b.Unit(UnitClass.Archer, B, H(0, 6));
            b.Unit(UnitClass.Archer, B, H(1, 6));
            var s = b.Build();
            Assert.That(End(s).OfType<BearerSpawned>().Single().Cell, Is.EqualTo(H(-3, 5).Mirror()));
        }

        [Test]
        public void U44_AppearsOnlyOnce_NotAgainWhileAlive()
        {
            var s = OpensAtTurnEnd().Build();
            End(s);
            End(s);
            Assert.That(End(s).OfType<BearerSpawned>(), Is.Empty);
            Assert.That(s.Units.Count(u => u.Class == UnitClass.PortalBearer), Is.EqualTo(1));
        }

        [Test]
        public void U44_NoFreeCell_ItWaitsAndCannotActTheTurnItAppears()
        {
            var b = OpensAtTurnEnd();
            var blockers = Enumerable.Range(0, 6).Select(d => TestBoard.DefaultTowerA.Neighbor(d)).Where(Board.IsOnBoard)
                .Select(h => b.Unit(UnitClass.Healer, A, h)).ToList();
            var s = b.Build();
            Assert.That(End(s).OfType<BearerSpawned>(), Is.Empty);
            Assert.That(PlayerView.For(s, A).OwnBearerPending, Is.True);
            s.RemoveUnit(s.GetUnit(blockers[0]));
            var ev = End(s); // A's turn start: it appears now
            var u = s.GetUnit(ev.OfType<BearerSpawned>().Single().UnitId);
            Assert.That(u.ActedThisTurn, Is.True);
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveCommand>().Any(m => m.UnitId == u.Id), Is.False);
        }

        [Test]
        public void U44_ADeadBearerReturnsAtTheNextOwnTurnStart()
        {
            var b = new TestBoard().Round(3).Active(B).Quests(A, "Q-11", "Q-12").QuestStatus(A, "Q-11", QuestStatus.Completed)
                .QuestStatus(A, "Q-12", QuestStatus.Completed);
            int bearer = b.Unit(UnitClass.PortalBearer, A, H(0, 2), health: 5);
            int rider = b.Unit(UnitClass.Rider, B, H(0, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(B, rider, H(0, 2)));
            Assert.That(ev.OfType<UnitDied>().Single().UnitId, Is.EqualTo(bearer));
            Assert.That(s.GetProgress(A).BearerPending, Is.True);
            ev = End(s); // A's turn start
            var back = ev.OfType<BearerSpawned>().Single();
            Assert.That(back.UnitId, Is.Not.EqualTo(bearer));
            Assert.That(s.GetUnit(back.UnitId).Health, Is.EqualTo(Catalog.PortalBearer.Health));
            Assert.That(s.GetUnit(back.UnitId).ActedThisTurn, Is.True);
        }

        [Test]
        public void U44_V10_TheOpponentSeesTheSpawnOnlyOnAVisibleCell()
        {
            var b = OpensAtTurnEnd();
            var s = b.Build();
            var ev = End(s);
            Assert.That(EventFilter.For(ev, B).OfType<BearerSpawned>(), Is.Empty, "B cannot see A's tower area");
            Assert.That(EventFilter.For(ev, A).OfType<BearerSpawned>().Count(), Is.EqualTo(1));
            Assert.That(PlayerView.For(s, B).EnemyUnits.Any(u => u.Class == UnitClass.PortalBearer), Is.False);
        }

        [Test]
        public void W01_OnlyTheBearerCanWinViaThePortal()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12", "Q-13", "Q-17").QuestStatus(A, "Q-12", QuestStatus.Completed)
                .QuestStatus(A, "Q-13", QuestStatus.Completed);
            b.Unit(UnitClass.Guardian, A, Board.Portal);
            var s = b.Build();
            End(s);
            End(s);
            End(s);
            End(s);
            Assert.That(s.IsOver, Is.False);
        }

        [Test]
        public void W01_ANonBearerOnThePortalStillBlocksTheEnemyBearer()
        {
            var b = new TestBoard().Round(3).Quests(A, "Q-12", "Q-13", "Q-17").QuestStatus(A, "Q-12", QuestStatus.Completed)
                .QuestStatus(A, "Q-13", QuestStatus.Completed);
            int bearer = b.Unit(UnitClass.PortalBearer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, Board.Portal);
            var s = b.Build();
            Assert.That(Movement.Destinations(s, s.GetUnit(bearer)), Does.Not.Contain(Board.Portal));
        }
    }
}
