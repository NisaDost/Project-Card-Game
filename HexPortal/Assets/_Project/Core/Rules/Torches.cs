using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>V-12 (v4.0) torches. A torch keeps the cells within Catalog.TorchSight Visible to its owner (Visibility),
    /// costs TorchCost Mana, uses no action or Energy, at most TorchesPerUnitPerTurn per unit per turn, at most
    /// MaxTorchesPerPlayer per player (a new one puts out the oldest), and goes out when an enemy unit stops on its cell.
    /// Interim (not in the GDD): a unit may not place a torch on a cell that already holds an own torch.</summary>
    public static class Torches
    {
        public static int ActiveCount(GameState state, PlayerId owner)
        {
            int n = 0;
            foreach (var t in state.Torches)
                if (t.Owner == owner) n++;
            return n;
        }

        static Torch OwnTorchAt(GameState state, PlayerId owner, Hex h)
        {
            foreach (var t in state.Torches)
                if (t.Owner == owner && t.Pos == h) return t;
            return null;
        }

        /// <summary>The single legality check (own turn and the draw are checked by Engine).</summary>
        internal static bool IsLegal(GameState state, PlayerId p, Unit u) =>
            u != null && u.Owner == p && !u.TorchUsedThisTurn && state.GetMana(p) >= Catalog.TorchCost
            && OwnTorchAt(state, p, u.Pos) == null;

        internal static void AddLegal(GameState state, PlayerId p, List<ICommand> list)
        {
            foreach (var u in state.Units)
                if (IsLegal(state, p, u)) list.Add(new PlaceTorchCommand(p, u.Id));
        }

        internal static void Apply(GameState state, PlaceTorchCommand cmd, EventLog events)
        {
            var u = state.GetUnit(cmd.UnitId);
            if (!IsLegal(state, cmd.Player, u)) throw new IllegalCommandException("Illegal torch: " + cmd);
            Mana.Spend(state, cmd.Player, Catalog.TorchCost);
            u.TorchUsedThisTurn = true;
            if (ActiveCount(state, cmd.Player) >= Catalog.MaxTorchesPerPlayer)
                foreach (var t in state.Torches)
                    if (t.Owner == cmd.Player)
                    {
                        Remove(state, t, TorchEndReason.Replaced, events); // the oldest (placement order)
                        break;
                    }
            var torch = new Torch(state.NextTorchId++, cmd.Player, u.Pos);
            state.TorchList.Add(torch);
            events.Add(new TorchPlaced(cmd.Player, torch.Id, u.Id, u.Pos));
        }

        /// <summary>V-12: an enemy unit stopped on the cell (any arrival: move, push, teleport, deploy, return).</summary>
        internal static void OnArrival(GameState state, Unit unit, EventLog events)
        {
            var t = OwnTorchAt(state, unit.Owner.Opponent(), unit.Pos);
            if (t != null) Remove(state, t, TorchEndReason.EnemyStood, events);
        }

        static void Remove(GameState state, Torch t, TorchEndReason reason, EventLog events)
        {
            state.TorchList.Remove(t);
            events.Add(new TorchExtinguished(t.Owner, t.Id, t.Pos, reason));
        }
    }
}
