using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD v4.0 §3.4: U-40 Gatherer (B-11, B-12, T-03, T-05), U-41 Besieger, U-42 Assassin, U-43 Flyer; "melee" as in §3.4.
    public class NewClassTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static int Hp(UnitClass c) => Unit.DefOf(c).Health;

        // ---------- U-40 Gatherer: B-11, B-12, T-03, T-05 ----------

        [Test]
        public void B11_T03_RuneGivesOneManaOnlyWithAnOwnGatherer()
        {
            var b = new TestBoard().Rune(H(-1, 2)).Rune(H(1, -2)).Rune(H(2, 0)).Round(8);
            b.Unit(UnitClass.Gatherer, A, H(-1, 2));
            b.Unit(UnitClass.Guardian, A, H(1, -2));  // not a Gatherer: nothing
            b.Unit(UnitClass.Gatherer, B, H(2, 0));   // the enemy's Gatherer gives A nothing
            var s = b.Build();
            Assert.That(Mana.TurnStartMana(s, A), Is.EqualTo(Catalog.ManaCap + Catalog.RuneManaBonus), "may exceed the cap");
            Assert.That(Mana.TurnStartEnergy(s, A), Is.EqualTo(Catalog.EnergyPerTurn), "a rune gives no Energy");
            Assert.That(Mana.TurnStartMana(s, B), Is.EqualTo(Catalog.ManaCap + Catalog.RuneManaBonus));
        }

        [Test]
        public void B12_T05_WellspringGivesOneEnergyOnlyWithAnOwnGatherer()
        {
            var b = new TestBoard().Wellspring(H(-1, 2)).Wellspring(H(1, -2)).Wellspring(H(2, 1));
            b.Unit(UnitClass.Gatherer, A, H(-1, 2));
            b.Unit(UnitClass.Gatherer, A, H(1, -2));
            b.Unit(UnitClass.Rider, A, H(2, 1)); // not a Gatherer
            var s = b.Build();
            Assert.That(Mana.TurnStartEnergy(s, A), Is.EqualTo(Catalog.EnergyPerTurn + 2 * Catalog.WellspringEnergyBonus));
            Assert.That(Mana.TurnStartMana(s, A), Is.EqualTo(1), "a wellspring gives no Mana");
        }

        [Test]
        public void B11_Q10_QuestsStillAcceptAnyUnitOnARune()
        {
            var b = new TestBoard().Rune(H(-1, 2)).Quests(A, "Q-11", "Q-12", "Q-13").Rune(H(1, 1));
            b.Unit(UnitClass.Guardian, A, H(-1, 2));
            b.Unit(UnitClass.Archer, A, H(1, 1));
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetProgress(A).GetQuestStatus(0), Is.EqualTo(QuestStatus.Completed));
        }

        // ---------- U-41 Besieger ----------

        [Test]
        public void U41_BesiegerDealsTenToUnitsAndFiftyToTheTower()
        {
            var b = new TestBoard().Energy(A, 3);
            int s1 = b.Unit(UnitClass.Besieger, A, H(3, -5));          // next to B's tower (3,-6)
            int s2 = b.Unit(UnitClass.Besieger, A, H(0, 0));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, s2, H(1, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(Hp(UnitClass.Guardian) - 10));
            Engine.Apply(s, new AttackCommand(A, s1, TestBoard.DefaultTowerB));
            Assert.That(s.GetTower(B).Health, Is.EqualTo(Catalog.Tower.Health - Catalog.BesiegerTowerAttack));
            Assert.That(Catalog.BesiegerTowerAttack, Is.EqualTo(50));
        }

        // ---------- U-42 Assassin ----------

        [Test]
        public void U42_AssassinReachesTargetsAtDistanceOneToFour()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            b.Unit(UnitClass.Archer, A, H(3, 0));       // sight onto (4,0) and (5,0)
            b.Unit(UnitClass.Guardian, B, H(-1, 0));    // 1
            b.Unit(UnitClass.Guardian, B, H(4, 0) + new Hex(0, -1)); // (4,-1): distance 4
            b.Unit(UnitClass.Guardian, B, H(5, 0));     // 5: out of range
            var s = b.Build();
            var t = Combat.LegalTargets(s, s.GetUnit(a));
            Assert.That(t, Does.Contain(H(-1, 0)));
            Assert.That(t, Does.Contain(H(4, -1)));
            Assert.That(t, Does.Not.Contain(H(5, 0)));
            Assert.That(Unit.DefOf(UnitClass.Assassin).IsMelee, Is.True);
        }

        [Test]
        public void U42_V07_LeapRangeBeyondOwnSightNeedsAVisibleTarget()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(4, 0)); // distance 4 > Sight 3, nothing else of A sees it
            var s = b.Build();
            Assert.That(Combat.LegalTargets(s, s.GetUnit(a)), Is.Empty);
        }

        [Test]
        public void U42_AssassinThatKillsReturnsToItsOwnCell()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            int v = b.Unit(UnitClass.Healer, B, H(3, 0), health: 30);
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(3, 0)));
            Assert.That(s.GetUnit(v), Is.Null);
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(0, 0)));
            Assert.That(ev.OfType<UnitMoved>(), Is.Empty);
        }

        [Test]
        public void U42_AssassinThatDoesNotKillStaysOnTheNearestFreeCellNextToTheTarget()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            int g = b.Unit(UnitClass.Guardian, B, H(3, 0));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(3, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(Hp(UnitClass.Guardian) - Unit.DefOf(UnitClass.Assassin).Attack));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(2, 0)), "the free neighbour of the target closest to the start cell");
            Assert.That(ev.OfType<UnitMoved>().Single().To, Is.EqualTo(H(2, 0)));
        }

        [Test]
        public void U42_AdjacentTargetSurvives_AssassinStaysPut()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, a, H(1, 0)));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(0, 0)));
        }

        [Test]
        public void U42_LandingTriggersTheTrapThere()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(3, 0));
            b.Trap(B, "C-20", H(2, 0));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, a, H(3, 0)));
            Assert.That(s.GetUnit(a).Health, Is.EqualTo(Hp(UnitClass.Assassin) - 30));
            Assert.That(s.Traps, Is.Empty);
        }

        [Test]
        public void U42_LandingInTowerRangeDrawsTheTowerShot()
        {
            var b = new TestBoard().Tower(B, H(2, -2));
            int a = b.Unit(UnitClass.Assassin, A, H(-1, 0));
            b.Unit(UnitClass.Archer, A, H(0, 1)); // sight onto the target (distance 4 from the Assassin)
            b.Unit(UnitClass.Guardian, B, H(3, 0));
            var s = b.Build();
            // Start (-1,0) is at distance 3 from the tower, so only the landing on (2,0) (distance 2) can trigger the shot.
            Engine.Apply(s, new AttackCommand(A, a, H(3, 0)));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(2, 0)));
            Assert.That(s.GetUnit(a).Health, Is.EqualTo(Hp(UnitClass.Assassin) - Catalog.Tower.Attack));
        }

        [Test]
        public void U42_NoFreeCellNextToTheTarget_AssassinStaysHome()
        {
            var b = new TestBoard();
            foreach (var n in new[] { H(3, 0), H(3, -1), H(2, -1), H(1, 0), H(1, 1), H(2, 1) }) b.Mountain(n);
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            int g = b.Unit(UnitClass.Guardian, B, H(2, 0));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, a, H(2, 0)));
            Assert.That(s.GetUnit(g).Health, Is.LessThan(Hp(UnitClass.Guardian)));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(0, 0)));
        }

        [Test]
        public void U42_U43_AssassinCannotHitAFlyer()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Assassin, A, H(0, 0));
            b.Unit(UnitClass.Flyer, B, H(2, 0));
            var s = b.Build();
            Assert.That(Combat.LegalTargets(s, s.GetUnit(a)), Does.Not.Contain(H(2, 0)));
        }

        // ---------- U-43 Flyer ----------

        [Test]
        public void U43_MeleeAttacksCannotTargetAFlyer_RangedAndTowerCan()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(1, 0));
            int ar = b.Unit(UnitClass.Archer, A, H(-1, 0));
            int f = b.Unit(UnitClass.Flyer, B, H(2, 0));
            var s = b.Build();
            Assert.That(Combat.LegalTargets(s, s.GetUnit(g)), Does.Not.Contain(H(2, 0)));
            Assert.That(Combat.LegalTargets(s, s.GetUnit(ar)), Does.Contain(H(2, 0)));
            Assert.That(Unit.DefOf(UnitClass.Archer).IsMelee, Is.False);
            Assert.That(Unit.DefOf(UnitClass.Mage).IsMelee, Is.False);
            foreach (var c in new[] { UnitClass.Guardian, UnitClass.Rider, UnitClass.Healer, UnitClass.Gatherer,
                         UnitClass.Besieger, UnitClass.Flyer, UnitClass.PortalBearer })
                Assert.That(Unit.DefOf(c).IsMelee, Is.True, c.ToString());
        }

        [Test]
        public void U43_TowerShootsAFlyer()
        {
            var b = new TestBoard().Active(B);
            int f = b.Unit(UnitClass.Flyer, B, H(-3, 3));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(B, f, H(-3, 4))); // distance 2 from A's tower (-3,6)
            Assert.That(s.GetUnit(f).Health, Is.EqualTo(Hp(UnitClass.Flyer) - Catalog.Tower.Attack));
        }

        [Test]
        public void U43_FlyerPassesMountainRiverLavaAndUnits_StopsOnRiverNotOnMountain()
        {
            var b = new TestBoard().Mountain(H(1, 0)).River(H(2, 0)).Lava(H(-1, 0));
            int f = b.Unit(UnitClass.Flyer, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(0, -1));
            var s = b.Build();
            var d = Movement.Destinations(s, s.GetUnit(f));
            Assert.That(d, Does.Contain(H(3, 0)), "over the Mountain and the River");
            Assert.That(d, Does.Contain(H(2, 0)), "stops on River");
            Assert.That(d, Does.Contain(H(-1, 0)), "stops on Lava");
            Assert.That(d, Does.Contain(H(0, -2)), "over a unit");
            Assert.That(d, Does.Not.Contain(H(1, 0)));
            Assert.That(d, Does.Not.Contain(H(0, -1)));
        }

        [Test]
        public void U43_FlyerIgnoresLavaAndTraps()
        {
            var b = new TestBoard().Lava(H(-1, 0));
            int f = b.Unit(UnitClass.Flyer, A, H(0, 0));
            int f2 = b.Unit(UnitClass.Flyer, A, H(2, 0));
            b.Trap(B, "C-20", H(2, -1));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, f, H(-1, 0)));
            Engine.Apply(s, new MoveCommand(A, f2, H(2, -1)));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(f).Health, Is.EqualTo(Hp(UnitClass.Flyer)));
            Assert.That(s.GetUnit(f2).Health, Is.EqualTo(Hp(UnitClass.Flyer)));
            Assert.That(s.Traps.Count(), Is.EqualTo(1), "the trap is not used up");
        }

        [Test]
        public void U43_GroundUnitsPassThroughAFlyerButCannotStopOnIt()
        {
            var b = new TestBoard();
            int ar = b.Unit(UnitClass.Archer, A, H(0, 0));
            b.Unit(UnitClass.Flyer, B, H(1, 0));
            var s = b.Build();
            var d = Movement.Destinations(s, s.GetUnit(ar));
            Assert.That(d, Does.Contain(H(2, 0)), "(2,0) is reachable in 2 steps only through (1,0)");
            Assert.That(d, Does.Not.Contain(H(1, 0)));
        }

        [Test]
        public void U43_FlyerDoesNotPutOutAnEnemyTorch()
        {
            var b = new TestBoard().Active(B).Mana(B, 0);
            int f = b.Unit(UnitClass.Flyer, B, H(0, 0));
            var s = b.Build();
            s.TorchList.Add(new Torch(1, A, H(1, 0)));
            Engine.Apply(s, new MoveCommand(B, f, H(1, 0)));
            Assert.That(s.Torches.Count(), Is.EqualTo(1));
        }
    }
}
