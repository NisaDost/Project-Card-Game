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

        /// <summary>The unit has stopped on its cell (move, push, teleport incl. Mirror Trap, deploy, P-01 return, U-44 spawn): V-12 enemy torch out,
        /// the trap there (C-32), then B-16 Lava and P-06 if the unit is still alive on that cell. Passing through never calls this
        /// (interim: Lava damages only the stop cell).</summary>
        internal static void ResolveArrival(GameState state, Unit unit, EventLog events)
        {
            if (state.IsOver || state.GetUnit(unit.Id) == null) return;
            var cell = unit.Pos;
            Torches.OnArrival(state, unit, events); // V-12 (v4.0): an enemy torch here goes out
            TriggerTrap(state, unit, events);
            if (state.IsOver || state.GetUnit(unit.Id) == null || unit.Pos != cell) return;
            if (state.Map.IsLava(cell)) // B-16 (v4.0), C-18: entering Lava. No killer credit (the owner is the source).
            {
                Combat.DealDamage(state, unit.Owner, DamageDealt.NoUnit, cell, Catalog.LavaDamage, DamageKind.Lava, events);
                if (state.IsOver || state.GetUnit(unit.Id) == null) return;
            }
            Passives.PortalWarden(state, unit, events);
        }

        /// <summary>B-16 (v4.0), T-08: each own unit ending the own turn on Lava takes LavaDamage.</summary>
        internal static void LavaTurnEnd(GameState state, PlayerId p, EventLog events)
        {
            var burning = new List<Unit>();
            foreach (var u in state.Units)
                if (u.Owner == p && state.Map.IsLava(u.Pos)) burning.Add(u);
            foreach (var u in burning)
                if (!state.IsOver && state.GetUnit(u.Id) != null)
                    Combat.DealDamage(state, p, DamageDealt.NoUnit, u.Pos, Catalog.LavaDamage, DamageKind.Lava, events);
        }

        // C-32: only an opponent's trap fires (C-34); it is used up.
        static void TriggerTrap(GameState state, Unit unit, EventLog events)
        {
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
            state.GetProgress(trap.Owner).TrapsSprung++; // Q-17
            // C-20 damage and the P-02 bonus (also on the Mirror Trap, dealt before its teleport; v2.8). Shield applies (U-20).
            int amount = def.Amount + Passives.TrapBonusDamage(state, trap.Owner, events);
            if (amount > 0)
            {
                Combat.DealDamage(state, trap.Owner, DamageDealt.NoUnit, unit.Pos, amount, DamageKind.Trap, events);
                if (state.IsOver || state.GetUnit(unit.Id) == null) return;
            }
            switch (def.Effect)
            {
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
