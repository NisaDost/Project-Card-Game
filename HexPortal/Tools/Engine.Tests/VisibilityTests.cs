using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §11, M2 part only: the "currently Visible" set (V-02, V-06) and targeting (V-07).
    // Explored memory, last-seen, V-08 and PlayerView are M4.
    public class VisibilityTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static int Sight(UnitClass c) => Catalog.Units.Single(u => u.Class == c).Sight;

        [Test]
        public void V02_VisibleIsUnionOfSightRangesPlusPortal()
        {
            var b = new TestBoard();
            var g = H(-2, -1);
            var r = H(1, 2);
            b.Unit(UnitClass.Guardian, A, g);
            b.Unit(UnitClass.Rider, A, r);
            b.Unit(UnitClass.Archer, B, H(3, -3)); // enemy sight never counts
            var s = b.Build();
            Assert.That(Hex.Distance(g, Board.Portal), Is.GreaterThan(Sight(UnitClass.Guardian)));

            var expected = Board.Cells.Where(c =>
                Hex.Distance(c, g) <= Sight(UnitClass.Guardian) ||
                Hex.Distance(c, r) <= Sight(UnitClass.Rider) ||
                Hex.Distance(c, TestBoard.DefaultTowerA) <= Catalog.Tower.Sight ||
                c == Board.Portal);
            Assert.That(Visibility.VisibleCells(s, A), Is.EquivalentTo(expected));
        }

        [Test]
        public void V02_RockDoesNotBlockSight()
        {
            var b = new TestBoard();
            var center = H(-1, 0);
            for (int d = 0; d < 6; d++) b.Rock(center.Neighbor(d));
            b.Unit(UnitClass.Guardian, A, center);
            var visible = Visibility.VisibleCells(b.Build(), A);
            foreach (var c in Board.Cells.Where(c => Hex.Distance(c, center) <= Sight(UnitClass.Guardian)))
                Assert.That(visible.Contains(c), Is.True, c.ToString());
        }

        [Test]
        public void V06_PortalAlwaysVisible()
        {
            var s = new TestBoard().Build(); // only the towers, both far from the Portal
            Assert.That(Hex.Distance(TestBoard.DefaultTowerA, Board.Portal), Is.GreaterThan(Catalog.Tower.Sight));
            Assert.That(Visibility.VisibleCells(s, A).Contains(Board.Portal), Is.True);
            Assert.That(Visibility.VisibleCells(s, B).Contains(Board.Portal), Is.True);
        }

        [Test]
        public void V07_CannotTargetNonVisible()
        {
            // With the current Catalog every MaxRange <= Sight, so a target in range is always Visible to the
            // attacker itself; the rule is checked through the explicit-visibility overload.
            foreach (var d in Catalog.Units) Assert.That(d.MaxRange, Is.LessThanOrEqualTo(d.Sight), d.Id);

            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 0));
            b.Unit(UnitClass.Guardian, B, H(0, -3));
            b.Unit(UnitClass.Guardian, B, H(2, 0));
            var s = b.Build();
            var visible = new HashSet<Hex>(Board.Cells);
            visible.Remove(H(0, -3));
            Assert.That(Combat.LegalTargets(s, s.GetUnit(a), visible), Is.EqualTo(new[] { H(2, 0) }));
            Assert.That(Combat.LegalTargets(s, s.GetUnit(a)), Is.EquivalentTo(new[] { H(0, -3), H(2, 0) }));

            // Overwatch / tower shot use the same target check.
            Assert.That(Combat.IsValidTarget(s, B, H(0, 0), new HashSet<Hex>()), Is.False);
        }
    }
}
