using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>M7a low-poly look: captures on demand (board with fog, zoomed units, a fight). PNGs go to
    /// Application.temporaryCachePath/m7a.</summary>
    public class M7ArtCaptures
    {
        [UnityTest, Explicit("Writes screenshots; run on demand")]
        public IEnumerator M7a_Captures()
        {
            if (GameApp.Instance == null) GameApp.Create();
            var app = GameApp.Instance;
            yield return null;
            var m = app.Match;
            var dir = Path.Combine(Application.temporaryCachePath, "m7a");
            Directory.CreateDirectory(dir);
            bool hit = false;
            System.Action<IReadOnlyList<GameEvent>> onEvents = es => { if (es.Any(e => e is DamageDealt d && (d.Kind == DamageKind.Attack || d.Kind == DamageKind.TowerShot))) hit = true; };
            m.EventsApplied += onEvents;
            m.NewAiMatch(64, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random(64);
            int steps = 0, bestClasses = 0;
            bool board = false, fight = false;
            while (!m.IsOver && steps++ < 20000 && !(board && fight && bestClasses >= 5))
            {
                var v = m.View;
                bool humanTurn = v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && !v.DrawPending;
                if (humanTurn && !board && v.Round >= 4) { yield return Shot(dir, "01_board_fog.png"); board = true; }
                int classes = v.OwnUnits.Select(u => u.Class).Concat(v.EnemyUnits.Where(u => !u.IsGhost).Select(u => u.Class)).Distinct().Count();
                if (humanTurn && classes > bestClasses && v.OwnUnits.Count > 0)
                {
                    bestClasses = classes;
                    var c = Vector3.zero;
                    foreach (var u in v.OwnUnits) c += HexLayout.ToWorld(u.Pos, BoardView.TileHeight);
                    c /= v.OwnUnits.Count;
                    CameraRig.Instance.ZoomBy(2.0f, Camera.main.WorldToScreenPoint(c));
                    yield return Shot(dir, "02_units_zoomed.png");
                    CameraRig.Instance.ResetView();
                }
                if (m.AiToAct)
                {
                    hit = false;
                    m.StepAi();
                    if (hit && !fight && v.Round >= 3) { yield return new WaitForSeconds(0.12f); yield return Shot(dir, "03_fight.png"); fight = true; }
                    else yield return null;
                }
                else
                {
                    var legal = m.Legal;
                    var cmd = legal.FirstOrDefault(x => x is FinishSetupCommand) ?? legal.FirstOrDefault(x => x is DrawCommand)
                              ?? legal.FirstOrDefault(x => x is DeployCommand)
                              ?? legal.FirstOrDefault(x => x is AttackCommand || x is MoveAttackCommand)
                              ?? (rng.Next(3) == 0 ? legal.FirstOrDefault(x => x is EndTurnCommand) : null)
                              ?? legal[rng.Next(legal.Count)];
                    hit = false;
                    m.Apply(cmd);
                    if (hit && !fight) { yield return new WaitForSeconds(0.12f); yield return Shot(dir, "03_fight.png"); fight = true; }
                }
            }
            m.EventsApplied -= onEvents;
            Debug.Log("[M7a] captures in " + dir + " board=" + board + " fight=" + fight + " classes=" + bestClasses);
        }

        static IEnumerator Shot(string dir, string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
