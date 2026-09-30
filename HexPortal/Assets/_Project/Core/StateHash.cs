using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>Deterministic FNV-1a 64-bit hash over the whole GameState (replay checks, desync detection).</summary>
    public static class StateHash
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        static readonly CardPool[] Slots = { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap };

        public static ulong Compute(GameState s)
        {
            ulong h = Offset;
            Add(ref h, s.Round);
            Add(ref h, (int)s.ActivePlayer);
            Add(ref h, s.Winner.HasValue ? (int)s.Winner.Value + 1 : 0);
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                Add(ref h, s.GetEnergy(p));
                Add(ref h, s.GetMana(p));
                Add(ref h, s.IsTowerShotAvailable(p) ? 1 : 0);
                Add(ref h, s.IsDrawPending(p) ? 1 : 0);
                Add(ref h, s.GetPrePickSlot(p));
                Add(ref h, s.GetPrePickCardId(p));
                var t = s.GetTower(p);
                Add(ref h, t.Pos.Q);
                Add(ref h, t.Pos.R);
                Add(ref h, t.Health);
                AddCards(ref h, s.GetHand(p));
            }
            Add(ref h, s.NextUnitId);
            Add(ref h, s.Units.Count);
            foreach (var u in s.Units) // id order
            {
                Add(ref h, u.Id);
                Add(ref h, (int)u.Owner);
                Add(ref h, (int)u.Class);
                Add(ref h, (int)u.Biome);
                Add(ref h, u.Pos.Q);
                Add(ref h, u.Pos.R);
                Add(ref h, u.Health);
                Add(ref h, (u.ActedThisTurn ? 1 : 0) | (u.MovedThisTurn ? 2 : 0) | (u.MovedLastOwnTurn ? 4 : 0) | (u.OnOverwatch ? 8 : 0));
                Add(ref h, u.MoveBonus);
                AddEffect(ref h, u.Buff);
                AddEffect(ref h, u.Debuff);
            }
            Add(ref h, s.NextCardId);
            foreach (var slot in Slots)
            {
                AddCards(ref h, s.GetPool(slot)); // order matters
                var m = s.GetMarket(slot);
                Add(ref h, m == null ? 0 : m.Id);
            }
            Add(ref h, s.Traps.Count);
            foreach (var t in s.Traps)
            {
                Add(ref h, (int)t.Owner);
                Add(ref h, t.Pos.Q);
                Add(ref h, t.Pos.R);
                Add(ref h, t.Card.Id);
            }
            ulong rng = s.Rng.Clone().NextULong(); // the stream position, without advancing it
            Add(ref h, (int)rng);
            Add(ref h, (int)(rng >> 32));
            foreach (var c in Board.Cells)
            {
                var tile = s.Map.Get(c);
                Add(ref h, (int)tile.Biome);
                Add(ref h, (int)tile.Marker);
            }
            return h;
        }

        static void AddCards(ref ulong h, IReadOnlyList<CardInstance> cards)
        {
            Add(ref h, cards.Count);
            foreach (var c in cards) Add(ref h, c.Id);
        }

        static void AddEffect(ref ulong h, ActiveEffect e)
        {
            if (e == null)
            {
                Add(ref h, -1);
                return;
            }
            foreach (char ch in e.Def.Id) Add(ref h, ch);
            Add(ref h, e.TurnsLeft);
        }

        static void Add(ref ulong h, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    h ^= (byte)(value >> (8 * i));
                    h *= Prime;
                }
            }
        }
    }
}
