using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Tests
{
    /// <summary>
    /// Builds exact GameState scenarios. Default: flat Forest map (Portal = None/Portal), towers at
    /// doubled (6,8) for A and (6,0) for B, A active in round 1 with full Energy.
    /// Units default to Desert, so they get no biome bonus on the default map.
    /// Cards: pools, Market and hands start EMPTY (so turn-start draws are skipped unless a test adds cards);
    /// FullPools() restores the D-01/D-02 pools. Mana is A's round-1 value (1) unless set.
    /// </summary>
    public sealed class TestBoard
    {
        public static readonly Hex DefaultTowerA = Hex.FromDoubled(6, 8); // (-2,4)
        public static readonly Hex DefaultTowerB = Hex.FromDoubled(6, 0); // (2,-4)

        readonly GameMap map;
        public readonly GameState State;

        public TestBoard(Core.Data.Biome biome = Core.Data.Biome.Forest, ulong seed = 0)
        {
            map = new GameMap(0, 1);
            foreach (var h in Board.Cells) map.Set(h, new Tile(biome, Marker.None));
            map.Set(Board.Portal, new Tile(Core.Data.Biome.None, Marker.Portal));
            State = new GameState(map, DefaultTowerA, DefaultTowerB, seed);
            EmptyPools();
        }

        /// <summary>Uses an existing (e.g. generated) map. Towers still default to the home rows.</summary>
        public TestBoard(GameMap map)
        {
            this.map = map;
            State = new GameState(map, DefaultTowerA, DefaultTowerB);
            EmptyPools();
        }

        void EmptyPools()
        {
            foreach (var p in new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }) State.PoolList(p).Clear();
        }

        public TestBoard FullPools()
        {
            State.ResetPools();
            return this;
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

        public TestBoard Wellspring(Hex h)
        {
            map.Set(h, new Tile(map.Get(h).Biome, Marker.Wellspring));
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

        public TestBoard Mana(PlayerId p, int mana)
        {
            State.SetMana(p, mana);
            return this;
        }

        public TestBoard Round(int round)
        {
            State.Round = round;
            return this;
        }

        /// <summary>A new card instance (support "C-10" or character "U-01-Forest").</summary>
        public CardInstance NewCard(string defId)
        {
            var s = Catalog.SupportCards.FirstOrDefault(c => c.Id == defId);
            if (s != null) return State.NewCard(s);
            return State.NewCard(Catalog.CharacterCards.Single(c => c.Id == defId));
        }

        /// <summary>Adds a new card to p's hand. Returns the card id.</summary>
        public int Hand(PlayerId p, string defId)
        {
            var c = NewCard(defId);
            State.HandList(p).Add(c);
            return c.Id;
        }

        /// <summary>Adds a new card to the pool it belongs to. Returns the card id.</summary>
        public int PoolCard(string defId)
        {
            var c = NewCard(defId);
            State.PoolList(c.Pool).Add(c);
            return c.Id;
        }

        /// <summary>Puts a new card in its Market slot. Returns the card id.</summary>
        public int Market(string defId)
        {
            var c = NewCard(defId);
            State.SetMarket(c.Pool, c);
            return c.Id;
        }

        /// <summary>Places a face-down trap directly (no rule checks). Returns the card id.</summary>
        public int Trap(PlayerId owner, string defId, Hex pos)
        {
            var c = NewCard(defId);
            State.TrapList.Add(new Core.Trap(owner, pos, c));
            return c.Id;
        }

        /// <summary>Puts an effect straight into the unit's buff or debuff slot (no rule checks).</summary>
        public TestBoard Effect(int unitId, string defId, int turnsLeft = -1)
        {
            var d = Catalog.SupportCards.Single(c => c.Id == defId);
            var e = new ActiveEffect(d);
            if (turnsLeft >= 0) e.TurnsLeft = turnsLeft;
            var u = State.GetUnit(unitId);
            if (d.Pool == SupportPool.Buff) u.Buff = e;
            else u.Debuff = e;
            return this;
        }

        public Core.Unit U(int id) => State.GetUnit(id);

        /// <summary>Also rebuilds both players' fog memory for the scenario as placed (V-04 start + current sight).</summary>
        public GameState Build()
        {
            Visibility.ResetMemory(State);
            return State;
        }

        static PlayerId Opp(PlayerId p) => p == PlayerId.A ? PlayerId.B : PlayerId.A;
    }
}
