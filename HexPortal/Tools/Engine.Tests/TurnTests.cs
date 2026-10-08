using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD §7 unit part of the turn (T-04, T-05, T-08, T-10) and the tower win (W-02).
    public class TurnTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void T05_ThreeEnergyPerTurn_OneActionPerUnit_MoveOrAttackOrOverwatch()
        {
            var b = new TestBoard();
            int u1 = b.Unit(UnitClass.Guardian, A, H(-3, 0));
            int u2 = b.Unit(UnitClass.Archer, A, H(0, 0));
            int u3 = b.Unit(UnitClass.Mage, A, H(-1, 1));
            int u4 = b.Unit(UnitClass.Healer, A, H(-2, -1));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(Catalog.EnergyPerTurn, Is.EqualTo(3));

            Engine.Apply(s, new MoveCommand(A, u1, H(-2, 0)));
            Engine.Apply(s, new AttackCommand(A, u2, H(1, 0)));
            Engine.Apply(s, new OverwatchCommand(A, u3));
            Assert.That(s.GetEnergy(A), Is.EqualTo(0));

            // Acted units can do nothing else; u4 has not acted but there is no Energy left.
            Assert.That(Engine.GetLegalCommands(s, A), Is.EqualTo(new ICommand[] { new EndTurnCommand(A) }));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new MoveCommand(A, u4, H(-1, -1))));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new MoveCommand(A, u1, H(-1, 0))));
        }

        [Test]
        public void T05_MoveAttack_OneCommand_TwoEnergy_OnePerTargetListed()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0)); // Move 1, adjacent range
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            var legal = Engine.GetLegalCommands(s, A);
            var ma = legal.OfType<MoveAttackCommand>().Where(c => c.UnitId == g).ToList();
            // v3.4: one command per reachable target (board order), with the automatic approach.
            var vis = Visibility.VisibleCells(s, A);
            var targets = Movement.Destinations(s, s.GetUnit(g)).SelectMany(h => Combat.LegalTargetsFrom(s, s.GetUnit(g), h, vis)).ToHashSet();
            var expected = Board.Cells.Where(targets.Contains)
                .Select(t => new MoveAttackCommand(A, g, MoveAttack.ApproachFor(s, s.GetUnit(g), t).Value, t)).ToList();
            Assert.That(ma, Is.EqualTo(expected));
            Assert.That(ma, Does.Contain(new MoveAttackCommand(A, g, H(0, 0), H(1, 0))));
            Assert.That(ma.All(c => c.Approach != H(-1, 0)), Is.True); // staying in place is a plain attack

            var ev = Engine.Apply(s, new MoveAttackCommand(A, g, H(0, 0), H(1, 0)));
            Assert.That(ev.OfType<UnitMoved>().Single().To, Is.EqualTo(H(0, 0)));
            Assert.That(ev.OfType<DamageDealt>().Single().Amount, Is.EqualTo(Catalog.Units.Single(u => u.Class == UnitClass.Guardian).Attack));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn - Catalog.MoveAttackEnergy));
            Assert.That(s.GetUnit(g).ActedThisTurn, Is.True);
        }

        [Test]
        public void T05_MoveAttackIllegal_WithOneEnergy_OutOfReach_OrStayingInPlace()
        {
            var b = new TestBoard().Energy(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A).OfType<MoveAttackCommand>(), Is.Empty);
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new MoveAttackCommand(A, g, H(0, 0), H(1, 0))));
            Assert.That(StateHash.Compute(s), Is.EqualTo(before));

            var b2 = new TestBoard();
            int g2 = b2.Unit(UnitClass.Guardian, A, H(-2, 0));
            b2.Unit(UnitClass.Guardian, B, H(1, 0));
            var s2 = b2.Build();
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s2, new MoveAttackCommand(A, g2, H(0, 0), H(1, 0)))); // 2 steps
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s2, new MoveAttackCommand(A, g2, H(-1, 0), H(1, 0)))); // out of range
            var b3 = new TestBoard();
            int g3 = b3.Unit(UnitClass.Guardian, A, H(0, 0));
            b3.Unit(UnitClass.Guardian, B, H(1, 0));
            var s3 = b3.Build();
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s3, new MoveAttackCommand(A, g3, H(0, 0), H(1, 0))));
        }

        [Test]
        public void T05_NoAttackAfterAMove_NoMoveAfterAnAttack()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, g, H(0, 0)));
            Assert.That(Engine.GetLegalCommands(s, A).Any(c => c is AttackCommand || c is MoveAttackCommand || c is OverwatchCommand), Is.False);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new AttackCommand(A, g, H(1, 0))));

            var b2 = new TestBoard();
            int g2 = b2.Unit(UnitClass.Guardian, A, H(0, 0));
            b2.Unit(UnitClass.Guardian, B, H(1, 0));
            var s2 = b2.Build();
            Engine.Apply(s2, new AttackCommand(A, g2, H(1, 0)));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s2, new MoveCommand(A, g2, H(-1, 0))));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s2, new MoveAttackCommand(A, g2, H(0, 1), H(1, 0))));
        }

        [Test]
        public void T05_C14_C19_WindStepExtendsTheMoveAttack_RootedUnitsCannotMoveAttack()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(-3, 0));
            b.Unit(UnitClass.Archer, A, H(0, 1)); // sees the target (V-07: legality from what A sees now)
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            int wind = b.Hand(A, "C-14");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, wind, H(-3, 0)));
            Engine.Apply(s, new MoveAttackCommand(A, g, H(0, 0), H(1, 0))); // 3 steps: Move 1 + Wind Step 2
            Assert.That(s.GetUnit(g).ActedThisTurn, Is.True);

            var b2 = new TestBoard();
            int r = b2.Unit(UnitClass.Guardian, A, H(0, 0));
            b2.Unit(UnitClass.Guardian, B, H(1, 0));
            b2.Effect(r, "C-19");
            var s2 = b2.Build();
            var legal = Engine.GetLegalCommands(s2, A);
            Assert.That(legal.Any(c => c is MoveCommand || c is MoveAttackCommand), Is.False);
            Assert.That(legal, Does.Contain(new AttackCommand(A, r, H(1, 0))));
        }

        [Test]
        public void T05_T07_DeployedUnitsCannotActThatTurn()
        {
            var b = new TestBoard().Mana(A, 3);
            b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Unit(UnitClass.Guardian, B, H(1, -1));
            int card = b.Hand(A, "U-01-Forest");
            var s = b.Build();
            var ev = Engine.Apply(s, new DeployCommand(A, card, H(1, 0)));
            var u = s.GetUnit(ev.OfType<UnitDeployed>().Single().UnitId);
            Assert.That(u.ActedThisTurn, Is.True);
            Assert.That(Engine.GetLegalCommands(s, A).Any(c => c is AttackCommand a && a.UnitId == u.Id
                || c is MoveAttackCommand m && m.UnitId == u.Id), Is.False);
        }

        [Test]
        public void T05_UnspentEnergyNotCarried()
        {
            var b = new TestBoard();
            int g = b.Unit(UnitClass.Guardian, A, H(-1, 0));
            var s = b.Build();
            Engine.Apply(s, new MoveCommand(A, g, H(0, 0)));
            Assert.That(s.GetEnergy(A), Is.EqualTo(2));
            Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.GetEnergy(A), Is.EqualTo(0));
            Assert.That(s.GetEnergy(B), Is.EqualTo(Catalog.EnergyPerTurn));
            Engine.Apply(s, new EndTurnCommand(B));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetEnergy(B), Is.EqualTo(0));
        }

        [Test]
        public void T04_EnergyRefillAndHealAtTurnStart()
        {
            var b = new TestBoard();
            int bh = b.Unit(UnitClass.Healer, B, H(0, -1));
            int bg = b.Unit(UnitClass.Guardian, B, H(1, -1), health: 3);
            var s = b.Build();
            s.GetUnit(bg).ActedThisTurn = true; // stale flag from B's previous turn
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That(s.ActivePlayer, Is.EqualTo(B));
            Assert.That(s.GetEnergy(B), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetUnit(bg).ActedThisTurn, Is.False);
            Assert.That(s.GetUnit(bg).Health, Is.EqualTo(3 + Catalog.HealerHealAmount));
            var started = ev.OfType<TurnStarted>().Single();
            Assert.That(started.Player, Is.EqualTo(B));
            Assert.That(ev.IndexOf(started), Is.LessThan(ev.IndexOf(ev.OfType<UnitHealed>().Single())));
            Assert.That(Engine.GetLegalCommands(s, B).OfType<MoveCommand>().Any(c => c.UnitId == bh), Is.True);
        }

        [Test]
        public void T08_OpponentOverwatchEndsAtEndOfTurn()
        {
            var b = new TestBoard();
            int a = b.Unit(UnitClass.Archer, A, H(0, 1));
            int r = b.Unit(UnitClass.Rider, A, H(-3, 0));
            var s = b.Build();
            Engine.Apply(s, new OverwatchCommand(A, a));
            Engine.Apply(s, new MoveCommand(A, r, H(-2, 0)));
            Engine.Apply(s, new EndTurnCommand(A)); // own overwatch survives own end of turn
            Assert.That(s.GetUnit(a).OnOverwatch, Is.True);

            var ev = Engine.Apply(s, new EndTurnCommand(B)); // ends A's overwatch
            Assert.That(s.GetUnit(a).OnOverwatch, Is.False);
            Assert.That(ev.OfType<OverwatchEnded>().Single().UnitId, Is.EqualTo(a));
        }

        [Test]
        public void T10_RoundAdvancesAfterB()
        {
            var s = new TestBoard().Build();
            Assert.That(s.Round, Is.EqualTo(1));
            Assert.That(s.ActivePlayer, Is.EqualTo(A));
            var ev = Engine.Apply(s, new EndTurnCommand(A));
            Assert.That((s.Round, s.ActivePlayer), Is.EqualTo((1, B)));
            Assert.That(ev.OfType<TurnStarted>().Single().Round, Is.EqualTo(1));
            ev = Engine.Apply(s, new EndTurnCommand(B));
            Assert.That((s.Round, s.ActivePlayer), Is.EqualTo((2, A)));
            Assert.That(ev.OfType<TurnStarted>().Single().Round, Is.EqualTo(2));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new EndTurnCommand(B)));
        }

        [Test]
        public void W02_TowerAtZeroWinsImmediately_FurtherCommandsRejected()
        {
            var b = new TestBoard().Tower(B, H(2, -2), 2);
            int a = b.Unit(UnitClass.Archer, A, H(0, -2)); // in tower range: a shot would follow if the game went on
            b.Unit(UnitClass.Guardian, A, H(-2, 0));
            var s = b.Build();
            var ev = Engine.Apply(s, new AttackCommand(A, a, H(2, -2)));
            Assert.That(s.GetTower(B).Health, Is.EqualTo(0));
            Assert.That(s.Winner, Is.EqualTo(A));
            Assert.That(ev.OfType<TowerDestroyed>().Single().Owner, Is.EqualTo(B));
            Assert.That(ev.OfType<GameOver>().Single().Winner, Is.EqualTo(A));
            Assert.That(ev.OfType<TowerShot>(), Is.Empty);
            Assert.That(ev.Last(), Is.InstanceOf<GameOver>());

            Assert.That(Engine.GetLegalCommands(s, A), Is.Empty);
            Assert.That(Engine.GetLegalCommands(s, B), Is.Empty);
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new EndTurnCommand(A)));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new EndTurnCommand(B)));
            Assert.That(StateHash.Compute(s), Is.EqualTo(before));

            // Splash damage counts too.
            var b2 = new TestBoard().Tower(B, H(2, -2), 1);
            int m = b2.Unit(UnitClass.Mage, A, H(-1, 0));
            b2.Unit(UnitClass.Archer, B, H(1, -1)); // next to the tower
            var s2 = b2.Build();
            Engine.Apply(s2, new AttackCommand(A, m, H(1, -1)));
            Assert.That(s2.Winner, Is.EqualTo(A));
        }
    }
}
