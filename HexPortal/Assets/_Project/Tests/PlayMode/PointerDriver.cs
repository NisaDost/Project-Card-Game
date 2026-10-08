using System.Collections;
using HexPortal.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using ITouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace HexPortal.Game.Tests
{
    /// <summary>
    /// Drives the REAL UI through the Input System: a test Touchscreen device queues touch state events, which go
    /// through the project-wide UI actions, the InputForUI provider and UI Toolkit's event dispatch exactly like a
    /// finger on a phone. No direct calls into GameUi handlers. Editor focus is ignored while the driver is active.
    /// </summary>
    public sealed class PointerDriver : System.IDisposable
    {
        // One device for the whole play session: a removed + re-added touchscreen is not picked up again by the UI
        // actions of the InputForUI provider, so every later test would get no pointer events at all.
        static Touchscreen shared;
        readonly Touchscreen screen;
        readonly InputSettings.BackgroundBehavior oldBackground;
#if UNITY_EDITOR
        readonly InputSettings.EditorInputBehaviorInPlayMode oldEditor;
#endif
        static int nextTouchId = 1; // never reused across tests (the device lives for the whole session)

        public PointerDriver()
        {
            // Runtime settings object (the project has no input settings asset); values are restored on Dispose.
            var settings = InputSystem.settings;
            oldBackground = settings.backgroundBehavior;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            if (shared == null || !shared.added) shared = InputSystem.AddDevice<Touchscreen>("TestTouchscreen");
            screen = shared;
            var root = GameApp.Instance.Ui.Root;
            root.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
        }

        public void Dispose()
        {
            var root = GameApp.Instance != null ? GameApp.Instance.Ui.Root : null;
            root?.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            var settings = InputSystem.settings;
            if (settings == null) return;
            settings.backgroundBehavior = oldBackground;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = oldEditor;
#endif
        }

        /// <summary>Pointer downs/ups seen by the UI (for failure messages).</summary>
        public readonly System.Text.StringBuilder Log = new System.Text.StringBuilder();
        void OnDown(PointerDownEvent e) => Log.Append("D").Append(e.pointerId).Append(e.position.ToString("F0")).Append("->").Append((e.target as VisualElement)?.name).Append(' ');
        void OnUp(PointerUpEvent e) => Log.Append("U").Append(e.pointerId).Append(' ');

        static VisualElement Root => GameApp.Instance.Ui.Root;

        /// <summary>Panel position (1920x1080-scaled UI units, origin top-left) to screen pixels (origin bottom-left).</summary>
        public static Vector2 PanelToScreen(Vector2 p)
        {
            var size = Root.layout.size;
            return new Vector2(p.x / size.x * Screen.width, (1f - p.y / size.y) * Screen.height);
        }

        public static Vector2 CellScreen(Hex cell)
        {
            var s = Camera.main.WorldToScreenPoint(HexLayout.ToWorld(cell, BoardView.TileHeight));
            return new Vector2(s.x, s.y);
        }

        public static Vector2 ElementScreen(VisualElement el) => PanelToScreen(el.worldBound.center);

        void Queue(int id, ITouchPhase phase, Vector2 pos, int finger = 0)
        {
            Log.Append(phase.ToString()[0]).Append(pos.ToString("F0")).Append(' ');
            InputSystem.QueueStateEvent(screen, new TouchState
            {
                touchId = id,
                phase = phase,
                position = pos,
                pressure = phase == ITouchPhase.Ended || phase == ITouchPhase.Canceled ? 0f : 1f,
            });
        }

        public int Down(Vector2 screenPos) { int id = nextTouchId++; Queue(id, ITouchPhase.Began, screenPos); return id; }
        public void Move(int id, Vector2 screenPos) => Queue(id, ITouchPhase.Moved, screenPos);
        public void Up(int id, Vector2 screenPos) => Queue(id, ITouchPhase.Ended, screenPos);

        /// <summary>A finger tap: down, two frames, up, two frames (the UI handles each phase on its own frame).</summary>
        public IEnumerator Tap(Vector2 screenPos)
        {
            int id = Down(screenPos);
            yield return null; yield return null;
            Up(id, screenPos);
            yield return null; yield return null;
            // A spacer so two taps in a row are never read as a double tap (board double tap = reset view).
            yield return new WaitForSecondsRealtime(0.36f);
        }

        public IEnumerator TapCell(Hex cell) => Tap(CellScreen(cell));
        public IEnumerator TapElement(VisualElement el) => Tap(ElementScreen(el));

        /// <summary>Press at <paramref name="from"/>, move in steps, release at <paramref name="to"/>.</summary>
        public IEnumerator Drag(Vector2 from, Vector2 to, int steps = 6)
        {
            int id = Down(from);
            yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                Move(id, Vector2.Lerp(from, to, i / (float)steps));
                yield return null;
            }
            yield return null;
            Up(id, to);
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(0.36f);
        }

        /// <summary>Two-finger pinch around a centre (UX-04 zoom). spread &gt; 0 zooms in.</summary>
        public IEnumerator Pinch(Vector2 centre, float startHalf, float endHalf, int steps = 6)
        {
            var dir = Vector2.right;
            int a = nextTouchId++, b = nextTouchId++;
            Queue(a, ITouchPhase.Began, centre - dir * startHalf);
            yield return null;
            Queue(b, ITouchPhase.Began, centre + dir * startHalf);
            yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                float h = Mathf.Lerp(startHalf, endHalf, i / (float)steps);
                Queue(a, ITouchPhase.Moved, centre - dir * h);
                Queue(b, ITouchPhase.Moved, centre + dir * h);
                yield return null;
            }
            Queue(b, ITouchPhase.Ended, centre + dir * endHalf);
            yield return null;
            Queue(a, ITouchPhase.Ended, centre - dir * endHalf);
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(0.36f);
        }
    }
}
