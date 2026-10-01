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
        /// (4) no field, property or local is declared with the GameState type (fields without an access modifier
        /// included; locals use `var`), and no generic type argument is GameState.
        /// Each file is checked as one line (comments removed, whitespace collapsed), so signatures split over several
        /// lines are caught. Private helpers may take the belief state as a parameter (it is built from the view alone).
        /// The behavioural part is AiTests.AI01_ChoiceIsTheSameWhenHiddenDataIsScrambled.</summary>
        [Test]
        public void Arch_AiUsesOnlyThePlayerView()
        {
            var aiDir = Path.Combine(CoreDir(), "Ai");
            Assert.That(Directory.Exists(aiDir), "Core/Ai missing");
            var files = Directory.GetFiles(aiDir, "*.cs", SearchOption.AllDirectories);
            Assert.That(files, Is.Not.Empty);
            var hits = new System.Collections.Generic.List<string>();
            int fromSignatures = 0;
            const string from = @"\binternal\s+static\s+GameState\s+From\s*\(\s*PlayerView\s+\w+\s*\)";
            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                bool belief = name == "BeliefState.cs";
                var code = string.Join(" ", File.ReadAllLines(f).Select(l => Regex.Replace(l, @"//.*$", "")));
                code = Regex.Replace(code, @"/\*.*?\*/", " ");
                code = Regex.Replace(code, @"\s+", " ");
                if (belief)
                {
                    fromSignatures += Regex.Matches(code, from).Count;
                    code = Regex.Replace(code, from, "/*From*/");
                }
                void Check(string pattern, string why)
                {
                    foreach (Match m in Regex.Matches(code, pattern)) hits.Add(name + ": " + why + ": " + m.Value.Trim());
                }
                Check(@"PlayerView\s*\.\s*For\s*\(|GetLegalCommands\s*\(|Match\s*\.\s*Create\s*\(", "calls the real-state API");
                if (!belief) Check(@"\bnew\s+GameState\s*\(", "creates a GameState");
                Check(NonPrivateSignature, "GameState in a non-private signature");
                Check(Declaration, "field, property or local of type GameState");
                Check(GenericArgument, "GameState as a generic argument");
            }
            Assert.That(hits, Is.Empty);
            Assert.That(fromSignatures, Is.EqualTo(1), "BeliefState.From(PlayerView) is the one entry point");
        }

        /// <summary>The scan above must catch what it claims to (checked on planted snippets, not on Core).</summary>
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
            };
            foreach (var b in bad)
            {
                var code = Regex.Replace(b, @"\s+", " ");
                bool caught = Regex.IsMatch(code, NonPrivateSignature) || Regex.IsMatch(code, Declaration)
                              || Regex.IsMatch(code, GenericArgument);
                Assert.That(caught, "not caught: " + b);
            }
            foreach (var p in new[] { NonPrivateSignature, Declaration, GenericArgument })
                Assert.That(Regex.IsMatch("static int Eval(GameState s, Context ctx) { var x = s.Clone(); }", p), Is.False,
                    "a private helper parameter is allowed");
        }

        // Applied to a whole file collapsed to one line (Arch_AiUsesOnlyThePlayerView).
        const string NonPrivateSignature = @"\b(public|internal|protected)\b[^;{}=]*\bGameState\b";
        const string Declaration = @"\bGameState\s+\w+\s*(=|;|\{)";
        const string GenericArgument = @"<[^<>;{}]*\bGameState\b";

        [Test]
        public void Arch_CoreHasNoRecordOrInit()
        {
            // Unity (netstandard2.1) has no IsExternalInit, so these compile here but fail in Unity.
            AssertNoMatch(@"\brecord\s+(class\s+|struct\s+)?[A-Z]\w*", "No record types in Core.");
            AssertNoMatch(@"\binit\s*(;|=>|\{)", "No init accessors in Core.");
        }
    }
}
