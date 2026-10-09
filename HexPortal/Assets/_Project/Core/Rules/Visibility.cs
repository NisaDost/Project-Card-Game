using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§11 fog of war: the Visible set (V-02, V-06, V-08), reveals (V-08) and each player's exploration
    /// memory with last-seen snapshots (V-01, V-03, V-04, V-10). PlayerView reads the memory.</summary>
    public static class Visibility
    {
        /// <summary>V-02, B-25: playable (never Void) cells within Sight of any own unit or the own (placed) tower (nothing blocks sight),
        /// plus the Portal (V-06), plus the cells of enemy units and the enemy tower revealed by an attack (V-08).</summary>
        public static HashSet<Hex> VisibleCells(GameState state, PlayerId player)
        {
            var visible = new HashSet<Hex> { Board.Portal };
            var tower = state.GetTower(player);
            if (tower.IsPlaced) AddWithin(visible, tower.Pos, Catalog.Tower.Sight, state.Map);
            foreach (var u in state.Units)
            {
                if (u.Owner == player) AddWithin(visible, u.Pos, u.Def.Sight, state.Map);
                else if (IsRevealed(state, u)) visible.Add(u.Pos);
            }
            var enemyTower = state.GetTower(player.Opponent());
            if (enemyTower.IsPlaced && enemyTower.RevealedUntilTurn >= state.TurnIndex) visible.Add(enemyTower.Pos);
            return visible;
        }

        /// <summary>V-08: the unit attacked recently and is Visible to its opponent wherever it is.</summary>
        public static bool IsRevealed(GameState state, Unit unit) => unit.RevealedUntilTurn >= state.TurnIndex;

        static void AddWithin(HashSet<Hex> set, Hex center, int sight, GameMap map) => Board.AddWithin(set, center, sight, map);

        /// <summary>V-08 (v2.8 V1): an attacker becomes Visible to the opponent until the end of the opponent's next turn;
        /// an attack made during the opponent's turn (overwatch, tower shot) until the end of that turn. Call before the
        /// attack's events. Emits Revealed if the opponent could not see it yet. Mage splash victims are never revealed.</summary>
        internal static void Reveal(GameState state, Unit attacker, EventLog events)
        {
            bool seen = VisibleCells(state, attacker.Owner.Opponent()).Contains(attacker.Pos);
            int until = RevealEnd(state, attacker.Owner);
            if (until > attacker.RevealedUntilTurn) attacker.RevealedUntilTurn = until;
            if (!seen) events.Add(new Revealed(attacker.Owner, attacker.Id, attacker.Pos));
        }

        internal static void Reveal(GameState state, Tower tower, EventLog events)
        {
            bool seen = VisibleCells(state, tower.Owner.Opponent()).Contains(tower.Pos);
            int until = RevealEnd(state, tower.Owner);
            if (until > tower.RevealedUntilTurn) tower.RevealedUntilTurn = until;
            if (!seen) events.Add(new Revealed(tower.Owner, DamageDealt.Tower, tower.Pos));
        }

        static int RevealEnd(GameState state, PlayerId attacker) =>
            attacker == state.ActivePlayer ? state.TurnIndex + 1 : state.TurnIndex;

        /// <summary>V-04 (v4.0): each player starts with only the own home zone (B-03) Explored with its current terrain, the
        /// Portal Visible (V-06), everything else Hidden, incl. the rest of the own half and template rock and Void (the v3.3
        /// exception is removed). The current sight of the own units and tower is added by UpdateMemory when round 1 starts.
        /// Void is never Visible (V-02), so it stays Hidden for good; only PlayerView.PlayableCells (the public shape) shows it.</summary>
        internal static void InitMemory(GameState state)
        {
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                var mem = state.GetFog(p);
                foreach (var h in Board.Cells)
                {
                    var terrain = new LastSeen(state.Map.Get(h));
                    if (h == Board.Portal) mem.Set(h, CellVisibility.Visible, terrain);
                    else if (Board.IsHomeZone(h, p) && state.Map.IsPlayable(h)) mem.Set(h, CellVisibility.Explored, terrain);
                    else mem.Set(h, CellVisibility.Hidden, default(LastSeen));
                }
            }
        }

        /// <summary>InitMemory, then UpdateMemory: the memory of a scenario that starts as placed (TestBoard).</summary>
        internal static void ResetMemory(GameState state)
        {
            InitMemory(state);
            UpdateMemory(state);
        }

        internal static void UpdateMemory(GameState state)
        {
            if (state.InSetup || state.Light) return;
            UpdateMemory(state, VisibleCells(state, PlayerId.A), VisibleCells(state, PlayerId.B));
        }

        /// <summary>V-02, V-03: every Visible cell gets a fresh snapshot; a cell that left sight becomes Explored and
        /// keeps the snapshot of its last Visible moment. Interim (V-05): a ghost of a unit that is now seen live
        /// elsewhere is dropped, so one unit never shows twice. Not during setup (S-07: fog starts with round 1), not in
        /// light mode (AI simulation).</summary>
        internal static void UpdateMemory(GameState state, ISet<Hex> visibleA, ISet<Hex> visibleB)
        {
            if (state.InSetup || state.Light) return;
            // Hot path (runs after every event): index the pieces once instead of UnitAt/TowerAt per cell. Same pieces
            // as UnitAt/TowerAt: the first unit in list order and the first placed tower on a cell.
            int n = Board.Cells.Count;
            var unitAt = new Unit[n];
            foreach (var u in state.Units)
            {
                int i = Board.IndexOf(u.Pos);
                if (i >= 0 && unitAt[i] == null) unitAt[i] = u;
            }
            var towerAt = new Tower[n];
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                var t = state.GetTower(p);
                int i = t.IsPlaced ? Board.IndexOf(t.Pos) : -1;
                if (i >= 0 && towerAt[i] == null) towerAt[i] = t;
            }
            var isVisible = new bool[n];
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                var mem = state.GetFog(p);
                var visible = p == PlayerId.A ? visibleA : visibleB;
                System.Array.Clear(isVisible, 0, n);
                foreach (var h in visible)
                {
                    int i = Board.IndexOf(h);
                    if (i >= 0) isVisible[i] = true;
                }
                var seenUnits = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    if (!isVisible[i]) continue;
                    var snap = Capture(state.Map.Get(Board.Cells[i]), unitAt[i], towerAt[i], p);
                    if (snap.HasUnit) seenUnits.Add(snap.UnitId);
                    mem.SetAt(i, CellVisibility.Visible, snap);
                }
                for (int i = 0; i < n; i++)
                {
                    if (isVisible[i]) continue;
                    var v = mem.GetAt(i);
                    if (v == CellVisibility.Hidden) continue;
                    var snap = mem.GetLastSeenAt(i);
                    if (snap.HasUnit && seenUnits.Contains(snap.UnitId)) snap = snap.WithoutUnit();
                    mem.SetAt(i, CellVisibility.Explored, snap);
                }
            }
        }

        /// <summary>E-05 (v2.9, v2.10): after a map event, each player's Explored snapshot of a changed cell gets the new
        /// terrain. A ghost unit stays (no unit is revealed) unless <paramref name="dropGhosts"/> (E-10/E-12: the event proves
        /// the cell empty). Visible cells were refreshed by the event itself. A Hidden cell stays Hidden (never seen); the
        /// public event carries the change.</summary>
        internal static void TerrainChanged(GameState state, IEnumerable<Hex> cells, bool dropGhosts)
        {
            if (state.Light) return;
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                var mem = state.GetFog(p);
                foreach (var h in cells)
                {
                    if (mem.Get(h) != CellVisibility.Explored) continue;
                    var seen = mem.GetLastSeen(h).WithTile(state.Map.Get(h));
                    mem.Set(h, CellVisibility.Explored, dropGhosts ? seen.WithoutUnit() : seen);
                }
            }
        }

        // The cell as the viewer sees it now: terrain, and an enemy unit (alive) or the enemy tower. Own pieces are
        // never stored: the viewer always knows them.
        static LastSeen Capture(Tile tile, Unit u, Tower t, PlayerId viewer)
        {
            bool unit = u != null && u.Owner != viewer && u.Health > 0;
            bool tower = t != null && t.Owner != viewer;
            return new LastSeen(tile,
                unit ? u.Id : 0, unit ? u.Owner : PlayerId.A, unit ? u.Class : UnitClass.Guardian,
                unit ? u.Biome : Biome.None, unit ? u.Health : 0,
                tower, tower ? t.Owner : PlayerId.A, tower ? t.Health : 0);
        }
    }
}
