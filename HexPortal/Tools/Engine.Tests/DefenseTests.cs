using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §3.3 off-turn defense: tower shot (U-27), overwatch (U-28), trigger order (U-29).
    // Most scenarios put B's tower at (2,-2); its range 1–2 covers (0,-1), (0,-2), (1,-1), (1,-2), ...
    public class DefenseTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static readonly Hex TowerB = new Hex(2, -2);
        static Hex H(int q, int r) => new Hex(q, r);
        static UnitDef Def(UnitClass c) => Catalog.Units.Single(u => u.Class == c);

        // ---------- U-27 tower shot ----------

        [Test]
        public void U27_TowerShootsEnemyEndingMoveInRange()
        {
            var b = new TestBoard().Tower(B, TowerB);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0)); // distance 3
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(0, -1))); // distance 2
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Catalog.Tower.Attack));
            var shot = ev.OfType<TowerShot>().Single();
            Assert.That(shot.Owner, Is.EqualTo(B));
            Assert.That(shot.TargetUnitId, Is.EqualTo(g));
            var dmg = ev.OfType<DamageDealt>().Single();
            Assert.That(dmg.Kind, Is.EqualTo(DamageKind.TowerShot));
            Assert.That(dmg.SourceUnitId, Is.EqualTo(DamageDealt.Tower));
            Assert.That(s.IsTowerShotAvailable(B), Is.False);
            Assert.That(ev[0], Is.InstanceOf<UnitMoved>());
        }

        [Test]
        public void U27_TowerShootsEnemyAttackingFromRange()
        {
            var b = new TestBoard().Tower(B, TowerB);
            int a = b.Unit(UnitClass.Archer, A, H(0, -1)); // distance 2 from the tower
            int g = b.Unit(UnitClass.Guardian, B, H(-1, -1));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(-1, -1)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Def(UnitClass.Archer).Attack));
            Assert.That(s.GetUnit(a).Health, Is.EqualTo(3 - Catalog.Tower.Attack));
            var kinds = ev.OfType<DamageDealt>().Select(d => d.Kind).ToList();
            Assert.That(kinds, Is.EqualTo(new[] { DamageKind.Attack, DamageKind.TowerShot }));
        }

        [Test]
        public void U27_AtMostOncePerOpponentTurn()
        {
            var b = new TestBoard().Tower(B, TowerB);
            int g1 = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            int g2 = b.Unit(UnitClass.Guardian, A, H(-1, -1));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, g1, H(0, -1)));
            var ev = Engine.Apply(s, new MoveCommand(A, g2, H(0, -2)));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(s.GetUnit(g2).Health, Is.EqualTo(6));

            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.IsTowerShotAvailable(B), Is.True); // reset at the start of A's turn
            ev = Engine.Apply(s, new MoveCommand(A, g2, H(1, -2)));
            Assert.That(ev.OfType<TowerShot>().Count(), Is.EqualTo(1));
            Assert.That(s.GetUnit(g2).Health, Is.EqualTo(6 - Catalog.Tower.Attack));
        }

        [Test]
        public void U27_InvalidTargetKeepsShot()
        {
            // Covered: an Archer ends its move in range next to its Guardian, which the tower sees (V-11).
            var b = new TestBoard().Tower(B, TowerB);
            int g = b.Unit(UnitClass.Guardian, A, H(1, -1));
            int a = b.Unit(UnitClass.Archer, A, H(-2, 0));
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, B).Contains(H(1, -1)), Is.True);
            var ev = Engine.Apply(s, new MoveCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(s.GetUnit(a).Health, Is.EqualTo(3));
            Assert.That(s.IsTowerShotAvailable(B), Is.True);

            // The kept shot fires at the next valid trigger (Guardians are never covered).
            ev = Engine.Apply(s, new MoveCommand(A, g, H(1, -2)));
            Assert.That(ev.OfType<TowerShot>().Single().TargetUnitId, Is.EqualTo(g));

            // Not Visible to the tower owner: never a valid shot target. With the current Catalog the tower's
            // Sight equals its MaxRange, so this is checked through the explicit-visibility overload.
            Assert.That(Catalog.Tower.MaxRange, Is.LessThanOrEqualTo(Catalog.Tower.Sight));
            var b2 = new TestBoard().Tower(B, TowerB);
            b2.Unit(UnitClass.Archer, A, H(0, -1));
            var s2 = b2.Build();
            var none = new HashSet<Hex>();
            var all = new HashSet<Hex>(Board.Cells);
            Assert.That(Combat.IsValidTarget(s2, B, H(0, -1), none), Is.False);
            Assert.That(Combat.IsValidTarget(s2, B, H(0, -1), all), Is.True);
        }

        [Test]
        public void U27_DamageIsTwo_NoBiome()
        {
            // The target stands on its own biome and the tower cell is Forest: the shot is still Tower.Attack.
            var b = new TestBoard().Tower(B, TowerB);
            int r = b.Unit(UnitClass.Rider, A, H(-1, 0), Biome.Forest);
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, r, H(1, -1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Catalog.Tower.Attack));
            Assert.That(Catalog.Tower.Attack, Is.EqualTo(2));
            Assert.That(s.GetUnit(r).Health, Is.EqualTo(4 - 2));
        }

        [Test]
        public void U27_PassThroughDoesNotTrigger()
        {
            // B's tower at doubled (5,1) = (1,-3). From the corner (-1,-4) (distance 3) both exits are in range.
            var tower = H(1, -3);
            var corner = Hex.FromDoubled(0, 0);
            var b = new TestBoard().Tower(B, tower);
            int r = b.Unit(UnitClass.Rider, A, corner);
            var s = b.Build();
            Assert.That(Hex.Distance(corner, tower), Is.EqualTo(3));
            Assert.That(Hex.Distance(corner.Neighbor(0), tower), Is.EqualTo(2));
            Assert.That(Hex.Distance(corner.Neighbor(5), tower), Is.EqualTo(2));
            var dest = H(-2, -1);
            Assert.That(Hex.Distance(dest, tower), Is.EqualTo(3));

            var ev = Engine.Apply(s, new MoveCommand(A, r, dest));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(s.IsTowerShotAvailable(B), Is.True);
        }

        // ---------- U-28 overwatch ----------

        [Test]
        public void U28_OverwatchCostsActionAndEnergy()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new OverwatchCommand(A, a)));
            var ev = Engine.Apply(s, new OverwatchCommand(A, a));
            Assert.That(ev.OfType<OverwatchSet>().Single().UnitId, Is.EqualTo(a));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn - Catalog.EnergyPerAction));
            var u = s.GetUnit(a);
            Assert.That(u.OnOverwatch, Is.True);
            Assert.That(u.ActedThisTurn, Is.True);
            Assert.That(Engine.GetLegalCommands(s, A), Is.EqualTo(new ICommand[] { new EndTurnCommand(A) }));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new AttackCommand(A, a, H(1, 0))));
        }

        [Test]
        public void U28_FiresOnFirstTriggerInRange()
        {
            var b = new TestBoard();
            int ow = b.Unit(UnitClass.Archer, B, H(0, -2));
            b.U(ow).OnOverwatch = true;
            int g1 = b.Unit(UnitClass.Guardian, A, H(-2, 1));
            int g2 = b.Unit(UnitClass.Guardian, A, H(1, 0));
            int g3 = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            var s = b.Build();

            var ev = Engine.Apply(s, new MoveCommand(A, g1, H(-1, 1))); // not on the Archer's lines
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);

            ev = Engine.Apply(s, new MoveCommand(A, g2, H(0, 1))); // line (0,+1), k = 3
            var fired = ev.OfType<OverwatchFired>().Single();
            Assert.That(fired.UnitId, Is.EqualTo(ow));
            Assert.That(fired.TargetUnitId, Is.EqualTo(g2));
            Assert.That(s.GetUnit(g2).Health, Is.EqualTo(6 - Def(UnitClass.Archer).Attack));
            Assert.That(ev.OfType<DamageDealt>().Single().Kind, Is.EqualTo(DamageKind.Overwatch));
            Assert.That(s.GetUnit(ow).OnOverwatch, Is.False);

            ev = Engine.Apply(s, new MoveCommand(A, g3, H(0, 0))); // in line, but overwatch is spent
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s.GetUnit(g3).Health, Is.EqualTo(6));
        }

        [Test]
        public void U28_EndsAfterFiringOrOpponentTurnEnd()
        {
            // Fires -> ends (also covered above). Here: never triggered -> ends when A's turn ends.
            var b = new TestBoard().Active(B);
            int ow = b.Unit(UnitClass.Archer, B, H(0, -2));
            int g = b.Unit(UnitClass.Guardian, A, H(-2, 1));
            var s = b.Build();
            Engine.Apply(s, new OverwatchCommand(B, ow));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(ow).OnOverwatch, Is.True); // lasts through A's turn
            Engine.Apply(s, new MoveCommand(A, g, H(-1, 1)));
            Assert.That(s.GetUnit(ow).OnOverwatch, Is.True);
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(ow).OnOverwatch, Is.False);
            Assert.That(ev.OfType<OverwatchEnded>().Single().UnitId, Is.EqualTo(ow));
        }

        [Test]
        public void U28_MageOverwatchSplashes()
        {
            var b = new TestBoard();
            int mage = b.Unit(UnitClass.Mage, B, H(0, -2));
            b.U(mage).OnOverwatch = true;
            int ally = b.Unit(UnitClass.Archer, B, H(-1, 0)); // next to the target, same side as the Mage
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            int near = b.Unit(UnitClass.Archer, A, H(1, 0));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(0, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Def(UnitClass.Mage).Attack));
            Assert.That(s.GetUnit(near).Health, Is.EqualTo(3 - Catalog.MageSplashDamage));
            Assert.That(s.GetUnit(ally).Health, Is.EqualTo(3));
            Assert.That(ev.OfType<DamageDealt>().Select(d => d.Kind),
                Is.EqualTo(new[] { DamageKind.Overwatch, DamageKind.Splash }));
        }

        [Test]
        public void U28_OverwatchRespectsCover()
        {
            var b = new TestBoard();
            int ow = b.Unit(UnitClass.Archer, B, H(0, -2));
            b.U(ow).OnOverwatch = true;
            int g = b.Unit(UnitClass.Guardian, A, H(1, 0));
            int a = b.Unit(UnitClass.Archer, A, H(-1, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, a, H(0, 0))); // in line, next to its Guardian
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s.GetUnit(ow).OnOverwatch, Is.True);

            ev = Engine.Apply(s, new MoveCommand(A, g, H(0, 1))); // Guardian itself is never covered
            Assert.That(ev.OfType<OverwatchFired>().Single().TargetUnitId, Is.EqualTo(g));
        }

        [Test]
        public void U27_HiddenGuardianDoesNotBlockTowerShot()
        {
            // A's Guardian at (-1,-1) is 3 from B's tower (Sight 2) and B has no units: hidden from B.
            var b = new TestBoard().Tower(B, TowerB);
            b.Unit(UnitClass.Guardian, A, H(-1, -1));
            int m = b.Unit(UnitClass.Mage, A, H(-2, 1));
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, B).Contains(H(-1, -1)), Is.False);
            var ev = Engine.Apply(s, new MoveCommand(A, m, H(0, -1))); // in range, next to the hidden Guardian
            Assert.That(ev.OfType<TowerShot>().Single().TargetUnitId, Is.EqualTo(m));
            Assert.That(s.GetUnit(m).Health, Is.EqualTo(3 - Catalog.Tower.Attack));

            // Control: when a unit of B sees the Guardian, the same move is covered.
            var b2 = new TestBoard().Tower(B, TowerB);
            b2.Unit(UnitClass.Guardian, A, H(-1, -1));
            b2.Unit(UnitClass.Healer, B, H(0, -3)); // sees (-1,-1) at distance 2
            int m2 = b2.Unit(UnitClass.Mage, A, H(-2, 1));
            var s2 = b2.Build();
            Assert.That(Engine.Apply(s2, new MoveCommand(A, m2, H(0, -1))).OfType<TowerShot>(), Is.Empty);
            Assert.That(s2.IsTowerShotAvailable(B), Is.True);
        }

        [Test]
        public void U28_HiddenGuardianDoesNotBlockOverwatch()
        {
            // B's overwatching Archer at (0,-2) (Sight 3) cannot see A's Guardian at (1,1) (distance 4).
            var b = new TestBoard();
            int ow = b.Unit(UnitClass.Archer, B, H(0, -2));
            b.U(ow).OnOverwatch = true;
            b.Unit(UnitClass.Guardian, A, H(1, 1));
            int m = b.Unit(UnitClass.Mage, A, H(-1, 2));
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, B).Contains(H(1, 1)), Is.False);
            var ev = Engine.Apply(s, new MoveCommand(A, m, H(0, 1))); // on the Archer's line at k = 3
            Assert.That(ev.OfType<OverwatchFired>().Single().TargetUnitId, Is.EqualTo(m));

            // Control: when B sees the Guardian, the same move is covered and the overwatch stays.
            var b2 = new TestBoard();
            int ow2 = b2.Unit(UnitClass.Archer, B, H(0, -2));
            b2.U(ow2).OnOverwatch = true;
            b2.Unit(UnitClass.Healer, B, H(1, -1)); // sees (1,1) at distance 2
            b2.Unit(UnitClass.Guardian, A, H(1, 1));
            int m2 = b2.Unit(UnitClass.Mage, A, H(-1, 2));
            var s2 = b2.Build();
            Assert.That(Engine.Apply(s2, new MoveCommand(A, m2, H(0, 1))).OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s2.GetUnit(ow2).OnOverwatch, Is.True);
        }

        // ---------- U-29 order ----------

        [Test]
        public void U29_TowerFiresBeforeOverwatch_ThenByUnitId()
        {
            var b = new TestBoard().Tower(B, TowerB);
            int g = b.Unit(UnitClass.Guardian, A, H(0, -1));
            int bg = b.Unit(UnitClass.Guardian, B, H(2, -1)); // lower id, later in board order
            int bh = b.Unit(UnitClass.Healer, B, H(1, -2));
            b.U(bg).OnOverwatch = true;
            b.U(bh).OnOverwatch = true;
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, -1)));

            var shooters = ev.Where(e => e is TowerShot || e is OverwatchFired)
                .Select(e => e is OverwatchFired f ? f.UnitId : -1).ToList();
            Assert.That(shooters, Is.EqualTo(new[] { -1, bg, bh }));
            int expected = 6 - Catalog.Tower.Attack - Def(UnitClass.Guardian).Attack - Def(UnitClass.Healer).Attack;
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(expected));
        }

        [Test]
        public void U29_TargetDiesRemainingOverwatchStays()
        {
            var b = new TestBoard().Tower(B, TowerB);
            int g = b.Unit(UnitClass.Guardian, A, H(0, -1), health: 2);
            int bg = b.Unit(UnitClass.Guardian, B, H(2, -1));
            int bh = b.Unit(UnitClass.Healer, B, H(1, -2));
            b.U(bg).OnOverwatch = true;
            b.U(bh).OnOverwatch = true;
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, -1)));
            Assert.That(s.GetUnit(g), Is.Null);
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s.GetUnit(bg).OnOverwatch && s.GetUnit(bh).OnOverwatch, Is.True);

            // Without the tower: the first overwatcher kills, the second stays on overwatch.
            var b2 = new TestBoard().Tower(B, TowerB);
            int g2 = b2.Unit(UnitClass.Guardian, A, H(0, -1), health: 2);
            int bg2 = b2.Unit(UnitClass.Guardian, B, H(2, -1));
            int bh2 = b2.Unit(UnitClass.Healer, B, H(1, -2));
            b2.U(bg2).OnOverwatch = true;
            b2.U(bh2).OnOverwatch = true;
            var s2 = b2.Build();
            s2.SetTowerShotAvailable(B, false);
            ev = Engine.Apply(s2, new MoveCommand(A, g2, H(1, -1)));
            Assert.That(s2.GetUnit(g2), Is.Null);
            Assert.That(ev.OfType<OverwatchFired>().Single().UnitId, Is.EqualTo(bg2));
            Assert.That(s2.GetUnit(bg2).OnOverwatch, Is.False);
            Assert.That(s2.GetUnit(bh2).OnOverwatch, Is.True);
        }

        [Test]
        public void U29_AttackTriggeredShotsResolveAfterAttack()
        {
            var b = new TestBoard().Tower(B, TowerB);
            int m = b.Unit(UnitClass.Mage, A, H(0, -1));                         // in tower range
            int target = b.Unit(UnitClass.Archer, B, H(0, -3), health: 2);       // killed by the attack
            int splashed = b.Unit(UnitClass.Healer, B, H(1, -3), health: 1);     // killed by the splash
            int shooter = b.Unit(UnitClass.Healer, B, H(-1, -1));                // alive, adjacent to the Mage
            foreach (var id in new[] { target, splashed, shooter }) b.U(id).OnOverwatch = true;
            var s = b.Build();

            var ev = Engine.Apply(s, new AttackCommand(A, m, H(0, -3)));
            var seq = ev.Where(e => !(e is UnitMoved)).Select(e =>
                e is DamageDealt d ? "Damage:" + d.Kind :
                e is UnitDied u ? "Died:" + u.UnitId :
                e is TowerShot ? "TowerShot" :
                e is OverwatchFired f ? "Fired:" + f.UnitId : e.GetType().Name).ToList();
            Assert.That(seq, Is.EqualTo(new[]
            {
                "Damage:Attack", "Died:" + target, "Damage:Splash", "Died:" + splashed,
                "TowerShot", "Damage:TowerShot", "Fired:" + shooter, "Damage:Overwatch", "Died:" + m,
            }));
            Assert.That(s.GetUnit(m), Is.Null);
        }
    }
}
