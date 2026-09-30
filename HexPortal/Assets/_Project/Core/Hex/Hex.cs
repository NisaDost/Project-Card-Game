using System;
using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>Axial hex coordinate (pointy-top), origin at the Portal. S = -Q - R. See the hex-grid skill.</summary>
    public readonly struct Hex : IEquatable<Hex>
    {
        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <summary>The 6 edge directions, in this fixed order.</summary>
        public static readonly IReadOnlyList<Hex> Directions = new[]
        {
            new Hex(1, 0), new Hex(1, -1), new Hex(0, -1), new Hex(-1, 0), new Hex(-1, 1), new Hex(0, 1),
        };

        public Hex Neighbor(int dir) => this + Directions[dir];

        public static int Distance(Hex a, Hex b)
        {
            int dq = a.Q - b.Q, dr = a.R - b.R;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        /// <summary>B-04: the 180° pair cell around the Portal.</summary>
        public Hex Mirror() => new Hex(-Q, -R);

        /// <summary>Doubled-width storage coords (x, y), y = 0 at the top (player B's side).</summary>
        public static Hex FromDoubled(int x, int y)
        {
            int r = y - 4;
            return new Hex(((x - 6) - r) / 2, r);
        }

        public void ToDoubled(out int x, out int y)
        {
            y = R + 4;
            x = 2 * Q + R + 6;
        }

        public static Hex operator +(Hex a, Hex b) => new Hex(a.Q + b.Q, a.R + b.R);
        public static bool operator ==(Hex a, Hex b) => a.Q == b.Q && a.R == b.R;
        public static bool operator !=(Hex a, Hex b) => !(a == b);

        public bool Equals(Hex other) => this == other;
        public override bool Equals(object obj) => obj is Hex h && this == h;
        public override int GetHashCode() => unchecked(Q * 397) ^ R;
        public override string ToString() => "(" + Q + "," + R + ")";
    }
}
