using System;
using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>
    /// Commands in, events out. Apply validates with the same rule functions GetLegalCommands uses
    /// (Turn.CanAct, Movement.Destinations, Combat.LegalTargets), fully before any change.
    /// </summary>
    public static class Engine
    {
        /// <summary>Throws IllegalCommandException (state untouched) if the command is not legal.</summary>
        public static List<GameEvent> Apply(GameState state, ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (state.IsOver) throw new IllegalCommandException("The game is over: " + command);
            if (command.Player != state.ActivePlayer) throw new IllegalCommandException("Not your turn: " + command);

            var events = new List<GameEvent>();
            switch (command)
            {
                case MoveCommand m: Move(state, m, events); break;
                case AttackCommand a: Attack(state, a, events); break;
                case OverwatchCommand o: Overwatch(state, o, events); break;
                case EndTurnCommand _: Turn.EndTurn(state, events); break;
                default: throw new IllegalCommandException("Unknown command: " + command);
            }
            return events;
        }

        /// <summary>All legal commands for the player, in a fixed order: units by id (moves, attacks, overwatch), then EndTurn.
        /// Empty when it is not the player's turn or the game is over.</summary>
        public static List<ICommand> GetLegalCommands(GameState state, PlayerId player)
        {
            var list = new List<ICommand>();
            if (state.IsOver || player != state.ActivePlayer) return list;
            var visible = Visibility.VisibleCells(state, player);
            foreach (var u in state.Units)
            {
                if (u.Owner != player || !Turn.CanAct(state, u)) continue;
                foreach (var h in Movement.Destinations(state, u, visible)) list.Add(new MoveCommand(player, u.Id, h));
                foreach (var h in Combat.LegalTargets(state, u, visible)) list.Add(new AttackCommand(player, u.Id, h));
                list.Add(new OverwatchCommand(player, u.Id));
            }
            list.Add(new EndTurnCommand(player));
            return list;
        }

        static Unit ActingUnit(GameState state, ICommand command, int unitId)
        {
            var u = state.GetUnit(unitId);
            if (u == null || u.Owner != command.Player) throw new IllegalCommandException("Not your unit: " + command);
            if (!Turn.CanAct(state, u)) throw new IllegalCommandException("Unit cannot act (acted or no Energy): " + command);
            return u;
        }

        static void Move(GameState state, MoveCommand cmd, List<GameEvent> events)
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

        static void Attack(GameState state, AttackCommand cmd, List<GameEvent> events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            if (!Combat.LegalTargets(state, u).Contains(cmd.Target)) throw new IllegalCommandException("Illegal target: " + cmd);

            Turn.SpendAction(state, u);
            Combat.ResolveAttack(state, u, cmd.Target, DamageKind.Attack, events);
            Defense.ResolveAttackTriggers(state, u, events); // U-29: after the attack and its splash
        }

        static void Overwatch(GameState state, OverwatchCommand cmd, List<GameEvent> events)
        {
            var u = ActingUnit(state, cmd, cmd.UnitId);
            Turn.SpendAction(state, u);
            u.OnOverwatch = true;
            events.Add(new OverwatchSet(u.Id));
        }
    }
}
