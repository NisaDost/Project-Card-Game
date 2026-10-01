using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §9 commander passives: P-00…P-06 with the v2.7/v2.8 decisions (P-01 new unit, retry, trap but no shots;
    // P-02 +1 also on the Mirror Trap, damage before the teleport; P-06 on every arrival, Shield blocks, before shots).
    public class PassiveTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static int MaxHp(UnitClass c) => Catalog.Units.Single(u => u.Class == c).Health;

        /// <summary>Ends the active player's turn (a pending draw is made blind first).</summary>
        static List<GameEvent> End(GameState s)
        {
            var p = s.ActivePlayer;
            var ev = new List<GameEvent>();
            if (s.IsDrawPending(p)) ev.AddRange(Engine.Apply(s, new DrawCommand(p, DrawCommand.Blind)));
            ev.AddRange(Engine.Apply(s, new EndTurnCommand(p)));
            return ev;
        }

        /// <summary>Rock on every home cell of p except <paramref name="free"/> and p's tower.</summary>
        static TestBoard RockHomeExcept(TestBoard b, PlayerId p, Hex free, Hex tower)
        {
            foreach (var h in Board.Cells.Where(h => Board.IsHomeZone(h, p)))
                if (h != free && h != tower) b.Rock(h);
            return b;
        }

        static void AssertRevealed(List<GameEvent> ev, PlayerId owner, string id, GameState s)
        {
            foreach (var p in new[] { A, B })
            {
                var r = EventFilter.For(ev, p).OfType<PassiveRevealed>().Single();
                Assert.That((r.Player, r.PassiveId), Is.EqualTo((owner, id)));
            }
            Assert.That(s.GetProgress(owner).PassiveRevealed, Is.True);
            Assert.That(PlayerView.For(s, owner.Opponent()).OpponentPassive, Is.EqualTo(id));
            Assert.That(PlayerView.For(s, owner).PassiveRevealed, Is.True);
        }

        // ---------- P-00, P-03 ----------

        [Test]
        public void P00_P03_QuickStartGivesTwoManaInRoundThree_AndIsRevealedThen()
        {
            var s = new TestBoard().Round(2).Active(B).Passive(A, "P-03").Build();
            Assert.That(PlayerView.For(s, B).OpponentPassive, Is.Null);
            Assert.That(PlayerView.For(s, A).PassiveRevealed, Is.False);
            var ev = End(s); // round 3, A's turn start (T-04 step 1)
            Assert.That(s.GetMana(A), Is.EqualTo(3 + 2));
            AssertRevealed(ev, A, "P-03", s);
            End(s);
            ev = End(s); // round 4
            Assert.That(s.GetMana(A), Is.EqualTo(4));
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
        }

        [Test]
        public void P03_MayExceedTheManaCap_OwnerOnly()
        {
            var b = new TestBoard().Round(3).Passive(B, "P-03").Wellspring(H(0, -2)).Wellspring(H(1, -2));
            b.Unit(UnitClass.Guardian, B, H(0, -2));
            b.Unit(UnitClass.Guardian, B, H(1, -2));
            var s = b.Build();
            End(s); // B's turn in round 3
            Assert.That(s.GetMana(B), Is.EqualTo(3 + 2 * Catalog.WellspringManaBonus + 2));
            Assert.That(s.GetMana(B), Is.GreaterThan(Catalog.ManaCap));
            End(s);
            Assert.That(s.GetMana(A), Is.EqualTo(4)); // A has no passive
        }

        // ---------- P-01 ----------

        [Test]
        public void P01_FirstDeadUnitReturnsAsANewUnitAtTheNextOwnTurnStart_Once()
        {
            var free = H(0, 3);
            var b = RockHomeExcept(new TestBoard().Round(3).Active(B).Passive(A, "P-01"), A, free, TestBoard.DefaultTowerA);
            int victim = b.Unit(UnitClass.Rider, A, H(0, 1), Biome.Snow, health: 1);
            b.Effect(victim, "C-11");
            int second = b.Unit(UnitClass.Archer, A, H(1, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            int mage = b.Unit(UnitClass.Mage, B, H(2, -1));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            Engine.Apply(s, new AttackCommand(B, mage, H(1, 1))); // a second death: only the first one returns
            Assert.That(s.GetUnit(second), Is.Null);
            Assert.That(s.GetProgress(A).PassiveRevealed, Is.False); // P-00: not yet taken effect
            Assert.That(PlayerView.For(s, B).OpponentPassive, Is.Null);

            var ev = End(s); // A's turn start (T-04 step 2)
            AssertRevealed(ev, A, "P-01", s);
            var rev = ev.OfType<UnitRevived>().Single();
            var u = s.GetUnit(rev.UnitId);
            Assert.That(rev.UnitId, Is.Not.EqualTo(victim));
            Assert.That((rev.Owner, rev.Class, rev.Biome, rev.Cell), Is.EqualTo((A, UnitClass.Rider, Biome.Snow, free)));
            Assert.That((u.Pos, u.Health, u.Buff == null, u.Debuff == null), Is.EqualTo((free, 2, true, true)));
            Assert.That(ev.FindIndex(e => e is TurnStarted), Is.LessThan(ev.FindIndex(e => e is UnitRevived)));
            Assert.That(Turn.CanAct(s, u), Is.False); // v2.9: like T-07
            Assert.That(s.GetProgress(A).LastBreathPending, Is.False);
            End(s);
            ev = End(s);
            Assert.That(ev.OfType<UnitRevived>(), Is.Empty);
        }

        [Test]
        public void P01_TheReturnedUnitCannotActInTheTurnItReturns_ButCanInTheNext()
        {
            var free = H(0, 3);
            var b = RockHomeExcept(new TestBoard().Round(3).Active(B).Passive(A, "P-01"), A, free, TestBoard.DefaultTowerA);
            b.Unit(UnitClass.Archer, A, H(0, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            var ev = End(s);
            var u = s.GetUnit(ev.OfType<UnitRevived>().Single().UnitId);
            Assert.That(Turn.CanAct(s, u), Is.False); // v2.9, like T-07
            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal.OfType<MoveCommand>().Any(m => m.UnitId == u.Id), Is.False);
            Assert.That(legal.OfType<OverwatchCommand>().Any(m => m.UnitId == u.Id), Is.False);
            End(s);
            End(s);
            Assert.That(Turn.CanAct(s, u), Is.True);
        }

        [Test]
        public void P01_APoisonDeathAtTheOwnersTurnStartReturnsOnlyAtTheNextOwnTurnStart()
        {
            var b = new TestBoard().Round(3).Active(B).Passive(A, "P-01");
            int u = b.Unit(UnitClass.Mage, A, H(0, 1), health: 1);
            b.Effect(u, "C-17");
            var s = b.Build();
            var ev = End(s); // A's turn start: the poison kills it (T-04 step 2)
            Assert.That(ev.OfType<UnitDied>().Single().UnitId, Is.EqualTo(u));
            Assert.That(ev.OfType<UnitRevived>(), Is.Empty); // v2.10: not in the same turn start
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
            Assert.That(s.GetProgress(A).LastBreathPending, Is.True);
            End(s);
            ev = End(s); // A's next turn start
            var rev = ev.OfType<UnitRevived>().Single();
            Assert.That((rev.Class, Board.IsHomeZone(rev.Cell, A)), Is.EqualTo((UnitClass.Mage, true)));
        }

        [Test]
        public void P01_NoEmptyHomeCell_RetriedAtLaterOwnTurnStarts()
        {
            var free = H(0, 3);
            var b = RockHomeExcept(new TestBoard().Round(3).Active(B).Passive(A, "P-01"), A, free, TestBoard.DefaultTowerA);
            int blocker = b.Unit(UnitClass.Guardian, A, free);
            b.Unit(UnitClass.Archer, A, H(0, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            var ev = End(s);
            Assert.That(ev.OfType<UnitRevived>(), Is.Empty);
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
            Assert.That(s.GetProgress(A).LastBreathPending, Is.True);
            Engine.Apply(s, new MoveCommand(A, blocker, H(0, 2)));
            End(s);
            ev = End(s);
            Assert.That(ev.OfType<UnitRevived>().Single().Cell, Is.EqualTo(free));
            Assert.That(ev.OfType<PassiveRevealed>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void P01_ReturnTriggersAnEnemyTrap()
        {
            var free = H(0, 3);
            var b = RockHomeExcept(new TestBoard().Round(3).Active(B).Passive(A, "P-01"), A, free, TestBoard.DefaultTowerA);
            b.Unit(UnitClass.Archer, A, H(0, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            int trap = b.Trap(B, "C-20", free);
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            var ev = End(s);
            var rev = ev.OfType<UnitRevived>().Single();
            var t = ev.OfType<TrapTriggered>().Single();
            Assert.That((t.CardId, t.UnitId), Is.EqualTo((trap, rev.UnitId)));
            var died = ev.OfType<UnitDied>().Single(); // 3 damage on 2 Health
            Assert.That((died.UnitId, died.Killer), Is.EqualTo((rev.UnitId, B)));
        }

        [Test]
        public void P01_ReturnNeverTriggersTowerOrOverwatchShots()
        {
            var free = H(0, 3);
            var b = RockHomeExcept(new TestBoard().Round(3).Active(B).Passive(A, "P-01"), A, free, TestBoard.DefaultTowerA)
                .Tower(B, H(1, 2)); // 1 cell from the return cell
            b.Unit(UnitClass.Archer, A, H(0, 1), health: 1);
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            int watcher = b.Unit(UnitClass.Archer, B, H(2, 1)); // straight line (2,1) -> (1,2) -> (0,3)
            b.U(watcher).OnOverwatch = true;
            var s = b.Build();
            Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            var ev = End(s);
            Assert.That(ev.OfType<UnitRevived>().Count(), Is.EqualTo(1));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
        }

        // ---------- P-02 ----------

        [Test]
        public void P02_TrapMasterAddsOneDamage()
        {
            var b = new TestBoard().Round(3).Active(B).Passive(A, "P-02");
            int g = b.Unit(UnitClass.Guardian, B, H(2, -2));
            b.Trap(A, "C-20", H(1, -2));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, g, H(1, -2)));
            AssertRevealed(ev, A, "P-02", s);
            var dmg = ev.OfType<DamageDealt>().Single();
            Assert.That((dmg.Kind, dmg.Amount, dmg.SourcePlayer), Is.EqualTo((DamageKind.Trap, 3 + 1, A)));
            Assert.That(ev.FindIndex(e => e is PassiveRevealed), Is.LessThan(ev.FindIndex(e => e is DamageDealt)));
        }

        [Test]
        public void P02_MirrorTrapDealsOneDamageBeforeTheTeleport_TeleportOnlyIfAlive()
        {
            var b = new TestBoard().Round(3).Active(B).Passive(A, "P-02");
            int tough = b.Unit(UnitClass.Guardian, B, H(2, -2), health: 2);
            int weak = b.Unit(UnitClass.Guardian, B, H(-1, 0), health: 1);
            b.Trap(A, "C-21", H(1, -2));
            b.Trap(A, "C-21", H(-1, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, tough, H(1, -2)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(1));
            Assert.That(ev.FindIndex(e => e is DamageDealt), Is.LessThan(ev.FindIndex(e => e is UnitTeleported)));
            Assert.That(s.GetUnit(tough).Health, Is.EqualTo(1));
            Assert.That(Board.IsHomeZone(s.GetUnit(tough).Pos, B), Is.True);

            ev = Engine.Apply(s, new MoveCommand(B, weak, H(-1, 1)));
            Assert.That(ev.OfType<UnitDied>().Single().UnitId, Is.EqualTo(weak));
            Assert.That(ev.OfType<UnitTeleported>(), Is.Empty);
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty); // revealed once
        }

        [Test]
        public void P02_ShieldBlocksTheWholeTrapHit()
        {
            var b = new TestBoard().Round(3).Active(B).Passive(A, "P-02");
            int g = b.Unit(UnitClass.Guardian, B, H(2, -2), health: 1);
            b.Effect(g, "C-12");
            b.Trap(A, "C-21", H(1, -2));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, g, H(1, -2)));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Amount, Is.EqualTo(1));
            Assert.That(ev.OfType<DamageDealt>(), Is.Empty);
            Assert.That(ev.OfType<UnitTeleported>().Count(), Is.EqualTo(1));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(1));
        }

        [Test]
        public void P02_OnlyTheTrapOwnersPassiveCounts()
        {
            var b = new TestBoard().Round(3).Active(B).Passive(B, "P-02");
            int g = b.Unit(UnitClass.Guardian, B, H(2, -2));
            b.Trap(A, "C-20", H(1, -2));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(B, g, H(1, -2)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(3));
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
        }

        // ---------- P-04 with Q-13 ----------

        [Test]
        public void P04_ThickWallBlocksTheFirstThreeTowerDamage_NotCountedForQ13()
        {
            var b = new TestBoard().Round(3).Passive(B, "P-04").Quests(A, "Q-13");
            int archer = b.Unit(UnitClass.Archer, A, H(2, -2));
            int mage = b.Unit(UnitClass.Mage, A, H(1, -2));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, archer, TestBoard.DefaultTowerB)); // 2: all blocked
            AssertRevealed(ev, B, "P-04", s);
            var blocked = ev.OfType<TowerDamageBlocked>().Single();
            Assert.That((blocked.Owner, blocked.Amount), Is.EqualTo((B, 2)));
            Assert.That(ev.OfType<DamageDealt>().Any(d => d.TargetUnitId == DamageDealt.Tower), Is.False);
            Assert.That(s.GetTower(B).Health, Is.EqualTo(Catalog.Tower.Health));
            Assert.That(EventFilter.For(ev, A).OfType<TowerDamageBlocked>().Count(), Is.EqualTo(1));

            ev = Engine.Apply(s, new AttackCommand(A, mage, TestBoard.DefaultTowerB)); // 2: 1 blocked, 1 dealt
            Assert.That(ev.OfType<TowerDamageBlocked>().Single().Amount, Is.EqualTo(1));
            Assert.That(ev.OfType<DamageDealt>().Single(d => d.TargetUnitId == DamageDealt.Tower).Amount, Is.EqualTo(1));
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
            Assert.That(s.GetTower(B).Health, Is.EqualTo(Catalog.Tower.Health - 1));
            Assert.That(s.GetProgress(A).TowerDamage, Is.EqualTo(1));
            Assert.That(s.GetProgress(B).WallBlocked, Is.EqualTo(3));

            End(s);
            End(s);
            ev = Engine.Apply(s, new AttackCommand(A, mage, TestBoard.DefaultTowerB)); // the wall is used up
            Assert.That(ev.OfType<TowerDamageBlocked>(), Is.Empty);
            Assert.That(s.GetProgress(A).TowerDamage, Is.EqualTo(3));
        }

        // ---------- P-05 ----------

        [Test]
        public void P05_MerchantDrawsOneExtraBlindCardOnTheFirstMarketBuy_Once()
        {
            var b = new TestBoard().FullPools().Round(3).Passive(A, "P-05");
            int m1 = b.Market("C-10");
            b.Market("C-16");
            var s = b.Build();
            s.SetDrawPending(A, true);
            var ev = Engine.Apply(s, new DrawCommand(A, (int)CardPool.Buff));
            var drawn = ev.OfType<CardDrawn>().ToList();
            Assert.That(drawn.Select(d => d.Source), Is.EqualTo(new[] { DrawSource.Market, DrawSource.Blind }));
            Assert.That(drawn[0].CardId, Is.EqualTo(m1));
            AssertRevealed(ev, A, "P-05", s);
            Assert.That(s.GetHand(A).Count, Is.EqualTo(2));

            End(s);
            End(s);
            Assert.That(s.IsDrawPending(A), Is.True);
            ev = Engine.Apply(s, new DrawCommand(A, (int)CardPool.DebuffTrap));
            Assert.That(ev.OfType<CardDrawn>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void P05_WorksThroughThePrePick_AndRespectsTheHandLimit()
        {
            var b = new TestBoard().FullPools().Round(3).Active(B).Passive(A, "P-05");
            b.Market("C-10");
            for (int i = 0; i < Catalog.HandLimit - 1; i++) b.Hand(A, "C-13");
            var s = b.Build();
            Engine.Apply(s, new PrePickCommand(A, (int)CardPool.Buff));
            var ev = End(s); // A's turn start applies the pre-pick (T-11)
            Assert.That(ev.OfType<CardDrawn>().Select(d => d.Source), Is.EqualTo(new[] { DrawSource.Market })); // D-07: now full
            Assert.That(s.GetHand(A).Count, Is.EqualTo(Catalog.HandLimit));
            Assert.That(ev.OfType<PassiveRevealed>().Single().PassiveId, Is.EqualTo("P-05"));
            Assert.That(s.GetProgress(A).MerchantUsed, Is.True); // interim (flagged): used by the first buy even when the hand is full
        }

        // ---------- P-06 ----------

        [Test]
        public void P06_EnemyMovingOntoThePortalTakesThreeDamage()
        {
            var b = new TestBoard().Round(3).Passive(B, "P-06");
            int rider = b.Unit(UnitClass.Rider, A, H(0, 2));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, rider, Board.Portal));
            AssertRevealed(ev, B, "P-06", s);
            var d = ev.OfType<DamageDealt>().Single();
            Assert.That((d.Kind, d.Amount, d.SourcePlayer, d.TargetUnitId), Is.EqualTo((DamageKind.PortalWarden, 3, B, rider)));
            Assert.That(s.GetUnit(rider).Health, Is.EqualTo(MaxHp(UnitClass.Rider) - 3));
        }

        [Test]
        public void P06_ADeathMeansNoShots_AndTheKillCountsForThePassiveOwner()
        {
            var b = new TestBoard().Round(3).Passive(B, "P-06").Tower(B, H(1, -1));
            int archer = b.Unit(UnitClass.Archer, A, H(0, 2));
            int watcher = b.Unit(UnitClass.Archer, B, H(0, -2));
            b.U(watcher).OnOverwatch = true;
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, archer, Board.Portal));
            Assert.That(ev.OfType<UnitDied>().Single().Killer, Is.EqualTo(B));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s.GetProgress(B).Kills, Is.EqualTo(1));
        }

        [Test]
        public void P06_ResolvesBeforeTheTowerShot_WhenTheUnitSurvives()
        {
            var b = new TestBoard().Round(3).Passive(B, "P-06").Tower(B, H(1, -1));
            int guardian = b.Unit(UnitClass.Guardian, A, H(0, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, guardian, Board.Portal));
            Assert.That(ev.OfType<DamageDealt>().Select(d => d.Kind), Is.EqualTo(new[] { DamageKind.PortalWarden, DamageKind.TowerShot }));
        }

        [Test]
        public void P06_APushOntoThePortalCounts()
        {
            var b = new TestBoard().Round(3).Passive(A, "P-06").Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 2));
            int target = b.Unit(UnitClass.Guardian, B, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1)); // stops the push on the Portal
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(0, 1), 2)); // direction 2 = (0,-1)
            Assert.That(ev.OfType<UnitPushed>().Single().To, Is.EqualTo(Board.Portal));
            AssertRevealed(ev, A, "P-06", s);
            Assert.That(ev.OfType<DamageDealt>().Single().Kind, Is.EqualTo(DamageKind.PortalWarden));
            Assert.That(s.GetUnit(target).Health, Is.EqualTo(MaxHp(UnitClass.Guardian) - 3));
        }

        [Test]
        public void P06_PassingOverThePortalDoesNothing_OnlyAStopCounts()
        {
            // v2.10. A Rider moving (0,1) -> (0,-2): 3 steps, and the only 3-step path is (0,0), (0,-1), (0,-2).
            var b = new TestBoard().Round(3).Passive(B, "P-06").Tower(B, H(4, -4)); // tower out of the shot range
            int rider = b.Unit(UnitClass.Rider, A, H(0, 1));
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, rider, H(0, -2)));
            Assert.That(ev.OfType<DamageDealt>(), Is.Empty);
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
            Assert.That(s.GetUnit(rider).Health, Is.EqualTo(MaxHp(UnitClass.Rider)));

            // A 2-cell push over the Portal: (0,1) -> (0,0) -> (0,-1).
            var b2 = new TestBoard().Round(3).Passive(A, "P-06").Mana(A, 1);
            b2.Unit(UnitClass.Archer, A, H(0, 2));
            int pushed = b2.Unit(UnitClass.Guardian, B, H(0, 1));
            int push = b2.Hand(A, "C-18");
            var s2 = b2.Build();
            ev = Engine.Apply(s2, new PlayCardCommand(A, push, H(0, 1), 2)); // direction 2 = (0,-1)
            Assert.That(ev.OfType<UnitPushed>().Single().To, Is.EqualTo(H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>(), Is.Empty);
            Assert.That(ev.OfType<PassiveRevealed>(), Is.Empty);
            Assert.That(s2.GetUnit(pushed).Health, Is.EqualTo(MaxHp(UnitClass.Guardian)));
        }

        [Test]
        public void P06_ShieldBlocksIt_AndTheOwnersUnitsAreNeverHit()
        {
            var b = new TestBoard().Round(3).Passive(A, "P-06").Passive(B, "P-06");
            int shielded = b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Effect(shielded, "C-12");
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, shielded, Board.Portal));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Kind, Is.EqualTo(DamageKind.PortalWarden));
            Assert.That(ev.OfType<PassiveRevealed>().Select(r => r.Player), Is.EqualTo(new[] { B })); // A's own P-06 never fires
            Assert.That(s.GetUnit(shielded).Health, Is.EqualTo(MaxHp(UnitClass.Guardian)));
        }
    }
}
