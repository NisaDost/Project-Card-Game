using System.Collections;
using System.IO;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>On demand: holds three v3.4 UI states for an external capture_game_view (no eval). Each stage writes
    /// "stageN.ready" into HEXPORTAL_STAGE_DIR (or temporaryCachePath) and holds for a while.</summary>
    public class V34CaptureStaging
    {
        const float HoldSeconds = 25f;

        [UnityTest, Explicit("Holds UI states for manual captures; run on demand")]
        public IEnumerator V34_Stages()
        {
            if (GameApp.Instance == null) GameApp.Create();
            yield return null;
            var app = GameApp.Instance;
            var m = app.Match;
            string dir = System.Environment.GetEnvironmentVariable("HEXPORTAL_STAGE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
            Directory.CreateDirectory(dir);

            // 1: panels / top bar: a turn in play with the quests panel open.
            Assert.That(UiInputSuiteTests.FastForward(12, v => v.Round >= 3 && v.OwnUnits.Count > 0), Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            app.Ui.OpenQuests();
            yield return Hold(dir, 1);
            app.Ui.CloseQuests();

            // 2: market area with a pending draw.
            Assert.That(UiInputSuiteTests.FastForward(7, v => v.Round >= 2 && v.Hand.Count < Core.Data.Catalog.HandLimit, true, 4000, true), Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Hold(dir, 2);

            // 3: an own unit on a rune stone (or wellspring), zoomed onto it.
            System.Func<PlayerView, UnitView> onSpot = v => v.OwnUnits.FirstOrDefault(u =>
                v.Cells[Board.IndexOf(u.Pos)].Tile.Marker == Marker.RuneStone || v.Cells[Board.IndexOf(u.Pos)].Tile.Marker == Marker.Wellspring);
            bool found = false;
            foreach (ulong s in new ulong[] { 11, 12, 13, 14, 15 })
                if (UiInputSuiteTests.FastForward(s, v => onSpot(v) != null && v.OwnUnits.Any(u => u.Class == Core.Data.UnitClass.Guardian && u.Pos == onSpot(v).Pos))
                    || UiInputSuiteTests.FastForward(s, v => onSpot(v) != null)) { found = true; break; }
            Assert.That(found, Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            var unit = onSpot(m.View);
            var world = HexLayout.ToWorld(unit.Pos, BoardView.TileHeight);
            CameraRig.Instance.ZoomBy(2.2f, Camera.main.WorldToScreenPoint(world));
            yield return Hold(dir, 3);
            m.Close();
        }

        static IEnumerator Hold(string dir, int stage)
        {
            yield return null;
            File.WriteAllText(Path.Combine(dir, "stage" + stage + ".ready"), stage.ToString());
            yield return new WaitForSecondsRealtime(HoldSeconds);
        }
    }
}
