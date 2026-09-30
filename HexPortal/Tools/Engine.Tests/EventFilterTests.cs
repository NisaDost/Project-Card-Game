using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // V-05, V-07, V-09, V-10, C-33, C-35 (v2.7), T-11: events are tagged at emission with who may see them.
    public class EventFilterTests
    {
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void V10_MoveEntirelyInFog_NothingForTheOpponent()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Guardian, A, H(-1, 2));
            int e = b.Unit(UnitClass.Guardian, B, H(0, -2));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, e, H(1, -2)));
            Assert.That(EventFilter.For(ev, A), Is.Empty);
            Assert.That(EventFilter.For(ev, B).OfType<UnitMoved>().Single().UnitId, Is.EqualTo(e));
            Assert.That(ev.Count, Is.EqualTo(1)); // Apply returns the full list
        }

        [Test]
        public void V10_MoveFromFogIntoSight_AppearsWithoutOrigin()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int r = b.Unit(UnitClass.Rider, B, H(1, -3));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, r, H(0, -1)));
            var seen = EventFilter.For(ev, A);
            Assert.That(seen.OfType<UnitMoved>(), Is.Empty);
            var appeared = seen.OfType<UnitAppeared>().Single();
            Assert.That((appeared.UnitId, appeared.Owner, appeared.Class, appeared.Biome, appeared.Health, appeared.Cell),
                Is.EqualTo((r, B, UnitClass.Rider, Biome.Desert, 4, H(0, -1))));
            Assert.That(MatchHelper.Serialize(seen), Does.Not.Contain(H(1, -3).ToString()));
        }

        [Test]
        public void V10_MoveOutOfSight_Vanishes()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int r = b.Unit(UnitClass.Rider, B, H(0, -1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, r, H(1, -3)));
            var seen = EventFilter.For(ev, A);
            var gone = seen.OfType<UnitVanished>().Single();
            Assert.That((gone.UnitId, gone.Cell), Is.EqualTo((r, H(0, -1))));
            Assert.That(MatchHelper.Serialize(seen), Does.Not.Contain(H(1, -3).ToString()));
        }

        [Test]
        public void C33_TrapPlacedAndRemovedAreOwnerOnly_TriggeredIsPublic()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Guardian, A, H(-1, 2));
            int card = b.Hand(A, "C-20");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, card, H(-1, 1)));
            Assert.That(EventFilter.For(ev, B), Is.Empty);
            Assert.That(EventFilter.For(ev, A).OfType<TrapPlaced>().Single().CardId, Is.EqualTo(card));

            var removed = new System.Collections.Generic.List<GameEvent>();
            Traps.RemoveAt(s, H(-1, 1), removed); // C-35 (v2.7): only the owner learns it
            Assert.That(EventFilter.For(removed, A).OfType<TrapRemoved>().Count(), Is.EqualTo(1));
            Assert.That(EventFilter.For(removed, B), Is.Empty);
        }

        [Test]
        public void C33_TriggeredTrapIsShownToBoth_HiddenVictimRedactedForTheOwner()
        {
            var b = new TestBoard().Active(B);
            b.Unit(UnitClass.Guardian, A, H(-1, 2));
            int card = b.Trap(A, "C-20", H(1, -2)); // far from A's sight
            int e = b.Unit(UnitClass.Guardian, B, H(1, -3));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, e, H(1, -2)));
            var forA = EventFilter.For(ev, A);
            var trig = forA.OfType<TrapTriggered>().Single();
            Assert.That((trig.Owner, trig.CardId, trig.Cell, trig.UnitId), Is.EqualTo((A, card, H(1, -2), 0)));
            Assert.That(forA.OfType<DamageDealt>(), Is.Empty);
            var forB = EventFilter.For(ev, B);
            Assert.That(forB.OfType<TrapTriggered>().Single().UnitId, Is.EqualTo(e));
            Assert.That(forB.OfType<DamageDealt>().Single().Amount, Is.EqualTo(3));
        }

        [Test]
        public void V09_BlindDrawIsCountOnlyForTheOpponent_MarketDrawIsPublic()
        {
            var b = new TestBoard();
            b.PoolCard("C-10");
            int market = b.Market("C-12");
            var s = b.Build();
            s.SetDrawPending(A, true);
            var blind = Engine.Apply(s, new DrawCommand(A, DrawCommand.Blind));
            var mine = EventFilter.For(blind, A).OfType<CardDrawn>().Single();
            var theirs = EventFilter.For(blind, B).OfType<CardDrawn>().Single();
            Assert.That(mine.CardId, Is.GreaterThan(0));
            Assert.That((theirs.Player, theirs.CardId, theirs.Source), Is.EqualTo((A, 0, DrawSource.Blind)));

            Engine.Apply(s, new EndTurnCommand(A));
            s.SetDrawPending(B, true);
            var take = Engine.Apply(s, new DrawCommand(B, (int)CardPool.Buff));
            Assert.That(EventFilter.For(take, A).OfType<CardDrawn>().Single().CardId, Is.EqualTo(market));
        }

        [Test]
        public void T11_PrePickIsOwnerOnly()
        {
            var b = new TestBoard();
            b.PoolCard("C-10");
            var s = b.Build();
            var ev = Engine.Apply(s, new PrePickCommand(B, DrawCommand.Blind));
            Assert.That(EventFilter.For(ev, B).OfType<PrePickSet>().Single().Slot, Is.EqualTo(DrawCommand.Blind));
            Assert.That(EventFilter.For(ev, A), Is.Empty);
        }

        [Test]
        public void V08_SplashOnHiddenUnit_OnlyTheDeathDrawCountReachesTheAttacker()
        {
            var b = new TestBoard();
            int m = b.Unit(UnitClass.Mage, A, H(-1, 1));
            b.Unit(UnitClass.Guardian, B, H(-1, -1));
            int hidden = b.Unit(UnitClass.Healer, B, H(0, -2), health: 1);
            b.PoolCard("C-10");
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, m, H(-1, -1)));
            Assert.That(ev.OfType<UnitDied>().Single().UnitId, Is.EqualTo(hidden));
            var forA = EventFilter.For(ev, A);
            Assert.That(forA.OfType<UnitDied>(), Is.Empty);
            Assert.That(forA.SelectMany(UnitIds), Does.Not.Contain(hidden));
            var draw = forA.OfType<CardDrawn>().Single();
            Assert.That((draw.Player, draw.CardId), Is.EqualTo((B, 0)));
            Assert.That(EventFilter.For(ev, B).OfType<UnitDied>().Single().UnitId, Is.EqualTo(hidden));
        }

        [Test]
        public void V09_DamageToAnUnseenTowerShowsOnlyItsHealth()
        {
            var b = new TestBoard().Tower(B, H(0, -2));
            int m = b.Unit(UnitClass.Mage, A, H(-1, 1));
            b.Unit(UnitClass.Guardian, B, H(-1, -1)); // next to B's tower, distance 3 from the Mage
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, m, H(-1, -1)));
            var forA = EventFilter.For(ev, A);
            Assert.That(forA.OfType<DamageDealt>().Where(d => d.TargetUnitId == DamageDealt.Tower), Is.Empty);
            var hp = forA.OfType<TowerHealthChanged>().Single();
            Assert.That((hp.Owner, hp.Health), Is.EqualTo((B, Catalog.Tower.Health - Catalog.MageSplashDamage)));
            Assert.That(MatchHelper.Serialize(forA), Does.Not.Contain(H(0, -2).ToString()));
            Assert.That(PlayerView.For(s, A).EnemyTowerPos, Is.Null);
            Assert.That(PlayerView.For(s, A).EnemyTowerHealth, Is.EqualTo(hp.Health));
            Assert.That(EventFilter.For(ev, B).OfType<DamageDealt>().Count(d => d.TargetUnitId == DamageDealt.Tower), Is.EqualTo(1));
        }

        [Test]
        public void V05_CardsOnUnseenUnitsAreHidden_DebuffOnVisibleEnemyIsShown()
        {
            var b = new TestBoard().Mana(A, 3);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int e = b.Unit(UnitClass.Archer, B, H(-1, 0));
            int buff = b.Hand(A, "C-10");
            int debuff = b.Hand(A, "C-16");
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, B).Contains(H(-1, 1)), Is.True); // the Archer sees the Guardian
            var ev1 = Engine.Apply(s, new PlayCardCommand(A, debuff, H(-1, 0)));
            Assert.That(EventFilter.For(ev1, B).Select(x => x.GetType()), Is.EqualTo(new[] { typeof(CardPlayed), typeof(EffectApplied) }));

            // A buff on a unit B cannot see: nothing for B.
            var b2 = new TestBoard().Mana(A, 1);
            int g2 = b2.Unit(UnitClass.Guardian, A, H(-1, 1));
            b2.Unit(UnitClass.Guardian, B, H(1, -3));
            int buff2 = b2.Hand(A, "C-10");
            var s2 = b2.Build();
            var ev2 = Engine.Apply(s2, new PlayCardCommand(A, buff2, H(-1, 1)));
            Assert.That(EventFilter.For(ev2, B), Is.Empty);
            Assert.That(EventFilter.For(ev2, A).Count, Is.EqualTo(2));
        }

        [Test]
        public void V09_TurnFlowAndGameOverArePublic()
        {
            var b = new TestBoard().Tower(B, H(2, -2), 2);
            int a = b.Unit(UnitClass.Archer, A, H(0, -2));
            var s = b.Build();
            var end = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(EventFilter.For(end, A).OfType<TurnStarted>().Single().Player, Is.EqualTo(B));
            Assert.That(EventFilter.For(end, B).OfType<TurnStarted>().Single().Player, Is.EqualTo(B));
            Engine.Apply(s, new EndTurnCommand(B));
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(2, -2)));
            foreach (var p in Players)
            {
                var over = EventFilter.For(ev, p).OfType<GameOver>().Single();
                Assert.That((over.Winner, over.Result.Reason), Is.EqualTo(((PlayerId?)A, WinReason.Tower)));
                Assert.That(EventFilter.For(ev, p).OfType<TowerDestroyed>().Count(), Is.EqualTo(1));
            }
        }
    }
}
