using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // T-05 (v3.4): the move-attack approach cell is automatic.
    // Scenario: A's Rider (Desert) at (-3,3), B's Archer at (-2,1) (distance 2, out of adjacent range). The cells next to
    // both are (-3,2) and (-2,2) (path 1); (-1,1), (-3,1), (-2,0), (-1,0) are also next to the target but farther away.
    public class MoveAttackTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static readonly Hex Start = H(-3, 3), Target = H(-2, 1);

        static (GameState s, int rider) Scenario(TestBoard tb, int archerHealth = -1)
        {
            int rider = tb.Unit(UnitClass.Rider, A, Start, Biome.Desert);
            tb.Unit(UnitClass.Archer, B, Target, health: archerHealth);
            return (tb.Build(), rider);
        }

        [Test]
        public void T05_MoveAttack_ApproachIsTheShortestPath_BeforeBiome()
        {
            var (s, r) = Scenario(new TestBoard().Biome(H(-1, 1), Biome.Desert)); // own biome, but path 2
            Assert.That(MoveAttack.ApproachFor(s, s.GetUnit(r), Target), Is.EqualTo(H(-3, 2)));
        }

        [Test]
        public void T05_MoveAttack_TieGoesToOwnBiome()
        {
            var (s, r) = Scenario(new TestBoard().Biome(H(-2, 2), Biome.Desert)); // later in board order, own biome
            Assert.That(MoveAttack.ApproachFor(s, s.GetUnit(r), Target), Is.EqualTo(H(-2, 2)));
        }

        [Test]
        public void T05_MoveAttack_TieThenGoesToBoardOrderAsTheActorSeesIt()
        {
            var (s, r) = Scenario(new TestBoard());
            Assert.That(MoveAttack.ApproachFor(s, s.GetUnit(r), Target), Is.EqualTo(H(-3, 2))); // lower Board.Cells index

            // B (mirrored scenario) breaks the same tie the same way in its own view: the mirror of (-3,2).
            var tb = new TestBoard().Active(B);
            int rb = tb.Unit(UnitClass.Rider, B, Start.Mirror(), Biome.Desert);
            tb.Unit(UnitClass.Archer, A, Target.Mirror());
            var sb = tb.Build();
            Assert.That(MoveAttack.ApproachFor(sb, sb.GetUnit(rb), Target.Mirror()), Is.EqualTo(H(-3, 2).Mirror()));
        }

        [Test]
        public void T05_MoveAttack_OneLegalCommandPerTarget_WithTheComputedApproach()
        {
            var (s, r) = Scenario(new TestBoard().Biome(H(-2, 2), Biome.Desert));
            var mas = Engine.GetLegalCommands(s, A).OfType<MoveAttackCommand>().Where(c => c.UnitId == r).ToList();
            Assert.That(mas.GroupBy(c => c.Target).All(g => g.Count() == 1), Is.True);
            var cmd = mas.Single(c => c.Target == Target);
            Assert.That(cmd.Approach, Is.EqualTo(H(-2, 2)));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new MoveAttackCommand(A, r, H(-3, 2), Target)));
            var opt = Preview.MoveAttackTargets(PlayerView.For(s, A), r).Single(o => o.Target == Target);
            Assert.That(opt.Approaches, Is.EqualTo(new[] { H(-2, 2) }));
        }

        [Test]
        public void T05_MoveAttack_AiUsesTheComputedApproach()
        {
            var (s, r) = Scenario(new TestBoard().Biome(H(-2, 2), Biome.Desert), archerHealth: 1); // lethal only by move-attack
            var c = GreedyAi.Choose(PlayerView.For(s, A), Engine.GetLegalCommands(s, A), AiLevel.Normal, new Rng(1));
            Assert.That(c, Is.InstanceOf<MoveAttackCommand>(), c.ToString());
            var ma = (MoveAttackCommand)c;
            Assert.That(ma.Target, Is.EqualTo(Target));
            Assert.That(ma.Approach, Is.EqualTo(MoveAttack.ApproachFor(s, s.GetUnit(r), Target)));
        }
    }
}
