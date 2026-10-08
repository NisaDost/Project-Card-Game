using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>C-02: where a player may play cards.</summary>
    public static class ControlZone
    {
        /// <summary>Playable cells (B-25: never Void) within Catalog.ControlRange of an own unit or the own tower (including those cells).
        /// Every Sight is larger than ControlRange, so the zone is always Visible to its owner.</summary>
        public static HashSet<Hex> Cells(GameState state, PlayerId player)
        {
            var zone = new HashSet<Hex>();
            var tower = state.GetTower(player);
            if (tower.IsPlaced) AddWithin(zone, tower.Pos, state.Map); // setup: the tower may not be placed yet (S-05)
            foreach (var u in state.Units)
                if (u.Owner == player) AddWithin(zone, u.Pos, state.Map);
            return zone;
        }

        static void AddWithin(HashSet<Hex> set, Hex center, GameMap map) => Board.AddWithin(set, center, Catalog.ControlRange, map);
    }
}
