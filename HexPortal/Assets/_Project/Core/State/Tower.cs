using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>U-06: one per player, never moves. Stats come from Catalog.Tower.
    /// During setup (S-05) it is not on the board until placed.</summary>
    public sealed class Tower
    {
        public readonly PlayerId Owner;
        public Hex Pos { get; internal set; }
        public int Health { get; internal set; }
        /// <summary>S-05: false until the tower is put on the board. An unplaced tower occupies no cell.</summary>
        public bool IsPlaced { get; internal set; }
        /// <summary>V-08: Visible to the opponent while RevealedUntilTurn >= GameState.TurnIndex. -1 = never.</summary>
        public int RevealedUntilTurn { get; internal set; } = -1;

        internal Tower(PlayerId owner, Hex pos)
        {
            Owner = owner;
            Pos = pos;
            Health = Catalog.Tower.Health;
            IsPlaced = true;
        }

        /// <summary>Not on the board yet (setup).</summary>
        internal Tower(PlayerId owner) : this(owner, default(Hex))
        {
            IsPlaced = false;
        }

        internal Tower Clone() =>
            new Tower(Owner, Pos) { Health = Health, IsPlaced = IsPlaced, RevealedUntilTurn = RevealedUntilTurn };
    }
}
