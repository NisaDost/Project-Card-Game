using System;

namespace HexPortal.Core
{
    /// <summary>A player's order. Commands compare by value, so they can be looked up in the legal list.</summary>
    public interface ICommand
    {
        PlayerId Player { get; }
    }

    /// <summary>Thrown by Engine.Apply for a command that is not legal. The state is left unchanged.</summary>
    public sealed class IllegalCommandException : InvalidOperationException
    {
        public IllegalCommandException(string message) : base(message) { }
    }

    /// <summary>U-07, U-09: move a unit to a destination cell (T-05 action).</summary>
    public sealed class MoveCommand : ICommand
    {
        public PlayerId Player { get; }
        public readonly int UnitId;
        public readonly Hex Dest;

        public MoveCommand(PlayerId player, int unitId, Hex dest)
        {
            Player = player;
            UnitId = unitId;
            Dest = dest;
        }

        public override bool Equals(object obj) =>
            obj is MoveCommand c && c.Player == Player && c.UnitId == UnitId && c.Dest == Dest;
        public override int GetHashCode() => ((int)Player * 31 + UnitId) * 397 ^ Dest.GetHashCode();
        public override string ToString() => "Move(" + Player + " #" + UnitId + " -> " + Dest + ")";
    }

    /// <summary>Attack the cell holding an enemy unit or the enemy tower (T-05 action).</summary>
    public sealed class AttackCommand : ICommand
    {
        public PlayerId Player { get; }
        public readonly int UnitId;
        public readonly Hex Target;

        public AttackCommand(PlayerId player, int unitId, Hex target)
        {
            Player = player;
            UnitId = unitId;
            Target = target;
        }

        public override bool Equals(object obj) =>
            obj is AttackCommand c && c.Player == Player && c.UnitId == UnitId && c.Target == Target;
        public override int GetHashCode() => ((int)Player * 31 + UnitId) * 397 ^ Target.GetHashCode() ^ 0x5A5A;
        public override string ToString() => "Attack(" + Player + " #" + UnitId + " -> " + Target + ")";
    }

    /// <summary>U-28: go on overwatch (T-05 action).</summary>
    public sealed class OverwatchCommand : ICommand
    {
        public PlayerId Player { get; }
        public readonly int UnitId;

        public OverwatchCommand(PlayerId player, int unitId)
        {
            Player = player;
            UnitId = unitId;
        }

        public override bool Equals(object obj) => obj is OverwatchCommand c && c.Player == Player && c.UnitId == UnitId;
        public override int GetHashCode() => ((int)Player * 31 + UnitId) * 397 ^ 0x3C3C;
        public override string ToString() => "Overwatch(" + Player + " #" + UnitId + ")";
    }

    /// <summary>T-08. Always legal for the active player while the game is running.</summary>
    public sealed class EndTurnCommand : ICommand
    {
        public PlayerId Player { get; }

        public EndTurnCommand(PlayerId player)
        {
            Player = player;
        }

        public override bool Equals(object obj) => obj is EndTurnCommand c && c.Player == Player;
        public override int GetHashCode() => (int)Player ^ 0x7E7E;
        public override string ToString() => "EndTurn(" + Player + ")";
    }
}
