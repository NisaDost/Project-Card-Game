using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // Static checks on Core sources. Complements the guard hook, which only sees edits made by Claude.
    public class ArchitectureTests
    {
        static string CoreDir()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                var core = Path.Combine(dir.FullName, "Assets", "_Project", "Core");
                if (Directory.Exists(core)) return core;
                dir = dir.Parent;
            }
            Assert.Fail("Assets/_Project/Core not found above the test directory.");
            return null;
        }

        static void AssertNoMatch(string pattern, string why)
        {
            var files = Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories);
            Assert.That(files, Is.Not.Empty);
            var hits = files
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(x => Regex.IsMatch(x.line, pattern))
                .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.line.Trim()}")
                .ToList();
            Assert.That(hits, Is.Empty, why);
        }

        [Test]
        public void Arch_CoreHasNoUnityOrNondeterministicApis()
        {
            AssertNoMatch(@"\bUnityEngine\b|\bUnityEditor\b", "Core must not reference Unity.");
            AssertNoMatch(@"System\.Random|\bnew\s+Random\s*\(|\bRandom\.Shared\b", "Use the deterministic Rng.");
            AssertNoMatch(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b|\bEnvironment\.TickCount(64)?\b|\bStopwatch\b",
                "No wall-clock time.");
            AssertNoMatch(@"\bGuid\.NewGuid\b", "Use sequential ids.");
        }

        /// <summary>AI-01 (static part): the AI decides from a PlayerView only. In Core/Ai:
        /// (1) no code calls PlayerView.For, Engine.GetLegalCommands or Match.Create (the caller passes the view and the legal
        /// list); (2) a GameState is created only in BeliefState.cs (`new GameState(`);
        /// (3) GameState appears in no public/internal/protected member signature except BeliefState.From(PlayerView);
        /// (4) GameState is never the type of a field, property, local or foreach variable (fields without an access
        /// modifier and `GameState a, b;` included; locals use `var`), never an array element type, generic argument or
        /// tuple element, and never a cast target.
        /// Each file goes through AiNormalize (comments removed, whitespace collapsed to one line, no space before '('), so
        /// signatures split over several lines are caught; AiViolations then applies the patterns. Private helpers may take
        /// the belief state as a parameter (it is built from the view alone); typed lambda parameters are not allowed.
        /// The behavioural part is AiTests.AI01_ChoiceIsTheSameWhenHiddenDataIsScrambled.</summary>
        [Test]
        public void Arch_AiUsesOnlyThePlayerView()
        {
            var aiDir = Path.Combine(CoreDir(), "Ai");
            Assert.That(Directory.Exists(aiDir), "Core/Ai missing");
            var files = Directory.GetFiles(aiDir, "*.cs", SearchOption.AllDirectories);
            Assert.That(files, Is.Not.Empty);
            var hits = new List<string>();
            int fromSignatures = 0;
            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                foreach (var v in AiViolations(AiNormalize(File.ReadAllText(f)), name == "BeliefState.cs", ref fromSignatures))
                    hits.Add(name + ": " + v);
            }
            Assert.That(hits, Is.Empty);
            Assert.That(fromSignatures, Is.EqualTo(1), "BeliefState.From(PlayerView) is the one entry point");
        }

        /// <summary>The scan above catches what it claims to: planted snippets go through the same AiNormalize and
        /// AiViolations as the Core files.</summary>
        [Test]
        public void Arch_AiScanCatchesPlantedViolations()
        {
            string[] bad =
            {
                "internal static int Eval(\n PlayerView v,\n GameState s)",
                "sealed class X { GameState cached; }",
                "static GameState last = null;",
                "GameState Current { get; set; }",
                "readonly List<GameState> history;",
                "GameState[] states;",
                "static int F(GameState [] all) { return 0; }",
                "GameState a, b;",
                "(GameState, int) pair;",
                "(int, GameState) pair;",
                "(int n, GameState s) named;",
                "var t = (GameState)obj;",
                "var t = (GameState) obj;",
                "foreach (GameState g in list) { }",
                "PlayerView.For(s, p);",
                "var l = Engine.GetLegalCommands (s, p);",
                "var x = new GameState(map, a, b);",
            };
            foreach (var b in bad)
            {
                int from = 0;
                Assert.That(AiViolations(AiNormalize(b), false, ref from), Is.Not.Empty, "not caught: " + b);
            }
            string[] good =
            {
                "static int Eval(GameState s, Context ctx) { var x = s.Clone(); return 0; }",
                "static void Put(GameState s, UnitView v, HashSet<int> added, PlayerId viewer) { }",
                "static long TieKey(ICommand c, GameState belief, PlayerId me) { int kind, primary = 0; return 0; }",
                "// GameState cached;\nstatic int F(GameState s) { return 0; }",
                "/* public GameState X;\n internal GameState Y; */ int y;",
                "internal static GameState From(PlayerView view) { var s = new GameState(map, a, b, 0UL); return s; }",
            };
            foreach (var g in good)
            {
                int from = 0;
                Assert.That(AiViolations(AiNormalize(g), true, ref from), Is.Empty, "false positive: " + g);
            }
        }

        /// <summary>Comments removed, the file collapsed to one line, no whitespace before '('.</summary>
        static string AiNormalize(string text)
        {
            var code = string.Join(" ", text.Split('\n').Select(l => Regex.Replace(l, @"//.*$", "")));
            code = Regex.Replace(code, @"/\*.*?\*/", " ");
            code = Regex.Replace(code, @"\s+", " ");
            return Regex.Replace(code, @"\s+\(", "(");
        }

        static List<string> AiViolations(string code, bool beliefFile, ref int fromSignatures)
        {
            const string from = @"\binternal\s+static\s+GameState\s+From\(\s*PlayerView\s+\w+\s*\)";
            if (beliefFile)
            {
                fromSignatures += Regex.Matches(code, from).Count;
                code = Regex.Replace(code, from, "/*From*/");
            }
            var hits = new List<string>();
            void Check(string pattern, string why)
            {
                foreach (Match m in Regex.Matches(code, pattern)) hits.Add(why + ": " + m.Value.Trim());
            }
            Check(@"PlayerView\s*\.\s*For\(|GetLegalCommands\(|Match\s*\.\s*Create\(", "calls the real-state API");
            if (!beliefFile) Check(@"\bnew\s+GameState\(", "creates a GameState");
            Check(@"\b(public|internal|protected)\b[^;{}=]*\bGameState\b", "GameState in a non-private signature");
            Check(@"\bGameState\s+\w+\s*(=|;|\{|,\s*\w+\s*(=|;|,))", "field, property or local of type GameState");
            Check(@"\bGameState\s+\w+\s+in\b", "foreach variable of type GameState");
            Check(@"\bGameState\s*\[", "GameState array");
            Check(@"<[^<>;{}]*\bGameState\b", "GameState as a generic argument");
            Check(@"(?<![\w>\]?])\(\s*GameState\b", "GameState cast or tuple element");          // not a call/declaration '('
            Check(@",\s*GameState\s*[,)]", "unnamed GameState tuple element");
            Check(@"\([^()]*\bGameState\b[^()]*\)\s*\w+\s*(=|;|\{)", "tuple-typed member with a GameState element");
            return hits;
        }

        [Test]
        public void Arch_CoreHasNoRecordOrInit()
        {
            // Unity (netstandard2.1) has no IsExternalInit, so these compile here but fail in Unity.
            AssertNoMatch(@"\brecord\s+(class\s+|struct\s+)?[A-Z]\w*", "No record types in Core.");
            AssertNoMatch(@"\binit\s*(;|=>|\{)", "No init accessors in Core.");
        }
    }
}
