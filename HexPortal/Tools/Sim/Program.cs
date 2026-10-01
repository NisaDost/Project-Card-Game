using System;
using System.Diagnostics;
using System.IO;
using HexPortal.Core;

namespace HexPortal.Sim
{
    /// <summary>AI-vs-AI batch simulator (M5b).
    /// Usage: Sim [--games N] [--seed S] [--a normal|easy] [--b normal|easy] [--first a|b] [--swap] [--parallel] [--json path]
    /// "a" and "b" are two AI configurations; --first chooses which one sits on seat A (A moves first, S-01); --swap plays
    /// every seed twice with the configurations on both seats (each keeps its own Rng).</summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var o = new SimOptions();
            string json = null;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(args[i] + " needs a value");
                    switch (args[i])
                    {
                        case "--games": o.Games = int.Parse(Next()); break;
                        case "--seed": o.Seed = ulong.Parse(Next()); break;
                        case "--a": o.LevelA = Level(Next()); break;
                        case "--b": o.LevelB = Level(Next()); break;
                        case "--first": o.FirstIsA = First(Next()); break;
                        case "--swap": o.Swap = true; break;
                        case "--parallel": o.Parallel = true; break;
                        case "--json": json = Next(); break;
                        default: throw new ArgumentException("Unknown argument " + args[i]);
                    }
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine("Usage: Sim [--games N] [--seed S] [--a normal|easy] [--b normal|easy] [--first a|b] [--swap] [--parallel] [--json path]");
                return 1;
            }

            Console.WriteLine("HexPortal Sim: " + o);
            var wall = Stopwatch.StartNew();
            var games = SimReport.Run(o);
            wall.Stop();
            var report = SimReport.Build(o, games, true, wall.Elapsed.TotalSeconds);
            Console.Write(SimReport.Text(report));
            if (json != null)
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(json));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(json, SimReport.Json(report));
                Console.WriteLine("JSON: " + Path.GetFullPath(json));
            }
            return 0;
        }

        static AiLevel Level(string s) =>
            s == "normal" ? AiLevel.Normal : s == "easy" ? AiLevel.Easy : throw new ArgumentException("level must be normal or easy: " + s);

        static bool First(string s) => s == "a" ? true : s == "b" ? false : throw new ArgumentException("--first must be a or b: " + s);
    }
}
