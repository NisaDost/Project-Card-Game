using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§7 turn flow (T-01…T-05, T-08, T-10), timeouts (T-09, W-04) and the tower win (W-02).
    /// Quests (Quests), passives (Passives), map events (MapEvents) and W-01/W-03 (Win) plug in here.</summary>
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

        /// <summary>T-08: timers (C-04), the owner's quests (Q-02), the W-01 Portal watch; at the end of B's turn also the
        /// end of the round: round quests (Q-14, Q-16) and W-03. Then T-10, the round start (map events, E-01/E-02, before
        /// A's turn start) and the next player's turn start (T-04).</summary>
        internal static void EndTurn(GameState state, EventLog events)
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
            Quests.EndOfTurn(state, p, events); // Q-02
            Win.RecordPortal(state, p);         // W-01
            if (p == PlayerId.B)
            {
                Quests.EndOfRound(state, events); // Q-14, Q-16
                Win.CheckRoundLimit(state, events); // W-03
                if (state.IsOver) return;
            }
            foreach (var u in state.Units)
                if (u.Owner == opp) Defense.BreakOverwatch(state, u, events); // U-28, T-08
            state.SetEnergy(p, 0); // T-05: unspent Energy is not carried
            state.SetMana(p, 0);   // T-01: unspent Mana is not carried
            if (p == PlayerId.B) state.Round++; // T-10
            state.ActivePlayer = opp;
            if (p == PlayerId.B) MapEvents.RoundStart(state, events); // E-01, E-02
            StartTurn(state, events);
        }

        /// <summary>T-04: 1. refill Mana (T-01…T-03, P-03) and Energy, reset action flags and the opponent's tower shot;
        /// 2. heal (U-05), poison (C-17), Last Breath (P-01); 3. W-01; 4. the draw (pre-pick applied, else the player must
        /// draw first). Also used by the setup phase to start A's first turn.</summary>
        internal static void StartTurn(GameState state, List<GameEvent> events) => StartTurn(state, new EventLog(state, events));

        internal static void StartTurn(GameState state, EventLog events)
        {
            var p = state.ActivePlayer;
            int quickStart = Passives.QuickStartMana(state, p);
            state.SetMana(p, Mana.TurnStartMana(state, p) + quickStart);
            state.SetEnergy(p, Catalog.EnergyPerTurn);
            state.SetTowerShotAvailable(p.Opponent(), true); // U-27: once per opponent turn
            foreach (var u in state.Units)
                if (u.Owner == p) u.ActedThisTurn = false;
            events.Add(new TurnStarted(p, state.Round));
            if (quickStart > 0) Passives.QuickStartApplied(state, p, events); // P-00
            Heal(state, p, events);
            Cards.PoisonTicks(state, p, events);
            Passives.LastBreath(state, p, events);
            Win.CheckPortal(state, p, events);
            if (!state.IsOver) Pools.TurnStartDraw(state, p, events);
        }

        // U-05, U-24: each own Healer gives HealerHealAmount to each adjacent own unit (not itself, never towers); stacks.
        static void Heal(GameState state, PlayerId p, EventLog events)
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
        internal static void TowerDestroyed(GameState state, Tower tower, EventLog events)
        {
            state.Result = new GameResult(tower.Owner.Opponent(), WinReason.Tower);
            events.Add(new TowerDestroyed(tower.Owner));
            events.Add(new GameOver(state.Result));
        }

        /// <summary>T-09 (v2.8): the active player's clocks ran out. A pending draw is made first: the pre-pick was
        /// already used at turn start (T-11), so it is a blind draw; interim: if no blind draw is possible, the first
        /// non-empty Market slot. Skipped when the hand is full (D-07). Then the automatic turn end is counted; the
        /// MaxConsecutiveTimeouts-th in a row loses at once (W-04, interim: before the opponent's turn starts);
        /// otherwise the turn ends as with EndTurn.</summary>
        internal static void Timeout(GameState state, EventLog events)
        {
            var p = state.ActivePlayer;
            if (state.IsDrawPending(p))
            {
                state.SetDrawPending(p, false);
                var options = Pools.DrawOptions(state);
                if (!Pools.IsHandFull(state, p) && options.Count > 0)
                    Pools.Draw(state, p, options.Contains(DrawCommand.Blind) ? DrawCommand.Blind : options[0], events);
            }
            int count = state.GetConsecutiveTimeouts(p) + 1;
            state.SetConsecutiveTimeouts(p, count);
            events.Add(new TurnTimedOut(p, count));
            if (count >= Catalog.MaxConsecutiveTimeouts)
            {
                state.Result = new GameResult(p.Opponent(), WinReason.Timeout);
                events.Add(new GameOver(state.Result));
                return;
            }
            EndTurn(state, events);
        }
    }
}
