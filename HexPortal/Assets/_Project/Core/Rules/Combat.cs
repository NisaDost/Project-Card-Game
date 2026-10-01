using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§3.3 combat: ranges (U-03, U-25), cover (U-11), damage (U-20, U-02), splash (U-04), death (U-22), towers as targets (U-26).</summary>
    public static class Combat
    {
        /// <summary>U-03/U-25: Archer on one of the 6 straight lines at k = Min..Max, nothing blocks.
        /// Other classes: plain distance Min..Max.</summary>
        public static bool InRange(UnitDef def, Hex from, Hex to)
        {
            if (def.Range != RangeKind.StraightLine)
            {
                int d = Hex.Distance(from, to);
                return d >= def.MinRange && d <= def.MaxRange;
            }
            foreach (var dir in Hex.Directions)
            {
                var c = from;
                for (int k = 1; k <= def.MaxRange; k++)
                {
                    c += dir;
                    if (!Board.IsOnBoard(c)) break; // a line never re-enters the board
                    if (k >= def.MinRange && c == to) return true;
                }
            }
            return false;
        }

        /// <summary>True if h holds an enemy unit or the enemy tower of <paramref name="attacker"/>.</summary>
        public static bool HoldsEnemy(GameState state, PlayerId attacker, Hex h)
        {
            var u = state.UnitAt(h);
            if (u != null) return u.Owner != attacker;
            var t = state.TowerAt(h);
            return t != null && t.Owner != attacker;
        }

        /// <summary>U-11: a non-Guardian unit or a tower adjacent to a Guardian of its own side cannot be attacked.
        /// V-11: only Guardians in the attacking side's Visible set count.</summary>
        public static bool IsCovered(GameState state, Hex target, ISet<Hex> visible)
        {
            PlayerId side;
            var u = state.UnitAt(target);
            if (u != null)
            {
                if (u.Class == UnitClass.Guardian) return false; // Guardians never receive cover
                side = u.Owner;
            }
            else
            {
                var t = state.TowerAt(target);
                if (t == null) return false;
                side = t.Owner;
            }
            foreach (var g in state.Units)
                if (g.Owner == side && g.Class == UnitClass.Guardian && Hex.Distance(g.Pos, target) == 1
                    && visible.Contains(g.Pos)) return true;
            return false;
        }

        /// <summary>The one target check shared by attacks, overwatch shots and tower shots:
        /// an enemy is there, it is Visible to the attacking player (V-07), and it is not covered by a Guardian
        /// that player can see (U-11, V-11).</summary>
        internal static bool IsValidTarget(GameState state, PlayerId attacker, Hex target, ISet<Hex> visible) =>
            HoldsEnemy(state, attacker, target) && visible.Contains(target) && !IsCovered(state, target, visible);

        /// <summary>Cells the unit may attack now, in Board.Cells order.</summary>
        public static List<Hex> LegalTargets(GameState state, Unit unit) =>
            LegalTargets(state, unit, Visibility.VisibleCells(state, unit.Owner));

        internal static List<Hex> LegalTargets(GameState state, Unit unit, ISet<Hex> visible)
        {
            var result = new List<Hex>();
            foreach (var c in Board.Cells)
                if (InRange(unit.Def, unit.Pos, c) && IsValidTarget(state, unit.Owner, c, visible)) result.Add(c);
            return result;
        }

        /// <summary>U-20: attack + bonuses − penalties, at least 0.</summary>
        public static int Damage(int attack, int bonuses, int penalties) => Math.Max(0, attack + bonuses - penalties);

        /// <summary>U-20 biome bonus (attacker on a tile of its own biome), U-02 charge (not on overwatch shots),
        /// the buff (C-10, C-11) and the debuff (C-16).</summary>
        public static int AttackDamage(GameState state, Unit attacker, bool overwatchShot)
        {
            int bonus = 0, penalty = 0;
            if (state.Map.Get(attacker.Pos).Biome == attacker.Biome) bonus += Catalog.BiomeAttackBonus;
            if (!overwatchShot && attacker.Class == UnitClass.Rider && attacker.MovedLastOwnTurn)
                bonus += Catalog.RiderChargeBonus;
            if (attacker.Buff != null && attacker.Buff.Kind == EffectKind.AttackBonus) bonus += attacker.Buff.Def.Amount;
            if (attacker.Debuff != null && attacker.Debuff.Kind == EffectKind.AttackPenalty) penalty += attacker.Debuff.Def.Amount;
            return Damage(attacker.Def.Attack, bonus, penalty);
        }

        /// <summary>A normal attack or an overwatch shot on a validated target, then the Mage splash (U-04).
        /// U-21: no counter-attack. V-08: the attacker is revealed to the opponent first; splash victims are not.</summary>
        internal static void ResolveAttack(GameState state, Unit attacker, Hex target, DamageKind kind, EventLog events)
        {
            Visibility.Reveal(state, attacker, events);
            int amount = AttackDamage(state, attacker, kind == DamageKind.Overwatch);
            DealDamage(state, attacker.Owner, attacker.Id, target, amount, kind, events);
            if (attacker.Class != UnitClass.Mage) return;
            // U-04: every enemy unit and the enemy tower next to the target cell. Ignores cover; not an attack.
            for (int d = 0; d < 6; d++)
            {
                if (state.IsOver) return;
                var n = target.Neighbor(d);
                if (Board.IsOnBoard(n) && HoldsEnemy(state, attacker.Owner, n))
                    DealDamage(state, attacker.Owner, attacker.Id, n, Catalog.MageSplashDamage, DamageKind.Splash, events);
            }
        }

        /// <summary>Damages the unit or tower on <paramref name="target"/> (every damage source, U-20 v2.6).
        /// C-12: a Shield takes the whole hit and ends (a 0 hit lowers no Health, so it is not damage and keeps the Shield).
        /// U-22: a unit at 0 is removed at once (with its effects); U-23: its owner draws. W-02: a tower at 0 ends the game.</summary>
        internal static void DealDamage(GameState state, PlayerId sourcePlayer, int sourceUnitId, Hex target, int amount,
            DamageKind kind, EventLog events)
        {
            var u = state.UnitAt(target);
            if (u != null)
            {
                if (amount > 0 && u.Buff != null && u.Buff.Kind == EffectKind.BlockNextDamage)
                {
                    u.Buff = null;
                    events.Add(new ShieldBlocked(u.Id, amount, kind));
                    return;
                }
                u.Health = Math.Max(0, u.Health - amount);
                events.Add(new DamageDealt(sourcePlayer, sourceUnitId, target, u.Id, amount, kind));
                if (u.Health == 0)
                {
                    events.Add(new UnitDied(u.Id, u.Owner, sourcePlayer)); // tagged while the unit is still on its cell
                    state.RemoveUnit(u);
                    Quests.OnUnitDied(state, u, sourcePlayer);         // Q-12, Q-16 (hidden until judged)
                    Passives.OnUnitDied(state, u);                     // P-01
                    Pools.DeathDraw(state, u.Owner, events);
                }
                return;
            }
            var t = state.TowerAt(target);
            if (t == null) throw new InvalidOperationException("Nothing to damage on " + target);
            if (amount > 0)
            {
                amount = Passives.ThickWall(state, t, amount, events); // P-04
                if (amount == 0) return;                               // all blocked: no damage (U-20)
            }
            if (sourcePlayer != t.Owner) state.GetProgress(sourcePlayer).TowerDamage += amount; // Q-13: blocked damage excluded
            t.Health = Math.Max(0, t.Health - amount);
            events.Add(new DamageDealt(sourcePlayer, sourceUnitId, target, DamageDealt.Tower, amount, kind));
            if (t.Health == 0) Turn.TowerDestroyed(state, t, events);
        }
    }
}
