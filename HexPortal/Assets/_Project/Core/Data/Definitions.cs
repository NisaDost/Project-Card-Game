namespace HexPortal.Core.Data
{
    public enum UnitClass { Guardian, Rider, Archer, Mage, Healer }

    public enum Biome { None, Forest, Desert, Snow }

    /// <summary>Support cards only (§4.2). Character cards have no rarity (UX-07).</summary>
    public enum Rarity { None, Common, Rare, Epic }

    public enum CardCategory { Power, Protection, Movement, Curse, Control, Trap }

    /// <summary>D-02: which support pool a card belongs to.</summary>
    public enum SupportPool { Buff, DebuffTrap }

    /// <summary>C-03 duration types. Timed lasts Catalog.TimedEffectTurns turns (C-04).</summary>
    public enum DurationKind { Instant, Timed, Permanent }

    /// <summary>Adjacent = distance 1; StraightLine = along one of 6 directions; Distance = any cell within range.</summary>
    public enum RangeKind { Adjacent, StraightLine, Distance }

    public enum EffectKind
    {
        AttackBonus,
        BlockNextDamage,
        Heal,
        MoveBonus,
        Teleport,
        AttackPenalty,
        Poison,
        Push,
        Root,
        TrapDamage,
        TrapTeleportHome,
    }

    public sealed class UnitDef
    {
        public readonly string Id;
        public readonly UnitClass Class;
        /// <summary>Mana paid to deploy the card (T-07). Not Energy.</summary>
        public readonly int Cost;
        public readonly int Attack;
        public readonly int Health;
        public readonly int Move;
        public readonly int Sight;
        public readonly RangeKind Range;
        public readonly int MinRange;
        public readonly int MaxRange;

        public UnitDef(string id, UnitClass cls, int cost, int attack, int health, int move, int sight,
            RangeKind range, int minRange, int maxRange)
        {
            Id = id; Class = cls; Cost = cost; Attack = attack; Health = health; Move = move; Sight = sight;
            Range = range; MinRange = minRange; MaxRange = maxRange;
        }
    }

    public sealed class TowerDef
    {
        public readonly int Health;
        public readonly int Attack;
        public readonly int MinRange;
        public readonly int MaxRange;
        public readonly int Sight;
        public readonly int Move;

        public TowerDef(int health, int attack, int minRange, int maxRange, int sight, int move)
        {
            Health = health; Attack = attack; MinRange = minRange; MaxRange = maxRange; Sight = sight; Move = move;
        }
    }

    /// <summary>One biome variant of a class (D-01). Stats come from the UnitDef of its class.</summary>
    public sealed class CharacterCardDef
    {
        public readonly string Id;
        public readonly UnitClass Class;
        public readonly Biome Biome;
        public readonly int Copies;
        public Rarity Rarity => Rarity.None;

        public CharacterCardDef(string id, UnitClass cls, Biome biome, int copies)
        {
            Id = id; Class = cls; Biome = biome; Copies = copies;
        }
    }

    public sealed class SupportCardDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly CardCategory Category;
        public readonly SupportPool Pool;
        public readonly EffectKind Effect;
        /// <summary>Effect magnitude (attack, heal, move, push distance, damage). 0 when the effect has none.</summary>
        public readonly int Amount;
        public readonly DurationKind Duration;
        /// <summary>Mana cost (C-01).</summary>
        public readonly int Cost;
        public readonly Rarity Rarity;
        public readonly int Copies;

        public SupportCardDef(string id, string name, CardCategory category, SupportPool pool, EffectKind effect,
            int amount, DurationKind duration, int cost, Rarity rarity, int copies)
        {
            Id = id; Name = name; Category = category; Pool = pool; Effect = effect; Amount = amount;
            Duration = duration; Cost = cost; Rarity = rarity; Copies = copies;
        }
    }

    public sealed class QuestDef
    {
        public readonly string Id;
        public readonly string Name;
        /// <summary>Count or threshold the quest checks (units, kills, damage, tower health...).</summary>
        public readonly int Amount;
        /// <summary>Round the quest is judged at, or 0 if none.</summary>
        public readonly int Round;
        /// <summary>Q-05: condition must hold at the end of 2 consecutive own turns.</summary>
        public readonly bool Hold;
        /// <summary>Q-04.</summary>
        public readonly bool CanFail;

        public QuestDef(string id, string name, int amount, int round, bool hold, bool canFail)
        {
            Id = id; Name = name; Amount = amount; Round = round; Hold = hold; CanFail = canFail;
        }
    }

    public sealed class PassiveDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Amount;
        /// <summary>Round the passive applies in, or 0 if none.</summary>
        public readonly int Round;
        public readonly bool OncePerMatch;

        public PassiveDef(string id, string name, int amount, int round, bool oncePerMatch)
        {
            Id = id; Name = name; Amount = amount; Round = round; OncePerMatch = oncePerMatch;
        }
    }

    public sealed class MapEventDef
    {
        public readonly string Id;
        public readonly string Name;
        /// <summary>Mirror pairs affected (E-03). 0 for cluster events (E-11).</summary>
        public readonly int Pairs;

        public MapEventDef(string id, string name, int pairs)
        {
            Id = id; Name = name; Pairs = pairs;
        }
    }
}
