using HexPortal.Core;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>
    /// Slightly tilted perspective camera that fits the board (UX-04), with zoom and pan clamped to the board.
    /// B's view is the same camera rotated 180° around the board centre (B-05); the data is never mirrored.
    /// Framing, clamping and picking derive from the world positions of Board.Cells, so they follow any board size.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        const float Pitch = 60f;
        const float Fov = 34f;
        // Tuned on the 59-cell board (centre-to-centre extent 12 world units): distance 28.5, aim shifted 3.1 toward
        // the viewer so the hand fan only overlaps the near edge. Both scale with the board extent.
        const float DistancePerExtent = 28.5f / 12f;
        const float ShiftPerExtent = 3.1f / 12f;
        const float MinZoom = 0.35f; // closest distance as a fraction of the default framing
        /// <summary>A tap picks the nearest cell centre within this radius (world units; a hex has outer radius 1).</summary>
        public const float PickRadius = 1.2f;

        static bool boundsReady;
        static Vector3 boundsMin, boundsMax, boardCentre;
        static float extent;

        Camera cam;
        float yaw, distance;
        Vector3 target;

        public static CameraRig Instance { get; private set; }
        public float Zoom => DefaultDistance / distance; // 1 = default framing
        static float DefaultDistance => extent * DistancePerExtent;

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
            return cam;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static void EnsureBounds()
        {
            if (boundsReady) return;
            var cells = Board.Cells;
            boundsMin = boundsMax = HexLayout.ToWorld(cells[0], BoardView.TileHeight);
            for (int i = 1; i < cells.Count; i++)
            {
                var p = HexLayout.ToWorld(cells[i], BoardView.TileHeight);
                boundsMin = Vector3.Min(boundsMin, p);
                boundsMax = Vector3.Max(boundsMax, p);
            }
            boardCentre = (boundsMin + boundsMax) * 0.5f;
            extent = Mathf.Max(boundsMax.x - boundsMin.x, boundsMax.z - boundsMin.z, 1f);
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
            target = boardCentre + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -extent * ShiftPerExtent);
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
            distance = Mathf.Clamp(distance / factor, DefaultDistance * MinZoom, DefaultDistance);
            Apply();
            if (hasPivot && PlanePoint(pivotScreen, out var after)) target += before - after;
            Clamp();
            Apply();
        }

        void Clamp()
        {
            // The aim point stays over the board rectangle (cell centres), so the board can never leave the screen.
            target.x = Mathf.Clamp(target.x, boundsMin.x, boundsMax.x);
            target.z = Mathf.Clamp(target.z, boundsMin.z, boundsMax.z);
            target.y = boardCentre.y;
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
            var cells = Board.Cells;
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
