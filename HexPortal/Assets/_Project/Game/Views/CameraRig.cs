using HexPortal.Core;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>
    /// Slightly tilted perspective camera that fits the board (UX-04), with zoom and pan clamped to the board.
    /// B's view is the same camera rotated 180° around the board centre (B-05); the data is never mirrored.
    /// Framing, clamping and picking derive from the world positions of the playable cells (B-25), so they follow any board size or template.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        const float Pitch = 60f;
        const float Fov = 34f;
        // Tuned on the 127-cell board (v3.0, centre-to-centre depth 18 world units): the default distance follows the board
        // DEPTH because UX-04 fits the board to the screen height; the aim is shifted toward the viewer so the hand fan only
        // overlaps the near edge. Both scale with the depth, so another board size keeps the framing.
        const float DistancePerDepth = 40f / 18f;
        const float ShiftPerDepth = 2.6f / 18f;
        /// <summary>Closest camera distance in world units (about 6 rows on screen). Fixed, so a bigger board zooms deeper.</summary>
        const float ClosestDistance = 11f;
        /// <summary>A tap picks the nearest cell centre within this radius (world units; a hex has outer radius 1).</summary>
        public const float PickRadius = 1.2f;

        static bool boundsReady;
        static Vector3 boundsMin, boundsMax, boardCentre;
        static float depth, reach;

        Camera cam;
        float yaw, distance;
        Vector3 target;

        public static CameraRig Instance { get; private set; }
        public float Zoom => DefaultDistance / distance; // 1 = default framing
        /// <summary>The largest Zoom (closest framing).</summary>
        public static float MaxZoom { get { EnsureBounds(); return DefaultDistance / MinDistance; } }
        static float DefaultDistance => depth * DistancePerDepth;
        static float MinDistance => Mathf.Min(ClosestDistance, DefaultDistance);

        public static Camera Create()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 100f;
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.cam = cam;
            Instance = rig;
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            light.shadowStrength = 0.6f;
            light.color = new Color(1f, 0.96f, 0.88f);
            // A-01 soft look: warm sun + cool trilight ambient so faceted sides stay readable.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.26f);
            return cam;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>B-25: the cells used for framing, clamping and picking (the template's playable cells). Defaults to the
        /// whole board until a match sets them.</summary>
        static System.Collections.Generic.IReadOnlyList<Hex> playable = Board.Cells;
        static int playableTemplate = -1;

        /// <summary>Sets the playable cells of the current map (PlayerView.PlayableCells). Recomputes bounds only when the
        /// template changes (a template fixes its Void cells, B-25).</summary>
        public static void SetPlayable(int templateId, System.Collections.Generic.IReadOnlyList<Hex> cells)
        {
            if (cells == null || cells.Count == 0 || templateId == playableTemplate) return;
            playableTemplate = templateId;
            playable = cells;
            boundsReady = false;
            EnsureBounds();
        }

        static void EnsureBounds()
        {
            if (boundsReady) return;
            var cells = playable;
            boundsMin = boundsMax = HexLayout.ToWorld(cells[0], BoardView.TileHeight);
            for (int i = 1; i < cells.Count; i++)
            {
                var p = HexLayout.ToWorld(cells[i], BoardView.TileHeight);
                boundsMin = Vector3.Min(boundsMin, p);
                boundsMax = Vector3.Max(boundsMax, p);
            }
            boardCentre = (boundsMin + boundsMax) * 0.5f;
            depth = Mathf.Max(boundsMax.z - boundsMin.z, 1f);
            reach = 0f;
            for (int i = 0; i < cells.Count; i++)
                reach = Mathf.Max(reach, HexReach(HexLayout.ToWorld(cells[i], BoardView.TileHeight) - boardCentre));
            boundsReady = true;
        }

        /// <summary>Default framing for the viewer (B-05); also resets zoom and pan.</summary>
        public static void Face(Camera cam, PlayerId viewer)
        {
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) { rig = cam.gameObject.AddComponent<CameraRig>(); rig.cam = cam; }
            rig.yaw = viewer == PlayerId.A ? 0f : 180f;
            rig.ResetView();
        }

        /// <summary>Double tap: back to the default framing for the current side.</summary>
        public void ResetView()
        {
            EnsureBounds();
            distance = DefaultDistance;
            target = boardCentre + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -depth * ShiftPerDepth);
            Apply();
        }

        /// <summary>Default zoom when nothing is framed: a bit closer than the whole board (double tap shows all).</summary>
        const float StartZoom = 1.2f;
        /// <summary>World units of margin around framed points (about two cells).</summary>
        const float FrameMargin = 3.5f;

        /// <summary>Start-of-turn framing: the bounding area of the given world points (own units + tower), zoomed as close
        /// as fits but within the zoom limits. With no points, the default framing a bit closer than the whole board.</summary>
        public void Frame(System.Collections.Generic.List<Vector3> points)
        {
            EnsureBounds();
            var shift = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -depth * ShiftPerDepth);
            if (points == null || points.Count == 0)
            {
                distance = DefaultDistance / StartZoom;
                target = boardCentre + shift;
            }
            else
            {
                Vector3 min = points[0], max = points[0];
                for (int i = 1; i < points.Count; i++) { min = Vector3.Min(min, points[i]); max = Vector3.Max(max, points[i]); }
                float extent = Mathf.Max(max.x - min.x, max.z - min.z) + FrameMargin * 2f;
                // Same distance-per-extent ratio as the whole-board framing (board depth -> DefaultDistance).
                distance = Mathf.Clamp(extent * DistancePerDepth, MinDistance, DefaultDistance / StartZoom);
                target = (min + max) * 0.5f + shift * (distance / DefaultDistance);
            }
            Clamp();
            Apply();
        }

        /// <summary>One-finger / right-mouse drag: the board point under the pointer follows the pointer.</summary>
        public void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            if (!PlanePoint(fromScreen, out var a) || !PlanePoint(toScreen, out var b)) return;
            target += a - b;
            Clamp();
            Apply();
        }

        /// <summary>Pinch / wheel. factor &gt; 1 zooms in. The board point under the pivot stays put.</summary>
        public void ZoomBy(float factor, Vector2 pivotScreen)
        {
            if (factor <= 0f) return;
            bool hasPivot = PlanePoint(pivotScreen, out var before);
            distance = Mathf.Clamp(distance / factor, MinDistance, DefaultDistance);
            Apply();
            if (hasPivot && PlanePoint(pivotScreen, out var after)) target += before - after;
            Clamp();
            Apply();
        }

        void Clamp()
        {
            // The aim point stays inside the hexagon spanned by the cell centres (not their bounding rectangle, whose
            // corners are off a hexagonal board), so the screen centre is always over a cell.
            target.y = boardCentre.y;
            var off = target - boardCentre;
            float d = HexReach(off);
            if (d > reach) target = boardCentre + off * (reach / d);
        }

        /// <summary>Hex distance (fractional, cube max-norm) of a world offset from the board centre.</summary>
        static float HexReach(Vector3 off)
        {
            var c = HexLayout.FromWorldFractional(off);
            return Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y), Mathf.Abs(c.x + c.y));
        }

        void Apply()
        {
            var rot = Quaternion.Euler(Pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }

        bool PlanePoint(Vector2 screen, out Vector3 p)
        {
            p = default;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, BoardView.TileHeight, 0f));
            if (!plane.Raycast(ray, out float d)) return false;
            p = ray.GetPoint(d);
            return true;
        }

        /// <summary>The board cell for a screen point (pixels, origin bottom-left): ray–plane intersection, then the
        /// nearest cell centre within PickRadius. No physics. Units and towers are picked through their cell.</summary>
        public static bool TryPick(Camera cam, Vector2 screen, out Hex cell)
        {
            cell = default;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, BoardView.TileHeight, 0f));
            if (!plane.Raycast(ray, out float d)) return false;
            var p = ray.GetPoint(d);
            var cells = playable; // B-25: Void cells are never picked
            float best = PickRadius * PickRadius;
            bool found = false;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = HexLayout.ToWorld(cells[i], BoardView.TileHeight);
                float dx = c.x - p.x, dz = c.z - p.z, sq = dx * dx + dz * dz;
                if (sq <= best) { best = sq; cell = cells[i]; found = true; }
            }
            return found;
        }
    }
}
