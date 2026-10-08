using System.Collections.Generic;

namespace HexPortal.Core.Data
{
    /// <summary>
    /// All game numbers. Must match docs/GDD.md (v2.3) exactly; change only with the user's approval.
    /// </summary>
    public static class Catalog
    {
        // ---------- Board (B) ----------
        public const int BoardRadius = 7;            // B-01 (v3.3): hexagon of radius 7 around the Portal
        public static readonly IReadOnlyList<int> RowLengths = new[] { 8, 9, 10, 11, 12, 13, 14, 15, 14, 13, 12, 11, 10, 9, 8 }; // B-01
        public const int CellCount = 169;            // B-01 (v3.3): at most 169; a template may mark cells Void (B-25)
        public const int HomeZoneRows = 2;           // B-03: 8 + 9 = 17 cells
        public const int UpperHalfCells = 84;        // B-21 (v3.3), before the template removes rock/void cells
        public const int BiomeMinPercent = 28;       // B-25 (v3.3): biome minimum = floor(0.28 × N), N = playable non-rock upper cells
        public const int BiomeSeedsPerBiomeMin = 6;  // B-25 (v3.3): seeds per biome
        public const int BiomeSeedsPerBiomeMax = 8;  // B-25 (v3.3): seeds per biome
        public const int RuneStonesPerHalf = 2;      // B-23, B-25
        public const int WellspringsPerHalf = 1;     // B-23, B-25
        public const int SpecialTileRowMin = 3;      // B-25 (v3.3), GDD rows (1-based) of the upper half: axial r = -5
        public const int SpecialTileRowMax = 5;      // B-25 (v3.3): axial r = -3
        public const int BiomeAttackBonus = 1;       // B-10, U-20

        /// <summary>B-25: biome minimum for a template with <paramref name="playableUpperCells"/> playable non-rock upper cells.</summary>
        public static int MinCellsPerBiome(int playableUpperCells) => playableUpperCells * BiomeMinPercent / 100;

        /// <summary>B-25 (v3.3) map pool. Chosen by seed mod MapTemplates.Count. Cells are given for the upper half only
        /// (B-21, axial (q, r)); the lower half is their mirror. Home zones and the special band are never touched.</summary>
        public static readonly IReadOnlyList<MapTemplateDef> MapTemplates = new[]
        {
            new MapTemplateDef(0, "Plain", new int[0], new int[0]),
            // Rocky ring at distance 2 from the Portal, 8 rocks and 4 gaps ((0,-2), (2,-2) and their mirrors).
            new MapTemplateDef(1, "RockyRing", new[] { 1, -2, 2, -1, -1, -1, -2, 0 }, new int[0]),
            // Void notches: every cell within distance 2 of the two side corners (-7,0) and (7,0). The four other corners
            // lie in the home zones, which stay complete (B-25).
            new MapTemplateDef(2, "IndentedEdges", new int[0],
                new[] { -5, -2, -6, -1, -5, -1, -7, 0, -6, 0, -5, 0, 7, -1, 6, -1, 7, -2 }),
            // Two corridors: a rock wall across row r = -2 (and its mirror r = 2) with two 2-cell gaps (q = -2,-1 and 3,4).
            new MapTemplateDef(3, "TwoCorridors",
                new[] { -5, -2, -4, -2, -3, -2, 0, -2, 1, -2, 2, -2, 5, -2, 6, -2, 7, -2 }, new int[0]),
        };

        // ---------- Units (U) ----------
        public static readonly IReadOnlyList<UnitDef> Units = new[]
        {
            //          id      class               cost atk hp mv sight range                 min max
            new UnitDef("U-01", UnitClass.Guardian, 3,   2, 12, 1, 2,    RangeKind.Adjacent,     1,  1), // v3.1 Health
            new UnitDef("U-02", UnitClass.Rider,    3,   3,  8, 3, 3,    RangeKind.Adjacent,     1,  1),
            new UnitDef("U-03", UnitClass.Archer,   2,   2,  6, 2, 3,    RangeKind.StraightLine, 1,  3),
            new UnitDef("U-04", UnitClass.Mage,     3,   2,  6, 2, 2,    RangeKind.Distance,     1,  2),
            new UnitDef("U-05", UnitClass.Healer,   2,   1,  7, 1, 2,    RangeKind.Adjacent,     1,  1),
        };

        public const int RiderChargeBonus = 1;       // U-02 Hücum (v3.0: attack after a move in the same turn)
        public const int MageSplashDamage = 1;       // U-04 Patlama
        public const int HealerHealAmount = 2;       // U-05 Şifa (v3.1)
        public const int DeathDrawCount = 1;         // U-23

