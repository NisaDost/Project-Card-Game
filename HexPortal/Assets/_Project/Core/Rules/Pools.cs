using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>§5 pools, dealing and Market (D-01…D-07), the turn-start draw (T-04 step 4), pre-pick (T-11)
    /// and the death draw (U-23). All randomness comes from the match Rng.</summary>
    public static class Pools
    {
        static readonly CardPool[] Slots = { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap };

        /// <summary>D-03, D-04 for A then B. Dealt cards ignore the hand limit (D-07). Called by the setup phase.</summary>
        public static void Deal(GameState state)
        {
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                // D-03: one character per class; the biome variant is chosen uniformly among the variants left.
                foreach (var def in Catalog.Units)
                    for (int k = 0; k < Catalog.DealCharactersPerClass; k++)
                    {
                        var pool = state.PoolList(CardPool.Character);
                        var biomes = new List<Biome>();
                        foreach (var c in pool)
                            if (c.Character.Class == def.Class && !biomes.Contains(c.Character.Biome)) biomes.Add(c.Character.Biome);
                        if (biomes.Count == 0) continue;
                        var biome = biomes[state.Rng.NextInt(biomes.Count)];
                        foreach (var c in pool)
                            if (c.Character.Class == def.Class && c.Character.Biome == biome)
                            {
                                pool.Remove(c);
                                state.HandList(p).Add(c);
                                break;
                            }
                    }
                // D-04: common then rare support cards, uniformly over the copies left in both support pools. No epics.
                DealSupport(state, p, Rarity.Common, Catalog.DealCommonSupport);
                DealSupport(state, p, Rarity.Rare, Catalog.DealRareSupport);
            }
        }

        static void DealSupport(GameState state, PlayerId p, Rarity rarity, int count)
        {
            for (int k = 0; k < count; k++)
            {
                var candidates = new List<CardInstance>();
                foreach (var pool in new[] { CardPool.Buff, CardPool.DebuffTrap })
                    foreach (var c in state.PoolList(pool))
                        if (c.Support.Rarity == rarity && Catalog.IsSupportDealtAtSetup(c.Support)) candidates.Add(c);
                if (candidates.Count == 0) return;
                var card = candidates[state.Rng.NextInt(candidates.Count)];
                state.PoolList(card.Pool).Remove(card);
                state.HandList(p).Add(card);
            }
        }

        /// <summary>D-05: fill the three slots, each from its own pool. Called by the setup phase after dealing.</summary>
        public static void OpenMarket(GameState state, List<GameEvent> events) => OpenMarket(state, new EventLog(state, events));

        public static void OpenMarket(GameState state, EventLog events)
        {
            foreach (var slot in Slots) Refill(state, slot, events);
        }

        // D-05: a random card of the slot's own pool; the slot stays empty if that pool is empty.
        static void Refill(GameState state, CardPool slot, EventLog events)
        {
            var pool = state.PoolList(slot);
            CardInstance card = null;
            if (pool.Count > 0)
            {
                int i = state.Rng.NextInt(pool.Count);
                card = pool[i];
                pool.RemoveAt(i);
            }
            state.SetMarket(slot, card);
            events.Add(new MarketRefilled(slot, card == null ? 0 : card.Id));
        }

        public static bool CanBlindDraw(GameState state)
        {
            foreach (var slot in Slots)
                if (state.GetPool(slot).Count > 0) return true;
            return false;
        }

        /// <summary>T-04 step 4 choices, in order: each non-empty Market slot, then Blind if any pool has a card.</summary>
        public static List<int> DrawOptions(GameState state)
        {
            var options = new List<int>();
            foreach (var slot in Slots)
                if (state.GetMarket(slot) != null) options.Add((int)slot);
            if (CanBlindDraw(state)) options.Add(DrawCommand.Blind);
            return options;
        }

        /// <summary>T-11 choices: the draw options now, plus None (clear).</summary>
        public static List<int> PrePickOptions(GameState state)
        {
            var options = DrawOptions(state);
            options.Add(PrePickCommand.None);
            return options;
        }

        public static bool IsHandFull(GameState state, PlayerId p) => state.GetHand(p).Count >= Catalog.HandLimit;

        /// <summary>A draw option (validated by the caller).</summary>
        internal static void Draw(GameState state, PlayerId p, int slot, EventLog events)
        {
            if (slot == DrawCommand.Blind) BlindDraw(state, p, events);
            else TakeFromMarket(state, p, (CardPool)slot, events);
        }

        // D-06: uniform over the union of the three pools. Market cards are not in the pools.
        static void BlindDraw(GameState state, PlayerId p, EventLog events)
        {
            int total = 0;
            foreach (var slot in Slots) total += state.GetPool(slot).Count;
            int i = state.Rng.NextInt(total);
            foreach (var slot in Slots)
            {
                var pool = state.PoolList(slot);
                if (i >= pool.Count)
                {
                    i -= pool.Count;
                    continue;
                }
                var card = pool[i];
                pool.RemoveAt(i);
                state.HandList(p).Add(card);
                events.Add(new CardDrawn(p, card.Id, DrawSource.Blind));
                return;
            }
        }

        static void TakeFromMarket(GameState state, PlayerId p, CardPool slot, EventLog events)
        {
            var card = state.GetMarket(slot);
            state.HandList(p).Add(card);
            events.Add(new CardDrawn(p, card.Id, DrawSource.Market));
            Refill(state, slot, events);
            Passives.OnMarketBuy(state, p, events);
        }

        /// <summary>T-04 step 4 and T-11. A valid pre-pick is applied; a Market pre-pick whose slot now holds another
        /// card (or nothing) is void. Otherwise the player must choose (IsDrawPending). Skipped when the hand is full
        /// or there is nothing to draw (D-07). The pre-pick is always cleared.</summary>
        internal static void TurnStartDraw(GameState state, PlayerId p, EventLog events)
        {
            int slot = state.GetPrePickSlot(p);
            int cardId = state.GetPrePickCardId(p);
            state.SetPrePick(p, PrePickCommand.None, 0);
            state.SetDrawPending(p, false);
            if (IsHandFull(state, p) || DrawOptions(state).Count == 0) return;
            if (slot == DrawCommand.Blind && CanBlindDraw(state))
            {
                BlindDraw(state, p, events);
                return;
            }
            if (slot >= 0)
            {
                var card = state.GetMarket((CardPool)slot);
                if (card != null && card.Id == cardId)
                {
                    TakeFromMarket(state, p, (CardPool)slot, events);
                    return;
                }
            }
            state.SetDrawPending(p, true);
        }

        /// <summary>T-11: store the waiting player's choice (validated by the caller), with the Market card it saw.</summary>
        internal static void SetPrePick(GameState state, PlayerId p, int slot)
        {
            var card = slot >= 0 ? state.GetMarket((CardPool)slot) : null;
            state.SetPrePick(p, slot, card == null ? 0 : card.Id);
        }

        /// <summary>U-23: the owner of a unit that died draws Catalog.DeathDrawCount blind cards, unless the hand is full.</summary>
        internal static void DeathDraw(GameState state, PlayerId owner, EventLog events)
        {
            for (int k = 0; k < Catalog.DeathDrawCount; k++)
                if (!IsHandFull(state, owner) && CanBlindDraw(state)) BlindDraw(state, owner, events);
        }
    }
}
