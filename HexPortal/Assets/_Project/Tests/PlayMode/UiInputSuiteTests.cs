using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HexPortal.Game.Tests
{
    /// <summary>
    /// Permanent input regression suite (GDD v3.4 device reports, UX-03, UX-15, UX-16). The tested action is always a
    /// simulated finger on a test Touchscreen, routed by the Input System and UI Toolkit to the real GameUi. To reach a
    /// play situation quickly, earlier turns are fast-forwarded with legal commands through the MatchController (never
    /// GameUi handlers). Fixed seeds keep it deterministic.
    /// </summary>
    public class UiInputSuiteTests
    {
        PointerDriver drv;

        static GameUi Ui => GameApp.Instance.Ui;
        static MatchController M => GameApp.Instance.Match;
        static VisualElement Q(string name) => Ui.Root.Q(name);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameApp.Instance == null) GameApp.Create();
            yield return null;
            if (M.HasMatch) M.Close();
            drv = new PointerDriver();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            drv?.Dispose();
            drv = null;
            GameUi.SafeAreaOverride = null;
            if (GameApp.Instance != null) M.Paused = false;
            if (GameApp.Instance != null && M.HasMatch) M.Close();
        }

        // ---------- helpers ----------

        /// <summary>The element a finger at the cell would hit (null when off screen).</summary>
        static VisualElement PickAt(Hex cell)
        {
            var s = PointerDriver.CellScreen(cell);
            if (s.x < 0 || s.y < 0 || s.x > Screen.width || s.y > Screen.height) return null;
            var size = Ui.Root.layout.size;
            return Ui.Root.panel.Pick(new Vector2(s.x / Screen.width * size.x, (1f - s.y / Screen.height) * size.y));
        }

        /// <summary>A finger on the cell reaches the board (not covered by HUD, hand or panels).</summary>
        static bool Tappable(Hex cell) => PickAt(cell)?.name == "board-hit";

        static T FirstTappable<T>(IEnumerable<T> cmds, System.Func<T, Hex> cell) where T : class
        {
            foreach (var c in cmds) if (Tappable(cell(c))) return c;
            return null;
        }

        /// <summary>Menu → vs AI (Normal) → 3 quests → passive, all by taps. Ends on the board in the tower step.</summary>
        IEnumerator StartAiToTowerStep(ulong seed)
        {
            Assert.That(Ui.Current, Is.EqualTo(GameUi.Screen.Menu));
            Ui.NextSeed = seed;
            yield return drv.TapElement(Q("menu-ai-normal"));
            Assert.That(Ui.Current, Is.EqualTo(GameUi.Screen.Quests), "menu button started the match " + drv.Log);
            foreach (var o in Q("quest-options").Children().Take(3).ToList()) yield return drv.TapElement(o);
            yield return drv.TapElement(Q("quests-confirm"));
            Assert.That(Ui.Current, Is.EqualTo(GameUi.Screen.Passive), "quests confirmed by tap " + Ui.LastReason + " " + drv.Log);
            yield return drv.TapElement(Q("passive-options").Children().First());
            Assert.That(Ui.Current, Is.EqualTo(GameUi.Screen.Board), "passive chosen by tap");
            Assert.That(M.View.SetupStep, Is.EqualTo(SetupStep.Tower));
        }

        IEnumerator TapTower()
        {
            var cell = FirstTappable(M.Legal.OfType<PlaceTowerCommand>(), c => c.Cell);
            Assert.That(cell, Is.Not.Null, "a tower cell is reachable by a finger");
            yield return drv.TapCell(cell.Cell);
            Assert.That(M.View.OwnTowerPos, Is.EqualTo(cell.Cell), "tower placed by tap");
        }

        /// <summary>vs AI (human A) fast-forwarded with legal commands until <paramref name="stop"/> holds on A's turn.</summary>
        internal static bool FastForward(ulong seed, System.Func<PlayerView, bool> stop, bool beforeDraw = false, int maxSteps = 4000, bool spendCards = false)
        {
            var m = M;
            m.NewAiMatch(seed, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random((int)seed);
            for (int i = 0; i < maxSteps && !m.IsOver; i++)
            {
                var v = m.View;
                if (v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && v.DrawPending == beforeDraw && stop(v)) return true;
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                var pick = legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is DrawCommand)
                    ?? (spendCards ? legal.FirstOrDefault(c => c is DeployCommand || c is PlayCardCommand) : null)
                    ?? (rng.Next(3) > 0 ? legal.FirstOrDefault(c => c is MoveCommand || c is PlaceUnitCommand || c is DeployCommand) : null)
                    ?? legal.FirstOrDefault(c => c is EndTurnCommand) ?? legal[rng.Next(legal.Count)];
                m.Apply(pick);
            }
            return false;
        }

        static IEnumerator Settle()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f); // hop/pop tweens and the hand transition end
        }

        IEnumerator FastForwardOrIgnore(System.Func<PlayerView, bool> stop, string what, bool beforeDraw = false, params ulong[] seeds)
            => FastForwardOrIgnore(stop, what, beforeDraw, false, seeds);

        IEnumerator FastForwardOrIgnore(System.Func<PlayerView, bool> stop, string what, bool beforeDraw, bool spendCards, params ulong[] seeds)
        {
            foreach (var s in seeds)
                if (FastForward(s, stop, beforeDraw, 4000, spendCards)) { yield return Settle(); yield break; }
            Assert.Ignore("no " + what + " in the sampled seeds");
        }

        UnitView OwnUnit(int id) => M.View.OwnUnits.First(u => u.Id == id);

        static bool NoReplace(PlayerView v, PlayCardCommand p)
        {
            var card = v.Hand.First(c => c.Id == p.CardId);
            if (card.IsCharacter) return false;
            int i = Board.IndexOf(p.Target);
            var u = i >= 0 ? v.Cells[i].Unit : null;
            if (u == null || u.IsGhost || card.Support.Duration == DurationKind.Instant) return true;
            return (card.Support.Pool == SupportPool.Buff ? u.BuffId : u.DebuffId) == null;
        }

        /// <summary>A support card command that needs one target only (no C-15/C-18 second pick, no C-05 confirm).</summary>
        static PlayCardCommand SimpleCard(PlayerView v, IEnumerable<ICommand> legal) =>
            legal.OfType<PlayCardCommand>().Where(p => !p.Dest.HasValue && p.Direction < 0 && NoReplace(v, p)
                                                       && legal.OfType<PlayCardCommand>().Count(o => o.CardId == p.CardId && o.Target == p.Target) == 1)
                .FirstOrDefault(p => Tappable(p.Target));

        // ---------- setup (S-05) ----------

        /// <summary>Device report: "if the first tap is not tower placement, the tower can't be placed".</summary>
        [UnityTest]
        public IEnumerator Setup_FirstTapElsewhereThenTower()
        {
            yield return StartAiToTowerStep(101);
            var towers = M.Legal.OfType<PlaceTowerCommand>().Select(c => c.Cell).ToList();
            var other = M.View.PlayableCells.First(c => !towers.Contains(c) && Tappable(c) && c != new Hex(0, 0));
            yield return drv.TapCell(other);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle), "a tap on a plain cell leaves Idle");
            // ...and a character card (not playable before the tower): the tower cells must stay highlighted.
            yield return drv.TapElement(Ui.CardElement(M.View.Hand.First(c => c.IsCharacter).Id));
            Assert.That(Ui.HighlightCount, Is.GreaterThanOrEqualTo(towers.Count), "tower cells stay blue while a card is selected");
            yield return TapTower();
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Setup_CharacterByTapThenByDrag()
        {
            yield return StartAiToTowerStep(102);
            yield return TapTower();
            yield return Settle();
            // By tap: card, then a legal cell.
            var tap = FirstTappable(M.Legal.OfType<PlaceUnitCommand>(), c => c.Cell);
            Assert.That(tap, Is.Not.Null, "a placement cell is reachable");
            yield return drv.TapElement(Ui.CardElement(tap.CardId));
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.CardSelected), "card tap " + drv.Log);
            yield return Settle(); // the hand slides down (UX-12)
            Assert.That(Tappable(tap.Cell), Is.True, "the target is not under the hand");
            yield return drv.TapCell(tap.Cell);
            Assert.That(M.View.OwnUnits.Count, Is.EqualTo(1), "character placed by tap");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            yield return Settle();
            // By drag: another affordable character to another cell.
            var drag = FirstTappable(M.Legal.OfType<PlaceUnitCommand>(), c => c.Cell);
            Assert.That(drag, Is.Not.Null, "budget left for a second character");
            yield return drv.Drag(PointerDriver.ElementScreen(Ui.CardElement(drag.CardId)), PointerDriver.CellScreen(drag.Cell));
            Assert.That(M.View.OwnUnits.Count, Is.EqualTo(2), "character placed by drag");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            Assert.That(Ui.DragGhostVisible, Is.False);
        }

        static ulong TrapSeed(int skip)
        {
            for (ulong s = 200; s < 400; s++)
            {
                M.NewAiMatch(s, PlayerId.A, AiLevel.Normal, timer: false);
                if (M.View.Hand.Any(c => !c.IsCharacter && c.Support.Category == CardCategory.Trap) && skip-- <= 0) { M.Close(); return s; }
            }
            M.Close();
            Assert.Ignore("no dealt trap in the sampled seeds");
            return 0;
        }

        IEnumerator SetupToTrap(ulong seed)
        {
            yield return StartAiToTowerStep(seed);
            yield return TapTower();
            yield return Settle();
            var unit = FirstTappable(M.Legal.OfType<PlaceUnitCommand>().Where(c => M.View.Hand.First(h => h.Id == c.CardId).Cost <= 3), c => c.Cell);
            yield return drv.TapElement(Ui.CardElement(unit.CardId));
            yield return Settle();
            yield return drv.TapCell(unit.Cell);
            Assert.That(M.View.OwnUnits.Count, Is.EqualTo(1));
            yield return Settle();
        }

        [UnityTest]
        public IEnumerator Setup_TrapByTap()
        {
            yield return SetupToTrap(TrapSeed(0));
            var trap = FirstTappable(M.Legal.OfType<PlaceTrapCommand>(), c => c.Cell);
            Assert.That(trap, Is.Not.Null, "a trap cell is reachable");
            yield return drv.TapElement(Ui.CardElement(trap.CardId));
            yield return Settle();
            yield return drv.TapCell(trap.Cell);
            Assert.That(M.View.OwnTraps.Count, Is.EqualTo(1), "trap placed by tap");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Setup_TrapByDragThenFinish()
        {
            yield return SetupToTrap(TrapSeed(1));
            var trap = FirstTappable(M.Legal.OfType<PlaceTrapCommand>(), c => c.Cell);
            Assert.That(trap, Is.Not.Null, "a trap cell is reachable");
            yield return drv.Drag(PointerDriver.ElementScreen(Ui.CardElement(trap.CardId)), PointerDriver.CellScreen(trap.Cell));
            Assert.That(M.View.OwnTraps.Count, Is.EqualTo(1), "trap placed by drag");
            // Finish (S-05): the AI set up at once, so play starts.
            yield return drv.TapElement(Q("end-btn"));
            Assert.That(M.View.Phase, Is.EqualTo(GamePhase.Playing), "setup finished by tap");
            Assert.That(Ui.Mode, Is.Not.EqualTo(GameUi.InputMode.Dragging));
        }

        // ---------- play ----------

        [UnityTest]
        public IEnumerator Play_DrawBlindAndMarket()
        {
            yield return FastForwardOrIgnore(v => v.Round >= 2 && v.Hand.Count < Catalog.HandLimit, "draw", true, true, 7, 8, 9);
            int hand = M.View.Hand.Count;
            bool market = M.Legal.OfType<DrawCommand>().Any(d => d.Slot == 1);
            yield return drv.TapElement(market ? Q("market-1") : Q("blind-btn"));
            Assert.That(M.View.DrawPending, Is.False, "drawn by tap");
            Assert.That(M.View.Hand.Count, Is.EqualTo(hand + 1));
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Play_Move()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<MoveCommand>().Any(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos)), "move", false, 11, 12);
            var mv = M.Legal.OfType<MoveCommand>().First(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos));
            yield return drv.TapCell(OwnUnit(mv.UnitId).Pos);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected));
            yield return drv.TapCell(mv.Dest);
            Assert.That(OwnUnit(mv.UnitId).Pos, Is.EqualTo(mv.Dest), "moved by taps");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Play_Attack()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<AttackCommand>().Any(c => Tappable(c.Target) && Tappable(OwnUnit(c.UnitId).Pos)), "attack", false, 3, 13, 23, 33, 43);
            var a = M.Legal.OfType<AttackCommand>().First(c => Tappable(c.Target) && Tappable(OwnUnit(c.UnitId).Pos));
            int energy = M.View.Energy;
            yield return drv.TapCell(OwnUnit(a.UnitId).Pos);
            yield return drv.TapCell(a.Target);
            Assert.That(M.View.Energy, Is.EqualTo(energy - 1), "attacked by taps");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        /// <summary>T-05 (v3.4): one tap on the orange target; the engine computes the approach.</summary>
        [UnityTest]
        public IEnumerator Play_MoveAttackOneTap()
        {
            System.Func<MoveAttackCommand, bool> ok = c => !M.Legal.OfType<AttackCommand>().Any(a => a.UnitId == c.UnitId && a.Target == c.Target)
                                                         && Tappable(c.Target) && Tappable(OwnUnit(c.UnitId).Pos);
            yield return FastForwardOrIgnore(v => M.Legal.OfType<MoveAttackCommand>().Any(ok), "move-attack", false, 3, 13, 23, 33, 43, 53);
            var ma = M.Legal.OfType<MoveAttackCommand>().First(ok);
            int energy = M.View.Energy;
            yield return drv.TapCell(OwnUnit(ma.UnitId).Pos);
            yield return drv.TapCell(ma.Target);
            Assert.That(M.View.Energy, Is.EqualTo(energy - Catalog.MoveAttackEnergy), "move-attack applied by one tap");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Play_Overwatch()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<OverwatchCommand>().Any(c => Tappable(OwnUnit(c.UnitId).Pos)), "overwatch", false, 11, 12);
            var o = M.Legal.OfType<OverwatchCommand>().First(c => Tappable(OwnUnit(c.UnitId).Pos));
            yield return drv.TapCell(OwnUnit(o.UnitId).Pos);
            Assert.That(Q("overwatch-btn").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            yield return drv.TapElement(Q("overwatch-btn"));
            Assert.That(OwnUnit(o.UnitId).OnOverwatch, Is.True, "overwatch by tap " + drv.Log);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Play_CardByTap()
        {
            yield return FastForwardOrIgnore(v => SimpleCard(v, M.Legal) != null, "playable card", false, 5, 15, 25);
            var p = SimpleCard(M.View, M.Legal);
            int hand = M.View.Hand.Count;
            yield return drv.TapElement(Ui.CardElement(p.CardId));
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.CardSelected));
            yield return Settle();
            yield return drv.TapCell(p.Target);
            Assert.That(M.View.Hand.Count, Is.EqualTo(hand - 1), "card played by tap");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Play_CardByDrag()
        {
            yield return FastForwardOrIgnore(v => SimpleCard(v, M.Legal) != null, "playable card", false, 6, 16, 26);
            var p = SimpleCard(M.View, M.Legal);
            int hand = M.View.Hand.Count;
            yield return drv.Drag(PointerDriver.ElementScreen(Ui.CardElement(p.CardId)), PointerDriver.CellScreen(p.Target));
            Assert.That(M.View.Hand.Count, Is.EqualTo(hand - 1), "card played by drag");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            Assert.That(Ui.DragGhostVisible, Is.False);
        }

        /// <summary>Device report "pause → resume fixes touch": input works after pause/resume and after the quests and
        /// info panels, and the panels are mutually exclusive (UX-16).</summary>
        [UnityTest]
        public IEnumerator Panels_ExclusiveAndInputWorksAfterwards()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<MoveCommand>().Count(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos)) >= 1, "move", false, 11, 12);
            var mv = M.Legal.OfType<MoveCommand>().First(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos));
            // Info (select a unit) → quests: info closes.
            yield return drv.TapCell(OwnUnit(mv.UnitId).Pos);
            Assert.That(Ui.InfoOpen, Is.True);
            yield return drv.TapElement(Q("quest-btn"));
            Assert.That(Ui.QuestsOpen, Is.True);
            Assert.That(Ui.InfoOpen, Is.False, "info closed by the quests panel");
            // Quests → pause: quests close.
            yield return drv.TapElement(Q("pause-btn"));
            Assert.That(Ui.PauseOpen, Is.True);
            Assert.That(Ui.QuestsOpen, Is.False, "quests closed by the pause panel");
            yield return drv.TapElement(Q("pause-resume"));
            Assert.That(Ui.PauseOpen, Is.False);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            // Quests open/close by its button, then the move by taps.
            yield return drv.TapElement(Q("quest-btn"));
            yield return drv.TapElement(Q("quest-close"));
            Assert.That(Ui.QuestsOpen, Is.False);
            yield return drv.TapCell(OwnUnit(mv.UnitId).Pos);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected));
            yield return drv.TapCell(mv.Dest);
            Assert.That(OwnUnit(mv.UnitId).Pos, Is.EqualTo(mv.Dest), "moved after the panels");
        }

        /// <summary>Root cause regression: a board press whose up was lost on another touch slot must not turn every later
        /// tap into the second finger of a pinch.</summary>
        [UnityTest]
        public IEnumerator Input_LostUpOnAnotherFingerSlotDoesNotBlock()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<MoveCommand>().Any(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos)), "move", false, 11, 12);
            var mv = M.Legal.OfType<MoveCommand>().First(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos));
            // A press on another pointer id (mouse) whose release never reaches GameUi.
            var hit = Q("board-hit");
            var size = Ui.Root.layout.size;
            var s = PointerDriver.CellScreen(mv.Dest);
            var panelPos = new Vector2(s.x / Screen.width * size.x, (1f - s.y / Screen.height) * size.y);
            using (var e = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = panelPos, button = 0 }))
            {
                hit.SendEvent(e);
            }
            hit.ReleasePointer(PointerId.mousePointerId); // the UI forgets the press (lost up); only GameUi still counts it
            // The next real finger (a different slot) must tap normally.
            yield return drv.TapCell(OwnUnit(mv.UnitId).Pos);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected), "selection after a lost up");
            yield return drv.TapCell(mv.Dest);
            Assert.That(OwnUnit(mv.UnitId).Pos, Is.EqualTo(mv.Dest));
        }

        /// <summary>UX-04/UX-16: after a pinch zoom and a one-finger pan, taps still select and act; zoom survives the turn.</summary>
        [UnityTest]
        public IEnumerator Camera_ZoomPanThenSelectAndEndTurnKeepsZoom()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<MoveCommand>().Any(), "move", false, 11, 12);
            var rig = CameraRig.Instance;
            float z0 = rig.Zoom;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return drv.Pinch(centre, 80f, 220f);
            Assert.That(rig.Zoom, Is.GreaterThan(z0 * 1.2f), "pinch zoomed in");
            var camBefore = Camera.main.transform.position;
            yield return drv.Drag(centre, centre + new Vector2(-60f, -60f)); // finger down: the camera moves away from the near-edge clamp
            Assert.That(Vector3.Distance(camBefore, Camera.main.transform.position), Is.GreaterThan(0.05f), "one finger panned");
            var mv = M.Legal.OfType<MoveCommand>().FirstOrDefault(c => Tappable(c.Dest) && Tappable(OwnUnit(c.UnitId).Pos));
            if (mv != null)
            {
                yield return drv.TapCell(OwnUnit(mv.UnitId).Pos);
                Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected), "selection after zoom/pan");
                yield return drv.TapCell(mv.Dest);
                Assert.That(OwnUnit(mv.UnitId).Pos, Is.EqualTo(mv.Dest), "moved after zoom/pan");
            }
            float zoomed = rig.Zoom;
            yield return EndTurnAndWaitForAi();
            Assert.That(rig.Zoom, Is.EqualTo(zoomed).Within(0.001f), "UX-16: no reframing at the turn change");
        }

        IEnumerator EndTurnAndWaitForAi()
        {
            int turn = M.View.TurnIndex;
            yield return drv.TapElement(Q("end-btn"));
            Assert.That(M.View.TurnIndex, Is.GreaterThan(turn), "turn ended by tap " + drv.Log);
            float until = Time.realtimeSinceStartup + 40f;
            while (!M.IsOver && M.View.ActivePlayer != M.View.Viewer && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(M.IsOver || M.View.ActivePlayer == M.View.Viewer, Is.True, "the AI turn completed");
        }

        [UnityTest]
        public IEnumerator Play_EndTurnThenAiTurnCompletes()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<EndTurnCommand>().Any(), "end turn", false, 9);
            yield return EndTurnAndWaitForAi();
            yield return Settle();
            if (!M.IsOver && M.View.DrawPending)
            {
                yield return drv.TapElement(Q("blind-btn"));
                Assert.That(M.View.DrawPending, Is.False, "input works on the next own turn");
            }
        }

        // ---------- v4.0 phase 1: draw call (T-04), no draw on the opponent's turn (T-11 removed), blocked paths, safe area ----------

        static bool HasClass(string name, string cls) => Q(name).ClassListContains(cls);

        /// <summary>T-04: while the mandatory draw is pending the "Kart çek" call and the Market glow are shown; the draw clears them.</summary>
        [UnityTest]
        public IEnumerator Draw_CallVisibleThenClearedAfterDraw()
        {
            yield return FastForwardOrIgnore(v => v.Round >= 2 && v.Hand.Count < Catalog.HandLimit, "pending draw", true, true, 7, 8, 9);
            Assert.That(M.View.DrawPending, Is.True);
            Assert.That(Ui.DrawCallVisible, Is.True, "draw call on");
            Assert.That(Q("draw-call").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the call label is shown");
            Assert.That(HasClass("market-box", "call"), Is.True, "Market and blind draw highlighted");
            Assert.That(Ui.IsOn(Q("blind-btn")), Is.True);
            yield return drv.TapElement(Q("blind-btn"));
            yield return null;
            Assert.That(M.View.DrawPending, Is.False, "drawn by tap");
            Assert.That(Ui.DrawCallVisible, Is.False, "draw call cleared");
            Assert.That(Q("draw-call").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(HasClass("market-box", "call"), Is.False, "glow cleared");
        }

        /// <summary>T-11 removed (v4.0): on the opponent's turn the Market and blind draw are off and a tap changes nothing.</summary>
        [UnityTest]
        public IEnumerator Draw_MarketOffOnOpponentTurn()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<EndTurnCommand>().Any(), "end turn", false, 9, 10);
            yield return drv.TapElement(Q("end-btn"));
            Assert.That(M.View.ActivePlayer, Is.Not.EqualTo(M.View.Viewer), "the AI's turn");
            M.Paused = true; // freeze the AI loop so the check runs on the opponent's turn (pause panel not opened)
            yield return null;
            int hand = M.View.Hand.Count;
            foreach (var n in new[] { "market-0", "market-1", "market-2", "blind-btn" })
            {
                Assert.That(Ui.IsOn(Q(n)), Is.False, n + " is off on the opponent's turn");
                Assert.That(HasClass(n, "off"), Is.True, n + " looks off");
            }
            Assert.That(Ui.DrawCallVisible, Is.False);
            Assert.That(M.Legal.OfType<DrawCommand>().Any(), Is.False, "no draw command for the waiting player");
            yield return drv.TapElement(Q("blind-btn"));
            yield return drv.TapElement(Q("market-0"));
            Assert.That(M.View.Hand.Count, Is.EqualTo(hand), "a tap draws nothing");
            Assert.That(Ui.LastReason, Is.EqualTo(Strings.ReasonOpponentTurn), "the tap explains why");
            M.Paused = false;
        }

        /// <summary>Cells a unit that cannot pass units (U-07) reaches in its Move steps through Visible, empty cells
        /// (U-12), computed independently from the view. Superset of the legal moves (ignores extra terrain rules).</summary>
        static HashSet<Hex> Reach(PlayerView v, UnitView u)
        {
            int steps = Unit.DefOf(u.Class).Move + u.MoveBonus;
            var seen = new HashSet<Hex> { u.Pos };
            var frontier = new List<Hex> { u.Pos };
            for (int s = 0; s < steps; s++)
            {
                var next = new List<Hex>();
                foreach (var h in frontier)
                    for (int d = 0; d < 6; d++)
                    {
                        var n = h.Neighbor(d);
                        int i = Board.IndexOf(n);
                        if (i < 0 || seen.Contains(n)) continue;
                        var c = v.Cells[i];
                        if (c.Visibility != CellVisibility.Visible || c.Unit != null || c.HasTower
                            || c.Tile.Marker == Marker.Rock || c.Tile.Marker == Marker.Void) continue;
                        seen.Add(n);
                        next.Add(n);
                    }
                frontier = next;
            }
            return seen;
        }

        static bool Blocked(PlayerView v, UnitView u)
        {
            if (u.Class != UnitClass.Guardian && u.Class != UnitClass.Archer) return false;
            for (int d = 0; d < 6; d++)
            {
                int i = Board.IndexOf(u.Pos.Neighbor(d));
                if (i >= 0 && v.Cells[i].Unit != null && !v.Cells[i].Unit.IsGhost) return true;
            }
            return false;
        }

        /// <summary>UX-02 / U-07: a Guardian or Archer next to a unit gets no blue cell (move) or orange target
        /// (move-attack) that is only reachable by passing through the obstacle.</summary>
        [UnityTest]
        public IEnumerator Highlights_BlockedPathHasNoCellsBeyondObstacle()
        {
            System.Func<PlayerView, bool> found = v => v.OwnUnits.Any(u => Blocked(v, u) && Tappable(u.Pos)
                && M.Legal.OfType<MoveCommand>().Any(m => m.UnitId == u.Id));
            yield return FastForwardOrIgnore(found, "a blocked Guardian/Archer", false, 11, 12, 13, 14, 15, 16, 17, 18);
            var v = M.View;
            var unit = v.OwnUnits.First(u => Blocked(v, u) && Tappable(u.Pos) && M.Legal.OfType<MoveCommand>().Any(m => m.UnitId == u.Id));
            yield return drv.TapCell(unit.Pos);
            Assert.That(Ui.SelectedUnit, Is.EqualTo(unit.Id), "unit selected by tap");
            var reach = Reach(v, unit);
            int maxRange = Unit.DefOf(unit.Class).MaxRange;
            int blue = 0;
            foreach (var cell in v.PlayableCells)
            {
                var col = Ui.HighlightAt(cell);
                if (col == Gfx.MoveHl)
                {
                    blue++;
                    Assert.That(reach.Contains(cell), Is.True, "blue cell " + cell + " is only reachable through a unit");
                }
                else if (col == Gfx.MoveAttackHl || col == Gfx.MoveAttackOffHl)
                    Assert.That(reach.Any(r => Hex.Distance(r, cell) <= maxRange), Is.True,
                        "move-attack target " + cell + " needs a path through a unit");
            }
            Assert.That(blue, Is.GreaterThan(0), "the unit has highlighted moves");
            // The cell straight behind each adjacent unit (distance 2 along that direction) is never blue unless reachable around it.
            for (int d = 0; d < 6; d++)
            {
                int i = Board.IndexOf(unit.Pos.Neighbor(d));
                if (i < 0 || v.Cells[i].Unit == null) continue;
                var behind = unit.Pos.Neighbor(d).Neighbor(d);
                if (!reach.Contains(behind)) Assert.That(Ui.HighlightAt(behind), Is.Not.EqualTo(Gfx.MoveHl), "no blue cell behind the obstacle at " + behind);
            }
        }

        /// <summary>The HUD root insets by the safe area (notch / punch-hole): every HUD control stays inside a reduced
        /// safe area, keeps a >= 48 dp target (144 units), and the inset goes back when the safe area changes again.</summary>
        [UnityTest]
        public IEnumerator Hud_FitsInsideReducedSafeArea()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<EndTurnCommand>().Any(), "own turn", false, 9, 10);
            float sw = Screen.width, sh = Screen.height;
            GameUi.SafeAreaOverride = new Rect(sw * 0.07f, sh * 0.05f, sw * 0.86f, sh * 0.90f); // notch left and right, bars top/bottom
            yield return null;
            yield return Settle();
            var size = Ui.Root.layout.size;
            var safe = new Rect(0.07f * size.x, 0.05f * size.y, 0.86f * size.x, 0.90f * size.y);
            var hud = Q("hud");
            Assert.That(hud.worldBound.xMin, Is.EqualTo(safe.xMin).Within(1f), "HUD inset left");
            Assert.That(hud.worldBound.yMax, Is.EqualTo(safe.yMax).Within(1f), "HUD inset bottom");
            var names = new[] { "own-tower", "round", "turn-banner", "opp-info", "pause-btn", "quest-btn", "mana-text", "energy-text",
                "market-0", "market-1", "market-2", "blind-btn", "end-btn", "hand" };
            foreach (var n in names)
            {
                var b = Q(n).worldBound;
                Assert.That(b.width, Is.GreaterThan(0f), n + " laid out");
                Assert.That(b.xMin >= safe.xMin - 1f && b.xMax <= safe.xMax + 1f && b.yMin >= safe.yMin - 1f && b.yMax <= safe.yMax + 1f,
                    Is.True, n + " " + b + " inside the safe area " + safe);
            }
            foreach (var n in new[] { "pause-btn", "quest-btn", "opp-info", "market-0", "market-1", "market-2", "blind-btn", "end-btn" })
            {
                var b = Q(n).worldBound;
                Assert.That(Mathf.Min(b.width, b.height), Is.GreaterThanOrEqualTo(143.5f), n + " touch target >= 48 dp");
            }
            // UX-01 thumb zones: draw and End Turn in the bottom-right, quests in the bottom-left.
            foreach (var n in new[] { "end-btn", "blind-btn", "market-0" })
                Assert.That(Q(n).worldBound.center.x > size.x * 0.66f && Q(n).worldBound.center.y > size.y * 0.4f, Is.True, n + " in the right thumb zone");
            Assert.That(Q("quest-btn").worldBound.center.x < size.x * 0.34f && Q("quest-btn").worldBound.center.y > size.y * 0.4f, Is.True, "quests in the left thumb zone");
            // A safe-area change (e.g. rotating to the other landscape side) re-applies the inset.
            GameUi.SafeAreaOverride = new Rect(0f, 0f, sw, sh);
            yield return null;
            yield return null;
            Assert.That(hud.worldBound.xMin, Is.EqualTo(0f).Within(1f), "inset removed with a full safe area");
            // Buttons still work after the change.
            Assert.That(Ui.IsOn(Q("end-btn")), Is.True);
        }

        // ---------- UX-16 markers ----------

        [UnityTest]
        public IEnumerator UX16_RuneUnderUnitShowsRingAndIcon()
        {
            System.Func<PlayerView, bool> onRune = v => v.OwnUnits.Any(u => v.Cells[Board.IndexOf(u.Pos)].Tile.Marker == Marker.RuneStone
                                                                          || v.Cells[Board.IndexOf(u.Pos)].Tile.Marker == Marker.Wellspring);
            yield return FastForwardOrIgnore(onRune, "unit on a rune/wellspring", false, 11, 12, 13, 14);
            var board = Object.FindAnyObjectByType<BoardView>(FindObjectsInactive.Include);
            Assert.That(board.RingsShown, Is.GreaterThan(0), "rings on the tile edge");
            Assert.That(GameApp.Instance.Units.SpotIconsShown, Is.GreaterThan(0), "icon above the unit");
        }

        // ---------- GDD v4.0 phase 2: D-05 refresh, V-12 torch, B-15 river, U-44 Portal Bearer ----------

        static BoardView BoardV => Object.FindAnyObjectByType<BoardView>(FindObjectsInactive.Include);

        /// <summary>D-05: "Yenile (1 Mana)" by tap spends Mana and replaces the Market slots (with the slot animation).</summary>
        [UnityTest]
        public IEnumerator V40_RefreshMarketByTap()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<RefreshMarketCommand>().Any(), "market refresh", false, 3, 13, 23);
            var v0 = M.View;
            int mana = v0.Mana, refreshes = v0.MarketRefreshesThisTurn, anims = Ui.MarketRefreshAnimations;
            var before = v0.Market.Select(c => c == null ? -1 : c.Id).ToList();
            Assert.That(Ui.IsOn(Q("refresh-btn")), Is.True, "refresh button is on");
            yield return drv.TapElement(Q("refresh-btn"));
            var v = M.View;
            Assert.That(v.Mana, Is.EqualTo(mana - Catalog.MarketRefreshCost), "Mana spent " + drv.Log);
            Assert.That(v.MarketRefreshesThisTurn, Is.EqualTo(refreshes + 1));
            Assert.That(v.Market.Select(c => c == null ? -1 : c.Id).ToList(), Is.Not.EqualTo(before), "slots changed");
            Assert.That(Ui.MarketRefreshAnimations, Is.GreaterThan(anims), "slot animation played");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        /// <summary>V-12: select an own unit, tap "Meşale": a torch is on its cell and drawn.</summary>
        [UnityTest]
        public IEnumerator V40_PlaceTorchFromSelectedUnit()
        {
            yield return FastForwardOrIgnore(v => M.Legal.OfType<PlaceTorchCommand>().Any(t => Tappable(OwnUnit(t.UnitId).Pos)), "torch", false, 4, 14, 24);
            var t = M.Legal.OfType<PlaceTorchCommand>().First(c => Tappable(OwnUnit(c.UnitId).Pos));
            var cell = OwnUnit(t.UnitId).Pos;
            int torches = M.View.Torches.Count;
            yield return drv.TapCell(cell);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected), "unit selected " + drv.Log);
            Assert.That(Q("torch-btn").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "Meşale button shown");
            yield return drv.TapElement(Q("torch-btn"));
            Assert.That(M.View.Torches.Count, Is.EqualTo(torches + 1), "torch placed by tap " + drv.Log);
            Assert.That(M.View.Torches.Any(x => x.Pos == cell && x.Owner == M.Viewer), Is.True);
            Assert.That(BoardV.TorchesShown, Is.EqualTo(M.View.Torches.Count), "torch props drawn");
            Assert.That(OwnUnit(t.UnitId).TorchUsedThisTurn, Is.True);
        }

        static bool RiverVisible(PlayerView v) => v.Cells.Any(c => c.TerrainKnown && c.Tile.Marker == Marker.River);

        /// <summary>B-15: a river cell is never highlighted as a move destination, for any selected own unit.</summary>
        [UnityTest]
        public IEnumerator V40_RiverNeverMoveHighlighted()
        {
            yield return FastForwardOrIgnore(v => RiverVisible(v) && M.Legal.OfType<MoveCommand>().Any(c => Tappable(OwnUnit(c.UnitId).Pos)),
                "river + move", false, 3, 7, 11, 15, 19, 23); // TwoCorridors (seed % 4 == 3) has rivers
            var rivers = M.View.Cells.Where(c => c.TerrainKnown && c.Tile.Marker == Marker.River).Select(c => c.Cell).ToList();
            Assert.That(BoardV.RiversShown, Is.EqualTo(rivers.Count), "river tiles drawn");
            var movers = M.Legal.OfType<MoveCommand>().Select(c => c.UnitId).Distinct().Where(id => Tappable(OwnUnit(id).Pos)).ToList();
            foreach (var id in movers)
            {
                yield return drv.TapCell(OwnUnit(id).Pos);
                Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected), "unit selected " + drv.Log);
                Assert.That(Ui.HighlightCount, Is.GreaterThan(0), "moves highlighted");
                foreach (var r in rivers)
                {
                    var hl = Ui.HighlightAt(r);
                    Assert.That(hl == Gfx.MoveHl || hl == Gfx.PreviewMoveHl, Is.False, "river " + r + " highlighted as a move");
                }
                yield return drv.TapCell(OwnUnit(id).Pos); // deselect
                yield return Settle();
            }
        }

        /// <summary>U-44 / UX-14: both seats play GreedyAi (legal commands via MatchController) until A's Portal opens;
        /// the Bearer spawns (the "Taşıyıcı bekleniyor" hint follows OwnBearerPending) with its effect and the Portal glows.</summary>
        [UnityTest]
        public IEnumerator V40_BearerAppearsAfterPortalOpens()
        {
            int effects = GameApp.Instance.Units.BearerSpawnEffects;
            bool staged = false, pendingSeen = false, hintSeen = false;
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
            {
                M.NewAiMatch(seed, PlayerId.A, AiLevel.Normal, timer: false);
                var rng = new Rng(seed + 77);
                for (int i = 0; i < 6000 && !M.IsOver; i++)
                {
                    var v = M.View;
                    if (v.OwnBearerPending && v.Phase == GamePhase.Playing) { pendingSeen = true; hintSeen |= Ui.BearerHintVisible; }
                    if (v.OwnBearerId != 0 && v.OwnUnits.Any(u => u.Id == v.OwnBearerId)) { staged = true; break; }
                    if (M.AiToAct) { M.StepAi(); continue; }
                    var legal = M.Legal;
                    if (legal.Count == 0) break;
                    M.Apply(GreedyAi.Choose(v, legal, AiLevel.Normal, rng));
                }
                if (staged) break;
            }
            if (!staged) Assert.Ignore("no Portal opening for A in the sampled seeds");
            yield return Settle();
            var view = M.View;
            Assert.That(view.PortalOpen, Is.True);
            // U-44 spawns at once when the Portal opens; "pending" only happens after a death or a blocked tower side.
            if (pendingSeen) Assert.That(hintSeen, Is.True, "Taşıyıcı bekleniyor shown while pending");
            Assert.That(Ui.BearerHintVisible, Is.False, "hint gone once the Bearer is on the board");
            Assert.That(BoardV.PortalOpenShown, Is.True, "UX-14 follows PlayerView.PortalOpen");
            Assert.That(GameApp.Instance.Units.BearerSpawnEffects, Is.GreaterThan(effects), "spawn effect played");
            Assert.That(GameApp.Instance.Units.TryGetSeen(view.OwnBearerId, out var s) && s.Class == UnitClass.PortalBearer, Is.True, "Bearer drawn");
        }

        // ---------- GDD v4.0 phase 3: new classes (§3.4) ----------

        static bool IsClass(PlayerView v, int cardId, UnitClass cls)
        {
            foreach (var h in v.Hand) if (h.Id == cardId) return h.IsCharacter && h.Character.Class == cls;
            return false;
        }

        /// <summary>vs AI (human A), legal commands only: setup keeps cards of <paramref name="want"/> in the hand, the T-04
        /// draw takes the Market character slot when it holds <paramref name="want"/> (else D-05 refresh while Mana lasts,
        /// else blind), a <paramref name="want"/> card is deployed when legal, otherwise GreedyAi plays A. Stops on A's turn
        /// after the draw when <paramref name="stop"/> holds.</summary>
        static bool Drive(ulong seed, UnitClass want, System.Func<PlayerView, bool> stop, int maxSteps = 3000)
        {
            var m = M;
            m.NewAiMatch(seed, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new Rng(seed + 11);
            for (int i = 0; i < maxSteps && !m.IsOver; i++)
            {
                var v = m.View;
                if (v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && !v.DrawPending && stop(v)) return true;
                if (m.AiToAct) { m.StepAi(); continue; }
                var legal = m.Legal;
                if (legal.Count == 0) return false;
                ICommand pick = null;
                if (v.Phase != GamePhase.Playing)
                    pick = legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is PlaceTowerCommand)
                        ?? legal.OfType<PlaceUnitCommand>().FirstOrDefault(c => !IsClass(v, c.CardId, want));
                else if (v.DrawPending)
                {
                    var slot = v.Market.Count > 0 ? v.Market[0] : null;
                    if (slot != null && slot.IsCharacter && slot.Character.Class == want) pick = legal.OfType<DrawCommand>().FirstOrDefault(d => d.Slot == 0);
                    pick = pick ?? legal.OfType<RefreshMarketCommand>().FirstOrDefault()
                        ?? (ICommand)legal.OfType<DrawCommand>().FirstOrDefault(d => d.Slot == DrawCommand.Blind) ?? legal.OfType<DrawCommand>().FirstOrDefault();
                }
                else pick = legal.OfType<DeployCommand>().FirstOrDefault(d => IsClass(v, d.CardId, want));
                m.Apply(pick ?? GreedyAi.Choose(v, legal, AiLevel.Normal, rng));
            }
            return false;
        }

        IEnumerator DriveOrIgnore(UnitClass want, System.Func<PlayerView, bool> stop, string what, params ulong[] seeds)
        {
            foreach (var s in seeds)
                if (Drive(s, want, stop)) { yield return Settle(); yield break; }
            Assert.Ignore("no " + what + " in the sampled seeds");
        }

        /// <summary>U-42 / T-05: selecting an own Assassin marks the landing cell of each non-kill hit (Preview.AssassinLanding);
        /// one tap on the red target attacks; the Assassin ends on its own cell (kill) or on the landing cell.</summary>
        [UnityTest]
        public IEnumerator V40_AssassinAttackShowsLandingHint()
        {
            System.Func<AttackCommand, bool> ok = a =>
            {
                var u = M.View.OwnUnits.FirstOrDefault(x => x.Id == a.UnitId);
                if (u == null || u.Class != UnitClass.Assassin || !Tappable(u.Pos) || !Tappable(a.Target)) return false;
                var land = Preview.AssassinLanding(M.View, a.UnitId, a.Target);
                return land.HasValue && land.Value != u.Pos;
            };
            yield return DriveOrIgnore(UnitClass.Assassin, v => M.Legal.OfType<AttackCommand>().Any(ok), "Assassin attack", 1, 2, 3, 4, 5, 6, 7, 8);
            var atk = M.Legal.OfType<AttackCommand>().First(ok);
            var from = OwnUnit(atk.UnitId).Pos;
            var landing = Preview.AssassinLanding(M.View, atk.UnitId, atk.Target).Value;
            int energy = M.View.Energy;
            yield return drv.TapCell(from);
            Assert.That(Ui.SelectedUnit, Is.EqualTo(atk.UnitId), "Assassin selected " + drv.Log);
            Assert.That(Ui.HighlightAt(atk.Target), Is.EqualTo(Gfx.AttackHl), "target red");
            Assert.That(Ui.LandingHintAt(landing), Is.True, "landing hint on " + landing);
            Assert.That(Ui.LandingHintAt(from), Is.False, "no hint on its own cell");
            yield return drv.TapCell(atk.Target);
            Assert.That(M.View.Energy, Is.EqualTo(energy - 1), "attacked by tap " + drv.Log);
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            Assert.That(Ui.LandingHintCount, Is.EqualTo(0), "hints cleared after the attack");
            var after = M.View.OwnUnits.FirstOrDefault(u => u.Id == atk.UnitId);
            if (after != null) Assert.That(after.Pos == from || after.Pos == landing, Is.True, "Assassin at " + after.Pos);
            yield return Settle();
        }

        /// <summary>U-43 / B-15: an own Flyer gets a blue cell on a river and moves there by tap.</summary>
        [UnityTest]
        public IEnumerator V40_FlyerHighlightedOntoRiver()
        {
            System.Func<MoveCommand, bool> ok = mv =>
            {
                var v = M.View;
                var u = v.OwnUnits.FirstOrDefault(x => x.Id == mv.UnitId);
                return u != null && u.Class == UnitClass.Flyer && v.Cells[Board.IndexOf(mv.Dest)].Tile.Marker == Marker.River
                    && Tappable(u.Pos) && Tappable(mv.Dest);
            };
            yield return DriveOrIgnore(UnitClass.Flyer, v => M.Legal.OfType<MoveCommand>().Any(ok), "Flyer next to a river", 3, 7, 11, 15, 19, 23, 27, 31);
            var move = M.Legal.OfType<MoveCommand>().First(ok);
            yield return drv.TapCell(OwnUnit(move.UnitId).Pos);
            Assert.That(Ui.SelectedUnit, Is.EqualTo(move.UnitId), "Flyer selected " + drv.Log);
            Assert.That(Ui.HighlightAt(move.Dest), Is.EqualTo(Gfx.MoveHl), "river cell is blue for the Flyer");
            yield return drv.TapCell(move.Dest);
            Assert.That(OwnUnit(move.UnitId).Pos, Is.EqualTo(move.Dest), "Flyer on the river");
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        /// <summary>T-07 for each §3.4 card class: tap the card, then a legal cell; the unit of that class is on the board.</summary>
        [UnityTest]
        public IEnumerator V40_DeployEachNewClassByTap()
        {
            var missing = new List<UnitClass>();
            foreach (var cls in new[] { UnitClass.Gatherer, UnitClass.Besieger, UnitClass.Assassin, UnitClass.Flyer })
            {
                System.Func<DeployCommand, bool> ok = d => IsClass(M.View, d.CardId, cls) && Tappable(d.Cell);
                bool found = false;
                foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
                    if (Drive(seed, cls, v => M.Legal.OfType<DeployCommand>().Any(ok))) { found = true; break; }
                if (!found) { missing.Add(cls); continue; }
                yield return Settle();
                var dep = M.Legal.OfType<DeployCommand>().First(ok);
                int units = M.View.OwnUnits.Count;
                yield return drv.TapElement(Ui.CardElement(dep.CardId));
                Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.CardSelected), cls + " card selected " + drv.Log);
                yield return Settle();
                Assert.That(Tappable(dep.Cell), Is.True, "deploy cell not under the hand");
                yield return drv.TapCell(dep.Cell);
                Assert.That(M.View.OwnUnits.Count, Is.EqualTo(units + 1), cls + " deployed by tap " + drv.Log);
                Assert.That(M.View.OwnUnits.Any(u => u.Class == cls && u.Pos == dep.Cell), Is.True, cls + " on " + dep.Cell);
                Assert.That(GameApp.Instance.Units.TryGetSeen(M.View.OwnUnits.First(u => u.Pos == dep.Cell).Id, out var seen) && seen.Class == cls, Is.True, cls + " drawn");
                Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
                M.Close();
                yield return null;
            }
            Assert.That(missing, Is.Empty, "no deployable card in the sampled seeds for " + string.Join(", ", missing));
        }
    }
}
