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
            Assert.That(Ui.Current, Is.EqualTo(GameUi.Screen.Passive), "quests confirmed by tap");
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
            Assert.That(Ui.Mode, Is.EqualTo(GameUi.InputMode.CardSelected));
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
    }
}
