using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>
    /// The whole match state. Read freely; change only through Engine.Apply (mutators are internal).
    /// Units are kept in ascending id order, so every iteration over Units is deterministic.
    /// Hidden information (redacted by PlayerView): hands, pool order, traps, pre-picks, active quests and quest progress,
    /// unrevealed passives and their state, Mana/Energy of the opponent, units outside the viewer's sight, the match Rng.
    /// Public: completed/failed quests, revealed passives, the announced map event (V-09, E-05).
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
        readonly List<QuestDef>[] questOffer = { new List<QuestDef>(), new List<QuestDef>() };     // S-03
        readonly List<QuestDef>[] questChoices = { new List<QuestDef>(), new List<QuestDef>() };   // S-03, empty = not chosen
        readonly List<PassiveDef>[] passiveOffer = { new List<PassiveDef>(), new List<PassiveDef>() }; // S-04
        readonly PassiveDef[] passiveChoice = new PassiveDef[2];                                     // S-04, null = not chosen
        readonly bool[] setupFinished = new bool[2];         // S-05
        readonly int[] consecutiveTimeouts = new int[2];     // T-09, W-04
        readonly int[] dealtHandCount = new int[2];          // S-02: hand size right after dealing (S-08 view)
        readonly FogMemory[] fog = { new FogMemory(), new FogMemory() }; // V-10
        readonly PlayerProgress[] progress = { new PlayerProgress(), new PlayerProgress() }; // Q, P, W-01
        readonly List<Hex> pendingEventCells = new List<Hex>(); // E-02, E-04

        /// <summary>The match stream (deals, offers, draws, Market refills, Mirror Trap, setup auto-completion).
        /// Separate from the map stream.</summary>
        internal Rng Rng { get; set; }

        /// <summary>True until both players finished setup (S-05…S-07).</summary>
        internal bool InSetup { get; set; }

        /// <summary>Light simulation mode (AI belief states): no fog-memory updates and no per-event visibility tagging
        /// (events are not filtered: ViewFor returns null). Rules outcomes are identical (LightMode test); PlayerView and
        /// EventFilter must not be used on a light state.</summary>
        internal bool Light { get; set; }

        public PlayerId ActivePlayer { get; internal set; }
        /// <summary>T-10: 1-based; increments when B ends its turn.</summary>
        public int Round { get; internal set; }
        /// <summary>Null while the game is running.</summary>
        public GameResult Result { get; internal set; }
        /// <summary>The id the next unit will get. Ids are sequential and never reused.</summary>
        public int NextUnitId { get; private set; } = 1;
        /// <summary>The id the next card instance will get. Ids are sequential and never reused.</summary>
        public int NextCardId { get; private set; } = 1;

        /// <summary>Same as the 4-argument constructor, with the map's requested seed as the match seed.</summary>
        public GameState(GameMap map, Hex towerA, Hex towerB)
            : this(map, towerA, towerB, map == null ? 0UL : map.RequestedSeed) { }

        /// <summary>A scenario already in play (tests, tools): towers placed, setup done, round 1, A's turn, A has full
        /// Energy and its T-01 Mana. Pools are full (D-01, D-02); the Market and hands are empty.
        /// Real matches start with Match.Create (setup phase).</summary>
        public GameState(GameMap map, Hex towerA, Hex towerB, ulong matchSeed)
            : this(map, matchSeed, new Tower(PlayerId.A, towerA), new Tower(PlayerId.B, towerB))
        {
            InSetup = false;
            setupFinished[0] = setupFinished[1] = true;
            energy[(int)PlayerId.A] = Catalog.EnergyPerTurn;
            mana[(int)PlayerId.A] = Mana.TurnStartMana(this, PlayerId.A);
        }

        /// <summary>S-01: the setup phase. Towers are not placed, nobody has Mana or Energy.</summary>
        internal GameState(GameMap map, ulong matchSeed)
            : this(map, matchSeed, new Tower(PlayerId.A), new Tower(PlayerId.B))
        {
            InSetup = true;
        }

        GameState(GameMap map, ulong matchSeed, Tower towerA, Tower towerB)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Rng = new Rng(matchSeed ^ MapGenerator.MatchStreamSalt);
            towers = new[] { towerA, towerB };
            ActivePlayer = PlayerId.A;
            Round = 1;
            ResetPools();
            Visibility.InitMemory(this);
        }

        /// <summary>Deep copy, including the Rng position and fog memory (AI search, leak tests).</summary>
        public GameState Clone() => new GameState(this);

        GameState(GameState o)
        {
            Map = o.Map.Clone();
            foreach (var u in o.units) units.Add(u.Clone());
            towers = new[] { o.towers[0].Clone(), o.towers[1].Clone() };
            Array.Copy(o.energy, energy, 2);
            Array.Copy(o.mana, mana, 2);
            Array.Copy(o.towerShotAvailable, towerShotAvailable, 2);
            Array.Copy(o.drawPending, drawPending, 2);
            Array.Copy(o.prePickSlot, prePickSlot, 2);
            Array.Copy(o.prePickCardId, prePickCardId, 2);
            for (int i = 0; i < pools.Length; i++) pools[i].AddRange(o.pools[i]); // card instances are immutable
            Array.Copy(o.market, market, market.Length);
            traps.AddRange(o.traps);                                              // traps are immutable
            for (int p = 0; p < 2; p++)
            {
                hands[p].AddRange(o.hands[p]);
                questOffer[p].AddRange(o.questOffer[p]);
                questChoices[p].AddRange(o.questChoices[p]);
                passiveOffer[p].AddRange(o.passiveOffer[p]);
                fog[p] = o.fog[p].Clone();
                progress[p] = o.progress[p].Clone();
            }
            pendingEventCells.AddRange(o.pendingEventCells);
            PendingEvent = o.PendingEvent;
            PendingEventRound = o.PendingEventRound;
            Array.Copy(o.passiveChoice, passiveChoice, 2);
            Array.Copy(o.setupFinished, setupFinished, 2);
            Array.Copy(o.consecutiveTimeouts, consecutiveTimeouts, 2);
            Array.Copy(o.dealtHandCount, dealtHandCount, 2);
            Rng = o.Rng.Clone();
            InSetup = o.InSetup;
            Light = o.Light;
            ActivePlayer = o.ActivePlayer;
            Round = o.Round;
            Result = o.Result;
            NextUnitId = o.NextUnitId;
            NextCardId = o.NextCardId;
        }

        public GamePhase Phase => Result != null ? GamePhase.Over : InSetup ? GamePhase.Setup : GamePhase.Playing;
        public bool IsOver => Result != null;
        /// <summary>Null while the game runs or after a draw (W-03).</summary>
        public PlayerId? Winner => Result == null ? null : Result.Winner;

        /// <summary>0-based count of turns: A's turn in round r = 2(r−1), B's = 2(r−1)+1. Used for V-08 reveal timers.</summary>
        public int TurnIndex => 2 * (Round - 1) + (ActivePlayer == PlayerId.B ? 1 : 0);

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

        /// <summary>The placed tower on h, or null.</summary>
        public Tower TowerAt(Hex h)
        {
            foreach (var t in towers)
                if (t.IsPlaced && t.Pos == h) return t;
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

        /// <summary>S-03: the 5 offered quests, in Catalog order.</summary>
        public IReadOnlyList<QuestDef> GetQuestOffer(PlayerId p) => questOffer[(int)p];
        internal void SetQuestOffer(PlayerId p, IEnumerable<QuestDef> offer) => Replace(questOffer[(int)p], offer);
        /// <summary>S-03: the 3 chosen quests in offer order; empty until chosen.</summary>
        public IReadOnlyList<QuestDef> GetQuestChoices(PlayerId p) => questChoices[(int)p];
        internal void SetQuestChoices(PlayerId p, IEnumerable<QuestDef> choices) => Replace(questChoices[(int)p], choices);

        /// <summary>S-04: the 3 offered passives, in Catalog order.</summary>
        public IReadOnlyList<PassiveDef> GetPassiveOffer(PlayerId p) => passiveOffer[(int)p];
        internal void SetPassiveOffer(PlayerId p, IEnumerable<PassiveDef> offer) => Replace(passiveOffer[(int)p], offer);
        /// <summary>S-04: null until chosen.</summary>
        public PassiveDef GetPassiveChoice(PlayerId p) => passiveChoice[(int)p];
        internal void SetPassiveChoice(PlayerId p, PassiveDef d) => passiveChoice[(int)p] = d;

        public bool IsSetupFinished(PlayerId p) => setupFinished[(int)p];
        internal void SetSetupFinished(PlayerId p, bool value) => setupFinished[(int)p] = value;

        /// <summary>T-09: automatic turn ends in a row (reset by a normal EndTurn).</summary>
        public int GetConsecutiveTimeouts(PlayerId p) => consecutiveTimeouts[(int)p];
        internal void SetConsecutiveTimeouts(PlayerId p, int value) => consecutiveTimeouts[(int)p] = value;

        /// <summary>S-02: the hand size right after dealing. Shown as the opponent's hand count until both players
        /// finished setup (S-08, PM decision), so placements do not leak through the public hand count.</summary>
        public int GetDealtHandCount(PlayerId p) => dealtHandCount[(int)p];
        internal void SetDealtHandCount(PlayerId p, int value) => dealtHandCount[(int)p] = value;

        /// <summary>V-10: the player's exploration map and last-seen snapshots.</summary>
        public FogMemory GetFog(PlayerId p) => fog[(int)p];

        /// <summary>Quest progress, passive state and the W-01 watch (hidden from the opponent, see PlayerProgress).</summary>
        public PlayerProgress GetProgress(PlayerId p) => progress[(int)p];

        /// <summary>Q-03, Q-04. Throws if <paramref name="questId"/> is not one of p's chosen quests.</summary>
        public QuestStatus GetQuestStatus(PlayerId p, string questId)
        {
            var choices = questChoices[(int)p];
            for (int i = 0; i < choices.Count; i++)
                if (choices[i].Id == questId) return progress[(int)p].GetQuestStatus(i);
            throw new ArgumentException(questId + " is not a quest of " + p, nameof(questId));
        }

        /// <summary>W-01, W-03: public (Q-03).</summary>
        public int CompletedQuestCount(PlayerId p)
        {
            int n = 0;
            for (int i = 0; i < questChoices[(int)p].Count; i++)
                if (progress[(int)p].GetQuestStatus(i) == QuestStatus.Completed) n++;
            return n;
        }

        /// <summary>E-02: the announced map event (public), or null.</summary>
        public MapEventDef PendingEvent { get; private set; }
        /// <summary>E-01: the round the pending event happens at.</summary>
        public int PendingEventRound { get; private set; }
        /// <summary>E-03, E-04: its cells, chosen at the announcement (see MapEvents for the order).</summary>
        public IReadOnlyList<Hex> PendingEventCells => pendingEventCells;

        internal void SetPendingEvent(MapEventDef def, int round, IEnumerable<Hex> cells)
        {
            PendingEvent = def;
            PendingEventRound = def == null ? 0 : round;
            Replace(pendingEventCells, cells ?? new Hex[0]);
        }

        static void Replace<T>(List<T> list, IEnumerable<T> items)
        {
            var copy = new List<T>(items);
            list.Clear();
            list.AddRange(copy);
        }

        /// <summary>Places a new unit with full Health (deploy, setup placement and test setup). No rule checks here.</summary>
        internal Unit AddUnit(PlayerId owner, UnitClass cls, Biome biome, Hex pos)
        {
            var u = new Unit(NextUnitId++, owner, cls, biome, pos);
            units.Add(u); // ids ascend, so the list stays in id order
            return u;
        }

        /// <summary>Puts a unit with a known id (BeliefState). Keeps the id order; later units get higher ids.</summary>
        internal void PutUnit(Unit u)
        {
            int i = 0;
            while (i < units.Count && units[i].Id < u.Id) i++;
            units.Insert(i, u);
            if (u.Id >= NextUnitId) NextUnitId = u.Id + 1;
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
