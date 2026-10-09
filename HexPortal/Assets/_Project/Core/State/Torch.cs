namespace HexPortal.Core
{
    /// <summary>V-12 (v4.0): a torch keeps the cells within Catalog.TorchSight Visible to its owner. Immutable; ids ascend
    /// in placement order (the lowest own id is the oldest).</summary>
    public sealed class Torch
    {
        public readonly int Id;
        public readonly PlayerId Owner;
        public readonly Hex Pos;

        internal Torch(int id, PlayerId owner, Hex pos)
        {
            Id = id;
            Owner = owner;
            Pos = pos;
        }

        public override string ToString() => "Torch#" + Id + " " + Owner + " " + Pos;
    }
}
