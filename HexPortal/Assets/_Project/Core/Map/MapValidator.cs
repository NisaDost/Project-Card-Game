using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>B-24: checks symmetry (B-04), tile kinds (B-13, B-14), biome counts (B-22) and special tiles (B-23).</summary>
    public static class MapValidator
    {
        static readonly Biome[] Biomes = { Biome.Forest, Biome.Desert, Biome.Snow };

        /// <summary>Human-readable errors, each starting with a fixed "Category:" prefix. Empty = valid.</summary>
        public static List<string> Validate(GameMap map)
        {
            var errors = new List<string>();

            foreach (var h in Board.Cells)
            {
                if (!Board.IsInUpperHalf(h)) continue; // every non-portal cell is upper or the mirror of one
                Tile t = map.Get(h), m = map.Get(h.Mirror());
                if (t.Biome != m.Biome || t.Marker != m.Marker)
                    errors.Add("Symmetry: " + h + " is " + t + " but mirror " + h.Mirror() + " is " + m);
            }

            var portal = map.Get(Board.Portal);
            if (portal.Biome != Biome.None || portal.Marker != Marker.Portal)
                errors.Add("Portal: " + Board.Portal + " must be None/Portal, is " + portal);

            var runes = new List<Hex>();
            var specials = new List<Hex>();
            foreach (var h in Board.Cells)
            {
                if (h == Board.Portal) continue;
                var t = map.Get(h);
                if (t.Marker == Marker.Portal) errors.Add("Portal: extra Portal at " + h);
                if (t.Marker == Marker.Rock) errors.Add("Rock: " + h + " (only map events create rock)");
                if (t.Biome == Biome.None) errors.Add("No biome: " + h);
                if (t.Marker == Marker.RuneStone) runes.Add(h);
                if (t.Marker == Marker.RuneStone || t.Marker == Marker.Wellspring) specials.Add(h);
            }

            foreach (var b in Biomes)
            {
                int n = 0;
                foreach (var h in Board.Cells)
                    if (Board.IsInUpperHalf(h) && map.Get(h).Biome == b) n++;
                if (n < Catalog.MinCellsPerBiome)
                    errors.Add("Biome count: " + b + " has " + n + " cells in the upper half (min " + Catalog.MinCellsPerBiome + ")");
            }

            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                int r = 0, w = 0;
                foreach (var h in specials)
                    if (Board.IsInHalf(h, p))
                    {
                        if (map.Get(h).Marker == Marker.RuneStone) r++;
                        else w++;
                    }
                if (r != Catalog.RuneStonesPerHalf)
                    errors.Add("Special count: half " + p + " has " + r + " RuneStone(s), expected " + Catalog.RuneStonesPerHalf);
                if (w != Catalog.WellspringsPerHalf)
                    errors.Add("Special count: half " + p + " has " + w + " Wellspring(s), expected " + Catalog.WellspringsPerHalf);
            }

            foreach (var h in specials)
            {
                var marker = map.Get(h).Marker;
                // Upper-half row, measured on the mirror for lower-half cells.
                int row = Board.GddRow(Board.IsInUpperHalf(h) ? h : h.Mirror());
                if (row < Catalog.SpecialTileRowMin || row > Catalog.SpecialTileRowMax)
                    errors.Add("Special row: " + marker + " at " + h + " (GDD row " + Board.GddRow(h) + ")");
                if (Board.IsHomeZone(h, PlayerId.A) || Board.IsHomeZone(h, PlayerId.B))
                    errors.Add("Special in home zone: " + marker + " at " + h);
            }

            for (int i = 0; i < runes.Count; i++)
                for (int j = i + 1; j < runes.Count; j++)
                    if (Hex.Distance(runes[i], runes[j]) == 1)
                        errors.Add("Adjacent runes: " + runes[i] + " and " + runes[j]);

            foreach (var h in specials)
            {
                if (map.Get(h).Marker != Marker.Wellspring) continue;
                foreach (var r in runes)
                    if (Hex.Distance(h, r) == 1)
                        errors.Add("Wellspring next to rune: " + h + " and " + r);
            }

            return errors;
        }
    }
}
