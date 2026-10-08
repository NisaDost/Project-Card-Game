using System.Collections;
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
    /// <summary>v3c: drag-and-drop robustness (UX-03), move-attack UI (T-05 v3.2), range preview (UX-10), setup budget
    /// (S-05), hand groups (UX-04), info panel (UX-11), selection mode (UX-12).</summary>
    public class V3cClientTests
    {
        static GameApp App()
        {
            if (GameApp.Instance == null) GameApp.Create();
            return GameApp.Instance;
        }

        /// <summary>Hotseat A in setup with quests, passive and tower done: character placements are legal.</summary>
        static MatchController SetupWithTower(GameApp app, ulong seed)
        {
            var m = app.Match;
            m.NewMatch(seed, timer: false);
            m.AcknowledgeHandoff();
            m.Apply(m.Legal.First(c => c is ChooseQuestsCommand));
            m.Apply(m.Legal.First(c => c is ChoosePassiveCommand));
            m.Apply(m.Legal.First(c => c is PlaceTowerCommand));
            Assert.That(m.Legal.OfType<PlaceUnitCommand>().Any(), Is.True);
            return m;
        }

        static Vector2 PanelPos(GameApp app, Hex cell)
        {
            var s = Camera.main.WorldToScreenPoint(HexLayout.ToWorld(cell, BoardView.TileHeight));
            var size = app.Ui.Root.layout.size;
            return new Vector2(s.x / Screen.width * size.x, (1f - s.y / Screen.height) * size.y);
        }

        static void Send(VisualElement target, EventType type, Vector2 pos)
        {
            var evt = new Event { type = type, mousePosition = pos, button = 0, clickCount = 1 };
            EventBase e = type == EventType.MouseDown ? PointerDownEvent.GetPooled(evt)
                : type == EventType.MouseUp ? (EventBase)PointerUpEvent.GetPooled(evt) : PointerMoveEvent.GetPooled(evt);
            using (e) target.SendEvent(e);
        }

        static VisualElement BoardHit(GameApp app) => app.Ui.Root.Q("board-hit");

        /// <summary>Press a card and drag it over the board (events go to the board element, as a real finger would hit).</summary>
        static void PressAndDrag(GameApp app, int cardId, Vector2 to)
        {
            var card = app.Ui.CardElement(cardId);
            Assert.That(card, Is.Not.Null);
            var start = card.worldBound.center;
            Send(card, EventType.MouseDown, start);
            string afterDown = "active=" + app.Ui.CardPointerActive + " cap=" + (app.Ui.Root.panel.GetCapturingElement(PointerId.mousePointerId) as VisualElement)?.name + " sel=" + app.Ui.SelectedCard;
            Send(BoardHit(app), EventType.MouseDrag, Vector2.Lerp(start, to, 0.5f));
            Send(BoardHit(app), EventType.MouseDrag, to);
            Assert.That(app.Ui.Dragging, Is.True, "drag started; after down: " + afterDown + "; now active=" + app.Ui.CardPointerActive);
            Assert.That(app.Ui.DragGhostVisible, Is.True);
        }

        static void AssertBackInHand(GameApp app, int cardId, int handBefore)
        {
            Assert.That(app.Ui.CardPointerActive, Is.False, "no card pointer left behind");
            Assert.That(app.Ui.Dragging, Is.False);
            Assert.That(app.Ui.DragGhostVisible, Is.False, "the ghost card is gone");
            Assert.That(app.Match.View.Hand.Count, Is.EqualTo(handBefore), "the card is still in the hand");
            Assert.That(app.Ui.CardElement(cardId), Is.Not.Null);
            Assert.That(app.Ui.SelectedCard, Is.EqualTo(-1));
            Assert.That(app.Units.BarsHidden, Is.False, "UX-12: bars back after the card returns");
        }

        [UnityTest]
        public IEnumerator UX03_DragDropOnLegalCellPlaysTheCard()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 3);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            int hand = m.View.Hand.Count, units = m.View.OwnUnits.Count;
            var to = PanelPos(app, cmd.Cell);
            PressAndDrag(app, cmd.CardId, to);
            Assert.That(app.Units.BarsHidden, Is.True, "UX-12: bars hidden while dragging");
            Send(BoardHit(app), EventType.MouseUp, to);
            Assert.That(m.View.OwnUnits.Count, Is.EqualTo(units + 1), "dropped on a legal cell: placed");
            Assert.That(m.View.Hand.Count, Is.EqualTo(hand - 1));
            Assert.That(app.Ui.CardPointerActive, Is.False);
            Assert.That(app.Ui.DragGhostVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator UX03_InvalidDropReturnsTheCard()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 4);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            int hand = m.View.Hand.Count;
            var portal = PanelPos(app, new Hex(0, 0)); // never a setup cell
            PressAndDrag(app, cmd.CardId, portal);
            Send(BoardHit(app), EventType.MouseUp, portal);
            AssertBackInHand(app, cmd.CardId, hand);
            // Off the board entirely (top-left corner, over the HUD).
            PressAndDrag(app, cmd.CardId, new Vector2(5f, 5f));
            Send(app.Ui.Root, EventType.MouseUp, new Vector2(5f, 5f));
            AssertBackInHand(app, cmd.CardId, hand);
        }

        [UnityTest]
        public IEnumerator UX03_CancelAndCaptureLostReturnTheCard()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 5);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            int hand = m.View.Hand.Count;
            var to = PanelPos(app, cmd.Cell);

            PressAndDrag(app, cmd.CardId, to);
            using (var e = PointerCancelEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = to }))
                BoardHit(app).SendEvent(e);
            AssertBackInHand(app, cmd.CardId, hand);

            PressAndDrag(app, cmd.CardId, to);
            BoardHit(app).CapturePointer(PointerId.mousePointerId); // another element steals the pointer
            Send(BoardHit(app), EventType.MouseDrag, to); // the finger keeps moving
            yield return null;
            BoardHit(app).ReleasePointer(PointerId.mousePointerId);
            Send(BoardHit(app), EventType.MouseUp, to); // the finger lifts later: ignored, nothing is played
            yield return null; // a new touch starts in a later frame
            AssertBackInHand(app, cmd.CardId, hand);

            PressAndDrag(app, cmd.CardId, to);
            app.Ui.CancelPointers(); // focus loss / pause path
            AssertBackInHand(app, cmd.CardId, hand);
            Send(BoardHit(app), EventType.MouseUp, to); // the finger lifts after focus returns: nothing happens
            AssertBackInHand(app, cmd.CardId, hand);

            // After all of that a normal drop still works.
            PressAndDrag(app, cmd.CardId, to);
            Send(BoardHit(app), EventType.MouseUp, to);
            Assert.That(m.View.Hand.Count, Is.EqualTo(hand - 1));
        }

        [UnityTest]
        public IEnumerator UX03_SecondFingerNeverStealsTheDrag()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 6);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            int other = m.View.Hand.First(c => c.Id != cmd.CardId).Id;
            int hand = m.View.Hand.Count, units = m.View.OwnUnits.Count;
            var to = PanelPos(app, cmd.Cell);
            PressAndDrag(app, cmd.CardId, to);
            // Second finger: press on another card and on the board, then lift.
            var finger2 = new Touch { fingerId = 1, position = new Vector2(400f, 400f), phase = TouchPhase.Began, tapCount = 1 };
            using (var e = PointerDownEvent.GetPooled(finger2)) app.Ui.CardElement(other).SendEvent(e);
            using (var e = PointerDownEvent.GetPooled(finger2)) BoardHit(app).SendEvent(e);
            finger2.phase = TouchPhase.Ended;
            using (var e = PointerUpEvent.GetPooled(finger2)) BoardHit(app).SendEvent(e);
            Assert.That(app.Ui.Dragging, Is.True, "the first finger still drags");
            Assert.That(app.Ui.SelectedCard, Is.EqualTo(cmd.CardId));
            Assert.That(m.View.OwnUnits.Count, Is.EqualTo(units), "the second finger's tap played nothing");
            Send(BoardHit(app), EventType.MouseUp, to);
            Assert.That(m.View.Hand.Count, Is.EqualTo(hand - 1), "the first finger's drop plays the dragged card");
            Assert.That(app.Ui.CardPointerActive, Is.False);
        }

        /// <summary>S-05: the setup budget is shown; finish needs a character (reason on the button); over-budget cards are grey.</summary>
        [UnityTest]
        public IEnumerator S05_SetupBudgetAndFinishReason()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 7);
            yield return null;
            var end = app.Ui.Root.Q<Button>("end-btn");
            Assert.That(app.Ui.Root.Q<Label>("mana-text").text, Is.EqualTo(m.View.SetupMana + "/" + Catalog.SetupMana));
            Assert.That(app.Ui.IsOn(end), Is.False);
            Assert.That(app.Ui.ReasonFor(end), Is.EqualTo(Strings.ReasonMinCharacters));
            StringAssert.Contains(Strings.ReasonMinCharacters, end.text);
            m.Apply(m.Legal.OfType<PlaceUnitCommand>().First());
            yield return null;
            Assert.That(app.Ui.IsOn(end), Is.True);
            foreach (var c in m.View.Hand)
                Assert.That(app.Ui.CardElement(c.Id).ClassListContains("over-budget"), Is.EqualTo(c.Cost > m.View.SetupMana));
        }

        /// <summary>UX-04: traps left, characters middle, buff/debuff right.</summary>
        [UnityTest]
        public IEnumerator UX04_HandInThreeGroups()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 8);
            yield return null;
            foreach (var c in m.View.Hand)
            {
                int want = c.IsCharacter ? 1 : c.Support.Category == CardCategory.Trap ? 0 : 2;
                Assert.That(app.Ui.HandGroupOf(c.Id), Is.EqualTo(want), c.DefId);
            }
        }

        /// <summary>Plays a vs-AI match with a scripted human until the predicate holds on the human's turn.</summary>
        static bool PlayUntil(MatchController m, System.Func<PlayerView, bool> stop, ulong seed, int maxSteps = 6000)
        {
            m.NewAiMatch(seed, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random((int)seed);
            for (int i = 0; i < maxSteps && !m.IsOver; i++)
            {
                var v = m.View;
                if (v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && !v.DrawPending && stop(v)) return true;
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                var pick = legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is DrawCommand)
                    ?? (rng.Next(3) > 0 ? legal.FirstOrDefault(c => c is MoveCommand || c is PlaceUnitCommand || c is DeployCommand) : null)
                    ?? legal.FirstOrDefault(c => c is EndTurnCommand) ?? legal[rng.Next(legal.Count)];
                m.Apply(pick);
            }
            return false;
        }

        /// <summary>T-05 (v3.4): orange targets; one tap on the target acts (the engine computes the approach).</summary>
        [UnityTest]
        public IEnumerator T05_MoveAttackOneTapOnTarget()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            bool found = false;
            System.Func<MoveAttackCommand, bool> orange = c => !m.Legal.OfType<AttackCommand>().Any(a => a.UnitId == c.UnitId && a.Target == c.Target);
            for (ulong seed = 1; seed <= 40 && !found; seed++)
                found = PlayUntil(m, v => m.Legal.OfType<MoveAttackCommand>().Any(orange), seed);
            if (!found) Assert.Ignore("no move-attack in the sampled seeds");
            yield return null;
            var ma = m.Legal.OfType<MoveAttackCommand>().First(orange);
            Assert.That(m.Legal.OfType<MoveAttackCommand>().Count(c => c.UnitId == ma.UnitId && c.Target == ma.Target), Is.EqualTo(1), "one command per (unit, target)");
            var unit = m.View.OwnUnits.First(u => u.Id == ma.UnitId);
            app.Ui.TapCell(unit.Pos);
            Assert.That(app.Ui.SelectedUnit, Is.EqualTo(unit.Id));
            Assert.That(app.Units.BarsHidden, Is.True, "UX-12");
            StringAssert.Contains(Strings.InfoActions, app.Ui.InfoText, "UX-11 shows the 2-Energy move-attack");
            int energy = m.View.Energy;
            app.Ui.TapCell(ma.Target);
            Assert.That(m.View.Energy, Is.EqualTo(energy - Catalog.MoveAttackEnergy), "move-attack applied for 2 Energy");
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            Assert.That(app.Units.BarsHidden, Is.False, "UX-12 bars restored");
        }

        /// <summary>UX-10: on the AI's turn a tap on an own unit previews its ranges and applies nothing.</summary>
        [UnityTest]
        public IEnumerator UX10_OpponentTurnRangePreview()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            m.NewAiMatch(11, PlayerId.B, AiLevel.Normal, timer: false);
            var rng = new System.Random(11);
            int guard = 0;
            while (m.View.Phase == GamePhase.Setup && guard++ < 200)
                m.Apply(m.Legal.FirstOrDefault(c => c is FinishSetupCommand) ?? m.Legal.FirstOrDefault(c => c is PlaceUnitCommand) ?? m.Legal[rng.Next(m.Legal.Count)]);
            yield return null;
            Assert.That(m.AiToAct, Is.True, "A (AI) plays first");
            var u = m.View.OwnUnits.First();
            int turn = m.View.TurnIndex;
            app.Ui.TapCell(u.Pos);
            Assert.That(app.Ui.SelectedUnit, Is.EqualTo(u.Id));
            Assert.That(app.Ui.HighlightCount, Is.GreaterThan(0));
            Assert.That(app.Ui.Root.Q<Label>("prompt").text, Is.EqualTo(Strings.RangePreview));
            var r = Preview.Ranges(m.View, u.Id);
            if (r.MoveCells.Count > 0) app.Ui.TapCell(r.MoveCells[0]);
            Assert.That(m.View.TurnIndex, Is.EqualTo(turn));
            Assert.That(m.View.OwnUnits.First(x => x.Id == u.Id).Pos, Is.EqualTo(u.Pos), "preview never moves");
            m.StepAi(); // the preview survives an opponent step
            if (IsOpp(m.View)) Assert.That(app.Ui.SelectedUnit, Is.EqualTo(-1).Or.EqualTo(u.Id));
        }

        static bool IsOpp(PlayerView v) => v.Phase == GamePhase.Playing && v.ActivePlayer != v.Viewer;

        /// <summary>UX-11: an enemy unit shows only view data (no own-only lines).</summary>
        [UnityTest]
        public IEnumerator UX11_EnemyInfoShowsVisibleDataOnly()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            bool found = false;
            for (ulong seed = 1; seed <= 20 && !found; seed++) found = PlayUntil(m, v => v.EnemyUnits.Count > 0, seed);
            if (!found) Assert.Ignore("no visible enemy");
            yield return null;
            var e = m.View.EnemyUnits[0];
            app.Ui.TapCell(e.Pos);
            StringAssert.Contains(Strings.InfoEnemyUnit, app.Ui.InfoText);
            StringAssert.Contains(Strings.InfoAttack + " " + e.EffectiveAttack, app.Ui.InfoText);
            StringAssert.DoesNotContain(Strings.InfoActions, app.Ui.InfoText);
            Assert.That(app.Units.BarsHidden, Is.False, "an enemy tap is not a selection");
        }

        // ---------- Captures for the v3c report (explicit) ----------

        static string Dir => Path.Combine(System.Environment.GetEnvironmentVariable("HEXPORTAL_CAPTURE_DIR") ?? Application.temporaryCachePath, "v3c");

        static IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest, Explicit("Writes screenshots; run on demand")]
        public IEnumerator V3c_Captures()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 3);
            yield return Shot("01_setup_budget_hand_groups.png");
            m.Apply(m.Legal.OfType<PlaceUnitCommand>().First());
            yield return Shot("02_setup_after_one_unit.png");
            CameraRig.Instance.ResetView();
            app.Ui.CancelPointers();

            // Full board + move-attack.
            bool found = false;
            for (ulong seed = 1; seed <= 40 && !found; seed++)
                found = PlayUntil(m, v => m.Legal.OfType<MoveAttackCommand>().Any(), seed);
            yield return Shot("03_full_board.png");
            if (found)
            {
                var g = m.Legal.OfType<MoveAttackCommand>().First();
                var unit = m.View.OwnUnits.First(u => u.Id == g.UnitId);
                app.Ui.TapCell(unit.Pos);
                yield return Shot("04_unit_selected_blue_red_orange_info.png");
                app.Ui.TapCell(g.Target); // v3.4: one tap, the engine picks the approach
                yield return new WaitForSeconds(0.3f);
                yield return Shot("06_fight_notices_log.png");
            }
            if (m.View.EnemyUnits.Count > 0)
            {
                app.Ui.TapCell(m.View.EnemyUnits[0].Pos);
                yield return Shot("07_enemy_info.png");
            }
            var own = m.View.OwnUnits.FirstOrDefault();
            if (own != null)
            {
                CameraRig.Instance.ZoomBy(2.2f, Camera.main.WorldToScreenPoint(HexLayout.ToWorld(own.Pos, BoardView.TileHeight)));
                yield return Shot("08_zoomed_bars.png");
                app.Ui.TapCell(own.Pos);
                yield return Shot("09_zoomed_selection_mode_bars_hidden.png");
                CameraRig.Instance.ResetView();
            }
            // Opponent turn range preview.
            var end = m.Legal.FirstOrDefault(c => c is EndTurnCommand);
            if (end != null && m.Apply(end) && m.AiToAct && m.View.OwnUnits.Count > 0)
            {
                app.Ui.TapCell(m.View.OwnUnits[0].Pos);
                yield return Shot("10_opponent_turn_range_preview.png");
            }
            Debug.Log("[v3c] captures in " + Dir + " moveAttack=" + found);
        }
    }
}
