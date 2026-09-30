using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§7 turn flow (T-01…T-05, T-08, T-10) and the tower win (W-02). Quests (T-08) and W-01 (T-04 step 3) are M4.</summary>
    public static class Turn
    {
        /// <summary>T-05: the owner is active, the game runs, the unit has not acted, and there is Energy for an action.</summary>
        public static bool CanAct(GameState state, Unit unit) =>
            !state.IsOver && unit.Owner == state.ActivePlayer && !unit.ActedThisTurn
            && state.GetEnergy(unit.Owner) >= Catalog.EnergyPerAction;

        internal static void SpendAction(GameState state, Unit unit)
        {
            state.SetEnergy(unit.Owner, state.GetEnergy(unit.Owner) - Catalog.EnergyPerAction);
            unit.ActedThisTurn = true;
        }

        /// <summary>T-08 then T-10, then the next player's turn start (T-04).</summary>
        internal static void EndTurn(GameState state, List<GameEvent> events)
        {
            var p = state.ActivePlayer;
            var opp = p.Opponent();
            foreach (var u in state.Units)
                if (u.Owner == p)
                {
                    u.MovedLastOwnTurn = u.MovedThisTurn; // U-02
                    u.MovedThisTurn = false;
                }
            Cards.EndOfTurn(state, p, events); // C-04, C-14
            foreach (var u in state.Units)
                if (u.Owner == opp) Defense.BreakOverwatch(state, u, events); // U-28, T-08
            state.SetEnergy(p, 0); // T-05: unspent Energy is not carried
            state.SetMana(p, 0);   // T-01: unspent Mana is not carried
            if (p == PlayerId.B) state.Round++; // T-10
            state.ActivePlayer = opp;
            StartTurn(state, events);
        }

        /// <summary>T-04: 1. refill Mana and Energy, reset action flags and the opponent's tower shot; 2. heal (U-05), then
        /// poison (C-17); (3. W-01 is M4); 4. the draw (pre-pick applied, else the player must draw first).
        /// Also used by the setup phase (M4) to start A's first turn.</summary>
        internal static void StartTurn(GameState state, List<GameEvent> events)
        {
            var p = state.ActivePlayer;
            state.SetMana(p, Mana.TurnStartMana(state, p));
            state.SetEnergy(p, Catalog.EnergyPerTurn);
            state.SetTowerShotAvailable(p.Opponent(), true); // U-27: once per opponent turn
            foreach (var u in state.Units)
                if (u.Owner == p) u.ActedThisTurn = false;
            events.Add(new TurnStarted(p, state.Round));
            Heal(state, p, events);
            Cards.PoisonTicks(state, p, events);
            if (!state.IsOver) Pools.TurnStartDraw(state, p, events);
        }

        // U-05, U-24: each own Healer gives HealerHealAmount to each adjacent own unit (not itself, never towers); stacks.
        static void Heal(GameState state, PlayerId p, List<GameEvent> events)
        {
            foreach (var u in state.Units)
            {
                if (u.Owner != p) continue;
                int amount = 0;
                foreach (var h in state.Units)
                    if (h.Owner == p && h.Class == UnitClass.Healer && h.Id != u.Id && Hex.Distance(h.Pos, u.Pos) == 1)
                        amount += Catalog.HealerHealAmount;
                int gained = Math.Min(u.Def.Health, u.Health + amount) - u.Health;
                if (gained <= 0) continue;
                u.Health += gained;
                events.Add(new UnitHealed(u.Id, gained));
            }
        }

        /// <summary>W-02: checked immediately whenever a tower reaches 0 Health.</summary>
        internal static void TowerDestroyed(GameState state, Tower tower, List<GameEvent> events)
        {
            var winner = tower.Owner.Opponent();
            state.Winner = winner;
            events.Add(new TowerDestroyed(tower.Owner));
            events.Add(new GameOver(winner));
        }
    }
}
