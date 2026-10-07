using System;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§7 Mana: T-01…T-03. Mana pays card costs only; Energy (T-05) is a separate resource.</summary>
    public static class Mana
    {
        /// <summary>T-01 min(round, cap), T-02 B's round-1 bonus, T-03 +1 per Wellspring holding an own unit
        /// (each Wellspring separately; may exceed the cap).</summary>
        public static int TurnStartMana(GameState state, PlayerId p)
        {
            int mana = Math.Min(state.Round, Catalog.ManaCap);
            if (p == PlayerId.B && state.Round == 1) mana += Catalog.ManaFirstRoundBonusB;
            mana += WellspringBonus(state, p);
            // P-03 (Quick Start) is added by Turn.StartTurn (it reveals the passive).
            return mana;
        }

        /// <summary>T-05 Energy refill plus B-11 (v3.3): +1 per Rune Stone holding an own unit (each rune separately).
        /// Energy stays a separate resource from Mana.</summary>
        public static int TurnStartEnergy(GameState state, PlayerId p)
        {
            return Catalog.EnergyPerTurn + RuneBonus(state, p);
        }

        /// <summary>T-03: +1 Mana per Wellspring holding an own unit (each separately).</summary>
        public static int WellspringBonus(GameState state, PlayerId p) => HeldBonus(state, p, Marker.Wellspring, Catalog.WellspringManaBonus);

        /// <summary>B-11 (v3.3): +1 Energy per Rune Stone holding an own unit (each separately).</summary>
        public static int RuneBonus(GameState state, PlayerId p) => HeldBonus(state, p, Marker.RuneStone, Catalog.RuneEnergyBonus);

        static int HeldBonus(GameState state, PlayerId p, Marker marker, int perCell)
        {
            int n = 0;
            foreach (var u in state.Units)
                if (u.Owner == p && state.Map.Get(u.Pos).Marker == marker) n += perCell;
            return n;
        }

        internal static void Spend(GameState state, PlayerId p, int cost) => state.SetMana(p, state.GetMana(p) - cost);
    }
}
