using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>T-07: deploying a character card from the hand.</summary>
    public static class Deploy
    {
        /// <summary>T-07, C-02: an empty cell (no unit, tower or rock) in the Control Zone, not the Portal, Visible.</summary>
        internal static bool IsLegalCell(GameState state, Hex h, ISet<Hex> visible, ISet<Hex> zone) =>
            zone.Contains(h) && visible.Contains(h) && h != Board.Portal && Movement.IsEmpty(state, h);

        internal static bool IsLegal(GameState state, PlayerId p, CardInstance card, Hex h, ISet<Hex> visible, ISet<Hex> zone) =>
            card != null && card.IsCharacter && state.GetMana(p) >= card.Cost && IsLegalCell(state, h, visible, zone);

        /// <summary>Hand order, then Board.Cells order.</summary>
        internal static void AddLegal(GameState state, PlayerId p, ISet<Hex> visible, ISet<Hex> zone, List<ICommand> list)
        {
            foreach (var card in state.GetHand(p))
            {
                if (!card.IsCharacter || state.GetMana(p) < card.Cost) continue;
                foreach (var h in Board.Cells)
                    if (IsLegalCell(state, h, visible, zone)) list.Add(new DeployCommand(p, card.Id, h));
            }
        }

        internal static void Apply(GameState state, DeployCommand cmd, List<GameEvent> events)
        {
            var p = cmd.Player;
            var card = state.FindInHand(p, cmd.CardId);
            if (!IsLegal(state, p, card, cmd.Cell, Visibility.VisibleCells(state, p), ControlZone.Cells(state, p)))
                throw new IllegalCommandException("Illegal deploy: " + cmd);

            Mana.Spend(state, p, card.Cost); // Mana only, no Energy
            state.HandList(p).Remove(card);
            var u = state.AddUnit(p, card.Character.Class, card.Character.Biome, cmd.Cell);
            u.ActedThisTurn = true; // T-07: no action on the turn it arrives
            events.Add(new UnitDeployed(u.Id, card.Id, cmd.Cell));
            // U-29 (v2.6): the trap on the cell first (C-32), then tower and overwatch (U-27 "deployed").
            Defense.ResolveArrivalTriggers(state, u, ArrivalKind.Deployed, events);
        }
    }
}
