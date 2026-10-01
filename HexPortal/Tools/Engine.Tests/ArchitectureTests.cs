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
        /// list); (2) a GameState is created only in BeliefState.cs (`new GameState(`), and no static field holds one;
        /// (3) GameState appears in no public/internal/protected member signature except BeliefState.From(PlayerView).
        /// Private helpers may take the belief state (it is built from the view alone). The behavioural part is
        /// AiTests.AI01_ChoiceIsTheSameWhenHiddenDataIsScrambled.</summary>
        [Test]
        public void Arch_AiUsesOnlyThePlayerView()
        {
            var aiDir = Path.Combine(CoreDir(), "Ai");
            Assert.That(Directory.Exists(aiDir), "Core/Ai missing");
            var files = Directory.GetFiles(aiDir, "*.cs", SearchOption.AllDirectories);
            Assert.That(files, Is.Not.Empty);
            var hits = new System.Collections.Generic.List<string>();
            int fromSignatures = 0;
            foreach (var f in files)
            {
                bool belief = Path.GetFileName(f) == "BeliefState.cs";
                var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = Regex.Replace(lines[i], @"//.*$", ""); // code only, not comments
                    string where = Path.GetFileName(f) + ":" + (i + 1) + ": " + lines[i].Trim();
                    if (Regex.IsMatch(line, @"PlayerView\s*\.\s*For\s*\(|GetLegalCommands\s*\(|Match\s*\.\s*Create\s*\("))
                        hits.Add("calls the real-state API: " + where);
                    if (!belief && Regex.IsMatch(line, @"\bnew\s+GameState\s*\(")) hits.Add("creates a GameState: " + where);
                    if (Regex.IsMatch(line, @"\bstatic\b[^(=]*\bGameState\b\s+\w+\s*(=|;)")) hits.Add("static GameState field: " + where);
                    if (Regex.IsMatch(line, @"\b(public|internal|protected)\b[^=]*\bGameState\b"))
                    {
                        if (belief && Regex.IsMatch(line, @"\binternal\s+static\s+GameState\s+From\s*\(\s*PlayerView\s+\w+\s*\)")) fromSignatures++;
                        else hits.Add("GameState in a non-private signature: " + where);
                    }
                }
            }
            Assert.That(hits, Is.Empty);
            Assert.That(fromSignatures, Is.EqualTo(1), "BeliefState.From(PlayerView) is the one entry point");
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
