using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>D-02, D-05: the three pools, and the Market slot each one fills (slot index = (int)pool).</summary>
    public enum CardPool { Character, Buff, DebuffTrap }

    /// <summary>One physical card (D-01, D-02). The id is unique and permanent for the match.
    /// Exactly one of Character / Support is set.</summary>
    public sealed class CardInstance
    {
        public readonly int Id;
        public readonly CharacterCardDef Character;
        public readonly SupportCardDef Support;

        internal CardInstance(int id, CharacterCardDef character, SupportCardDef support)
        {
            Id = id;
            Character = character;
            Support = support;
        }

        public bool IsCharacter => Character != null;
        public string DefId => IsCharacter ? Character.Id : Support.Id;

        /// <summary>Mana cost: T-07 for characters (the class Cost), C-01 for support cards.</summary>
        public int Cost => IsCharacter ? Unit.DefOf(Character.Class).Cost : Support.Cost;

        public CardPool Pool =>
            IsCharacter ? CardPool.Character : Support.Pool == SupportPool.Buff ? CardPool.Buff : CardPool.DebuffTrap;

        public override string ToString() => "card#" + Id + " " + DefId;
    }
}
