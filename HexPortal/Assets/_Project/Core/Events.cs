using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>What happened during Engine.Apply, in order. Engine.Apply returns the full list (server, tests);
    /// each event is tagged when it is emitted with what each player may see of it (EventFilter).</summary>
    public abstract class GameEvent
    {
        GameEvent viewA, viewB;

        /// <summary>The version of this event the player may see: this event, a redacted copy, or null (hidden).
        /// Events not emitted by the engine are hidden from everyone.</summary>
        public GameEvent ViewFor(PlayerId p) => p == PlayerId.A ? viewA : viewB;

        internal void SetView(PlayerId p, GameEvent view)
        {
            if (p == PlayerId.A) viewA = view;
            else viewB = view;
        }
    }

    /// <summary>The event list of one Engine.Apply. Every Add tags the event with the players' current visibility
    /// (V-10) and then updates both players' fog memory (V-03).</summary>
    public sealed class EventLog
    {
        readonly GameState state;
        internal readonly List<GameEvent> Items;

        internal EventLog(GameState state) : this(state, new List<GameEvent>()) { }

        internal EventLog(GameState state, List<GameEvent> items)
        {
            this.state = state;
            Items = items;
        }

        internal void Add(GameEvent e)
        {
            if (state.Light) // AI simulation: no filtering, no fog memory
            {
                Items.Add(e);
                return;
            }
            var va = Visibility.VisibleCells(state, PlayerId.A);
            var vb = Visibility.VisibleCells(state, PlayerId.B);
            EventFilter.Tag(state, e, va, vb);
            Items.Add(e);
            Visibility.UpdateMemory(state, va, vb);
        }
    }

    public enum DamageKind { Attack, Splash, TowerShot, Overwatch, Trap, Poison, PortalWarden }

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

    /// <summary>D-05, D-06, U-23: a card went into the player's hand. The opponent sees a blind draw with CardId 0
    /// (V-09: only the hand count is public); a Market card was public already.</summary>
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

    /// <summary>C-30, S-05. Owner only (C-33).</summary>
    public sealed class TrapPlaced : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int CardId;
        public readonly Hex Cell;
        public TrapPlaced(PlayerId owner, int cardId, Hex cell) { Owner = owner; CardId = cardId; Cell = cell; }
    }

    /// <summary>C-32, C-33: shown to both players; UnitId is 0 for a viewer who cannot see the triggering unit.
    /// The trap's effect events follow.</summary>
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

    /// <summary>W-01…W-04. Public.</summary>
    public sealed class GameOver : GameEvent
    {
        public readonly GameResult Result;
        public GameOver(GameResult result) { Result = result; }
        public PlayerId? Winner => Result.Winner;
    }

    // ---------- Setup (S), owner-only until SetupFinished ----------

    /// <summary>S-03. Owner only.</summary>
    public sealed class QuestsChosen : GameEvent
    {
        public readonly PlayerId Player;
        public readonly IReadOnlyList<string> QuestIds;
        public QuestsChosen(PlayerId player, IReadOnlyList<string> questIds) { Player = player; QuestIds = questIds; }
    }

    /// <summary>S-04. Owner only.</summary>
    public sealed class PassiveChosen : GameEvent
    {
        public readonly PlayerId Player;
        public readonly string PassiveId;
        public PassiveChosen(PlayerId player, string passiveId) { Player = player; PassiveId = passiveId; }
    }

    /// <summary>S-05. Owner only (setup placements of units use UnitDeployed, traps TrapPlaced).</summary>
    public sealed class TowerPlaced : GameEvent
    {
        public readonly PlayerId Player;
        public readonly Hex Cell;
        public TowerPlaced(PlayerId player, Hex cell) { Player = player; Cell = cell; }
    }

    /// <summary>S-05, S-08: the only setup event the opponent sees.</summary>
    public sealed class SetupFinished : GameEvent
    {
        public readonly PlayerId Player;
        public SetupFinished(PlayerId player) { Player = player; }
    }

    // ---------- Turn (T) ----------

    /// <summary>T-11: the waiting player's pre-pick. Owner only.</summary>
    public sealed class PrePickSet : GameEvent
    {
        public readonly PlayerId Player;
        public readonly int Slot;
        public PrePickSet(PlayerId player, int slot) { Player = player; Slot = slot; }
    }

    /// <summary>T-09: an automatic turn end; Count = consecutive automatic ends (W-04). Public.</summary>
    public sealed class TurnTimedOut : GameEvent
    {
        public readonly PlayerId Player;
        public readonly int Count;
        public TurnTimedOut(PlayerId player, int count) { Player = player; Count = count; }
    }

    // ---------- Quests (Q), passives (P), map events (E): public (V-09) ----------

    /// <summary>Q-03. Public.</summary>
    public sealed class QuestCompleted : GameEvent
    {
        public readonly PlayerId Player;
        public readonly string QuestId;
        public QuestCompleted(PlayerId player, string questId) { Player = player; QuestId = questId; }
    }

    /// <summary>Q-04. Public.</summary>
    public sealed class QuestFailed : GameEvent
    {
        public readonly PlayerId Player;
        public readonly string QuestId;
        public QuestFailed(PlayerId player, string questId) { Player = player; QuestId = questId; }
    }

    /// <summary>P-00: the passive took effect for the first time. Public; its effect events follow.</summary>
    public sealed class PassiveRevealed : GameEvent
    {
        public readonly PlayerId Player;
        public readonly string PassiveId;
        public PassiveRevealed(PlayerId player, string passiveId) { Player = player; PassiveId = passiveId; }
    }

    /// <summary>P-01: the first dead unit returned as a new unit. Seen like a deploy (owner, or the cell is Visible).</summary>
    public sealed class UnitRevived : GameEvent
    {
        public readonly int UnitId;
        public readonly PlayerId Owner;
        public readonly Data.UnitClass Class;
        public readonly Data.Biome Biome;
        public readonly Hex Cell;
        public UnitRevived(int unitId, PlayerId owner, Data.UnitClass cls, Data.Biome biome, Hex cell)
        {
            UnitId = unitId; Owner = owner; Class = cls; Biome = biome; Cell = cell;
        }
    }

    /// <summary>P-04: tower damage absorbed by Thick Wall (no DamageDealt for that part). Public (tower Health, V-09).</summary>
    public sealed class TowerDamageBlocked : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int Amount;
        public TowerDamageBlocked(PlayerId owner, int amount) { Owner = owner; Amount = amount; }
    }

    /// <summary>E-02, E-05: the next map event, its round and cells (order: see MapEvents). Public.</summary>
    public sealed class MapEventAnnounced : GameEvent
    {
        public readonly string DefId;
        public readonly int Round;
        public readonly IReadOnlyList<Hex> Cells;
        public MapEventAnnounced(string defId, int round, IReadOnlyList<Hex> cells) { DefId = defId; Round = round; Cells = cells; }
    }

    /// <summary>E-01, E-04, E-05: the event happened. Cells[i] now has Tiles[i]; Skipped pairs were no longer eligible.
    /// Public.</summary>
    public sealed class MapEventResolved : GameEvent
    {
        public readonly string DefId;
        public readonly IReadOnlyList<Hex> Cells;
        public readonly IReadOnlyList<Tile> Tiles;
        public readonly IReadOnlyList<Hex> Skipped;
        public MapEventResolved(string defId, IReadOnlyList<Hex> cells, IReadOnlyList<Tile> tiles, IReadOnlyList<Hex> skipped)
        {
            DefId = defId; Cells = cells; Tiles = tiles; Skipped = skipped;
        }
    }

    // ---------- Fog (V): redacted views and reveals ----------

    /// <summary>V-08: an attacking unit or tower (UnitId = DamageDealt.Tower) the opponent could not see is now Visible
    /// to them. Opponent only.</summary>
    public sealed class Revealed : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int UnitId;
        public readonly Hex Cell;
        public Revealed(PlayerId owner, int unitId, Hex cell) { Owner = owner; UnitId = unitId; Cell = cell; }
    }

    /// <summary>Redacted move/push/teleport: an enemy unit came into view from an unseen cell.</summary>
    public sealed class UnitAppeared : GameEvent
    {
        public readonly int UnitId;
        public readonly PlayerId Owner;
        public readonly Data.UnitClass Class;
        public readonly Data.Biome Biome;
        public readonly int Health;
        public readonly Hex Cell;
        public UnitAppeared(int unitId, PlayerId owner, Data.UnitClass cls, Data.Biome biome, int health, Hex cell)
        {
            UnitId = unitId; Owner = owner; Class = cls; Biome = biome; Health = health; Cell = cell;
        }
    }

    /// <summary>Redacted move/push/teleport: an enemy unit left the viewer's sight to an unseen cell.</summary>
    public sealed class UnitVanished : GameEvent
    {
        public readonly int UnitId;
        public readonly Hex Cell;
        public UnitVanished(int unitId, Hex cell) { UnitId = unitId; Cell = cell; }
    }

    /// <summary>V-09: redacted damage to a tower the viewer cannot see (tower Health is public).</summary>
    public sealed class TowerHealthChanged : GameEvent
    {
        public readonly PlayerId Owner;
        public readonly int Health;
        public TowerHealthChanged(PlayerId owner, int health) { Owner = owner; Health = health; }
    }
}
