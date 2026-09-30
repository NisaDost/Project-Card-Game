using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§4.3 traps: C-30…C-35, C-20, C-21. Traps are hidden information (C-33).</summary>
    public static class Traps
    {
        public static int ActiveCount(GameState state, PlayerId owner)
        {
            int n = 0;
            foreach (var t in state.Traps)
                if (t.Owner == owner) n++;
            return n;
        }

        static bool HasOwnTrap(GameState state, PlayerId owner, Hex h)
        {
            foreach (var t in state.Traps)
                if (t.Owner == owner && t.Pos == h) return true;
            return false;
        }

        /// <summary>C-30: an empty cell (no unit, tower or rock), not the Portal, in the Control Zone (C-02).
        /// Only the placer's own traps count (C-33, C-34): a cell already holding one of them is not offered.
        /// C-31 (the per-player limit) is checked separately by the caller.</summary>
        internal static bool IsLegalCell(GameState state, PlayerId owner, Hex h, ISet<Hex> visible, ISet<Hex> zone) =>
            zone.Contains(h) && visible.Contains(h) && h != Board.Portal && Movement.IsEmpty(state, h)
            && !HasOwnTrap(state, owner, h);

        internal static void Place(GameState state, PlayerId owner, CardInstance card, Hex h, EventLog events)
        {
            state.TrapList.Add(new Trap(owner, h, card));
            events.Add(new TrapPlaced(owner, card.Id, h));
        }

        /// <summary>C-32: the unit has stopped on its cell (move, push, teleport incl. Mirror Trap, deploy).
        /// Only an opponent's trap fires (C-34); it is used up. Passing through never calls this.</summary>
        internal static void ResolveArrival(GameState state, Unit unit, EventLog events)
        {
            if (state.IsOver || state.GetUnit(unit.Id) == null) return;
            Trap trap = null;
            foreach (var t in state.Traps)
                if (t.Pos == unit.Pos && t.Owner != unit.Owner)
                {
                    trap = t;
                    break;
                }
            if (trap == null) return;

            state.TrapList.Remove(trap);
            var def = trap.Card.Support;
            events.Add(new TrapTriggered(trap.Owner, trap.Card.Id, def.Id, trap.Pos, unit.Id));
            switch (def.Effect)
            {
                case EffectKind.TrapDamage: // C-20 (+ P-02 hook). Shield applies (U-20, C-12).
                    int amount = def.Amount + Passives.TrapBonusDamage(state, trap.Owner);
                    Combat.DealDamage(state, trap.Owner, DamageDealt.NoUnit, unit.Pos, amount, DamageKind.Trap, events);
                    break;
                case EffectKind.TrapTeleportHome: // C-21: random empty cell of the unit owner's home zone; none = nothing.
                    var cells = new List<Hex>();
                    foreach (var h in Board.Cells)
                        if (Board.IsHomeZone(h, unit.Owner) && Movement.IsEmpty(state, h)) cells.Add(h);
                    if (cells.Count > 0) Teleport(state, unit, cells[state.Rng.NextInt(cells.Count)], events);
                    break;
            }
        }

        /// <summary>C-15, C-21: move the unit to an empty cell. Breaks its overwatch (U-28); triggers only traps
        /// (C-32, chains allowed), never tower or overwatch shots (U-27).</summary>
        internal static void Teleport(GameState state, Unit unit, Hex dest, EventLog events)
        {
            var from = unit.Pos;
            unit.Pos = dest;
            events.Add(new UnitTeleported(unit.Id, from, dest));
            Defense.BreakOverwatch(state, unit, events);
            ResolveArrival(state, unit, events);
        }

        /// <summary>C-35 hook for map events (M4): every trap on a cell that became rock is destroyed.</summary>
        internal static void RemoveAt(GameState state, Hex h, List<GameEvent> events) => RemoveAt(state, h, new EventLog(state, events));

        internal static void RemoveAt(GameState state, Hex h, EventLog events)
        {
            for (int i = 0; i < state.TrapList.Count; )
            {
                var t = state.TrapList[i];
                if (t.Pos != h) { i++; continue; }
                state.TrapList.RemoveAt(i); // placement order, so the events come out oldest first
                events.Add(new TrapRemoved(t.Owner, t.Card.Id, h));
            }
        }
    }
}
