namespace HexPortal.Core
{
    /// <summary>C-30…C-35: a face-down trap. Hidden from the opponent until it triggers (C-33).</summary>
    public sealed class Trap
    {
        public readonly PlayerId Owner;
        public readonly Hex Pos;
        public readonly CardInstance Card;

        internal Trap(PlayerId owner, Hex pos, CardInstance card)
        {
            Owner = owner;
            Pos = pos;
            Card = card;
        }
    }
}
