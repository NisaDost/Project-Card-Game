using System;
using System.Collections.Generic;

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
            if (command is PrePickCommand pre) // T-11: the only command of the waiting player
            {
                if (pre.Player == state.ActivePlayer || !Pools.PrePickOptions(state).Contains(pre.Slot))
                    throw new IllegalCommandException("Illegal pre-pick: " + command);
                Pools.SetPrePick(state, pre.Player, pre.Slot);
                events.Add(new PrePickSet(pre.Player, pre.Slot));
                return events.Items;
            }
            if (command.Player != state.ActivePlayer) throw new IllegalCommandException("Not your turn: " + command);
            if (command is TurnTimeoutCommand)
            {
                Turn.Timeout(state, events); // T-09: even with a pending draw
                Visibility.UpdateMemory(state);
                return events.Items;
            }
            // T-04 step 4: the draw comes first and is mandatory.
            if (state.IsDrawPending(command.Player) != (command is DrawCommand))
                throw new IllegalCommandException((command is DrawCommand ? "No draw pending: " : "Draw first: ") + command);

            switch (command)
            {
                case DrawCommand d: Draw(state, d, events); break;
                case MoveCommand m: Move(state, m, events); break;
                case AttackCommand a: Attack(state, a, events); break;
                case OverwatchCommand o: Overwatch(state, o, events); break;
                case DeployCommand dep: Deploy.Apply(state, dep, events); break;
                case PlayCardCommand pc: Cards.Apply(state, pc, events); break;
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
        /// Setup: the player's options at their setup step (Setup.AddLegal). Waiting player: pre-pick options only (T-11).
        /// Active player with a pending draw: draw options only (T-04).
        /// Otherwise: units by id (moves, attacks, overwatch), deploys, card plays, then EndTurn.
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
            if (player != state.ActivePlayer)
            {
                foreach (int slot in Pools.PrePickOptions(state)) list.Add(new PrePickCommand(player, slot));
                return list;
            }
            if (state.IsDrawPending(player))
            {
                foreach (int slot in Pools.DrawOptions(state)) list.Add(new DrawCommand(player, slot));
                return list;
            }
            var visible = Visibility.VisibleCells(state, player);
            foreach (var u in state.Units)
            {
                if (u.Owner != player || !Turn.CanAct(state, u)) continue;
                foreach (var h in Movement.Destinations(state, u, visible)) list.Add(new MoveCommand(player, u.Id, h));
                foreach (var h in Combat.LegalTargets(state, u, visible)) list.Add(new AttackCommand(player, u.Id, h));
                list.Add(new OverwatchCommand(player, u.Id));
            }
            var zone = ControlZone.Cells(state, player);
            Deploy.AddLegal(state, player, visible, zone, list);
            Cards.AddLegal(state, player, visible, zone, list);
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

            Turn.SpendAction(state, u);
            var from = u.Pos;
            u.Pos = cmd.Dest;
            u.MovedThisTurn = true;
            events.Add(new UnitMoved(u.Id, from, cmd.Dest));
            Defense.ResolveArrivalTriggers(state, u, ArrivalKind.Move, events);
        }

        static void Attack(GameState state, AttackCommand cmd, EventLog events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            if (!Combat.LegalTargets(state, u).Contains(cmd.Target)) throw new IllegalCommandException("Illegal target: " + cmd);

            Turn.SpendAction(state, u);
            Combat.ResolveAttack(state, u, cmd.Target, DamageKind.Attack, events);
            Defense.ResolveAttackTriggers(state, u, events); // U-29: after the attack and its splash
        }

        static void Overwatch(GameState state, OverwatchCommand cmd, EventLog events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            Turn.SpendAction(state, u);
            u.OnOverwatch = true;
            events.Add(new OverwatchSet(u.Id));
        }
    }
}
