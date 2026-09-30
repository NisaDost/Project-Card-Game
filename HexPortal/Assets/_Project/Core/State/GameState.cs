using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>
    /// The whole match state. Read freely; change only through Engine.Apply (mutators are internal).
    /// Units are kept in ascending id order, so every iteration over Units is deterministic.
    /// Hidden information (redacted by PlayerView in M4): hands, pool order, traps, pre-picks, the match Rng.
    /// </summary>
    public sealed class GameState
    {
        public readonly GameMap Map;
        readonly List<Unit> units = new List<Unit>();
        readonly Tower[] towers;
        readonly int[] energy = new int[2];                  // T-05
        readonly int[] mana = new int[2];                    // T-01…T-03. Never merged with Energy.
        readonly bool[] towerShotAvailable = { true, true }; // U-27, per tower owner
        readonly bool[] drawPending = new bool[2];           // T-04 step 4
        readonly int[] prePickSlot = { PrePickCommand.None, PrePickCommand.None }; // T-11
        readonly int[] prePickCardId = new int[2];           // T-11: the Market card seen when pre-picking
        readonly List<CardInstance>[] pools = { new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>() };
        readonly CardInstance[] market = new CardInstance[Catalog.MarketSlots]; // D-05, index = (int)CardPool
        readonly List<CardInstance>[] hands = { new List<CardInstance>(), new List<CardInstance>() };
        readonly List<Trap> traps = new List<Trap>();

        /// <summary>The match stream (deals, draws, Market refills, Mirror Trap). Separate from the map stream.</summary>
        internal readonly Rng Rng;

        public PlayerId ActivePlayer { get; internal set; }
        /// <summary>T-10: 1-based; increments when B ends its turn.</summary>
        public int Round { get; internal set; }
        /// <summary>W-02. Null while the game is running.</summary>
        public PlayerId? Winner { get; internal set; }
        /// <summary>The id the next unit will get. Ids are sequential and never reused.</summary>
        public int NextUnitId { get; private set; } = 1;
        /// <summary>The id the next card instance will get. Ids are sequential and never reused.</summary>
        public int NextCardId { get; private set; } = 1;

        /// <summary>Same as the 4-argument constructor, with the map's requested seed as the match seed.</summary>
        public GameState(GameMap map, Hex towerA, Hex towerB)
            : this(map, towerA, towerB, map == null ? 0UL : map.RequestedSeed) { }

        /// <summary>Round 1, A's turn, A has full Energy and its T-01 Mana. Pools are full (D-01, D-02);
        /// the Market and hands are empty. Dealing (D-03, D-04), opening the Market (D-05) and A's first
        /// turn-start draw (T-04) are separate steps run by the setup phase (M4): Pools.Deal, Pools.OpenMarket.</summary>
        public GameState(GameMap map, Hex towerA, Hex towerB, ulong matchSeed)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Rng = new Rng(matchSeed ^ MapGenerator.MatchStreamSalt);
            towers = new[] { new Tower(PlayerId.A, towerA), new Tower(PlayerId.B, towerB) };
            ActivePlayer = PlayerId.A;
            Round = 1;
            energy[(int)PlayerId.A] = Catalog.EnergyPerTurn;
            mana[(int)PlayerId.A] = Mana.TurnStartMana(this, PlayerId.A);
            ResetPools();
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

        public int GetMana(PlayerId p) => mana[(int)p];
        internal void SetMana(PlayerId p, int value) => mana[(int)p] = value;

        public bool IsTowerShotAvailable(PlayerId towerOwner) => towerShotAvailable[(int)towerOwner];
        internal void SetTowerShotAvailable(PlayerId towerOwner, bool value) => towerShotAvailable[(int)towerOwner] = value;

        /// <summary>T-04 step 4: the player must draw before doing anything else this turn.</summary>
        public bool IsDrawPending(PlayerId p) => drawPending[(int)p];
        internal void SetDrawPending(PlayerId p, bool value) => drawPending[(int)p] = value;

        /// <summary>T-11: a Market slot index ((int)CardPool), DrawCommand.Blind, or PrePickCommand.None.</summary>
        public int GetPrePickSlot(PlayerId p) => prePickSlot[(int)p];
        /// <summary>T-11: id of the card that was in the pre-picked Market slot at the time of the pick (0 otherwise).</summary>
        public int GetPrePickCardId(PlayerId p) => prePickCardId[(int)p];
        internal void SetPrePick(PlayerId p, int slot, int cardId)
        {
            prePickSlot[(int)p] = slot;
            prePickCardId[(int)p] = cardId;
        }

        public IReadOnlyList<CardInstance> GetPool(CardPool pool) => pools[(int)pool];
        internal List<CardInstance> PoolList(CardPool pool) => pools[(int)pool];

        /// <summary>D-05: null when the slot is empty.</summary>
        public CardInstance GetMarket(CardPool slot) => market[(int)slot];
        internal void SetMarket(CardPool slot, CardInstance card) => market[(int)slot] = card;

        public IReadOnlyList<CardInstance> GetHand(PlayerId p) => hands[(int)p];
        internal List<CardInstance> HandList(PlayerId p) => hands[(int)p];

        public CardInstance FindInHand(PlayerId p, int cardId)
        {
            foreach (var c in hands[(int)p])
                if (c.Id == cardId) return c;
            return null;
        }

        /// <summary>Active traps of both players, in placement order.</summary>
        public IReadOnlyList<Trap> Traps => traps;
        internal List<Trap> TrapList => traps;

        /// <summary>Places a new unit with full Health (deploy and test setup). No rule checks here.</summary>
        internal Unit AddUnit(PlayerId owner, UnitClass cls, Biome biome, Hex pos)
        {
            var u = new Unit(NextUnitId++, owner, cls, biome, pos);
            units.Add(u); // ids ascend, so the list stays in id order
            return u;
        }

        internal void RemoveUnit(Unit u) => units.Remove(u);

        internal CardInstance NewCard(CharacterCardDef def) => new CardInstance(NextCardId++, def, null);
        internal CardInstance NewCard(SupportCardDef def) => new CardInstance(NextCardId++, null, def);

        /// <summary>D-01, D-02: every copy of every card, in Catalog order, as new card instances.</summary>
        internal void ResetPools()
        {
            foreach (var p in pools) p.Clear();
            foreach (var d in Catalog.CharacterCards)
                for (int k = 0; k < d.Copies; k++) pools[(int)CardPool.Character].Add(NewCard(d));
            foreach (var d in Catalog.SupportCards)
                for (int k = 0; k < d.Copies; k++)
                {
                    var c = NewCard(d);
                    pools[(int)c.Pool].Add(c);
                }
        }
    }
}
