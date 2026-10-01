using System;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>V-01: how a player sees a cell.</summary>
    public enum CellVisibility { Hidden, Explored, Visible }

    /// <summary>V-03, V-05 (v2.8 V2): a cell as the player last saw it: terrain, and the enemy unit (owner, class, biome,
    /// Health at that moment; no effects, no overwatch) or enemy tower on it. The viewer's own pieces are never stored.
    /// UnitId 0 = no unit.</summary>
    public readonly struct LastSeen : IEquatable<LastSeen>
    {
        public readonly Tile Tile;
        public readonly int UnitId;
        public readonly PlayerId UnitOwner;
        public readonly UnitClass UnitClass;
        public readonly Biome UnitBiome;
        public readonly int UnitHealth;
        public readonly bool HasTower;
        public readonly PlayerId TowerOwner;
        public readonly int TowerHealth;

        public LastSeen(Tile tile, int unitId, PlayerId unitOwner, UnitClass unitClass, Biome unitBiome, int unitHealth,
            bool hasTower, PlayerId towerOwner, int towerHealth)
        {
            Tile = tile; UnitId = unitId; UnitOwner = unitOwner; UnitClass = unitClass; UnitBiome = unitBiome;
            UnitHealth = unitHealth; HasTower = hasTower; TowerOwner = towerOwner; TowerHealth = towerHealth;
        }

        /// <summary>Terrain only.</summary>
        public LastSeen(Tile tile) : this(tile, 0, PlayerId.A, UnitClass.Guardian, Biome.None, 0, false, PlayerId.A, 0) { }

        public bool HasUnit => UnitId != 0;

        /// <summary>The same snapshot on other terrain (E-05).</summary>
        public LastSeen WithTile(Tile tile) =>
            new LastSeen(tile, UnitId, UnitOwner, UnitClass, UnitBiome, UnitHealth, HasTower, TowerOwner, TowerHealth);

        /// <summary>The same snapshot without its unit.</summary>
        public LastSeen WithoutUnit() => new LastSeen(Tile, 0, PlayerId.A, UnitClass.Guardian, Biome.None, 0, HasTower, TowerOwner, TowerHealth);

        public bool Equals(LastSeen o) =>
            Tile.Biome == o.Tile.Biome && Tile.Marker == o.Tile.Marker && UnitId == o.UnitId && UnitOwner == o.UnitOwner
            && UnitClass == o.UnitClass && UnitBiome == o.UnitBiome && UnitHealth == o.UnitHealth && HasTower == o.HasTower
            && TowerOwner == o.TowerOwner && TowerHealth == o.TowerHealth;
        public override bool Equals(object obj) => obj is LastSeen o && Equals(o);
        public override int GetHashCode() => UnitId * 397 ^ (int)Tile.Biome * 31 ^ (int)Tile.Marker;
    }

    /// <summary>V-10: one player's exploration map and last-seen snapshots, indexed like Board.Cells.
    /// Kept in GameState; only Visibility changes it.</summary>
    public sealed class FogMemory
    {
        readonly CellVisibility[] visibility = new CellVisibility[Board.Cells.Count];
        readonly LastSeen[] lastSeen = new LastSeen[Board.Cells.Count];

        public CellVisibility Get(Hex h) => visibility[Index(h)];
        public LastSeen GetLastSeen(Hex h) => lastSeen[Index(h)];

        internal void Set(Hex h, CellVisibility v, LastSeen seen)
        {
            int i = Index(h);
            visibility[i] = v;
            lastSeen[i] = seen;
        }

        internal FogMemory Clone()
        {
            var m = new FogMemory();
            Array.Copy(visibility, m.visibility, visibility.Length);
            Array.Copy(lastSeen, m.lastSeen, lastSeen.Length);
            return m;
        }

        static int Index(Hex h)
        {
            int i = Board.IndexOf(h);
            if (i < 0) throw new ArgumentOutOfRangeException(nameof(h), "Off the board: " + h);
            return i;
        }
    }
}
