using System.Collections.Generic;
using System.Linq;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // Verifies Catalog.cs against docs/GDD.md (v2.1). Consistency tests first, then per-row values.
    public class CatalogTests
    {
        static SupportCardDef Card(string id) => Catalog.SupportCards.Single(c => c.Id == id);
        static UnitDef Unit(UnitClass c) => Catalog.Units.Single(u => u.Class == c);
        static QuestDef Quest(string id) => Catalog.Quests.Single(q => q.Id == id);
        static PassiveDef Passive(string id) => Catalog.Passives.Single(p => p.Id == id);

        // ---------- Consistency ----------

        [Test]
        public void Catalog_AllIdsUniqueAndMatchGddPattern()
        {
            var ids = new List<string>();
            ids.AddRange(Catalog.Units.Select(u => u.Id));
            ids.AddRange(Catalog.SupportCards.Select(c => c.Id));
            ids.AddRange(Catalog.Quests.Select(q => q.Id));
            ids.AddRange(Catalog.Passives.Select(p => p.Id));
            ids.AddRange(Catalog.MapEvents.Select(e => e.Id));
            ids.AddRange(Catalog.CharacterCards.Select(c => c.Id));

            Assert.That(ids, Is.Unique);
            foreach (var u in Catalog.Units) Assert.That(u.Id, Does.Match(@"^U-0[1-5]$"));
            foreach (var c in Catalog.SupportCards) Assert.That(c.Id, Does.Match(@"^C-(1\d|2[01])$"));
            foreach (var q in Catalog.Quests) Assert.That(q.Id, Does.Match(@"^Q-1\d$"));
            foreach (var p in Catalog.Passives) Assert.That(p.Id, Does.Match(@"^P-0[1-6]$"));
            foreach (var e in Catalog.MapEvents) Assert.That(e.Id, Does.Match(@"^E-1[0-2]$"));
        }

        [Test]
        public void Catalog_IdSetsMatchGdd()
        {
            Assert.That(Catalog.Units.Select(u => u.Id), Is.EqualTo(new[] { "U-01", "U-02", "U-03", "U-04", "U-05" }));
            Assert.That(Catalog.SupportCards.Select(c => c.Id),
                Is.EqualTo(Enumerable.Range(10, 12).Select(n => "C-" + n)));
            Assert.That(Catalog.Quests.Select(q => q.Id),
                Is.EqualTo(Enumerable.Range(10, 10).Select(n => "Q-" + n)));
            Assert.That(Catalog.Passives.Select(p => p.Id),
                Is.EqualTo(Enumerable.Range(1, 6).Select(n => "P-0" + n)));
            Assert.That(Catalog.MapEvents.Select(e => e.Id), Is.EqualTo(new[] { "E-10", "E-11", "E-12" }));
        }

        [Test]
        public void D01_CharacterPoolIs30_FiveClassesTimesThreeBiomesTimesTwo()
        {
            Assert.That(Catalog.CharacterCards.Count, Is.EqualTo(15));
            Assert.That(Catalog.CharacterCards.Select(c => c.Copies).Distinct(), Is.EqualTo(new[] { 2 }));
            Assert.That(Catalog.CharacterCards.Sum(c => c.Copies), Is.EqualTo(30));

            var biomes = new[] { Biome.Forest, Biome.Desert, Biome.Snow };
            foreach (var u in Catalog.Units)
                Assert.That(Catalog.CharacterCards.Where(c => c.Class == u.Class).Select(c => c.Biome),
                    Is.EquivalentTo(biomes), u.Class.ToString());
        }

        [Test]
        public void UX07_CharacterCardsHaveNoRarity()
        {
            foreach (var c in Catalog.CharacterCards) Assert.That(c.Rarity, Is.EqualTo(Rarity.None), c.Id);
        }

        [Test]
        public void C_SupportCardRarityIsCommonRareOrEpic()
        {
            foreach (var c in Catalog.SupportCards)
                Assert.That(c.Rarity, Is.AnyOf(Rarity.Common, Rarity.Rare, Rarity.Epic), c.Id);
        }

        [Test]
        public void D02_SupportPoolsTotal34_Buff19_DebuffTrap15()
        {
            int buff = Catalog.SupportCards.Where(c => c.Pool == SupportPool.Buff).Sum(c => c.Copies);
            int debuff = Catalog.SupportCards.Where(c => c.Pool == SupportPool.DebuffTrap).Sum(c => c.Copies);
            Assert.That(buff, Is.EqualTo(19));
            Assert.That(debuff, Is.EqualTo(15));
            Assert.That(buff + debuff, Is.EqualTo(34));

            // Buff pool = C-10…C-15, Debuff/Trap pool = C-16…C-21.
            foreach (var c in Catalog.SupportCards)
            {
                int n = int.Parse(c.Id.Substring(2));
                Assert.That(c.Pool, Is.EqualTo(n <= 15 ? SupportPool.Buff : SupportPool.DebuffTrap), c.Id);
            }
        }

        [Test]
        public void C_CopiesFollowRarity_Common4_Rare2_Epic1()
        {
            foreach (var c in Catalog.SupportCards)
            {
                int expected = c.Rarity == Rarity.Common ? 4 : c.Rarity == Rarity.Rare ? 2 : 1;
                Assert.That(c.Copies, Is.EqualTo(expected), c.Id);
            }
            Assert.That(Catalog.SupportCards.Count(c => c.Rarity == Rarity.Common), Is.EqualTo(6));
            Assert.That(Catalog.SupportCards.Count(c => c.Rarity == Rarity.Rare), Is.EqualTo(4));
            Assert.That(Catalog.SupportCards.Count(c => c.Rarity == Rarity.Epic), Is.EqualTo(2));
        }

        [Test]
        public void D03_DealsOneCharacterPerClass_FiveTotal()
        {
            Assert.That(Catalog.DealCharactersPerClass, Is.EqualTo(1));
            Assert.That(Catalog.DealCharactersPerClass * Catalog.Units.Count, Is.EqualTo(5));
            int dealt = 2 * Catalog.DealCharactersPerClass * Catalog.Units.Count; // both players
            int left = Catalog.CharacterCards.Sum(c => c.Copies) - dealt;
            Assert.That(left, Is.EqualTo(20));
        }

        [Test]
        public void D04_DealIsThreeCommonOneRare_NoEpics()
        {
            Assert.That(Catalog.DealCommonSupport, Is.EqualTo(3));
            Assert.That(Catalog.DealRareSupport, Is.EqualTo(1));
            foreach (var c in Catalog.SupportCards)
                Assert.That(Catalog.IsSupportDealtAtSetup(c), Is.EqualTo(c.Rarity != Rarity.Epic), c.Id);
        }

        // ---------- Board (B) ----------

        [Test]
        public void B01_RowLengthsTotal59()
        {
            Assert.That(Catalog.RowLengths, Is.EqualTo(new[] { 7, 6, 7, 6, 7, 6, 7, 6, 7 }));
            Assert.That(Catalog.RowLengths.Sum(), Is.EqualTo(59));
            Assert.That(Catalog.CellCount, Is.EqualTo(59));
        }

        [Test]
        public void B03_HomeZonesAreTwoRowsOf13Cells()
        {
            Assert.That(Catalog.HomeZoneRows, Is.EqualTo(2));
            Assert.That(Catalog.RowLengths[7] + Catalog.RowLengths[8], Is.EqualTo(13));
            Assert.That(Catalog.RowLengths[0] + Catalog.RowLengths[1], Is.EqualTo(13));
        }

        [Test]
        public void B22_MinEightPerBiome_ThreeToFiveSeeds()
        {
            Assert.That(Catalog.MinCellsPerBiome, Is.EqualTo(8));
            Assert.That(Catalog.BiomeSeedsMin, Is.EqualTo(3));
            Assert.That(Catalog.BiomeSeedsMax, Is.EqualTo(5));
            Assert.That(Catalog.MinCellsPerBiome * 3, Is.LessThanOrEqualTo(29));
        }

        [Test]
        public void B23_SpecialTileCounts()
        {
            Assert.That(Catalog.RuneStonesPerHalf, Is.EqualTo(2));
            Assert.That(Catalog.WellspringsPerHalf, Is.EqualTo(1));
            Assert.That(Catalog.SpecialTileRowMin, Is.EqualTo(3));
            Assert.That(Catalog.SpecialTileRowMax, Is.EqualTo(4));
        }

        [Test]
        public void B10_BiomeAttackBonusIsOne()
        {
            Assert.That(Catalog.BiomeAttackBonus, Is.EqualTo(1));
        }

        // ---------- Units (U) ----------

        [TestCase(UnitClass.Guardian, "U-01", 3, 2, 6, 1, 2, RangeKind.Adjacent, 1, 1, TestName = "U01_GuardianStats")]
        [TestCase(UnitClass.Rider, "U-02", 3, 3, 4, 3, 3, RangeKind.Adjacent, 1, 1, TestName = "U02_RiderStats")]
        [TestCase(UnitClass.Archer, "U-03", 2, 2, 3, 2, 3, RangeKind.StraightLine, 1, 3, TestName = "U03_ArcherStats")]
        [TestCase(UnitClass.Mage, "U-04", 3, 2, 3, 2, 2, RangeKind.Distance, 1, 2, TestName = "U04_MageStats")]
        [TestCase(UnitClass.Healer, "U-05", 2, 1, 3, 1, 2, RangeKind.Adjacent, 1, 1, TestName = "U05_HealerStats")]
        public void UnitStats(UnitClass cls, string id, int cost, int atk, int hp, int move, int sight,
            RangeKind range, int minRange, int maxRange)
        {
            var u = Unit(cls);
            Assert.That(u.Id, Is.EqualTo(id));
            Assert.That(u.Cost, Is.EqualTo(cost), "Cost (Mana)");
            Assert.That(u.Attack, Is.EqualTo(atk), "Attack");
            Assert.That(u.Health, Is.EqualTo(hp), "Health");
            Assert.That(u.Move, Is.EqualTo(move), "Move");
            Assert.That(u.Sight, Is.EqualTo(sight), "Sight");
            Assert.That(u.Range, Is.EqualTo(range), "Range kind");
            Assert.That(u.MinRange, Is.EqualTo(minRange), "Min range");
            Assert.That(u.MaxRange, Is.EqualTo(maxRange), "Max range");
        }

        [Test]
        public void U02_RiderChargeBonusIsOne() => Assert.That(Catalog.RiderChargeBonus, Is.EqualTo(1));

        [Test]
        public void U04_MageSplashIsOne() => Assert.That(Catalog.MageSplashDamage, Is.EqualTo(1));

        [Test]
        public void U05_HealAmountIsOne() => Assert.That(Catalog.HealerHealAmount, Is.EqualTo(1));

        [Test]
        public void U06_TowerStats()
        {
            var t = Catalog.Tower;
            Assert.That(t.Health, Is.EqualTo(10));
            Assert.That(t.Attack, Is.EqualTo(2));
            Assert.That(t.MinRange, Is.EqualTo(1));
            Assert.That(t.MaxRange, Is.EqualTo(2));
            Assert.That(t.Sight, Is.EqualTo(2));
            Assert.That(t.Move, Is.EqualTo(0));
            Assert.That(Catalog.TowersPerPlayer, Is.EqualTo(1));
        }

        [Test]
        public void U27_TowerShotDamageIsFixedTwo_OncePerOpponentTurn()
        {
            Assert.That(Catalog.Tower.Attack, Is.EqualTo(2));
            Assert.That(Catalog.TowerShotsPerOpponentTurn, Is.EqualTo(1));
        }

        [Test]
        public void U23_DeathDrawIsOne() => Assert.That(Catalog.DeathDrawCount, Is.EqualTo(1));

        // ---------- Support cards (C) ----------

        [TestCase("C-10", CardCategory.Power, SupportPool.Buff, EffectKind.AttackBonus, 2, DurationKind.Timed, 1, Rarity.Common, 4, TestName = "C10_Rage")]
        [TestCase("C-11", CardCategory.Power, SupportPool.Buff, EffectKind.AttackBonus, 1, DurationKind.Permanent, 2, Rarity.Rare, 2, TestName = "C11_GiantStrength")]
        [TestCase("C-12", CardCategory.Protection, SupportPool.Buff, EffectKind.BlockNextDamage, 0, DurationKind.Permanent, 1, Rarity.Common, 4, TestName = "C12_Shield")]
        [TestCase("C-13", CardCategory.Protection, SupportPool.Buff, EffectKind.Heal, 3, DurationKind.Instant, 1, Rarity.Common, 4, TestName = "C13_HealingPotion")]
        [TestCase("C-14", CardCategory.Movement, SupportPool.Buff, EffectKind.MoveBonus, 2, DurationKind.Instant, 1, Rarity.Common, 4, TestName = "C14_WindStep")]
        [TestCase("C-15", CardCategory.Movement, SupportPool.Buff, EffectKind.Teleport, 0, DurationKind.Instant, 3, Rarity.Epic, 1, TestName = "C15_Teleport")]
        [TestCase("C-16", CardCategory.Curse, SupportPool.DebuffTrap, EffectKind.AttackPenalty, 2, DurationKind.Timed, 1, Rarity.Common, 4, TestName = "C16_Weakness")]
        [TestCase("C-17", CardCategory.Curse, SupportPool.DebuffTrap, EffectKind.Poison, 1, DurationKind.Timed, 2, Rarity.Rare, 2, TestName = "C17_Poison")]
        [TestCase("C-18", CardCategory.Control, SupportPool.DebuffTrap, EffectKind.Push, 2, DurationKind.Instant, 1, Rarity.Common, 4, TestName = "C18_Push")]
        [TestCase("C-19", CardCategory.Control, SupportPool.DebuffTrap, EffectKind.Root, 0, DurationKind.Timed, 2, Rarity.Rare, 2, TestName = "C19_Root")]
        [TestCase("C-20", CardCategory.Trap, SupportPool.DebuffTrap, EffectKind.TrapDamage, 3, DurationKind.Permanent, 1, Rarity.Rare, 2, TestName = "C20_SpikeTrap")]
        [TestCase("C-21", CardCategory.Trap, SupportPool.DebuffTrap, EffectKind.TrapTeleportHome, 0, DurationKind.Permanent, 2, Rarity.Epic, 1, TestName = "C21_MirrorTrap")]
        public void SupportCardRow(string id, CardCategory cat, SupportPool pool, EffectKind effect, int amount,
            DurationKind duration, int cost, Rarity rarity, int copies)
        {
            var c = Card(id);
            Assert.That(c.Category, Is.EqualTo(cat), "Category");
            Assert.That(c.Pool, Is.EqualTo(pool), "Pool");
            Assert.That(c.Effect, Is.EqualTo(effect), "Effect");
            Assert.That(c.Amount, Is.EqualTo(amount), "Amount");
            Assert.That(c.Duration, Is.EqualTo(duration), "Duration");
            Assert.That(c.Cost, Is.EqualTo(cost), "Cost (Mana)");
            Assert.That(c.Rarity, Is.EqualTo(rarity), "Rarity");
            Assert.That(c.Copies, Is.EqualTo(copies), "Copies");
        }

        [Test]
        public void C02_ControlRangeIsOne() => Assert.That(Catalog.ControlRange, Is.EqualTo(1));

        [Test]
        public void C04_TimedEffectsLastTwoTurns() => Assert.That(Catalog.TimedEffectTurns, Is.EqualTo(2));

        [Test]
        public void C05_MaxOneBuffAndOneDebuff()
        {
            Assert.That(Catalog.MaxBuffsPerUnit, Is.EqualTo(1));
            Assert.That(Catalog.MaxDebuffsPerUnit, Is.EqualTo(1));
        }

        [Test]
        public void C31_MaxTwoActiveTraps() => Assert.That(Catalog.MaxActiveTraps, Is.EqualTo(2));

        // ---------- Pools, market (D) ----------

        [Test]
        public void D05_MarketHasThreeSlots() => Assert.That(Catalog.MarketSlots, Is.EqualTo(3));

        [Test]
        public void D07_HandLimitIsSix() => Assert.That(Catalog.HandLimit, Is.EqualTo(6));

        // ---------- Setup (S) ----------

        [Test]
        public void S03_QuestOfferFivePickThree()
        {
            Assert.That(Catalog.QuestOffer, Is.EqualTo(5));
            Assert.That(Catalog.QuestPick, Is.EqualTo(3));
            Assert.That(Catalog.QuestOffer, Is.LessThanOrEqualTo(Catalog.Quests.Count));
        }

        [Test]
        public void S04_PassiveOfferThreePickOne()
        {
            Assert.That(Catalog.PassiveOffer, Is.EqualTo(3));
            Assert.That(Catalog.PassivePick, Is.EqualTo(1));
            Assert.That(Catalog.PassiveOffer, Is.LessThanOrEqualTo(Catalog.Passives.Count));
        }

        [Test]
        public void S05_PlacementIs45Seconds_AtLeastThreeCharacters()
        {
            Assert.That(Catalog.PlacementSeconds, Is.EqualTo(45));
            Assert.That(Catalog.MinPlacedCharacters, Is.EqualTo(3));
        }

        // ---------- Turn (T) ----------

        [Test]
        public void T01_ManaCapIsSix() => Assert.That(Catalog.ManaCap, Is.EqualTo(6));

        [Test]
        public void T02_PlayerBFirstRoundBonusIsOne() => Assert.That(Catalog.ManaFirstRoundBonusB, Is.EqualTo(1));

        [Test]
        public void T03_WellspringBonusIsOne() => Assert.That(Catalog.WellspringManaBonus, Is.EqualTo(1));

        [Test]
        public void T05_EnergyPerTurnIsThree_ActionCostsOne()
        {
            Assert.That(Catalog.EnergyPerTurn, Is.EqualTo(3));
            Assert.That(Catalog.EnergyPerAction, Is.EqualTo(1));
            Assert.That(Catalog.ActionsPerUnitPerTurn, Is.EqualTo(1));
        }

        [Test]
        public void T09_TurnTimers()
        {
            Assert.That(Catalog.TurnSeconds, Is.EqualTo(30));
            Assert.That(Catalog.TimeBankSeconds, Is.EqualTo(60));
            Assert.That(Catalog.MaxConsecutiveTimeouts, Is.EqualTo(3));
        }

        // ---------- Quests (Q) ----------

        [Test]
        public void Q_PoolHasTenQuests() => Assert.That(Catalog.Quests.Count, Is.EqualTo(10));

        [Test]
        public void Q01_ThreeQuestsPerPlayer() => Assert.That(Catalog.QuestsPerPlayer, Is.EqualTo(3));

        [Test]
        public void Q05_HoldMeansTwoConsecutiveTurns() => Assert.That(Catalog.QuestHoldTurns, Is.EqualTo(2));

        [TestCase("Q-10", 1, 0, true, false, TestName = "Q10_RuneKeeper")]
        [TestCase("Q-11", 2, 0, false, false, TestName = "Q11_DoubleRune")]
        [TestCase("Q-12", 2, 0, false, false, TestName = "Q12_Hunter")]
        [TestCase("Q-13", 5, 0, false, false, TestName = "Q13_Siege")]
        [TestCase("Q-14", 7, 8, false, true, TestName = "Q14_StrongKeep")]
        [TestCase("Q-15", 3, 0, false, false, TestName = "Q15_BiomeMaster")]
        [TestCase("Q-16", 0, 6, false, true, TestName = "Q16_Flawless")]
        [TestCase("Q-17", 1, 0, false, false, TestName = "Q17_Trapper")]
        [TestCase("Q-18", 1, 0, true, false, TestName = "Q18_WellspringLord")]
        [TestCase("Q-19", 2, 0, false, false, TestName = "Q19_DeepRaid")]
        public void QuestRow(string id, int amount, int round, bool hold, bool canFail)
        {
            var q = Quest(id);
            Assert.That(q.Amount, Is.EqualTo(amount), "Amount");
            Assert.That(q.Round, Is.EqualTo(round), "Round");
            Assert.That(q.Hold, Is.EqualTo(hold), "Hold (Q-05)");
            Assert.That(q.CanFail, Is.EqualTo(canFail), "CanFail (Q-04)");
        }

        // ---------- Passives (P) ----------

        [Test]
        public void P_PoolHasSixPassives() => Assert.That(Catalog.Passives.Count, Is.EqualTo(6));

        [TestCase("P-01", 2, 0, true, TestName = "P01_LastBreath")]
        [TestCase("P-02", 1, 0, false, TestName = "P02_TrapMaster")]
        [TestCase("P-03", 2, 3, false, TestName = "P03_QuickStart")]
        [TestCase("P-04", 3, 0, false, TestName = "P04_ThickWall")]
        [TestCase("P-05", 1, 0, true, TestName = "P05_Merchant")]
        [TestCase("P-06", 3, 0, false, TestName = "P06_PortalWarden")]
        public void PassiveRow(string id, int amount, int round, bool oncePerMatch)
        {
            var p = Passive(id);
            Assert.That(p.Amount, Is.EqualTo(amount), "Amount");
            Assert.That(p.Round, Is.EqualTo(round), "Round");
            Assert.That(p.OncePerMatch, Is.EqualTo(oncePerMatch), "OncePerMatch");
        }

        // ---------- Map events (E) ----------

        [Test]
        public void E01_EventRounds_3_6_9_12() =>
            Assert.That(Catalog.EventRounds, Is.EqualTo(new[] { 3, 6, 9, 12 }));

        [Test]
        public void E02_AnnouncedOneRoundEarlier() => Assert.That(Catalog.EventAnnounceLead, Is.EqualTo(1));

        [Test]
        public void E10_EarthquakeAffectsOneMirrorPair() =>
            Assert.That(Catalog.MapEvents.Single(e => e.Id == "E-10").Pairs, Is.EqualTo(1));

        [Test]
        public void E12_RuneRainAffectsOneMirrorPair() =>
            Assert.That(Catalog.MapEvents.Single(e => e.Id == "E-12").Pairs, Is.EqualTo(1));

        [Test]
        public void E11_ClusterLimits()
        {
            Assert.That(Catalog.BiomeShiftMaxCells, Is.EqualTo(7));
            Assert.That(Catalog.BiomeShiftMinPortalDistance, Is.EqualTo(3));
        }

        // ---------- Win (W) ----------

        [Test]
        public void W01_PortalNeedsTwoQuests() => Assert.That(Catalog.PortalQuestsRequired, Is.EqualTo(2));

        [Test]
        public void W03_RoundLimitIs15() => Assert.That(Catalog.RoundLimit, Is.EqualTo(15));

        // ---------- AI ----------

        [Test]
        public void AI03_EasyPicksFromTopThree() => Assert.That(Catalog.AiEasyTopMoves, Is.EqualTo(3));

        [Test]
        public void AI05_MaxOneSecondPerMove() => Assert.That(Catalog.AiMaxSecondsPerMove, Is.EqualTo(1));
    }
}
