using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§9 commander passives P-01…P-06. P-00: a passive is hidden until it first takes effect; at that moment a
    /// public PassiveRevealed is emitted, before the effect's own events. Amounts and rounds come from the PassiveDef.</summary>
    public static class Passives
    {
        static PassiveDef Get(GameState state, PlayerId p, string id)
        {
            var d = state.GetPassiveChoice(p);
            return d != null && d.Id == id ? d : null;
        }

        /// <summary>P-00.</summary>
        static void Reveal(GameState state, PlayerId p, EventLog events)
        {
            var prog = state.GetProgress(p);
            if (prog.PassiveRevealed) return;
            prog.PassiveRevealed = true;
            events.Add(new PassiveRevealed(p, state.GetPassiveChoice(p).Id));
        }

        /// <summary>P-01 hook (U-22): the owner's first unit death is remembered for the return (once per match).</summary>
        internal static void OnUnitDied(GameState state, Unit unit)
        {
            var prog = state.GetProgress(unit.Owner);
            if (Get(state, unit.Owner, "P-01") == null || prog.LastBreathUsed) return;
            prog.LastBreathUsed = true;
            prog.LastBreathPending = true;
            prog.LastBreathClass = unit.Class;
            prog.LastBreathBiome = unit.Biome;
            prog.LastBreathDeathTurn = state.TurnIndex;
        }

        /// <summary>P-01 Last Breath, T-04 step 2: the first dead unit returns as a new unit (new id, same class and biome,
        /// Amount Health, no effects) on a random empty cell of the owner's home zone. No empty cell: retried at the next
        /// own turn start (v2.8). The arrival triggers an enemy trap (C-32) but never tower or overwatch shots (v2.8).
        /// Like T-07, the returned unit cannot act in the turn it returns (v2.9). A unit that died during this same turn
        /// start (Poison, step 2) returns only at the next own turn start (v2.10).</summary>
        internal static void LastBreath(GameState state, PlayerId p, EventLog events)
        {
            var prog = state.GetProgress(p);
            var def = Get(state, p, "P-01");
            if (def == null || !prog.LastBreathPending || state.IsOver || prog.LastBreathDeathTurn >= state.TurnIndex) return;
            var cells = new List<Hex>();
            foreach (var h in Board.Cells)
                if (Board.IsHomeZone(h, p) && Movement.IsEmpty(state, h)) cells.Add(h);
            if (cells.Count == 0) return;
            prog.LastBreathPending = false;
            Reveal(state, p, events);
            var u = state.AddUnit(p, prog.LastBreathClass, prog.LastBreathBiome, cells[state.Rng.NextInt(cells.Count)]);
            u.Health = def.Amount;
            u.ActedThisTurn = true; // v2.9: no action in the turn it returns (like T-07)
            events.Add(new UnitRevived(u.Id, p, u.Class, u.Biome, u.Pos));
            Traps.ResolveArrival(state, u, events);
        }

        /// <summary>P-02 Trap Master: the extra damage of the trap owner's traps (0 without the passive); reveals it.</summary>
        internal static int TrapBonusDamage(GameState state, PlayerId trapOwner, EventLog events)
        {
            var def = Get(state, trapOwner, "P-02");
            if (def == null) return 0;
            Reveal(state, trapOwner, events);
            return def.Amount;
        }

        /// <summary>P-03 Quick Start: the extra Mana at the owner's turn start in the passive's round (may exceed the cap).</summary>
        internal static int QuickStartMana(GameState state, PlayerId p)
        {
            var def = Get(state, p, "P-03");
            return def != null && state.Round == def.Round ? def.Amount : 0;
        }

        /// <summary>P-03: call after TurnStarted when QuickStartMana was added.</summary>
        internal static void QuickStartApplied(GameState state, PlayerId p, EventLog events) => Reveal(state, p, events);

        /// <summary>P-04 Thick Wall: blocks the first Amount damage (in total, every source) to the owner's tower.
        /// Returns the damage left. Emits TowerDamageBlocked for the blocked part.</summary>
        internal static int ThickWall(GameState state, Tower tower, int amount, EventLog events)
        {
            var def = Get(state, tower.Owner, "P-04");
            var prog = state.GetProgress(tower.Owner);
            if (def == null || amount <= 0 || prog.WallBlocked >= def.Amount) return amount;
            int blocked = System.Math.Min(amount, def.Amount - prog.WallBlocked);
            prog.WallBlocked += blocked;
            Reveal(state, tower.Owner, events);
            events.Add(new TowerDamageBlocked(tower.Owner, blocked));
            return amount - blocked;
        }

        /// <summary>P-05 Merchant: the first Market buy (incl. via pre-pick or timeout) gives one extra blind draw (D-07
        /// applies). The first buy uses the passive up even when the hand is full and the draw is skipped (v2.9).</summary>
        internal static void OnMarketBuy(GameState state, PlayerId player, EventLog events)
        {
            var def = Get(state, player, "P-05");
            var prog = state.GetProgress(player);
            if (def == null || prog.MerchantUsed) return;
            prog.MerchantUsed = true;
            Reveal(state, player, events);
            for (int k = 0; k < def.Amount; k++)
                if (!Pools.IsHandFull(state, player) && Pools.CanBlindDraw(state)) Pools.BlindDraw(state, player, events);
        }

        /// <summary>P-06 Portal Warden: an enemy unit that STOPS on the Portal (move destination, push stop, teleport
        /// arrival; v2.7, v2.10) takes Amount damage (Shield applies); passing through does nothing (only stop cells call
        /// this, via Traps.ResolveArrival). Resolved like a trap, before tower and overwatch shots (v2.8). Never hits the
        /// passive owner's own units; a kill counts for the owner (Q-12).</summary>
        internal static void PortalWarden(GameState state, Unit unit, EventLog events)
        {
            if (unit.Pos != Board.Portal) return;
            var owner = unit.Owner.Opponent();
            var def = Get(state, owner, "P-06");
            if (def == null) return;
            Reveal(state, owner, events);
            Combat.DealDamage(state, owner, DamageDealt.NoUnit, unit.Pos, def.Amount, DamageKind.PortalWarden, events);
        }
    }
}
