using System;

namespace HexPortal.Core
{
    /// <summary>Terrain of the 59 cells, stored by Board.Cells index. B-24: records the requested seed and attempt count.</summary>
    public sealed class GameMap
    {
        public readonly ulong RequestedSeed;
        /// <summary>1 = valid on the first attempt.</summary>
        public readonly int Attempts;
        readonly Tile[] tiles = new Tile[Board.Cells.Count];

        public GameMap(ulong requestedSeed, int attempts)
        {
            RequestedSeed = requestedSeed;
            Attempts = attempts;
        }

        public Tile Get(Hex h) => tiles[Index(h)];

        /// <summary>Engine-only: the client must not change terrain (map events go through the engine).</summary>
        internal void Set(Hex h, Tile tile) => tiles[Index(h)] = tile;

        internal GameMap Clone()
        {
            var m = new GameMap(RequestedSeed, Attempts);
            Array.Copy(tiles, m.tiles, tiles.Length);
            return m;
        }

        static int Index(Hex h)
        {
            int i = Board.IndexOf(h);
            if (i < 0) throw new ArgumentOutOfRangeException(nameof(h), "Off the board: " + h);
            return i;
        }
    }
}
