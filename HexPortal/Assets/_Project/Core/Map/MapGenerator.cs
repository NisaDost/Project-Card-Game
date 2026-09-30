using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>B-20…B-24 map generation. Uses its own Rng stream seeded from the map seed.</summary>
    public static class MapGenerator
    {
        const int MaxAttempts = 1000;
        // Separates the map stream from other streams seeded from the same match seed (deals, draws).
        const ulong MapStreamSalt = 0x4D41505F53545245UL; // "MAP_STRE"
        static readonly Biome[] Biomes = { Biome.Forest, Biome.Desert, Biome.Snow };

        public static GameMap Generate(ulong seed)
        {
            var rng = new Rng(seed ^ MapStreamSalt);
            // B-24: on failure keep drawing from the same stream (attempt 2, 3, ...), never seed + 1.
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var map = new GameMap(seed, attempt);
                GenerateUpperBiomes(map, rng);
                PlaceSpecials(map, rng);
                MirrorAndPortal(map);
                if (MapValidator.Validate(map).Count == 0) return map;
            }
            throw new InvalidOperationException("No valid map after " + MaxAttempts + " attempts, seed " + seed);
        }

        // B-22: 3–5 seeds per biome on distinct random upper cells, then balanced growth.
        static void GenerateUpperBiomes(GameMap map, Rng rng)
        {
            var empty = new List<Hex>();
            foreach (var h in Board.Cells)
                if (Board.IsInUpperHalf(h)) empty.Add(h);

            var counts = new int[Biomes.Length];
            for (int b = 0; b < Biomes.Length; b++)
            {
                int n = rng.NextInt(Catalog.BiomeSeedsPerBiomeMin, Catalog.BiomeSeedsPerBiomeMax + 1);
                for (int k = 0; k < n; k++)
                {
                    int i = rng.NextInt(empty.Count);
                    map.Set(empty[i], new Tile(Biomes[b], Marker.None));
                    empty.RemoveAt(i);
                    counts[b]++;
                }
            }

            var frontiers = new List<Hex>[Biomes.Length];
            var ties = new List<int>();
            while (empty.Count > 0)
            {
                // Among biomes with an empty neighbouring upper cell, grow the one with the fewest cells.
                int min = int.MaxValue;
                for (int b = 0; b < Biomes.Length; b++)
                {
                    frontiers[b] = new List<Hex>();
                    foreach (var h in empty)
                        if (HasNeighborWithBiome(map, h, Biomes[b])) frontiers[b].Add(h);
                    if (frontiers[b].Count > 0 && counts[b] < min) min = counts[b];
                }
                ties.Clear();
                for (int b = 0; b < Biomes.Length; b++)
                    if (frontiers[b].Count > 0 && counts[b] == min) ties.Add(b);
                // Unreachable: the upper half is connected and every biome has seeds.
                if (ties.Count == 0) throw new InvalidOperationException("Upper half is not connected.");

                int grow = ties[rng.NextInt(ties.Count)];
                var cell = frontiers[grow][rng.NextInt(frontiers[grow].Count)];
                map.Set(cell, new Tile(Biomes[grow], Marker.None));
                empty.Remove(cell);
                counts[grow]++;
            }
        }

        static bool HasNeighborWithBiome(GameMap map, Hex h, Biome biome)
        {
            for (int d = 0; d < 6; d++)
            {
                var n = h.Neighbor(d);
                if (Board.IsInUpperHalf(n) && map.Get(n).Biome == biome) return true;
            }
            return false;
        }

        // B-23: runes in the band, not adjacent to each other; wellsprings in the band, not adjacent to any rune.
        static void PlaceSpecials(GameMap map, Rng rng)
        {
            var band = new List<Hex>();
            foreach (var h in Board.Cells)
            {
                int row = Board.GddRow(h);
                if (Board.IsInUpperHalf(h) && row >= Catalog.SpecialTileRowMin && row <= Catalog.SpecialTileRowMax
                    && !Board.IsHomeZone(h, PlayerId.A) && !Board.IsHomeZone(h, PlayerId.B))
                    band.Add(h);
            }

            var runes = new List<Hex>();
            var used = new List<Hex>();
            for (int k = 0; k < Catalog.RuneStonesPerHalf; k++)
            {
                var h = Pick(band, used, runes, rng);
                runes.Add(h);
                used.Add(h);
                map.Set(h, new Tile(map.Get(h).Biome, Marker.RuneStone));
            }
            for (int k = 0; k < Catalog.WellspringsPerHalf; k++)
            {
                var h = Pick(band, used, runes, rng);
                used.Add(h);
                map.Set(h, new Tile(map.Get(h).Biome, Marker.Wellspring));
            }
        }

        // A random band cell that is unused and not adjacent to any of avoidNear.
        static Hex Pick(List<Hex> band, List<Hex> used, List<Hex> avoidNear, Rng rng)
        {
            var candidates = new List<Hex>();
            foreach (var h in band)
            {
                if (used.Contains(h)) continue;
                bool near = false;
                foreach (var a in avoidNear)
                    if (Hex.Distance(a, h) == 1) near = true;
                if (!near) candidates.Add(h);
            }
            // Unreachable with the current Catalog: 2 runes block at most 10 of the 13 band cells.
            if (candidates.Count == 0) throw new InvalidOperationException("No room for special tiles (check Catalog B-23).");
            return candidates[rng.NextInt(candidates.Count)];
        }

        // B-04, B-21: the lower half copies its mirror. B-13: the Portal has no biome.
        static void MirrorAndPortal(GameMap map)
        {
            foreach (var h in Board.Cells)
                if (h != Board.Portal && !Board.IsInUpperHalf(h))
                    map.Set(h, map.Get(h.Mirror()));
            map.Set(Board.Portal, new Tile(Biome.None, Marker.Portal));
        }
    }
}
