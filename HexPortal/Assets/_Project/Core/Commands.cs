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

    /// <summary>T-04 step 4: the mandatory turn-start draw. Slot = a Market slot ((int)CardPool) or Blind (D-06).</summary>
    public sealed class DrawCommand : ICommand
    {
        public const int Blind = -1;

        public PlayerId Player { get; }
        public readonly int Slot;

        public DrawCommand(PlayerId player, int slot)
        {
            Player = player;
            Slot = slot;
        }

        public override bool Equals(object obj) => obj is DrawCommand c && c.Player == Player && c.Slot == Slot;
        public override int GetHashCode() => ((int)Player * 31 + Slot) * 397 ^ 0x1D1D;
        public override string ToString() => "Draw(" + Player + " " + (Slot == Blind ? "Blind" : ((CardPool)Slot).ToString()) + ")";
    }

    /// <summary>T-11: issued by the waiting (non-active) player during the opponent's turn.
    /// Slot = a Market slot, DrawCommand.Blind, or None (clear the pre-pick).</summary>
    public sealed class PrePickCommand : ICommand
    {
        public const int None = -2;

        public PlayerId Player { get; }
        public readonly int Slot;

        public PrePickCommand(PlayerId player, int slot)
        {
            Player = player;
            Slot = slot;
        }

        public override bool Equals(object obj) => obj is PrePickCommand c && c.Player == Player && c.Slot == Slot;
        public override int GetHashCode() => ((int)Player * 31 + Slot) * 397 ^ 0x2E2E;
        public override string ToString() =>
            "PrePick(" + Player + " " + (Slot == None ? "None" : Slot == DrawCommand.Blind ? "Blind" : ((CardPool)Slot).ToString()) + ")";
    }

    /// <summary>T-07: put a character card from the hand on a cell.</summary>
    public sealed class DeployCommand : ICommand
    {
        public PlayerId Player { get; }
        public readonly int CardId;
        public readonly Hex Cell;

        public DeployCommand(PlayerId player, int cardId, Hex cell)
        {
            Player = player;
            CardId = cardId;
            Cell = cell;
        }

        public override bool Equals(object obj) =>
            obj is DeployCommand c && c.Player == Player && c.CardId == CardId && c.Cell == Cell;
        public override int GetHashCode() => ((int)Player * 31 + CardId) * 397 ^ Cell.GetHashCode() ^ 0x4B4B;
        public override string ToString() => "Deploy(" + Player + " card#" + CardId + " -> " + Cell + ")";
    }

    /// <summary>C-01: play a support card. Target = the unit's cell (buff, debuff, Teleport's unit) or the trap cell.
    /// Dest is only for Teleport (C-15); Direction (0–5, Hex.Directions) only for Push (C-18).</summary>
    public sealed class PlayCardCommand : ICommand
    {
        public const int NoDirection = -1;

        public PlayerId Player { get; }
        public readonly int CardId;
        public readonly Hex Target;
        public readonly Hex? Dest;
        public readonly int Direction;

        public PlayCardCommand(PlayerId player, int cardId, Hex target)
            : this(player, cardId, target, null, NoDirection) { }

        /// <summary>C-18 Push.</summary>
        public PlayCardCommand(PlayerId player, int cardId, Hex target, int direction)
            : this(player, cardId, target, null, direction) { }

        /// <summary>C-15 Teleport.</summary>
        public PlayCardCommand(PlayerId player, int cardId, Hex target, Hex dest)
            : this(player, cardId, target, (Hex?)dest, NoDirection) { }

        PlayCardCommand(PlayerId player, int cardId, Hex target, Hex? dest, int direction)
        {
            Player = player;
            CardId = cardId;
            Target = target;
            Dest = dest;
            Direction = direction;
        }

        public override bool Equals(object obj) =>
            obj is PlayCardCommand c && c.Player == Player && c.CardId == CardId && c.Target == Target
            && c.Dest == Dest && c.Direction == Direction;
        public override int GetHashCode() =>
            (((int)Player * 31 + CardId) * 397 ^ Target.GetHashCode()) * 31 + (Dest.HasValue ? Dest.Value.GetHashCode() : 0) + Direction;
        public override string ToString() =>
            "PlayCard(" + Player + " card#" + CardId + " -> " + Target
            + (Dest.HasValue ? " to " + Dest.Value : "") + (Direction != NoDirection ? " dir " + Direction : "") + ")";
    }

    /// <summary>T-08. Legal for the active player while the game is running and no draw is pending (T-04).</summary>
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
