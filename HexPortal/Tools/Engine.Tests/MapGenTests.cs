using System.Collections.Generic;
using System.Linq;
using System.Text;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §2.2 map generation (B-20…B-24) plus tile rules B-04, B-13, B-14.
    public class MapGenTests
    {
        const int SeedCount = 1000;
        static readonly Biome[] Biomes = { Biome.Forest, Biome.Desert, Biome.Snow };

        GameMap[] maps; // maps[i] = Generate(i + 1)

        [OneTimeSetUp]
        public void GenerateAll()
        {
            maps = new GameMap[SeedCount];
            for (int i = 0; i < SeedCount; i++) maps[i] = MapGenerator.Generate((ulong)(i + 1));
        }

        static string Signature(GameMap m)
        {
            var sb = new StringBuilder();
            foreach (var h in Board.Cells)
            {
                var t = m.Get(h);
                sb.Append((int)t.Biome).Append((int)t.Marker);
            }
            return sb.ToString();
        }

        static List<Hex> Find(GameMap m, Marker marker, bool upperOnly) =>
            Board.Cells.Where(h => m.Get(h).Marker == marker && (!upperOnly || Board.IsInUpperHalf(h))).ToList();

        static void SetPair(GameMap m, Hex h, Tile t)
        {
            m.Set(h, t);
            m.Set(h.Mirror(), t);
        }

        static void MoveMarkerPair(GameMap m, Hex from, Hex to)
        {
            var marker = m.Get(from).Marker;
            SetPair(m, from, new Tile(m.Get(from).Biome, Marker.None));
            SetPair(m, to, new Tile(m.Get(to).Biome, marker));
        }

        static bool InBand(Hex h) =>
            Board.IsInUpperHalf(h) && Board.GddRow(h) >= Catalog.SpecialTileRowMin && Board.GddRow(h) <= Catalog.SpecialTileRowMax;

        /// <summary>B-25: playable non-rock upper-half cells of the map's template (biome cells).</summary>
        static bool IsUpperBiomeCell(GameMap m, Hex h) =>
            Board.IsInUpperHalf(h) && m.Get(h).Marker != Marker.Void && m.Get(h).Marker != Marker.Rock;

        static int N(GameMap m) => Board.Cells.Count(h => IsUpperBiomeCell(m, h));

        static IEnumerable<GameMap> OfTemplate(IEnumerable<GameMap> ms, int t) => ms.Where(m => m.TemplateId == t);

        static List<List<Hex>> UpperClusters(GameMap m)
        {
            var seen = new HashSet<Hex>();
            var clusters = new List<List<Hex>>();
            foreach (var start in Board.Cells.Where(h => IsUpperBiomeCell(m, h)))
            {
                if (!seen.Add(start)) continue;
                var biome = m.Get(start).Biome;
                var cluster = new List<Hex>();
                var queue = new Queue<Hex>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    cluster.Add(c);
                    for (int d = 0; d < 6; d++)
                    {
                        var n = c.Neighbor(d);
                        if (IsUpperBiomeCell(m, n) && m.Get(n).Biome == biome && seen.Add(n)) queue.Enqueue(n);
                    }
                }
                clusters.Add(cluster);
            }
            return clusters;
        }

        // ---------- Determinism (B-20) ----------

        [Test]
        public void B20_SameSeedSameMap()
        {
            for (int i = 0; i < SeedCount; i++)
            {
                var again = MapGenerator.Generate((ulong)(i + 1));
                Assert.That(Signature(again), Is.EqualTo(Signature(maps[i])), "seed " + (i + 1));
                Assert.That(again.Attempts, Is.EqualTo(maps[i].Attempts));
            }
        }

        [Test]
        public void B20_DifferentSeedsGiveDifferentMaps()
        {
            int distinct = maps.Select(Signature).Distinct().Count();
            TestContext.Out.WriteLine($"[stats] distinct maps: {distinct}/{SeedCount} = {100.0 * distinct / SeedCount:F1}%");
            Assert.That(distinct, Is.EqualTo(SeedCount));
        }

        [Test]
        public void B24_RetryContinuesSameStream_NotNextSeed()
        {
            var retried = maps.Where(m => m.Attempts > 1).ToList();
            Assert.That(retried, Is.Not.Empty, "need at least one retried map in seeds 1..1000");
            foreach (var m in retried)
                for (int k = 1; k < m.Attempts; k++)
                    Assert.That(Signature(MapGenerator.Generate(m.RequestedSeed + (ulong)k)), Is.Not.EqualTo(Signature(m)),
                        $"seed {m.RequestedSeed} (attempts {m.Attempts}) equals seed {m.RequestedSeed + (ulong)k}");
        }

        // ---------- Validity (B-24) ----------

        [Test]
        public void B24_AllSeeds1To1000ProduceValidMaps()
        {
            for (int i = 0; i < SeedCount; i++)
                Assert.That(MapValidator.Validate(maps[i]), Is.Empty, "seed " + (i + 1));
        }

        [Test]
        public void B24_AtLeast90PercentValidOnFirstAttempt_PerTemplate_Seeds1To1000()
        {
            TestContext.Out.WriteLine($"[stats] max attempts: {maps.Max(m => m.Attempts)}, mean attempts: {maps.Average(m => m.Attempts):F3}");
            for (int t = 0; t < Catalog.MapTemplates.Count; t++)
            {
                var ms = OfTemplate(maps, t).ToList();
                int first = ms.Count(m => m.Attempts == 1);
                TestContext.Out.WriteLine($"[stats] template {t} {Catalog.MapTemplates[t].Name}: valid on first attempt {first}/{ms.Count} = {100.0 * first / ms.Count:F1}%, max attempts {ms.Max(m => m.Attempts)}");
                Assert.That(first, Is.GreaterThanOrEqualTo(ms.Count * 90 / 100), "template " + t);
            }
        }

        [Test]
        public void B24_MapRecordsRequestedSeedAndAttempts()
        {
            for (int i = 0; i < SeedCount; i++)
            {
                Assert.That(maps[i].RequestedSeed, Is.EqualTo((ulong)(i + 1)));
                Assert.That(maps[i].Attempts, Is.GreaterThanOrEqualTo(1));
            }
            Assert.That(MapGenerator.Generate(ulong.MaxValue).RequestedSeed, Is.EqualTo(ulong.MaxValue));
            Assert.That(MapGenerator.Generate(0).RequestedSeed, Is.EqualTo(0UL));
        }

        // ---------- Symmetry and tile kinds ----------

        [Test]
        public void B04_GeneratedMapsAreExactlySymmetric()
        {
            foreach (var m in maps)
                foreach (var h in Board.Cells)
                {
                    Assert.That(m.Get(h.Mirror()).Biome, Is.EqualTo(m.Get(h).Biome));
                    Assert.That(m.Get(h.Mirror()).Marker, Is.EqualTo(m.Get(h).Marker));
                }
        }

        [Test]
        public void B13_PortalHasNoBiome()
        {
            foreach (var m in maps)
            {
                Assert.That(m.Get(Board.Portal).Biome, Is.EqualTo(Biome.None));
                Assert.That(m.Get(Board.Portal).Marker, Is.EqualTo(Marker.Portal));
                Assert.That(Find(m, Marker.Portal, false), Is.EqualTo(new[] { Board.Portal }));
                // B-25: template rock and Void cells have no biome; every other cell has one.
                foreach (var h in Board.Cells.Where(h => h != Board.Portal))
                {
                    var t = m.Get(h);
                    bool templateCell = t.Marker == Marker.Rock || t.Marker == Marker.Void;
                    Assert.That(t.Biome == Biome.None, Is.EqualTo(templateCell), "seed " + m.RequestedSeed + " " + h);
                }
            }
        }

        [Test]
        public void B14_B25_GeneratedRockAndVoidAreExactlyTheTemplateCells()
        {
            foreach (var m in maps)
            {
                var def = Catalog.MapTemplates[m.TemplateId];
                var rocks = def.UpperRocks.Concat(def.UpperRocks.Select(h => h.Mirror()));
                var voids = def.UpperVoids.Concat(def.UpperVoids.Select(h => h.Mirror()));
                Assert.That(Find(m, Marker.Rock, false), Is.EquivalentTo(rocks), "seed " + m.RequestedSeed);
                Assert.That(Find(m, Marker.Void, false), Is.EquivalentTo(voids), "seed " + m.RequestedSeed);
            }
        }

        // ---------- Templates (B-25) ----------

        [Test]
        public void B25_TemplateIsSeedMod4_AndStoredOnTheMap()
        {
            Assert.That(Catalog.MapTemplates.Count, Is.EqualTo(4));
            foreach (var m in maps)
                Assert.That(m.TemplateId, Is.EqualTo((int)(m.RequestedSeed % 4)));
            Assert.That(MapGenerator.Generate(ulong.MaxValue).TemplateId, Is.EqualTo((int)(ulong.MaxValue % 4)));
        }

        [Test]
        public void B25_TemplateCells_AreUpperHalf_NotHome_NotInSpecialBand_AndDistinct()
        {
            foreach (var def in Catalog.MapTemplates)
            {
                var all = def.UpperRocks.Concat(def.UpperVoids).ToList();
                Assert.That(all, Is.Unique, def.Name);
                foreach (var h in all)
                {
                    Assert.That(Board.IsInUpperHalf(h), Is.True, def.Name + " " + h);
                    Assert.That(Board.IsHomeZone(h, PlayerId.A) || Board.IsHomeZone(h, PlayerId.B), Is.False, def.Name + " " + h);
                    // The special band (r = -5..-3) keeps all its cells.
                    Assert.That(Board.GddRow(h) >= Catalog.SpecialTileRowMin && Board.GddRow(h) <= Catalog.SpecialTileRowMax,
                        Is.False, def.Name + " " + h);
                }
            }
            // Plain has nothing; RockyRing rocks are at distance 2; IndentedEdges has only Void; TwoCorridors rocks lie on r = -2.
            Assert.That(Catalog.MapTemplates[0].UpperRocks.Count + Catalog.MapTemplates[0].UpperVoids.Count, Is.EqualTo(0));
            Assert.That(Catalog.MapTemplates[1].UpperRocks.All(h => Hex.Distance(h, Board.Portal) == 2), Is.True);
            Assert.That(Catalog.MapTemplates[1].UpperVoids, Is.Empty);
            Assert.That(Catalog.MapTemplates[2].UpperRocks, Is.Empty);
            Assert.That(Catalog.MapTemplates[2].UpperVoids, Is.Not.Empty);
            Assert.That(Catalog.MapTemplates[3].UpperRocks.All(h => h.R == -2), Is.True);
            Assert.That(Catalog.MapTemplates[3].UpperVoids, Is.Empty);
        }

        [Test]
        public void B25_HomeZonesComplete_ConnectedAndPortalReachableFromEveryHomeCell()
        {
            foreach (var m in maps)
                foreach (var p in new[] { PlayerId.A, PlayerId.B })
                {
                    var home = Board.Cells.Where(h => Board.IsHomeZone(h, p)).ToList();
                    Assert.That(home.Count, Is.EqualTo(17));
                    Assert.That(home.All(h => m.Get(h).Marker == Marker.None), Is.True, "seed " + m.RequestedSeed);
                    // Connected inside the home zone.
                    var inHome = HexSearch.Reachable(home[0], 99, h => Board.IsHomeZone(h, p) && m.IsPassable(h), _ => true);
                    Assert.That(inHome.Count + 1, Is.EqualTo(home.Count), "seed " + m.RequestedSeed + " " + p);
                    // Portal reachable without units (BFS over passable cells).
                    foreach (var h in home)
                        Assert.That(HexSearch.Reachable(h, 99, m.IsPassable, _ => true), Does.Contain(Board.Portal),
                            "seed " + m.RequestedSeed + " " + h);
                }
        }

        [Test]
        public void B25_PlayableCells_ExcludeVoidOnly_InBoardOrder()
        {
            foreach (var m in maps.Take(8))
            {
                var expected = Board.Cells.Where(h => m.Get(h).Marker != Marker.Void).ToList();
                Assert.That(m.PlayableCells, Is.EqualTo(expected));
                foreach (var h in Board.Cells) Assert.That(m.IsPlayable(h), Is.EqualTo(expected.Contains(h)));
            }
            Assert.That(maps.Where(m => m.TemplateId == 2).All(m => m.PlayableCells.Count == 169 - 18), Is.True);
            Assert.That(maps.Where(m => m.TemplateId != 2).All(m => m.PlayableCells.Count == 169), Is.True);
        }

        // ---------- Biomes (B-22, B-25) ----------

        [Test]
        public void B25_EachBiomeAtLeastFloor028NInUpperHalf()
        {
            foreach (var m in maps)
                foreach (var b in Biomes)
                    Assert.That(Board.Cells.Count(h => Board.IsInUpperHalf(h) && m.Get(h).Biome == b),
                        Is.GreaterThanOrEqualTo(Catalog.MinCellsPerBiome(N(m))), "seed " + m.RequestedSeed + " " + b);
            Assert.That(maps.Select(N).Distinct().OrderBy(x => x), Is.EqualTo(new[] { 75, 80, 84 }));
        }

        [Test]
        public void B22_SmallClusters_V30LimitsPerSeed_PerTemplate_Seeds1To1000()
        {
            for (int t = 0; t < Catalog.MapTemplates.Count; t++)
            {
                var ms = OfTemplate(maps, t).ToList();
                int n = N(ms[0]);
                int clusterCount = 0, cellCount = 0, maxCluster = 0;
                long largestSum = 0;
                foreach (var m in ms)
                {
                    var clusters = UpperClusters(m);
                    clusterCount += clusters.Count;
                    cellCount += clusters.Sum(c => c.Count);
                    largestSum += clusters.Max(c => c.Count);
                    maxCluster = System.Math.Max(maxCluster, clusters.Max(c => c.Count));
                }
                Assert.That(cellCount, Is.EqualTo(n * ms.Count));
                double mean = (double)cellCount / clusterCount;
                double meanLargest = (double)largestSum / ms.Count;
                TestContext.Out.WriteLine($"[stats] template {t} N={n}: mean cluster size {mean:F2}, mean largest cluster {meanLargest:F2}, max cluster {maxCluster}");
                // PM (v3.0 board): mean <= 7.5, mean largest <= 20 with 63 cells and 6 seeds per biome on average (3.5 cells per
                // seed). v3.3 keeps the same limits per seed: both scale with the cells per seed, (N / 21) / 3.5 = N / 73.5
                // (7 seeds per biome on average, B-25).
                double scale = n / 73.5;
                Assert.That(mean, Is.LessThanOrEqualTo(7.5 * scale), "template " + t);
                Assert.That(meanLargest, Is.LessThanOrEqualTo(20.0 * scale), "template " + t);
            }
        }

        // ---------- Special tiles (B-23) ----------

        [Test]
        public void B23_TwoRunesOneWellspringPerHalf_InGddRows3to5_And11to13()
        {
            foreach (var m in maps)
                foreach (var p in new[] { PlayerId.A, PlayerId.B })
                {
                    var runes = Find(m, Marker.RuneStone, false).Where(h => Board.IsInHalf(h, p)).ToList();
                    var wells = Find(m, Marker.Wellspring, false).Where(h => Board.IsInHalf(h, p)).ToList();
                    Assert.That(runes.Count, Is.EqualTo(Catalog.RuneStonesPerHalf));
                    Assert.That(wells.Count, Is.EqualTo(Catalog.WellspringsPerHalf));
                    var rows = p == PlayerId.B ? new[] { 3, 4, 5 } : new[] { 11, 12, 13 };
                    var axialR = p == PlayerId.B ? new[] { -5, -4, -3 } : new[] { 3, 4, 5 };
                    foreach (var h in runes.Concat(wells))
                    {
                        Assert.That(rows, Does.Contain(Board.GddRow(h)), "seed " + m.RequestedSeed);
                        Assert.That(axialR, Does.Contain(h.R), "seed " + m.RequestedSeed);
                        Assert.That(m.Get(h).Biome, Is.Not.EqualTo(Biome.None)); // markers keep the biome
                    }
                }
        }

        [Test]
        public void B23_RunesNotAdjacent_NoSpecialInHomeZone()
        {
            foreach (var m in maps)
            {
                var runes = Find(m, Marker.RuneStone, false);
                Assert.That(runes.Count, Is.EqualTo(2 * Catalog.RuneStonesPerHalf));
                foreach (var a in runes)
                    foreach (var b in runes)
                        if (a != b) Assert.That(Hex.Distance(a, b), Is.GreaterThan(1), "seed " + m.RequestedSeed);

                foreach (var h in runes.Concat(Find(m, Marker.Wellspring, false)))
                    Assert.That(Board.IsHomeZone(h, PlayerId.A) || Board.IsHomeZone(h, PlayerId.B), Is.False);
            }
        }

        [Test]
        public void B23_WellspringNotAdjacentToRune()
        {
            foreach (var m in maps)
            {
                var runes = Find(m, Marker.RuneStone, false);
                foreach (var w in Find(m, Marker.Wellspring, false))
                    foreach (var r in runes)
                        Assert.That(Hex.Distance(w, r), Is.GreaterThan(1), "seed " + m.RequestedSeed);
            }
        }

        // ---------- Validator negatives (B-24) ----------

        [Test]
        public void B24_ValidatorRejectsAsymmetry()
        {
            var m = MapGenerator.Generate(1);
            var upper = Board.Cells.First(h => Board.IsInUpperHalf(h) && m.Get(h).Marker == Marker.None);
            var lower = upper.Mirror();
            var other = m.Get(lower).Biome == Biome.Forest ? Biome.Snow : Biome.Forest;
            m.Set(lower, new Tile(other, Marker.None)); // upper-half biome counts unchanged

            var errors = MapValidator.Validate(m);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.All.StartsWith("Symmetry:"));
        }

        [Test]
        public void B24_ValidatorRejectsBiomeUnderMinimum()
        {
            var m = MapGenerator.Generate(1);
            var upper = Board.Cells.Where(Board.IsInUpperHalf).ToList();
            var smallest = Biomes.OrderBy(b => upper.Count(h => m.Get(h).Biome == b)).First();
            var target = smallest == Biome.Forest ? Biome.Desert : Biome.Forest;
            foreach (var h in upper.Where(h => m.Get(h).Biome == smallest).ToList())
            {
                if (upper.Count(c => m.Get(c).Biome == smallest) < Catalog.MinCellsPerBiome(N(m))) break;
                SetPair(m, h, new Tile(target, m.Get(h).Marker));
            }

            var errors = MapValidator.Validate(m);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.All.StartsWith("Biome count:"));
            Assert.That(errors, Has.Some.Contains(smallest.ToString()));
        }

        [Test]
        public void B24_ValidatorRejectsAdjacentRunes()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var m = MapGenerator.Generate(seed);
                var runes = Find(m, Marker.RuneStone, true);
                var well = Find(m, Marker.Wellspring, true).Single();
                // Move rune 2 next to rune 1, onto a band cell that is not next to the wellspring.
                var to = Board.Cells.FirstOrDefault(h => InBand(h) && Hex.Distance(h, runes[0]) == 1
                    && h != runes[1] && h != well && Hex.Distance(h, well) > 1);
                if (!Board.IsOnBoard(to) || to == Board.Portal) continue;
                MoveMarkerPair(m, runes[1], to);

                var errors = MapValidator.Validate(m);
                Assert.That(errors, Is.Not.Empty);
                Assert.That(errors, Has.All.StartsWith("Adjacent runes:"));
                return;
            }
            Assert.Fail("No suitable map found in seeds 1..100.");
        }

        [Test]
        public void B24_ValidatorRejectsSpecialInHomeZone()
        {
            var m = MapGenerator.Generate(1);
            var well = Find(m, Marker.Wellspring, true).Single();
            // GDD row 1 is far from the rune band, so only home-zone/row errors can appear.
            var to = Board.Cells.First(h => Board.GddRow(h) == 1);
            MoveMarkerPair(m, well, to);

            var errors = MapValidator.Validate(m);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.Some.StartsWith("Special in home zone:"));
            Assert.That(errors.All(e => e.StartsWith("Special in home zone:") || e.StartsWith("Special row:")), Is.True,
                string.Join("\n", errors));
        }

        [Test]
        public void B24_ValidatorRejectsWrongSpecialCounts()
        {
            var m = MapGenerator.Generate(1);
            var rune = Find(m, Marker.RuneStone, true)[0];
            SetPair(m, rune, new Tile(m.Get(rune).Biome, Marker.None));

            var errors = MapValidator.Validate(m);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.All.StartsWith("Special count:"));
            Assert.That(errors, Has.Some.Contains("RuneStone"));

            var m2 = MapGenerator.Generate(1);
            var well = Find(m2, Marker.Wellspring, true)[0];
            SetPair(m2, well, new Tile(m2.Get(well).Biome, Marker.None));
            var errors2 = MapValidator.Validate(m2);
            Assert.That(errors2, Is.Not.Empty);
            Assert.That(errors2, Has.All.StartsWith("Special count:"));
            Assert.That(errors2, Has.Some.Contains("Wellspring"));
        }

        [Test]
        public void B24_ValidatorRejectsWellspringAdjacentToRune()
        {
            var m = MapGenerator.Generate(1);
            var runes = Find(m, Marker.RuneStone, true);
            var well = Find(m, Marker.Wellspring, true).Single();
            var to = Board.Cells.First(h => InBand(h) && Hex.Distance(h, runes[0]) == 1 && !runes.Contains(h));
            MoveMarkerPair(m, well, to);

            var errors = MapValidator.Validate(m);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.All.StartsWith("Wellspring next to rune:"));
        }

        [Test]
        public void B24_ValidatorRejectsRockAndSecondPortal()
        {
            var m = MapGenerator.Generate(1);
            var h = Board.Cells.First(c => Board.IsInUpperHalf(c) && m.Get(c).Marker == Marker.None);
            SetPair(m, h, new Tile(m.Get(h).Biome, Marker.Rock));
            Assert.That(MapValidator.Validate(m), Has.All.StartsWith("Rock:").And.Not.Empty);

            var m2 = MapGenerator.Generate(1);
            SetPair(m2, h, new Tile(Biome.None, Marker.Portal));
            Assert.That(MapValidator.Validate(m2), Has.Some.StartsWith("Portal:"));
        }

        [Test]
        public void B25_ValidatorRejectsVoidOutsideTheTemplate_AndMissingTemplateRock()
        {
            var m = MapGenerator.Generate(4); // template 0 (Plain)
            Assert.That(m.TemplateId, Is.EqualTo(0));
            var h = Board.Cells.First(c => Board.IsInUpperHalf(c) && !InBand(c) && m.Get(c).Marker == Marker.None
                && Board.GddRow(c) > 5);
            SetPair(m, h, new Tile(Biome.None, Marker.Void));
            Assert.That(MapValidator.Validate(m), Has.Some.StartsWith("Template:"));

            var m2 = MapGenerator.Generate(1); // template 1 (RockyRing)
            var rock = Catalog.MapTemplates[1].UpperRocks[0];
            SetPair(m2, rock, new Tile(Biome.Forest, Marker.None));
            Assert.That(MapValidator.Validate(m2), Has.Some.StartsWith("Template:"));
        }

        [Test]
        public void B25_ValidatorRejectsPortalUnreachableFromHome()
        {
            var m = MapGenerator.Generate(1); // template 1: close the ring gaps (0,-2), (2,-2) and their mirrors
            foreach (var h in new[] { new Hex(0, -2), new Hex(2, -2) })
                SetPair(m, h, new Tile(m.Get(h).Biome, Marker.Rock));
            Assert.That(MapValidator.Validate(m), Has.Some.StartsWith("Reachability:"));
        }
    }
}
