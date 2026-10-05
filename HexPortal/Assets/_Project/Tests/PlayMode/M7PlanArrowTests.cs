using System.Collections;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>M7e: UX-10 / T-12 plan arrows are client-only, deletable and cleared at the player's own turn start.</summary>
    public class M7PlanArrowTests
    {
        [UnityTest]
        public IEnumerator UX10_PlanArrowsDrawDeleteAndClearWithoutCommands()
        {
            if (GameApp.Instance == null) GameApp.Create();
            var app = GameApp.Instance;
            yield return null;
            var m = app.Match;
            var ui = app.Ui;
            m.NewAiMatch(41, PlayerId.A, AiLevel.Normal, timer: false);
            int steps = 0;
            // Human: finish setup quickly, then end turns until it is the AI's turn with own units on the board.
            while (!(m.AiToAct && m.View.Phase == GamePhase.Playing && m.View.OwnUnits.Count > 0) && steps++ < 5000)
            {
                Assert.That(m.IsOver, Is.False);
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                var cmd = legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is DrawCommand)
                          ?? legal.FirstOrDefault(c => c is EndTurnCommand)
                          ?? (m.View.SetupStep == SetupStep.Placement ? legal.FirstOrDefault(c => c is PlaceUnitCommand) : null) ?? legal[0];
                Assert.That(m.Apply(cmd), Is.True);
            }
            Assert.That(m.AiToAct, Is.True);
            yield return null;

            int commands = 0;
            System.Action changed = () => commands++;
            m.Changed += changed;
            var own = m.View.OwnUnits[0].Pos;
            var to = m.View.Cells.Select(c => c.Cell).First(h => h != own && !m.View.OwnUnits.Any(u => u.Pos == h));
            int turn = m.View.TurnIndex;
            Assert.That(ui.DrawPlanArrow(to, own), Is.False, "an arrow starts on an own unit");
            Assert.That(ui.DrawPlanArrow(own, to), Is.True);
            Assert.That(ui.Plans.Count, Is.EqualTo(1));
            ui.TapCell(to); // a tap on the arrow head deletes it
            Assert.That(ui.Plans.Count, Is.EqualTo(0));
            for (int i = 0; i < PlanArrows.Max + 3; i++)
            {
                var t = m.View.Cells[i * 7 % m.View.Cells.Count].Cell;
                if (t != own) ui.DrawPlanArrow(own, t);
            }
            Assert.That(ui.Plans.Count, Is.LessThanOrEqualTo(PlanArrows.Max), "pooled, capped");
            Assert.That(ui.Plans.Count, Is.GreaterThan(0));
            Assert.That(commands, Is.EqualTo(0), "drawing and deleting arrows sends no commands");
            Assert.That(m.View.TurnIndex, Is.EqualTo(turn));
            m.Changed -= changed;

            int guard = 0;
            while (m.AiToAct && !m.IsOver && guard++ < 500) m.StepAi();
            Assert.That(m.AiToAct, Is.False);
            Assert.That(ui.Plans.Count, Is.EqualTo(0), "cleared at the start of the player's own turn");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
