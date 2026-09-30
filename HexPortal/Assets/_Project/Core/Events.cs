namespace HexPortal.Core
{
    /// <summary>What happened during Engine.Apply, in order. Per-player filtering (fog) is M4.</summary>
    public abstract class GameEvent { }

    public enum DamageKind { Attack, Splash, TowerShot, Overwatch, Trap, Poison }

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
        /// <summary>SourceUnitId for damage without a source unit (trap, poison).</summary>
        public const int NoUnit = -2;

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

    /// <summary>U-22. Killer is the player whose attack, splash, shot, trap or poison dealt the lethal damage. Followed by the U-23 draw.</summary>
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

    /// <summary>Overwatch ended without firing (end of the opponent's turn, or broken by push/teleport).</summary>
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

    public enum DrawSource { Market, Blind }

    /// <summary>D-05, D-06, U-23: a card went into the player's hand.</summary>
    public sealed class CardDrawn : GameEvent
    {
        public readonly PlayerId Player;
        public readonly int CardId;
        public readonly DrawSource Source;
        public CardDrawn(PlayerId player, int cardId, DrawSource source) { Player = player; CardId = cardId; Source = source; }
    }

    /// <summary>D-05: the slot was refilled from its pool. CardId 0 = the pool was empty, the slot stays empty.</summary>
    public sealed class MarketRefilled : GameEvent
    {
        public readonly CardPool Slot;
        public readonly int CardId;
        public MarketRefilled(CardPool slot, int cardId) { Slot = slot; CardId = cardId; }
    }

    /// <summary>T-07.</summary>
    public sealed class UnitDeployed : GameEvent
    {
        public readonly int UnitId;
        public readonly int CardId;
        public readonly Hex Cell;
        public UnitDeployed(int unitId, int cardId, Hex cell) { UnitId = unitId; CardId = cardId; Cell = cell; }
    }

    /// <summary>C-01: a support card was played (Mana paid). Its effect events follow.</summary>
    public sealed class CardPlayed : GameEvent
    {
        public readonly PlayerId Player;
        public readonly int CardId;
        public readonly string DefId;
        public readonly Hex Target;
        public CardPlayed(PlayerId player, int cardId, string defId, Hex target) { Player = player; CardId = cardId; DefId = defId; Target = target; }
    }

    /// <summary>C-05: a buff or debuff now sits in a unit's slot.</summary>
    public sealed class EffectApplied : GameEvent
    {
        public readonly int UnitId;
        public readonly string DefId;
        public EffectApplied(int unitId, string defId) { UnitId = unitId; DefId = defId; }
    }

    /// <summary>C-04 timer ran out, or C-05 a newer effect replaced it.</summary>
    public sealed class EffectExpired : GameEvent
    {
        public readonly int UnitId;
        public readonly string DefId;
        public EffectExpired(int unitId, string defId) { UnitId = unitId; DefId = defId; }
    }

    /// <summary>C-18. From == To when the push could not move the unit.</summary>
    public sealed class UnitPushed : GameEvent
    {
        public readonly int UnitId;
        public readonly Hex From;
        public readonly Hex To;
        public UnitPushed(int unitId, Hex from, Hex to) { UnitId = unitId; From = from; To = to; }
    }

    /// <summary>C-15 or C-21.</summary>
    public sealed class UnitTeleported : GameEvent
    {
        public readonly int UnitId;
        public readonly Hex From;
        public readonly Hex To;
        public UnitTeleported(int unitId, Hex from, Hex to) { UnitId = unitId; From = from; To = to; }
    }

    /// <summary>C-30. Hidden from the opponent (C-33); M4 filters it.</summary>
    public sealed class TrapPlaced : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int CardId;
        public readonly Hex Cell;
        public TrapPlaced(PlayerId owner, int cardId, Hex cell) { Owner = owner; CardId = cardId; Cell = cell; }
    }

    /// <summary>C-32, C-33: shown to both players. The trap's effect events follow.</summary>
    public sealed class TrapTriggered : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int CardId;
        public readonly string DefId;
        public readonly Hex Cell;
        public readonly int UnitId;
        public TrapTriggered(PlayerId owner, int cardId, string defId, Hex cell, int unitId)
        {
            Owner = owner; CardId = cardId; DefId = defId; Cell = cell; UnitId = unitId;
        }
    }

    /// <summary>C-35: the trap was destroyed without triggering.</summary>
    public sealed class TrapRemoved : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int CardId;
        public readonly Hex Cell;
        public TrapRemoved(PlayerId owner, int cardId, Hex cell) { Owner = owner; CardId = cardId; Cell = cell; }
    }

    /// <summary>C-12: the Shield took the whole damage and is gone (no DamageDealt for that hit).</summary>
    public sealed class ShieldBlocked : GameEvent
    {
        public readonly int UnitId;
        public readonly int Amount;
        public readonly DamageKind Kind;
        public ShieldBlocked(int unitId, int amount, DamageKind kind) { UnitId = unitId; Amount = amount; Kind = kind; }
    }

    public sealed class GameOver : GameEvent
    {
        public readonly PlayerId Winner;
        public GameOver(PlayerId winner) { Winner = winner; }
    }
}
