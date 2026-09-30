using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>A buff or debuff in one of a unit's two slots (C-05). Instant cards never create one.</summary>
    public sealed class ActiveEffect
    {
        public readonly SupportCardDef Def;
        /// <summary>C-04: turns left for Timed effects, counted down at the end of each of the unit owner's turns.
        /// Unused (0) for Permanent effects.</summary>
        public int TurnsLeft { get; internal set; }

        internal ActiveEffect(SupportCardDef def)
        {
            Def = def;
            TurnsLeft = def.Duration == DurationKind.Timed ? Catalog.TimedEffectTurns : 0;
        }

        public bool IsTimed => Def.Duration == DurationKind.Timed;
        public EffectKind Kind => Def.Effect;
    }
}
