using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>AI-01: the AI's guess of the match, built from one PlayerView and nothing else, in light mode (no fog
    /// memory, no event tagging). Exact: the viewer's units, tower, hand, Mana, Energy, traps, quests, passive; enemy units
    /// on Visible cells; the enemy tower if seen (towers never move, so a ghost tower is exact; Health is public V-09).
    /// Assumed: ghost enemy units stand where they were last seen; hidden enemy units are absent; an unseen enemy tower
    /// is not on the board; enemy Riders have Charge ready (U-02); a revealed enemy P-04 is used up if the enemy tower is
    /// below full Health, else partly used (WallEstimate). Unknown → empty: the opponent's hand, traps, quests, Mana and Energy, the pools (only counts
    /// are public; draws are scored by expected value, never simulated), the pending map event.
    /// Hidden terrain is the mirror cell's terrain (B-04; every map change is symmetric, E-03).</summary>
    internal static class BeliefState
    {
        /// <summary>P-04 revealed, enemy tower still at full Health: assumed blocked so far (AI estimate, not a rule).</summary>
        const int WallEstimate = 1;

        internal static GameState From(PlayerView view)
        {
            var me = view.Viewer;
            var opp = me.Opponent();

            var map = new GameMap(0, 1);
            foreach (var c in view.Cells)
            {
                var tile = new Tile(Biome.None, Marker.None);
                if (c.TerrainKnown) tile = c.Tile;
                else
                {
                    var mirror = view.Cells[Board.IndexOf(c.Cell.Mirror())];
                    if (mirror.TerrainKnown) tile = mirror.Tile;
                }
                map.Set(c.Cell, tile);
            }

            Hex? enemyTower = view.EnemyTowerPos;
            if (!enemyTower.HasValue)
                foreach (var c in view.Cells)
                    if (c.HasTower && c.TowerOwner == opp) enemyTower = c.Cell;
            var ownTower = view.OwnTowerPos ?? default(Hex);
            var s = new GameState(map, me == PlayerId.A ? ownTower : enemyTower ?? default(Hex),
                me == PlayerId.B ? ownTower : enemyTower ?? default(Hex), 0UL);
            s.Light = true;
            foreach (var pool in new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }) s.PoolList(pool).Clear();

            s.Round = view.Round;
            s.ActivePlayer = view.ActivePlayer;
            s.SetMana(me, view.Mana);
            s.SetEnergy(me, view.Energy);
            s.SetMana(opp, 0);
            s.SetEnergy(opp, 0);
            s.SetDrawPending(me, view.DrawPending);
            s.SetTowerShotAvailable(me, view.OwnTowerShotAvailable);
            s.SetTowerShotAvailable(opp, view.EnemyTowerShotAvailable);

            var mine = s.GetTower(me);
            mine.IsPlaced = view.OwnTowerPos.HasValue;
            mine.Health = view.OwnTowerHealth;
            mine.RevealedUntilTurn = view.OwnTowerRevealedUntilTurn;
            var theirs = s.GetTower(opp);
            theirs.IsPlaced = enemyTower.HasValue;
            theirs.Health = view.EnemyTowerHealth;

            s.HandList(me).AddRange(view.Hand);
            for (int i = 0; i < view.Market.Count; i++) s.SetMarket((CardPool)i, view.Market[i]);
            foreach (var t in view.OwnTraps) s.TrapList.Add(new Trap(me, t.Pos, new CardInstance(t.CardId, null, Support(t.DefId))));

            var quests = new List<QuestDef>();
            foreach (var id in view.QuestChoices) quests.Add(Quest(id));
            s.SetQuestChoices(me, quests);
            for (int i = 0; i < view.QuestStatuses.Count; i++) s.GetProgress(me).SetQuestStatus(i, view.QuestStatuses[i]);
            if (view.PassiveChoice != null) s.SetPassiveChoice(me, Passive(view.PassiveChoice));
            s.GetProgress(me).PassiveRevealed = view.PassiveRevealed;
            if (view.OpponentPassive != null)
            {
                var def = Passive(view.OpponentPassive);
                s.SetPassiveChoice(opp, def);
                s.GetProgress(opp).PassiveRevealed = true;
                // P-04: each blocked amount is public as a TowerDamageBlocked event, but the PlayerView carries no running
                // total and the AI keeps no history. Towers are never healed (U-05), so a tower below full Health means the
                // wall is used up; otherwise it was revealed by a smaller hit: a middle estimate.
                if (def.Id == "P-04")
                    s.GetProgress(opp).WallBlocked = view.EnemyTowerHealth < Catalog.Tower.Health ? def.Amount : WallEstimate;
            }

            var added = new HashSet<int>();
            foreach (var u in view.OwnUnits) Put(s, u, added, me);
            foreach (var u in view.EnemyUnits) Put(s, u, added, me);
            foreach (var c in view.Cells)
                if (c.Unit != null && c.Unit.IsGhost) Put(s, c.Unit, added, me);

            // V-08: enemy units (and the tower) the viewer sees outside its own sight were revealed by an attack.
            var visible = Visibility.VisibleCells(s, me);
            foreach (var u in s.Units)
                if (u.Owner == opp && !visible.Contains(u.Pos) && view.Cells[Board.IndexOf(u.Pos)].Visibility == CellVisibility.Visible)
                    u.RevealedUntilTurn = s.TurnIndex;
            if (theirs.IsPlaced && !visible.Contains(theirs.Pos)
                && view.Cells[Board.IndexOf(theirs.Pos)].Visibility == CellVisibility.Visible)
                theirs.RevealedUntilTurn = s.TurnIndex;
            return s;
        }

        static void Put(GameState s, UnitView v, HashSet<int> added, PlayerId viewer)
        {
            if (!added.Add(v.Id)) return;
            var u = new Unit(v.Id, v.Owner, v.Class, v.Biome, v.Pos)
            {
                Health = v.Health, ActedThisTurn = v.ActedThisTurn, MovedThisTurn = v.MovedThisTurn,
                MovedLastOwnTurn = v.MovedLastOwnTurn, OnOverwatch = v.OnOverwatch, MoveBonus = v.MoveBonus,
                RevealedUntilTurn = v.RevealedUntilTurn,
            };
            if (v.BuffId != null) u.Buff = new ActiveEffect(Support(v.BuffId)) { TurnsLeft = v.BuffTurnsLeft };
            if (v.DebuffId != null) u.Debuff = new ActiveEffect(Support(v.DebuffId)) { TurnsLeft = v.DebuffTurnsLeft };
            // U-02: an enemy Rider's last move is hidden (V-05): assume Charge is ready (conservative risk).
            if (v.Owner != viewer && v.Class == UnitClass.Rider) u.MovedLastOwnTurn = true;
            s.PutUnit(u);
        }

        static SupportCardDef Support(string id)
        {
            foreach (var d in Catalog.SupportCards)
                if (d.Id == id) return d;
            throw new KeyNotFoundException(id);
        }

        static QuestDef Quest(string id)
        {
            foreach (var d in Catalog.Quests)
                if (d.Id == id) return d;
            throw new KeyNotFoundException(id);
        }

        static PassiveDef Passive(string id)
        {
            foreach (var d in Catalog.Passives)
                if (d.Id == id) return d;
            throw new KeyNotFoundException(id);
        }
    }
}
