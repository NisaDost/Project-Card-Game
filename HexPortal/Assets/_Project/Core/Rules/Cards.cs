using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§4 support cards: C-01…C-06 and the effects C-10…C-21 (traps in Traps).</summary>
    public static class Cards
    {
        enum TargetKind { FriendlyUnit, EnemyUnit, TrapCell }

        static TargetKind KindOf(SupportCardDef d)
        {
            switch (d.Effect)
            {
                case EffectKind.AttackPenalty:
                case EffectKind.Poison:
                case EffectKind.Push:
                case EffectKind.Root:
                    return TargetKind.EnemyUnit;
                case EffectKind.TrapDamage:
                case EffectKind.TrapTeleportHome:
                    return TargetKind.TrapCell;
                default:
                    return TargetKind.FriendlyUnit;
            }
        }

        /// <summary>C-01, C-02, C-30, C-31, V-07: the single legality check for PlayCardCommand. Never targets towers.
        /// Uses only the player's own hand/traps, the Control Zone and the player's Visible set.</summary>
        internal static bool IsLegal(GameState state, PlayerId p, CardInstance card, Hex target, Hex? dest, int dir,
            ISet<Hex> visible, ISet<Hex> zone)
        {
            if (card == null || card.IsCharacter || state.GetMana(p) < card.Cost) return false;
            var d = card.Support;
            bool push = d.Effect == EffectKind.Push, teleport = d.Effect == EffectKind.Teleport;
            if (push ? dir < 0 || dir >= Hex.Directions.Count : dir != PlayCardCommand.NoDirection) return false;
            if (teleport != dest.HasValue) return false;

            var u = state.UnitAt(target);
            switch (KindOf(d))
            {
                case TargetKind.FriendlyUnit: // own units are always in the own Control Zone
                    if (u == null || u.Owner != p) return false;
                    return !teleport || IsTeleportDest(state, dest.Value, visible, zone);
                case TargetKind.EnemyUnit: // C-02 debuff: in the Control Zone and Visible (V-07). Cards ignore cover (U-11).
                    return u != null && u.Owner != p && zone.Contains(target) && visible.Contains(target);
                default:
                    return Traps.ActiveCount(state, p) < Catalog.MaxActiveTraps
                           && Traps.IsLegalCell(state, p, target, visible, zone);
            }
        }

        // C-15: an empty, non-Portal cell of the Control Zone (the zone as it is when the card is played).
        static bool IsTeleportDest(GameState state, Hex h, ISet<Hex> visible, ISet<Hex> zone) =>
            zone.Contains(h) && visible.Contains(h) && h != Board.Portal && Movement.IsEmpty(state, h);

        /// <summary>Hand order; units by id (Teleport: then destinations in Board.Cells order; Push: then directions);
        /// trap cells in Board.Cells order.</summary>
        internal static void AddLegal(GameState state, PlayerId p, ISet<Hex> visible, ISet<Hex> zone, List<ICommand> list)
        {
            foreach (var card in state.GetHand(p))
            {
                if (card.IsCharacter || state.GetMana(p) < card.Cost) continue;
                var d = card.Support;
                var candidates = new List<PlayCardCommand>();
                if (KindOf(d) == TargetKind.TrapCell)
                {
                    foreach (var h in Board.Cells) candidates.Add(new PlayCardCommand(p, card.Id, h));
                }
                else
                {
                    foreach (var u in state.Units)
                    {
                        if (d.Effect == EffectKind.Teleport)
                            foreach (var h in Board.Cells) candidates.Add(new PlayCardCommand(p, card.Id, u.Pos, h));
                        else if (d.Effect == EffectKind.Push)
                            for (int dir = 0; dir < Hex.Directions.Count; dir++) candidates.Add(new PlayCardCommand(p, card.Id, u.Pos, dir));
                        else
                            candidates.Add(new PlayCardCommand(p, card.Id, u.Pos));
                    }
                }
                foreach (var c in candidates)
                    if (IsLegal(state, p, card, c.Target, c.Dest, c.Direction, visible, zone)) list.Add(c);
            }
        }

        internal static void Apply(GameState state, PlayCardCommand cmd, EventLog events)
        {
            var p = cmd.Player;
            var card = state.FindInHand(p, cmd.CardId);
            if (!IsLegal(state, p, card, cmd.Target, cmd.Dest, cmd.Direction,
                    Visibility.VisibleCells(state, p), ControlZone.Cells(state, p)))
                throw new IllegalCommandException("Illegal card play: " + cmd);

            Mana.Spend(state, p, card.Cost); // C-01: Mana only; no Energy, no action
            state.HandList(p).Remove(card);
            var d = card.Support;
            events.Add(new CardPlayed(p, card.Id, d.Id, cmd.Target));
            var u = state.UnitAt(cmd.Target);
            switch (d.Effect)
            {
                case EffectKind.AttackBonus:     // C-10, C-11
                case EffectKind.BlockNextDamage: // C-12
                case EffectKind.AttackPenalty:   // C-16
                case EffectKind.Poison:          // C-17
                case EffectKind.Root:            // C-19
                    SetSlot(u, d, events);
                    break;
                case EffectKind.Heal: // C-13, U-24. A full-Health target is legal (C-01) and gains nothing.
                    int gained = Math.Min(u.Def.Health, u.Health + d.Amount) - u.Health;
                    if (gained > 0)
                    {
                        u.Health += gained;
                        events.Add(new UnitHealed(u.Id, gained));
                    }
                    break;
                case EffectKind.MoveBonus: // C-14: this turn only. Interim: a second Wind Step does not stack (C-06).
                    u.MoveBonus = d.Amount;
                    break;
                case EffectKind.Teleport: // C-15: no Energy, no action; rooted units too (C-19)
                    Traps.Teleport(state, u, cmd.Dest.Value, events);
                    break;
                case EffectKind.Push:
                    Push(state, u, cmd.Direction, d.Amount, events);
                    break;
                default: // C-20, C-21
                    Traps.Place(state, p, card, cmd.Target, events);
                    break;
            }
        }

        // C-05, C-06: one buff slot and one debuff slot; a new card replaces the old one (same card: timer restarts).
        static void SetSlot(Unit u, SupportCardDef d, EventLog events)
        {
            bool buff = d.Pool == SupportPool.Buff;
            var old = buff ? u.Buff : u.Debuff;
            if (old != null) events.Add(new EffectExpired(u.Id, old.Def.Id));
            var effect = new ActiveEffect(d);
            if (buff) u.Buff = effect;
            else u.Debuff = effect;
            events.Add(new EffectApplied(u.Id, d.Id));
        }

        /// <summary>C-18: up to <paramref name="cells"/> steps in one straight direction, stopping before a unit (even one
        /// the pusher cannot see: V-11 physical collision), a tower, rock or the board edge. The Portal is fine.
        /// No damage. A push that moves the unit breaks its overwatch (U-28) and fires a trap on the stop cell (C-32);
        /// it never triggers tower or overwatch shots (U-27). A 0-cell push is still a played card.</summary>
        static void Push(GameState state, Unit u, int dir, int cells, EventLog events)
        {
            var from = u.Pos;
            var pos = from;
            for (int k = 0; k < cells; k++)
            {
                var next = pos.Neighbor(dir);
                if (!Movement.IsEmpty(state, next)) break;
                pos = next;
            }
            u.Pos = pos; // before the event: the filter expects the unit on its stop cell (From == To for 0 cells)
            events.Add(new UnitPushed(u.Id, from, pos));
            if (pos == from) return; // interim: a unit that did not move was not pushed (no overwatch break, no trap)
            Defense.BreakOverwatch(state, u, events);
            Traps.ResolveArrival(state, u, events);
        }

        /// <summary>T-04 step 2, C-17: each own poisoned unit loses Amount Health (a Shield blocks it). The poisoner is the
        /// opponent (debuffs only target enemies), so a poison kill counts for them; U-23 draw for the owner.</summary>
        internal static void PoisonTicks(GameState state, PlayerId p, EventLog events)
        {
            var poisoned = new List<Unit>();
            foreach (var u in state.Units)
                if (u.Owner == p && u.Debuff != null && u.Debuff.Kind == EffectKind.Poison) poisoned.Add(u);
            foreach (var u in poisoned)
                Combat.DealDamage(state, p.Opponent(), DamageDealt.NoUnit, u.Pos, u.Debuff.Def.Amount, DamageKind.Poison, events);
        }

        /// <summary>T-08, C-04: at the end of the owner's turn, Wind Step ends and Timed effects count down.</summary>
        internal static void EndOfTurn(GameState state, PlayerId p, EventLog events)
        {
            foreach (var u in state.Units)
            {
                if (u.Owner != p) continue;
                u.MoveBonus = 0;
                if (Tick(u.Buff, u, events)) u.Buff = null;
                if (Tick(u.Debuff, u, events)) u.Debuff = null;
            }
        }

        static bool Tick(ActiveEffect e, Unit u, EventLog events)
        {
            if (e == null || !e.IsTimed) return false;
            e.TurnsLeft--;
            if (e.TurnsLeft > 0) return false;
            events.Add(new EffectExpired(u.Id, e.Def.Id));
            return true;
        }
    }
}
