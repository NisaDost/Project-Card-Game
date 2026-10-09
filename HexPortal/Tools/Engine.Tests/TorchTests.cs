using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD v4.0 V-12 torches: 1 Mana, no action/Energy, 1 per unit per turn, keeps 2 cells around Visible to its owner,
    // max 3 per player (the 4th puts out the oldest), goes out when an enemy unit stops on it, shown to the opponent only
    // while its cell is Visible to them.
    public class TorchTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static System.Collections.Generic.List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        [Test]
        public void V12_ATorchCostsOneManaAndNeitherAnActionNorEnergy()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 3));
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlaceTorchCommand(A, g)));
            var ev = Engine.Apply(s, new PlaceTorchCommand(A, g));
            var placed = ev.OfType<TorchPlaced>().Single();
            Assert.That((placed.Owner, placed.UnitId, placed.Cell), Is.EqualTo((A, g, H(0, 3))));
            Assert.That(s.GetMana(A), Is.EqualTo(1 - Catalog.TorchCost));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(Turn.CanAct(s, s.GetUnit(g)), Is.True, "the unit keeps its action");
            Assert.That(s.Torches.Single().Pos, Is.EqualTo(H(0, 3)));
        }

        [Test]
        public void V12_NoTorchWithoutMana()
        {
            var b = new TestBoard().Mana(A, 0);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 3));
            var s = b.Build();
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PlaceTorchCommand>(), Is.Empty);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new PlaceTorchCommand(A, g)));
        }

        [Test]
        public void V12_OneTorchPerUnitPerTurn()
        {
            var b = new TestBoard().Mana(A, 3);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 3));
            int h = b.Unit(UnitClass.Healer, A, H(2, 3));
            var s = b.Build();
            Engine.Apply(s, new PlaceTorchCommand(A, g));
            Engine.Apply(s, new MoveCommand(A, g, H(0, 4)));
            var legal = Engine.GetLegalCommands(s, A).OfType<PlaceTorchCommand>().ToList();
            Assert.That(legal, Is.EqualTo(new[] { new PlaceTorchCommand(A, h) }));
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, new PlaceTorchCommand(A, g)));
            End(s);
            End(s);
            s.SetMana(A, 1);
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(new PlaceTorchCommand(A, g)), "a new turn");
        }

        [Test]
        public void V12_TheTorchKeepsTwoCellsAroundVisibleWithoutAUnitThere()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 3));
            var s = b.Build();
            Engine.Apply(s, new PlaceTorchCommand(A, g));
            Engine.Apply(s, new MoveCommand(A, g, H(0, 4)));
            var visible = Visibility.VisibleCells(s, A);
            Assert.That(visible, Does.Contain(H(0, 1)), "distance 2 from the torch, 3 from the unit");
            Assert.That(visible, Does.Not.Contain(H(0, 0) + H(1, -1)), "distance 3 from the torch");
            Assert.That(PlayerView.For(s, A).Cells[Board.IndexOf(H(0, 1))].Visibility, Is.EqualTo(CellVisibility.Visible));
            Assert.That(Catalog.TorchSight, Is.EqualTo(2));
        }

        [Test]
        public void V12_AFourthTorchPutsOutTheOldest()
        {
            var b = new TestBoard().Mana(A, 4);
            var ids = new[] { H(0, 3), H(2, 3), H(-2, 4), H(1, 4) }.Select(h => b.Unit(UnitClass.Healer, A, h)).ToList();
            var s = b.Build();
            foreach (var id in ids.Take(3)) Engine.Apply(s, new PlaceTorchCommand(A, id));
            int oldest = s.Torches.First().Id;
            var ev = Engine.Apply(s, new PlaceTorchCommand(A, ids[3]));
            var x = ev.OfType<TorchExtinguished>().Single();
            Assert.That((x.TorchId, x.Cell, x.Reason), Is.EqualTo((oldest, H(0, 3), TorchEndReason.Replaced)));
            Assert.That(s.Torches.Select(t => t.Pos), Is.EqualTo(new[] { H(2, 3), H(-2, 4), H(1, 4) }));
            Assert.That(Torches.ActiveCount(s, A), Is.EqualTo(Catalog.MaxTorchesPerPlayer));
        }

        [Test]
        public void V12_AnEnemyUnitStoppingOnTheTorchPutsItOut_PassingThroughDoesNot()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            int rider = b.Unit(UnitClass.Rider, B, H(0, -1));
            int archer = b.Unit(UnitClass.Archer, B, H(-1, 0));
            var s = b.Build();
            Engine.Apply(s, new PlaceTorchCommand(A, g));
            Engine.Apply(s, new MoveCommand(A, g, H(0, 2)));
            End(s);
            Assert.That(s.IsDrawPending(B), Is.False, "empty pools");
            var ev = Engine.Apply(s, new MoveCommand(B, rider, H(-1, 2))); // may pass (0,1), stops beyond it
            Assert.That(ev.OfType<TorchExtinguished>(), Is.Empty);
            Assert.That(s.Torches.Count, Is.EqualTo(1));
            ev = Engine.Apply(s, new MoveCommand(B, archer, H(0, 1)));
            var x = ev.OfType<TorchExtinguished>().Single();
            Assert.That((x.Owner, x.Cell, x.Reason), Is.EqualTo((A, H(0, 1), TorchEndReason.EnemyStood)));
            Assert.That(EventFilter.For(ev, B).OfType<TorchExtinguished>().Count(), Is.EqualTo(1), "B stands there and sees it");
            Assert.That(s.Torches, Is.Empty);
        }

        [Test]
        public void V12_Leak_TheOpponentSeesATorchOnlyWhileItsCellIsVisibleToThem()
        {
            var b = new TestBoard().Mana(A, 1);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 4));
            int scout = b.Unit(UnitClass.Archer, B, H(0, -1)); // Sight 3: (0,4) at distance 5
            var s = b.Build();
            var visibleB = Visibility.VisibleCells(s, B);
            var ev = Engine.Apply(s, new PlaceTorchCommand(A, g));
            Engine.Apply(s, new MoveCommand(A, g, H(0, 5)));

            Assert.That(EventFilter.For(ev, B), Is.Empty, "TorchPlaced on a cell B cannot see");
            var viewB = PlayerView.For(s, B);
            Assert.That(viewB.Torches, Is.Empty);
            Assert.That(Visibility.VisibleCells(s, B), Is.EquivalentTo(visibleB), "a torch gives sight only to its owner");
            Assert.That(PlayerView.For(s, A).Torches.Single().Pos, Is.EqualTo(H(0, 4)));

            // B walks into sight of the torch cell: now it is shown.
            End(s);
            Engine.Apply(s, new MoveCommand(B, scout, H(0, 1))); // (0,4) at distance 3
            var torch = PlayerView.For(s, B).Torches.Single();
            Assert.That((torch.Owner, torch.Pos), Is.EqualTo((A, H(0, 4))));
        }

        [Test]
        public void V12_AnOwnTorchIsNotPlacedTwiceOnTheSameCell()
        {
            // Interim (GDD silent): a second own torch on a torch cell is not offered.
            var b = new TestBoard().Mana(A, 2);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 3));
            var s = b.Build();
            Engine.Apply(s, new PlaceTorchCommand(A, g));
            End(s);
            End(s);
            s.SetMana(A, 2);
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PlaceTorchCommand>(), Is.Empty);
        }
    }
}
