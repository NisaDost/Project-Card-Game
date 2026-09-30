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
            foreach (var u in state.Units)
                if (u.Owner == p && state.Map.Get(u.Pos).Marker == Marker.Wellspring) mana += Catalog.WellspringManaBonus;
            // P-03 (Quick Start) is M4.
            return mana;
        }

        internal static void Spend(GameState state, PlayerId p, int cost) => state.SetMana(p, state.GetMana(p) - cost);
    }
}
