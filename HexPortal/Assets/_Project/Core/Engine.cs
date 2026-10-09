using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>
    /// Commands in, events out. Apply validates with the same rule functions GetLegalCommands uses
    /// (Setup.IsLegal, Turn.CanAct, Movement.Destinations, Combat.LegalTargets, Deploy.IsLegal, Cards.IsLegal,
    /// Pools.DrawOptions), fully before any change. Fog memory is updated with every event and at the end (V-03).
    /// </summary>
    public static class Engine
    {
        /// <summary>Throws IllegalCommandException (state untouched) if the command is not legal.
        /// Returns every event (server, tests); EventFilter.For gives each player's view of them.</summary>
        public static List<GameEvent> Apply(GameState state, ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (state.IsOver) throw new IllegalCommandException("The game is over: " + command);
            var events = new EventLog(state);
            if (state.InSetup)
            {
                Setup.Apply(state, command, events); // S-05: both players act independently
                return events.Items;
            }
            if (Setup.IsSetupCommand(command)) throw new IllegalCommandException("Setup is over: " + command);
            if (command.Player != state.ActivePlayer) throw new IllegalCommandException("Not your turn: " + command);
            if (command is TurnTimeoutCommand)
            {
                Turn.Timeout(state, events); // T-09: even with a pending draw
                Visibility.UpdateMemory(state);
                return events.Items;
            }
            // T-04 step 4: the draw comes first and is mandatory. D-05 (v4.0): a paid Market refresh is allowed before it.
            if (!(command is RefreshMarketCommand) && state.IsDrawPending(command.Player) != (command is DrawCommand))
                throw new IllegalCommandException((command is DrawCommand ? "No draw pending: " : "Draw first: ") + command);

            switch (command)
            {
                case DrawCommand d: Draw(state, d, events); break;
                case MoveCommand m: Move(state, m, events); break;
                case AttackCommand a: Attack(state, a, events); break;
                case MoveAttackCommand ma: ApplyMoveAttack(state, ma, events); break;
                case OverwatchCommand o: Overwatch(state, o, events); break;
                case DeployCommand dep: Deploy.Apply(state, dep, events); break;
                case PlayCardCommand pc: Cards.Apply(state, pc, events); break;
                case RefreshMarketCommand rm: Pools.ApplyRefresh(state, rm, events); break;
                case PlaceTorchCommand pt: Torches.Apply(state, pt, events); break;
                case EndTurnCommand _:
                    state.SetConsecutiveTimeouts(command.Player, 0); // T-09: only automatic ends in a row count
                    Turn.EndTurn(state, events);
                    break;
                default: throw new IllegalCommandException("Unknown command: " + command);
            }
            Visibility.UpdateMemory(state); // V-03 (e.g. after a unit was removed)
            return events.Items;
        }

        /// <summary>All legal commands for the player, in a fixed order. Empty when the game is over.
        /// Setup: the player's options at their setup step (Setup.AddLegal). Waiting player: nothing (T-11 removed in v4.0).
        /// Active player with a pending draw: draw options, then the paid refresh (T-04, D-05).
        /// Otherwise: units by id (moves, attacks, overwatch), deploys, card plays, torches (V-12), refresh (D-05), then EndTurn.
        /// Clock commands (SetupTimeoutCommand, TurnTimeoutCommand) are never listed; the client sends them.</summary>
        public static List<ICommand> GetLegalCommands(GameState state, PlayerId player)
        {
            var list = new List<ICommand>();
            if (state.IsOver) return list;
            if (state.InSetup)
            {
                Setup.AddLegal(state, player, list);
                return list;
            }
            if (player != state.ActivePlayer) return list; // T-11 KALDIRILDI (v4.0): the waiting player has no command
            if (state.IsDrawPending(player))
            {
                foreach (int slot in Pools.DrawOptions(state)) list.Add(new DrawCommand(player, slot));
                if (Pools.CanRefresh(state, player)) list.Add(new RefreshMarketCommand(player)); // D-05 (v4.0)
                return list;
            }
            var visible = Visibility.VisibleCells(state, player);
            foreach (var u in state.Units)
            {
                if (u.Owner != player || !Turn.CanAct(state, u)) continue;
                var dests = Movement.Destinations(state, u, visible);
                foreach (var h in dests) list.Add(new MoveCommand(player, u.Id, h));
                foreach (var h in Combat.LegalTargets(state, u, visible)) list.Add(new AttackCommand(player, u.Id, h));
                list.Add(new OverwatchCommand(player, u.Id));
                if (Turn.CanMoveAttack(state, u)) // T-05 (v3.4): one command per target, automatic approach, targets in board order
                    foreach (var o in MoveAttack.Options(state, u, visible))
                        list.Add(new MoveAttackCommand(player, u.Id, o.Value, o.Key));
            }
            var zone = ControlZone.Cells(state, player);
            Deploy.AddLegal(state, player, visible, zone, list);
            Cards.AddLegal(state, player, visible, zone, list);
            Torches.AddLegal(state, player, list);                                           // V-12 (v4.0)
            if (Pools.CanRefresh(state, player)) list.Add(new RefreshMarketCommand(player)); // D-05 (v4.0)
            list.Add(new EndTurnCommand(player));
            return list;
        }

        static void Draw(GameState state, DrawCommand cmd, EventLog events)
        {
            if (!Pools.DrawOptions(state).Contains(cmd.Slot)) throw new IllegalCommandException("Illegal draw: " + cmd);
            state.SetDrawPending(cmd.Player, false);
            Pools.Draw(state, cmd.Player, cmd.Slot, events);
        }

        static Unit ActingUnit(GameState state, ICommand command, int unitId)
        {
            var u = state.GetUnit(unitId);
            if (u == null || u.Owner != command.Player) throw new IllegalCommandException("Not your unit: " + command);
            if (!Turn.CanAct(state, u)) throw new IllegalCommandException("Unit cannot act (acted or no Energy): " + command);
            return u;
        }

        static void Move(GameState state, MoveCommand cmd, EventLog events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            if (!Movement.Destinations(state, u).Contains(cmd.Dest)) throw new IllegalCommandException("Illegal destination: " + cmd);

            Turn.SpendAction(state, u, Catalog.EnergyPerAction);
            Arrive(state, u, cmd.Dest, events);
        }

        static void Arrive(GameState state, Unit u, Hex dest, EventLog events)
        {
            var from = u.Pos;
            u.Pos = dest;
            events.Add(new UnitMoved(u.Id, from, dest));
            Defense.ResolveArrivalTriggers(state, u, ArrivalKind.Move, events);
        }

        static void Attack(GameState state, AttackCommand cmd, EventLog events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            if (!Combat.LegalTargets(state, u).Contains(cmd.Target)) throw new IllegalCommandException("Illegal target: " + cmd);

            Turn.SpendAction(state, u, Catalog.EnergyPerAction);
            Combat.ResolveAttack(state, u, cmd.Target, DamageKind.Attack, false, events);
            Defense.ResolveAttackTriggers(state, u, events); // U-29: after the attack and its splash
        }

        /// <summary>T-05 (v3.2): validated like a move to Approach plus an attack on Target from Approach (same functions as
        /// GetLegalCommands). Arrival first (trap, then tower and overwatch, U-29, C-32); the attack (with the U-02 Charge)
        /// happens only if the unit is still alive on Approach, the game goes on and Target is still a legal target. The
        /// Energy is spent either way.</summary>
        static void ApplyMoveAttack(GameState state, MoveAttackCommand cmd, EventLog events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            if (!Turn.CanMoveAttack(state, u)) throw new IllegalCommandException("Move-attack needs " + Catalog.MoveAttackEnergy + " Energy: " + cmd);
            // T-05 (v3.4): the approach is automatic; the command must carry exactly the computed one.
            var approach = MoveAttack.ApproachFor(state, u, cmd.Target);
            if (approach == null) throw new IllegalCommandException("Illegal target: " + cmd);
            if (approach.Value != cmd.Approach) throw new IllegalCommandException("Approach must be " + approach.Value + ": " + cmd);

            // v3.2 (PM S1): the target is locked now. After the arrival only the unit's death or displacement (Mirror Trap)
            // or the target's death cancels the attack; newly seen cover or other visibility changes do not.
            var targetUnit = state.UnitAt(cmd.Target);
            Turn.SpendAction(state, u, Catalog.MoveAttackEnergy);
            Arrive(state, u, cmd.Approach, events);
            if (state.IsOver || state.GetUnit(u.Id) == null || u.Pos != cmd.Approach) return;
            // Guard (PM Q1): no current rule can damage or move the target during the arrival; add a test if one appears.
            if (targetUnit != null ?state.GetUnit(targetUnit.Id) == null || targetUnit.Pos != cmd.Target
                                   : !Combat.HoldsEnemy(state, u.Owner, cmd.Target)) return;
            Combat.ResolveAttack(state, u, cmd.Target, DamageKind.Attack, true, events);
            Defense.ResolveAttackTriggers(state, u, events);
        }

        static void Overwatch(GameState state, OverwatchCommand cmd, EventLog events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            Turn.SpendAction(state, u, Catalog.EnergyPerAction);
            u.OnOverwatch = true;
            events.Add(new OverwatchSet(u.Id));
        }
    }
}
