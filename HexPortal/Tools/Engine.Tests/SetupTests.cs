using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // GDD §6 setup: S-01…S-08, D-07 (v2.8 S1–S4: fixed order, no undo, one clock, client picks the seats).
    public class SetupTests
    {
        static List<Hex> Home(PlayerId p) => Board.Cells.Where(h => Board.IsHomeZone(h, p)).ToList();

        static void AssertRejected(GameState s, ICommand c)
        {
            var before = StateHash.Compute(s);
            Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, c), c.ToString());
            Assert.That(StateHash.Compute(s), Is.EqualTo(before), c.ToString());
        }

        static int AddToHand(GameState s, PlayerId p, string defId)
        {
            var d = Catalog.SupportCards.First(c => c.Id == defId);
            var card = s.NewCard(d);
            s.HandList(p).Add(card);
            return card.Id;
        }

        [Test]
        public void S01_CreateGeneratesTheSeedMapAndStartsInSetup()
        {
            var s = Match.Create(7);
            var map = MapGenerator.Generate(7);
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Setup));
            Assert.That(Board.Cells.All(h => s.Map.Get(h).Equals(map.Get(h))), Is.True);
            Assert.That(s.Map.RequestedSeed, Is.EqualTo(7UL));
            Assert.That(s.GetTower(A).IsPlaced || s.GetTower(B).IsPlaced, Is.False);
            Assert.That(s.Units, Is.Empty);
            Assert.That(s.IsOver, Is.False);
            Assert.That(StateHash.Compute(Match.Create(7)), Is.EqualTo(StateHash.Compute(s)));
            Assert.That(StateHash.Compute(Match.Create(8)), Is.Not.EqualTo(StateHash.Compute(s)));
        }

        [Test]
        public void S02_HandsAreDealtAndTheMarketIsOpen()
        {
            var s = Match.Create(3);
            foreach (var p in Players)
            {
                var hand = s.GetHand(p);
                Assert.That(hand.Where(c => c.IsCharacter).Select(c => c.Character.Class), Is.EquivalentTo(Catalog.Units.Select(u => u.Class)));
                Assert.That(hand.Count(c => !c.IsCharacter && c.Support.Rarity == Rarity.Common), Is.EqualTo(Catalog.DealCommonSupport));
                Assert.That(hand.Count(c => !c.IsCharacter && c.Support.Rarity == Rarity.Rare), Is.EqualTo(Catalog.DealRareSupport));
            }
            foreach (var slot in new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap })
                Assert.That(s.GetMarket(slot), Is.Not.Null);
            Assert.That(s.GetMana(A) + s.GetMana(B) + s.GetEnergy(A) + s.GetEnergy(B), Is.EqualTo(0));
        }

        [Test]
        public void S03_FiveDistinctOfferedQuests_ChooseThree()
        {
            var s = Match.Create(5);
            foreach (var p in Players)
            {
                var offer = s.GetQuestOffer(p);
                Assert.That(offer.Count, Is.EqualTo(Catalog.QuestOffer));
                Assert.That(offer.Distinct().Count(), Is.EqualTo(Catalog.QuestOffer));
                Assert.That(offer.All(q => Catalog.Quests.Contains(q)), Is.True);
                Assert.That(s.GetQuestChoices(p), Is.Empty);
                Assert.That(Setup.StepOf(s, p), Is.EqualTo(SetupStep.Quests));
            }
            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal.Count, Is.EqualTo(10)); // C(5,3)
            Assert.That(legal.All(c => c is ChooseQuestsCommand), Is.True);

            var ids = s.GetQuestOffer(A).Select(q => q.Id).ToList();
            var pick = new ChooseQuestsCommand(A, new[] { ids[4], ids[0], ids[2] }); // any order
            Assert.That(legal, Does.Contain(pick));
            var ev = Engine.Apply(s, pick);
            Assert.That(s.GetQuestChoices(A).Select(q => q.Id), Is.EquivalentTo(new[] { ids[0], ids[2], ids[4] }));
            Assert.That(ev.OfType<QuestsChosen>().Single().Player, Is.EqualTo(A));
            Assert.That(Setup.StepOf(s, A), Is.EqualTo(SetupStep.Passive));
            Assert.That(Setup.StepOf(s, B), Is.EqualTo(SetupStep.Quests));
        }

        [Test]
        public void S03_OffersMayOverlapBetweenPlayersAndVaryBySeed()
        {
            bool overlap = false, differ = false;
            var firstOffers = new HashSet<string>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var s = Match.Create(seed);
                var a = s.GetQuestOffer(A).Select(q => q.Id).ToList();
                var b = s.GetQuestOffer(B).Select(q => q.Id).ToList();
                if (a.Intersect(b).Any()) overlap = true;
                if (!a.SequenceEqual(b)) differ = true;
                firstOffers.Add(string.Join(",", a));
            }
            Assert.That(overlap && differ, Is.True);
            Assert.That(firstOffers.Count, Is.GreaterThan(10));
        }

        [Test]
        public void S03_IllegalQuestChoicesAreRejected()
        {
            var s = Match.Create(9);
            var ids = s.GetQuestOffer(A).Select(q => q.Id).ToList();
            var outside = Catalog.Quests.Select(q => q.Id).First(id => !ids.Contains(id));
            AssertRejected(s, new ChooseQuestsCommand(A, new[] { ids[0], ids[1] }));
            AssertRejected(s, new ChooseQuestsCommand(A, new[] { ids[0], ids[1], ids[2], ids[3] }));
            AssertRejected(s, new ChooseQuestsCommand(A, new[] { ids[0], ids[0], ids[1] }));
            AssertRejected(s, new ChooseQuestsCommand(A, new[] { ids[0], ids[1], outside }));
            Engine.Apply(s, new ChooseQuestsCommand(A, new[] { ids[0], ids[1], ids[2] }));
            AssertRejected(s, new ChooseQuestsCommand(A, new[] { ids[0], ids[1], ids[3] })); // no undo (S1)
        }

        [Test]
        public void S04_ThreeDistinctOfferedPassives_ChooseOne()
        {
            var s = Match.Create(12);
            var offer = s.GetPassiveOffer(A);
            Assert.That(offer.Count, Is.EqualTo(Catalog.PassiveOffer));
            Assert.That(offer.Distinct().Count(), Is.EqualTo(Catalog.PassiveOffer));
            AssertRejected(s, new ChoosePassiveCommand(A, offer[0].Id)); // quests first (S1)
            Engine.Apply(s, Engine.GetLegalCommands(s, A).First());
            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal, Is.EqualTo(offer.Select(d => (ICommand)new ChoosePassiveCommand(A, d.Id)).ToList()));
            var outside = Catalog.Passives.First(d => !offer.Contains(d));
            AssertRejected(s, new ChoosePassiveCommand(A, outside.Id));
            var ev = Engine.Apply(s, new ChoosePassiveCommand(A, offer[1].Id));
            Assert.That(s.GetPassiveChoice(A), Is.SameAs(offer[1]));
            Assert.That(ev.OfType<PassiveChosen>().Single().Player, Is.EqualTo(A));
            AssertRejected(s, new ChoosePassiveCommand(A, offer[2].Id)); // no undo
            Assert.That(Setup.StepOf(s, A), Is.EqualTo(SetupStep.Tower));
        }

        [Test]
        public void S05_FixedOrder_TowerBeforeUnitsAndTraps()
        {
            var s = Match.Create(4);
            var cell = Home(A)[0];
            int character = s.GetHand(A).First(c => c.IsCharacter).Id;
            AssertRejected(s, new PlaceTowerCommand(A, cell));
            AssertRejected(s, new PlaceUnitCommand(A, character, cell));
            AssertRejected(s, new FinishSetupCommand(A));
            Engine.Apply(s, Engine.GetLegalCommands(s, A).First());
            Engine.Apply(s, Engine.GetLegalCommands(s, A).First());
            AssertRejected(s, new PlaceUnitCommand(A, character, cell));

            // Tower: any empty cell of the own home zone (S-05), never elsewhere.
            var towerCells = Engine.GetLegalCommands(s, A).Cast<PlaceTowerCommand>().Select(c => c.Cell).ToList();
            Assert.That(towerCells, Is.EquivalentTo(Home(A)));
            AssertRejected(s, new PlaceTowerCommand(A, Home(B)[0]));
            AssertRejected(s, new PlaceTowerCommand(A, Board.Portal));
            var ev = Engine.Apply(s, new PlaceTowerCommand(A, cell));
            Assert.That(s.GetTower(A).IsPlaced, Is.True);
            Assert.That(s.GetTower(A).Pos, Is.EqualTo(cell));
            Assert.That(ev.OfType<TowerPlaced>().Single().Cell, Is.EqualTo(cell));
            AssertRejected(s, new PlaceTowerCommand(A, Home(A)[1])); // once
            Assert.That(Setup.StepOf(s, A), Is.EqualTo(SetupStep.Placement));
        }

        [Test]
        public void S05_CharactersGoOnEmptyOwnHomeCells_NoMana()
        {
            var s = Match.Create(6);
            SetupPlayer(s, A, 0, finish: false);
            var towerCell = s.GetTower(A).Pos;
            var character = s.GetHand(A).First(c => c.IsCharacter);
            var support = s.GetHand(A).First(c => !c.IsCharacter);
            var places = Engine.GetLegalCommands(s, A).OfType<PlaceUnitCommand>().ToList();
            Assert.That(places.Where(c => c.CardId == character.Id).Select(c => c.Cell), Is.EquivalentTo(Home(A).Where(h => h != towerCell)));
            Assert.That(places.Select(c => c.CardId).Distinct(), Is.EquivalentTo(s.GetHand(A).Where(c => c.IsCharacter).Select(c => c.Id)));
            AssertRejected(s, new PlaceUnitCommand(A, character.Id, towerCell));
            AssertRejected(s, new PlaceUnitCommand(A, character.Id, Home(B)[0]));
            AssertRejected(s, new PlaceUnitCommand(A, support.Id, Home(A).First(h => h != towerCell)));
            AssertRejected(s, new PlaceUnitCommand(B, character.Id, Home(B)[0])); // not B's card

            var cell = Home(A).First(h => h != towerCell);
            int handBefore = s.GetHand(A).Count;
            var ev = Engine.Apply(s, new PlaceUnitCommand(A, character.Id, cell));
            var u = s.Units.Single();
            Assert.That((u.Owner, u.Class, u.Biome, u.Pos, u.Health), Is.EqualTo((A, character.Character.Class, character.Character.Biome, cell, u.Def.Health)));
            Assert.That(s.GetHand(A).Count, Is.EqualTo(handBefore - 1));
            Assert.That(s.GetMana(A), Is.EqualTo(0));
            Assert.That(ev.OfType<UnitDeployed>().Single().Cell, Is.EqualTo(cell));
            AssertRejected(s, new PlaceUnitCommand(A, s.GetHand(A).First(c => c.IsCharacter).Id, cell)); // occupied
        }

        [Test]
        public void S05_TrapsGoInOwnHalfAndControlZone_UpToTheLimit_NoMana()
        {
            var s = Match.Create(10);
            SetupPlayer(s, A, 1, finish: false);
            int t1 = AddToHand(s, A, "C-20"), t2 = AddToHand(s, A, "C-21"), t3 = AddToHand(s, A, "C-20");
            var zone = ControlZone.Cells(s, A);
            var expected = Board.Cells.Where(h => zone.Contains(h) && Board.IsInHalf(h, A) && Movement.IsEmpty(s, h)).ToList();
            var cells = Engine.GetLegalCommands(s, A).OfType<PlaceTrapCommand>().Where(c => c.CardId == t1).Select(c => c.Cell).ToList();
            Assert.That(cells, Is.EquivalentTo(expected));
            var trapCards = s.GetHand(A).Where(c => !c.IsCharacter && c.Support.Category == CardCategory.Trap).Select(c => c.Id).ToList();
            Assert.That(trapCards, Is.SupersetOf(new[] { t1, t2, t3 })); // the deal may have given one more (C-20 is rare)
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PlaceTrapCommand>().Select(c => c.CardId).Distinct(), Is.EquivalentTo(trapCards));
            var far = Board.Cells.First(h => Board.IsInHalf(h, A) && !zone.Contains(h) && Movement.IsEmpty(s, h));
            AssertRejected(s, new PlaceTrapCommand(A, t1, far));
            AssertRejected(s, new PlaceTrapCommand(A, t1, s.GetTower(A).Pos));
            AssertRejected(s, new PlaceTrapCommand(A, s.GetHand(A).First(c => c.IsCharacter).Id, expected[0]));
            AssertRejected(s, new PlaceTrapCommand(A, AddToHand(s, A, "C-10"), expected[0])); // not a trap card

            var ev = Engine.Apply(s, new PlaceTrapCommand(A, t1, expected[0]));
            Assert.That(ev.OfType<TrapPlaced>().Single().Cell, Is.EqualTo(expected[0]));
            AssertRejected(s, new PlaceTrapCommand(A, t2, expected[0])); // C-30 v2.7: not on an own trap
            Engine.Apply(s, new PlaceTrapCommand(A, t2, expected[1]));
            Assert.That(s.Traps.Count, Is.EqualTo(Catalog.MaxActiveTraps));
            AssertRejected(s, new PlaceTrapCommand(A, t3, expected[2])); // C-31
            Assert.That(Engine.GetLegalCommands(s, A).OfType<PlaceTrapCommand>(), Is.Empty);
            Assert.That(s.GetMana(A), Is.EqualTo(0));
        }

        [Test]
        public void S05_FinishNeedsTowerAndMinimumCharacters()
        {
            var s = Match.Create(11);
            SetupPlayer(s, A, Catalog.MinPlacedCharacters - 1, finish: false);
            Assert.That(Engine.GetLegalCommands(s, A).OfType<FinishSetupCommand>(), Is.Empty);
            AssertRejected(s, new FinishSetupCommand(A));
            Engine.Apply(s, Engine.GetLegalCommands(s, A).OfType<PlaceUnitCommand>().First());
            Assert.That(Engine.GetLegalCommands(s, A).Last(), Is.EqualTo(new FinishSetupCommand(A)));
            var ev = Engine.Apply(s, new FinishSetupCommand(A));
            Assert.That(ev.OfType<SetupFinished>().Single().Player, Is.EqualTo(A));
            Assert.That(s.IsSetupFinished(A), Is.True);
            Assert.That(Setup.StepOf(s, A), Is.EqualTo(SetupStep.Done));
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Setup));
            Assert.That(Engine.GetLegalCommands(s, A), Is.Empty);
            AssertRejected(s, new FinishSetupCommand(A));
            AssertRejected(s, new EndTurnCommand(A)); // not playing yet
            AssertRejected(s, new PrePickCommand(B, DrawCommand.Blind));
        }

        [Test]
        public void S05_PlayersSetUpIndependently()
        {
            var s = Match.Create(13);
            SetupPlayer(s, B, 3, finish: false); // B first: ActivePlayer is ignored during setup
            SetupPlayer(s, A, 4);
            Engine.Apply(s, new FinishSetupCommand(B));
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Playing));
        }

        [Test]
        public void S07_BothFinished_Round1StartsWithAsTurnAndDraw()
        {
            var s = Match.Create(14);
            SetupPlayer(s, A, 4); // hand 9 - 4 = 5 < 6: A must draw
            var ev = SetupPlayer(s, B, 3);
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That((s.Round, s.ActivePlayer), Is.EqualTo((1, A)));
            Assert.That(s.GetEnergy(A), Is.EqualTo(Catalog.EnergyPerTurn));
            Assert.That(s.GetMana(A), Is.EqualTo(1));
            Assert.That(s.GetMana(B), Is.EqualTo(0));
            Assert.That(s.IsDrawPending(A), Is.True);
            Assert.That(ev.OfType<TurnStarted>().Single().Player, Is.EqualTo(A));
            Assert.That(Engine.GetLegalCommands(s, A).All(c => c is DrawCommand), Is.True);
            Assert.That(s.Units.Count(u => u.Owner == A), Is.EqualTo(4));
        }

        [Test]
        public void D07_HandAboveLimitAfterSetupIsKept_DrawSkipped()
        {
            var s = Match.Create(15);
            for (int i = 0; i < 3; i++) AddToHand(s, A, "C-10"); // 12 cards dealt/added
            SetupPlayer(s, A, 3);
            int hand = s.GetHand(A).Count;
            Assert.That(hand, Is.GreaterThan(Catalog.HandLimit));
            var ev = SetupPlayer(s, B, 3);
            Assert.That(s.GetHand(A).Count, Is.EqualTo(hand)); // nothing discarded (S4)
            Assert.That(s.IsDrawPending(A), Is.False);
            Assert.That(ev.OfType<CardDrawn>(), Is.Empty);
        }

        [Test]
        public void S06_TimeoutCompletesMissingStepsRandomly_NoTraps()
        {
            var s = Match.Create(16);
            var ids = s.GetQuestOffer(B).Select(q => q.Id).ToList();
            Engine.Apply(s, new ChooseQuestsCommand(B, new[] { ids[1], ids[3], ids[4] })); // kept
            var evA = Engine.Apply(s, new SetupTimeoutCommand(A));
            var evB = Engine.Apply(s, new SetupTimeoutCommand(B));
            foreach (var p in Players)
            {
                Assert.That(s.GetQuestChoices(p).Count, Is.EqualTo(Catalog.QuestPick));
                Assert.That(s.GetQuestChoices(p).All(q => s.GetQuestOffer(p).Contains(q)), Is.True);
                Assert.That(s.GetPassiveOffer(p), Does.Contain(s.GetPassiveChoice(p)));
                Assert.That(Board.IsHomeZone(s.GetTower(p).Pos, p), Is.True);
                Assert.That(s.Units.Where(u => u.Owner == p).Select(u => u.Pos).All(h => Board.IsHomeZone(h, p)), Is.True);
                Assert.That(s.Units.Count(u => u.Owner == p), Is.EqualTo(Catalog.MinPlacedCharacters));
            }
            Assert.That(s.GetQuestChoices(B).Select(q => q.Id), Is.EquivalentTo(new[] { ids[1], ids[3], ids[4] }));
            Assert.That(s.Traps, Is.Empty);
            Assert.That(evA.OfType<SetupFinished>().Single().Player, Is.EqualTo(A));
            Assert.That(evB.OfType<SetupFinished>().Single().Player, Is.EqualTo(B));
            Assert.That(s.Phase, Is.EqualTo(GamePhase.Playing));

            // Deterministic, and random: some other seed gives a different auto-placement.
            var again = Match.Create(16);
            Engine.Apply(again, new ChooseQuestsCommand(B, new[] { ids[1], ids[3], ids[4] }));
            Engine.Apply(again, new SetupTimeoutCommand(A));
            Engine.Apply(again, new SetupTimeoutCommand(B));
            Assert.That(StateHash.Compute(again), Is.EqualTo(StateHash.Compute(s)));
            var towers = new HashSet<Hex>();
            for (ulong seed = 20; seed < 30; seed++)
            {
                var t = Match.Create(seed);
                Engine.Apply(t, new SetupTimeoutCommand(A));
                towers.Add(t.GetTower(A).Pos);
            }
            Assert.That(towers.Count, Is.GreaterThan(1));
        }

        [Test]
        public void S06_TimeoutKeepsPlacedPiecesAndTopsUpCharacters()
        {
            var s = Match.Create(17);
            SetupPlayer(s, A, 1, finish: false);
            var tower = s.GetTower(A).Pos;
            var first = s.Units.Single();
            Engine.Apply(s, new SetupTimeoutCommand(A));
            Assert.That(s.GetTower(A).Pos, Is.EqualTo(tower));
            Assert.That(s.GetUnit(first.Id).Pos, Is.EqualTo(first.Pos));
            Assert.That(s.Units.Count(u => u.Owner == A), Is.EqualTo(Catalog.MinPlacedCharacters));
            Assert.That(s.IsSetupFinished(A), Is.True);
            AssertRejected(s, new SetupTimeoutCommand(A)); // already finished
        }

        [Test]
        public void S08_NothingOfASetupReachesTheOtherPlayerUntilBothFinished()
        {
            var s = Match.Create(18);
            var ev = SetupPlayer(s, A, 4);
            int trap = AddToHand(s, A, "C-20"); // (added after finishing: never shown anyway)
            Assert.That(EventFilter.For(ev, B).Select(e => e.GetType()), Is.EqualTo(new[] { typeof(SetupFinished) }));
            Assert.That(EventFilter.For(ev, A).Count, Is.EqualTo(ev.Count));
            var view = PlayerView.For(s, B);
            Assert.That(view.EnemyUnits, Is.Empty);
            Assert.That(view.Cells.Where(c => c.Unit != null || c.HasTower), Is.Empty);
            Assert.That(view.EnemyTowerPos, Is.Null);
            Assert.That(view.OpponentSetupFinished, Is.True);
            Assert.That(view.OpponentHandCount, Is.EqualTo(DealtHandSize)); // not the live count (PM decision)
            Assert.That(view.Hand.Any(c => c.Id == trap), Is.False);
            Assert.That(PlayerView.For(s, A).OwnUnits.Count, Is.EqualTo(4));
        }

        [Test]
        public void S08_OpponentHandCountIsTheDealtCountUntilBothFinished()
        {
            var s = Match.Create(20);
            Assert.That(DealtHandSize, Is.EqualTo(9));
            Assert.That(s.GetDealtHandCount(A), Is.EqualTo(DealtHandSize));
            var seen = new List<int> { PlayerView.For(s, B).OpponentHandCount };
            var events = new List<GameEvent>();
            events.AddRange(SetupPlayer(s, A, 2, finish: false));
            seen.Add(PlayerView.For(s, B).OpponentHandCount);
            int trap = AddToHand(s, A, "C-20");
            events.AddRange(Engine.Apply(s, Engine.GetLegalCommands(s, A).OfType<PlaceTrapCommand>().First(c => c.CardId == trap)));
            events.AddRange(Engine.Apply(s, Engine.GetLegalCommands(s, A).OfType<PlaceUnitCommand>().First()));
            events.AddRange(Engine.Apply(s, new FinishSetupCommand(A)));
            seen.Add(PlayerView.For(s, B).OpponentHandCount);
            Assert.That(s.GetHand(A).Count, Is.EqualTo(DealtHandSize - 3)); // live count changed...
            Assert.That(seen, Is.All.EqualTo(DealtHandSize));              // ...the view did not
            Assert.That(EventFilter.For(events, B).Select(e => e.GetType()), Is.EqualTo(new[] { typeof(SetupFinished) }));

            SetupPlayer(s, B, 3); // both finished: the real count
            Assert.That(PlayerView.For(s, B).OpponentHandCount, Is.EqualTo(s.GetHand(A).Count));
            Assert.That(PlayerView.For(s, A).OpponentHandCount, Is.EqualTo(s.GetHand(B).Count));
        }

        [Test]
        public void S08_SetupEventsOfOnePlayerAreOwnerOnly()
        {
            var s = Match.Create(19);
            SetupPlayer(s, A, 1, finish: false);
            int t = AddToHand(s, A, "C-20");
            var place = Engine.GetLegalCommands(s, A).OfType<PlaceTrapCommand>().First(c => c.CardId == t);
            var ev = Engine.Apply(s, place);
            Assert.That(EventFilter.For(ev, B), Is.Empty);
            Assert.That(EventFilter.For(ev, A).OfType<TrapPlaced>().Single().CardId, Is.EqualTo(t));
        }
    }
}
