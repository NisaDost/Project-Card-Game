using HexPortal.Core;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>Slightly tilted perspective camera that fits the board height (UX-04). B's view is the same camera
    /// rotated 180° around the board centre (B-05); the data is never mirrored.</summary>
    public static class CameraRig
    {
        const float Pitch = 60f;
        const float Fov = 34f;
        const float Distance = 27.5f;

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
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            return cam;
        }

        public static void Face(Camera cam, PlayerId viewer)
        {
            float yaw = viewer == PlayerId.A ? 0f : 180f;
            var rot = Quaternion.Euler(Pitch, yaw, 0f);
            // Shift the aim a little toward the viewer so the hand fan overlaps the near edge only slightly.
            var target = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -2.2f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * Distance, rot);
        }

        /// <summary>The board cell under a screen point (pixels, origin bottom-left), if any.</summary>
        public static bool TryPick(Camera cam, Vector2 screen, out Hex cell)
        {
            cell = default;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, BoardView.TileHeight, 0f));
            if (!plane.Raycast(ray, out float d)) return false;
            cell = HexLayout.FromWorld(ray.GetPoint(d));
            return Board.IsOnBoard(cell);
        }
    }
}
