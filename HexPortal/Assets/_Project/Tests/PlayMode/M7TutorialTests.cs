using System.Collections;
using System.IO;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HexPortal.Game.Tests
{
    /// <summary>UX-08: the 4 tutorial steps advance on the player's actions and the tutorial exits cleanly.</summary>
    public class M7TutorialTests
    {
        static void Press(GameApp app, string name)
        {
            var b = app.Ui.Root.Q<Button>(name);
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        /// <summary>Plays the tutorial through the client API; capture = write step 1 / step 4 PNGs.</summary>
        static IEnumerator Run(bool capture)
        {
            if (GameApp.Instance == null) GameApp.Create();
            var app = GameApp.Instance;
            yield return null;
            var m = app.Match;
            var ui = app.Ui;
            var t = ui.Tutorial;
            int errorsBefore = app.LoggedErrors;
            string dir = Path.Combine(Application.temporaryCachePath, "m7d");
            if (capture) Directory.CreateDirectory(dir);

            Press(app, "menu-tutorial");
            yield return null;
            Assert.That(t.Step, Is.EqualTo(0));
            Assert.That(m.View.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(m.View.ActivePlayer, Is.EqualTo(PlayerId.A));
            Assert.That(t.Text, Is.EqualTo(Strings.TutorialStep1));
            if (capture) yield return Shot(dir, "01_step1.png");

            int fogStart = -1;
            int guard = 0;
            while (t.Step < 3 && guard++ < 400)
            {
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                ICommand cmd = legal.FirstOrDefault(c => c is DrawCommand);
                if (cmd == null && t.Step == 1) cmd = legal.FirstOrDefault(c => c is DeployCommand || c is PlayCardCommand);
                if (cmd == null) cmd = legal.FirstOrDefault(c => c is MoveCommand);
                if (cmd == null) cmd = legal.First(c => c is EndTurnCommand);
                if (t.Step == 2 && fogStart < 0) fogStart = m.View.TurnIndex;
                int before = t.Step;
                Assert.That(m.Apply(cmd), Is.True);
                if (cmd is MoveCommand && before == 0) Assert.That(t.Step, Is.EqualTo(1), "a move finishes the Energy step");
                if ((cmd is DeployCommand || cmd is PlayCardCommand) && before == 1) Assert.That(t.Step, Is.EqualTo(2), "a card finishes the Mana step");
            }
            Assert.That(t.Step, Is.EqualTo(3), "exploring finishes the fog step");
            Assert.That(m.View.TurnIndex, Is.LessThanOrEqualTo(fogStart + 2), "fog step ends within one full own turn");
            Assert.That(t.Text, Is.EqualTo(Strings.TutorialStep4));
            yield return null;
            if (capture) yield return Shot(dir, "02_step4.png");
            Press(app, "quest-btn");
            Assert.That(t.Step, Is.EqualTo(Tutorial.Steps), "opening the quests finishes the last step");
            Assert.That(t.Text, Is.EqualTo(Strings.TutorialDone));
            app.Ui.Root.Q("quest-panel").style.display = DisplayStyle.None;

            Press(app, "tutorial-exit");
            yield return null;
            Assert.That(t.Active, Is.False);
            Assert.That(m.HasMatch, Is.False);
            Assert.That(ui.Current, Is.EqualTo(GameUi.Screen.Menu));
            Assert.That(app.LoggedErrors, Is.EqualTo(errorsBefore));
        }

        [UnityTest]
        public IEnumerator UX08_TutorialStepsAdvanceAndExit()
        {
            yield return Run(false);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>"Atla" skips a step without an action.</summary>
        [UnityTest]
        public IEnumerator UX08_SkipAdvances()
        {
            if (GameApp.Instance == null) GameApp.Create();
            var app = GameApp.Instance;
            yield return null;
            Press(app, "menu-tutorial");
            Assert.That(app.Ui.Tutorial.Step, Is.EqualTo(0));
            Press(app, "tutorial-skip");
            Assert.That(app.Ui.Tutorial.Step, Is.EqualTo(1));
            Press(app, "tutorial-exit");
            Assert.That(app.Match.HasMatch, Is.False);
        }

        /// <summary>The fog step ends after one full own turn even if nothing new was explored.</summary>
        [UnityTest]
        public IEnumerator UX08_FogStepEndsAfterOneOwnTurn()
        {
            if (GameApp.Instance == null) GameApp.Create();
            var app = GameApp.Instance;
            yield return null;
            var m = app.Match;
            var t = app.Ui.Tutorial;
            Press(app, "menu-tutorial");
            Press(app, "tutorial-skip");
            Press(app, "tutorial-skip");
            Assert.That(t.Step, Is.EqualTo(2));
            int start = m.View.TurnIndex, guard = 0;
            while (t.Step == 2 && guard++ < 50)
            {
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                Assert.That(m.Apply(legal.FirstOrDefault(c => c is DrawCommand) ?? legal.First(c => c is EndTurnCommand)), Is.True);
            }
            Assert.That(t.Step, Is.EqualTo(3));
            Assert.That(m.View.TurnIndex, Is.LessThanOrEqualTo(start + 2), "at most one full own turn");
            Press(app, "tutorial-exit");
        }

        [UnityTest, Explicit("Writes screenshots; run on demand")]
        public IEnumerator UX08_Captures() => Run(true);

        static IEnumerator Shot(string dir, string name)
        {
            yield return new WaitForSeconds(0.9f);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
