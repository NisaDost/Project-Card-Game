using System;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>A unit on the board. Only the engine changes it (setters are internal).</summary>
    public sealed class Unit
    {
        public readonly int Id;
        public readonly PlayerId Owner;
        public readonly UnitClass Class;
        public readonly Biome Biome;
        public readonly UnitDef Def;

        public Hex Pos { get; internal set; }
        public int Health { get; internal set; }
        /// <summary>T-05: used its one action this turn.</summary>
        public bool ActedThisTurn { get; internal set; }
        public bool MovedThisTurn { get; internal set; }
        /// <summary>U-02: moved during the owner's previous own turn (set at the end of each own turn).</summary>
        public bool MovedLastOwnTurn { get; internal set; }
        /// <summary>U-28.</summary>
        public bool OnOverwatch { get; internal set; }

        internal Unit(int id, PlayerId owner, UnitClass cls, Biome biome, Hex pos)
        {
            Id = id;
            Owner = owner;
            Class = cls;
            Biome = biome;
            Def = DefOf(cls);
            Pos = pos;
            Health = Def.Health;
        }

        public static UnitDef DefOf(UnitClass cls)
        {
            foreach (var d in Catalog.Units)
                if (d.Class == cls) return d;
            throw new ArgumentOutOfRangeException(nameof(cls), "No UnitDef for " + cls);
        }

        public override string ToString() => "#" + Id + " " + Owner + " " + Class + " " + Pos + " hp" + Health;
    }
}
