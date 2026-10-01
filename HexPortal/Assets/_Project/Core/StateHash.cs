using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>Deterministic FNV-1a 64-bit hash over the whole GameState (replay checks, desync detection).</summary>
    public static class StateHash
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        static readonly CardPool[] Slots = { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap };

        public static ulong Compute(GameState s) => Compute(s, true);

        /// <summary><paramref name="includeFog"/> false: everything except the fog memory (light-mode comparisons).</summary>
        public static ulong Compute(GameState s, bool includeFog)
        {
            ulong h = Offset;
            Add(ref h, s.Round);
            Add(ref h, (int)s.ActivePlayer);
            Add(ref h, s.Winner.HasValue ? (int)s.Winner.Value + 1 : 0);
            Add(ref h, s.Result == null ? -1 : (int)s.Result.Reason * 16 + s.Result.Criterion);
            Add(ref h, s.InSetup ? 1 : 0);
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                Add(ref h, s.IsSetupFinished(p) ? 1 : 0);
                Add(ref h, s.GetConsecutiveTimeouts(p));
                Add(ref h, s.GetDealtHandCount(p));
                AddIds(ref h, s.GetQuestOffer(p));
                AddIds(ref h, s.GetQuestChoices(p));
                AddIds(ref h, s.GetPassiveOffer(p));
                AddString(ref h, s.GetPassiveChoice(p) == null ? "" : s.GetPassiveChoice(p).Id);
                var tw = s.GetTower(p);
                Add(ref h, (tw.IsPlaced ? 1 : 0) + 2 * (tw.RevealedUntilTurn + 1));
                var pr = s.GetProgress(p);
                for (int i = 0; i < Data.Catalog.QuestPick; i++) Add(ref h, (int)pr.GetQuestStatus(i));
                Add(ref h, pr.Kills);
                if (includeFog) Add(ref h, pr.SeenKills); // visibility-derived like the fog: light states do not track it
                Add(ref h, pr.TowerDamage);
                Add(ref h, pr.TrapsSprung);
                Add(ref h, pr.UnitsLost);
                Add(ref h, pr.LastBreathDeathTurn);
                Add(ref h, (pr.PassiveRevealed ? 1 : 0) | (pr.LastBreathUsed ? 2 : 0) | (pr.LastBreathPending ? 4 : 0) | (pr.MerchantUsed ? 8 : 0));
                Add(ref h, (int)pr.LastBreathClass * 8 + (int)pr.LastBreathBiome);
                Add(ref h, pr.WallBlocked);
                Add(ref h, pr.PortalUnitId);
                foreach (var c in Board.Cells) Add(ref h, pr.GetHoldStreak(c));
                var fog = s.GetFog(p);
                foreach (var c in Board.Cells)
                {
                    if (!includeFog) break;
                    var seen = fog.GetLastSeen(c);
                    Add(ref h, (int)fog.Get(c));
                    Add(ref h, (int)seen.Tile.Biome * 8 + (int)seen.Tile.Marker);
                    Add(ref h, seen.UnitId);
                    Add(ref h, ((int)seen.UnitOwner * 8 + (int)seen.UnitClass) * 8 + (int)seen.UnitBiome);
                    Add(ref h, seen.UnitHealth);
                    Add(ref h, (seen.HasTower ? 1 : 0) + 2 * (int)seen.TowerOwner + 4 * seen.TowerHealth);
                }
            }
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
                Add(ref h, u.RevealedUntilTurn);
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
            AddString(ref h, s.PendingEvent == null ? "" : s.PendingEvent.Id);
            Add(ref h, s.PendingEventRound);
            Add(ref h, s.PendingEventCells.Count);
            foreach (var c in s.PendingEventCells)
            {
                Add(ref h, c.Q);
                Add(ref h, c.R);
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

        static void AddIds(ref ulong h, IReadOnlyList<Data.QuestDef> defs)
        {
            Add(ref h, defs.Count);
            foreach (var d in defs) AddString(ref h, d.Id);
        }

        static void AddIds(ref ulong h, IReadOnlyList<Data.PassiveDef> defs)
        {
            Add(ref h, defs.Count);
            foreach (var d in defs) AddString(ref h, d.Id);
        }

        static void AddString(ref ulong h, string s)
        {
            Add(ref h, s.Length);
            foreach (char ch in s) Add(ref h, ch);
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
