using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>U-06: one per player, never moves. Stats come from Catalog.Tower.</summary>
    public sealed class Tower
    {
        public readonly PlayerId Owner;
        public Hex Pos { get; internal set; }
        public int Health { get; internal set; }

        internal Tower(PlayerId owner, Hex pos)
        {
            Owner = owner;
            Pos = pos;
            Health = Catalog.Tower.Health;
        }
    }
}
