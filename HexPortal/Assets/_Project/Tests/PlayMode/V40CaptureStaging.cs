using System.Collections;
using System.IO;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>On demand: holds the v4.0 compact HUD in setup and in play for an external capture_game_view (no eval).
    /// Each stage writes "v40stageN.ready" into Application.temporaryCachePath and holds for a while.</summary>
    public class V40CaptureStaging
    {
        const float HoldSeconds = 20f;

        [UnityTest, Explicit("Holds UI states for manual captures; run on demand")]
        public IEnumerator V40_Stages()
        {
            if (GameApp.Instance == null) GameApp.Create();
            yield return null;
            var m = GameApp.Instance.Match;
            string dir = Application.temporaryCachePath;

            // 1: setup, unit step (S-05): tower and one character placed with legal commands, setup Mana partly spent.
            m.NewAiMatch(102, PlayerId.A, AiLevel.Normal, timer: false);
            for (int i = 0; i < 20 && m.View.Phase == GamePhase.Setup; i++)
            {
                var legal = m.Legal;
                if (m.View.OwnUnits.Count >= 1 && m.View.OwnTowerPos.HasValue) break;
                var pick = legal.FirstOrDefault(c => c is ChooseQuestsCommand) ?? legal.FirstOrDefault(c => c is ChoosePassiveCommand)
                    ?? legal.FirstOrDefault(c => c is PlaceTowerCommand) ?? legal.FirstOrDefault(c => c is PlaceUnitCommand);
                if (pick == null) break;
                m.Apply(pick);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Hold(dir, 1);

            // 2: play, own turn with the mandatory draw pending (draw call + Market glow).
            Assert.That(UiInputSuiteTests.FastForward(7, v => v.Round >= 2 && v.Hand.Count < Core.Data.Catalog.HandLimit, true, 4000, true), Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Hold(dir, 2);
            m.Close();
        }

        static IEnumerator Hold(string dir, int stage)
        {
            yield return null;
            File.WriteAllText(Path.Combine(dir, "v40stage" + stage + ".ready"), stage.ToString());
            yield return new WaitForSecondsRealtime(HoldSeconds);
        }
    }
}
