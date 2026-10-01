using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §10 map events: E-01…E-05, E-10…E-12 (v2.8: announced and resolved before A's turn start, public; type
    // fallback; "empty" = no unit or tower; E-10 destroys traps, E-12 keeps them; E-11 turns into one other biome).
    public class MapEventTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        /// <summary>E-11 cluster: the center, then its on-board neighbours (not the Portal) in direction order,
        /// then the mirror images in the same order.</summary>
        static Hex[] Cluster(Hex center)
        {
            var half = new List<Hex> { center };
            half.AddRange(Hex.Directions.Select(d => center + d).Where(h => Board.IsOnBoard(h) && h != Board.Portal));
            return half.Concat(half.Select(h => h.Mirror())).ToArray();
        }

        static bool Home(Hex h) => Board.IsHomeZone(h, A) || Board.IsHomeZone(h, B);

        // ---------- E-01, E-02, E-05 ----------

        [Test]
        public void E01_E02_AnnouncedAtTheStartOfRoundTwo_ResolvedAtTheStartOfRoundThree_BeforeATurnStart()
        {
            var s = new TestBoard().Active(B).Build();
            var ev = End(s); // round 2
            var ann = ev.OfType<MapEventAnnounced>().Single();
            Assert.That(ev.FindIndex(e => e is MapEventAnnounced), Is.LessThan(ev.FindIndex(e => e is TurnStarted)));
            Assert.That(ann.Round, Is.EqualTo(3));
            Assert.That((s.PendingEvent.Id, s.PendingEventRound, s.PendingEventCells), Is.EqualTo((ann.DefId, 3, ann.Cells)));
            foreach (var p in new[] { A, B })
            {
                Assert.That(EventFilter.For(ev, p).OfType<MapEventAnnounced>().Single(), Is.SameAs(ann)); // E-05: public
                var v = PlayerView.For(s, p);
                Assert.That((v.AnnouncedEvent, v.AnnouncedEventRound, v.AnnouncedEventCells), Is.EqualTo((ann.DefId, 3, ann.Cells)));
            }
            End(s);
            ev = End(s); // round 3
            var res = ev.OfType<MapEventResolved>().Single();
            Assert.That(res.DefId, Is.EqualTo(ann.DefId));
            Assert.That(ev.FindIndex(e => e is MapEventResolved), Is.LessThan(ev.FindIndex(e => e is TurnStarted)));
            Assert.That(ev.OfType<MapEventAnnounced>(), Is.Empty);
            Assert.That(s.PendingEvent, Is.Null);
            foreach (var p in new[] { A, B })
            {
                Assert.That(EventFilter.For(ev, p).OfType<MapEventResolved>().Single(), Is.SameAs(res));
                Assert.That(PlayerView.For(s, p).AnnouncedEvent, Is.Null);
            }
        }

        [Test]
        public void E01_EventsHappenInRounds3_6_9_12_AnnouncedOneRoundBefore()
        {
            var s = new TestBoard().Build();
            var announced = new List<int>();
            var resolved = new List<int>();
            while (s.Round <= 14)
            {
                var ev = End(s);
                if (ev.OfType<MapEventAnnounced>().Any()) announced.Add(s.Round);
                if (ev.OfType<MapEventResolved>().Any()) resolved.Add(s.Round);
            }
            Assert.That(resolved, Is.EqualTo(Catalog.EventRounds));
            Assert.That(announced, Is.EqualTo(Catalog.EventRounds.Select(r => r - Catalog.EventAnnounceLead)));
        }

        // ---------- E-03, E-10, E-11, E-12 targets ----------

        [Test]
        public void E03_TargetsAreMirrorPairs_ValidPerType_AndTheMapStaysSymmetric()
        {
            var types = new HashSet<string>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var b = new TestBoard(Biome.Forest, seed).Active(B).Rune(H(1, -1)).Rune(H(-1, 1)).Wellspring(H(2, -1)).Wellspring(H(-2, 1));
                b.Unit(UnitClass.Guardian, A, H(0, 1));
                b.Unit(UnitClass.Guardian, B, H(1, -2));
                b.Unit(UnitClass.Guardian, A, H(-2, 2));
                var s = b.Build();
                var ann = End(s).OfType<MapEventAnnounced>().Single();
                types.Add(ann.DefId);
                var cells = ann.Cells.ToList();
                Assert.That(cells.Count % 2, Is.EqualTo(0));
                int n = cells.Count / 2;
                for (int i = 0; i < cells.Count; i++) Assert.That(cells.Contains(cells[i].Mirror()), Is.True, "mirror of " + cells[i]);
                Assert.That(cells, Does.Not.Contain(Board.Portal));
                Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Count));
                if (ann.DefId == "E-11")
                {
                    Assert.That(Hex.Distance(cells[0], Board.Portal), Is.GreaterThanOrEqualTo(Catalog.BiomeShiftMinPortalDistance));
                    Assert.That(n, Is.LessThanOrEqualTo(Catalog.BiomeShiftMaxCells));
                    Assert.That(cells.Take(n), Is.EqualTo(Cluster(cells[0]).Take(n)));
                    Assert.That(cells.Skip(n), Is.EqualTo(cells.Take(n).Select(h => h.Mirror())));
                }
                else
                {
                    Assert.That(n, Is.EqualTo(Catalog.MapEvents.Single(e => e.Id == ann.DefId).Pairs));
                    Assert.That(cells[1], Is.EqualTo(cells[0].Mirror()));
                    foreach (var h in cells) // E-04 (v2.9): terrain only at the announcement
                    {
                        Assert.That(Home(h), Is.False, h.ToString());
                        Assert.That(s.Map.Get(h).Marker, Is.EqualTo(Marker.None), h.ToString());
                    }
                }
                End(s);
                End(s); // resolved
                foreach (var h in Board.Cells)
                {
                    var t = s.Map.Get(h);
                    var m = s.Map.Get(h.Mirror());
                    Assert.That((t.Biome, t.Marker), Is.EqualTo((m.Biome, m.Marker)), "B-04 symmetry at " + h);
                }
            }
            Assert.That(types, Is.EquivalentTo(Catalog.MapEvents.Select(e => e.Id)));
        }

        [Test]
        public void E03_ATypeWithoutTargetsIsReplacedByOneWithTargets()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var b = new TestBoard(Biome.Forest, seed).Active(B);
                foreach (var h in Board.Cells.Where(h => !Home(h) && h != Board.Portal)) b.Rune(h); // nothing is non-special
                var s = b.Build();
                Assert.That(End(s).OfType<MapEventAnnounced>().Single().DefId, Is.EqualTo("E-11"));
            }
        }

        // ---------- E-04 ----------

        [Test]
        public void E04_TheAnnouncementIgnoresUnits_OccupancyIsCheckedWhenTheEventHappens()
        {
            // Only one terrain-eligible pair exists, and a unit stands on one of its cells (v2.9).
            var h = H(1, -1);
            int pairAnnounced = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var b = new TestBoard(Biome.Forest, seed).Active(B);
                foreach (var c in Board.Cells.Where(c => !Home(c) && c != Board.Portal && c != h && c != h.Mirror())) b.Rune(c);
                b.Unit(UnitClass.Guardian, A, h);
                var s = b.Build();
                var ann = End(s).OfType<MapEventAnnounced>().Single();
                if (ann.DefId == "E-11") continue;
                pairAnnounced++;
                Assert.That(ann.Cells, Is.EqualTo(new[] { h, h.Mirror() }));
                End(s);
                var res = End(s).OfType<MapEventResolved>().Single(); // the unit is still there: skipped
                Assert.That((res.Cells.Count, res.Skipped), Is.EqualTo((0, new[] { h, h.Mirror() })));
                Assert.That(s.Map.Get(h).Marker, Is.EqualTo(Marker.None));
            }
            Assert.That(pairAnnounced, Is.GreaterThan(0));
        }

        [Test]
        public void E04_APairThatIsNoLongerEligibleIsSkipped()
        {
            var h = H(1, -1);
            var b = new TestBoard().Round(2).Active(B).PendingEvent("E-10", 3, h, h.Mirror());
            int g = b.Unit(UnitClass.Guardian, B, H(1, -2));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(B, g, h));
            var res = End(s).OfType<MapEventResolved>().Single();
            Assert.That(res.Cells, Is.Empty);
            Assert.That(res.Skipped, Is.EqualTo(new[] { h, h.Mirror() }));
            Assert.That(s.Map.Get(h).Marker, Is.EqualTo(Marker.None));
            Assert.That(s.Map.Get(h.Mirror()).Marker, Is.EqualTo(Marker.None));
        }

        // ---------- E-10 ----------

        [Test]
        public void E10_EarthquakeTurnsThePairIntoRock_KeepsTheBiome_TrapsGoneShownToTheirOwnerOnly()
        {
            var h = H(1, -1);
            var b = new TestBoard().Round(2).Active(B).Biome(h, Biome.Snow).Biome(h.Mirror(), Biome.Snow)
                .PendingEvent("E-10", 3, h, h.Mirror());
            int trapA = b.Trap(A, "C-20", h);
            int trapB = b.Trap(B, "C-20", h.Mirror());
            var s = b.Build();
            var ev = End(s);
            var res = ev.OfType<MapEventResolved>().Single();
            Assert.That(res.Cells, Is.EqualTo(new[] { h, h.Mirror() }));
            foreach (var c in new[] { h, h.Mirror() })
            {
                Assert.That((s.Map.Get(c).Biome, s.Map.Get(c).Marker), Is.EqualTo((Biome.Snow, Marker.Rock)));
                Assert.That(Movement.IsEmpty(s, c), Is.False);
            }
            Assert.That(res.Tiles.Select(t => t.Marker), Is.EqualTo(new[] { Marker.Rock, Marker.Rock }));
            Assert.That(s.Traps, Is.Empty);
            Assert.That(EventFilter.For(ev, A).OfType<TrapRemoved>().Select(t => t.CardId), Is.EqualTo(new[] { trapA }));
            Assert.That(EventFilter.For(ev, B).OfType<TrapRemoved>().Select(t => t.CardId), Is.EqualTo(new[] { trapB }));
        }

        // ---------- E-11 ----------

        [Test]
        public void E11_TheClusterAndItsMirrorBecomeOneOtherBiome_MarkersKept()
        {
            var center = H(2, -3); // distance 3 from the Portal; its neighbours include B's tower (2,-4)
            var cells = Cluster(center);
            Assert.That(cells.Length, Is.EqualTo(14));
            var seenBiomes = new HashSet<Biome>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var b = new TestBoard(Biome.Forest, seed).Round(2).Active(B).PendingEvent("E-11", 3, cells);
                b.Biome(center, Biome.Desert).Biome(center.Mirror(), Biome.Desert);
                b.Rune(H(1, -2)).Rune(H(-1, 2)).Rock(H(3, -3)).Rock(H(-3, 3)).Wellspring(H(2, -2)).Wellspring(H(-2, 2));
                var s = b.Build();
                var before = Board.Cells.ToDictionary(h => h, h => s.Map.Get(h));
                var res = End(s).OfType<MapEventResolved>().Single();
                Assert.That(res.Cells, Is.EqualTo(cells));
                Assert.That(res.Skipped, Is.Empty);
                var biome = s.Map.Get(center).Biome;
                Assert.That(biome, Is.Not.EqualTo(Biome.Desert).And.Not.EqualTo(Biome.None));
                seenBiomes.Add(biome);
                foreach (var h in Board.Cells)
                {
                    var t = s.Map.Get(h);
                    Assert.That(t.Marker, Is.EqualTo(before[h].Marker), "marker kept at " + h);
                    Assert.That(t.Biome, Is.EqualTo(cells.Contains(h) ? biome : before[h].Biome), h.ToString());
                }
            }
            Assert.That(seenBiomes, Is.EquivalentTo(new[] { Biome.Forest, Biome.Snow }));
        }

        // ---------- E-12 ----------

        [Test]
        public void E12_RuneRainAddsARuneStonePair_TrapsStay()
        {
            var h = H(1, -1);
            var b = new TestBoard().Round(2).Active(B).PendingEvent("E-12", 3, h, h.Mirror());
            b.Trap(A, "C-20", h);
            var s = b.Build();
            var ev = End(s);
            Assert.That(ev.OfType<MapEventResolved>().Single().Cells, Is.EqualTo(new[] { h, h.Mirror() }));
            Assert.That(s.Map.Get(h).Marker, Is.EqualTo(Marker.RuneStone));
            Assert.That(s.Map.Get(h.Mirror()).Marker, Is.EqualTo(Marker.RuneStone));
            Assert.That(s.Map.Get(h).Biome, Is.EqualTo(Biome.Forest));
            Assert.That(s.Traps.Count, Is.EqualTo(1));
            Assert.That(ev.OfType<TrapRemoved>(), Is.Empty);
        }

        // ---------- E-05 fog memory ----------

        [Test]
        public void E05_GhostsOnCellsTurnedIntoRockOrRuneAreRemoved()
        {
            foreach (var type in new[] { "E-10", "E-12" })
            {
                var h = H(-2, -1); // B's half, out of B's sight
                var s = new TestBoard().Round(2).Active(B).PendingEvent(type, 3, h, h.Mirror()).Build();
                var ghost = new LastSeen(s.Map.Get(h), 77, A, UnitClass.Mage, Biome.Snow, 2, false, A, 0);
                s.GetFog(B).Set(h, CellVisibility.Explored, ghost);
                End(s);
                var seen = s.GetFog(B).GetLastSeen(h);
                Assert.That(s.GetFog(B).Get(h), Is.EqualTo(CellVisibility.Explored), type);
                Assert.That(seen.Tile.Marker, Is.EqualTo(type == "E-10" ? Marker.Rock : Marker.RuneStone), type);
                Assert.That(seen.HasUnit, Is.False, type); // v2.10: the event proves the cell empty
                Assert.That(PlayerView.For(s, B).Cells.Single(c => c.Cell == h).Unit, Is.Null, type);
            }
        }

        [Test]
        public void E05_ExploredCellsGetTheNewTerrainButUnitsAreNotRevealed()
        {
            var center = H(-2, -1); // in B's half, out of everyone's sight
            var cells = Cluster(center);
            var b = new TestBoard().Round(2).Active(B).PendingEvent("E-11", 3, cells);
            int hiddenA = b.Unit(UnitClass.Guardian, A, H(-1, -1));
            var s = b.Build();
            var ghostCell = H(-2, 0);
            var ghost = new LastSeen(s.Map.Get(ghostCell), 77, A, UnitClass.Mage, Biome.Snow, 2, false, A, 0);
            s.GetFog(B).Set(ghostCell, CellVisibility.Explored, ghost);
            var fogB = s.GetFog(B);
            var unitCell = s.GetUnit(hiddenA).Pos;
            Assert.That(Visibility.VisibleCells(s, B).Contains(unitCell), Is.False);
            Assert.That(fogB.Get(center.Mirror()), Is.EqualTo(CellVisibility.Hidden));

            var ev = End(s);
            var biome = s.Map.Get(center).Biome;
            Assert.That(EventFilter.For(ev, B).OfType<MapEventResolved>().Count(), Is.EqualTo(1));
            // The hidden A unit's cell: Explored for B, new terrain, still no unit.
            Assert.That(fogB.Get(unitCell), Is.EqualTo(CellVisibility.Explored));
            Assert.That(fogB.GetLastSeen(unitCell).Tile.Biome, Is.EqualTo(biome));
            Assert.That(fogB.GetLastSeen(unitCell).HasUnit, Is.False);
            var cv = PlayerView.For(s, B).Cells.Single(c => c.Cell == unitCell);
            Assert.That((cv.Tile.Biome, cv.Unit), Is.EqualTo((biome, (UnitView)null)));
            // A ghost stays exactly as it was seen (V-05), only the terrain changes.
            var after = fogB.GetLastSeen(ghostCell);
            Assert.That(after.Tile.Biome, Is.EqualTo(biome));
            Assert.That((after.UnitId, after.UnitClass, after.UnitHealth), Is.EqualTo((77, UnitClass.Mage, 2)));
            // Interim (flagged): a Hidden cell stays Hidden; the change is public through the event.
            Assert.That(fogB.Get(center.Mirror()), Is.EqualTo(CellVisibility.Hidden));
        }
    }
}
