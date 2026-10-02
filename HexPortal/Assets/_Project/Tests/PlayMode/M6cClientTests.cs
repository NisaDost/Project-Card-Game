using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HexPortal.Game.Tests
{
    /// <summary>M6c device-test fixes: no physics, pooled visuals, camera/picking, button reasons, C-05 hint, event log.</summary>
    public class M6cClientTests
    {
        static GameApp App()
        {
            if (GameApp.Instance == null) GameApp.Create();
            return GameApp.Instance;
        }

        static int ObjectCount() =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        /// <summary>Scripted human: lasting buffs on own units first, then attacks, then a few moves/deploys, then end.</summary>
        static ICommand HumanChoice(MatchController m, System.Random rng, ref int actions)
        {
            var legal = m.Legal;
            if (m.View.Phase == GamePhase.Setup)
                return legal.FirstOrDefault(c => c is FinishSetupCommand)
                       ?? (m.View.SetupStep == SetupStep.Placement ? legal.First(c => c is PlaceUnitCommand) : legal[rng.Next(legal.Count)]);
            var draw = legal.FirstOrDefault(c => c is DrawCommand);
            if (draw != null) return draw;
            var buff = legal.FirstOrDefault(c => c is PlayCardCommand p && IsLastingBuff(m.View, p.CardId)
                                                 && m.View.OwnUnits.Any(u => u.Pos == p.Target));
            if (buff != null) return buff;
            var attacks = legal.Where(c => c is AttackCommand).ToList();
            if (attacks.Count > 0) return attacks[rng.Next(attacks.Count)];
            var moves = legal.Where(c => c is MoveCommand || c is DeployCommand).ToList();
            if (moves.Count > 0 && actions++ < 4) return moves[rng.Next(moves.Count)];
            return legal.FirstOrDefault(c => c is EndTurnCommand) ?? legal[0];
        }

        static bool IsLastingBuff(PlayerView v, int cardId)
        {
            var c = v.Hand.FirstOrDefault(x => x.Id == cardId);
            return c != null && !c.IsCharacter && c.Support.Pool == SupportPool.Buff && c.Support.Duration != DurationKind.Instant;
        }

        /// <summary>Device bug A: a whole AI match through the real client (views, UI, events) creates no GameObjects
        /// beyond the pooled pieces (fixed objects per piece, pool never larger than the most pieces shown at once),
        /// adds no colliders and logs no errors.</summary>
        [UnityTest]
        public IEnumerator M6c_AiMatchHasBoundedObjectsNoCollidersNoErrors()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            var units = app.Units;
            int errorsBefore = app.LoggedErrors;
            int poolBefore = units.PoolSize;
            int fixedObjects = ObjectCount() - units.PoolSize * UnitsView.ObjectsPerPiece - units.BurstPoolSize;
            m.NewAiMatch(77, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random(77);
            int steps = 0, actions = 0, maxShown = 0, lastTurn = -1, turns = 0;
            while (!m.IsOver && steps++ < 50000)
            {
                if (m.AiToAct) { m.StepAi(); actions = 0; }
                else Assert.That(m.Apply(HumanChoice(m, rng, ref actions)), Is.True);
                maxShown = Mathf.Max(maxShown, units.ShownCount);
                if (m.View.TurnIndex != lastTurn)
                {
                    lastTurn = m.View.TurnIndex;
                    turns++;
                    yield return null; // let LateUpdate, floaters and bursts run
                    Assert.That(ObjectCount(), Is.EqualTo(fixedObjects + units.PoolSize * UnitsView.ObjectsPerPiece + units.BurstPoolSize),
                        "only pooled pieces and bursts may add GameObjects (turn " + turns + ")");
                }
            }
            Assert.That(m.IsOver, Is.True);
            Assert.That(turns, Is.GreaterThan(4));
            Assert.That(units.PoolSize, Is.LessThanOrEqualTo(Mathf.Max(maxShown, poolBefore)), "pool reuses visuals of dead units");
            Assert.That(units.BurstPoolSize, Is.LessThanOrEqualTo(6));
            Assert.That(Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty,
                "no colliders: the Physics module is stripped in builds");
            Assert.That(app.LoggedErrors, Is.EqualTo(errorsBefore), "no errors logged during the match");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>No type in the client assembly references the Physics module (stripped on device).</summary>
        [Test]
        public void M6c_ClientHasNoPhysicsDependency()
        {
            var asm = typeof(GameApp).Assembly;
            var refs = asm.GetReferencedAssemblies().Select(a => a.Name).ToList();
            Assert.That(refs, Has.None.Contains("PhysicsModule"));
        }

        /// <summary>Picking is the nearest cell centre (no physics), works zoomed in, and pan/zoom stay on the board.</summary>
        [UnityTest]
        public IEnumerator M6c_PickZoomPanAreBoardBounded()
        {
            var app = App();
            yield return null;
            app.Match.NewAiMatch(5, PlayerId.A, AiLevel.Easy, timer: false);
            yield return null;
            var cam = Camera.main;
            var rig = CameraRig.Instance;
            Assert.That(rig, Is.Not.Null);
            CameraRig.Face(cam, PlayerId.A);
            AssertPicksAllCells(cam, 0.6f);
            rig.ZoomBy(10f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.That(rig.Zoom, Is.GreaterThan(1.5f).And.LessThanOrEqualTo(1f / 0.35f + 0.01f), "zoom is clamped");
            for (int i = 0; i < 20; i++) rig.Pan(new Vector2(0f, 0f), new Vector2(Screen.width, Screen.height)); // fling far away
            Assert.That(CameraRig.TryPick(cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out _), Is.True,
                "pan is clamped: the screen centre stays over the board");
            rig.ZoomBy(0.001f, Vector2.zero);
            Assert.That(rig.Zoom, Is.EqualTo(1f).Within(0.001f), "never zooms out past the default framing");
            rig.ResetView();
            CameraRig.Face(cam, PlayerId.B); // B-05: rotated view picks the same data cells
            AssertPicksAllCells(cam, 0.6f);
            CameraRig.Face(cam, PlayerId.A);
        }

        static void AssertPicksAllCells(Camera cam, float offset)
        {
            foreach (var h in Board.Cells)
            {
                var w = HexLayout.ToWorld(h, BoardView.TileHeight);
                Assert.That(CameraRig.TryPick(cam, cam.WorldToScreenPoint(w), out var c) && c == h, Is.True, "centre of " + h);
                // A tap off the centre (inside the hex) still hits the cell: big hit areas.
                Assert.That(CameraRig.TryPick(cam, cam.WorldToScreenPoint(w + new Vector3(offset, 0f, 0f)), out c) && c == h, Is.True, "off-centre " + h);
            }
        }

        /// <summary>UX: an off button explains itself instead of doing nothing.</summary>
        [UnityTest]
        public IEnumerator M6c_OffButtonShowsReason()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            m.NewAiMatch(9, PlayerId.A, AiLevel.Easy, timer: false);
            var rng = new System.Random(9);
            int actions = 0, guard = 0;
            while (m.View.Phase == GamePhase.Setup && guard++ < 200) m.Apply(HumanChoice(m, rng, ref actions));
            Assert.That(m.View.Phase, Is.EqualTo(GamePhase.Playing));
            Button b;
            string reason;
            if (m.View.DrawPending) { b = app.Ui.Root.Q<Button>("end-btn"); reason = Strings.ReasonDrawFirst; } // T-04 draw first
            else { b = app.Ui.Root.Q<Button>("market-0"); reason = Strings.ReasonHandFull; } // D-07: the dealt hand is over the limit
            Assert.That(app.Ui.IsOn(b), Is.False);
            Assert.That(app.Ui.ReasonFor(b), Is.EqualTo(reason));
            int hand = m.View.Hand.Count;
            bool pending = m.View.DrawPending;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
            Assert.That(app.Ui.LastReason, Is.EqualTo(reason));
            Assert.That(m.View.DrawPending, Is.EqualTo(pending), "an off button changes nothing");
            Assert.That(m.View.Hand.Count, Is.EqualTo(hand));
        }

        /// <summary>C-05/C-06: a lasting buff on a unit that already has one asks first ("... kartının yerini alacak");
        /// confirming plays it, and the event log records the plays.</summary>
        [UnityTest]
        public IEnumerator C05_ReplacingABuffAsksForConfirmation()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            bool checkedOnce = false;
            for (ulong seed = 1; seed <= 80 && !checkedOnce; seed++)
            {
                m.NewMatch(seed, timer: false);
                var rng = new System.Random((int)seed);
                int steps = 0, actions = 0;
                while (!m.IsOver && steps++ < 4000 && !checkedOnce)
                {
                    if (m.HandoffPending) { m.AcknowledgeHandoff(); actions = 0; continue; }
                    var v = m.View;
                    if (v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && !v.DrawPending)
                    {
                        var buffed = v.OwnUnits.FirstOrDefault(u => u.BuffId != null);
                        var next = buffed == null ? null : m.Legal.OfType<PlayCardCommand>()
                            .FirstOrDefault(p => p.Target == buffed.Pos && IsLastingBuff(v, p.CardId));
                        if (next != null)
                        {
                            string old = buffed.BuffId;
                            app.Ui.TapCard(next.CardId);
                            app.Ui.TapCell(buffed.Pos);
                            Assert.That(app.Ui.ConfirmText, Is.EqualTo(Strings.Replaces(old)));
                            int hand = v.Hand.Count;
                            app.Ui.AcceptConfirm();
                            Assert.That(m.View.Hand.Count, Is.EqualTo(hand - 1), "confirm plays the card");
                            StringAssert.Contains(Strings.YouPlayed, app.Ui.EventLogText);
                            checkedOnce = true;
                            break;
                        }
                    }
                    Assert.That(m.Apply(HumanChoice(m, rng, ref actions)), Is.True);
                }
            }
            if (!checkedOnce) Assert.Ignore("No second lasting buff on a buffed unit in the sampled seeds.");
        }

        /// <summary>Captures for the M6c report (run explicitly): HUD, buffed unit, event log, zoomed view. PNGs go to
        /// Application.temporaryCachePath/m6c.</summary>
        [UnityTest, Explicit("Writes screenshots; run on demand")]
        public IEnumerator M6c_Captures()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            var dir = Path.Combine(Application.temporaryCachePath, "m6c");
            Directory.CreateDirectory(dir);
            m.NewAiMatch(31, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random(31);
            int steps = 0, actions = 0;
            bool hud = false, buff = false, fight = false, reason = false;
            while (!m.IsOver && steps++ < 20000 && !(hud && buff && fight))
            {
                var v = m.View;
                if (!reason && v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer)
                {
                    var off = new[] { "end-btn", "market-0", "blind-btn" }.Select(n => app.Ui.Root.Q<Button>(n)).FirstOrDefault(x => !app.Ui.IsOn(x));
                    if (off != null)
                    {
                        using (var e = NavigationSubmitEvent.GetPooled()) { e.target = off; off.SendEvent(e); }
                        yield return Shot(dir, "00_off_button_reason.png");
                        reason = true;
                    }
                }
                bool humanTurn = v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && !v.DrawPending;
                if (humanTurn && !hud && v.Round >= 2) { yield return Shot(dir, "01_hud.png"); hud = true; }
                if (humanTurn && !buff && v.OwnUnits.Any(u => u.BuffId != null))
                {
                    var u = v.OwnUnits.First(x => x.BuffId != null);
                    CameraRig.Instance.ZoomBy(2.2f, Camera.main.WorldToScreenPoint(HexLayout.ToWorld(u.Pos, BoardView.TileHeight)));
                    yield return Shot(dir, "02_unit_buff_zoomed.png");
                    CameraRig.Instance.ResetView();
                    buff = true;
                }
                if (!fight && app.Ui.EventLogText.Contains("−") && v.Round >= 3)
                {
                    yield return new WaitForSeconds(0.25f); // floaters mid-flight
                    yield return Shot(dir, "03_event_log_fight.png");
                    fight = true;
                }
                if (m.AiToAct) { m.StepAi(); actions = 0; yield return new WaitForSeconds(0.05f); }
                else m.Apply(HumanChoice(m, rng, ref actions));
            }
            Debug.Log("[M6c] captures in " + dir + " hud=" + hud + " buff=" + buff + " fight=" + fight + " errors=" + app.LoggedErrors);
            yield return Shot(dir, "04_end_error_counter.png");
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
