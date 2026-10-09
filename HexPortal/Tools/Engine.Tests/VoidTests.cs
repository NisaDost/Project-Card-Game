using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD B-25 (v3.3): Void cells left out by a map template are not drawn, not passable, never targeted and never count:
    // movement (U-07, U-09), sight and fog (V-02, V-04), Control Zone (C-02), cards and traps, map events (E-10…E-12).
    public class VoidTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void B25_VoidIsImpassable_ForNormalUnitsAndRiders()
        {
            var b = new TestBoard().Void(H(1, 1)).Void(H(2, 1));
            int g = b.Unit(UnitClass.Archer, A, H(0, 1)); // Move 2
            int r = b.Unit(UnitClass.Rider, A, H(1, 2));  // Move 3, passes units but not Void
            var s = b.Build();
            var archer = Movement.Destinations(s, s.GetUnit(g));
            Assert.That(archer, Does.Not.Contain(H(1, 1)));
            Assert.That(archer, Does.Not.Contain(H(2, 1)));
            Assert.That(Movement.Destinations(s, s.GetUnit(r)), Does.Not.Contain(H(1, 1)).And.Not.Contain(H(2, 1)));
            Assert.That(Movement.IsEmpty(s, H(1, 1)), Is.False);

            // A Rider walled in by Void (and nothing else) cannot cross it.
            var b2 = new TestBoard();
            int rider = b2.Unit(UnitClass.Rider, A, H(0, 2));
            foreach (var d in Hex.Directions) b2.Void(H(0, 2) + d);
            var s2 = b2.Build();
            Assert.That(Movement.Destinations(s2, s2.GetUnit(rider)), Is.Empty);
        }

        [Test]
        public void B25_V04_VoidIsNeverVisible_HiddenForBoth_ShapeOnlyInPlayerView()
        {
            var b = new TestBoard().Void(H(1, 1)).Void(H(-1, -1));
            b.Unit(UnitClass.Archer, A, H(0, 1)); // Sight 3 covers both
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A), Does.Not.Contain(H(1, 1)).And.Not.Contain(H(-1, -1)));
            // V-04 (v4.0): the v3.3 exception is removed; Void is never seen, so it stays Hidden.
            foreach (var p in new[] { A, B })
                foreach (var h in new[] { H(1, 1), H(-1, -1) })
                    Assert.That(s.GetFog(p).Get(h), Is.EqualTo(CellVisibility.Hidden), p + " " + h);

            foreach (var p in new[] { A, B })
            {
                var view = PlayerView.For(s, p);
                var cell = view.Cells[Board.IndexOf(H(1, 1))];
                Assert.That(cell.Visibility, Is.EqualTo(CellVisibility.Hidden));
                Assert.That(cell.TerrainKnown, Is.False);
                Assert.That(cell.Unit, Is.Null);
                Assert.That(view.PlayableCells, Does.Not.Contain(H(1, 1)).And.Not.Contain(H(-1, -1)));
                Assert.That(view.PlayableCells.Count, Is.EqualTo(Board.Cells.Count - 2));
            }
        }

        [Test]
        public void B25_VoidIsOutsideTheControlZone_NoDeployNoTrapNoTeleport()
        {
            var b = new TestBoard().Mana(A, 6).Void(H(1, 1));
            int archer = b.Unit(UnitClass.Archer, A, H(0, 1));
            int card = b.Hand(A, "U-03-Forest");
            int trap = b.Hand(A, "C-20");
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            Assert.That(ControlZone.Cells(s, A), Does.Not.Contain(H(1, 1)));
            Assert.That(PlayerView.For(s, A).ControlZone, Does.Not.Contain(H(1, 1)));
            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal.OfType<DeployCommand>().Where(c => c.CardId == card).Select(c => c.Cell), Does.Not.Contain(H(1, 1)));
            Assert.That(legal.OfType<PlayCardCommand>().Where(c => c.CardId == trap).Select(c => c.Target), Does.Not.Contain(H(1, 1)));
            Assert.That(legal.OfType<PlayCardCommand>().Where(c => c.CardId == tp).Select(c => c.Dest), Does.Not.Contain((Hex?)H(1, 1)));
            Assert.That(legal.OfType<PlayCardCommand>().Any(c => c.CardId == trap), Is.True); // other cells stay legal
        }

        [Test]
        public void B25_C18_PushStopsBeforeVoid()
        {
            var b = new TestBoard().Mana(A, 1).Void(H(3, 0));
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(2, 0)));
        }

        [Test]
        public void B25_E10_E11_E12_VoidIsNeverAnEventTarget()
        {
            var b = new TestBoard();
            // Void every upper non-home cell at distance >= 3 except one pair, plus its mirrors.
            var keep = H(-3, -1);
            foreach (var h in Board.Cells.Where(h => Board.IsInUpperHalf(h) && h != keep && h.R > -6))
            {
                b.Void(h);
                b.Void(h.Mirror());
            }
            var s = b.Build();
            foreach (var def in Catalog.MapEvents)
            {
                var cand = MapEvents.Candidates(s, def);
                Assert.That(cand.All(h => s.Map.IsPlayable(h)), Is.True, def.Id);
                if (def.Id == "E-11") // E-11 centers may lie in the (kept) home rows; never on Void
                    Assert.That(cand, Is.EqualTo(Board.Cells.Where(h => h == keep || (Board.IsInUpperHalf(h) && h.R <= -6))), def.Id);
                else
                    Assert.That(cand, Is.EqualTo(new[] { keep }), def.Id);
            }
            // E-11: the cluster around the kept center skips its Void neighbours.
            var cells = MapEvents.ClusterCells(s, keep);
            Assert.That(cells, Is.EqualTo(new[] { keep }));
        }
    }
}
