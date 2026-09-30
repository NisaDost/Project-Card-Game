using System.Collections.Generic;

namespace HexPortal.Core.Data
{
    /// <summary>
    /// All game numbers. Must match docs/GDD.md (v2.1) exactly; change only with the user's approval.
    /// </summary>
    public static class Catalog
    {
        // ---------- Board (B) ----------
        public static readonly IReadOnlyList<int> RowLengths = new[] { 7, 6, 7, 6, 7, 6, 7, 6, 7 }; // B-01
        public const int CellCount = 59;             // B-01
        public const int HomeZoneRows = 2;           // B-03
        public const int MinCellsPerBiome = 8;       // B-22, per upper half
        public const int BiomeSeedsMin = 3;          // B-22
        public const int BiomeSeedsMax = 5;          // B-22
        public const int RuneStonesPerHalf = 2;      // B-23
        public const int WellspringsPerHalf = 1;     // B-23
        public const int SpecialTileRowMin = 3;      // B-23, GDD rows (1-based) of the upper half
        public const int SpecialTileRowMax = 4;      // B-23
        public const int BiomeAttackBonus = 1;       // B-10, U-20

        // ---------- Units (U) ----------
        public static readonly IReadOnlyList<UnitDef> Units = new[]
        {
            //          id      class               cost atk hp mv sight range                 min max
            new UnitDef("U-01", UnitClass.Guardian, 3,   2,  6, 1, 2,    RangeKind.Adjacent,     1,  1),
            new UnitDef("U-02", UnitClass.Rider,    3,   3,  4, 3, 3,    RangeKind.Adjacent,     1,  1),
            new UnitDef("U-03", UnitClass.Archer,   2,   2,  3, 2, 3,    RangeKind.StraightLine, 1,  3),
            new UnitDef("U-04", UnitClass.Mage,     3,   2,  3, 2, 2,    RangeKind.Distance,     1,  2),
            new UnitDef("U-05", UnitClass.Healer,   2,   1,  3, 1, 2,    RangeKind.Adjacent,     1,  1),
        };

        public const int RiderChargeBonus = 1;       // U-02 Hücum
        public const int MageSplashDamage = 1;       // U-04 Patlama
        public const int HealerHealAmount = 1;       // U-05 Şifa
        public const int DeathDrawCount = 1;         // U-23

        //                                        hp  atk min max sight move
        public static readonly TowerDef Tower = new TowerDef(10, 2, 1, 2, 2, 0); // U-06
        public const int TowersPerPlayer = 1;        // U-06
        // U-27 (v2.1): a tower shot always deals Tower.Attack damage, no biome/buff/debuff.
        public const int TowerShotsPerOpponentTurn = 1; // U-27

        // ---------- Character cards (D-01) ----------
        public const int CharacterCopies = 2;
        public static readonly IReadOnlyList<CharacterCardDef> CharacterCards = BuildCharacterCards();

        // ---------- Support cards (C) ----------
        public static readonly IReadOnlyList<SupportCardDef> SupportCards = new[]
        {
            //                 id      name              category                 pool                    effect                        amt duration                cost rarity         copies
            new SupportCardDef("C-10", "Rage",           CardCategory.Power,      SupportPool.Buff,       EffectKind.AttackBonus,       2, DurationKind.Timed,  1, Rarity.Common, 4),
            new SupportCardDef("C-11", "GiantStrength",  CardCategory.Power,      SupportPool.Buff,       EffectKind.AttackBonus,       1, DurationKind.Permanent, 2, Rarity.Rare,   2),
            new SupportCardDef("C-12", "Shield",         CardCategory.Protection, SupportPool.Buff,       EffectKind.BlockNextDamage,   0, DurationKind.Permanent, 1, Rarity.Common, 4),
            new SupportCardDef("C-13", "HealingPotion",  CardCategory.Protection, SupportPool.Buff,       EffectKind.Heal,              3, DurationKind.Instant,   1, Rarity.Common, 4),
            new SupportCardDef("C-14", "WindStep",       CardCategory.Movement,   SupportPool.Buff,       EffectKind.MoveBonus,         2, DurationKind.Instant,   1, Rarity.Common, 4),
            new SupportCardDef("C-15", "Teleport",       CardCategory.Movement,   SupportPool.Buff,       EffectKind.Teleport,          0, DurationKind.Instant,   3, Rarity.Epic,   1),
            new SupportCardDef("C-16", "Weakness",       CardCategory.Curse,      SupportPool.DebuffTrap, EffectKind.AttackPenalty,     2, DurationKind.Timed,  1, Rarity.Common, 4),
            new SupportCardDef("C-17", "Poison",         CardCategory.Curse,      SupportPool.DebuffTrap, EffectKind.Poison,            1, DurationKind.Timed,  2, Rarity.Rare,   2),
            new SupportCardDef("C-18", "Push",           CardCategory.Control,    SupportPool.DebuffTrap, EffectKind.Push,              2, DurationKind.Instant,   1, Rarity.Common, 4),
            new SupportCardDef("C-19", "Root",           CardCategory.Control,    SupportPool.DebuffTrap, EffectKind.Root,              0, DurationKind.Timed,  2, Rarity.Rare,   2),
            new SupportCardDef("C-20", "SpikeTrap",      CardCategory.Trap,       SupportPool.DebuffTrap, EffectKind.TrapDamage,        3, DurationKind.Permanent, 1, Rarity.Rare,   2),
            new SupportCardDef("C-21", "MirrorTrap",     CardCategory.Trap,       SupportPool.DebuffTrap, EffectKind.TrapTeleportHome,  0, DurationKind.Permanent, 2, Rarity.Epic,   1),
        };

