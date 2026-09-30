using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>§9 commander passive hooks. M3 only marks where they plug in; passive selection and effects are M4.</summary>
    public static class Passives
    {
        /// <summary>P-02 Trap Master: extra trap damage for the trap owner. M4 hook: always 0 for now.</summary>
        internal static int TrapBonusDamage(GameState state, PlayerId trapOwner) => 0;

        /// <summary>P-05 Merchant: one extra blind draw on the first Market buy. M4 hook: no-op for now.</summary>
        internal static void OnMarketBuy(GameState state, PlayerId player, List<GameEvent> events) { }
    }
}
