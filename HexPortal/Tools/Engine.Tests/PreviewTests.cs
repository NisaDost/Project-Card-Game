using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // T-05 (v3.2) move-attack preview and the UX-10 range preview: computed by Core from PlayerView only (AI-01 style),
    // so hidden traps, hidden units and the opponent's hand never change them.
    public class PreviewTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);

        static string Ser(IEnumerable<MoveAttackOption> opts) =>
            string.Join(" ", opts.Select(o => o.Target + "<-" + string.Join(",", o.Approaches)));

        [Test]
        public void T05_PreviewListsEveryLegalMoveAttack_GroupedByTarget()
        {
            var b = new TestBoard();
            int r = b.Unit(UnitClass.Rider, A, H(-2, 0));
            b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Unit(UnitClass.Archer, B, H(0, -1));
            var s = b.Build();
            var opts = Preview.MoveAttackTargets(PlayerView.For(s, A), r);
            var legal = Engine.GetLegalCommands(s, A).OfType<MoveAttackCommand>().Where(c => c.UnitId == r).ToList();
            Assert.That(legal, Is.Not.Empty);
            var fromPreview = opts.SelectMany(o => o.Approaches.Select(h => new MoveAttackCommand(A, r, h, o.Target))).ToList();
            Assert.That(fromPreview, Is.EquivalentTo(legal));
            Assert.That(opts.Select(o => o.Target), Is.EqualTo(Board.Cells.Where(h => opts.Any(o => o.Target == h))));
        }

        [Test]
        public void T05_PreviewIgnoresHiddenTrapsUnitsAndTheOpponentHand()
        {
            GameState Make(bool hidden)
            {
                var b = new TestBoard();
                b.Unit(UnitClass.Rider, A, H(-2, 0));
                b.Unit(UnitClass.Guardian, B, H(1, 0));
                if (hidden)
                {
                    b.Trap(B, "C-20", H(0, 1));                 // on an approach cell
                    b.Unit(UnitClass.Guardian, B, H(4, -4));    // out of A's sight
                    b.Hand(B, "C-18");
                }
                return b.Build();
            }
            var with = Make(true);
            Assert.That(Visibility.VisibleCells(with, A).Contains(H(4, -4)), Is.False);
            int rider = with.Units.First(u => u.Owner == A).Id;
            Assert.That(Ser(Preview.MoveAttackTargets(PlayerView.For(with, A), rider)),
                Is.EqualTo(Ser(Preview.MoveAttackTargets(PlayerView.For(Make(false), A), rider))));
            Assert.That(Preview.MoveAttackTargets(PlayerView.For(with, A), rider).Single(o => o.Target == H(1, 0)).Approaches,
                Does.Contain(H(0, 1)));
        }

        [Test]
        public void UX10_OwnUnitRangesOnTheOpponentsTurn()
        {
            var b = new TestBoard().Active(B);
            int g = b.Unit(UnitClass.Guardian, A, H(0, 1));
            b.Unit(UnitClass.Archer, B, H(2, 0));
            var s = b.Build();
            var v = PlayerView.For(s, A);
            var r = Preview.Ranges(v, g);
            var unit = s.GetUnit(g);
            Assert.That(r.MoveCells, Is.EqualTo(Movement.Destinations(s, unit)));
            Assert.That(r.AttackCells, Is.EqualTo(Board.Cells.Where(h => Combat.InRange(unit.Def, unit.Pos, h))));
            var reach = Board.Cells.Where(h => r.MoveCells.Any(m => Combat.InRange(unit.Def, m, h))
                || Combat.InRange(unit.Def, unit.Pos, h)).ToList();
            Assert.That(r.MoveAttackCells, Is.EqualTo(reach));
            Assert.That(r.MoveAttackCells, Does.Contain(H(2, 0)));
            Assert.That(Preview.Ranges(v, 999), Is.Null); // not an own unit
            Assert.That(Preview.MoveAttackTargets(v, 999), Is.Empty);
        }
    }
}
