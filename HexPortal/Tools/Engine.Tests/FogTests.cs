using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // GDD §11 fog memory: V-01…V-06, V-08 (v2.8 V1: a revealed unit stays Visible while it moves; V2: ghosts).
    public class FogTests
    {
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void V04_OpeningOwnHalfExplored_OpponentHalfHidden_PortalVisible()
        {
            var s = StartedMatch(21);
            foreach (var p in Players)
            {
                var fog = s.GetFog(p);
                var visible = Visibility.VisibleCells(s, p);
                foreach (var h in Board.Cells)
                {
                    var v = fog.Get(h);
                    if (visible.Contains(h)) Assert.That(v, Is.EqualTo(CellVisibility.Visible), h.ToString());
                    else if (Board.IsInHalf(h, p)) Assert.That(v, Is.EqualTo(CellVisibility.Explored), h.ToString());
                    else Assert.That(v, Is.EqualTo(CellVisibility.Hidden), h.ToString());
                }
                Assert.That(fog.Get(Board.Portal), Is.EqualTo(CellVisibility.Visible));
                // Own half terrain is known; enemy units and tower are never shown at the start.
                Assert.That(Board.Cells.Where(h => Board.IsInHalf(h, p)).All(h => fog.GetLastSeen(h).Tile.Equals(s.Map.Get(h))), Is.True);
                var view = PlayerView.For(s, p);
                Assert.That(view.EnemyUnits, Is.Empty);
                Assert.That(view.EnemyTowerPos, Is.Null);
                Assert.That(view.Cells.Where(c => c.Unit != null && c.Unit.Owner != p), Is.Empty);
            }
        }

        [Test]
        public void V01_HiddenThenVisibleThenExplored()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            var s = b.Build();
            var fog = s.GetFog(A);
            Assert.That(fog.Get(H(-1, -1)), Is.EqualTo(CellVisibility.Visible)); // distance 2, B's half
            Assert.That(fog.Get(H(2, -3)), Is.EqualTo(CellVisibility.Hidden));
            Engine.Apply(s, new MoveCommand(A, g, H(-1, 2)));
            Assert.That(s.GetFog(A).Get(H(-1, -1)), Is.EqualTo(CellVisibility.Explored)); // V-02: stays explored
            Assert.That(s.GetFog(A).Get(H(2, -3)), Is.EqualTo(CellVisibility.Hidden));
        }

        [Test]
        public void V03_CellLeavingSightKeepsItsLastSeenState()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int e = b.Unit(UnitClass.Guardian, B, H(-1, -1), health: 5);
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, g, H(-1, 2)));
            var seen = s.GetFog(A).GetLastSeen(H(-1, -1));
            Assert.That((seen.UnitId, seen.UnitOwner, seen.UnitClass, seen.UnitHealth), Is.EqualTo((e, B, UnitClass.Guardian, 5)));

            // B moves the unit away in the fog: A's memory is stale on purpose.
            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new MoveCommand(B, e, H(0, -2)));
            Assert.That(s.GetFog(A).GetLastSeen(H(-1, -1)).UnitId, Is.EqualTo(e));
            var ghost = PlayerView.For(s, A).Cells.Single(c => c.Cell == H(-1, -1));
            Assert.That(ghost.Visibility, Is.EqualTo(CellVisibility.Explored));
            Assert.That(ghost.Unit.IsGhost, Is.True);
            Assert.That((ghost.Unit.Id, ghost.Unit.Health), Is.EqualTo((e, 5)));
            Assert.That(PlayerView.For(s, A).EnemyUnits, Is.Empty);
        }

        [Test]
        public void V05_GhostsShowClassBiomeOwnerHealth_NoEffectsNoOverwatch()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int e = b.Unit(UnitClass.Archer, B, H(-1, -1), Biome.Snow);
            b.Effect(e, "C-10").Effect(e, "C-16");
            b.U(e).OnOverwatch = true;
            var s = b.Build();
            var live = PlayerView.For(s, A).EnemyUnits.Single();
            Assert.That((live.BuffId, live.DebuffId, live.OnOverwatch, live.IsGhost), Is.EqualTo(("C-10", "C-16", true, false)));

            Engine.Apply(s, new MoveCommand(A, g, H(-2, 2))); // distance 3, not on the Archer's lines (no overwatch shot)
            var ghost = PlayerView.For(s, A).Cells.Single(c => c.Cell == H(-1, -1)).Unit;
            Assert.That((ghost.Owner, ghost.Class, ghost.Biome, ghost.Health), Is.EqualTo((B, UnitClass.Archer, Biome.Snow, 3)));
            Assert.That((ghost.BuffId, ghost.DebuffId, ghost.OnOverwatch, ghost.IsGhost), Is.EqualTo(((string)null, (string)null, false, true)));
        }

        [Test]
        public void V05_OwnUnitsAreNeverGhosts()
        {
            // A Mage with Wind Step walks 4 cells through the Archer's sight; its start cell then leaves every own sight.
            var b = new TestBoard().Tower(B, H(-1, -3));
            int m = b.Unit(UnitClass.Mage, A, H(2, 1));
            b.Unit(UnitClass.Archer, A, H(1, -4));
            b.U(m).MoveBonus = 2;
            var s = b.Build();
            var dest = H(1, -2);
            Assert.That(Movement.Destinations(s, s.GetUnit(m)), Does.Contain(dest));
            Engine.Apply(s, new MoveCommand(A, m, dest));
            Assert.That(s.GetFog(A).Get(H(2, 1)), Is.EqualTo(CellVisibility.Explored));
            var view = PlayerView.For(s, A);
            Assert.That(view.Cells.Where(c => c.Unit != null && c.Unit.Id == m).Select(c => c.Cell), Is.EqualTo(new[] { dest }));
            Assert.That(view.OwnUnits.Single(u => u.Id == m).IsGhost, Is.False);
        }

        [Test]
        public void V06_PortalAndItsUnitAlwaysVisibleToBoth()
        {
            var b = new TestBoard();
            int e = b.Unit(UnitClass.Healer, B, Board.Portal);
            var s = b.Build();
            foreach (var p in Players) Assert.That(s.GetFog(p).Get(Board.Portal), Is.EqualTo(CellVisibility.Visible));
            Assert.That(PlayerView.For(s, A).EnemyUnits.Single().Id, Is.EqualTo(e));
        }

        [Test]
        public void V08_OwnTurnAttackRevealsUntilEndOfOpponentsNextTurn()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int archer = b.Unit(UnitClass.Archer, B, H(-1, -2)); // distance 3: out of the Guardian's sight
            var s = b.Build();
            var cell = H(-1, -2);
            Assert.That(Visibility.VisibleCells(s, A).Contains(cell), Is.False);

            var ev = Engine.Apply(s, new AttackCommand(B, archer, H(-1, 1)));
            Assert.That(Visibility.VisibleCells(s, A).Contains(cell), Is.True);
            Assert.That(s.GetFog(A).Get(cell), Is.EqualTo(CellVisibility.Visible));
            Assert.That(PlayerView.For(s, A).EnemyUnits.Single().Id, Is.EqualTo(archer));
            var revealed = EventFilter.For(ev, A).OfType<Revealed>().Single();
            Assert.That((revealed.Owner, revealed.UnitId, revealed.Cell), Is.EqualTo((B, archer, cell)));
            Assert.That(EventFilter.For(ev, B).OfType<Revealed>(), Is.Empty);

            Engine.Apply(s, new EndTurnCommand(B)); // A's turn: still revealed, so a valid target (V-07)
            Assert.That(Visibility.VisibleCells(s, A).Contains(cell), Is.True);
            Assert.That(Combat.IsValidTarget(s, A, cell, Visibility.VisibleCells(s, A)), Is.True);
            Engine.Apply(s, new EndTurnCommand(A)); // end of A's next turn: over
            Assert.That(Visibility.VisibleCells(s, A).Contains(cell), Is.False);
            Assert.That(s.GetFog(A).Get(cell), Is.EqualTo(CellVisibility.Explored));
            Assert.That(s.GetFog(A).GetLastSeen(cell).UnitId, Is.EqualTo(archer));
        }

        [Test]
        public void V08_RevealedUnitStaysVisibleWhileItMoves()
        {
            var b = new TestBoard().Active(B).Mana(B, 3);
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int archer = b.Unit(UnitClass.Archer, B, H(-1, -2));
            int teleport = b.Hand(B, "C-15");
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(B, archer, H(-1, 1)));
            var dest = H(2, -3); // next to B's tower, distance 4 from A's Guardian
            var ev = Engine.Apply(s, new PlayCardCommand(B, teleport, H(-1, -2), dest));
            Assert.That(Visibility.VisibleCells(s, A).Contains(dest), Is.True);
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(-1, -2)), Is.False);
            var moved = EventFilter.For(ev, A).OfType<UnitTeleported>().Single();
            Assert.That((moved.From, moved.To), Is.EqualTo((H(-1, -2), dest)));
            var view = PlayerView.For(s, A);
            Assert.That(view.EnemyUnits.Single().Pos, Is.EqualTo(dest));
            // No second (ghost) copy of the same unit at the old cell.
            Assert.That(view.Cells.Count(c => c.Unit != null && c.Unit.Id == archer), Is.EqualTo(1));

            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(Visibility.VisibleCells(s, A).Contains(dest), Is.True);
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(Visibility.VisibleCells(s, A).Contains(dest), Is.False);
            Assert.That(s.GetFog(A).GetLastSeen(dest).UnitId, Is.EqualTo(archer));
        }

        [Test]
        public void V08_OffTurnOverwatchRevealsOnlyUntilEndOfThatTurn()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 2));
            int archer = b.Unit(UnitClass.Archer, B, H(-1, -2));
            b.U(archer).OnOverwatch = true;
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(-1, 1))); // distance 3 on a straight line
            Assert.That(ev.OfType<OverwatchFired>().Count(), Is.EqualTo(1));
            Assert.That(EventFilter.For(ev, A).OfType<Revealed>().Single().UnitId, Is.EqualTo(archer));
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(-1, -2)), Is.True);
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(-1, -2)), Is.False);
        }

        [Test]
        public void V08_TowerShotRevealsTheTowerUntilEndOfThatTurn()
        {
            var b = new TestBoard().Tower(B, H(0, -2));
            int g = b.Unit(UnitClass.Healer, A, H(0, 1), health: 2); // the shot kills it: A loses its sight there
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, g, H(0, 0)));
            Assert.That(s.GetUnit(g), Is.Null);
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(0, -2)), Is.True);
            Assert.That(PlayerView.For(s, A).EnemyTowerPos, Is.EqualTo(H(0, -2)));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(0, -2)), Is.False);
            var view = PlayerView.For(s, A);
            Assert.That(view.EnemyTowerPos, Is.Null);
            var ghost = view.Cells.Single(c => c.Cell == H(0, -2));
            Assert.That((ghost.HasTower, ghost.TowerIsGhost, ghost.TowerOwner), Is.EqualTo((true, true, B)));
        }

        [Test]
        public void V08_MageSplashDoesNotRevealHitUnits()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(-1, 1));
            b.Unit(UnitClass.Guardian, B, H(-1, -1));
            int hidden = b.Unit(UnitClass.Healer, B, H(0, -2)); // next to the target, distance 3 from the Mage
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, m, H(-1, -1)));
            Assert.That(ev.OfType<DamageDealt>().Any(d => d.TargetUnitId == hidden), Is.True);
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(0, -2)), Is.False);
            Assert.That(s.GetFog(A).Get(H(0, -2)), Is.Not.EqualTo(CellVisibility.Visible));
            Assert.That(PlayerView.For(s, A).EnemyUnits.Select(u => u.Id), Does.Not.Contain(hidden));
            Assert.That(EventFilter.For(ev, A).SelectMany(UnitIds), Does.Not.Contain(hidden));
            Assert.That(s.GetUnit(m).RevealedUntilTurn, Is.EqualTo(s.TurnIndex + 1)); // the attacker is revealed
        }

        [Test]
        public void V03_MemoryMatchesLiveVisibilityAfterCommands()
        {
            var s = StartedMatch(22);
            var rng = new Rng(5);
            for (int i = 0; i < 200 && !s.IsOver; i++)
            {
                Engine.Apply(s, NextRandom(s, rng));
                foreach (var p in Players)
                {
                    var visible = Visibility.VisibleCells(s, p);
                    foreach (var h in Board.Cells)
                        Assert.That(s.GetFog(p).Get(h) == CellVisibility.Visible, Is.EqualTo(visible.Contains(h)), "step " + i + " " + p + " " + h);
                }
            }
        }
    }
}
