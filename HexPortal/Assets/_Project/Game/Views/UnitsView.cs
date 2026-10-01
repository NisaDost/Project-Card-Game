using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexPortal.Game
{
    /// <summary>Units, towers and ghosts (UX-02, UX-09) from a PlayerView, plus event-driven tweens and floating
    /// numbers from filtered events. Visuals and labels are pooled; LateUpdate does not allocate.</summary>
    public sealed class UnitsView : MonoBehaviour
    {
        const float MoveSeconds = 0.35f;
        const float FloatSeconds = 1.1f;

        sealed class Visual
        {
            public int Key;
            public bool Used;
            public GameObject Root;
            public GameObject Body;
            public UnitClass BodyClass = (UnitClass)(-1);
            public bool IsTower;
            public Renderer BodyRenderer, Rim;
            public GameObject Watch;
            public Label Label;
            public Vector3 From, To;
            public float T = 1f;
        }

        sealed class Floater
        {
            public Label Label;
            public Vector3 World;
            public float Age = FloatSeconds;
        }

        readonly List<Visual> visuals = new List<Visual>();
        readonly List<Floater> floaters = new List<Floater>();
        VisualElement overlay;
        Camera cam;
        BoardView board;
        PlayerView view;

        public static UnitsView Create(Transform parent, VisualElement overlay, Camera cam, BoardView board)
        {
            var go = new GameObject("Units");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<UnitsView>();
            v.overlay = overlay;
            v.cam = cam;
            v.board = board;
            return v;
        }

        static int TowerKey(PlayerId p) => p == PlayerId.A ? -1 : -2;

        public void Render(PlayerView v)
        {
            view = v;
            foreach (var vis in visuals) vis.Used = false;
            foreach (var c in v.Cells)
            {
                if (c.Unit != null) ShowUnit(c.Unit, v);
                if (c.HasTower) ShowTower(c, v);
            }
            foreach (var vis in visuals)
                if (!vis.Used && vis.Root.activeSelf)
                {
                    vis.Root.SetActive(false);
                    vis.Label.style.display = DisplayStyle.None;
                }
        }

        public void Hide()
        {
            foreach (var vis in visuals)
            {
                vis.Root.SetActive(false);
                vis.Label.style.display = DisplayStyle.None;
            }
            foreach (var f in floaters) { f.Age = FloatSeconds; f.Label.style.display = DisplayStyle.None; }
        }

        void ShowUnit(UnitView u, PlayerView v)
        {
            var vis = Get(u.Id);
            vis.IsTower = false;
            if (vis.BodyClass != u.Class) BuildBody(vis, u.Class);
            Place(vis, u.Pos);
            bool own = u.Owner == v.Viewer;
            bool dim = own && u.ActedThisTurn && v.ActivePlayer == v.Viewer && v.Phase == GamePhase.Playing;
            var body = Gfx.BiomeColor(u.Biome);
            var rim = Gfx.OwnerColor(u.Owner);
            if (dim) body *= 0.45f; // UX-02: acted units look faded
            SetLook(vis, body, rim, u.IsGhost);
            vis.Watch.SetActive(u.OnOverwatch && !u.IsGhost);
            string fx = (u.BuffId != null ? " ▲" + Strings.Name(u.BuffId) : "") + (u.DebuffId != null ? " ▼" + Strings.Name(u.DebuffId) : "")
                + (u.OnOverwatch ? " ⌖" : "");
            vis.Label.text = Strings.ClassLetter(u.Class) + " " + u.Health + fx;
            vis.Label.EnableInClassList("ghost", u.IsGhost);
            vis.Label.style.borderBottomColor = rim;
        }

        void ShowTower(CellView c, PlayerView v)
        {
            var vis = Get(TowerKey(c.TowerOwner));
            if (!vis.IsTower || vis.Body == null)
            {
                if (vis.Body != null) Destroy(vis.Body);
                vis.Body = Gfx.Primitive(PrimitiveType.Cylinder, "Tower", vis.Root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
                vis.BodyRenderer = vis.Body.GetComponent<Renderer>();
                vis.BodyClass = (UnitClass)(-1);
                vis.IsTower = true;
            }
            Place(vis, c.Cell);
            SetLook(vis, Color.Lerp(Gfx.OwnerColor(c.TowerOwner), Color.white, 0.35f), Gfx.OwnerColor(c.TowerOwner), c.TowerIsGhost);
            vis.Watch.SetActive(false);
            vis.Label.text = Strings.Tower + " " + c.TowerHealth;
            vis.Label.EnableInClassList("ghost", c.TowerIsGhost);
        }

        void Place(Visual vis, Hex h)
        {
            vis.Used = true;
            vis.To = HexLayout.ToWorld(h, BoardView.TileHeight);
            if (!vis.Root.activeSelf) { vis.Root.SetActive(true); vis.T = 1f; }
            if (vis.T >= 1f) vis.Root.transform.localPosition = vis.To;
            vis.Label.style.display = DisplayStyle.Flex;
        }

        static void SetLook(Visual vis, Color body, Color rim, bool ghost)
        {
            var mat = ghost ? Gfx.Transparent : Gfx.Opaque;
            vis.BodyRenderer.sharedMaterial = mat;
            vis.Rim.sharedMaterial = mat;
            if (ghost) { body.a = 0.35f; rim.a = 0.35f; }
            Gfx.SetColor(vis.BodyRenderer, body);
            Gfx.SetColor(vis.Rim, rim);
        }

        Visual Get(int key)
        {
            foreach (var v in visuals)
                if (v.Key == key) return v;
            var vis = new Visual { Key = key };
            vis.Root = new GameObject("Piece " + key);
            vis.Root.transform.SetParent(transform, false);
            var rim = Gfx.Primitive(PrimitiveType.Cylinder, "Rim", vis.Root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.25f, 0.05f, 1.25f));
            vis.Rim = rim.GetComponent<Renderer>();
            vis.Watch = Gfx.Primitive(PrimitiveType.Sphere, "Overwatch", vis.Root.transform, new Vector3(0f, 1.7f, 0f), new Vector3(0.3f, 0.3f, 0.3f));
            Gfx.SetColor(vis.Watch.GetComponent<Renderer>(), new Color(1f, 0.9f, 0.1f));
            vis.Label = new Label { pickingMode = PickingMode.Ignore };
            vis.Label.AddToClassList("unit-label");
            vis.Label.style.borderBottomWidth = 4;
            overlay.Add(vis.Label);
            visuals.Add(vis);
            return vis;
        }

        static void BuildBody(Visual vis, UnitClass cls)
        {
            if (vis.Body != null) Destroy(vis.Body);
            PrimitiveType type; Vector3 scale; float y;
            switch (cls)
            {
                case UnitClass.Guardian: type = PrimitiveType.Cube; scale = new Vector3(0.8f, 0.8f, 0.8f); y = 0.45f; break;
                case UnitClass.Rider: type = PrimitiveType.Capsule; scale = new Vector3(0.55f, 0.6f, 0.9f); y = 0.6f; break;
                case UnitClass.Archer: type = PrimitiveType.Cylinder; scale = new Vector3(0.4f, 0.7f, 0.4f); y = 0.7f; break;
                case UnitClass.Mage: type = PrimitiveType.Capsule; scale = new Vector3(0.5f, 0.75f, 0.5f); y = 0.75f; break;
                default: type = PrimitiveType.Sphere; scale = new Vector3(0.7f, 0.7f, 0.7f); y = 0.4f; break;
            }
            vis.Body = Gfx.Primitive(type, cls.ToString(), vis.Root.transform, new Vector3(0f, y, 0f), scale);
            if (cls == UnitClass.Rider) vis.Body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vis.BodyRenderer = vis.Body.GetComponent<Renderer>();
            vis.BodyClass = cls;
            vis.IsTower = false;
        }

        // ---------- Event playback (filtered events only) ----------

        public void Play(IReadOnlyList<GameEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case UnitMoved m: Tween(m.UnitId, m.From); break;
                    case UnitPushed p: if (p.From != p.To) Tween(p.UnitId, p.From); break;
                    case UnitTeleported t: Tween(t.UnitId, t.From); break;
                    case DamageDealt d: Float(d.Target, "-" + d.Amount, new Color(1f, 0.3f, 0.25f)); break;
                    case UnitHealed h: FloatAtUnit(h.UnitId, "+" + h.Amount, new Color(0.3f, 1f, 0.4f)); break;
                    case ShieldBlocked s: FloatAtUnit(s.UnitId, "Kalkan", Color.white); break;
                    case UnitDied u: FloatAtUnit(u.UnitId, "✖", new Color(1f, 0.1f, 0.1f)); break;
                    case TrapTriggered tr: board.Flash(tr.Cell); Float(tr.Cell, "Tuzak!", new Color(0.8f, 0.4f, 1f)); break;
                    case TowerShot ts: FloatAtUnit(ts.TargetUnitId, "Kule atışı", new Color(1f, 0.85f, 0.2f)); break;
                    case OverwatchFired of: FloatAtUnit(of.TargetUnitId, "Nöbet!", new Color(1f, 0.85f, 0.2f)); break;
                }
            }
        }

        void Tween(int unitId, Hex from)
        {
            foreach (var v in visuals)
                if (v.Key == unitId && v.Root.activeSelf)
                {
                    v.From = HexLayout.ToWorld(from, BoardView.TileHeight);
                    v.Root.transform.localPosition = v.From;
                    v.T = 0f;
                }
        }

        void FloatAtUnit(int unitId, string text, Color color)
        {
            if (view == null) return;
            foreach (var c in view.Cells)
                if (c.Unit != null && c.Unit.Id == unitId) { Float(c.Cell, text, color); return; }
        }

        void Float(Hex cell, string text, Color color)
        {
            Floater f = null;
            foreach (var x in floaters)
                if (x.Age >= FloatSeconds) { f = x; break; }
            if (f == null)
            {
                f = new Floater { Label = new Label { pickingMode = PickingMode.Ignore } };
                f.Label.AddToClassList("float-label");
                overlay.Add(f.Label);
                floaters.Add(f);
            }
            f.World = HexLayout.ToWorld(cell, BoardView.TileHeight + 1.2f);
            f.Age = 0f;
            f.Label.text = text;
            f.Label.style.color = color;
            f.Label.style.display = DisplayStyle.Flex;
        }

        void LateUpdate()
        {
            if (overlay == null || overlay.panel == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < visuals.Count; i++)
            {
                var v = visuals[i];
                if (!v.Root.activeSelf) continue;
                if (v.T < 1f)
                {
                    v.T = Mathf.Min(1f, v.T + dt / MoveSeconds);
                    v.Root.transform.localPosition = Vector3.Lerp(v.From, v.To, Mathf.SmoothStep(0f, 1f, v.T));
                }
                PlaceLabel(v.Label, v.Root.transform.position + new Vector3(0f, v.IsTower ? 2.1f : 1.5f, 0f));
            }
            for (int i = 0; i < floaters.Count; i++)
            {
                var f = floaters[i];
                if (f.Age >= FloatSeconds) continue;
                f.Age += dt;
                if (f.Age >= FloatSeconds) { f.Label.style.display = DisplayStyle.None; continue; }
                PlaceLabel(f.Label, f.World + new Vector3(0f, f.Age * 1.2f, 0f));
            }
        }

        void PlaceLabel(Label label, Vector3 world)
        {
            var s = cam.WorldToScreenPoint(world);
            var p = RuntimePanelUtils.ScreenToPanel(overlay.panel, new Vector2(s.x, Screen.height - s.y));
            label.style.left = p.x;
            label.style.top = p.y;
        }
    }
}
