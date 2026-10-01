using System.Collections.Generic;
using System.Diagnostics;
using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Sim
{
    /// <summary>What one simulated game is: the match seed, which AI configuration sits on seat A ("a" or "b"; the
    /// other sits on B; A always moves first, S-01), and both configurations' levels.</summary>
    public sealed class GameSpec
    {
        public int Index;
        public ulong Seed;
        public bool ConfigAOnSeatA = true;
        public AiLevel LevelA = AiLevel.Normal; // configuration "a"
        public AiLevel LevelB = AiLevel.Normal; // configuration "b"
    }

    /// <summary>Per seat statistics of one game.</summary>
    public sealed class SeatStats
    {
        public string Config;                 // "a" or "b"
        public SortedSet<string> Classes = new SortedSet<string>(); // classes that were on the board
        public SortedDictionary<string, int> CardsPlayed = new SortedDictionary<string, int>(); // support cards (incl. setup traps)
        public SortedDictionary<string, int> Deployed = new SortedDictionary<string, int>();    // character classes deployed in play
        public SortedDictionary<string, int> Draws = new SortedDictionary<string, int>();       // Market slot / Blind (turn-start draws)
        public List<string> Quests = new List<string>();
        public List<string> QuestsCompleted = new List<string>();
        public List<string> QuestsFailed = new List<string>();
        public string Passive;
        public bool PassiveRevealed;
        public int Turns, UnusedMana, UnusedEnergy;
        public int PortalWaits, PortalWins, PortalKilled, PortalPushed, PortalVacated, PortalCut;
    }

    public sealed class GameRecord
    {
        public int Index;
        public ulong Seed;
        public SeatStats[] Seats = new SeatStats[2];
        public string Winner;          // "A", "B" or null (draw)
        public string WinnerConfig;    // "a", "b" or null
        public string ResultKey;       // W-01, W-02, W-03 c1..c4, W-04
        public int Rounds, Commands, PortalWinRound;
        public int TowerShots, OverwatchShots;
        public SortedDictionary<string, int> Events = new SortedDictionary<string, int>();
        public List<double> DecisionMs = new List<double>();
        public List<double> ActionDecisionMs = new List<double>();
    }

    /// <summary>Plays AI-vs-AI games through Core only: Match.Create, PlayerView.For + Engine.GetLegalCommands for each
    /// decision (AI-01), GreedyAi.Choose, Engine.Apply. The waiting AI pre-picks once per opponent turn (AI-06). No clock
    /// commands. Each configuration has its own Rng per game, so swapping seats keeps an AI with its Rng.</summary>
    public static class SimRunner
    {
        public const int MaxCommands = 6000;

        public static ulong AiSeed(ulong gameSeed, int config) => gameSeed * 0x9E3779B97F4A7C15UL + (ulong)config + 1;

        public static string ResultKey(GameResult r) =>
            r.Reason == WinReason.Portal ? "W-01" : r.Reason == WinReason.Tower ? "W-02"
            : r.Reason == WinReason.Timeout ? "W-04" : "W-03 c" + r.Criterion;

        public static GameRecord Play(GameSpec spec, bool timing)
        {
            var s = Match.Create(spec.Seed);
            var rec = new GameRecord { Index = spec.Index, Seed = spec.Seed };
            // Seat → configuration (0 = "a", 1 = "b").
            int[] config = spec.ConfigAOnSeatA ? new[] { 0, 1 } : new[] { 1, 0 };
            var levels = new[] { spec.LevelA, spec.LevelB };
            var rngs = new[] { new Rng(AiSeed(spec.Seed, 0)), new Rng(AiSeed(spec.Seed, 1)) };
            for (int seat = 0; seat < 2; seat++) rec.Seats[seat] = new SeatStats { Config = config[seat] == 0 ? "a" : "b" };

            var pending = new int[2];       // W-01: the unit recorded on the open Portal at the seat's last turn end
            var killed = new bool[2];
            var pushed = new bool[2];
            var sw = new Stopwatch();
            int prePickedTurn = -1;

            void Decide(PlayerId p)
            {
                int seat = (int)p;
                var st = rec.Seats[seat];
                var legal = Engine.GetLegalCommands(s, p);
                var view = PlayerView.For(s, p);
                if (timing) sw.Restart();
                var c = GreedyAi.Choose(view, legal, levels[config[seat]], rngs[config[seat]]);
                if (timing)
                {
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds;
                    rec.DecisionMs.Add(ms);
                    if (s.Phase == GamePhase.Playing && !(c is DrawCommand) && !(c is PrePickCommand)) rec.ActionDecisionMs.Add(ms);
                }
                if (!legal.Contains(c)) throw new System.InvalidOperationException("AI chose an illegal command: " + c);

                // Before applying: what the command is.
                switch (c)
                {
                    case PlayCardCommand pc: Add(st.CardsPlayed, s.FindInHand(p, pc.CardId).DefId); break;
                    case PlaceTrapCommand pt: Add(st.CardsPlayed, s.FindInHand(p, pt.CardId).DefId); break;
                    case DeployCommand d: Add(st.Deployed, s.FindInHand(p, d.CardId).Character.Class.ToString()); break;
                    case DrawCommand d: Add(st.Draws, d.Slot == DrawCommand.Blind ? "Blind" : ((CardPool)d.Slot).ToString()); break;
                    case EndTurnCommand _:
                        st.Turns++;
                        st.UnusedMana += s.GetMana(p);
                        st.UnusedEnergy += s.GetEnergy(p);
                        break;
                }

                var events = Engine.Apply(s, c);
                rec.Commands++;
                foreach (var e in events)
                {
                    Add(rec.Events, e.GetType().Name);
                    switch (e)
                    {
                        case TowerShot _: rec.TowerShots++; break;
                        case OverwatchFired _: rec.OverwatchShots++; break;
                        case QuestCompleted q: rec.Seats[(int)q.Player].QuestsCompleted.Add(q.QuestId); break;
                        case QuestFailed q: rec.Seats[(int)q.Player].QuestsFailed.Add(q.QuestId); break;
                        case PassiveRevealed x: rec.Seats[(int)x.Player].PassiveRevealed = true; Add(rec.Events, "Passive " + x.PassiveId); break;
                        case MapEventResolved m: Add(rec.Events, "MapEvent " + m.DefId); break;
                        case TrapTriggered t: Add(rec.Events, "Trap " + t.DefId); break;
                        case UnitDied d:
                            for (int w = 0; w < 2; w++) if (pending[w] == d.UnitId) killed[w] = true;
                            break;
                        case UnitPushed u:
                            for (int w = 0; w < 2; w++) if (pending[w] == u.UnitId && u.To != Board.Portal) pushed[w] = true;
                            break;
                    }
                }
                foreach (var u in s.Units) rec.Seats[(int)u.Owner].Classes.Add(u.Class.ToString());

                // W-01 waits: resolved at the waiter's next turn start (inside the opponent's EndTurn) or at the game end.
                int other = 1 - seat;
                if (pending[other] != 0 && (c is EndTurnCommand || s.IsOver))
                {
                    var o = rec.Seats[other];
                    if (s.IsOver && s.Result.Reason == WinReason.Portal && s.Result.Winner == (PlayerId)other) o.PortalWins++;
                    else if (killed[other]) o.PortalKilled++;
                    else if (pushed[other]) o.PortalPushed++;
                    else if (s.IsOver) o.PortalCut++;
                    else o.PortalVacated++;
                    pending[other] = 0;
                }
                if (c is EndTurnCommand)
                {
                    int id = s.GetProgress(p).PortalUnitId;
                    if (id != 0)
                    {
                        st.PortalWaits++;
                        if (s.IsOver) st.PortalCut++; // W-03 ended the game at this round end
                        else
                        {
                            pending[seat] = id;
                            killed[seat] = pushed[seat] = false;
                        }
                    }
                }
            }

            while (s.Phase == GamePhase.Setup && rec.Commands < MaxCommands)
                foreach (var p in new[] { PlayerId.A, PlayerId.B })
                    if (s.Phase == GamePhase.Setup && !s.IsSetupFinished(p)) Decide(p);
            while (!s.IsOver && rec.Commands < MaxCommands)
            {
                if (prePickedTurn != s.TurnIndex)
                {
                    prePickedTurn = s.TurnIndex;
                    var waiting = s.ActivePlayer.Opponent();
                    if (Engine.GetLegalCommands(s, waiting).Count > 0) Decide(waiting);
                }
                Decide(s.ActivePlayer);
            }
            if (!s.IsOver) throw new System.InvalidOperationException("Game " + spec.Seed + " did not end in " + MaxCommands + " commands");

            for (int seat = 0; seat < 2; seat++)
            {
                var p = (PlayerId)seat;
                foreach (var q in s.GetQuestChoices(p)) rec.Seats[seat].Quests.Add(q.Id);
                rec.Seats[seat].Passive = s.GetPassiveChoice(p) == null ? null : s.GetPassiveChoice(p).Id;
            }
            rec.Rounds = s.Round;
            rec.ResultKey = ResultKey(s.Result);
            rec.Winner = s.Result.Winner.HasValue ? s.Result.Winner.Value.ToString() : null;
            rec.WinnerConfig = s.Result.Winner.HasValue ? rec.Seats[(int)s.Result.Winner.Value].Config : null;
            if (s.Result.Reason == WinReason.Portal) rec.PortalWinRound = s.Round;
            return rec;
        }

        static void Add(SortedDictionary<string, int> d, string key) => d[key] = d.TryGetValue(key, out int n) ? n + 1 : 1;
    }
}
