using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexPortal.Game
{
    /// <summary>Units, towers and ghosts (UX-02, UX-09) from a PlayerView, plus event-driven tweens, floating numbers,
    /// death bursts and cell flashes from filtered events (UX-10). Piece visuals are pooled and reused across unit ids
    /// (a fixed set of GameObjects per piece, never destroyed); floaters and bursts are pooled; LateUpdate does not
    /// allocate.</summary>
    public sealed class UnitsView : MonoBehaviour
    {
        const float MoveSeconds = 0.25f;
        const float LungeSeconds = 0.2f, ShakeSeconds = 0.2f, DieSeconds = 0.3f, PopSeconds = 0.2f;
        const float CardFlySeconds = 0.35f, BoltSeconds = 0.15f;
        const float FloatSeconds = 1.4f;
        const float BurstSeconds = 0.6f;
        const int TowerKeyA = -1, TowerKeyB = -2, NoKey = int.MinValue;

        /// <summary>GameObjects per pooled piece: root, body, rim, overwatch marker.</summary>
        public const int ObjectsPerPiece = 4;

        sealed class Visual
        {
            public int Key = NoKey;
            public bool Used;
            public GameObject Root, Body, Watch;
            public MeshFilter BodyMesh;
            public int BodyKind = -1; // UnitClass, or 100 for a tower
            public bool IsTower;
            public Renderer BodyRenderer, Rim;
            public Vector3 From, To;
            public float T = 1f;
            // M7 tweens: 1 = idle, negative = waiting (delay). Purely visual; finished at once when new events arrive.
            public float LungeT = 1f, ShakeT = 1f, DieT = 1f, PopT = 1f;
            public Vector3 LungeDir;
            public bool Animating;
            public bool Dying => DieT < 1f;
            // Overlay tag (UX "stat bars"): big Health bar + number, small Attack chip, buff/debuff/overwatch chips.
            public VisualElement Tag, HpFill;
            public Label Hp, Atk, Range, Buff, Debuff, Watching, Spot;
            public string ShownSpot;
            public int ShownRangeClass = -1;
            public int ShownHp = -1, ShownMax = -1, ShownAtk = -1, ShownBuffTurns = -1, ShownDebuffTurns = -1;
            public string ShownBuff, ShownDebuff;
        }

        sealed class Floater
        {
            public Label Label;
            public Vector3 World;
            public float Age = FloatSeconds;
        }

        sealed class Burst
        {
            public GameObject Go;
            public Renderer R;
            public float Age = BurstSeconds;
        }

        /// <summary>Last-seen identity of units the viewer has seen (for death notices and the event log).</summary>
        public struct Seen
        {
            public UnitClass Class;
            public PlayerId Owner;
            public Hex Pos;
        }

        readonly List<Visual> visuals = new List<Visual>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Burst> bursts = new List<Burst>();
        readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>();
        readonly List<Fx> fxs = new List<Fx>();
        readonly List<int> pendingPops = new List<int>();

        /// <summary>Pooled overlay effect (UI only, no GameObjects): a card flying to its target cell, or a shot bolt.</summary>
        sealed class Fx
        {
            public VisualElement El;
            public bool Card;
            public Vector2 FromPanel;
            public Vector3 From, To;
            public float Age, Dur;
            public bool Active;
        }

        public int FxPoolSize => fxs.Count;
        VisualElement overlay, tagLayer;
        bool barsHidden;
        Camera cam;
        BoardView board;
        PlayerView view;

        public int PoolSize => visuals.Count;
        public int ShownCount { get; private set; }
        public int FloaterPoolSize => floaters.Count;
        public int BurstPoolSize => bursts.Count;
        /// <summary>UX-12: true while a card or unit is selected (stat bars hidden).</summary>
        public bool BarsHidden => barsHidden;

        /// <summary>UX-12 selection mode: hide every stat bar so tiles and highlights read clearly; restore on deselect.</summary>
        public void SetBarsHidden(bool hidden)
        {
            if (hidden == barsHidden) return;
            barsHidden = hidden;
            tagLayer.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public static UnitsView Create(Transform parent, VisualElement overlay, Camera cam, BoardView board)
        {
            var go = new GameObject("Units");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<UnitsView>();
            v.overlay = overlay;
            // Stat bars live in their own layer so UX-12 selection mode hides them all with one style change.
            v.tagLayer = new VisualElement { name = "unit-tags", pickingMode = PickingMode.Ignore };
            v.tagLayer.AddToClassList("fill");
            overlay.Add(v.tagLayer);
            v.cam = cam;
            v.board = board;
            return v;
        }

        static int TowerKey(PlayerId p) => p == PlayerId.A ? TowerKeyA : TowerKeyB;

        public bool TryGetSeen(int unitId, out Seen s) => seen.TryGetValue(unitId, out s);
        /// <summary>U-44 spawn effects played (tests).</summary>
        public int BearerSpawnEffects { get; private set; }

        public void Render(PlayerView v)
        {
            view = v;
            foreach (var vis in visuals) vis.Used = false;
            // Pass 1: keep the visual that already shows a key. Pass 2: new keys take a free pooled visual.
            foreach (var c in v.Cells)
            {
                if (c.Unit != null) Claim(c.Unit.Id);
                if (c.HasTower) Claim(TowerKey(c.TowerOwner));
            }
            int shown = 0;
            foreach (var c in v.Cells)
            {
                if (c.Unit != null)
                {
                    ShowUnit(c.Unit, v, c.TerrainKnown ? c.Tile.Marker : Marker.None);
                    shown++;
                    if (!c.Unit.IsGhost) seen[c.Unit.Id] = new Seen { Class = c.Unit.Class, Owner = c.Unit.Owner, Pos = c.Unit.Pos };
                }
                if (c.HasTower) { ShowTower(c); shown++; }
            }
            ShownCount = shown;
            foreach (var vis in visuals)
                if (!vis.Used && vis.Root.activeSelf)
                {
                    vis.Tag.style.display = DisplayStyle.None;
                    vis.Key = NoKey;
                    if (!vis.Dying) vis.Root.SetActive(false); // a dying piece stays until its death tween ends
                }
            pendingPops.Clear();
        }

        void Claim(int key)
        {
            foreach (var vis in visuals)
                if (vis.Key == key) { vis.Used = true; return; }
        }

        public void Hide()
        {
            foreach (var vis in visuals)
            {
                vis.Root.SetActive(false);
                vis.Tag.style.display = DisplayStyle.None;
                vis.Key = NoKey;
                vis.Used = false;
                ResetAnim(vis);
            }
            pendingPops.Clear();
            foreach (var f in fxs) { f.Active = false; f.El.style.display = DisplayStyle.None; }
            foreach (var f in floaters) { f.Age = FloatSeconds; f.Label.style.display = DisplayStyle.None; }
            foreach (var b in bursts) { b.Age = BurstSeconds; b.Go.SetActive(false); }
        }

        void ShowUnit(UnitView u, PlayerView v, Marker spot)
        {
            var vis = Get(u.Id);
            vis.IsTower = false;
            if (vis.BodyKind != (int)u.Class) SetBody(vis, (int)u.Class);
            Place(vis, u.Pos);
            if (pendingPops.Contains(u.Id)) { vis.PopT = 0f; vis.Animating = true; vis.Root.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); }
            bool own = u.Owner == v.Viewer;
            bool dim = own && u.ActedThisTurn && v.ActivePlayer == v.Viewer && v.Phase == GamePhase.Playing;
            var body = Gfx.BiomeColor(u.Biome);
            var rim = Gfx.OwnerColor(u.Owner);
            if (dim) body *= 0.45f; // UX-02: acted units look faded
            SetLook(vis, body, rim, u.IsGhost);
            vis.Watch.SetActive(u.OnOverwatch && !u.IsGhost);
            var def = Unit.DefOf(u.Class);
            if (vis.ShownRangeClass != (int)u.Class) // UX-13: compact range chip, rebuilt only when the class changes
            {
                vis.ShownRangeClass = (int)u.Class;
                vis.Range.text = Strings.RangeChip(def.Range, def.MinRange, def.MaxRange);
            }
            vis.Range.style.display = DisplayStyle.Flex;
            SetSpot(vis, ArtCatalog.MarkerOf(spot).Icon, spot);
            SetTag(vis, u.Health, def.Health, u.EffectiveAttack, u.IsGhost ? null : u.BuffId, u.BuffTurnsLeft,
                u.IsGhost ? null : u.DebuffId, u.DebuffTurnsLeft, u.OnOverwatch && !u.IsGhost, rim, u.IsGhost, dim);
        }

        void ShowTower(CellView c)
        {
            var vis = Get(TowerKey(c.TowerOwner));
            if (!vis.IsTower) { SetBody(vis, 100); vis.IsTower = true; }
            Place(vis, c.Cell);
            SetLook(vis, Color.Lerp(Gfx.OwnerColor(c.TowerOwner), Color.white, 0.35f), Gfx.OwnerColor(c.TowerOwner), c.TowerIsGhost);
            SetSpot(vis, "", Marker.None);
            vis.Range.style.display = DisplayStyle.None;
            vis.ShownRangeClass = -1;
            vis.Watch.SetActive(false);
            SetTag(vis, c.TowerHealth, Mathf.Max(c.TowerHealth, Catalog.Tower.Health), -1, null, 0, null, 0, false,
                Gfx.OwnerColor(c.TowerOwner), c.TowerIsGhost, false);
        }

        /// <summary>Updates the overlay tag; strings are only rebuilt when a shown value changes.</summary>
        static void SetTag(Visual vis, int hp, int max, int atk, string buff, int buffTurns, string debuff, int debuffTurns,
            bool watch, Color owner, bool ghost, bool dim)
        {
            if (hp != vis.ShownHp || max != vis.ShownMax)
            {
                vis.ShownHp = hp; vis.ShownMax = max;
                vis.Hp.text = hp.ToString();
                float f = max <= 0 ? 0f : Mathf.Clamp01((float)hp / max);
                vis.HpFill.style.width = Length.Percent(f * 100f);
                vis.HpFill.style.backgroundColor = f > 0.6f ? new Color(0.25f, 0.85f, 0.3f) : f > 0.3f ? new Color(1f, 0.75f, 0.1f) : new Color(1f, 0.2f, 0.15f);
            }
            if (atk != vis.ShownAtk)
            {
                vis.ShownAtk = atk;
                vis.Atk.style.display = atk < 0 ? DisplayStyle.None : DisplayStyle.Flex;
                if (atk >= 0) vis.Atk.text = Strings.AttackIcon + atk;
            }
            if (buff != vis.ShownBuff || buffTurns != vis.ShownBuffTurns)
            {
                vis.ShownBuff = buff; vis.ShownBuffTurns = buffTurns;
                vis.Buff.style.display = buff == null ? DisplayStyle.None : DisplayStyle.Flex;
                if (buff != null) vis.Buff.text = ArtCatalog.EffectIcon(buff) + (buffTurns > 0 ? buffTurns.ToString() : "");
            }
            if (debuff != vis.ShownDebuff || debuffTurns != vis.ShownDebuffTurns)
            {
                vis.ShownDebuff = debuff; vis.ShownDebuffTurns = debuffTurns;
                vis.Debuff.style.display = debuff == null ? DisplayStyle.None : DisplayStyle.Flex;
                if (debuff != null) vis.Debuff.text = ArtCatalog.EffectIcon(debuff) + (debuffTurns > 0 ? debuffTurns.ToString() : "");
            }
            vis.Watching.style.display = watch ? DisplayStyle.Flex : DisplayStyle.None;
            vis.Tag.style.borderBottomColor = owner;
            vis.Tag.EnableInClassList("ghost", ghost);
            vis.Tag.EnableInClassList("dim", dim);
            vis.Tag.EnableInClassList("tower", vis.IsTower);
        }

        void Place(Visual vis, Hex h)
        {
            vis.Used = true;
            vis.To = HexLayout.ToWorld(h, BoardView.TileHeight);
            if (!vis.Root.activeSelf) { vis.Root.SetActive(true); vis.T = 1f; }
            if (vis.T >= 1f) vis.Root.transform.localPosition = vis.To;
            vis.Tag.style.display = DisplayStyle.Flex;
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

        /// <summary>The visual for a key: the one already showing it, else a free pooled one, else a new one.</summary>
        Visual Get(int key)
        {
            foreach (var v in visuals)
                if (v.Key == key) return v;
            foreach (var v in visuals)
                if (!v.Used && !v.Dying) // every key in the view was claimed in pass 1, so this key is gone
                {
                    v.Key = key;
                    v.T = 1f;
                    v.ShownHp = v.ShownMax = v.ShownAtk = -1;
                    return v;
                }
            var vis = new Visual { Key = key };
            vis.Root = new GameObject("Piece");
            vis.Root.transform.SetParent(transform, false);
            vis.Body = Gfx.Primitive(Gfx.Shape.Sphere, "Body", vis.Root.transform, Vector3.zero, Vector3.one);
            vis.BodyMesh = vis.Body.GetComponent<MeshFilter>();
            vis.BodyRenderer = vis.Body.GetComponent<Renderer>();
            var rim = Gfx.Primitive(Gfx.Shape.Cylinder, "Rim", vis.Root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.25f, 0.05f, 1.25f));
            vis.Rim = rim.GetComponent<Renderer>();
            vis.Watch = Gfx.Primitive(Gfx.Shape.Sphere, "Overwatch", vis.Root.transform, new Vector3(0f, 1.7f, 0f), new Vector3(0.3f, 0.3f, 0.3f));
            Gfx.SetColor(vis.Watch.GetComponent<Renderer>(), new Color(1f, 0.9f, 0.1f));
            BuildTag(vis);
            visuals.Add(vis);
            return vis;
        }

        void BuildTag(Visual vis)
        {
            vis.Tag = new VisualElement { pickingMode = PickingMode.Ignore };
            vis.Tag.AddToClassList("unit-tag");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("hp-bar");
            vis.HpFill = new VisualElement { pickingMode = PickingMode.Ignore };
            vis.HpFill.AddToClassList("hp-fill");
            vis.Hp = new Label { pickingMode = PickingMode.Ignore };
            vis.Hp.AddToClassList("hp-text");
            bar.Add(vis.HpFill);
            bar.Add(vis.Hp);
            var chips = new VisualElement { pickingMode = PickingMode.Ignore };
            chips.AddToClassList("chips");
            vis.Atk = Chip(chips, "chip-atk");
            vis.Range = Chip(chips, "chip-range");
            vis.Buff = Chip(chips, "chip-buff");
            vis.Debuff = Chip(chips, "chip-debuff");
            vis.Watching = Chip(chips, "chip-watch");
            vis.Watching.text = Strings.OverwatchIcon;
            vis.Spot = Chip(chips, "chip-spot");
            vis.Spot.style.display = DisplayStyle.None;
            vis.Tag.Add(bar);
            vis.Tag.Add(chips);
            tagLayer.Add(vis.Tag);
        }

        /// <summary>UX-16: the marker under the unit (rune / wellspring) as a small icon in its tag, so a tall model never
        /// hides it. Rebuilt only when it changes.</summary>
        static void SetSpot(Visual vis, string icon, Marker m)
        {
            if (icon == vis.ShownSpot) return;
            vis.ShownSpot = icon;
            bool on = !string.IsNullOrEmpty(icon);
            vis.Spot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;
            vis.Spot.text = icon;
            vis.Spot.style.backgroundColor = ArtCatalog.MarkerOf(m).Color;
        }

        /// <summary>Units whose tag shows a marker icon (tests).</summary>
        public int SpotIconsShown
        {
            get { int n = 0; foreach (var v in visuals) if (v.Used && v.Spot.style.display == DisplayStyle.Flex) n++; return n; }
        }

        static Label Chip(VisualElement row, string cls)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("chip");
            l.AddToClassList(cls);
            row.Add(l);
            return l;
        }

        /// <summary>Swaps the body mesh in place (no GameObject churn). kind = UnitClass, or 100 for a tower.</summary>
        static void SetBody(Visual vis, int kind)
        {
            // A-01/A-02: the model and its scale come from the theme (ArtCatalog), keyed by the class; biome = colour.
            var art = kind == 100 ? ArtCatalog.Tower : ArtCatalog.Unit((UnitClass)kind);
            vis.BodyMesh.sharedMesh = kind == 100 ? ArtCatalog.TowerMesh : ArtCatalog.UnitMesh((UnitClass)kind);
            vis.Body.transform.localPosition = Vector3.zero;
            vis.Body.transform.localRotation = Quaternion.identity;
            vis.Body.transform.localScale = Vector3.one * art.ModelScale;
            vis.BodyKind = kind;
            vis.IsTower = kind == 100;
        }

        // ---------- Event playback (filtered events only) ----------

        public void Play(IReadOnlyList<GameEvent> events)
        {
            SkipRunning(); // a new batch always wins: running tweens jump to their end
            foreach (var e in events)
            {
                switch (e)
                {
                    case UnitDeployed dep: pendingPops.Add(dep.UnitId); break;
                    case CardPlayed cp: CardFly(cp.Player, cp.Target); break;
                    case UnitMoved m: Tween(m.UnitId, m.From); break;
                    case UnitPushed p: if (p.From != p.To) Tween(p.UnitId, p.From); break;
                    case UnitTeleported t: Tween(t.UnitId, t.From); break;
                    case DamageDealt d:
                        Float(d.Target, "-" + d.Amount, new Color(1f, 0.25f, 0.2f), true);
                        board.Flash(d.Target, d.Kind == DamageKind.Trap ? Gfx.TrapHl
                            : d.Kind == DamageKind.Lava ? Gfx.LavaHl : d.Kind == DamageKind.Collision ? Gfx.FlashHl
                            : d.Kind == DamageKind.TowerShot || d.Kind == DamageKind.Overwatch ? Gfx.ShotHl : Gfx.AttackHl);
                        AnimateDamage(d);
                        break;
                    case UnitHealed h: FloatAtUnit(h.UnitId, "+" + h.Amount, new Color(0.3f, 1f, 0.4f)); break;
                    case ShieldBlocked s: FloatAtUnit(s.UnitId, Strings.Blocked, Color.white); break;
                    case TowerDamageBlocked tb: if (TowerCell(tb.Owner, out var tc)) Float(tc, Strings.Blocked, Color.white, false); break;
                    case UnitDied u:
                        StartDeath(u.UnitId);
                        if (seen.TryGetValue(u.UnitId, out var s2)) { BurstAt(s2.Pos); board.Flash(s2.Pos, Gfx.DeathHl); Float(s2.Pos, Strings.DeathMark, new Color(1f, 0.15f, 0.15f), true); }
                        break;
                    case TrapTriggered tr: board.Flash(tr.Cell, Gfx.TrapHl); Float(tr.Cell, Strings.TrapHit, new Color(0.85f, 0.45f, 1f), false); break;
                    case TowerShot ts:
                        if (TowerCell(ts.Owner, out var shooter)) { board.Flash(shooter, Gfx.ShotHl); Float(shooter, Strings.TowerShotMark, new Color(1f, 0.85f, 0.2f), false); }
                        break;
                    case BearerSpawned bs: // U-44: spawn burst + flash where the Bearer appears
                        BearerSpawnEffects++;
                        pendingPops.Add(bs.UnitId); BurstAt(bs.Cell); board.Flash(bs.Cell, ArtCatalog.MarkerOf(Marker.Portal).Color);
                        Float(bs.Cell, ArtCatalog.UnitIcon(UnitClass.PortalBearer), Color.white, true);
                        break;
                    case TorchPlaced tp: board.Flash(tp.Cell, ArtCatalog.TorchColor); Float(tp.Cell, Strings.TorchIcon, ArtCatalog.TorchColor, false); break;
                    case TorchExtinguished te: board.Flash(te.Cell, Gfx.FlashHl); break;
                    case OverwatchFired of:
                        if (seen.TryGetValue(of.UnitId, out var w)) { board.Flash(w.Pos, Gfx.ShotHl); Float(w.Pos, Strings.OverwatchMark, new Color(1f, 0.85f, 0.2f), false); }
                        break;
                }
            }
        }

        bool TowerCell(PlayerId owner, out Hex cell)
        {
            cell = default;
            if (view == null) return false;
            var pos = owner == view.Viewer ? view.OwnTowerPos : view.EnemyTowerPos;
            if (!pos.HasValue) return false;
            cell = pos.Value;
            return true;
        }

        void Tween(int unitId, Hex from)
        {
            foreach (var v in visuals)
                if (v.Key == unitId && v.Root.activeSelf)
                {
                    v.From = HexLayout.ToWorld(from, BoardView.TileHeight);
                    v.Root.transform.localPosition = v.From;
                    v.T = 0f;
                    v.Animating = true;
                }
        }

        Visual Active(int key)
        {
            foreach (var v in visuals)
                if (v.Key == key && v.Root.activeSelf) return v;
            return null;
        }

        /// <summary>Seconds until the piece's current move tween ends (a move-attack lunges after the hop).</summary>
        static float Busy(Visual v) => v == null ? 0f : (1f - v.T) * MoveSeconds + (v.LungeT < 1f ? (1f - v.LungeT) * LungeSeconds : 0f);

        void AnimateDamage(DamageDealt d)
        {
            float hitDelay = 0f;
            bool shot = d.Kind == DamageKind.TowerShot || d.Kind == DamageKind.Overwatch;
            if (d.Kind == DamageKind.Attack && d.SourceUnitId >= 0)
            {
                var a = Active(d.SourceUnitId);
                if (a != null)
                {
                    float wait = (1f - a.T) * MoveSeconds;
                    var dir = HexLayout.ToWorld(d.Target, BoardView.TileHeight) - a.To;
                    dir.y = 0f;
                    a.LungeDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
                    a.LungeT = -wait / LungeSeconds;
                    a.Animating = true;
                    hitDelay = wait + LungeSeconds * 0.5f;
                }
            }
            else if (shot)
            {
                Vector3 from;
                bool ok;
                if (d.SourceUnitId == DamageDealt.Tower) { ok = TowerCell(d.SourcePlayer, out var tc); from = HexLayout.ToWorld(tc, BoardView.TileHeight + 1.6f); }
                else { var sv = Active(d.SourceUnitId); ok = sv != null; from = ok ? sv.To + new Vector3(0f, 1f, 0f) : default; }
                if (ok) { Bolt(from, HexLayout.ToWorld(d.Target, BoardView.TileHeight + 0.8f)); hitDelay = BoltSeconds; }
            }
            Visual t = d.TargetUnitId >= 0 ? Active(d.TargetUnitId)
                : d.TargetUnitId == DamageDealt.Tower ? Active(TowerKey(d.SourcePlayer == PlayerId.A ? PlayerId.B : PlayerId.A)) : null;
            if (t != null)
            {
                hitDelay = Mathf.Max(hitDelay, Busy(t));
                t.ShakeT = -hitDelay / ShakeSeconds;
                t.Animating = true;
            }
        }

        void StartDeath(int unitId)
        {
            var v = Active(unitId);
            if (v == null) return;
            float wait = Mathf.Max(Busy(v), v.ShakeT < 1f ? (1f - v.ShakeT) * ShakeSeconds : 0f);
            v.DieT = -wait / DieSeconds;
            v.Animating = true;
        }

        static void ResetAnim(Visual v)
        {
            v.T = 1f; v.LungeT = v.ShakeT = v.DieT = v.PopT = 1f;
            v.Animating = false;
            v.Root.transform.localScale = Vector3.one;
        }

        /// <summary>Ends every running tween at its final state (pieces snap, dying pieces go, overlay effects hide).</summary>
        void SkipRunning()
        {
            foreach (var v in visuals)
            {
                if (!v.Animating) continue;
                bool dying = v.Dying;
                ResetAnim(v);
                if (dying && v.Key == NoKey) { v.Root.SetActive(false); continue; }
                v.Root.transform.localPosition = v.To;
            }
            foreach (var f in fxs)
                if (f.Active) { f.Active = false; f.El.style.display = DisplayStyle.None; }
        }

        Fx GetFx(bool card)
        {
            foreach (var x in fxs)
                if (!x.Active && x.Card == card) return x;
            var f = new Fx { Card = card, El = new VisualElement { pickingMode = PickingMode.Ignore } };
            var s = f.El.style;
            s.position = Position.Absolute;
            if (card)
            {
                s.width = 56; s.height = 78; s.marginLeft = -28; s.marginTop = -39;
                s.backgroundColor = new Color(0.95f, 0.92f, 0.8f);
                s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 4;
                s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = new Color(0.55f, 0.4f, 0.9f);
                s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 8;
            }
            else
            {
                s.width = 18; s.height = 18; s.marginLeft = -9; s.marginTop = -9;
                s.backgroundColor = new Color(1f, 0.9f, 0.2f);
                s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 9;
            }
            s.display = DisplayStyle.None;
            overlay.Add(f.El);
            fxs.Add(f);
            return f;
        }

        void CardFly(PlayerId player, Hex target)
        {
            if (overlay == null || overlay.panel == null || view == null) return;
            var f = GetFx(true);
            var r = overlay.layout;
            bool mine = player == view.Viewer;
            f.FromPanel = new Vector2(r.width * 0.5f, mine ? r.height - 60f : 60f);
            f.To = HexLayout.ToWorld(target, BoardView.TileHeight + 0.5f);
            f.Age = 0f; f.Dur = CardFlySeconds; f.Active = true;
            f.El.style.opacity = 1f;
            f.El.style.display = DisplayStyle.Flex;
        }

        void Bolt(Vector3 from, Vector3 to)
        {
            if (overlay == null || overlay.panel == null) return;
            var f = GetFx(false);
            f.From = from; f.To = to;
            f.Age = 0f; f.Dur = BoltSeconds; f.Active = true;
            f.El.style.opacity = 1f;
            f.El.style.display = DisplayStyle.Flex;
        }

        /// <summary>Applies the M7 tweens to one piece; returns false when all of them are done.</summary>
        static bool Animate(Visual v, float dt)
        {
            bool running = false;
            var pos = v.To;
            if (v.T < 1f)
            {
                v.T = Mathf.Min(1f, v.T + dt / MoveSeconds);
                float k = Mathf.SmoothStep(0f, 1f, v.T);
                pos = Vector3.Lerp(v.From, v.To, k);
                pos.y += Mathf.Sin(v.T * Mathf.PI) * 0.45f; // hop
                running |= v.T < 1f;
            }
            if (v.LungeT < 1f)
            {
                v.LungeT = Mathf.Min(1f, v.LungeT + dt / LungeSeconds);
                if (v.LungeT > 0f) pos += v.LungeDir * (Mathf.Sin(v.LungeT * Mathf.PI) * 0.4f);
                running |= v.LungeT < 1f;
            }
            if (v.ShakeT < 1f)
            {
                v.ShakeT = Mathf.Min(1f, v.ShakeT + dt / ShakeSeconds);
                if (v.ShakeT > 0f) pos.x += Mathf.Sin(v.ShakeT * 40f) * 0.09f * (1f - v.ShakeT);
                running |= v.ShakeT < 1f;
            }
            float scale = 1f;
            if (v.PopT < 1f)
            {
                v.PopT = Mathf.Min(1f, v.PopT + dt / PopSeconds);
                scale = v.PopT < 0.7f ? Mathf.Lerp(0.1f, 1.15f, v.PopT / 0.7f) : Mathf.Lerp(1.15f, 1f, (v.PopT - 0.7f) / 0.3f);
                running |= v.PopT < 1f;
            }
            if (v.DieT < 1f)
            {
                v.DieT = Mathf.Min(1f, v.DieT + dt / DieSeconds);
                float k = Mathf.Max(0f, v.DieT);
                scale = 1f - k;
                pos.y -= k * 0.6f;
                running |= v.DieT < 1f;
            }
            v.Root.transform.localPosition = pos;
            v.Root.transform.localScale = new Vector3(scale, scale, scale);
            return running;
        }

        void FloatAtUnit(int unitId, string text, Color color)
        {
            if (seen.TryGetValue(unitId, out var s)) Float(s.Pos, text, color, false);
        }

        void Float(Hex cell, string text, Color color, bool big)
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
            // Stack floaters on the same cell so simultaneous numbers stay readable.
            float lift = 0f;
            var world = HexLayout.ToWorld(cell, BoardView.TileHeight + 1.2f);
            foreach (var x in floaters)
                if (x != f && x.Age < 0.3f && (x.World - world).sqrMagnitude < 0.01f) lift += 0.45f;
            f.World = world + new Vector3(0f, lift, 0f);
            f.Age = 0f;
            f.Label.text = text;
            f.Label.style.color = color;
            f.Label.EnableInClassList("big", big);
            f.Label.style.display = DisplayStyle.Flex;
        }

        void BurstAt(Hex cell)
        {
            Burst b = null;
            foreach (var x in bursts)
                if (x.Age >= BurstSeconds) { b = x; break; }
            if (b == null)
            {
                b = new Burst();
                b.Go = Gfx.Primitive(Gfx.Shape.Sphere, "DeathBurst", transform, Vector3.zero, Vector3.one);
                b.R = b.Go.GetComponent<Renderer>();
                b.R.sharedMaterial = Gfx.Transparent;
                bursts.Add(b);
            }
            b.Age = 0f;
            b.Go.transform.localPosition = HexLayout.ToWorld(cell, BoardView.TileHeight + 0.5f);
            b.Go.SetActive(true);
        }

        void LateUpdate()
        {
            if (overlay == null || overlay.panel == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < visuals.Count; i++)
            {
                var v = visuals[i];
                if (!v.Root.activeSelf) continue;
                bool wasDying = v.Dying;
                if (v.Animating && !Animate(v, dt))
                {
                    bool gone = wasDying;
                    ResetAnim(v);
                    if (gone) { v.Root.SetActive(false); continue; }
                    v.Root.transform.localPosition = v.To;
                }
                if (!barsHidden) PlaceLabel(v.Tag, v.Root.transform.position + new Vector3(0f, v.IsTower ? 2.1f : 1.55f, 0f));
            }
            for (int i = 0; i < floaters.Count; i++)
            {
                var f = floaters[i];
                if (f.Age >= FloatSeconds) continue;
                f.Age += dt;
                if (f.Age >= FloatSeconds) { f.Label.style.display = DisplayStyle.None; continue; }
                PlaceLabel(f.Label, f.World + new Vector3(0f, f.Age * 0.9f, 0f));
                f.Label.style.opacity = f.Age < FloatSeconds * 0.6f ? 1f : (FloatSeconds - f.Age) / (FloatSeconds * 0.4f);
            }
            for (int i = 0; i < bursts.Count; i++)
            {
                var b = bursts[i];
                if (b.Age >= BurstSeconds) continue;
                b.Age += dt;
                if (b.Age >= BurstSeconds) { b.Go.SetActive(false); continue; }
                float k = b.Age / BurstSeconds;
                float s = Mathf.Lerp(0.4f, 2.2f, k);
                b.Go.transform.localScale = new Vector3(s, s * 0.7f, s);
                Gfx.SetColor(b.R, new Color(1f, 0.15f, 0.1f, 0.75f * (1f - k)));
            }
            for (int i = 0; i < fxs.Count; i++)
            {
                var f = fxs[i];
                if (!f.Active) continue;
                f.Age += dt;
                if (f.Age >= f.Dur) { f.Active = false; f.El.style.display = DisplayStyle.None; continue; }
                float k = f.Age / f.Dur;
                if (f.Card)
                {
                    var s = cam.WorldToScreenPoint(f.To);
                    var end = RuntimePanelUtils.ScreenToPanel(overlay.panel, new Vector2(s.x, Screen.height - s.y));
                    var p = Vector2.Lerp(f.FromPanel, end, Mathf.SmoothStep(0f, 1f, k));
                    f.El.style.left = p.x;
                    f.El.style.top = p.y;
                    f.El.style.opacity = k < 0.6f ? 1f : (1f - k) / 0.4f;
                }
                else PlaceLabel(f.El, Vector3.Lerp(f.From, f.To, k));
            }
        }

        void PlaceLabel(VisualElement label, Vector3 world)
        {
            var s = cam.WorldToScreenPoint(world);
            var p = RuntimePanelUtils.ScreenToPanel(overlay.panel, new Vector2(s.x, Screen.height - s.y));
            label.style.left = p.x;
            label.style.top = p.y;
        }
    }
}
