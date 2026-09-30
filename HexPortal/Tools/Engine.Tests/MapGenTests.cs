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

        static List<List<Hex>> UpperClusters(GameMap m)
        {
            var seen = new HashSet<Hex>();
            var clusters = new List<List<Hex>>();
            foreach (var start in Board.Cells.Where(Board.IsInUpperHalf))
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
                        if (Board.IsInUpperHalf(n) && m.Get(n).Biome == biome && seen.Add(n)) queue.Enqueue(n);
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
        public void B24_AtLeast90PercentValidOnFirstAttempt_Seeds1To1000()
        {
            int first = maps.Count(m => m.Attempts == 1);
            TestContext.Out.WriteLine($"[stats] valid on first attempt: {first}/{SeedCount} = {100.0 * first / SeedCount:F1}%");
            TestContext.Out.WriteLine($"[stats] max attempts: {maps.Max(m => m.Attempts)}, mean attempts: {maps.Average(m => m.Attempts):F3}");
            Assert.That(first, Is.GreaterThanOrEqualTo(SeedCount * 90 / 100));
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
                foreach (var h in Board.Cells.Where(h => h != Board.Portal))
                    Assert.That(m.Get(h).Biome, Is.Not.EqualTo(Biome.None), h.ToString());
            }
        }

        [Test]
        public void B14_GeneratedMapsHaveNoRock()
        {
            foreach (var m in maps) Assert.That(Find(m, Marker.Rock, false), Is.Empty);
        }

        // ---------- Biomes (B-22) ----------

        [Test]
        public void B22_EachBiomeAtLeast8InUpperHalf()
        {
            foreach (var m in maps)
                foreach (var b in Biomes)
                    Assert.That(Board.Cells.Count(h => Board.IsInUpperHalf(h) && m.Get(h).Biome == b),
                        Is.GreaterThanOrEqualTo(Catalog.MinCellsPerBiome), "seed " + m.RequestedSeed + " " + b);
        }

        [Test]
        public void B22_MeanClusterSizeAtMost6_Seeds1To1000()
        {
            int clusterCount = 0, cellCount = 0;
            long largestSum = 0;
            foreach (var m in maps)
            {
                var clusters = UpperClusters(m);
                clusterCount += clusters.Count;
                cellCount += clusters.Sum(c => c.Count);
                largestSum += clusters.Max(c => c.Count);
            }
            Assert.That(cellCount, Is.EqualTo(29 * SeedCount));
            double mean = (double)cellCount / clusterCount;
            TestContext.Out.WriteLine($"[stats] mean cluster size: {mean:F2}, mean largest cluster: {(double)largestSum / SeedCount:F2}");
            Assert.That(mean, Is.LessThanOrEqualTo(6.0));
        }

        // ---------- Special tiles (B-23) ----------

        [Test]
        public void B23_TwoRunesOneWellspringPerHalf_InGddRows3to4_And6to7()
        {
            foreach (var m in maps)
                foreach (var p in new[] { PlayerId.A, PlayerId.B })
                {
                    var runes = Find(m, Marker.RuneStone, false).Where(h => Board.IsInHalf(h, p)).ToList();
                    var wells = Find(m, Marker.Wellspring, false).Where(h => Board.IsInHalf(h, p)).ToList();
                    Assert.That(runes.Count, Is.EqualTo(Catalog.RuneStonesPerHalf));
                    Assert.That(wells.Count, Is.EqualTo(Catalog.WellspringsPerHalf));
                    var rows = p == PlayerId.B ? new[] { 3, 4 } : new[] { 6, 7 };
                    foreach (var h in runes.Concat(wells))
                    {
                        Assert.That(rows, Does.Contain(Board.GddRow(h)), "seed " + m.RequestedSeed);
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
        public void B24_ValidatorRejectsBiomeUnder8()
        {
            var m = MapGenerator.Generate(1);
            var upper = Board.Cells.Where(Board.IsInUpperHalf).ToList();
            var smallest = Biomes.OrderBy(b => upper.Count(h => m.Get(h).Biome == b)).First();
            var target = smallest == Biome.Forest ? Biome.Desert : Biome.Forest;
            foreach (var h in upper.Where(h => m.Get(h).Biome == smallest).ToList())
            {
                if (upper.Count(c => m.Get(c).Biome == smallest) < Catalog.MinCellsPerBiome) break;
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
    }
}
