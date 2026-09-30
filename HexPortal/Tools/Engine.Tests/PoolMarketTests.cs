using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §5: pools (D-01, D-02), dealing (D-03, D-04), Market (D-05), blind draw (D-06), hand limit (D-07).
    public class PoolMarketTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static readonly UnitClass[] Classes =
            { UnitClass.Guardian, UnitClass.Rider, UnitClass.Archer, UnitClass.Mage, UnitClass.Healer };

        static GameState NewState(ulong seed) =>
            new GameState(new TestBoard().State.Map, TestBoard.DefaultTowerA, TestBoard.DefaultTowerB, seed);

        static IEnumerable<CardInstance> AllPoolCards(GameState s) =>
            s.GetPool(CardPool.Character).Concat(s.GetPool(CardPool.Buff)).Concat(s.GetPool(CardPool.DebuffTrap));

        [Test]
        public void D01_D02_NewGameHasFullPools()
        {
            var s = NewState(1);
            Assert.That(s.GetPool(CardPool.Character).Count, Is.EqualTo(30));
            Assert.That(s.GetPool(CardPool.Buff).Count, Is.EqualTo(19));
            Assert.That(s.GetPool(CardPool.DebuffTrap).Count, Is.EqualTo(15));
            foreach (var d in Catalog.CharacterCards)
                Assert.That(s.GetPool(CardPool.Character).Count(c => c.DefId == d.Id), Is.EqualTo(Catalog.CharacterCopies));
            foreach (var d in Catalog.SupportCards)
            {
                var pool = d.Pool == SupportPool.Buff ? CardPool.Buff : CardPool.DebuffTrap;
                Assert.That(s.GetPool(pool).Count(c => c.DefId == d.Id), Is.EqualTo(d.Copies), d.Id);
            }
            var ids = AllPoolCards(s).Select(c => c.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            Assert.That(s.GetHand(A), Is.Empty);
            Assert.That(s.GetMarket(CardPool.Character), Is.Null);
        }

        [Test]
        public void D03_DealGivesOneCharacterPerClass()
        {
            var s = NewState(7);
            Pools.Deal(s);
            foreach (var p in new[] { A, B })
            {
                var chars = s.GetHand(p).Where(c => c.IsCharacter).Select(c => c.Character.Class).ToList();
                Assert.That(chars, Is.EquivalentTo(Classes));
            }
            Assert.That(s.GetPool(CardPool.Character).Count, Is.EqualTo(20));
        }

        [Test]
        public void D03_BiomeIsRandom_DealIsDeterministicPerSeed()
        {
            string Hands(ulong seed)
            {
                var s = NewState(seed);
                Pools.Deal(s);
                return string.Join(",", s.GetHand(A).Concat(s.GetHand(B)).Select(c => c.DefId));
            }
            Assert.That(Hands(3), Is.EqualTo(Hands(3)));
            var biomes = new HashSet<Biome>();
            for (ulong seed = 0; seed < 30; seed++)
            {
                var s = NewState(seed);
                Pools.Deal(s);
                biomes.Add(s.GetHand(A).First(c => c.IsCharacter && c.Character.Class == UnitClass.Guardian).Character.Biome);
            }
            Assert.That(biomes, Is.EquivalentTo(new[] { Biome.Forest, Biome.Desert, Biome.Snow }));
        }

        [Test]
        public void D04_DealGivesThreeCommonAndOneRare_NoEpics()
        {
            for (ulong seed = 0; seed < 20; seed++)
            {
                var s = NewState(seed);
                Pools.Deal(s);
                foreach (var p in new[] { A, B })
                {
                    var support = s.GetHand(p).Where(c => !c.IsCharacter).Select(c => c.Support.Rarity).ToList();
                    Assert.That(support.Count(r => r == Rarity.Common), Is.EqualTo(Catalog.DealCommonSupport));
                    Assert.That(support.Count(r => r == Rarity.Rare), Is.EqualTo(Catalog.DealRareSupport));
                    Assert.That(support.Count(r => r == Rarity.Epic), Is.EqualTo(0));
                }
                Assert.That(s.GetPool(CardPool.Buff).Count + s.GetPool(CardPool.DebuffTrap).Count, Is.EqualTo(34 - 8));
            }
        }

        [Test]
        public void D07_DealtCardsIgnoreTheHandLimit()
        {
            var s = NewState(2);
            Pools.Deal(s);
            Assert.That(s.GetHand(A).Count, Is.EqualTo(9));
            Assert.That(s.GetHand(A).Count, Is.GreaterThan(Catalog.HandLimit));
        }

        [Test]
        public void D05_OpenMarketFillsEachSlotFromItsOwnPool()
        {
            var s = NewState(4);
            Pools.Deal(s);
            var ev = new List<GameEvent>();
            Pools.OpenMarket(s, ev);
            Assert.That(s.GetMarket(CardPool.Character).IsCharacter, Is.True);
            Assert.That(s.GetMarket(CardPool.Buff).Support.Pool, Is.EqualTo(SupportPool.Buff));
            Assert.That(s.GetMarket(CardPool.DebuffTrap).Support.Pool, Is.EqualTo(SupportPool.DebuffTrap));
            Assert.That(ev.OfType<MarketRefilled>().Count(), Is.EqualTo(3));
            Assert.That(s.GetPool(CardPool.Character).Count, Is.EqualTo(19));
            Assert.That(AllPoolCards(s).Any(c => c.Id == s.GetMarket(CardPool.Buff).Id), Is.False);
        }

        [Test]
        public void D05_TakenSlotRefillsFromSamePool_EmptyWhenPoolEmpty()
        {
            var b = new TestBoard();
            int first = b.Market("C-10");
            int next = b.PoolCard("C-12");
            for (int i = 0; i < Catalog.HandLimit; i++) b.Hand(A, "C-13"); // A's hand is full: A never draws here
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(A));
            var ev = Engine.Apply(s, new DrawCommand(B, (int)CardPool.Buff));
            Assert.That(s.GetHand(B).Single().Id, Is.EqualTo(first));
            Assert.That(ev.OfType<CardDrawn>().Single().Source, Is.EqualTo(DrawSource.Market));
            Assert.That(s.GetMarket(CardPool.Buff).Id, Is.EqualTo(next));
            var refill = ev.OfType<MarketRefilled>().Single();
            Assert.That((refill.Slot, refill.CardId), Is.EqualTo((CardPool.Buff, next)));
            Assert.That(s.GetPool(CardPool.Buff), Is.Empty);

            Engine.Apply(s, new EndTurnCommand(B));
            Engine.Apply(s, new EndTurnCommand(A));
            ev = Engine.Apply(s, new DrawCommand(B, (int)CardPool.Buff));
            Assert.That(s.GetMarket(CardPool.Buff), Is.Null);
            Assert.That(ev.OfType<MarketRefilled>().Single().CardId, Is.EqualTo(0));
        }

        [Test]
        public void D06_BlindDrawComesFromThePoolsNeverTheMarket()
        {
            var b = new TestBoard();
            int m = b.Market("C-10");
            int pooled = b.PoolCard("C-20");
            var s = b.Build();
            Engine.Apply(s, new EndTurnCommand(A));
            var ev = Engine.Apply(s, new DrawCommand(B, DrawCommand.Blind));
            Assert.That(s.GetHand(B).Single().Id, Is.EqualTo(pooled));
            Assert.That(ev.OfType<CardDrawn>().Single().Source, Is.EqualTo(DrawSource.Blind));
            Assert.That(s.GetMarket(CardPool.Buff).Id, Is.EqualTo(m));
            Assert.That(ev.OfType<MarketRefilled>(), Is.Empty);
        }

        [Test]
        public void D06_BlindDrawCoversTheUnionOfAllThreePools()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 0; seed < 40; seed++)
            {
                var b = new TestBoard(seed: seed);
                b.PoolCard("U-05-Snow");
                b.PoolCard("C-14");
                b.PoolCard("C-19");
                var s = b.Build();
                Engine.Apply(s, new EndTurnCommand(A));
                Assert.That(Engine.GetLegalCommands(s, B), Is.EqualTo(new ICommand[] { new DrawCommand(B, DrawCommand.Blind) }));
                Engine.Apply(s, new DrawCommand(B, DrawCommand.Blind));
                seen.Add(s.GetHand(B).Single().DefId);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { "U-05-Snow", "C-14", "C-19" }));
        }

        [Test]
        public void Rng_MatchStreamIsSeparateFromMapStream()
        {
            Assert.That(MapGenerator.MatchStreamSalt, Is.Not.EqualTo(MapGenerator.MapStreamSalt));
            var s = NewState(5);
            Assert.That(s.Rng.Clone().NextULong(), Is.EqualTo(new Rng(5 ^ MapGenerator.MatchStreamSalt).NextULong()));
            Assert.That(s.Rng.Clone().NextULong(), Is.Not.EqualTo(new Rng(5 ^ MapGenerator.MapStreamSalt).NextULong()));
        }
    }
}
