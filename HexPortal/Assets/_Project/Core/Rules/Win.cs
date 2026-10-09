using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§12 W-01 (Portal) and W-03 (round limit). W-02 is in Turn.TowerDestroyed, W-04 in Turn.Timeout.</summary>
    public static class Win
    {
        /// <summary>W-01 (v2.10), end of p's turn right after p's own quests were checked: if the Portal is open for p
        /// (PortalQuestsRequired completed quests) and an own unit stands on it, remember that unit. Quests completed later
        /// (round-end quests at the end of B's turn) do not validate this wait.</summary>
        internal static void RecordPortal(GameState state, PlayerId p)
        {
            var u = state.UnitAt(Board.Portal);
            bool open = IsPortalOpen(state, p);
            // v4.0: only the Portal Bearer (U-44) can use the Portal; other units only block it.
            state.GetProgress(p).PortalUnitId = open && u != null && u.Owner == p && u.Class == UnitClass.PortalBearer ? u.Id : 0;
        }

        /// <summary>W-01: PortalQuestsRequired completed quests.</summary>
        public static bool IsPortalOpen(GameState state, PlayerId p) => state.CompletedQuestCount(p) >= Catalog.PortalQuestsRequired;

        /// <summary>W-01, U-44 (v4.0): after quests were checked (own turn end, round end): a player whose Portal just opened is
        /// granted the Bearer, which appears at once (SpawnBearer). Both players, A first.</summary>
        internal static void OnQuestsChecked(GameState state, EventLog events)
        {
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                var prog = state.GetProgress(p);
                if (state.IsOver || prog.BearerGranted || !IsPortalOpen(state, p)) continue;
                prog.BearerGranted = true;
                prog.BearerPending = true;
                SpawnBearer(state, p, events);
            }
        }

        /// <summary>U-44: a pending Bearer appears on the free cell next to the own tower (Movement.IsEmpty) that is closest to
        /// the Portal; ties: Board.Cells order as the owner sees the board (B-05, interim tie-break; the GDD gives none).
        /// No free cell: it waits for a later own turn start. After a death it returns only at a later own turn start (like
        /// P-01, v2.10). It cannot act in the turn it appears (U-44). The arrival resolves the cell (torch, trap, Lava, P-06)
        /// but never tower or overwatch shots (like P-01).</summary>
        internal static void SpawnBearer(GameState state, PlayerId p, EventLog events)
        {
            var prog = state.GetProgress(p);
            var tower = state.GetTower(p);
            if (state.IsOver || !prog.BearerPending || prog.BearerDeathTurn >= state.TurnIndex || !tower.IsPlaced) return;
            var cell = BearerCell(state, p);
            if (!cell.HasValue) return;
            prog.BearerPending = false;
            var u = state.AddUnit(p, UnitClass.PortalBearer, Biome.None, cell.Value);
            u.ActedThisTurn = true;
            prog.BearerId = u.Id;
            events.Add(new BearerSpawned(u.Id, p, u.Pos));
            Traps.ResolveArrival(state, u, events);
        }

        /// <summary>U-44 placement (see SpawnBearer), or null.</summary>
        public static Hex? BearerCell(GameState state, PlayerId p)
        {
            var tower = state.GetTower(p);
            Hex? best = null;
            int bestDist = int.MaxValue, bestIndex = int.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                var h = tower.Pos.Neighbor(d);
                if (!Movement.IsEmpty(state, h)) continue;
                int dist = Hex.Distance(h, Board.Portal);
                int index = Board.IndexOf(p == PlayerId.A ? h : h.Mirror());
                if (dist < bestDist || (dist == bestDist && index < bestIndex)) { best = h; bestDist = dist; bestIndex = index; }
            }
            return best;
        }

        /// <summary>U-44, U-22: the Bearer died; it returns at a later own turn start.</summary>
        internal static void OnBearerDied(GameState state, Unit u)
        {
            var prog = state.GetProgress(u.Owner);
            prog.BearerId = 0;
            prog.BearerPending = true;
            prog.BearerDeathTurn = state.TurnIndex;
        }

        /// <summary>W-01, T-04 step 3 (after heal, poison and P-01): the unit recorded at p's last turn end is still on the
        /// Portal.</summary>
        internal static void CheckPortal(GameState state, PlayerId p, EventLog events)
        {
            var prog = state.GetProgress(p);
            int id = prog.PortalUnitId;
            prog.PortalUnitId = 0;
            if (id == 0 || state.IsOver) return;
            var u = state.GetUnit(id);
            if (u == null || u.Pos != Board.Portal) return;
            End(state, new GameResult(p, WinReason.Portal), events);
        }

        /// <summary>W-03 at the end of round RoundLimit (end of B's turn, v2.8): tower Health, then completed quests, then the
        /// total Health of own units on the board; all equal is a draw (criterion 4).</summary>
        internal static void CheckRoundLimit(GameState state, EventLog events)
        {
            if (state.IsOver || state.Round < Catalog.RoundLimit) return;
            int[] a = Criteria(state, PlayerId.A), b = Criteria(state, PlayerId.B);
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                {
                    End(state, new GameResult(a[i] > b[i] ? PlayerId.A : PlayerId.B, WinReason.RoundLimit, i + 1), events);
                    return;
                }
            End(state, new GameResult(null, WinReason.RoundLimit, a.Length + 1), events);
        }

        static int[] Criteria(GameState state, PlayerId p)
        {
            int health = 0;
            foreach (var u in state.Units)
                if (u.Owner == p) health += u.Health;
            return new[] { state.GetTower(p).Health, state.CompletedQuestCount(p), health };
        }

        static void End(GameState state, GameResult result, EventLog events)
        {
            state.Result = result;
            events.Add(new GameOver(result));
        }
    }
}