        public const int ControlRange = 1;           // C-02
        public const int TimedEffectTurns = 2;       // C-03, C-04
        public const int MaxBuffsPerUnit = 1;        // C-05
        public const int MaxDebuffsPerUnit = 1;      // C-05
        public const int MaxActiveTraps = 2;         // C-31

        // ---------- Pools, dealing, market (D) ----------
        public const int DealCharactersPerClass = 1; // D-03
        public const int DealCommonSupport = 3;      // D-04
        public const int DealRareSupport = 1;        // D-04
        public const int MarketSlots = 3;            // D-05
        public const int HandLimit = 6;              // D-07

        /// <summary>D-04: common and rare support cards are dealt; epics are only drawn from the pool.</summary>
        public static bool IsSupportDealtAtSetup(SupportCardDef card) =>
            card.Rarity == Rarity.Common || card.Rarity == Rarity.Rare;

        // ---------- Setup (S) ----------
        public const int QuestOffer = 5;             // S-03
        public const int QuestPick = 3;              // S-03
        public const int PassiveOffer = 3;           // S-04
        public const int PassivePick = 1;            // S-04
        public const int PlacementSeconds = 45;      // S-05
        public const int MinPlacedCharacters = 3;    // S-05

        // ---------- Turn: Mana (T-01…T-03) ----------
        public const int ManaCap = 6;                // T-01: Mana = min(round, ManaCap)
        public const int ManaFirstRoundBonusB = 1;   // T-02
        public const int WellspringManaBonus = 1;    // T-03, may exceed ManaCap

        // ---------- Turn: Energy (T-05) ----------
        public const int EnergyPerTurn = 3;          // T-05
        public const int EnergyPerAction = 1;        // T-05
        public const int ActionsPerUnitPerTurn = 1;  // T-05

        // ---------- Turn: timers (T-09) ----------
        public const int TurnSeconds = 30;
        public const int TimeBankSeconds = 60;
        public const int MaxConsecutiveTimeouts = 3;

        // ---------- Quests (Q) ----------
        public const int QuestsPerPlayer = 3;        // Q-01
        public const int QuestHoldTurns = 2;         // Q-05

        public static readonly IReadOnlyList<QuestDef> Quests = new[]
        {
            //           id      name              amount round hold   canFail
            new QuestDef("Q-10", "RuneKeeper",     1,     0,    true,  false), // hold the same rune stone
            new QuestDef("Q-11", "DoubleRune",     2,     0,    false, false), // units on 2 different rune stones
            new QuestDef("Q-12", "Hunter",         2,     0,    false, false), // kill 2 enemy units
            new QuestDef("Q-13", "Siege",          5,     0,    false, false), // 5 damage to enemy tower
            new QuestDef("Q-14", "StrongKeep",     7,     8,    false, true),  // tower health >= 7 at end of round 8
            new QuestDef("Q-15", "BiomeMaster",    3,     0,    false, false), // 3 units on own biome
            new QuestDef("Q-16", "Flawless",       0,     6,    false, true),  // no unit lost until end of round 6
            new QuestDef("Q-17", "Trapper",        1,     0,    false, false), // own trap triggers on an enemy
            new QuestDef("Q-18", "WellspringLord", 1,     0,    true,  false), // hold the wellspring
            new QuestDef("Q-19", "DeepRaid",       2,     0,    false, false), // 2 units in enemy home zone
        };

        // ---------- Commander passives (P) ----------
        public static readonly IReadOnlyList<PassiveDef> Passives = new[]
        {
            //             id      name            amount round once
            new PassiveDef("P-01", "LastBreath",   2,     0,    true),  // revive first dead unit with 2 Health
            new PassiveDef("P-02", "TrapMaster",   1,     0,    false), // +1 trap damage
            new PassiveDef("P-03", "QuickStart",   2,     3,    false), // +2 Mana in round 3
            new PassiveDef("P-04", "ThickWall",    3,     0,    false), // first 3 tower damage blocked
            new PassiveDef("P-05", "Merchant",     1,     0,    true),  // +1 blind draw on first market buy
            new PassiveDef("P-06", "PortalWarden", 3,     0,    false), // 3 damage to enemy entering the Portal
        };

        // ---------- Map events (E) ----------
        public static readonly IReadOnlyList<int> EventRounds = new[] { 3, 6, 9, 12 }; // E-01
        public const int EventAnnounceLead = 1;      // E-02
        public const int BiomeShiftMaxCells = 7;     // E-11
        public const int BiomeShiftMinPortalDistance = 3; // E-11

        public static readonly IReadOnlyList<MapEventDef> MapEvents = new[]
        {
            new MapEventDef("E-10", "Earthquake", 1),
            new MapEventDef("E-11", "BiomeShift", 0),
            new MapEventDef("E-12", "RuneRain",   1),
        };

        // ---------- Win (W) ----------
        public const int PortalQuestsRequired = 2;   // W-01
        public const int RoundLimit = 15;            // W-03

        // ---------- AI ----------
        public const int AiEasyTopMoves = 3;         // AI-03
        public const int AiMaxSecondsPerMove = 1;    // AI-05

        static CharacterCardDef[] BuildCharacterCards()
        {
            var biomes = new[] { Biome.Forest, Biome.Desert, Biome.Snow };
            var cards = new CharacterCardDef[Units.Count * biomes.Length];
            int i = 0;
            foreach (var u in Units)
                foreach (var b in biomes)
                    cards[i++] = new CharacterCardDef(u.Id + "-" + b, u.Class, b, CharacterCopies);
            return cards;
        }
    }
}
