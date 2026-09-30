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

        [Test]
        public void Arch_CoreHasNoRecordOrInit()
        {
            // Unity (netstandard2.1) has no IsExternalInit, so these compile here but fail in Unity.
            AssertNoMatch(@"\brecord\s+(class\s+|struct\s+)?[A-Z]\w*", "No record types in Core.");
            AssertNoMatch(@"\binit\s*(;|=>|\{)", "No init accessors in Core.");
        }
    }
}
