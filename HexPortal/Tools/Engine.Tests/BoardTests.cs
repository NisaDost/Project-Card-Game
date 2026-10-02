using System;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §2 board geometry (B-01…B-06, B-21; v3.0 radius-6 hexagon) and the generic hex helpers. Values from the
    // hex-grid skill.
    public class BoardTests
    {
        static Hex H(int q, int r) => new Hex(q, r);

        static bool InHexagon(int q, int r, int radius) =>
            Math.Abs(q) <= radius && Math.Abs(r) <= radius && Math.Abs(q + r) <= radius;

        // ---------- Geometry ----------

        [Test]
        public void B01_BoardIsRadius6Hexagon_127Cells_Rows7To13To7()
        {
            Assert.That(Board.Cells.Count, Is.EqualTo(127));
            Assert.That(Board.Cells.Count, Is.EqualTo(Catalog.CellCount));
            Assert.That(Board.Cells, Is.Unique);

            var perRow = Enumerable.Range(1, 13).Select(row => Board.Cells.Count(h => Board.GddRow(h) == row)).ToArray();
            Assert.That(perRow, Is.EqualTo(new[] { 7, 8, 9, 10, 11, 12, 13, 12, 11, 10, 9, 8, 7 }));
            Assert.That(perRow, Is.EqualTo(Catalog.RowLengths));

            // GddCol runs 1..rowLength within each row, left to right (increasing q).
            for (int row = 1; row <= 13; row++)
                Assert.That(Board.Cells.Where(h => Board.GddRow(h) == row).Select(Board.GddCol),
                    Is.EqualTo(Enumerable.Range(1, perRow[row - 1])), "row " + row);
        }

        [Test]
        public void B01_CellsAreExactlyTheAxialHexagon_OrderedByRThenQ()
        {
            var expected = new List<Hex>();
            for (int r = -6; r <= 6; r++)
                for (int q = -6; q <= 6; q++)
                    if (InHexagon(q, r, 6)) expected.Add(H(q, r));
            Assert.That(expected.Count, Is.EqualTo(127));
            Assert.That(Board.Cells, Is.EqualTo(expected));

            for (int r = -8; r <= 8; r++)
                for (int q = -8; q <= 8; q++)
                    Assert.That(Board.IsOnBoard(H(q, r)), Is.EqualTo(InHexagon(q, r, 6)), H(q, r).ToString());
            for (int i = 0; i < Board.Cells.Count; i++)
                Assert.That(Board.IndexOf(Board.Cells[i]), Is.EqualTo(i));
            Assert.That(Board.IndexOf(H(7, 0)), Is.EqualTo(-1));
            Assert.That(Board.IndexOf(H(4, 3)), Is.EqualTo(-1)); // |q+r| = 7
        }

        [Test]
        public void B02_PortalIsRow7Col7_Axial00()
        {
            Assert.That(Board.Portal, Is.EqualTo(H(0, 0)));
            Assert.That(Board.GddRow(Board.Portal), Is.EqualTo(7));
            Assert.That(Board.GddCol(Board.Portal), Is.EqualTo(7));
        }

        [Test]
        public void B03_HomeZones15Each_ARows12to13_BRows1to2_Distance5ToPortal()
        {
            var a = Board.Cells.Where(h => Board.IsHomeZone(h, PlayerId.A)).ToList();
            var b = Board.Cells.Where(h => Board.IsHomeZone(h, PlayerId.B)).ToList();
            Assert.That(a.Count, Is.EqualTo(15));
            Assert.That(b.Count, Is.EqualTo(15));
            Assert.That(a.Select(Board.GddRow).Distinct(), Is.EquivalentTo(new[] { 12, 13 }));
            Assert.That(b.Select(Board.GddRow).Distinct(), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(a.All(h => h.R >= 5), Is.True);
            Assert.That(b.All(h => h.R <= -5), Is.True);
            Assert.That(a.Intersect(b), Is.Empty);
            Assert.That(a.Min(h => Hex.Distance(h, Board.Portal)), Is.EqualTo(5));
            Assert.That(b.Min(h => Hex.Distance(h, Board.Portal)), Is.EqualTo(5));
            Assert.That(Board.IsHomeZone(H(0, 7), PlayerId.A), Is.False); // off board
        }

        [Test]
        public void B04_MirrorIsInvolution_AndOnBoardForAllCells()
        {
            foreach (var h in Board.Cells)
            {
                Assert.That(h.Mirror().Mirror(), Is.EqualTo(h));
                Assert.That(Board.IsOnBoard(h.Mirror()), Is.True, h.ToString());
                Assert.That(h.Mirror(), Is.EqualTo(H(-h.Q, -h.R)));
                Assert.That(Board.GddRow(h.Mirror()), Is.EqualTo(14 - Board.GddRow(h)));
            }
        }

        [Test]
        public void B04_MirrorMapsHomeZoneAToB_PortalToItself()
        {
            Assert.That(Board.Portal.Mirror(), Is.EqualTo(Board.Portal));
            foreach (var h in Board.Cells)
                Assert.That(Board.IsHomeZone(h.Mirror(), PlayerId.B), Is.EqualTo(Board.IsHomeZone(h, PlayerId.A)), h.ToString());
        }

        [Test]
        public void B06_HalvesAre63Each_Disjoint_CoverBoardWithPortal()
        {
            var a = Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.A)).ToList();
            var b = Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.B)).ToList();
            Assert.That(a.Count, Is.EqualTo(63));
            Assert.That(b.Count, Is.EqualTo(63));
            Assert.That(a.Intersect(b), Is.Empty);
            Assert.That(a.Contains(Board.Portal) || b.Contains(Board.Portal), Is.False);
            Assert.That(a.Concat(b).Append(Board.Portal), Is.EquivalentTo(Board.Cells));
        }

        [Test]
        public void B06_HalfAIsMirrorOfHalfB()
        {
            foreach (var h in Board.Cells)
                Assert.That(Board.IsInHalf(h.Mirror(), PlayerId.A), Is.EqualTo(Board.IsInHalf(h, PlayerId.B)), h.ToString());

            // A's half = rows 8–13 (r > 0) and the 6 cells of row 7 right of the Portal.
            var a = Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.A)).ToList();
            Assert.That(a.Count(h => Board.GddRow(h) >= 8), Is.EqualTo(57));
            Assert.That(a.Where(h => Board.GddRow(h) == 7).Select(Board.GddCol), Is.EquivalentTo(new[] { 8, 9, 10, 11, 12, 13 }));
        }

        [Test]
        public void B21_UpperHalfIs63Cells_EqualsHalfB()
        {
            var upper = Board.Cells.Where(Board.IsInUpperHalf).ToList();
            Assert.That(upper.Count, Is.EqualTo(63));
            Assert.That(upper, Is.EqualTo(Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.B))));
            // Rows 1–6 and the 6 cells of row 7 left of the Portal: r < 0, or r = 0 and q < 0.
            Assert.That(upper.Count(h => Board.GddRow(h) <= 6), Is.EqualTo(57));
            Assert.That(upper.Where(h => Board.GddRow(h) == 7).Select(Board.GddCol), Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(upper.All(h => h.R < 0 || (h.R == 0 && h.Q < 0)), Is.True);
        }

        [Test]
        public void B23_SpecialBand_IsRows3To5_AxialRMinus4ToMinus2_30Cells()
        {
            var band = Board.Cells.Where(h => Board.IsInUpperHalf(h)
                && Board.GddRow(h) >= Catalog.SpecialTileRowMin && Board.GddRow(h) <= Catalog.SpecialTileRowMax).ToList();
            Assert.That(band.Count, Is.EqualTo(30));
            Assert.That(band.Select(h => h.R).Distinct(), Is.EquivalentTo(new[] { -4, -3, -2 }));
        }

        [Test]
        public void Hex_SixNeighbors_DistanceSymmetric_DistanceToNeighborIsOne()
        {
            Assert.That(Hex.Directions, Is.EqualTo(new[]
            {
                new Hex(1, 0), new Hex(1, -1), new Hex(0, -1), new Hex(-1, 0), new Hex(-1, 1), new Hex(0, 1),
            }));

            var h0 = new Hex(2, -3);
            Assert.That(h0.S, Is.EqualTo(1));
            var ns = Enumerable.Range(0, 6).Select(h0.Neighbor).ToList();
            Assert.That(ns, Is.Unique);
            foreach (var n in ns) Assert.That(Hex.Distance(h0, n), Is.EqualTo(1));

            foreach (var a in Board.Cells)
            {
                Assert.That(Hex.Distance(a, a), Is.EqualTo(0));
                Assert.That(Hex.Distance(a, Board.Portal), Is.LessThanOrEqualTo(Catalog.BoardRadius));
                foreach (var b in Board.Cells)
                    Assert.That(Hex.Distance(a, b), Is.EqualTo(Hex.Distance(b, a)));
            }
            // Opposite corners: (0,-6) -> (0,6).
            Assert.That(Hex.Distance(H(0, -6), H(0, 6)), Is.EqualTo(12));
            Assert.That(Hex.Distance(H(6, -6), H(-6, 6)), Is.EqualTo(12));
            Assert.That(new Hex(1, 2) + new Hex(-3, 1), Is.EqualTo(new Hex(-2, 3)));
            Assert.That(new Hex(1, 2) != new Hex(1, 3), Is.True);
        }

        [Test]
        public void Hex_AddWithinEqualsAFilteredScanOfCells_SameOrder()
        {
            foreach (var c in Board.Cells)
                for (int radius = 0; radius <= 4; radius++)
                {
                    var set = new HashSet<Hex>();
                    Board.AddWithin(set, c, radius);
                    Assert.That(set.ToList(), Is.EqualTo(Board.Cells.Where(h => Hex.Distance(h, c) <= radius).ToList()), c + " r" + radius);
                }
        }

        // ---------- BFS (generic reach helper) ----------

        // Portal (0,0); corner (0,-6) (3 on-board neighbours); mid top edge (3,-6) (4 on-board neighbours).
        [TestCase(0, 0, 1, 6)]
        [TestCase(0, 0, 2, 18)]
        [TestCase(0, 0, 3, 36)]
        [TestCase(0, -6, 1, 3)]
        [TestCase(0, -6, 2, 8)]
        [TestCase(0, -6, 3, 15)]
        [TestCase(3, -6, 1, 4)]
        [TestCase(3, -6, 2, 11)]
        [TestCase(3, -6, 3, 21)]
        public void Hex_ReachCounts(int q, int r, int steps, int expected)
        {
            var start = H(q, r);
            var reach = HexSearch.Reachable(start, steps, _ => true, _ => true);
            Assert.That(reach.Count, Is.EqualTo(expected));
            Assert.That(reach, Is.Unique);
            Assert.That(reach, Does.Not.Contain(start));
            // On an empty board BFS reach equals the on-board distance ball.
            Assert.That(reach, Is.EquivalentTo(Board.Cells.Where(h => h != start && Hex.Distance(start, h) <= steps)));
        }

        [Test]
        public void Hex_ReachNeverEntersBlockedCells()
        {
            var p = Board.Portal;
            var open = new Hex(1, 0);
            var blocked = new HashSet<Hex>(Hex.Directions.Select(d => p + d).Where(h => h != open));

            var reach = HexSearch.Reachable(p, 2, h => !blocked.Contains(h), h => !blocked.Contains(h));
            Assert.That(reach, Is.EquivalentTo(new[] { new Hex(1, 0), new Hex(2, 0), new Hex(2, -1), new Hex(1, 1) }));

            // Fully enclosed: nothing reachable.
            blocked.Add(open);
            Assert.That(HexSearch.Reachable(p, 3, h => !blocked.Contains(h), h => !blocked.Contains(h)), Is.Empty);
        }

        [Test]
        public void Hex_ReachPassThroughButNotStop()
        {
            var p = Board.Portal;
            var occupied = new Hex(1, 0);
            var blocked = new HashSet<Hex>(Hex.Directions.Select(d => p + d).Where(h => h != occupied));

            var reach = HexSearch.Reachable(p, 2,
                h => !blocked.Contains(h),
                h => !blocked.Contains(h) && h != occupied);
            Assert.That(reach, Is.EquivalentTo(new[] { new Hex(2, 0), new Hex(2, -1), new Hex(1, 1) }));

            // Same result every call (deterministic order).
            Assert.That(HexSearch.Reachable(p, 2, h => !blocked.Contains(h), h => !blocked.Contains(h) && h != occupied),
                Is.EqualTo(reach));
        }
    }
}
