using System.Collections;
using System.Linq;
using System.Reflection;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HexPortal.Game.Tests
{
    /// <summary>v3.3 bug "sometimes units can't be deployed or moved until Turu Bitir": the client input state returns to
    /// Idle after every command, cancel and refresh, and a lost PointerUp never blocks later input.</summary>
    public class InputStateTests
    {
        static GameApp App()
        {
            if (GameApp.Instance == null) GameApp.Create();
            return GameApp.Instance;
        }

        static MatchController SetupWithTower(GameApp app, ulong seed)
        {
            var m = app.Match;
            m.NewMatch(seed, timer: false);
            m.AcknowledgeHandoff();
            m.Apply(m.Legal.First(c => c is ChooseQuestsCommand));
            m.Apply(m.Legal.First(c => c is ChoosePassiveCommand));
            m.Apply(m.Legal.First(c => c is PlaceTowerCommand));
            return m;
        }

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

        static void Deploy(GameApp app, MatchController m)
        {
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            int units = m.View.OwnUnits.Count;
            app.Ui.TapCard(cmd.CardId);
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.CardSelected));
            app.Ui.TapCell(cmd.Cell);
            Assert.That(m.View.OwnUnits.Count, Is.EqualTo(units + 1), "deployed");
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle), "Idle after the command");
        }

        static void MoveOne(GameApp app, MatchController m, int unitId)
        {
            var mv = m.Legal.OfType<MoveCommand>().First(c => c.UnitId == unitId);
            app.Ui.TapCell(m.View.OwnUnits.First(u => u.Id == unitId).Pos);
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.UnitSelected));
            app.Ui.TapCell(mv.Dest);
            Assert.That(m.View.OwnUnits.First(u => u.Id == unitId).Pos, Is.EqualTo(mv.Dest), "moved");
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Input_ConsecutiveDeploys()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 7);
            Deploy(app, m);
            Deploy(app, m);
        }

        /// <summary>Move, then another unit's move at once (the first unit's tween is still running: no lock).</summary>
        [UnityTest]
        public IEnumerator Input_MoveThenAnotherUnitMoveDuringAnimation()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            Assert.That(PlayUntil(m, v => m.Legal.OfType<MoveCommand>().Select(c => c.UnitId).Distinct().Count() >= 2, 11), Is.True);
            yield return null;
            var ids = m.Legal.OfType<MoveCommand>().Select(c => c.UnitId).Distinct().Take(2).ToList();
            MoveOne(app, m, ids[0]);
            MoveOne(app, m, ids[1]); // same frame: the hop of the first unit is still playing
        }

        [UnityTest]
        public IEnumerator Input_MoveAttack()
        {
            var app = App();
            yield return null;
            var m = app.Match;
            bool found = false;
            foreach (ulong seed in new ulong[] { 3, 13, 23, 33 })
                if (PlayUntil(m, v => m.Legal.OfType<MoveAttackCommand>().Any(c => !m.Legal.OfType<AttackCommand>().Any(a => a.UnitId == c.UnitId && a.Target == c.Target)), seed)) { found = true; break; }
            if (!found) Assert.Ignore("no move-attack in the sampled seeds");
            yield return null;
            var ma = m.Legal.OfType<MoveAttackCommand>().First(c => !m.Legal.OfType<AttackCommand>().Any(a => a.UnitId == c.UnitId && a.Target == c.Target));
            int energy = m.View.Energy;
            app.Ui.TapCell(m.View.OwnUnits.First(u => u.Id == ma.UnitId).Pos);
            app.Ui.TapCell(ma.Target);
            Assert.That(m.View.Energy, Is.EqualTo(energy - 2), "move-attack applied");
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Input_DragCancelThenDeploy()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 5);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            var card = app.Ui.CardElement(cmd.CardId);
            var start = card.worldBound.center;
            var to = PanelPos(app, cmd.Cell);
            Send(card, EventType.MouseDown, start);
            Send(BoardHit(app), EventType.MouseDrag, to);
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Dragging));
            using (var e = PointerCancelEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = to }))
                BoardHit(app).SendEvent(e);
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            Deploy(app, m);
        }

        /// <summary>Root cause: a press whose PointerUp never arrives must not block the next taps.</summary>
        [UnityTest]
        public IEnumerator Input_LostPointerUpDoesNotBlock()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 9);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            var card = app.Ui.CardElement(cmd.CardId);
            // A card press that never ends (lost up)...
            Send(card, EventType.MouseDown, card.worldBound.center);
            Assert.That(app.Ui.CardPointerActive, Is.True);
            // ...then the next press on the board heals it.
            var far = PanelPos(app, m.View.OwnTowerPos.Value);
            Send(BoardHit(app), EventType.MouseDown, far);
            Assert.That(app.Ui.CardPointerActive, Is.False, "stale card press dropped");
            // A board press that never ends either (lost up), then a normal deploy by real taps.
            app.Ui.TapCard(cmd.CardId);
            var to = PanelPos(app, cmd.Cell);
            Send(BoardHit(app), EventType.MouseDown, to);
            Send(BoardHit(app), EventType.MouseUp, to);
            Assert.That(m.View.OwnUnits.Count, Is.EqualTo(1), "the tap deployed although an earlier press never ended");
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
        }

        [UnityTest]
        public IEnumerator Input_ConfirmCancelThenAct()
        {
            var app = App();
            yield return null;
            var m = SetupWithTower(app, 15);
            yield return null;
            var cmd = m.Legal.OfType<PlaceUnitCommand>().First();
            app.Ui.TapCard(cmd.CardId);
            typeof(GameUi).GetMethod("AskConfirm", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(app.Ui, new object[] { "test", (System.Action)(() => { }) });
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Confirm));
            var cancel = app.Ui.Root.Q<Button>("confirm-cancel");
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = cancel; cancel.SendEvent(e); }
            Assert.That(app.Ui.Mode, Is.EqualTo(GameUi.InputMode.Idle));
            Deploy(app, m);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
