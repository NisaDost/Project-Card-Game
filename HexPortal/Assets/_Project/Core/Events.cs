namespace HexPortal.Core
{
    /// <summary>What happened during Engine.Apply, in order. Per-player filtering (fog) is M4.</summary>
    public abstract class GameEvent { }

    public enum DamageKind { Attack, Splash, TowerShot, Overwatch }

    public sealed class UnitMoved : GameEvent
    {
        public readonly int UnitId;
        public readonly Hex From;
        public readonly Hex To;
        public UnitMoved(int unitId, Hex from, Hex to) { UnitId = unitId; From = from; To = to; }
    }

    public sealed class DamageDealt : GameEvent
    {
        /// <summary>Used as SourceUnitId / TargetUnitId when the source / target is a tower.</summary>
        public const int Tower = -1;

        public readonly PlayerId SourcePlayer;
        public readonly int SourceUnitId;
        public readonly Hex Target;
        public readonly int TargetUnitId;
        public readonly int Amount;
        public readonly DamageKind Kind;

        public DamageDealt(PlayerId sourcePlayer, int sourceUnitId, Hex target, int targetUnitId, int amount, DamageKind kind)
        {
            SourcePlayer = sourcePlayer; SourceUnitId = sourceUnitId; Target = target;
            TargetUnitId = targetUnitId; Amount = amount; Kind = kind;
        }
    }

    /// <summary>U-22. Killer is the player whose attack, splash or shot dealt the lethal damage (U-23 draw: M3).</summary>
    public sealed class UnitDied : GameEvent
    {
        public readonly int UnitId;
        public readonly PlayerId Owner;
        public readonly PlayerId Killer;
        public UnitDied(int unitId, PlayerId owner, PlayerId killer) { UnitId = unitId; Owner = owner; Killer = killer; }
    }

    /// <summary>U-27. Followed by the DamageDealt of the shot.</summary>
    public sealed class TowerShot : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int TargetUnitId;
        public TowerShot(PlayerId owner, int targetUnitId) { Owner = owner; TargetUnitId = targetUnitId; }
    }

    public sealed class OverwatchSet : GameEvent
    {
        public readonly int UnitId;
        public OverwatchSet(int unitId) { UnitId = unitId; }
    }

    /// <summary>U-28. Followed by the attack's DamageDealt (and splash).</summary>
    public sealed class OverwatchFired : GameEvent
    {
        public readonly int UnitId;
        public readonly int TargetUnitId;
        public OverwatchFired(int unitId, int targetUnitId) { UnitId = unitId; TargetUnitId = targetUnitId; }
    }

    /// <summary>Overwatch ended without firing (end of the opponent's turn, or broken in M3).</summary>
    public sealed class OverwatchEnded : GameEvent
    {
        public readonly int UnitId;
        public OverwatchEnded(int unitId) { UnitId = unitId; }
    }

    public sealed class UnitHealed : GameEvent
    {
        public readonly int UnitId;
        public readonly int Amount;
        public UnitHealed(int unitId, int amount) { UnitId = unitId; Amount = amount; }
    }

    public sealed class TowerDestroyed : GameEvent
    {
        public readonly PlayerId Owner;
        public TowerDestroyed(PlayerId owner) { Owner = owner; }
    }

    public sealed class TurnStarted : GameEvent
    {
        public readonly PlayerId Player;
        public readonly int Round;
        public TurnStarted(PlayerId player, int round) { Player = player; Round = round; }
    }

    public sealed class GameOver : GameEvent
    {
        public readonly PlayerId Winner;
        public GameOver(PlayerId winner) { Winner = winner; }
    }
}
