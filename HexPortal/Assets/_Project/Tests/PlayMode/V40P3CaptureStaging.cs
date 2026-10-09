using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>On demand: holds v4.0 phase-3 states for an external capture_game_view (no eval). Each stage writes
    /// "v40p3stageN.ready" into Application.temporaryCachePath and holds. Only legal commands via MatchController.
    /// 1: as many of the four §3.4 classes on the board as the sampled seeds give. 2: a TwoCorridors map with rivers (B-15).</summary>
    public class V40P3CaptureStaging
    {
        const float HoldSeconds = 25f;
        static readonly UnitClass[] New = { UnitClass.Gatherer, UnitClass.Besieger, UnitClass.Assassin, UnitClass.Flyer };

        [UnityTest, Explicit("Holds UI states for manual captures; run on demand")]
        public IEnumerator V40P3_Stages()
        {
            if (GameApp.Instance == null) GameApp.Create();
            yield return null;
            var m = GameApp.Instance.Match;
            string dir = Application.temporaryCachePath;
            foreach (var f in Directory.GetFiles(dir, "v40p3stage*.ready")) File.Delete(f);

            ulong best = 0; int bestCount = -1, bestStep = 0;
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                int step = Run(m, seed, int.MaxValue, out int count);
                if (count > bestCount) { best = seed; bestCount = count; bestStep = step; }
                if (count == New.Length) break;
            }
            Run(m, best, bestStep, out _);
            Debug.Log("V40P3 stage 1: seed " + best + ", new classes on board " + bestCount);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Hold(dir, 1);

            foreach (ulong seed in new ulong[] { 3, 7, 11, 15, 19, 23 })
                if (UiInputSuiteTests.FastForward(seed, v => v.Cells.Count(c => c.Visibility == CellVisibility.Visible && c.Tile.Marker == Marker.River) >= 2)) break;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Hold(dir, 2);
            m.Close();
        }

        static int Distinct(PlayerView v) => v.OwnUnits.Select(u => u.Class).Where(c => New.Contains(c)).Distinct().Count();

        /// <summary>Plays A toward the new classes (draws and deploys them first, GreedyAi otherwise). Returns the step with the
        /// most distinct new classes on A's turn (or stops at <paramref name="stopAt"/>).</summary>
        static int Run(MatchController m, ulong seed, int stopAt, out int best)
        {
            m.NewAiMatch(seed, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new Rng(seed + 11);
            best = -1; int bestStep = 0;
            for (int i = 0; i < 2500 && !m.IsOver; i++)
            {
                var v = m.View;
                if (v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && !v.DrawPending)
                {
                    if (i >= stopAt) return i;
                    int n = Distinct(v);
                    if (n > best) { best = n; bestStep = i; }
                    if (n == New.Length && stopAt == int.MaxValue) return i;
                }
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                if (legal.Count == 0) break;
                ICommand pick = null;
                bool Want(int cardId) => v.Hand.Any(h => h.Id == cardId && h.IsCharacter && New.Contains(h.Character.Class)
                                                         && !v.OwnUnits.Any(u => u.Class == h.Character.Class));
                if (v.Phase != GamePhase.Playing)
                    pick = legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is PlaceTowerCommand)
                        ?? legal.OfType<PlaceUnitCommand>().FirstOrDefault(c => !Want(c.CardId));
                else if (v.DrawPending)
                {
                    var slot = v.Market.Count > 0 ? v.Market[0] : null;
                    if (slot != null && slot.IsCharacter && New.Contains(slot.Character.Class) && !v.OwnUnits.Any(u => u.Class == slot.Character.Class))
                        pick = legal.OfType<DrawCommand>().FirstOrDefault(d => d.Slot == 0);
                    pick = pick ?? legal.OfType<RefreshMarketCommand>().FirstOrDefault() ?? (ICommand)legal.OfType<DrawCommand>().FirstOrDefault();
                }
                else pick = legal.OfType<DeployCommand>().FirstOrDefault(d => Want(d.CardId));
                m.Apply(pick ?? GreedyAi.Choose(v, legal, AiLevel.Normal, rng));
            }
            return bestStep;
        }

        static IEnumerator Hold(string dir, int stage)
        {
            yield return null;
            File.WriteAllText(Path.Combine(dir, "v40p3stage" + stage + ".ready"), stage.ToString());
            yield return new WaitForSecondsRealtime(HoldSeconds);
        }
    }
}
