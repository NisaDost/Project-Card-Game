using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Sim
{
    /// <summary>Run settings, echoed in the report.</summary>
    public sealed class SimOptions
    {
        public int Games = 1000;
        public ulong Seed = 1;
        public AiLevel LevelA = AiLevel.Normal;
        public AiLevel LevelB = AiLevel.Normal;
        public bool FirstIsA = true; // configuration "a" on seat A
        public bool FirstSet;        // --first was given (not allowed with --swap)
        public bool Swap;            // every seed twice, configurations on both seats
        public bool Parallel;
        public bool Timing = true;

        /// <summary>Distinct runs: game i (0-based) of seed S uses match seed S × SeedStride + i + 1.</summary>
        public const ulong SeedStride = 1000000UL;
        public const int MaxGames = 1000000;
        public const ulong MaxSeed = (ulong.MaxValue - SeedStride) / SeedStride;

        /// <summary>Game i (0-based) of the run uses this match seed.</summary>
        public ulong SeedFor(int i) => Seed * SeedStride + (ulong)i + 1;

        /// <summary>Throws ArgumentException for settings that would overlap or overflow the seed range.</summary>
        public void Validate()
        {
            if (Games < 1 || Games > MaxGames) throw new ArgumentException("--games must be 1.." + MaxGames + " (seed stride), got " + Games);
            if (Seed > MaxSeed) throw new ArgumentException("--seed must be at most " + MaxSeed + " (seed × " + SeedStride + " + game must fit in 64 bits)");
            if (FirstSet && Swap) throw new ArgumentException("--first and --swap cannot be combined (--swap plays both seatings)");
        }

        public List<GameSpec> Specs()
        {
            Validate();
            var list = new List<GameSpec>();
            for (int i = 0; i < Games; i++)
            {
                if (Swap)
                {
                    list.Add(new GameSpec { Index = list.Count, Seed = SeedFor(i), ConfigAOnSeatA = true, LevelA = LevelA, LevelB = LevelB });
                    list.Add(new GameSpec { Index = list.Count, Seed = SeedFor(i), ConfigAOnSeatA = false, LevelA = LevelA, LevelB = LevelB });
                }
                else list.Add(new GameSpec { Index = list.Count, Seed = SeedFor(i), ConfigAOnSeatA = FirstIsA, LevelA = LevelA, LevelB = LevelB });
            }
            return list;
        }

        public override string ToString() =>
            "games=" + Games + (Swap ? " x2 (seat swap)" : "") + " seed=" + Seed + " a=" + LevelA + " b=" + LevelB
            + " first=" + (FirstIsA ? "a" : "b") + (Parallel ? " parallel" : "");
    }

    /// <summary>Aggregates game records (in game order, so the report is the same for the same arguments) into a text
    /// report and a JSON tree for the balance-analyst. Timing is reported separately (it is the only nondeterministic part).</summary>
    public static class SimReport
    {
        static readonly string[] Seats = { "A", "B" };

        /// <summary>Redacted views made only by EventFilter for a player; Engine.Apply never returns them, so the Sim
        /// (which counts the full event list) never sees them.</summary>
        static readonly string[] ViewOnlyEvents = { "UnitAppeared", "UnitVanished", "TowerHealthChanged" };

        public static List<GameRecord> Run(SimOptions o)
        {
            var specs = o.Specs();
            var records = new GameRecord[specs.Count];
            if (o.Parallel) System.Threading.Tasks.Parallel.For(0, specs.Count, i => records[i] = SimRunner.Play(specs[i], o.Timing));
            else for (int i = 0; i < specs.Count; i++) records[i] = SimRunner.Play(specs[i], o.Timing);
            return records.ToList();
        }

        /// <summary>The whole report as a tree of sorted dictionaries, lists and numbers.</summary>
        public static SortedDictionary<string, object> Build(SimOptions o, List<GameRecord> games, bool timing, double wallSeconds)
        {
            int n = games.Count;
            var root = new SortedDictionary<string, object>();
            root["options"] = o.ToString();
            root["games"] = n;

            // ---- Results ----
            int winsA = games.Count(g => g.Winner == "A"), winsB = games.Count(g => g.Winner == "B"), draws = games.Count(g => g.Winner == null);
            root["results"] = new SortedDictionary<string, object>
            {
                ["A wins"] = winsA, ["B wins"] = winsB, ["draws"] = draws,
                ["A win rate"] = Rate(winsA, n), ["B win rate"] = Rate(winsB, n), ["draw rate"] = Rate(draws, n),
                ["config a wins"] = games.Count(g => g.WinnerConfig == "a"), ["config b wins"] = games.Count(g => g.WinnerConfig == "b"),
                ["reasons"] = Count(games.Select(g => g.ResultKey)),
                ["W-03 quests (criterion 2) share"] = Rate(games.Count(g => g.ResultKey == "W-03 c2"), n),
                ["reached round 15 share"] = Rate(games.Count(g => g.Rounds >= Catalog.RoundLimit), n),
            };

            // ---- Length ----
            root["length"] = new SortedDictionary<string, object>
            {
                ["rounds"] = Stats(games.Select(g => (double)g.Rounds)),
                ["rounds histogram"] = Histogram(games.Select(g => g.Rounds)),
                ["commands"] = Stats(games.Select(g => (double)g.Commands)),
                ["portal win round histogram"] = Histogram(games.Where(g => g.PortalWinRound > 0).Select(g => g.PortalWinRound)),
            };

            // ---- Portal waits (W-01) by seat ----
            var waits = new SortedDictionary<string, object>();
            for (int seat = 0; seat < 2; seat++)
            {
                var ss = games.Select(g => g.Seats[seat]).ToList();
                waits[Seats[seat]] = new SortedDictionary<string, object>
                {
                    ["recorded"] = ss.Sum(x => x.PortalWaits), ["won"] = ss.Sum(x => x.PortalWins),
                    ["stopped: killed"] = ss.Sum(x => x.PortalKilled), ["stopped: pushed"] = ss.Sum(x => x.PortalPushed),
                    ["stopped: vacated otherwise"] = ss.Sum(x => x.PortalVacated), ["cut by another game end"] = ss.Sum(x => x.PortalCut),
                };
            }
            root["portal waits"] = waits;

            // ---- W-01: the round each player's Portal first opened (its 2nd completed quest) ----
            var opened = new SortedDictionary<string, object>();
            for (int seat = 0; seat < 2; seat++)
            {
                var rounds = games.Select(g => g.Seats[seat].PortalOpenRound).ToList();
                opened[Seats[seat]] = new SortedDictionary<string, object>
                {
                    ["opened"] = Stats(rounds.Where(r => r > 0).Select(r => (double)r)),
                    ["histogram"] = Histogram(rounds.Where(r => r > 0)),
                    ["never opened"] = rounds.Count(r => r == 0),
                };
            }
            root["portal open round"] = opened;

            // ---- Per class, card, quest, passive (player-games; win rate when present) ----
            var pg = new List<(SeatStats s, bool won, int seat)>();
            foreach (var g in games)
                for (int seat = 0; seat < 2; seat++) pg.Add((g.Seats[seat], g.Winner == Seats[seat], seat));
            int players = pg.Count;

            var classes = new SortedDictionary<string, object>();
            foreach (var c in Enum.GetNames(typeof(UnitClass)))
            {
                var has = pg.Where(x => x.s.Classes.Contains(c)).ToList();
                classes[c] = Presence(has.Count, players, has.Count(x => x.won), pg.Sum(x => x.s.Deployed.TryGetValue(c, out int k) ? k : 0), "deployed in play");
            }
            root["classes"] = classes;

            var cards = new SortedDictionary<string, object>();
            foreach (var d in Catalog.SupportCards)
            {
                var has = pg.Where(x => x.s.CardsPlayed.ContainsKey(d.Id)).ToList();
                cards[d.Id + " " + d.Name] = Presence(has.Count, players, has.Count(x => x.won), pg.Sum(x => x.s.CardsPlayed.TryGetValue(d.Id, out int k) ? k : 0), "plays");
            }
            root["support cards (played; setup traps included)"] = cards;

            var quests = new SortedDictionary<string, object>();
            foreach (var q in Catalog.Quests)
            {
                var has = pg.Where(x => x.s.Quests.Contains(q.Id)).ToList();
                var e = Presence(has.Count, players, has.Count(x => x.won), has.Count(x => x.s.QuestsCompleted.Contains(q.Id)), "completed");
                e["failed"] = has.Count(x => x.s.QuestsFailed.Contains(q.Id));
                e["completion rate"] = Rate(has.Count(x => x.s.QuestsCompleted.Contains(q.Id)), has.Count);
                quests[q.Id + " " + q.Name] = e;
            }
            root["quests (chosen)"] = quests;
            var completion = new SortedDictionary<string, object>(); // top level for the balance-analyst (completed / chosen)
            foreach (var q in Catalog.Quests)
            {
                int chosen = pg.Count(x => x.s.Quests.Contains(q.Id));
                completion[q.Id] = Rate(pg.Count(x => x.s.QuestsCompleted.Contains(q.Id)), chosen);
            }
            root["quest completion rates"] = completion;

            var passives = new SortedDictionary<string, object>();
            foreach (var d in Catalog.Passives)
            {
                var has = pg.Where(x => x.s.Passive == d.Id).ToList();
                passives[d.Id + " " + d.Name] = Presence(has.Count, players, has.Count(x => x.won), has.Count(x => x.s.PassiveRevealed), "revealed");
            }
            root["passives (chosen)"] = passives;

            // ---- Resources, draws, shots ----
            var res = new SortedDictionary<string, object>();
            for (int seat = 0; seat < 2; seat++)
            {
                var ss = games.Select(g => g.Seats[seat]).ToList();
                int turns = ss.Sum(x => x.Turns);
                res[Seats[seat]] = new SortedDictionary<string, object>
                {
                    ["turns ended"] = turns,
                    ["unused Mana per turn"] = Ratio(ss.Sum(x => x.UnusedMana), turns),
                    ["unused Energy per turn"] = Ratio(ss.Sum(x => x.UnusedEnergy), turns),
                    ["draw choices"] = Merge(ss.Select(x => x.Draws)),
                };
            }
            root["resources"] = res;
            root["shots per game"] = new SortedDictionary<string, object>
            {
                ["tower shots"] = Ratio(games.Sum(g => g.TowerShots), n),
                ["overwatch shots"] = Ratio(games.Sum(g => g.OverwatchShots), n),
            };

            // ---- Never triggered ----
            var events = Merge(games.Select(g => g.Events));
            var never = new List<string>();
            foreach (var t in typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract).OrderBy(t => t.Name))
                if (!ViewOnlyEvents.Contains(t.Name) && !events.ContainsKey(t.Name)) never.Add("event " + t.Name);
            foreach (var d in Catalog.Passives) if (!events.ContainsKey("Passive " + d.Id)) never.Add("passive revealed " + d.Id);
            foreach (var d in Catalog.MapEvents) if (!events.ContainsKey("MapEvent " + d.Id)) never.Add("map event " + d.Id);
            foreach (var d in Catalog.SupportCards.Where(c => c.Category == CardCategory.Trap))
                if (!events.ContainsKey("Trap " + d.Id)) never.Add("trap triggered " + d.Id);
            foreach (var q in Catalog.Quests) if (!pg.Any(x => x.s.QuestsCompleted.Contains(q.Id))) never.Add("quest completed " + q.Id);
            foreach (var d in Catalog.SupportCards) if (!pg.Any(x => x.s.CardsPlayed.ContainsKey(d.Id))) never.Add("card played " + d.Id);
            root["event counts"] = events;
            root["never triggered"] = never;

            // ---- Seat swap pairs ----
            if (o.Swap)
            {
                int bothA = 0, bothB = 0, bothConfigA = 0, bothConfigB = 0, withDraw = 0;
                for (int i = 0; i + 1 < n; i += 2)
                {
                    var g1 = games[i];
                    var g2 = games[i + 1];
                    if (g1.Winner == null || g2.Winner == null) withDraw++;
                    else if (g1.Winner == "A" && g2.Winner == "A") bothA++;
                    else if (g1.Winner == "B" && g2.Winner == "B") bothB++;
                    else if (g1.WinnerConfig == "a") bothConfigA++;
                    else bothConfigB++;
                }
                int pairs = n / 2;
                root["seat swap"] = new SortedDictionary<string, object>
                {
                    ["pairs"] = pairs,
                    ["seat A won both"] = bothA, ["seat B won both"] = bothB,
                    ["config a won both"] = bothConfigA, ["config b won both"] = bothConfigB, ["pairs with a draw"] = withDraw,
                    ["seat A win rate"] = Rate(winsA, n),
                    ["config a win rate on seat A"] = Rate(games.Count(g => g.Seats[0].Config == "a" && g.Winner == "A"), games.Count(g => g.Seats[0].Config == "a")),
                    ["config a win rate on seat B"] = Rate(games.Count(g => g.Seats[1].Config == "a" && g.Winner == "B"), games.Count(g => g.Seats[1].Config == "a")),
                };
            }

            if (timing)
                root["timing"] = new SortedDictionary<string, object>
                {
                    ["all decisions ms"] = Stats(games.SelectMany(g => g.DecisionMs)),
                    ["action decisions ms"] = Stats(games.SelectMany(g => g.ActionDecisionMs)),
                    ["wall seconds"] = Math.Round(wallSeconds, 1),
                };
            return root;
        }

        public static string Json(SortedDictionary<string, object> report) =>
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>Plain text: one "section: key = value" line per leaf, in key order.</summary>
        public static string Text(SortedDictionary<string, object> report)
        {
            var sb = new StringBuilder();
            void Walk(string prefix, object v)
            {
                switch (v)
                {
                    case SortedDictionary<string, object> d:
                        if (d.Values.All(x => !(x is SortedDictionary<string, object>) && !(x is SortedDictionary<string, int>) && !(x is List<string>)))
                        {
                            sb.Append(prefix).Append(": ").AppendLine(string.Join(", ", d.Select(kv => kv.Key + " = " + Format(kv.Value))));
                            return;
                        }
                        foreach (var kv in d) Walk(prefix.Length == 0 ? kv.Key : prefix + " / " + kv.Key, kv.Value);
                        break;
                    case SortedDictionary<string, int> c:
                        sb.Append(prefix).Append(": ").AppendLine(string.Join(", ", c.Select(kv => kv.Key + " = " + kv.Value)));
                        break;
                    case List<string> l:
                        sb.Append(prefix).Append(": ").AppendLine(l.Count == 0 ? "(none)" : string.Join(", ", l));
                        break;
                    default:
                        sb.Append(prefix).Append(" = ").AppendLine(Format(v));
                        break;
                }
            }
            foreach (var kv in report)
                if (kv.Key != "event counts") Walk(kv.Key, kv.Value);
            return sb.ToString();
        }

        static string Format(object v) => v is double d ? d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            : v is SortedDictionary<string, int> c ? "{" + string.Join(", ", c.Select(kv => kv.Key + ": " + kv.Value)) + "}"
            : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);

        static SortedDictionary<string, object> Presence(int count, int players, int wins, int extra, string extraName) =>
            new SortedDictionary<string, object>
            {
                ["player-games"] = count, ["rate"] = Rate(count, players), ["wins"] = wins, ["win rate"] = Rate(wins, count), [extraName] = extra,
            };

        static double Rate(int k, int n) => n == 0 ? 0 : Math.Round((double)k / n, 4);
        static double Ratio(int k, int n) => n == 0 ? 0 : Math.Round((double)k / n, 3);

        static SortedDictionary<string, int> Count(IEnumerable<string> keys)
        {
            var d = new SortedDictionary<string, int>();
            foreach (var k in keys) d[k] = d.TryGetValue(k, out int n) ? n + 1 : 1;
            return d;
        }

        static SortedDictionary<string, int> Merge(IEnumerable<SortedDictionary<string, int>> all)
        {
            var d = new SortedDictionary<string, int>();
            foreach (var x in all)
                foreach (var kv in x) d[kv.Key] = d.TryGetValue(kv.Key, out int n) ? n + kv.Value : kv.Value;
            return d;
        }

        static SortedDictionary<string, int> Histogram(IEnumerable<int> values)
        {
            var d = new SortedDictionary<string, int>();
            foreach (var v in values)
            {
                var k = v.ToString("00");
                d[k] = d.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            return d;
        }

        static SortedDictionary<string, object> Stats(IEnumerable<double> values)
        {
            var v = values.OrderBy(x => x).ToList();
            if (v.Count == 0) return new SortedDictionary<string, object> { ["count"] = 0 };
            return new SortedDictionary<string, object>
            {
                ["count"] = v.Count, ["mean"] = Math.Round(v.Average(), 3), ["median"] = Math.Round(v[v.Count / 2], 3),
                ["p90"] = Math.Round(v[(int)(v.Count * 0.9)], 3), ["p99"] = Math.Round(v[Math.Min(v.Count - 1, (int)(v.Count * 0.99))], 3),
                ["min"] = Math.Round(v[0], 3), ["max"] = Math.Round(v[v.Count - 1], 3),
            };
        }
    }
}
