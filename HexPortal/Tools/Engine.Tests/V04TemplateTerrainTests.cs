using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // V-04 exception (PM, v3.3): the template's permanent terrain (Void and template rock, B-25) is Explored for both
    // players at match start. Rock created later by E-10 keeps the normal fog rules (E-05: a never-seen cell stays Hidden).
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
        public void V04_TemplateRockAndVoidAreExploredForBothPlayersAtMatchStart(ulong seed)
        {
            var s = Match.Create(seed);
            Assert.That(TemplateCells(s.Map), Is.Not.Empty);
            foreach (var p in new[] { A, B })
            {
                var v = PlayerView.For(s, p);
                foreach (var h in TemplateCells(s.Map))
                {
                    Assert.That(s.GetFog(p).Get(h), Is.EqualTo(CellVisibility.Explored), p + " " + h);
                    var c = v.Cells[Board.IndexOf(h)];
                    Assert.That((c.TerrainKnown, c.Tile.Marker, c.Unit, c.HasTower), Is.EqualTo((true, s.Map.Get(h).Marker, (UnitView)null, false)));
                }
                // Other cells of the opponent's half stay Hidden (V-04).
                var other = Board.Cells.First(h => Board.IsInHalf(h, p.Opponent()) && s.Map.Get(h).Marker == Marker.None);
                Assert.That(s.GetFog(p).Get(other), Is.EqualTo(CellVisibility.Hidden));
            }
        }

        [Test]
        public void V04_E05_E10RockOnANeverSeenCellStaysHidden_UnlikeTemplateRock()
        {
            var h = new Hex(2, -4); // B's half, far from A's units and tower
            var s = new TestBoard().Round(2).Active(B).PendingEvent("E-10", 3, h, h.Mirror()).Build();
            Assert.That(s.GetFog(A).Get(h), Is.EqualTo(CellVisibility.Hidden));
            Engine.Apply(s, new EndTurnCommand(B)); // round 3 starts: the earthquake happens
            Assert.That(s.Map.Get(h).Marker, Is.EqualTo(Marker.Rock));
            Assert.That(s.GetFog(A).Get(h), Is.EqualTo(CellVisibility.Hidden));
            var c = PlayerView.For(s, A).Cells[Board.IndexOf(h)];
            Assert.That((c.TerrainKnown, c.Unit, c.HasTower), Is.EqualTo((false, (UnitView)null, false)));
        }
    }
}
