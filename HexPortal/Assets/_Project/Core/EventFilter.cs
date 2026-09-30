using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>V-10: which events a player may see. Tag runs once per event, at emission (EventLog.Add), with the
    /// players' Visible sets at that moment; For then only reads the stored views.</summary>
    public static class EventFilter
    {
        /// <summary>The events <paramref name="player"/> may see, in order, with hidden parts redacted.</summary>
        public static List<GameEvent> For(IReadOnlyList<GameEvent> events, PlayerId player)
        {
            var result = new List<GameEvent>();
            foreach (var e in events)
            {
                var v = e.ViewFor(player);
                if (v != null) result.Add(v);
            }
            return result;
        }

        internal static void Tag(GameState s, GameEvent e, ISet<Hex> visibleA, ISet<Hex> visibleB)
        {
            e.SetView(PlayerId.A, ViewOf(s, e, PlayerId.A, visibleA));
            e.SetView(PlayerId.B, ViewOf(s, e, PlayerId.B, visibleB));
        }

        static GameEvent ViewOf(GameState s, GameEvent e, PlayerId p, ISet<Hex> vis)
        {
            if (s.InSetup) return SetupView(s, e, p); // S-08
            switch (e)
            {
                case UnitMoved m: return MoveView(s, e, m.UnitId, m.From, m.To, p, vis);
                case UnitPushed m: return MoveView(s, e, m.UnitId, m.From, m.To, p, vis);
                case UnitTeleported m: return MoveView(s, e, m.UnitId, m.From, m.To, p, vis);
                case DamageDealt d:
                    if (d.TargetUnitId != DamageDealt.Tower) return Sees(s, d.TargetUnitId, p, vis) ? e : null;
                    var t = s.TowerAt(d.Target);
                    return t.Owner == p || vis.Contains(d.Target) ? e : new TowerHealthChanged(t.Owner, t.Health); // V-09
                case UnitDied d: return Sees(s, d.UnitId, p, vis) ? e : null; // emitted before the unit is removed
                case ShieldBlocked x: return Sees(s, x.UnitId, p, vis) ? e : null;
                case UnitHealed x: return Sees(s, x.UnitId, p, vis) ? e : null;
                case EffectApplied x: return Sees(s, x.UnitId, p, vis) ? e : null;  // V-05: effects only on Visible cells
                case EffectExpired x: return Sees(s, x.UnitId, p, vis) ? e : null;
                case OverwatchSet x: return Sees(s, x.UnitId, p, vis) ? e : null;   // U-28
                case OverwatchEnded x: return Sees(s, x.UnitId, p, vis) ? e : null;
                case OverwatchFired x: return Sees(s, x.UnitId, p, vis) ? e : null; // the shooter is revealed first (V-08)
                case UnitDeployed x: return Sees(s, x.UnitId, p, vis) ? e : null;
                case TowerShot x: return x.Owner == p || vis.Contains(s.GetTower(x.Owner).Pos) ? e : null;
                case CardDrawn d: // V-09: hand counts are public; a Market card was public already
                    return d.Player == p || d.Source == DrawSource.Market ? e : new CardDrawn(d.Player, 0, d.Source);
                case CardPlayed c: // emitted before the effect: the target unit is still on Target
                    if (c.Player == p) return e;
                    if (IsTrapCard(c.DefId)) return null; // C-33
                    var target = s.UnitAt(c.Target);
                    return target != null && Sees(s, target.Id, p, vis) ? e : null;
                case TrapPlaced x: return x.Owner == p ? e : null;   // C-33
                case TrapRemoved x: return x.Owner == p ? e : null;  // C-35 (v2.7)
                case TrapTriggered x: // C-33: public; the victim only if the viewer can see it
                    return Sees(s, x.UnitId, p, vis) ? e : new TrapTriggered(x.Owner, x.CardId, x.DefId, x.Cell, 0);
                case Revealed x: return x.Owner == p ? null : e;
                case PrePickSet x: return x.Player == p ? e : null;  // T-11
                case QuestsChosen x: return x.Player == p ? e : null;
                case PassiveChosen x: return x.Player == p ? e : null;
                case TowerPlaced x: return x.Player == p ? e : null;
                case TurnStarted _:
                case MarketRefilled _:  // D-05, V-09: the Market is public
                case TowerDestroyed _:  // V-09: tower Health is public
                case GameOver _:
                case SetupFinished _:
                case TurnTimedOut _:
                    return e;
                default: // fail closed: a new event type must get an explicit visibility rule here
                    throw new System.InvalidOperationException("No visibility rule for event " + e.GetType().Name);
            }
        }

        // S-08: until both players finished, a player sees only their own setup and the public SetupFinished.
        static GameEvent SetupView(GameState s, GameEvent e, PlayerId p)
        {
            switch (e)
            {
                case SetupFinished _: return e;
                case MarketRefilled _: return e;
                case QuestsChosen x: return x.Player == p ? e : null;
                case PassiveChosen x: return x.Player == p ? e : null;
                case TowerPlaced x: return x.Player == p ? e : null;
                case TrapPlaced x: return x.Owner == p ? e : null;
                case UnitDeployed x: return s.GetUnit(x.UnitId).Owner == p ? e : null;
                default: return null;
            }
        }

        /// <summary>The viewer owns the unit, or its cell is Visible to the viewer (sight, Portal, V-08 reveal).</summary>
        static bool Sees(GameState s, int unitId, PlayerId p, ISet<Hex> vis)
        {
            var u = s.GetUnit(unitId);
            return u != null && (u.Owner == p || vis.Contains(u.Pos));
        }

        // The unit already stands on `to`. A revealed unit is seen wherever it goes (v2.8 V1).
        static GameEvent MoveView(GameState s, GameEvent e, int unitId, Hex from, Hex to, PlayerId p, ISet<Hex> vis)
        {
            var u = s.GetUnit(unitId);
            if (u == null) return null;
            if (u.Owner == p || Visibility.IsRevealed(s, u)) return e;
            bool seeFrom = vis.Contains(from), seeTo = vis.Contains(to);
            if (seeFrom && seeTo) return e;
            if (seeTo) return new UnitAppeared(u.Id, u.Owner, u.Class, u.Biome, u.Health, to);
            if (seeFrom) return new UnitVanished(u.Id, from);
            return null;
        }

        static bool IsTrapCard(string defId)
        {
            foreach (var d in Catalog.SupportCards)
                if (d.Id == defId) return d.Category == CardCategory.Trap;
            return false;
        }
    }
}
