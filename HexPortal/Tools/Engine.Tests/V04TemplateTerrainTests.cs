using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // V-04 (v4.0): the v3.3 exception (template Void and rock Explored for both at match start) is removed. Template
    // terrain follows the normal fog rules like E-10 rock (E-05). Void is never Visible (V-02), so it stays Hidden; only
    // the public board shape (PlayerView.PlayableCells, B-25 template id) tells where it is.
    public class V04TemplateTerrainTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;

        static IEnumerable<Hex> TemplateCells(GameMap m)
        {
            var def = Catalog.MapTemplates[m.TemplateId];
            foreach (var h in def.UpperRocks.Concat(def.UpperVoids)) { yield return h; yield return h.Mirror(); }
        }

        [TestCase(1UL)] // RockyRing
        [TestCase(2UL)] // IndentedEdges
        [TestCase(3UL)] // TwoCorridors
        public void V04_TemplateRockAndVoidAreFoggedAtMatchStart(ulong seed)
        {
            var s = MatchHelper.StartedMatch(seed);
            Assert.That(TemplateCells(s.Map), Is.Not.Empty);
            foreach (var p in new[] { A, B })
            {
                var v = PlayerView.For(s, p);
                var visible = Visibility.VisibleCells(s, p);
                foreach (var h in TemplateCells(s.Map))
                {
                    var c = v.Cells[Board.IndexOf(h)];
                    if (visible.Contains(h))
                    {
                        Assert.That(s.Map.Get(h).Marker, Is.Not.EqualTo(Marker.Void), "Void is never Visible");
                        Assert.That(c.Visibility, Is.EqualTo(CellVisibility.Visible));
                        continue;
                    }
                    Assert.That(s.GetFog(p).Get(h), Is.EqualTo(CellVisibility.Hidden), p + " " + h);
                    Assert.That((c.TerrainKnown, c.Unit, c.HasTower), Is.EqualTo((false, (UnitView)null, false)), p + " " + h);
                }
                // The shape stays public (camera framing): every Void cell is missing from PlayableCells.
                foreach (var h in Board.Cells.Where(h => s.Map.Get(h).Marker == Marker.Void))
                    Assert.That(v.PlayableCells, Does.Not.Contain(h));
            }
        }

        [Test]
        public void V04_E05_E10RockOnANeverSeenCellStaysHidden()
        {
            var h = new Hex(2, -4); // B's half, far from A's units and tower
            var s = new TestBoard().Round(2).Active(B).PendingEvent("E-10", 3, h, h.Mirror()).Build();
            Assert.That(s.GetFog(A).Get(h), Is.EqualTo(CellVisibility.Hidden));
            Engine.Apply(s, new EndTurnCommand(B)); // round 3 starts: the earthquake happens
            Assert.That(s.Map.Get(h).Marker, Is.EqualTo(Marker.Mountain));
            Assert.That(s.GetFog(A).Get(h), Is.EqualTo(CellVisibility.Hidden));
            var c = PlayerView.For(s, A).Cells[Board.IndexOf(h)];
            Assert.That((c.TerrainKnown, c.Unit, c.HasTower), Is.EqualTo((false, (UnitView)null, false)));
        }
    }
}
