using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§10 map events. At the start of every round, before A's turn start (v2.8): the pending event of this round
    /// happens (E-01), then the event of round + EventAnnounceLead is announced (E-02). Everything is public (E-05).
    /// E-04 (v2.9): targets are chosen at the announcement by TERRAIN only; occupancy ("empty" = no unit or tower) is
    /// checked only when the event happens, and a pair that fails is skipped.
    /// Cells: E-10/E-12 list the pairs as (cell, mirror, cell, mirror, ...); E-11 lists the cluster (center first, then its
    /// neighbours in direction order) followed by the mirror of each, in the same order. All randomness: the match Rng.</summary>
    public static class MapEvents
    {
        static readonly Biome[] Biomes = { Biome.Forest, Biome.Desert, Biome.Snow };

        internal static void RoundStart(GameState state, EventLog events)
        {
            if (state.PendingEvent != null && state.PendingEventRound == state.Round) Resolve(state, events);
            foreach (int r in Catalog.EventRounds)
                if (r - Catalog.EventAnnounceLead == state.Round) Announce(state, r, events);
        }

        // ---------- Targets ----------

        static bool IsHome(Hex h) => Board.IsHomeZone(h, PlayerId.A) || Board.IsHomeZone(h, PlayerId.B);

        /// <summary>E-10, E-12 terrain: outside both home zones, no marker (not rune, wellspring, Portal or rock).</summary>
        static bool IsPairTerrainEligible(GameState state, Hex h) =>
            !IsHome(h) && state.Map.Get(h).Marker == Marker.None && !IsHome(h.Mirror()) && state.Map.Get(h.Mirror()).Marker == Marker.None;

        /// <summary>E-04 at resolution: the terrain is still eligible and both cells are empty (no unit or tower; traps
        /// allowed, v2.8).</summary>
        static bool IsPairEligibleNow(GameState state, Hex h) =>
            IsPairTerrainEligible(state, h) && IsEmpty(state, h) && IsEmpty(state, h.Mirror());

        static bool IsEmpty(GameState state, Hex h) => state.UnitAt(h) == null && state.TowerAt(h) == null;

        /// <summary>Candidate targets of a type at the announcement, by terrain only (v2.9), one representative per mirror
        /// pair (the upper-half cell, B-21): terrain-eligible pairs for E-10/E-12, cluster centers at
        /// BiomeShiftMinPortalDistance or more for E-11.</summary>
        internal static List<Hex> Candidates(GameState state, MapEventDef def)
        {
            var list = new List<Hex>();
            foreach (var h in Board.Cells)
            {
                if (!Board.IsInUpperHalf(h)) continue;
                if (IsCluster(def) ? Hex.Distance(h, Board.Portal) >= Catalog.BiomeShiftMinPortalDistance : IsPairTerrainEligible(state, h))
                    list.Add(h);
            }
            return list;
        }

        static bool IsCluster(MapEventDef def) => def.Id == "E-11";

        /// <summary>E-11: the center and its on-board neighbours except the Portal (at most BiomeShiftMaxCells).</summary>
        static List<Hex> Cluster(Hex center)
        {
            var cells = new List<Hex> { center };
            for (int d = 0; d < Hex.Directions.Count && cells.Count < Catalog.BiomeShiftMaxCells; d++)
            {
                var n = center.Neighbor(d);
                if (Board.IsOnBoard(n) && n != Board.Portal) cells.Add(n);
            }
            return cells;
        }

        // ---------- Announcement (E-02, E-03, E-04) ----------

        /// <summary>E-03: a random type; if it has no target, a random type among those that have one (v2.8, terrain only
        /// v2.9); none: no event.</summary>
        static void Announce(GameState state, int round, EventLog events)
        {
            var eligible = new List<MapEventDef>();
            foreach (var d in Catalog.MapEvents)
                if (Candidates(state, d).Count > 0) eligible.Add(d);
            if (eligible.Count == 0) return;
            var def = Catalog.MapEvents[state.Rng.NextInt(Catalog.MapEvents.Count)];
            if (!eligible.Contains(def)) def = eligible[state.Rng.NextInt(eligible.Count)];

            var candidates = Candidates(state, def);
            var cells = new List<Hex>();
            if (IsCluster(def))
            {
                var half = Cluster(candidates[state.Rng.NextInt(candidates.Count)]);
                cells.AddRange(half);
                foreach (var h in half) cells.Add(h.Mirror());
            }
            else
                for (int k = 0; k < def.Pairs && candidates.Count > 0; k++)
                {
                    int i = state.Rng.NextInt(candidates.Count);
                    cells.Add(candidates[i]);
                    cells.Add(candidates[i].Mirror());
                    candidates.RemoveAt(i);
                }
            state.SetPendingEvent(def, round, cells);
            events.Add(new MapEventAnnounced(def.Id, round, new List<Hex>(cells)));
        }

        // ---------- Resolution (E-01, E-04, E-10…E-12) ----------

        static void Resolve(GameState state, EventLog events)
        {
            var def = state.PendingEvent;
            var cells = new List<Hex>(state.PendingEventCells);
            state.SetPendingEvent(null, 0, null);
            var changed = new List<Hex>();
            var skipped = new List<Hex>();
            bool cluster = IsCluster(def);
            if (cluster)
            {
                // E-11 (v2.8, v2.9): one random biome other than the center's current one, chosen now; markers are kept.
                var from = state.Map.Get(cells[0]).Biome;
                var options = new List<Biome>();
                foreach (var b in Biomes)
                    if (b != from) options.Add(b);
                var biome = options[state.Rng.NextInt(options.Count)];
                foreach (var h in cells)
                {
                    state.Map.Set(h, new Tile(biome, state.Map.Get(h).Marker));
                    changed.Add(h);
                }
            }
            else
                for (int i = 0; i + 1 < cells.Count; i += 2)
                {
                    if (!IsPairEligibleNow(state, cells[i])) // E-04: the whole pair is skipped
                    {
                        skipped.Add(cells[i]);
                        skipped.Add(cells[i + 1]);
                        continue;
                    }
                    var marker = def.Id == "E-10" ? Marker.Rock : Marker.RuneStone;
                    foreach (var h in new[] { cells[i], cells[i + 1] })
                    {
                        if (marker == Marker.Rock) Traps.RemoveAt(state, h, events); // E-10, C-35 (owner only)
                        state.Map.Set(h, new Tile(state.Map.Get(h).Biome, marker)); // E-12 keeps traps
                        changed.Add(h);
                    }
                }
            var tiles = new List<Tile>();
            foreach (var h in changed) tiles.Add(state.Map.Get(h));
            events.Add(new MapEventResolved(def.Id, changed, tiles, skipped));
            // E-05 (v2.10): E-10/E-12 prove their cells empty, so ghosts there are removed; E-11 keeps them.
            Visibility.TerrainChanged(state, changed, !cluster);
        }
    }
}
