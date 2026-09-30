using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §3 units (U-01…U-05, U-07, U-09…U-12, U-20…U-26). Scenarios stay in rows r = -1..1 unless a
    // tower is involved: those cells are at distance >= 3 from both default towers (no tower shots).
    public class UnitTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static List<Hex> Targets(GameState s, int id) => Combat.LegalTargets(s, s.GetUnit(id));
        static List<Hex> Moves(GameState s, int id) => Movement.Destinations(s, s.GetUnit(id));
        static UnitDef Def(UnitClass c) => Catalog.Units.Single(u => u.Class == c);

        static HashSet<Hex> AllCells() => new HashSet<Hex>(Board.Cells);

        // ---------- U-01…U-05 class ranges ----------

        [Test]
        public void U01_GuardianAttacksAdjacentOnly_Move1()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(0, 0));
            b.Unit(UnitClass.Archer, B, H(1, 0));
            b.Unit(UnitClass.Archer, B, H(2, 0));
            var s = b.Build();
            Assert.That(Targets(s, g), Is.EqualTo(new[] { H(1, 0) }));
            Assert.That(Moves(s, g).Count, Is.EqualTo(5)); // 6 neighbours, one occupied
        }

        [Test]
        public void U02_RiderAttacksAdjacentOnly_Move3()
        {
            var b = new TestBoard();
            int r = b.Unit(UnitClass.Rider, A, H(0, 0));
            b.Unit(UnitClass.Archer, B, H(1, 0));
            b.Unit(UnitClass.Archer, B, H(2, 0));
            var s = b.Build();
            Assert.That(Targets(s, r), Is.EqualTo(new[] { H(1, 0) }));
            Assert.That(Moves(s, r).Count, Is.EqualTo(36 - 2)); // skill table: 36 from the Portal, minus 2 occupied
        }

        [Test]
        public void U04_MageAttacksDistance1to2()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(0, 0));
            b.Unit(UnitClass.Guardian, A, H(3, -1)); // reveals (3,0)
            b.Unit(UnitClass.Archer, B, H(1, 0));
            b.Unit(UnitClass.Archer, B, H(2, -1));
            b.Unit(UnitClass.Archer, B, H(3, 0));
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(3, 0)), Is.True);
            Assert.That(Targets(s, m), Is.EquivalentTo(new[] { H(1, 0), H(2, -1) }));
        }

        [Test]
        public void U05_HealerAttacksAdjacentOnly_Move1()
        {
            var b = new TestBoard();
            int h = b.Unit(UnitClass.Healer, A, H(0, 0));
            b.Unit(UnitClass.Archer, B, H(1, 0));
            b.Unit(UnitClass.Archer, B, H(2, 0));
            var s = b.Build();
            Assert.That(Targets(s, h), Is.EqualTo(new[] { H(1, 0) }));
            Assert.That(Moves(s, h).Count, Is.EqualTo(5));
        }

        // ---------- Movement U-07, U-09, U-10, U-12 ----------

        [Test]
        public void U07_MoveReachesUpToMoveSteps_ThroughEmptyCells()
        {
            // Reach counts on an empty, fully visible board (hex-grid skill).
            var cases = new (UnitClass cls, int x, int y, int expected)[]
            {
                (UnitClass.Guardian, 6, 4, 6), (UnitClass.Mage, 6, 4, 18), (UnitClass.Rider, 6, 4, 36),
                (UnitClass.Guardian, 0, 0, 2), (UnitClass.Archer, 0, 0, 6),
                (UnitClass.Healer, 1, 1, 5), (UnitClass.Mage, 1, 1, 10),
            };
            foreach (var c in cases)
            {
                var b = new TestBoard();
                var start = Hex.FromDoubled(c.x, c.y);
                int id = b.Unit(c.cls, A, start);
                var moves = Moves(b.Build(), id);
                Assert.That(moves.Count, Is.EqualTo(c.expected), c.cls + " at " + start);
                Assert.That(moves, Is.Unique);
                Assert.That(moves.All(h => Hex.Distance(h, start) <= Def(c.cls).Move && h != start), Is.True);
            }
        }

        [Test]
        public void U07_CannotPassOrStopOnUnitTowerRock()
        {
            var b = new TestBoard().Rock(H(0, -1)).Rock(H(-1, 1)).Tower(A, H(-1, 0));
            int m = b.Unit(UnitClass.Mage, A, H(0, 0));
            b.Unit(UnitClass.Guardian, A, H(1, 0));
            b.Unit(UnitClass.Guardian, B, H(1, -1));
            // Only (0,1) is open; from there the empty cells one step further.
            Assert.That(Moves(b.Build(), m), Is.EquivalentTo(new[] { H(0, 1), H(1, 1), H(-1, 2), H(0, 2) }));
        }

        [Test]
        public void U06_TowerStats_BlocksPassage_NeverMoves()
        {
            var b = new TestBoard().Tower(A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, A, H(0, 0));
            var s = b.Build();
            var tower = s.GetTower(A);
            Assert.That(tower.Health, Is.EqualTo(Catalog.Tower.Health));
            Assert.That(Catalog.Tower.Move, Is.EqualTo(0));
            // The tower cell is never a destination, and no command ever moves the tower.
            Assert.That(Moves(s, g), Does.Not.Contain(H(0, 1)));
            foreach (var cmd in Engine.GetLegalCommands(s, A))
                if (cmd is MoveCommand m) Assert.That(m.Dest, Is.Not.EqualTo(tower.Pos));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetTower(A).Pos, Is.EqualTo(H(0, 1)));
        }

        [Test]
        public void U09_RiderPassesThroughUnitsAndTowers_NotRock()
        {
            var b = new TestBoard().Rock(H(-1, 1)).Rock(H(0, 1)).Tower(A, H(-1, 0)).Tower(B, H(0, -1));
            int r = b.Unit(UnitClass.Rider, A, H(0, 0));
            b.Unit(UnitClass.Guardian, A, H(1, 0));
            b.Unit(UnitClass.Guardian, B, H(1, -1));
            var s = b.Build();
            var moves = Moves(s, r);
            Assert.That(moves, Does.Contain(H(2, 0)));   // through own unit
            Assert.That(moves, Does.Contain(H(2, -2)));  // through enemy unit
            Assert.That(moves, Does.Contain(H(0, -2)));  // through enemy tower
            Assert.That(moves, Does.Contain(H(-2, 0)));  // through own tower
            foreach (var n in new[] { H(1, 0), H(1, -1), H(0, -1), H(-1, 0), H(-1, 1), H(0, 1) })
                Assert.That(moves, Does.Not.Contain(n));

            // Rock blocks: both neighbours of a corner are rock -> no moves.
            var corner = Hex.FromDoubled(0, 0);
            var b2 = new TestBoard().Rock(Hex.FromDoubled(2, 0)).Rock(Hex.FromDoubled(1, 1));
            int r2 = b2.Unit(UnitClass.Rider, A, corner);
            Assert.That(Moves(b2.Build(), r2), Is.Empty);

            // Same corner with a unit instead of one rock: the Rider gets through.
            var b3 = new TestBoard().Rock(Hex.FromDoubled(1, 1));
            int r3 = b3.Unit(UnitClass.Rider, A, corner);
            b3.Unit(UnitClass.Guardian, B, Hex.FromDoubled(2, 0));
            Assert.That(Moves(b3.Build(), r3), Does.Contain(Hex.FromDoubled(4, 0)));
        }

        [Test]
        public void U09_RiderCannotStopOnOccupied()
        {
            var b = new TestBoard().Rock(H(-1, 0));
            int r = b.Unit(UnitClass.Rider, A, H(0, 0));
            b.Unit(UnitClass.Archer, A, H(1, 0));
            b.Unit(UnitClass.Archer, B, H(2, 0));
            var s = b.Build();
            var moves = Moves(s, r);
            Assert.That(moves, Does.Not.Contain(H(1, 0)).And.Not.Contain(H(2, 0)).And.Not.Contain(H(-1, 0)));
            Assert.That(moves, Does.Contain(H(3, 0)));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new MoveCommand(A, r, H(2, 0))));
        }

        [Test]
        public void U10_CanStopOnPortal()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(1, 0));
            var s = b.Build();
            Assert.That(Moves(s, g), Does.Contain(Board.Portal));
            Engine.Apply(s, new MoveCommand(A, g, Board.Portal));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(Board.Portal));
        }

        [Test]
        public void U12_CannotMoveThroughOrIntoNonVisibleCells()
        {
            // With the current Catalog every unit has Move <= Sight, so the mover always sees its whole reach.
            // The rule is checked through the explicit-visibility overload (it matters once C-14 adds Move).
            foreach (var d in Catalog.Units) Assert.That(d.Move, Is.LessThanOrEqualTo(d.Sight), d.Id);

            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(0, 0));
            int r = b.Unit(UnitClass.Rider, A, H(-3, 0));
            var s = b.Build();
            var visible = AllCells();
            visible.Remove(H(1, 0));
            visible.Remove(H(-2, 0));

            var mageMoves = Movement.Destinations(s, s.GetUnit(m), visible);
            Assert.That(mageMoves, Does.Not.Contain(H(1, 0)));  // not into
            Assert.That(mageMoves, Does.Not.Contain(H(2, 0)));  // only reachable through (1,0)
            Assert.That(mageMoves, Does.Contain(H(1, -1)));

            var riderMoves = Movement.Destinations(s, s.GetUnit(r), visible);
            Assert.That(riderMoves, Does.Not.Contain(H(-2, 0)));
            Assert.That(riderMoves, Does.Contain(H(-1, 0))); // around the hidden cell
            Assert.That(Moves(s, m), Does.Contain(H(1, 0))); // the real visibility sees it
        }

        [Test]
        public void U12_HiddenEnemyDoesNotChangeLegalMoves()
        {
            GameState Make(bool withEnemy)
            {
                var b = new TestBoard();
                b.Unit(UnitClass.Rider, A, H(0, 1));
                b.Unit(UnitClass.Archer, A, H(-2, 1));
                if (withEnemy) b.Unit(UnitClass.Guardian, B, H(3, -3));
                return b.Build();
            }
            var with = Make(true);
            Assert.That(Visibility.VisibleCells(with, A).Contains(H(3, -3)), Is.False);
            Assert.That(Engine.GetLegalCommands(with, A), Is.EqualTo(Engine.GetLegalCommands(Make(false), A)));
        }

        // ---------- U-11 Guardian cover ----------

        [Test]
        public void U11_GuardianCoverBlocksAttacksOnAdjacentAllies()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(-1, 0));
            b.Unit(UnitClass.Archer, B, H(1, -1));
            var s0 = b.Build();
            Assert.That(Targets(s0, m), Does.Contain(H(1, -1)));

            b.Unit(UnitClass.Guardian, B, H(0, -1)); // adjacent to (1,-1) and Visible to A (V-11)
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(0, -1)), Is.True);
            Assert.That(Targets(s, m), Is.EqualTo(new[] { H(0, -1) })); // only the Guardian itself
            long before = (long)StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new AttackCommand(A, m, H(1, -1))));
            Assert.That((long)StateHash.Compute(s), Is.EqualTo(before));
        }

        [Test]
        public void U11_GuardianItselfNotCovered()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(-1, 0));
            b.Unit(UnitClass.Archer, B, H(1, -1));
            b.Unit(UnitClass.Guardian, B, H(1, 0)); // covers (1,-1), itself targetable
            Assert.That(Targets(b.Build(), m), Is.EqualTo(new[] { H(1, 0) }));
        }

        [Test]
        public void U11_AdjacentGuardiansDoNotCoverEachOther()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(-1, 0));
            b.Unit(UnitClass.Guardian, B, H(1, -1));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            Assert.That(Targets(b.Build(), m), Is.EquivalentTo(new[] { H(1, -1), H(1, 0) }));
        }

        [Test]
        public void U11_CoverProtectsFriendlyTower()
        {
            var b = new TestBoard().Tower(B, H(2, -2));
            int a = b.Unit(UnitClass.Archer, A, H(-1, -2));
            Assert.That(Targets(b.Build(), a), Is.EqualTo(new[] { H(2, -2) }));
            b.Unit(UnitClass.Guardian, B, H(1, -1)); // adjacent to the tower, Visible (V-11), not on the Archer's lines
            Assert.That(Visibility.VisibleCells(b.Build(), A).Contains(H(1, -1)), Is.True);
            Assert.That(Targets(b.Build(), a), Is.Empty);
        }

        [Test]
        public void U11_HiddenGuardianDoesNotCoverVisibleTarget()
        {
            // Archer (Sight 3) sees the target at distance 3, but not the Guardian behind it at distance 4.
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 0));
            b.Unit(UnitClass.Healer, B, H(0, -3));
            b.Unit(UnitClass.Guardian, B, H(0, -4));
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(0, -4)), Is.False);
            Assert.That(Targets(s, a), Is.EqualTo(new[] { H(0, -3) }));
            Engine.Apply(s, new AttackCommand(A, a, H(0, -3)));
            Assert.That(s.UnitAt(H(0, -3)).Health, Is.EqualTo(3 - 2));

            // Once another unit of A sees the Guardian, its cover applies.
            var b2 = new TestBoard();
            int a2 = b2.Unit(UnitClass.Archer, A, H(0, 0));
            b2.Unit(UnitClass.Guardian, A, H(-1, -2)); // sees (0,-4) at distance 2
            b2.Unit(UnitClass.Healer, B, H(0, -3));
            b2.Unit(UnitClass.Guardian, B, H(0, -4));
            var s2 = b2.Build();
            Assert.That(Targets(s2, a2), Does.Not.Contain(H(0, -3)));
        }

        [Test]
        public void V11_LegalTargetsIgnoreHiddenUnits()
        {
            GameState Make(bool withHiddenGuardian)
            {
                var b = new TestBoard();
                b.Unit(UnitClass.Archer, A, H(0, 0));
                b.Unit(UnitClass.Mage, A, H(-1, 1));
                b.Unit(UnitClass.Healer, B, H(0, -3));
                if (withHiddenGuardian) b.Unit(UnitClass.Guardian, B, H(0, -4));
                return b.Build();
            }
            var with = Make(true);
            Assert.That(Visibility.VisibleCells(with, A).Contains(H(0, -4)), Is.False);
            Assert.That(Engine.GetLegalCommands(with, A), Is.EqualTo(Engine.GetLegalCommands(Make(false), A)));
        }

        [Test]
        public void U11_SplashIgnoresCover()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(-1, 0));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int covered = b.Unit(UnitClass.Archer, B, H(1, -1));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, m, H(1, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - 2));
            Assert.That(s.GetUnit(covered).Health, Is.EqualTo(3 - Catalog.MageSplashDamage));
        }

        // ---------- U-20 damage, U-02 charge ----------

        [Test]
        public void U20_DamageIsAttackPlusBiomeBonus()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(-1, 0), Biome.Forest);
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(1, 0)));
            int expected = Def(UnitClass.Archer).Attack + Catalog.BiomeAttackBonus;
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - expected));
            var dmg = ev.OfType<DamageDealt>().Single();
            Assert.That(dmg.Amount, Is.EqualTo(expected));
            Assert.That(dmg.Kind, Is.EqualTo(DamageKind.Attack));
            Assert.That(dmg.SourceUnitId, Is.EqualTo(a));
            Assert.That(dmg.TargetUnitId, Is.EqualTo(g));
        }

        [Test]
        public void U20_BiomeBonusOnlyOnOwnBiomeTile()
        {
            // Forest Archer standing on Desert; the target stands on its own biome (irrelevant).
            var b = new TestBoard().Biome(H(-1, 0), Biome.Desert);
            int a = b.Unit(UnitClass.Archer, A, H(-1, 0), Biome.Forest);
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0), Biome.Forest);
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, a, H(1, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Def(UnitClass.Archer).Attack));
        }

        [Test]
        public void U20_DamageNeverBelowZero()
        {
            // No debuffs exist before M3; check the formula directly.
            Assert.That(Combat.Damage(2, 0, 5), Is.EqualTo(0));
            Assert.That(Combat.Damage(2, 1, 0), Is.EqualTo(3));
            Assert.That(Combat.Damage(1, 1, 2), Is.EqualTo(0));
        }

        [Test]
        public void U02_RiderChargeBonusIfMovedLastOwnTurn()
        {
            var b = new TestBoard();
            int r = b.Unit(UnitClass.Rider, A, H(-3, 0));
            int g = b.Unit(UnitClass.Guardian, B, H(0, 0));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, r, H(-1, 0)));
            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(r).MovedLastOwnTurn, Is.True);
            Engine.Apply(s, new AttackCommand(A, r, H(0, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - (Def(UnitClass.Rider).Attack + Catalog.RiderChargeBonus)));

            // Did not move in the previous own turn -> no charge.
            var b2 = new TestBoard();
            int r2 = b2.Unit(UnitClass.Rider, A, H(-1, 0));
            int g2 = b2.Unit(UnitClass.Guardian, B, H(0, 0));
            var s2 = b2.Build();
            Engine.Apply(s2, new EndTurnCommand(A));
            Engine.Apply(s2, new EndTurnCommand(B));
            Engine.Apply(s2, new AttackCommand(A, r2, H(0, 0)));
            Assert.That(s2.GetUnit(g2).Health, Is.EqualTo(6 - Def(UnitClass.Rider).Attack));

            // Moved two own turns ago, not in the last one -> no charge.
            var b3 = new TestBoard();
            int r3 = b3.Unit(UnitClass.Rider, A, H(-3, 0));
            int g3 = b3.Unit(UnitClass.Guardian, B, H(0, 0));
            var s3 = b3.Build();
            Engine.Apply(s3, new MoveCommand(A, r3, H(-1, 0)));
            for (int i = 0; i < 4; i++) Engine.Apply(s3, new EndTurnCommand(s3.ActivePlayer));
            Assert.That(s3.GetUnit(r3).MovedLastOwnTurn, Is.False);
            Engine.Apply(s3, new AttackCommand(A, r3, H(0, 0)));
            Assert.That(s3.GetUnit(g3).Health, Is.EqualTo(6 - Def(UnitClass.Rider).Attack));
        }

        [Test]
        public void U02_NoChargeOnOverwatchShot()
        {
            var b = new TestBoard();
            int r = b.Unit(UnitClass.Rider, B, H(0, 0));
            b.U(r).OnOverwatch = true;
            b.U(r).MovedLastOwnTurn = true; // forced: even then, overwatch shots get no charge
            int g = b.Unit(UnitClass.Guardian, A, H(-2, 0));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(-1, 0)));
            Assert.That(ev.OfType<OverwatchFired>().Count(), Is.EqualTo(1));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Def(UnitClass.Rider).Attack));
        }

        // ---------- U-03 / U-25 Archer ----------

        [Test]
        public void U03_ArcherHitsStraightLine1to3()
        {
            var def = Def(UnitClass.Archer);
            var expected = new List<Hex>();
            for (int d = 0; d < 6; d++)
                for (int k = 1; k <= 3; k++)
                    expected.Add(H(Hex.Directions[d].Q * k, Hex.Directions[d].R * k));
            var inRange = Board.Cells.Where(c => Combat.InRange(def, Board.Portal, c)).ToList();
            Assert.That(inRange, Is.EquivalentTo(expected)); // all 18 are on the board from the Portal

            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 0));
            int far = b.Unit(UnitClass.Guardian, B, H(0, -3));
            var s = b.Build();
            Assert.That(Targets(s, a), Is.EqualTo(new[] { H(0, -3) }));
            Engine.Apply(s, new AttackCommand(A, a, H(0, -3)));
            Assert.That(s.GetUnit(far).Health, Is.EqualTo(6 - def.Attack));
        }

        [Test]
        public void U03_LineNotBlockedByUnitsTowersOrRocks()
        {
            var b = new TestBoard().Rock(H(0, -2)).Tower(B, H(2, -2));
            int a = b.Unit(UnitClass.Archer, A, H(-1, -2));
            b.Unit(UnitClass.Archer, B, H(1, -2));
            // Another line: own unit at k=1, enemy at k=3.
            b.Unit(UnitClass.Healer, A, H(-1, -1));
            b.Unit(UnitClass.Healer, B, H(-1, 1));
            var s = b.Build();
            Assert.That(Targets(s, a), Is.EquivalentTo(new[] { H(1, -2), H(2, -2), H(-1, 1) }));
        }

        [Test]
        public void U03_ArcherCannotHitOffLine()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(1, 1));   // distance 2, off line
            b.Unit(UnitClass.Guardian, B, H(2, -1));  // distance 2, off line
            b.Unit(UnitClass.Guardian, B, H(2, 1));   // distance 3, off line
            b.Unit(UnitClass.Guardian, B, H(0, 2));   // on line
            var s = b.Build();
            Assert.That(Targets(s, a), Is.EqualTo(new[] { H(0, 2) }));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new AttackCommand(A, a, H(1, 1))));
        }

        [Test]
        public void U25_ArcherCanHitAdjacent()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 0));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Assert.That(Targets(s, a), Is.EqualTo(new[] { H(1, 0) }));
            Engine.Apply(s, new AttackCommand(A, a, H(1, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Def(UnitClass.Archer).Attack));
        }

        // ---------- U-04 Mage splash ----------

        [Test]
        public void U04_MageRange1to2_SplashHitsAdjacentEnemiesAndTower_NotAllies()
        {
            var b = new TestBoard().Tower(B, H(2, -2));
            int m = b.Unit(UnitClass.Mage, A, H(-1, 0));
            int target = b.Unit(UnitClass.Guardian, B, H(1, -1));
            int e1 = b.Unit(UnitClass.Archer, B, H(2, -1));
            int e2 = b.Unit(UnitClass.Healer, B, H(1, 0));
            int ally = b.Unit(UnitClass.Archer, A, H(0, -1));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, m, H(1, -1)));

            Assert.That(s.GetUnit(target).Health, Is.EqualTo(6 - Def(UnitClass.Mage).Attack));
            Assert.That(s.GetUnit(e1).Health, Is.EqualTo(3 - Catalog.MageSplashDamage));
            Assert.That(s.GetUnit(e2).Health, Is.EqualTo(3 - Catalog.MageSplashDamage));
            Assert.That(s.GetTower(B).Health, Is.EqualTo(Catalog.Tower.Health - Catalog.MageSplashDamage));
            Assert.That(s.GetUnit(ally).Health, Is.EqualTo(3));
            Assert.That(ev.OfType<DamageDealt>().Count(d => d.Kind == DamageKind.Splash), Is.EqualTo(3));
            Assert.That(ev.OfType<DamageDealt>().First().Kind, Is.EqualTo(DamageKind.Attack));
        }

        // ---------- U-05 Healer, U-24 cap ----------

        [Test]
        public void U05_HealerHealsAdjacentAlliesAtOwnTurnStart_NotTower()
        {
            var b = new TestBoard().Active(B).Tower(A, H(-1, 1), 5);
            b.Unit(UnitClass.Healer, A, H(0, 0));
            int g = b.Unit(UnitClass.Guardian, A, H(1, 0), health: 3);
            int far = b.Unit(UnitClass.Guardian, A, H(-3, 0), health: 3);
            int enemy = b.Unit(UnitClass.Archer, B, H(0, -1), health: 1);
            var s = b.Build();

            var ev = Engine.Apply(s, new EndTurnCommand(B)); // A's turn starts
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(3 + Catalog.HealerHealAmount));
            Assert.That(s.GetUnit(far).Health, Is.EqualTo(3));
            Assert.That(s.GetUnit(enemy).Health, Is.EqualTo(1));
            Assert.That(s.GetTower(A).Health, Is.EqualTo(5));
            var heal = ev.OfType<UnitHealed>().Single();
            Assert.That(heal.UnitId, Is.EqualTo(g));
            Assert.That(heal.Amount, Is.EqualTo(Catalog.HealerHealAmount));

            // B's turn start: A's Healer does nothing.
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(enemy).Health, Is.EqualTo(1));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(3 + Catalog.HealerHealAmount));
        }

        [Test]
        public void U05_TwoHealersStack_HealersHealEachOther_NotSelf_NotTower()
        {
            var b = new TestBoard().Active(B).Tower(A, H(-1, 1), 5);
            int h1 = b.Unit(UnitClass.Healer, A, H(0, 0), health: 1);
            int h2 = b.Unit(UnitClass.Healer, A, H(1, 0), health: 1);
            int g = b.Unit(UnitClass.Guardian, A, H(1, -1), health: 2); // adjacent to both
            int alone = b.Unit(UnitClass.Healer, A, H(-3, 0), health: 1);
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(2 + 2 * Catalog.HealerHealAmount));
            Assert.That(s.GetUnit(h1).Health, Is.EqualTo(1 + Catalog.HealerHealAmount));
            Assert.That(s.GetUnit(h2).Health, Is.EqualTo(1 + Catalog.HealerHealAmount));
            Assert.That(s.GetUnit(alone).Health, Is.EqualTo(1));
            Assert.That(s.GetTower(A).Health, Is.EqualTo(5));
        }

        [Test]
        public void U24_HealCappedAtMaxHealth()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Healer, A, H(0, 0));
            b.Unit(UnitClass.Healer, A, H(1, 0));
            int full = b.Unit(UnitClass.Guardian, A, H(0, -1)); // adjacent to (0,0) only
            int almost = b.Unit(UnitClass.Guardian, A, H(1, -1), health: 5); // adjacent to both
            var s = b.Build();
            var ev = Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(full).Health, Is.EqualTo(Def(UnitClass.Guardian).Health));
            Assert.That(s.GetUnit(almost).Health, Is.EqualTo(Def(UnitClass.Guardian).Health));
            Assert.That(ev.OfType<UnitHealed>().Where(e => e.UnitId == full), Is.Empty);
            Assert.That(ev.OfType<UnitHealed>().Single(e => e.UnitId == almost).Amount, Is.EqualTo(1));
        }

        // ---------- U-21…U-23, U-26 ----------

        [Test]
        public void U21_NoCounterAttack()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Guardian, A, H(0, 0));
            int d = b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(1, 0)));
            Assert.That(s.GetUnit(a).Health, Is.EqualTo(6));
            Assert.That(s.GetUnit(d).Health, Is.EqualTo(4));
            Assert.That(ev.OfType<DamageDealt>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void U22_DeadUnitRemoved()
        {
            var b = new TestBoard();
            int r = b.Unit(UnitClass.Rider, A, H(0, 0));
            int g = b.Unit(UnitClass.Guardian, A, H(2, -1));
            int victim = b.Unit(UnitClass.Archer, B, H(1, 0));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(A, r, H(1, 0)));
            Assert.That(s.GetUnit(victim), Is.Null);
            Assert.That(s.UnitAt(H(1, 0)), Is.Null);
            Assert.That(s.Units.Count, Is.EqualTo(2));
            Assert.That(Moves(s, g), Does.Contain(H(1, 0))); // the cell is free again
        }

        [Test]
        public void U23_UnitDiedEventRecordsKiller()
        {
            var b = new TestBoard();
            int r = b.Unit(UnitClass.Rider, A, H(0, 0));
            int victim = b.Unit(UnitClass.Archer, B, H(1, 0));
            var s = b.Build();
            var died = Engine.Apply(s, new AttackCommand(A, r, H(1, 0))).OfType<UnitDied>().Single();
            Assert.That(died.UnitId, Is.EqualTo(victim));
            Assert.That(died.Owner, Is.EqualTo(B));
            Assert.That(died.Killer, Is.EqualTo(A));

            // Killed by a tower shot: the killer is the tower owner.
            var b2 = new TestBoard().Tower(B, H(2, -2));
            int m = b2.Unit(UnitClass.Mage, A, H(-1, 0), health: 2);
            var s2 = b2.Build();
            var died2 = Engine.Apply(s2, new MoveCommand(A, m, H(0, -1))).OfType<UnitDied>().Single();
            Assert.That(died2.UnitId, Is.EqualTo(m));
            Assert.That(died2.Owner, Is.EqualTo(A));
            Assert.That(died2.Killer, Is.EqualTo(B));
        }

        [Test]
        public void U26_UnitsCanAttackEnemyTower_CoverAndVisibilityApply()
        {
            var b = new TestBoard().Tower(B, H(2, -2)).Tower(A, H(-1, -1));
            int a = b.Unit(UnitClass.Archer, A, H(-1, -2));
            var s = b.Build();
            Assert.That(Targets(s, a), Is.EqualTo(new[] { H(2, -2) })); // never the own tower at (-1,-1)
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(2, -2)));
            Assert.That(s.GetTower(B).Health, Is.EqualTo(Catalog.Tower.Health - Def(UnitClass.Archer).Attack));
            Assert.That(ev.OfType<DamageDealt>().Single().TargetUnitId, Is.EqualTo(DamageDealt.Tower));

            // Visibility: a tower that is not Visible cannot be targeted.
            var b2 = new TestBoard().Tower(B, H(2, -2));
            int a2 = b2.Unit(UnitClass.Archer, A, H(-1, -2));
            var s2 = b2.Build();
            var visible = AllCells();
            visible.Remove(H(2, -2));
            Assert.That(Combat.LegalTargets(s2, s2.GetUnit(a2), visible), Is.Empty);

            // Cover: a Guardian next to the tower, Visible to the Archer (V-11) and not on its lines.
            b2.Unit(UnitClass.Guardian, B, H(1, -1));
            Assert.That(Visibility.VisibleCells(s2, A).Contains(H(1, -1)), Is.True);
            Assert.That(Targets(s2, a2), Is.Empty);
        }
    }
}