        //                                        hp  atk min max sight move
        public static readonly TowerDef Tower = new TowerDef(20, 3, 1, 2, 2, 0); // U-06 (v3.1)
        public const int TowersPerPlayer = 1;        // U-06
        // U-27: a tower shot always deals Tower.Attack damage, no biome/buff/debuff.
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
            new SupportCardDef("C-13", "HealingPotion",  CardCategory.Protection, SupportPool.Buff,       EffectKind.Heal,              5,DurationKind.Instant,   1, Rarity.Common, 4),
            new SupportCardDef("C-14", "WindStep",       CardCategory.Movement,   SupportPool.Buff,       EffectKind.MoveBonus,         2, DurationKind.Instant,   1, Rarity.Common, 4),
            new SupportCardDef("C-15", "Teleport",       CardCategory.Movement,   SupportPool.Buff,       EffectKind.Teleport,          0, DurationKind.Instant,   3, Rarity.Epic,   1),
            new SupportCardDef("C-16", "Weakness",       CardCategory.Curse,      SupportPool.DebuffTrap, EffectKind.AttackPenalty,     2, DurationKind.Timed,  1, Rarity.Common, 4),
            new SupportCardDef("C-17", "Poison",         CardCategory.Curse,      SupportPool.DebuffTrap, EffectKind.Poison,            2,DurationKind.Timed,  2, Rarity.Rare,   2),
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
        public const int MinPlacedCharacters = 1;    // S-05 (v3.0)
        public const int SetupMana = 8;              // S-05 (v3.0): setup budget, used from v3b

        // ---------- Turn: Mana (T-01…T-03) ----------
        public const int ManaCap = 6;                // T-01: Mana = min(round, ManaCap)
        public const int ManaFirstRoundBonusB = 1;   // T-02
        public const int WellspringManaBonus = 1;    // T-03, may exceed ManaCap
        public const int RuneEnergyBonus = 1;        // B-11 / T-05 (v3.3), per held rune stone

        // ---------- Turn: Energy (T-05) ----------
        public const int EnergyPerTurn = 3;          // T-05
        public const int EnergyPerAction = 1;        // T-05
        public const int ActionsPerUnitPerTurn = 1;  // T-05
        public const int MoveAttackEnergy = 2;       // T-05 (v3.2): move-attack, one action

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
            new QuestDef("Q-13", "Siege",          10,    0,    false, false), // 10 damage to enemy tower (v3.1)
            new QuestDef("Q-14", "StrongKeep",     14,    10,   false, true),  // tower health >= 14 at end of round 10 (v3.1)
            new QuestDef("Q-15", "BiomeMaster",    3,     0,    false, false), // 3 units on own biome
            new QuestDef("Q-16", "Flawless",       0,     8,    false, true),  // no unit lost until end of round 8 (v3.1)
            new QuestDef("Q-17", "Trapper",        1,     0,    false, false), // own trap triggers on an enemy
            new QuestDef("Q-18", "WellspringLord", 1,     0,    true,  false), // hold the wellspring
            new QuestDef("Q-19", "DeepRaid",       2,     0,    false, false), // 2 units in enemy home zone
        };

        // ---------- Commander passives (P) ----------
        public static readonly IReadOnlyList<PassiveDef> Passives = new[]
        {
            //             id      name            amount round once
            new PassiveDef("P-01", "LastBreath",   4,     0,    true),  // revive first dead unit with 4 Health (v3.1)
            new PassiveDef("P-02", "TrapMaster",   1,     0,    false), // +1 trap damage
            new PassiveDef("P-03", "QuickStart",   2,     3,    false), // +2 Mana in round 3
            new PassiveDef("P-04", "ThickWall",    5,     0,    false), // first 5 tower damage blocked (v3.1)
            new PassiveDef("P-05", "Merchant",     1,     0,    true),  // +1 blind draw on first market buy
            new PassiveDef("P-06", "PortalWarden", 3,     0,    false), // 3 damage to enemy entering the Portal
        };

        // ---------- Map events (E) ----------
        public const int EventFirstRound = 4;        // E-01 (v3.1)
        public const int EventInterval = 3;          // E-01 (v3.1)
        public const int EventLastRoundOffset = 3;   // E-01 (v3.1): the last event is at most RoundLimit - 3
        /// <summary>E-01: EventFirstRound, every EventInterval rounds, up to RoundLimit - EventLastRoundOffset (4,7,10,13,16).</summary>
        public static readonly IReadOnlyList<int> EventRounds = BuildEventRounds();
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
        public const int RoundLimit = 20;            // W-03 (v3.1)

        // ---------- AI ----------
        public const int AiEasyTopMoves = 3;         // AI-03
        public const int AiMaxSecondsPerMove = 1;    // AI-05

        static int[] BuildEventRounds()
        {
            var rounds = new List<int>();
            for (int r = EventFirstRound; r <= RoundLimit - EventLastRoundOffset; r += EventInterval) rounds.Add(r);
            return rounds.ToArray();
        }

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
