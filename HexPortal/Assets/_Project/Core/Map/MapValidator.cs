using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>B-24: checks symmetry (B-04), tile kinds (B-13, B-14), the template (B-25), biome counts (B-22, B-25),
    /// special tiles (B-23, B-25) and reachability (B-25: complete, connected home zones; the Portal reachable from every
    /// home cell).</summary>
    public static class MapValidator
    {
        static readonly Biome[] Biomes = { Biome.Forest, Biome.Desert, Biome.Snow };

        /// <summary>Human-readable errors, each starting with a fixed "Category:" prefix. Empty = valid.</summary>
        public static List<string> Validate(GameMap map)
        {
            var errors = new List<string>();
            var template = Catalog.MapTemplates[map.TemplateId];
            var templateRock = new HashSet<Hex>();
            var templateVoid = new HashSet<Hex>();
            foreach (var h in template.UpperRocks) { templateRock.Add(h); templateRock.Add(h.Mirror()); }
            foreach (var h in template.UpperVoids) { templateVoid.Add(h); templateVoid.Add(h.Mirror()); }
            var templateRiver = new HashSet<Hex>();
            var templateLava = new HashSet<Hex>();
            foreach (var h in template.UpperRivers) { templateRiver.Add(h); templateRiver.Add(h.Mirror()); }
            foreach (var h in template.UpperLavas) { templateLava.Add(h); templateLava.Add(h.Mirror()); }

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
            int playableUpper = 0;
            foreach (var h in Board.Cells)
            {
                if (h == Board.Portal) continue;
                var t = map.Get(h);
                bool isRock = t.Marker == Marker.Mountain, isVoid = t.Marker == Marker.Void;
                if (t.Marker == Marker.Portal) errors.Add("Portal: extra Portal at " + h);
                if (isRock && !templateRock.Contains(h)) errors.Add("Rock: " + h + " (only map events and the template create rock)");
                if (!isRock && templateRock.Contains(h)) errors.Add("Template: " + h + " must be rock, is " + t);
                if (isVoid != templateVoid.Contains(h)) errors.Add("Template: " + h + (isVoid ? " is Void outside the template" : " must be Void, is " + t));
                if (isRock && templateRock.Contains(h) && t.Biome != Biome.None) errors.Add("Template: rock " + h + " has a biome");
                if (!MapGenerator.IsTemplateTerrain(t.Marker) && t.Biome == Biome.None) errors.Add("No biome: " + h);
                if (isVoid && t.Biome != Biome.None) errors.Add("Template: Void " + h + " has a biome");
                // B-15, B-16: River and Lava exist only where the template puts them, without a biome.
                bool isRiver = t.Marker == Marker.River, isLava = t.Marker == Marker.Lava;
                if (isRiver != templateRiver.Contains(h)) errors.Add("Template: " + h + (isRiver ? " is River outside the template" : " must be River, is " + t));
                if (isLava != templateLava.Contains(h)) errors.Add("Template: " + h + (isLava ? " is Lava outside the template" : " must be Lava, is " + t));
                if ((isRiver || isLava) && t.Biome != Biome.None) errors.Add("Template: " + t.Marker + " " + h + " has a biome");
                if (Board.IsInUpperHalf(h) && !isRock && !isVoid && !isRiver && !isLava) playableUpper++;
                if (t.Marker == Marker.RuneStone) runes.Add(h);
                if (t.Marker == Marker.RuneStone || t.Marker == Marker.Wellspring) specials.Add(h);
            }

            int minPerBiome = Catalog.MinCellsPerBiome(playableUpper);
            foreach (var b in Biomes)
            {
                int n = 0;
                foreach (var h in Board.Cells)
                    if (Board.IsInUpperHalf(h) && map.Get(h).Biome == b) n++;
                if (n < minPerBiome)
                    errors.Add("Biome count: " + b + " has " + n + " cells in the upper half (min " + minPerBiome + ")");
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

            AddReachabilityErrors(map, errors);
            return errors;
        }

        // B-25: the home zones are connected and the Portal is reachable from every home cell (BFS without units over passable
        // cells). Rock or Void in a home zone is already reported by "Rock:" / "Template:" (template cells are never home cells).

        static void AddReachabilityErrors(GameMap map, List<string> errors)
        {
            var fromPortal = new HashSet<Hex>(HexSearch.Reachable(Board.Portal, int.MaxValue, map.IsPassable, _ => true));
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                Hex? first = null;
                int count = 0;
                foreach (var h in Board.Cells)
                {
                    if (!Board.IsHomeZone(h, p) || !map.IsPassable(h)) continue;
                    count++;
                    if (!first.HasValue) first = h;
                    if (!fromPortal.Contains(h)) errors.Add("Reachability: Portal unreachable from " + h);
                }
                if (!first.HasValue) { errors.Add("Reachability: home zone of " + p + " has no passable cell"); continue; }
                var inside = HexSearch.Reachable(first.Value, int.MaxValue,
                    h => Board.IsHomeZone(h, p) && map.IsPassable(h), _ => true);
                if (inside.Count + 1 != count) errors.Add("Reachability: home zone of " + p + " is not connected");
            }
        }
    }
}
