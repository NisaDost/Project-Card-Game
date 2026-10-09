using System.Collections;
using System.IO;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>On demand: holds v4.0 phase-2 states for an external capture_game_view (no eval). Each stage writes
    /// "v40p2stageN.ready" into Application.temporaryCachePath and holds. Only legal commands via MatchController.
    /// 1: TwoCorridors map (B-15 rivers). 2: RockyRing map (B-16 lava) with an own torch (V-12) and the Market refresh button.</summary>
    public class V40P2CaptureStaging
    {
        const float HoldSeconds = 25f;

        [UnityTest, Explicit("Holds UI states for manual captures; run on demand")]
        public IEnumerator V40P2_Stages()
        {
            if (GameApp.Instance == null) GameApp.Create();
            yield return null;
            var m = GameApp.Instance.Match;
            string dir = Application.temporaryCachePath;
            foreach (var f in Directory.GetFiles(dir, "v40p2stage*.ready")) File.Delete(f);

            bool river = false;
            foreach (ulong seed in new ulong[] { 3, 7, 11, 15, 19, 23, 27, 31 }) // seed % 4 == 3: rivers
                if (UiInputSuiteTests.FastForward(seed, v => Sees(v, Marker.River))) { river = true; break; }
            Assert.That(river, Is.True, "a visible river");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Hold(dir, 1);

            bool ok = false;
            foreach (ulong seed in new ulong[] { 1, 5, 9, 13, 17, 21, 25, 29 }) // seed % 4 == 1: lava
                if (UiInputSuiteTests.FastForward(seed, v => Sees(v, Marker.Lava) && m.Legal.OfType<PlaceTorchCommand>().Any())) { ok = true; break; }
            Assert.That(ok, Is.True, "a torch is legal on a lava map");
            m.Apply(m.Legal.OfType<PlaceTorchCommand>().First());
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Hold(dir, 2);
            m.Close();
        }

        static bool Sees(PlayerView v, Marker m) => v.Cells.Any(c => c.Visibility == CellVisibility.Visible && c.Tile.Marker == m);

        static IEnumerator Hold(string dir, int stage)
        {
            yield return null;
            File.WriteAllText(Path.Combine(dir, "v40p2stage" + stage + ".ready"), stage.ToString());
            yield return new WaitForSecondsRealtime(HoldSeconds);
        }
    }
}
