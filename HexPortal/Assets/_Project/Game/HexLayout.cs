using HexPortal.Core;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>World positions of pointy-top hexes (hex-grid skill). Row 0 (B's side) is at +Z. Pure layout math.</summary>
    public static class HexLayout
    {
        public const float Size = 1f;
        static readonly float Sqrt3 = Mathf.Sqrt(3f);

        public static Vector3 ToWorld(Hex h, float y = 0f) =>
            new Vector3(Size * Sqrt3 * (h.Q + h.R / 2f), y, -Size * 1.5f * h.R);

        /// <summary>Nearest hex to a world point on the board plane (cube rounding). May be off the board.</summary>
        public static Hex FromWorld(Vector3 p)
        {
            float r = -p.z / (1.5f * Size);
            float q = p.x / (Sqrt3 * Size) - r / 2f;
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return new Hex(rq, rr);
        }
    }
}
