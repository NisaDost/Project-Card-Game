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
            bool open = state.CompletedQuestCount(p) >= Catalog.PortalQuestsRequired;
            state.GetProgress(p).PortalUnitId = open && u != null && u.Owner == p ? u.Id : 0;
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
