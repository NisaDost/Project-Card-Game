using System;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>Q-03, Q-04: a completed or failed quest never changes again.</summary>
    public enum QuestStatus { Active, Completed, Failed }

    /// <summary>
    /// One player's quest progress (§8), commander passive state (§9) and the W-01 Portal watch. Only the engine changes it.
    /// Hidden from the opponent (PlayerView) except completed/failed quests (Q-03, Q-04) and a revealed passive (P-00).
    /// Counters run from the start of the match whatever quests were chosen.
    /// </summary>
    public sealed class PlayerProgress
    {
        readonly QuestStatus[] quests = new QuestStatus[Catalog.QuestPick];
        readonly int[] holdStreak = new int[Board.Cells.Count];

        /// <summary>Status of the quest at <paramref name="index"/> of GameState.GetQuestChoices.</summary>
        public QuestStatus GetQuestStatus(int index) => quests[index];
        internal void SetQuestStatus(int index, QuestStatus status) => quests[index] = status;

        /// <summary>Q-12: enemy units this player killed (every source, UnitDied.Killer).</summary>
        public int Kills { get; internal set; }
        /// <summary>Q-13: damage this player dealt to the enemy tower (blocked damage excluded).</summary>
        public int TowerDamage { get; internal set; }
        /// <summary>Q-17: own traps triggered on enemy units.</summary>
        public int TrapsSprung { get; internal set; }
        /// <summary>Q-16 (v2.10): own units that died. The failure is announced at the next own turn end or round end.</summary>
        public int UnitsLost { get; internal set; }

        /// <summary>Q-05: consecutive own turn ends with an own unit on this rune stone or wellspring (0 elsewhere).</summary>
        public int GetHoldStreak(Hex h) => holdStreak[Board.IndexOf(h)];
        internal void SetHoldStreak(Hex h, int value) => holdStreak[Board.IndexOf(h)] = value;

        /// <summary>P-00: the passive has taken effect once and is public.</summary>
        public bool PassiveRevealed { get; internal set; }
        /// <summary>P-01: the first own unit died (the passive's one use is spoken for).</summary>
        public bool LastBreathUsed { get; internal set; }
        /// <summary>P-01: waiting for an empty home cell at an own turn start.</summary>
        public bool LastBreathPending { get; internal set; }
        public UnitClass LastBreathClass { get; internal set; }
        public Biome LastBreathBiome { get; internal set; }
        /// <summary>P-01 (v2.10): GameState.TurnIndex of that death; the return needs a later own turn start.</summary>
        public int LastBreathDeathTurn { get; internal set; } = -1;
        /// <summary>P-04: tower damage blocked so far.</summary>
        public int WallBlocked { get; internal set; }
        /// <summary>P-05: the first Market buy happened.</summary>
        public bool MerchantUsed { get; internal set; }
        /// <summary>W-01 (v2.10): id of the own unit that stood on the Portal at the last own turn end while the Portal was
        /// already open for this player (0 = none).</summary>
        public int PortalUnitId { get; internal set; }

        internal PlayerProgress Clone()
        {
            var c = new PlayerProgress
            {
                Kills = Kills, TowerDamage = TowerDamage, TrapsSprung = TrapsSprung, UnitsLost = UnitsLost, PassiveRevealed = PassiveRevealed,
                LastBreathUsed = LastBreathUsed, LastBreathPending = LastBreathPending, LastBreathClass = LastBreathClass,
                LastBreathBiome = LastBreathBiome, LastBreathDeathTurn = LastBreathDeathTurn, WallBlocked = WallBlocked,
                MerchantUsed = MerchantUsed, PortalUnitId = PortalUnitId,
            };
            Array.Copy(quests, c.quests, quests.Length);
            Array.Copy(holdStreak, c.holdStreak, holdStreak.Length);
            return c;
        }
    }
}
