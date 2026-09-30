using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Tests
{
    /// <summary>
    /// Builds exact GameState scenarios. Default: flat Forest map (Portal = None/Portal), towers at
    /// doubled (6,8) for A and (6,0) for B, A active in round 1 with full Energy.
    /// Units default to Desert, so they get no biome bonus on the default map.
    /// </summary>
    public sealed class TestBoard
    {
        public static readonly Hex DefaultTowerA = Hex.FromDoubled(6, 8); // (-2,4)
        public static readonly Hex DefaultTowerB = Hex.FromDoubled(6, 0); // (2,-4)

        readonly GameMap map;
        public readonly GameState State;

        public TestBoard(Core.Data.Biome biome = Core.Data.Biome.Forest)
        {
            map = new GameMap(0, 1);
            foreach (var h in Board.Cells) map.Set(h, new Tile(biome, Marker.None));
            map.Set(Board.Portal, new Tile(Core.Data.Biome.None, Marker.Portal));
            State = new GameState(map, DefaultTowerA, DefaultTowerB);
        }

        /// <summary>Uses an existing (e.g. generated) map. Towers still default to the home rows.</summary>
        public TestBoard(GameMap map)
        {
            this.map = map;
            State = new GameState(map, DefaultTowerA, DefaultTowerB);
        }

        public TestBoard Biome(Hex h, Core.Data.Biome biome)
        {
            map.Set(h, new Tile(biome, map.Get(h).Marker));
            return this;
        }

        public TestBoard Rock(Hex h)
        {
            map.Set(h, new Tile(map.Get(h).Biome, Marker.Rock));
            return this;
        }

        public TestBoard Tower(PlayerId owner, Hex pos, int health = -1)
        {
            var t = State.GetTower(owner);
            t.Pos = pos;
            if (health >= 0) t.Health = health;
            return this;
        }

        /// <summary>Places a unit with full Health. Returns its id.</summary>
        public int Unit(UnitClass cls, PlayerId owner, Hex pos, Core.Data.Biome biome = Core.Data.Biome.Desert, int health = -1)
        {
            var u = State.AddUnit(owner, cls, biome, pos);
            if (health >= 0) u.Health = health;
            return u.Id;
        }

        /// <summary>Makes p active with full Energy; the other player gets 0.</summary>
        public TestBoard Active(PlayerId p)
        {
            State.ActivePlayer = p;
            State.SetEnergy(p, Catalog.EnergyPerTurn);
            State.SetEnergy(Opp(p), 0);
            return this;
        }

        public TestBoard Energy(PlayerId p, int energy)
        {
            State.SetEnergy(p, energy);
            return this;
        }

        public TestBoard Round(int round)
        {
            State.Round = round;
            return this;
        }

        public Core.Unit U(int id) => State.GetUnit(id);

        public GameState Build() => State;

        static PlayerId Opp(PlayerId p) => p == PlayerId.A ? PlayerId.B : PlayerId.A;
    }
}
