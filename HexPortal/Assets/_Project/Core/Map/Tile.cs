using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§2.1: a marker sits on top of the biome; the biome is kept. Rock is only created by map events (B-14).</summary>
    public enum Marker { None, RuneStone, Wellspring, Portal, Rock }

    public readonly struct Tile
    {
        public readonly Biome Biome;
        public readonly Marker Marker;

        public Tile(Biome biome, Marker marker)
        {
            Biome = biome;
            Marker = marker;
        }

        public override string ToString() => Biome + "/" + Marker;
    }
}
