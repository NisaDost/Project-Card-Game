using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§8 quests. Q-02: judged at the end of the owner's turn (T-08); quests with a Round (Q-14, Q-16) at the end of
    /// that round, which is the end of B's turn (v2.8). Q-05 "hold": the condition is true at QuestHoldTurns consecutive
    /// own turn ends on the same rune stone / wellspring; the unit on it may change (v2.8). Results are public (Q-03, Q-04).</summary>
    public static class Quests
    {
        /// <summary>T-08 for player p: update the hold streaks, then judge p's active quests. Quests without a Round can
        /// complete; Q-16 fails here if an own unit died (v2.10: announced at the next own turn end or the round end,
        /// whichever comes first).</summary>
        internal static void EndOfTurn(GameState state, PlayerId p, EventLog events)
        {
            var prog = state.GetProgress(p);
            foreach (var h in Board.Cells)
            {
                var own = state.UnitAt(h);
                bool held = IsHoldTarget(state.Map.Get(h).Marker) && own != null && own.Owner == p;
                prog.SetHoldStreak(h, held ? prog.GetHoldStreak(h) + 1 : 0);
            }
            var quests = state.GetQuestChoices(p);
            for (int i = 0; i < quests.Count; i++)
            {
                if (prog.GetQuestStatus(i) != QuestStatus.Active) continue;
                if (quests[i].Round == 0)
                {
                    if (IsMet(state, p, quests[i])) Complete(state, p, i, events);
                }
                else if (quests[i].Id == "Q-16" && prog.UnitsLost > 0) Fail(state, p, i, events);
            }
        }

        /// <summary>End of the round (end of B's turn): the Round quests of both players whose round it is.
        /// Q-14: tower Health ≥ Amount completes, else fails. Q-16: no own unit lost completes, else fails.</summary>
        internal static void EndOfRound(GameState state, EventLog events)
        {
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                var quests = state.GetQuestChoices(p);
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    if (q.Round != state.Round || state.GetProgress(p).GetQuestStatus(i) != QuestStatus.Active) continue;
                    bool ok;
                    switch (q.Id)
                    {
                        case "Q-14": ok = state.GetTower(p).Health >= q.Amount; break;
                        case "Q-16": ok = state.GetProgress(p).UnitsLost == 0; break;
                        default: continue;
                    }
                    if (ok) Complete(state, p, i, events);
                    else Fail(state, p, i, events);
                }
            }
        }

        /// <summary>U-22 hook: Q-12 kill credit (every source; never for the victim's own side) and the hidden Q-16 loss count
        /// (no event now, v2.10).</summary>
        internal static void OnUnitDied(GameState state, Unit unit, PlayerId killer, bool killerSaw)
        {
            if (killer != unit.Owner)
            {
                state.GetProgress(killer).Kills++;
                if (killerSaw) state.GetProgress(killer).SeenKills++; // M6 D2: display counter only
            }
            state.GetProgress(unit.Owner).UnitsLost++;
        }

        /// <summary>M6 D2: what the owner may see of an own quest's progress. Uses only owner-known data: own units, own tower,
        /// own hold streaks, own tower damage dealt (enemy tower Health is public), own traps sprung (public, C-33), own losses
        /// (own deaths are always seen) and SeenKills, never the real Kills. Returns (current, held cell or null).</summary>
        internal static int OwnerProgress(GameState state, PlayerId p, QuestDef q, out Hex? cell)
        {
            var prog = state.GetProgress(p);
            cell = null;
            switch (q.Id)
            {
                case "Q-10": return BestStreak(state, p, Marker.RuneStone, out cell);
                case "Q-18": return BestStreak(state, p, Marker.Wellspring, out cell);
                case "Q-11": return CountOn(state, p, Marker.RuneStone);
                case "Q-12": return prog.SeenKills;
                case "Q-13": return prog.TowerDamage;
                case "Q-14": return state.GetTower(p).Health;
                case "Q-15": return CountOnOwnBiome(state, p);
                case "Q-16": return prog.UnitsLost;
                case "Q-17": return prog.TrapsSprung;
                case "Q-19": return CountDeep(state, p);
                default: return 0;
            }
        }

        static int BestStreak(GameState state, PlayerId p, Marker marker, out Hex? cell)
        {
            cell = null;
            int best = 0;
            foreach (var h in Board.Cells)
            {
                int n = state.GetProgress(p).GetHoldStreak(h);
                if (state.Map.Get(h).Marker == marker && n > best) { best = n; cell = h; }
            }
            return best;
        }

        static int CountOnOwnBiome(GameState state, PlayerId p)
        {
            int n = 0;
            foreach (var u in state.Units)
                if (u.Owner == p && state.Map.Get(u.Pos).Biome == u.Biome) n++; // the Portal has no biome
            return n;
        }

        static int CountDeep(GameState state, PlayerId p)
        {
            int n = 0;
            foreach (var u in state.Units)
                if (u.Owner == p && Board.IsHomeZone(u.Pos, p.Opponent())) n++;
            return n;
        }

        static bool IsHoldTarget(Marker m) => m == Marker.RuneStone || m == Marker.Wellspring;

        static bool IsMet(GameState state, PlayerId p, QuestDef q)
        {
            var prog = state.GetProgress(p);
            switch (q.Id)
            {
                case "Q-10": return CountHeld(state, p, Marker.RuneStone) >= q.Amount;
                case "Q-11": return CountOn(state, p, Marker.RuneStone) >= q.Amount;
                case "Q-12": return prog.Kills >= q.Amount;
                case "Q-13": return prog.TowerDamage >= q.Amount;
                case "Q-15": return CountOnOwnBiome(state, p) >= q.Amount;
                case "Q-17": return prog.TrapsSprung >= q.Amount;
                case "Q-18": return CountHeld(state, p, Marker.Wellspring) >= q.Amount;
                case "Q-19": return CountDeep(state, p) >= q.Amount;
                default: return false;
            }
        }

        // Q-05: stones/wellsprings held at QuestHoldTurns consecutive own turn ends.
        static int CountHeld(GameState state, PlayerId p, Marker marker)
        {
            int n = 0;
            foreach (var h in Board.Cells)
                if (state.Map.Get(h).Marker == marker && state.GetProgress(p).GetHoldStreak(h) >= Catalog.QuestHoldTurns) n++;
            return n;
        }

        // Q-11: different stones with an own unit on them now.
        static int CountOn(GameState state, PlayerId p, Marker marker)
        {
            int n = 0;
            foreach (var u in state.Units)
                if (u.Owner == p && state.Map.Get(u.Pos).Marker == marker) n++;
            return n;
        }

        static void Complete(GameState state, PlayerId p, int index, EventLog events)
        {
            state.GetProgress(p).SetQuestStatus(index, QuestStatus.Completed);
            events.Add(new QuestCompleted(p, state.GetQuestChoices(p)[index].Id));
        }

        static void Fail(GameState state, PlayerId p, int index, EventLog events)
        {
            state.GetProgress(p).SetQuestStatus(index, QuestStatus.Failed);
            events.Add(new QuestFailed(p, state.GetQuestChoices(p)[index].Id));
        }
    }
}
