using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §2 board geometry (B-01…B-06, B-21) and the generic hex helpers. Values from the hex-grid skill.
    public class BoardTests
    {
        static Hex D(int x, int y) => Hex.FromDoubled(x, y);

        // ---------- Geometry ----------

        [Test]
        public void B01_BoardHas59Cells_RowLengths_7_6_7_6_7_6_7_6_7()
        {
            Assert.That(Board.Cells.Count, Is.EqualTo(59));
            Assert.That(Board.Cells.Count, Is.EqualTo(Catalog.CellCount));
            Assert.That(Board.Cells, Is.Unique);

            var perRow = Enumerable.Range(1, 9).Select(row => Board.Cells.Count(h => Board.GddRow(h) == row)).ToArray();
            Assert.That(perRow, Is.EqualTo(new[] { 7, 6, 7, 6, 7, 6, 7, 6, 7 }));
            Assert.That(perRow, Is.EqualTo(Catalog.RowLengths));

            // GddCol runs 1..rowLength within each row.
            for (int row = 1; row <= 9; row++)
                Assert.That(Board.Cells.Where(h => Board.GddRow(h) == row).Select(Board.GddCol),
                    Is.EqualTo(Enumerable.Range(1, perRow[row - 1])), "row " + row);
        }

        [Test]
        public void B01_AxialDoubledRoundTripForAll59Cells()
        {
            var expected = new List<(int, int)>();
            for (int y = 0; y <= 8; y++)
                for (int x = y % 2; x <= 12; x += 2)
                    expected.Add((x, y));
            Assert.That(expected.Count, Is.EqualTo(59));

            var actual = new List<(int, int)>();
            foreach (var h in Board.Cells)
            {
                h.ToDoubled(out int x, out int y);
                Assert.That((x + y) % 2, Is.EqualTo(0), h.ToString());
                Assert.That(Hex.FromDoubled(x, y), Is.EqualTo(h));
                actual.Add((x, y));
            }
            // Cells are ordered by y, then x.
            Assert.That(actual, Is.EqualTo(expected));

            foreach (var (x, y) in expected)
                Assert.That(Board.IsOnBoard(D(x, y)), Is.True);
            Assert.That(Board.IsOnBoard(new Hex(5, 0)), Is.False);
            Assert.That(Board.IsOnBoard(new Hex(0, 5)), Is.False);
            Assert.That(Board.IsOnBoard(D(13, 1)), Is.False);
        }

        [Test]
        public void B02_PortalIsRow5Col4_Axial00_Doubled64()
        {
            Assert.That(Board.Portal, Is.EqualTo(new Hex(0, 0)));
            Assert.That(Board.GddRow(Board.Portal), Is.EqualTo(5));
            Assert.That(Board.GddCol(Board.Portal), Is.EqualTo(4));
            Board.Portal.ToDoubled(out int x, out int y);
            Assert.That((x, y), Is.EqualTo((6, 4)));
        }

        [Test]
        public void B03_HomeZones13Each_ARows8to9_BRows1to2()
        {
            var a = Board.Cells.Where(h => Board.IsHomeZone(h, PlayerId.A)).ToList();
            var b = Board.Cells.Where(h => Board.IsHomeZone(h, PlayerId.B)).ToList();
            Assert.That(a.Count, Is.EqualTo(13));
            Assert.That(b.Count, Is.EqualTo(13));
            Assert.That(a.Select(Board.GddRow).Distinct(), Is.EquivalentTo(new[] { 8, 9 }));
            Assert.That(b.Select(Board.GddRow).Distinct(), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(a.Intersect(b), Is.Empty);
        }

        [Test]
        public void B04_MirrorIsInvolution_AndOnBoardForAllCells()
        {
            foreach (var h in Board.Cells)
            {
                Assert.That(h.Mirror().Mirror(), Is.EqualTo(h));
                Assert.That(Board.IsOnBoard(h.Mirror()), Is.True, h.ToString());
                h.ToDoubled(out int x, out int y);
                h.Mirror().ToDoubled(out int mx, out int my);
                Assert.That((mx, my), Is.EqualTo((12 - x, 8 - y)));
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
        public void B06_HalvesAre29Each_Disjoint_CoverBoardWithPortal()
        {
            var a = Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.A)).ToList();
            var b = Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.B)).ToList();
            Assert.That(a.Count, Is.EqualTo(29));
            Assert.That(b.Count, Is.EqualTo(29));
            Assert.That(a.Intersect(b), Is.Empty);
            Assert.That(a.Contains(Board.Portal) || b.Contains(Board.Portal), Is.False);
            Assert.That(a.Concat(b).Append(Board.Portal), Is.EquivalentTo(Board.Cells));
        }

        [Test]
        public void B06_HalfAIsMirrorOfHalfB()
        {
            foreach (var h in Board.Cells)
                Assert.That(Board.IsInHalf(h.Mirror(), PlayerId.A), Is.EqualTo(Board.IsInHalf(h, PlayerId.B)), h.ToString());

            // A's half = rows 6–9 and the 3 cells of row 5 right of the Portal.
            var a = Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.A)).ToList();
            Assert.That(a.Count(h => Board.GddRow(h) >= 6), Is.EqualTo(26));
            Assert.That(a.Where(h => Board.GddRow(h) == 5).Select(Board.GddCol), Is.EquivalentTo(new[] { 5, 6, 7 }));
        }

        [Test]
        public void B21_UpperHalfIs29Cells_EqualsHalfB()
        {
            var upper = Board.Cells.Where(Board.IsInUpperHalf).ToList();
            Assert.That(upper.Count, Is.EqualTo(29));
            Assert.That(upper, Is.EqualTo(Board.Cells.Where(h => Board.IsInHalf(h, PlayerId.B))));
            // Rows 1–4 and the 3 cells of row 5 left of the Portal.
            Assert.That(upper.Count(h => Board.GddRow(h) <= 4), Is.EqualTo(26));
            Assert.That(upper.Where(h => Board.GddRow(h) == 5).Select(Board.GddCol), Is.EquivalentTo(new[] { 1, 2, 3 }));
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
                foreach (var b in Board.Cells)
                    Assert.That(Hex.Distance(a, b), Is.EqualTo(Hex.Distance(b, a)));
            }
            // Corner to corner: doubled (0,0) -> (12,8).
            Assert.That(Hex.Distance(D(0, 0), D(12, 8)), Is.EqualTo(10));
            Assert.That(new Hex(1, 2) + new Hex(-3, 1), Is.EqualTo(new Hex(-2, 3)));
            Assert.That(new Hex(1, 2) != new Hex(1, 3), Is.True);
        }

        // ---------- BFS (generic reach helper; unit rules come in M2) ----------

        [TestCase(6, 4, 1, 6)]
        [TestCase(6, 4, 2, 18)]
        [TestCase(6, 4, 3, 36)]
        [TestCase(0, 0, 1, 2)]
        [TestCase(0, 0, 2, 6)]
        [TestCase(0, 0, 3, 11)]
        [TestCase(1, 1, 1, 5)]
        [TestCase(1, 1, 2, 10)]
        [TestCase(1, 1, 3, 17)]
        public void Hex_ReachCounts(int x, int y, int steps, int expected)
        {
            var start = D(x, y);
            var reach = HexSearch.Reachable(start, steps, _ => true, _ => true);
            Assert.That(reach.Count, Is.EqualTo(expected));
            Assert.That(reach, Is.Unique);
            Assert.That(reach, Does.Not.Contain(start));
            foreach (var h in reach)
            {
                Assert.That(Board.IsOnBoard(h), Is.True);
                Assert.That(Hex.Distance(start, h), Is.InRange(1, steps));
            }
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
