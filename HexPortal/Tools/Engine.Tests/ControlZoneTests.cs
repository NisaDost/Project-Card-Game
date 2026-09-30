using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD C-02: the Control Zone and where each kind of card may be played.
    public class ControlZoneTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static HashSet<Hex> Within1(params Hex[] centers) =>
            new HashSet<Hex>(Board.Cells.Where(c => centers.Any(x => Hex.Distance(c, x) <= 1)));

        [Test]
        public void C02_ZoneIsOwnUnitsAndTowerPlusNeighbours()
        {
            var b = new TestBoard();
            b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Mage, A, H(-3, 0)); // board edge: off-board neighbours are dropped
            b.Unit(UnitClass.Guardian, B, H(2, -1)); // enemy units add nothing to A's zone
            var s = b.Build();
            Assert.That(Catalog.ControlRange, Is.EqualTo(1));
            var expected = Within1(TestBoard.DefaultTowerA, H(0, 1), H(-3, 0));
            Assert.That(ControlZone.Cells(s, A), Is.EquivalentTo(expected));
            Assert.That(ControlZone.Cells(s, B), Is.EquivalentTo(Within1(TestBoard.DefaultTowerB, H(2, -1))));
        }

        [Test]
        public void C02_ZoneIsAlwaysVisibleToItsOwner()
        {
            foreach (var d in Catalog.Units) Assert.That(d.Sight, Is.GreaterThan(Catalog.ControlRange));
            Assert.That(Catalog.Tower.Sight, Is.GreaterThan(Catalog.ControlRange));
            var b = new TestBoard();
            b.Unit(UnitClass.Healer, A, H(1, -1));
            var s = b.Build();
            Assert.That(ControlZone.Cells(s, A).IsSubsetOf(Visibility.VisibleCells(s, A)), Is.True);
        }

        [Test]
        public void C02_EveryCardKindOnlyInsideTheZone()
        {
            var b = new TestBoard().Mana(A, 6);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int archerB = b.Unit(UnitClass.Archer, B, H(1, 1));   // in A's zone
            int farB = b.Unit(UnitClass.Archer, B, H(2, -1));     // visible to A's Archer (distance 2), outside the zone
            int ch = b.Hand(A, "U-03-Snow");
            int trap = b.Hand(A, "C-20");
            int weak = b.Hand(A, "C-16");
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A).Contains(H(2, -1)), Is.True);

            var zone = ControlZone.Cells(s, A);
            var legal = Engine.GetLegalCommands(s, A);
            var deployCells = legal.OfType<DeployCommand>().Select(c => c.Cell).ToList();
            var trapCells = legal.OfType<PlayCardCommand>().Where(c => c.CardId == trap).Select(c => c.Target).ToList();
            Assert.That(deployCells, Is.Not.Empty);
            Assert.That(deployCells.All(zone.Contains), Is.True);
            Assert.That(trapCells, Is.EquivalentTo(deployCells)); // same cell rule (empty, not Portal) with no traps yet
            Assert.That(legal.OfType<PlayCardCommand>().Where(c => c.CardId == weak).Select(c => c.Target),
                Is.EqualTo(new[] { H(1, 1) }));

            AssertRejected(s, new DeployCommand(A, ch, H(1, -2)));
            AssertRejected(s, new PlayCardCommand(A, trap, H(1, -2)));
            AssertRejected(s, new PlayCardCommand(A, weak, H(2, -1)));
            Engine.Apply(s, new PlayCardCommand(A, weak, H(1, 1)));
            Assert.That(s.GetUnit(archerB).Debuff.Def.Id, Is.EqualTo("C-16"));
            Assert.That(s.GetUnit(farB).Debuff, Is.Null);
        }

        [Test]
        public void C02_BuffsOnlyOnFriendlyUnits_CardsNeverTargetTowers()
        {
            var b = new TestBoard().Mana(A, 6).Tower(B, H(1, 0));
            b.Unit(UnitClass.Archer, A, H(0, 1));
            b.Unit(UnitClass.Archer, B, H(-1, 1));
            foreach (var id in new[] { "C-10", "C-11", "C-12", "C-13", "C-14", "C-16", "C-17", "C-18", "C-19", "C-20", "C-21" })
                b.Hand(A, id);
            var s = b.Build();
            var plays = Engine.GetLegalCommands(s, A).OfType<PlayCardCommand>().ToList();
            var towers = new[] { s.GetTower(A).Pos, s.GetTower(B).Pos };
            Assert.That(plays.Any(c => towers.Contains(c.Target)), Is.False);
            foreach (var c in plays)
            {
                var def = s.FindInHand(A, c.CardId).Support;
                if (def.Pool == SupportPool.Buff) Assert.That(s.UnitAt(c.Target).Owner, Is.EqualTo(A), c.ToString());
                else if (def.Category != CardCategory.Trap) Assert.That(s.UnitAt(c.Target).Owner, Is.EqualTo(B), c.ToString());
            }
            int rage = s.GetHand(A).First(c => c.DefId == "C-10").Id;
            AssertRejected(s, new PlayCardCommand(A, rage, H(-1, 1)));  // enemy unit
            AssertRejected(s, new PlayCardCommand(A, rage, H(1, 0)));   // enemy tower
            AssertRejected(s, new PlayCardCommand(A, rage, TestBoard.DefaultTowerA)); // own tower
            int weak = s.GetHand(A).First(c => c.DefId == "C-16").Id;
            AssertRejected(s, new PlayCardCommand(A, weak, H(1, 0)));   // enemy tower
            AssertRejected(s, new PlayCardCommand(A, weak, H(0, 1)));   // own unit
        }

        [Test]
        public void C02_NoUnitsNearMeansNoCards()
        {
            var b = new TestBoard().Mana(A, 6);
            b.Unit(UnitClass.Archer, B, H(0, 0));
            int ch = b.Hand(A, "U-01-Forest");
            var s = b.Build();
            var cells = Engine.GetLegalCommands(s, A).OfType<DeployCommand>().Select(c => c.Cell).ToList();
            Assert.That(cells, Is.EquivalentTo(Within1(TestBoard.DefaultTowerA).Where(h => h != TestBoard.DefaultTowerA)));
            AssertRejected(s, new DeployCommand(A, ch, H(0, 1)));
        }

        internal static void AssertRejected(GameState s, ICommand c)
        {
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, c), c.ToString());
            Assert.That(StateHash.Compute(s), Is.EqualTo(before), c.ToString());
        }
    }
}
