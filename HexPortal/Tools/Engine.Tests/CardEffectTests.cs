using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §4 support cards: C-03…C-06, C-10…C-19, with U-20 (damage), U-28 (overwatch broken), V-11 (push collision).
    // Most scenes: A's Archer at (0,1) (zone reaches (1,0), (1,1), (-1,1), ...), towers far away at the defaults.
    public class CardEffectTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static int Atk(UnitClass c) => Catalog.Units.Single(u => u.Class == c).Attack;

        static void EndRound(GameState s)
        {
            Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));
            Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));
        }

        // ---------- C-10, C-11, C-04, C-05, C-06 ----------

        [Test]
        public void C10_RageAddsTwoAttack_ForThisAndNextOwnTurn()
        {
            var b = new TestBoard().Mana(A, 1);
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1));
            int rage = b.Hand(A, "C-10");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, rage, H(0, 1)));
            Assert.That(ev.OfType<EffectApplied>().Single().DefId, Is.EqualTo("C-10"));
            ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Atk(UnitClass.Archer) + 2));

            ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(a).Buff.TurnsLeft, Is.EqualTo(1));
            Engine.Apply(s, new EndTurnCommand(B));
            ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().First().Amount, Is.EqualTo(Atk(UnitClass.Archer) + 2));
            ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(a).Buff, Is.Null);
            var exp = ev.OfType<EffectExpired>().Single();
            Assert.That((exp.UnitId, exp.DefId), Is.EqualTo((a, "C-10")));
        }

        [Test]
        public void C11_GiantStrengthIsPermanent()
        {
            var b = new TestBoard().Mana(A, 2);
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1));
            int gs = b.Hand(A, "C-11");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, gs, H(0, 1)));
            for (int i = 0; i < 4; i++) EndRound(s);
            Assert.That(s.GetUnit(a).Buff.Def.Id, Is.EqualTo("C-11"));
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Atk(UnitClass.Archer) + 1));
        }

        [Test]
        public void C05_RageReplacesGiantStrength()
        {
            var b = new TestBoard().Mana(A, 3);
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1));
            int gs = b.Hand(A, "C-11");
            int rage = b.Hand(A, "C-10");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, gs, H(0, 1)));
            var ev = Engine.Apply(s, new PlayCardCommand(A, rage, H(0, 1)));
            Assert.That(ev.OfType<EffectExpired>().Single().DefId, Is.EqualTo("C-11"));
            Assert.That(s.GetUnit(a).Buff.Def.Id, Is.EqualTo("C-10"));
            ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Atk(UnitClass.Archer) + 2)); // not +3

            EndRound(s);
            Engine.Apply(s, new EndTurnCommand(A)); // Rage runs out; Giant Strength does not come back
            Assert.That(s.GetUnit(a).Buff, Is.Null);
        }

        [Test]
        public void C06_SameCardAgainRestartsDuration_NoStacking()
        {
            var b = new TestBoard().Mana(A, 1);
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1));
            int r1 = b.Hand(A, "C-10");
            int r2 = b.Hand(A, "C-10");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, r1, H(0, 1)));
            EndRound(s);
            Assert.That(s.GetUnit(a).Buff.TurnsLeft, Is.EqualTo(1));
            Engine.Apply(s, new PlayCardCommand(A, r2, H(0, 1)));
            Assert.That(s.GetUnit(a).Buff.TurnsLeft, Is.EqualTo(Catalog.TimedEffectTurns));
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Atk(UnitClass.Archer) + 2));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(a).Buff, Is.Not.Null); // the restarted timer still has a turn left
        }

        [Test]
        public void C05_OneBuffAndOneDebuffTogether()
        {
            var b = new TestBoard().Mana(A, 1);
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(0, -1));
            b.Effect(a, "C-16");
            int rage = b.Hand(A, "C-10");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, rage, H(0, 1)));
            Assert.That(s.GetUnit(a).Buff.Def.Id, Is.EqualTo("C-10"));
            Assert.That(s.GetUnit(a).Debuff.Def.Id, Is.EqualTo("C-16"));
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(0, -1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Atk(UnitClass.Archer) + 2 - 2));
        }

        [Test]
        public void C04_DebuffLastsTheOpponentsNextTwoTurns()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int weak = b.Hand(A, "C-16");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, weak, H(1, 0)));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(g).Debuff.TurnsLeft, Is.EqualTo(2)); // A's turn end does not count
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(g).Debuff.TurnsLeft, Is.EqualTo(1));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(g).Debuff, Is.Not.Null); // still on during B's second turn
            var ev = Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetUnit(g).Debuff, Is.Null);
            Assert.That(ev.OfType<EffectExpired>().Single().DefId, Is.EqualTo("C-16"));
        }

        [Test]
        public void C16_WeaknessLowersAttack_DamageNeverBelowZero()
        {
            var b = new TestBoard().Active(B);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            int h = b.Unit(UnitClass.Healer, B, H(1, 0)); // Attack 1
            b.Effect(h, "C-16");
            b.Effect(g, "C-12");
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(B, h, H(0, 1)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(0));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            // Interim (flagged): a 0 hit lowers no Health, so it is not "damage" (U-20) and the Shield stays.
            Assert.That(s.GetUnit(g).Buff.Def.Id, Is.EqualTo("C-12"));
        }

        // ---------- C-12 Shield ----------

        [Test]
        public void C12_ShieldBlocksTheNextAttackCompletelyThenEnds()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            int archer = b.Unit(UnitClass.Archer, B, H(0, -1));
            int healer = b.Unit(UnitClass.Healer, B, H(1, 0));
            int shield = b.Hand(A, "C-12");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, shield, H(0, 1)));
            Engine.Apply(s, new EndTurnCommand(A));
            var ev = Engine.Apply(s, new AttackCommand(B, archer, H(0, 1)));
            var blocked = ev.OfType<ShieldBlocked>().Single();
            Assert.That((blocked.UnitId, blocked.Amount, blocked.Kind), Is.EqualTo((g, Atk(UnitClass.Archer), DamageKind.Attack)));
            Assert.That(ev.OfType<DamageDealt>(), Is.Empty);
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            Assert.That(s.GetUnit(g).Buff, Is.Null);
            Engine.Apply(s, new AttackCommand(B, healer, H(0, 1)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6 - Atk(UnitClass.Healer)));
        }

        [Test]
        public void C12_ShieldBlocksTowerShotAndSplash()
        {
            var b = new TestBoard().Tower(B, H(2, -2));
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            b.Effect(g, "C-12");
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(0, -1)));
            Assert.That(ev.OfType<TowerShot>().Count(), Is.EqualTo(1));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Kind, Is.EqualTo(DamageKind.TowerShot));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            Assert.That(s.IsTowerShotAvailable(B), Is.False);

            var b2 = new TestBoard();
            int m = b2.Unit(UnitClass.Mage, A, H(0, 1));
            b2.Unit(UnitClass.Guardian, B, H(1, -1));
            int next = b2.Unit(UnitClass.Archer, B, H(2, -1));
            b2.Effect(next, "C-12");
            var s2 = b2.Build();
            ev = Engine.Apply(s2, new AttackCommand(A, m, H(1, -1)));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Kind, Is.EqualTo(DamageKind.Splash));
            Assert.That(s2.GetUnit(next).Health, Is.EqualTo(3));
        }

        [Test]
        public void C12_ShieldBlocksPoisonTick()
        {
            var b = new TestBoard().Mana(A, 2);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Effect(g, "C-12");
            int poison = b.Hand(A, "C-17");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, poison, H(1, 0)));
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Kind, Is.EqualTo(DamageKind.Poison));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            Assert.That(s.GetUnit(g).Buff, Is.Null);
            EndRound(s);
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(5)); // the second tick lands
        }

        // ---------- C-13, C-14 ----------

        [Test]
        public void C13_HealingPotionAddsThree_CappedAtMax()
        {
            var b = new TestBoard().Mana(A, 2);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1), health: 1);
            int p1 = b.Hand(A, "C-13");
            int p2 = b.Hand(A, "C-13");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, p1, H(0, 1)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(4));
            Assert.That(ev.OfType<UnitHealed>().Single().Amount, Is.EqualTo(3));
            ev = Engine.Apply(s, new PlayCardCommand(A, p2, H(0, 1)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6)); // U-24
            Assert.That(ev.OfType<UnitHealed>().Single().Amount, Is.EqualTo(2));
            Assert.That(s.GetUnit(g).Buff, Is.Null); // instant: no slot
        }

        [Test]
        public void C14_WindStepAddsTwoMoveThisTurnOnly_NotABuffSlot()
        {
            var b = new TestBoard().Mana(A, 2);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 1));
            b.Unit(UnitClass.Archer, A, H(1, 1)); // sight, so distance-3 cells are Visible (U-12)
            b.Effect(g, "C-11");
            int w1 = b.Hand(A, "C-14");
            int w2 = b.Hand(A, "C-14");
            var s = b.Build();
            int MaxReach() => Movement.Destinations(s, s.GetUnit(g)).Max(h => Hex.Distance(h, s.GetUnit(g).Pos));
            Assert.That(MaxReach(), Is.EqualTo(1));
            Engine.Apply(s, new PlayCardCommand(A, w1, H(-1, 1)));
            Assert.That(MaxReach(), Is.EqualTo(3));
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new MoveCommand(A, g, H(2, 0))));
            Assert.That(s.GetUnit(g).Buff.Def.Id, Is.EqualTo("C-11"));
            Engine.Apply(s, new PlayCardCommand(A, w2, H(-1, 1)));
            Assert.That(MaxReach(), Is.EqualTo(3)); // interim (flagged): a second Wind Step does not stack (C-06)
            Engine.Apply(s, new EndTurnCommand(A));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(MaxReach(), Is.EqualTo(1));
        }

        // ---------- C-15 Teleport ----------

        [Test]
        public void C15_TeleportMovesFriendlyUnit_NoEnergyNoAction()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int a = b.Unit(UnitClass.Archer, A, H(-3, 0));
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            var legal = Engine.GetLegalCommands(s, A).OfType<PlayCardCommand>().Where(c => c.Target == H(-3, 0)).ToList();
            var zone = ControlZone.Cells(s, A);
            Assert.That(legal.Select(c => c.Dest.Value), Is.EquivalentTo(Board.Cells.Where(h =>
                zone.Contains(h) && h != Board.Portal && s.UnitAt(h) == null && s.TowerAt(h) == null)));

            var ev = Engine.Apply(s, new PlayCardCommand(A, tp, H(-3, 0), H(1, 1)));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(1, 1)));
            var t = ev.OfType<UnitTeleported>().Single();
            Assert.That((t.UnitId, t.From, t.To), Is.EqualTo((a, H(-3, 0), H(1, 1))));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Assert.That(s.GetUnit(a).ActedThisTurn, Is.False);
            Assert.That(s.GetUnit(a).MovedThisTurn, Is.False);
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveCommand>().Any(c => c.UnitId == a), Is.True);
        }

        [Test]
        public void C15_TeleportDestinationMustBeEmptyNonPortalZoneCell()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Unit(UnitClass.Archer, A, H(-3, 0));
            b.Unit(UnitClass.Archer, B, H(1, 0));
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            foreach (var bad in new[] { Board.Portal, H(1, 0), H(0, 1), H(2, -1), H(-3, 0) })
                ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, tp, H(-3, 0), bad));
            ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, tp, H(1, 0), H(1, 1)));  // enemy unit
            ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, tp, H(-3, 0)));          // no destination
            ControlZoneTests.AssertRejected(s, new PlayCardCommand(A, tp, H(-3, 0), 0));       // a direction instead
        }

        [Test]
        public void C15_TeleportIntoRangeTriggersNoTowerOrOverwatch()
        {
            var b = new TestBoard().Tower(B, H(2, -2)).Mana(A, 3);
            b.Unit(UnitClass.Mage, A, H(0, -1));
            int a = b.Unit(UnitClass.Archer, A, H(-3, 0));
            int w = b.Unit(UnitClass.Archer, B, H(2, -1));
            b.U(w).OnOverwatch = true;
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, tp, H(-3, 0), H(1, -1)));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(1, -1)));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
            Assert.That(s.IsTowerShotAvailable(B), Is.True);
            Assert.That(s.GetUnit(w).OnOverwatch, Is.True);
        }


        [Test]
        public void C19_RootedUnitCanStillBeTeleported()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int a = b.Unit(UnitClass.Archer, A, H(-3, 0));
            b.Effect(a, "C-19");
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveCommand>().Any(c => c.UnitId == a), Is.False);
            Engine.Apply(s, new PlayCardCommand(A, tp, H(-3, 0), H(1, 1)));
            Assert.That(s.GetUnit(a).Pos, Is.EqualTo(H(1, 1)));
            Assert.That(s.GetUnit(a).IsRooted, Is.True);
        }

        [Test]
        public void U28_OwnerTeleportBreaksOverwatchSetThisTurn()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            int a = b.Unit(UnitClass.Archer, A, H(-3, 0));
            int tp = b.Hand(A, "C-15");
            var s = b.Build();
            Engine.Apply(s, new OverwatchCommand(A, a));
            var ev = Engine.Apply(s, new PlayCardCommand(A, tp, H(-3, 0), H(1, 1)));
            Assert.That(s.GetUnit(a).OnOverwatch, Is.False);
            Assert.That(ev.OfType<OverwatchEnded>().Single().UnitId, Is.EqualTo(a));
        }

        // ---------- C-16…C-19 debuffs ----------

        [Test]
        public void C17_PoisonTicksAtOwnerTurnStart_TwoTurns()
        {
            var b = new TestBoard().Mana(A, 2);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Archer, B, H(1, 0));
            int poison = b.Hand(A, "C-17");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, poison, H(1, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(3)); // nothing on A's own turn
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            var dmg = ev.OfType<DamageDealt>().Single();
            Assert.That((dmg.Kind, dmg.SourcePlayer, dmg.SourceUnitId, dmg.Amount), Is.EqualTo((DamageKind.Poison, A, DamageDealt.NoUnit, 1)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(2));
            EndRound(s);
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(1));
            EndRound(s);
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(1));
            Assert.That(s.GetUnit(g).Debuff, Is.Null);
        }

        [Test]
        public void C17_PoisonDeathAtTurnStartTriggersDeathDraw()
        {
            var b = new TestBoard().Mana(A, 2);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Archer, B, H(1, 0), health: 1);
            int poison = b.Hand(A, "C-17");
            int pooled = b.PoolCard("C-16");
            b.Market("C-12");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, poison, H(1, 0)));
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetUnit(g), Is.Null);
            var died = ev.OfType<UnitDied>().Single();
            Assert.That((died.Owner, died.Killer), Is.EqualTo((B, A)));
            var drawn = ev.OfType<CardDrawn>().Single();
            Assert.That((drawn.Player, drawn.CardId, drawn.Source), Is.EqualTo((B, pooled, DrawSource.Blind)));
            Assert.That(ev.IndexOf(died), Is.LessThan(ev.IndexOf(drawn)));
            Assert.That(s.IsDrawPending(B), Is.True); // then the normal turn-start draw (Market only)
        }

        [Test]
        public void C19_RootStopsMovementButNotAttackOrOverwatch_TwoTurns()
        {
            var b = new TestBoard().Mana(A, 2);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int root = b.Hand(A, "C-19");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, root, H(1, 0)));
            Engine.Apply(s, new EndTurnCommand(A));
            var legal = Engine.GetLegalCommands(s, B);
            Assert.That(legal.OfType<MoveCommand>().Any(c => c.UnitId == g), Is.False);
            Assert.That(legal, Does.Contain(new AttackCommand(B, g, H(0, 1))));
            Assert.That(legal, Does.Contain(new OverwatchCommand(B, g)));
            ControlZoneTests.AssertRejected(s, new MoveCommand(B, g, H(2, 0)));
            EndRound(s);
            Assert.That(Engine.GetLegalCommands(s, B).OfType<MoveCommand>().Any(c => c.UnitId == g), Is.False);
            EndRound(s);
            Assert.That(Engine.GetLegalCommands(s, B).OfType<MoveCommand>().Any(c => c.UnitId == g), Is.True);
        }

        [Test]
        public void C18_PushMovesTwoCellsInTheChosenDirection_NoDamage()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var pushes = Engine.GetLegalCommands(s, A).OfType<PlayCardCommand>().Where(c => c.CardId == push).ToList();
            Assert.That(pushes.Select(c => c.Direction), Is.EqualTo(Enumerable.Range(0, 6)));
            Assert.That(pushes.All(c => c.Target == H(1, 0) && c.Dest == null), Is.True);
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0)); // direction (+1, 0)
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(3, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(6));
            var p = ev.OfType<UnitPushed>().Single();
            Assert.That((p.UnitId, p.From, p.To), Is.EqualTo((g, H(1, 0), H(3, 0))));
            Assert.That(ev.OfType<DamageDealt>(), Is.Empty);
            Assert.That(s.GetUnit(g).Debuff, Is.Null); // instant: no slot
        }

        [Test]
        public void C18_PushStopsBeforeUnitTowerRockAndEdge()
        {
            Hex Pushed(System.Action<TestBoard> obstacle, Hex start)
            {
                var b = new TestBoard().Mana(A, 1);
                b.Unit(UnitClass.Archer, A, H(start.Q - 1, start.R + 1));
                int g = b.Unit(UnitClass.Guardian, B, start);
                obstacle(b);
                int push = b.Hand(A, "C-18");
                var s = b.Build();
                Engine.Apply(s, new PlayCardCommand(A, push, start, 0));
                return s.GetUnit(g).Pos;
            }
            Assert.That(Pushed(b => b.Unit(UnitClass.Healer, A, H(3, 0)), H(1, 0)), Is.EqualTo(H(2, 0)));
            Assert.That(Pushed(b => b.Tower(B, H(3, 0)), H(1, 0)), Is.EqualTo(H(2, 0)));
            Assert.That(Pushed(b => b.Rock(H(3, 0)), H(1, 0)), Is.EqualTo(H(2, 0)));
            Assert.That(Pushed(b => { }, H(2, 0)), Is.EqualTo(H(3, 0))); // (4,0) is off the board
        }

        [Test]
        public void C18_PushStopsBeforeHiddenUnit()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Guardian, A, H(0, 1)); // Sight 2
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Unit(UnitClass.Healer, B, H(3, -2));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(3, -2)), Is.False);
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlayCardCommand(A, push, H(1, 0), 1)));
            Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 1)); // direction (+1,-1): (2,-1), then (3,-2)
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(2, -1))); // V-11 exception: physical collision
        }

        [Test]
        public void C18_ZeroCellPushIsLegalAndCostsMana()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Unit(UnitClass.Healer, B, H(2, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlayCardCommand(A, push, H(1, 0), 0)));
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(1, 0)));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Assert.That(s.GetHand(A), Is.Empty);
            var p = ev.OfType<UnitPushed>().Single();
            Assert.That(p.From, Is.EqualTo(p.To));
        }

        [Test]
        public void C18_PushedUnitMayStopOnThePortal()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(1, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(2, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, push, H(2, 0), 3)); // direction (-1, 0)
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(Board.Portal));
        }

        [Test]
        public void U28_EnemyPushBreaksOverwatch()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int mover = b.Unit(UnitClass.Guardian, A, H(1, 1));
            int w = b.Unit(UnitClass.Archer, B, H(1, 0));
            b.U(w).OnOverwatch = true;
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 1)); // to (3,-2)
            Assert.That(s.GetUnit(w).Pos, Is.EqualTo(H(3, -2)));
            Assert.That(s.GetUnit(w).OnOverwatch, Is.False);
            Assert.That(ev.OfType<OverwatchEnded>().Single().UnitId, Is.EqualTo(w));
            ev = Engine.Apply(s, new MoveCommand(A, mover, H(1, 0))); // on its line from (3,-2), but it is off overwatch
            Assert.That(ev.OfType<OverwatchFired>(), Is.Empty);
        }

        [Test]
        public void C03_InstantCardsKeepExistingSlots_CardsIgnoreCover()
        {
            var b = new TestBoard().Mana(A, 3);
            int a = b.Unit(UnitClass.Archer, A, H(0, 1), health: 1);
            int g = b.Unit(UnitClass.Archer, B, H(1, 0));
            b.Unit(UnitClass.Guardian, B, H(2, 0)); // covers (1,0) against attacks only (U-11)
            b.Effect(a, "C-10");
            b.Effect(g, "C-16");
            int potion = b.Hand(A, "C-13");
            int push = b.Hand(A, "C-18");
            int weak = b.Hand(A, "C-16");
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A).OfType<AttackCommand>().Any(c => c.Target == H(1, 0)), Is.False);
            Engine.Apply(s, new PlayCardCommand(A, potion, H(0, 1)));
            Assert.That(s.GetUnit(a).Buff.Def.Id, Is.EqualTo("C-10"));
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlayCardCommand(A, weak, H(1, 0))));
            Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 5)); // direction (0,+1): (1,1), (1,2)
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(1, 2)));
            Assert.That(s.GetUnit(g).Debuff.Def.Id, Is.EqualTo("C-16"));
        }
    }
}
