using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>
    /// The whole match state. Read freely; change only through Engine.Apply (mutators are internal).
    /// Units are kept in ascending id order, so every iteration over Units is deterministic.
    /// </summary>
    public sealed class GameState
    {
        public readonly GameMap Map;
        readonly List<Unit> units = new List<Unit>();
        readonly Tower[] towers;
        readonly int[] energy = new int[2];                  // T-05. Mana (M3) is a separate field.
        readonly bool[] towerShotAvailable = { true, true }; // U-27, per tower owner

        public PlayerId ActivePlayer { get; internal set; }
        /// <summary>T-10: 1-based; increments when B ends its turn.</summary>
        public int Round { get; internal set; }
        /// <summary>W-02. Null while the game is running.</summary>
        public PlayerId? Winner { get; internal set; }
        /// <summary>The id the next unit will get. Ids are sequential and never reused.</summary>
        public int NextUnitId { get; private set; } = 1;

        /// <summary>Start of round 1, A's turn, A has full Energy.</summary>
        public GameState(GameMap map, Hex towerA, Hex towerB)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            towers = new[] { new Tower(PlayerId.A, towerA), new Tower(PlayerId.B, towerB) };
            ActivePlayer = PlayerId.A;
            Round = 1;
            energy[(int)PlayerId.A] = Catalog.EnergyPerTurn;
        }

        public bool IsOver => Winner.HasValue;

        public IReadOnlyList<Unit> Units => units;

        public Unit GetUnit(int id)
        {
            foreach (var u in units)
                if (u.Id == id) return u;
            return null;
        }

        public Unit UnitAt(Hex h)
        {
            foreach (var u in units)
                if (u.Pos == h) return u;
            return null;
        }

        public Tower GetTower(PlayerId p) => towers[(int)p];

        public Tower TowerAt(Hex h)
        {
            foreach (var t in towers)
                if (t.Pos == h) return t;
            return null;
        }

        public int GetEnergy(PlayerId p) => energy[(int)p];
        internal void SetEnergy(PlayerId p, int value) => energy[(int)p] = value;

        public bool IsTowerShotAvailable(PlayerId towerOwner) => towerShotAvailable[(int)towerOwner];
        internal void SetTowerShotAvailable(PlayerId towerOwner, bool value) => towerShotAvailable[(int)towerOwner] = value;

        /// <summary>Places a new unit with full Health (test setup now, deploy in M3). No rule checks here.</summary>
        internal Unit AddUnit(PlayerId owner, UnitClass cls, Biome biome, Hex pos)
        {
            var u = new Unit(NextUnitId++, owner, cls, biome, pos);
            units.Add(u); // ids ascend, so the list stays in id order
            return u;
        }

        internal void RemoveUnit(Unit u) => units.Remove(u);
    }
}
