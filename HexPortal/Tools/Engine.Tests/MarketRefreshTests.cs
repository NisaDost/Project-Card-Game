using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD v4.0 D-05 Market refresh: automatic at every turn start right before the draw (T-04 step 4), and a manual refresh
    // for MarketRefreshCost Mana, own turn only, any number of times ("istediği kadar").
    public class MarketRefreshTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static readonly CardPool[] Slots = { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap };

        static GameState WithMarket(int manaA = 3)
        {
            var b = new TestBoard().FullPools().Mana(A, manaA);
            var s = b.Build();
            Pools.OpenMarket(s, new List<GameEvent>());
            return s;
        }

        static int CardCount(GameState s) =>
            Slots.Sum(p => s.GetPool(p).Count + (s.GetMarket(p) == null ? 0 : 1));

        [Test]
        public void D05_T04_TheMarketIsRefreshedAtEveryTurnStartRightBeforeTheDraw()
        {
            var s = WithMarket();
            int before = CardCount(s);
            var old = Slots.Select(p => s.GetMarket(p).Id).ToList();
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            int started = ev.FindIndex(e => e is TurnStarted);
            int refreshed = ev.FindIndex(e => e is MarketRefreshed);
            Assert.That(refreshed, Is.GreaterThan(started));
            Assert.That(((MarketRefreshed)ev[refreshed]).Player, Is.Null);
            Assert.That(ev.Skip(refreshed + 1).OfType<MarketRefilled>().Select(r => r.Slot), Is.EqualTo(Slots));
            Assert.That(s.IsDrawPending(B), Is.True);
            Assert.That(CardCount(s), Is.EqualTo(before), "old cards went back into their pools");
            foreach (var id in old)
                Assert.That(Slots.Any(p => s.GetPool(p).Any(c => c.Id == id) || s.GetMarket(p).Id == id), Is.True);
            foreach (var p in Slots) Assert.That(s.GetMarket(p).Pool, Is.EqualTo(p));
        }

        [Test]
        public void D05_ManualRefreshCostsOneManaAndMayBeRepeated()
        {
            var s = WithMarket(manaA: 2);
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new RefreshMarketCommand(A)));
            var ev = Engine.Apply(s, new RefreshMarketCommand(A));
            Assert.That(ev.OfType<MarketRefreshed>().Single().Player, Is.EqualTo((PlayerId?)A));
            Assert.That(ev.OfType<MarketRefilled>().Count(), Is.EqualTo(3));
            Engine.Apply(s, new RefreshMarketCommand(A));
            Assert.That(s.GetMana(A), Is.EqualTo(2 - 2 * Catalog.MarketRefreshCost));
            Assert.That(s.MarketRefreshesThisTurn, Is.EqualTo(2));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn), "no Energy");
            Assert.That(Engine.GetLegalCommands(s, A), Does.Not.Contain(new RefreshMarketCommand(A)));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new RefreshMarketCommand(A)));
        }

        [Test]
        public void D05_ManualRefreshOnlyOnTheOwnTurn()
        {
            var s = WithMarket();
            s.SetMana(B, 3);
            Assert.That(Engine.GetLegalCommands(s, B), Is.Empty);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new RefreshMarketCommand(B)));
        }

        [Test]
        public void D05_ManualRefreshIsAllowedBeforeTheTurnStartDraw()
        {
            var s = WithMarket();
            Engine.Apply(s, new EndTurnCommand(A)); // B: 2 Mana (T-02), draw pending
            var legal = Engine.GetLegalCommands(s, B);
            Assert.That(legal.Last(), Is.EqualTo(new RefreshMarketCommand(B)));
            Assert.That(legal.Take(legal.Count - 1).All(c => c is DrawCommand), Is.True);
            Engine.Apply(s, new RefreshMarketCommand(B));
            Assert.That(s.IsDrawPending(B), Is.True);
            Engine.Apply(s, new DrawCommand(B, (int)CardPool.Buff));
            Assert.That(s.IsDrawPending(B), Is.False);
            Assert.That(s.MarketRefreshesThisTurn, Is.EqualTo(1));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.MarketRefreshesThisTurn, Is.EqualTo(0));
        }

        [Test]
        public void D05_V09_RefreshEventsArePublic()
        {
            var s = WithMarket();
            var ev = Engine.Apply(s, new RefreshMarketCommand(A));
            foreach (var p in new[] { A, B })
            {
                var seen = EventFilter.For(ev, p);
                Assert.That(seen.OfType<MarketRefreshed>().Count(), Is.EqualTo(1));
                Assert.That(seen.OfType<MarketRefilled>().Count(), Is.EqualTo(3));
            }
            Assert.That(PlayerView.For(s, B).MarketRefreshesThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void D05_RefreshIsDeterministic()
        {
            var s1 = WithMarket();
            var s2 = WithMarket();
            Engine.Apply(s1, new RefreshMarketCommand(A));
            Engine.Apply(s2, new RefreshMarketCommand(A));
            Assert.That(StateHash.Compute(s1), Is.EqualTo(StateHash.Compute(s2)));
            Assert.That(Slots.Select(p => s1.GetMarket(p).Id), Is.EqualTo(Slots.Select(p => s2.GetMarket(p).Id)));
        }
    }
}
