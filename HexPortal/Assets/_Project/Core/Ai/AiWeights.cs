namespace HexPortal.Core
{
    /// <summary>AI tuning (AI-02, AI-04), not game rules: every weight of the greedy evaluation in one place. Integer
    /// points so scores are identical on every platform. Changing them never changes the rules (GDD numbers live in
    /// Catalog.cs); balance them with the Sim (M5b).</summary>
    internal static class AiWeights
    {
        // ---- Outcome ----
        public const int Win = 1000000;

        // ---- Material (each known unit: own adds, enemy subtracts) ----
        public const int UnitBase = 300;
        public const int HealthPoint = 60;
        public const int AttackPoint = 40;      // Attack + buff − debuff (U-20)
        public const int Shield = 80;           // C-12
        public const int Rooted = 60;           // C-19
        public const int PoisonPerTurn = 50;    // C-17, per turn left
        public const int Kill = 200;            // on top of the removed unit's value
        public const int OwnTowerHealth = 150;
        public const int EnemyTowerHealth = 120;

        // ---- Quests (own Active quests only) ----
        public const int QuestKill = 300;           // Q-12, per kill
        public const int QuestTowerDamage = 60;     // Q-13, per damage
        public const int QuestRuneHeld = 250;       // Q-10: a unit on a rune stone
        public const int QuestRunePerStone = 150;   // Q-11: per distinct stone
        public const int QuestRuneBoth = 450;       // Q-11: both stones now
        public const int QuestBiomeUnit = 150;      // Q-15, per unit on its biome (up to the amount)
        public const int QuestBiomeAll = 300;       // Q-15 met
        public const int QuestTrap = 120;           // Q-17, per own trap on the board
        public const int QuestTrapSprung = 400;     // Q-17, per trap that fired
        public const int QuestWellspring = 300;     // Q-18
        public const int QuestDeepRaidUnit = 250;   // Q-19, per unit in the enemy home zone (up to the amount)
        public const int QuestApproach = 15;        // positional quests: per step from the closest own unit to a target

        // ---- Portal (W-01) ----
        public const int PortalDistanceAll = 4;     // per step, every own unit
        public const int PortalLead = 10;           // per step, the closest own unit (0 quests done)
        public const int PortalLeadOneQuest = 40;   // same with 1 quest done
        public const int PortalLeadOpen = 120;      // same with the Portal open
        public const int PortalHoldOpen = 5000;     // own unit on the open Portal
        public const int EnemyOnOpenPortal = 4000;  // enemy unit on the Portal while it is open for the enemy

        // ---- Tower threat ----
        public const int TowerThreat = 80;          // per enemy unit and per step closer than TowerThreatRange + 1
        public const int TowerThreatRange = 3;

        // ---- Position ----
        public const int Biome = 30;                // own unit on its own biome (U-20)
        public const int Wellspring = 80;           // own unit on a Wellspring (T-03)
        public const int Explore = 12;              // per Hidden cell now Visible

        // ---- Efficiency (unspent Mana and Energy are lost at the turn end, T-01, T-05) ----
        public const int ManaSpent = 20;
        public const int EnergySpent = 5;
        public const int CardInHand = 40;

        // ---- Overwatch and exposure ----
        public const int Overwatch = 15;            // own unit on overwatch
        public const int OverwatchPerThreat = 60;   // per known enemy unit that could come into its range
        public const int RiskPerDamage = 25;        // per damage a known enemy unit could deal to an own unit next turn
        public const int GhostRiskPercent = 50;     // caution: a ghost counts at this share (it may have moved)
        public const int EnemyTowerRange = 30;      // own unit inside the known enemy tower's range
        public const int HiddenNeighbour = 8;       // caution: per Hidden cell next to an own unit

        // ---- Cards (draw, pre-pick and setup values; AI-06) ----
        public const int CharacterCard = 350;
        public const int CharacterCardShort = 150;  // extra while fewer than ShortUnits units are on the board and in hand
        public const int ShortUnits = 3;
        public const int TrapCardTrapper = 100;     // extra for a trap card with Q-17 active

        /// <summary>Support card value by Catalog id; 0 if unknown.</summary>
        public static int SupportCard(string id)
        {
            switch (id)
            {
                case "C-10": return 120;
                case "C-11": return 150;
                case "C-12": return 120;
                case "C-13": return 100;
                case "C-14": return 60;
                case "C-15": return 140;
                case "C-16": return 100;
                case "C-17": return 110;
                case "C-18": return 80;
                case "C-19": return 110;
                case "C-20": return 120;
                case "C-21": return 130;
                default: return 0;
            }
        }

        // ---- Setup (AI-04): quest and passive feasibility ----
        public static int Quest(string id)
        {
            switch (id)
            {
                case "Q-10": return 6;
                case "Q-11": return 4;
                case "Q-12": return 7;
                case "Q-13": return 5;
                case "Q-14": return 5;
                case "Q-15": return 4;
                case "Q-16": return 2;
                case "Q-17": return 3;
                case "Q-18": return 7;
                case "Q-19": return 3;
                default: return 0;
            }
        }

        public const int QuestTrapperPerTrapCard = 2; // Q-17 per trap card in hand

        public static int Passive(string id)
        {
            switch (id)
            {
                case "P-01": return 5;
                case "P-02": return 2;
                case "P-03": return 4;
                case "P-04": return 5;
                case "P-05": return 3;
                case "P-06": return 4;
                default: return 0;
            }
        }

        public const int PassiveTrapMasterPerTrapCard = 2; // P-02 per trap card in hand
        public const int TowerColumnSpread = 2;            // tower cell: |doubled x − 6| at most this (3 back-row cells)
        public const int SpreadCap = 3;                    // unit placement: min distance to own pieces, capped
    }
}
