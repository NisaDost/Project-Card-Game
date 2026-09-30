using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>U-27's closed trigger list: ending a move or being deployed. Only the final cell counts
    /// (Rider pass-through never triggers). Push/teleport never trigger shots (v2.4); traps handle them separately (C-32).</summary>
    public enum ArrivalKind { Move, Deployed }

    /// <summary>Off-turn defense: tower shot (U-27), overwatch (U-28), trigger order (U-29).
    /// Runs inside Engine.Apply of the opponent's command; never a player command.</summary>
    public static class Defense
    {
        /// <summary>Trigger (a): the unit ended a move or was deployed (T-07) on its cell. U-29 (v2.6): the trap on that
        /// cell resolves first (C-32); the shots then look at the unit as the trap left it: dead, or moved to another
        /// cell by the Mirror Trap (v2.7; a teleport never triggers, U-27), means no shots.</summary>
        public static void ResolveArrivalTriggers(GameState state, Unit unit, ArrivalKind kind, EventLog events)
        {
            var arrival = unit.Pos;
            Traps.ResolveArrival(state, unit, events);
            if (unit.Pos != arrival) return;
            ResolveTriggers(state, unit, events);
        }

        /// <summary>Trigger (b): the unit made an attack. Call after that attack and its splash fully resolved (U-29).</summary>
        public static void ResolveAttackTriggers(GameState state, Unit attacker, EventLog events) =>
            ResolveTriggers(state, attacker, events);

        /// <summary>U-28: overwatch ends without firing (end of the opponent's turn, pushed, teleported).</summary>
        public static void BreakOverwatch(GameState state, Unit unit, EventLog events)
        {
            if (!unit.OnOverwatch) return;
            unit.OnOverwatch = false;
            events.Add(new OverwatchEnded(unit.Id));
        }

        // U-29: tower first, then overwatching units in ascending id. Stop when the target dies or the game ends.
        static void ResolveTriggers(GameState state, Unit target, EventLog events)
        {
            if (state.IsOver || state.GetUnit(target.Id) == null) return;
            if (target.Owner != state.ActivePlayer) return; // shots happen only during the opponent's turn
            var side = target.Owner.Opponent();
            var visible = Visibility.VisibleCells(state, side);

            var tower = state.GetTower(side);
            int dist = Hex.Distance(tower.Pos, target.Pos);
            if (state.IsTowerShotAvailable(side) && dist >= Catalog.Tower.MinRange && dist <= Catalog.Tower.MaxRange
                && Combat.IsValidTarget(state, side, target.Pos, visible))
            {
                state.SetTowerShotAvailable(side, false);
                Visibility.Reveal(state, tower, events); // V-08: until the end of this turn
                events.Add(new TowerShot(side, target.Id));
                // U-27: always Tower.Attack, no biome/buff/debuff.
                Combat.DealDamage(state, side, DamageDealt.Tower, target.Pos, Catalog.Tower.Attack, DamageKind.TowerShot, events);
                if (state.IsOver || state.GetUnit(target.Id) == null) return;
            }

            var watchers = new List<Unit>();
            foreach (var u in state.Units)
                if (u.Owner == side && u.OnOverwatch) watchers.Add(u);
            foreach (var w in watchers)
            {
                if (!Combat.InRange(w.Def, w.Pos, target.Pos) || !Combat.IsValidTarget(state, side, target.Pos, visible)) continue;
                w.OnOverwatch = false;
                Visibility.Reveal(state, w, events); // V-08: before OverwatchFired, so the target's owner sees the shot
                events.Add(new OverwatchFired(w.Id, target.Id));
                Combat.ResolveAttack(state, w, target.Pos, DamageKind.Overwatch, events);
                if (state.IsOver || state.GetUnit(target.Id) == null) return;
            }
        }
    }
}
