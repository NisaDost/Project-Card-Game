using System;
using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>Terrain of the 169 cells, stored by Board.Cells index. B-24: records the requested seed and attempt count.
    /// B-25: records the template; cells the template leaves out are Void and are not part of the playable board.</summary>
    public sealed class GameMap
    {
        public readonly ulong RequestedSeed;
        /// <summary>1 = valid on the first attempt.</summary>
        public readonly int Attempts;
        /// <summary>B-25: index into Catalog.MapTemplates.</summary>
        public readonly int TemplateId;
        readonly Tile[] tiles = new Tile[Board.Cells.Count];

        public GameMap(ulong requestedSeed, int attempts, int templateId = 0)
        {
            RequestedSeed = requestedSeed;
            Attempts = attempts;
            TemplateId = templateId;
        }

        public Tile Get(Hex h) => tiles[Index(h)];

        /// <summary>B-25: on the board and not Void.</summary>
        public bool IsPlayable(Hex h)
        {
            int i = Board.IndexOf(h);
            return i >= 0 && tiles[i].Marker != Marker.Void;
        }

        /// <summary>U-07, U-09, B-14, B-15, B-25: terrain a ground unit may enter (no Mountain, no River, no Void). Lava (B-16)
        /// is passable. Ignores units and towers.</summary>
        public bool IsPassable(Hex h)
        {
            int i = Board.IndexOf(h);
            if (i < 0) return false;
            var m = tiles[i].Marker;
            return m != Marker.Mountain && m != Marker.Void && m != Marker.River;
        }

        /// <summary>B-16.</summary>
        public bool IsLava(Hex h)
        {
            int i = Board.IndexOf(h);
            return i >= 0 && tiles[i].Marker == Marker.Lava;
        }

        /// <summary>B-25: the playable cells (not Void) in Board.Cells order. For the client (drawing, camera framing).</summary>
        public IReadOnlyList<Hex> PlayableCells
        {
            get
            {
                var list = new List<Hex>(tiles.Length);
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i].Marker != Marker.Void) list.Add(Board.Cells[i]);
                return list;
            }
        }

        /// <summary>Engine-only: the client must not change terrain (map events go through the engine).</summary>
        internal void Set(Hex h, Tile tile) => tiles[Index(h)] = tile;

        internal GameMap Clone()
        {
            var m = new GameMap(RequestedSeed, Attempts, TemplateId);
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
