using System.Collections;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HexPortal.Game.Tests
{
    /// <summary>GDD v3.3 client: UX-13 range chip and held-marker glow, UX-14 own traps only, UX-15 pause menu.</summary>
    public class V33ClientTests
    {
        static GameApp App()
        {
            if (GameApp.Instance == null) GameApp.Create();
            return GameApp.Instance;
        }

        static void Press(GameApp app, string name)
        {
            var b = app.Ui.Root.Q<Button>(name);
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        static int ExpectedHeld(PlayerView v) =>
            v.Cells.Count(c => c.TerrainKnown && (c.Tile.Marker == Marker.RuneStone || c.Tile.Marker == Marker.Wellspring)
                               && c.Unit != null && !c.Unit.IsGhost && c.Unit.Owner == v.Viewer);

        /// <summary>Over a whole AI match: every shown unit has a range chip; the held glow and the trap props always
        /// match the view; the opponent's traps are never drawn (only PlayerView.OwnTraps exists).</summary>
        [UnityTest]
        public IEnumerator UX13_UX14_ChipsGlowAndOwnTrapsFollowTheView()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            var board = Object.FindAnyObjectByType<BoardView>(FindObjectsInactive.Include);
            m.NewAiMatch(52, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random(52);
            bool sawTrap = false, sawUnit = false;
            for (int i = 0; i < 4000 && !m.IsOver; i++)
            {
                if (m.AiToAct) m.StepAi();
                else
                {
                    var legal = m.Legal;
                    var cmd = legal.FirstOrDefault(c => c is FinishSetupCommand && m.View.OwnTraps.Count > 0)
                              ?? legal.FirstOrDefault(c => c is PlaceTrapCommand)
                              ?? legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is DrawCommand)
                              ?? (rng.Next(3) > 0 ? legal.FirstOrDefault(c => c is MoveCommand || c is DeployCommand) : null)
                              ?? legal.FirstOrDefault(c => c is EndTurnCommand) ?? legal[rng.Next(legal.Count)];
                    Assert.That(m.Apply(cmd), Is.True);
                }
                var v = m.View;
                Assert.That(board.TrapsShown, Is.EqualTo(v.OwnTraps.Count), "own traps drawn, nothing else");
                Assert.That(board.HeldCount, Is.EqualTo(ExpectedHeld(v)), "held rune/wellspring glow");
                sawTrap |= v.OwnTraps.Count > 0;
                if (v.Phase == GamePhase.Playing && v.OwnUnits.Count > 0 && !sawUnit)
                {
                    sawUnit = true;
                    yield return null;
                    Assert.That(app.Ui.Root.Query<Label>(className: "chip-range").ToList().Any(l => l.text.StartsWith("➶")), Is.True, "UX-13 range chip");
                }
            }
            Assert.That(sawUnit, Is.True);
            if (!sawTrap) Assert.Ignore("no own trap placed in this seed");
        }

        /// <summary>UX-15: pause stops the AI and blocks board input; Devam resumes; "Ana menüye dön" asks, then leaves.</summary>
        [UnityTest]
        public IEnumerator UX15_PauseMenu()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            m.NewAiMatch(53, PlayerId.A, AiLevel.Easy, timer: false);
            int guard = 0;
            while (!(m.View.Phase == GamePhase.Playing && m.View.ActivePlayer == PlayerId.A) && guard++ < 200)
            {
                var legal = m.Legal;
                m.Apply(legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is PlaceTowerCommand)
                        ?? legal.FirstOrDefault(c => c is PlaceUnitCommand) ?? legal[0]);
            }
            var legal2 = m.Legal;
            m.Apply(legal2.FirstOrDefault(c => c is DrawCommand) ?? legal2.First());
            m.Apply(m.Legal.First(c => c is EndTurnCommand));
            Assert.That(m.AiToAct, Is.True);
            Press(app, "pause-btn");
            Assert.That(app.Ui.Paused, Is.True);
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Locked));
            int turn = m.View.TurnIndex;
            yield return new WaitForSeconds(GameApp.AiStepSeconds * 3f);
            Assert.That(m.View.TurnIndex, Is.EqualTo(turn), "the AI waits while paused");
            Assert.That(m.AiToAct, Is.True);
            bool timer = m.TimerEnabled;
            Press(app, "pause-timer");
            Assert.That(m.TimerEnabled, Is.EqualTo(!timer));
            Assert.That(app.Ui.Root.Q<Button>("pause-sound").enabledSelf, Is.False, "sound: yakında");
            Press(app, "pause-resume");
            Assert.That(app.Ui.Paused, Is.False);
            guard = 0;
            while (m.AiToAct && guard++ < 100) yield return new WaitForSeconds(0.1f);
            Assert.That(m.AiToAct, Is.False, "the AI continued after Devam");

            Press(app, "pause-btn");
            Press(app, "pause-menu");
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Locked));
            Assert.That(m.HasMatch, Is.True, "asks first");
            app.Ui.AcceptConfirm();
            Assert.That(m.HasMatch, Is.False);
            Assert.That(app.Ui.Current, Is.EqualTo(GameUi.Screen.Menu));
            Assert.That(app.Ui.Paused, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
