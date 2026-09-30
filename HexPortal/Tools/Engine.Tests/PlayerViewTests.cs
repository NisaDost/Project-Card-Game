using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // V-05, V-09, V-10 (v2.8 V3: opponent Mana/Energy hidden, pool counts and Market public): the redacted read model.
    public class PlayerViewTests
    {
        static Hex H(int q, int r) => new Hex(q, r);

        [Test]
        public void V10_OwnInformationIsComplete()
        {
            var s = Match.Create(31);
            SetupPlayer(s, A, 3, finish: false);
            var view = PlayerView.For(s, A);
            Assert.That(view.Viewer, Is.EqualTo(A));
            Assert.That(view.Phase, Is.EqualTo(GamePhase.Setup));
            Assert.That(view.SetupStep, Is.EqualTo(SetupStep.Placement));
            Assert.That(view.Hand.Select(c => c.Id), Is.EqualTo(s.GetHand(A).Select(c => c.Id)));
            Assert.That(view.QuestOffer, Is.EqualTo(s.GetQuestOffer(A).Select(q => q.Id)));
            Assert.That(view.QuestChoices, Is.EqualTo(s.GetQuestChoices(A).Select(q => q.Id)));
            Assert.That(view.PassiveOffer, Is.EqualTo(s.GetPassiveOffer(A).Select(d => d.Id)));
            Assert.That(view.PassiveChoice, Is.EqualTo(s.GetPassiveChoice(A).Id));
            Assert.That(view.OwnTowerPos, Is.EqualTo(s.GetTower(A).Pos));
            Assert.That(view.OwnUnits.Select(u => u.Id), Is.EqualTo(s.Units.Where(u => u.Owner == A).Select(u => u.Id)));
            Assert.That(view.OpponentSetupFinished, Is.False);

            var other = PlayerView.For(s, B);
            Assert.That(other.SetupStep, Is.EqualTo(SetupStep.Quests));
            Assert.That(other.QuestChoices, Is.Empty);
            Assert.That(other.QuestOffer, Is.EqualTo(s.GetQuestOffer(B).Select(q => q.Id)));
            Assert.That(other.OwnTowerPos, Is.Null);
        }

        [Test]
        public void V10_OwnTurnResourcesDrawAndPrePick()
        {
            var s = StartedMatch(32, units: 4);
            var a = PlayerView.For(s, A);
            Assert.That((a.Mana, a.Energy, a.DrawPending, a.ActivePlayer, a.Round), Is.EqualTo((1, Catalog.EnergyPerTurn, true, A, 1)));
            Engine.Apply(s, new PrePickCommand(B, DrawCommand.Blind));
            var b = PlayerView.For(s, B);
            Assert.That((b.PrePickSlot, b.Mana, b.Energy, b.DrawPending), Is.EqualTo((DrawCommand.Blind, 0, 0, false)));
            Assert.That(PlayerView.For(s, A).PrePickSlot, Is.EqualTo(PrePickCommand.None)); // A's own (none), not B's
        }

        [Test]
        public void V09_PublicCountsMarketAndTowerHealth()
        {
            var s = StartedMatch(33);
            var v = PlayerView.For(s, A);
            Assert.That(v.OpponentHandCount, Is.EqualTo(s.GetHand(B).Count));
            Assert.That(v.Market.Select(c => c == null ? 0 : c.Id),
                Is.EqualTo(new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }.Select(p => s.GetMarket(p) == null ? 0 : s.GetMarket(p).Id)));
            Assert.That(v.PoolCounts, Is.EqualTo(new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }.Select(p => s.GetPool(p).Count)));
            Assert.That(v.EnemyTowerHealth, Is.EqualTo(s.GetTower(B).Health));
            Assert.That(v.EnemyTowerPos, Is.Null); // position only when Visible
            Assert.That(v.OwnTowerHealth, Is.EqualTo(s.GetTower(A).Health));
        }

        [Test]
        public void V05_VisibleEnemyShowsEffectsAndOverwatch_OwnTrapsOnly()
        {
            var b = new TestBoard();
            b.Unit(UnitClass.Guardian, A, H(-1, 1));
            int seen = b.Unit(UnitClass.Archer, B, H(0, 0));
            b.Unit(UnitClass.Rider, B, H(2, -3)); // hidden
            b.Effect(seen, "C-11");
            b.U(seen).OnOverwatch = true;
            int own = b.Trap(A, "C-20", H(-2, 2));
            b.Trap(B, "C-21", H(-1, 0)); // Visible cell, still hidden (C-33)
            b.Mana(B, 4).Energy(B, 2);
            var s = b.Build();
            var v = PlayerView.For(s, A);
            var enemy = v.EnemyUnits.Single();
            Assert.That((enemy.Id, enemy.BuffId, enemy.OnOverwatch, enemy.IsGhost), Is.EqualTo((seen, "C-11", true, false)));
            // Action flags and reveal timers are own-only.
            Assert.That((enemy.ActedThisTurn, enemy.MovedLastOwnTurn, enemy.MoveBonus, enemy.RevealedUntilTurn), Is.EqualTo((false, false, 0, -1)));
            Assert.That(v.OwnTraps.Select(t => (t.CardId, t.Pos)), Is.EqualTo(new[] { (own, H(-2, 2)) }));
            Assert.That(v.Cells.Single(c => c.Cell == H(2, -3)).Visibility, Is.EqualTo(CellVisibility.Hidden));
            Assert.That(v.Cells.Single(c => c.Cell == H(2, -3)).TerrainKnown, Is.False);
            Assert.That(v.Cells.Single(c => c.Cell == H(2, -3)).Unit, Is.Null);
            Assert.That(v.EnemyTowerPos, Is.Null);
            // No field of the view carries the opponent's Mana or Energy.
            Assert.That(typeof(PlayerView).GetMembers().Select(m => m.Name).Where(n => n.Contains("Opponent") && (n.Contains("Mana") || n.Contains("Energy"))), Is.Empty);
        }

        [Test]
        public void V05_HiddenCellsCarryNoData_ExploredCarrySnapshot()
        {
            var s = StartedMatch(34);
            var v = PlayerView.For(s, A);
            var fog = s.GetFog(A);
            foreach (var c in v.Cells)
            {
                Assert.That(c.Visibility, Is.EqualTo(fog.Get(c.Cell)));
                if (c.Visibility == CellVisibility.Hidden)
                    Assert.That((c.TerrainKnown, c.Unit, c.HasTower), Is.EqualTo((false, (UnitView)null, false)));
                else
                    Assert.That(c.Tile, Is.EqualTo(c.Visibility == CellVisibility.Visible ? s.Map.Get(c.Cell) : fog.GetLastSeen(c.Cell).Tile));
            }
        }
    }
}
