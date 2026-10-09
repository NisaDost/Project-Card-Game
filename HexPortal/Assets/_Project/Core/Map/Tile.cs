using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§2.1: a marker sits on top of the biome; the biome is kept. Rock comes from map events (B-14) or a template
    /// (B-25, no biome). Void (B-25): a cell the template leaves out; not part of the playable board, no biome.
    /// B-14 (v4.0): Rock is now Mountain; <c>Rock</c> stays as an alias of the same value for the client until it migrates.
    /// B-15 River (template only, impassable for ground units, no biome), B-16 Lava (template only, passable, damages, no biome).</summary>
    public enum Marker { None, RuneStone, Wellspring, Portal, Mountain, Void, River, Lava, Rock = Mountain }

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
