using System;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§7 Mana: T-01…T-03. Mana pays card costs only; Energy (T-05) is a separate resource.</summary>
    public static class Mana
    {
        /// <summary>T-01 min(round, cap), T-02 B's round-1 bonus, T-03 (v4.0) +1 per Rune Stone holding an own Gatherer
        /// (each rune separately; may exceed the cap).</summary>
        public static int TurnStartMana(GameState state, PlayerId p)
        {
            int mana = Math.Min(state.Round, Catalog.ManaCap);
            if (p == PlayerId.B && state.Round == 1) mana += Catalog.ManaFirstRoundBonusB;
            mana += RuneBonus(state, p);
            // P-03 (Quick Start) is added by Turn.StartTurn (it reveals the passive).
            return mana;
        }

        /// <summary>T-05 Energy refill plus B-12 (v4.0): +1 per Wellspring holding an own Gatherer (each separately).
        /// Energy stays a separate resource from Mana.</summary>
        public static int TurnStartEnergy(GameState state, PlayerId p)
        {
            return Catalog.EnergyPerTurn + WellspringBonus(state, p);
        }

        /// <summary>B-11, T-03 (v4.0): +1 Mana per Rune Stone holding an own Gatherer (U-40).</summary>
        public static int RuneBonus(GameState state, PlayerId p) => HeldBonus(state, p, Marker.RuneStone, Catalog.RuneManaBonus);

        /// <summary>B-12, T-05 (v4.0): +1 Energy per Wellspring holding an own Gatherer (U-40).</summary>
        public static int WellspringBonus(GameState state, PlayerId p) => HeldBonus(state, p, Marker.Wellspring, Catalog.WellspringEnergyBonus);

        /// <summary>U-40: only a Gatherer collects. Quests (Q-10, Q-11, Q-18) still accept any unit.</summary>
        static int HeldBonus(GameState state, PlayerId p, Marker marker, int perCell)
        {
            int n = 0;
            foreach (var u in state.Units)
                if (u.Owner == p && u.Class == UnitClass.Gatherer && state.Map.Get(u.Pos).Marker == marker) n += perCell;
            return n;
        }

        internal static void Spend(GameState state, PlayerId p, int cost) => state.SetMana(p, state.GetMana(p) - cost);
    }
}
