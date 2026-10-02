using System;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // §13 greedy AI (AI-01…AI-06): legality, PlayerView-only proofs, tactical scenarios, setup, pre-pick, AI-vs-AI matches.
    public class AiTests
    {
        static Hex D(int x, int y) => TestBoard.D59(x, y);
        static Hex H(int q, int r) => new Hex(q, r);

        /// <summary>The cell a plain attack or a move-attack of this unit hits; null for anything else.</summary>
        static Hex? HitTarget(ICommand c, int unitId) =>
            c is AttackCommand a && a.UnitId == unitId ? a.Target
            : c is MoveAttackCommand m && m.UnitId == unitId ? m.Target : (Hex?)null;

        static string Doubled(Hex h) => h.ToString();

        /// <summary>Every cell p has never seen becomes Explored with its real terrain (no exploration value left).</summary>
        static void ExploreAll(GameState s, PlayerId p)
        {
            var fog = s.GetFog(p);
            foreach (var h in Board.Cells)
                if (fog.Get(h) == CellVisibility.Hidden) fog.Set(h, CellVisibility.Explored, new LastSeen(s.Map.Get(h)));
        }

        static ICommand Choose(GameState s, PlayerId p, AiLevel level, ulong seed = 1) =>
            GreedyAi.Choose(PlayerView.For(s, p), Engine.GetLegalCommands(s, p), level, new Rng(seed));

        static List<AiCandidate> Rank(GameState s, PlayerId p) =>
            GreedyAi.Rank(PlayerView.For(s, p), Engine.GetLegalCommands(s, p));

        // ---------- AI-01: only the PlayerView ----------

        [Test]
        public void AI01_BeliefKeepsGhostsWhereLastSeenAndOmitsHiddenUnits()
        {
            var tb = new TestBoard();
            int archer = tb.Unit(UnitClass.Archer, A, D(6, 6));      // sight 3
            int seen = tb.Unit(UnitClass.Mage, B, D(7, 3));          // distance 3: Visible to A
            int hidden = tb.Unit(UnitClass.Healer, B, D(2, 2));      // never seen by A
            var s = tb.Build();
            Engine.Apply(s, new MoveCommand(A, archer, D(5, 7)));    // distance 4: the Mage leaves A's sight
            Assert.That(s.GetFog(A).Get(D(7, 3)), Is.EqualTo(CellVisibility.Explored));
            s.GetUnit(seen).Pos = D(8, 2); // the real Mage moved away unseen

            var belief = BeliefState.From(PlayerView.For(s, A));
            var ghost = belief.GetUnit(seen);
            Assert.That(ghost, Is.Not.Null);
            Assert.That(ghost.Pos, Is.EqualTo(D(7, 3)), "a ghost is assumed where it was last seen");
            Assert.That(belief.GetUnit(hidden), Is.Null, "a hidden unit is assumed absent");
            Assert.That(belief.GetUnit(archer).Pos, Is.EqualTo(D(5, 7)));
            Assert.That(belief.GetHand(B), Is.Empty);
            Assert.That(belief.Traps.Where(t => t.Owner == B), Is.Empty);
        }

        [Test]
        public void AI01_BeliefReproducesTheLegalListAndAcceptsEveryLegalCommand()
        {
            int points = 0, applied = 0;
            foreach (ulong seed in new ulong[] { 11, 12, 13, 14 })
            {
                var rng = new Rng(seed * 31);
                var s = Match.Create(seed);
                for (int i = 0; i < 700 && !s.IsOver; i++)
                {
                    var p = s.ActivePlayer;
                    if (s.Phase == GamePhase.Playing && !s.IsDrawPending(p))
                    {
                        var legal = Engine.GetLegalCommands(s, p);
                        var belief = BeliefState.From(PlayerView.For(s, p));
                        Assert.That(Engine.GetLegalCommands(belief, p), Is.EqualTo(legal), "seed " + seed + " step " + i);
                        foreach (var c in legal.Where((c, k) => k % 7 == i % 7))
                        {
                            Assert.DoesNotThrow(() => Engine.Apply(belief.Clone(), c), "seed " + seed + ": " + c);
                            applied++;
                        }
                        points++;
                    }
                    Engine.Apply(s, NextUniform(s, rng));
                }
            }
            TestContext.Out.WriteLine("decision points " + points + ", belief applies " + applied);
            Assert.That(points, Is.GreaterThan(300));
        }

        /// <summary>The AI's choice is a function of (PlayerView, legal list, level, AI Rng): scrambling everything hidden
        /// from the deciding player (opponent hand, quests, passive, Mana/Energy, pre-pick, hidden units, tower, traps,
        /// pools, match Rng) never changes the command it picks.</summary>
        [Test]
        public void AI01_ChoiceIsTheSameWhenHiddenDataIsScrambled()
        {
            var scr = new Rng(2024);
            int compares = 0, setupCompares = 0, drawCompares = 0, actionCompares = 0;

            void Compare(GameState before, PlayerId p, AiLevel level, Rng rngBefore, ICommand chosen)
            {
                var scrambled = Scramble(before, p, scr);
                var legal2 = Engine.GetLegalCommands(scrambled, p);
                var c2 = GreedyAi.Choose(PlayerView.For(scrambled, p), legal2, level, rngBefore.Clone());
                Assert.That(c2, Is.EqualTo(chosen), "the AI's choice changed with hidden data scrambled, " + p + " round " + before.Round);
                compares++;
                if (before.Phase == GamePhase.Setup) setupCompares++;
                else if (chosen is DrawCommand || chosen is PrePickCommand) drawCompares++;
                else actionCompares++;
            }

            // AI vs AI: every decision.
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
                AiHelper.Play(seed, seed == 4 ? AiLevel.Easy : AiLevel.Normal, seed == 2 ? AiLevel.Easy : AiLevel.Normal,
                    d => Compare(d.Before, d.Player, d.Level, d.RngBefore, d.Chosen));
            // Random legal play, the AI asked at every 4th state for the active player (and the waiting player).
            foreach (ulong seed in new ulong[] { 21, 22 })
            {
                var rng = new Rng(seed * 17);
                var s = Match.Create(seed);
                for (int i = 0; i < 500 && !s.IsOver; i++)
                {
                    if (i % 4 == 0)
                    {
                        var deciders = s.Phase == GamePhase.Setup ? Players.Where(x => !s.IsSetupFinished(x)) : Players;
                        foreach (var p in deciders)
                        {
                            var legal = Engine.GetLegalCommands(s, p);
                            if (legal.Count == 0) continue;
                            var aiRng = new Rng((ulong)i);
                            var level = i % 8 == 0 ? AiLevel.Easy : AiLevel.Normal;
                            var chosen = GreedyAi.Choose(PlayerView.For(s, p), legal, level, aiRng.Clone());
                            Assert.That(legal, Does.Contain(chosen));
                            Compare(s, p, level, aiRng, chosen);
                        }
                    }
                    Engine.Apply(s, NextUniform(s, rng));
                }
            }
            TestContext.Out.WriteLine("compares " + compares + " (setup " + setupCompares + ", draw/pre-pick " + drawCompares
                + ", actions " + actionCompares + ")");
            Assert.That(compares, Is.GreaterThan(300));
            Assert.That(setupCompares, Is.GreaterThan(10));
            Assert.That(drawCompares, Is.GreaterThan(20));
            Assert.That(actionCompares, Is.GreaterThan(200));
        }

        // ---------- AI-02: greedy evaluation ----------

        [Test]
        public void AI02_TakesALethalHit()
        {
            var tb = new TestBoard();
            int rider = tb.Unit(UnitClass.Rider, A, D(6, 6));                 // Attack 3
            tb.Unit(UnitClass.Archer, B, D(5, 5), health: 3);                 // wounded to 3: lethal
            tb.Unit(UnitClass.Guardian, B, D(8, 6), health: 6);               // not lethal, not next to the Archer (no cover)
            var s = tb.Build();
            var c = Choose(s, A, AiLevel.Normal);
            // v3.2: the kill may come as a plain attack or as a move-attack; either way it is the lethal one.
            Assert.That(c is AttackCommand a && a.Target == D(5, 5) || c is MoveAttackCommand m && m.Target == D(5, 5), Is.True, c.ToString());
        }

        [Test]
        public void AI02_StepsOntoAnOpenPortal()
        {
            var tb = new TestBoard();
            tb.Quests(A, "Q-10", "Q-11", "Q-12").QuestStatus(A, "Q-10", QuestStatus.Completed).QuestStatus(A, "Q-11", QuestStatus.Completed);
            int guardian = tb.Unit(UnitClass.Guardian, A, D(5, 5));
            var s = tb.Build();
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new MoveCommand(A, guardian, Board.Portal)));
        }

        [Test]
        public void AI02_DefendsItsTowerWhenThreatened()
        {
            // Two equal lethal targets; one stands next to A's tower (-3,6).
            var tb = new TestBoard();
            int mage = tb.Unit(UnitClass.Mage, A, H(-2, 4));
            tb.Unit(UnitClass.Rider, B, H(-3, 5), health: 2);   // next to the tower
            tb.Unit(UnitClass.Rider, B, H(-1, 3), health: 2);   // away from it (distance 3)
            var s = tb.Build();
            Assert.That(Hex.Distance(H(-3, 5), TestBoard.DefaultTowerA), Is.EqualTo(1));
            Assert.That(HitTarget(Choose(s, A, AiLevel.Normal), mage), Is.EqualTo(H(-3, 5))); // plain or move-attack (v3.2)
        }

        [Test]
        public void AI02_PrefersTheBiomeBonusWhenOtherwiseEqual()
        {
            // A Guardian at (6,6) with exactly two mirror-image destinations; only (7,5) is its own biome (Desert).
            var tb = new TestBoard();
            int guardian = tb.Unit(UnitClass.Guardian, A, D(6, 6));
            foreach (var r in new[] { D(4, 6), D(8, 6), D(5, 7), D(7, 7) }) tb.Rock(r);
            tb.Biome(D(7, 5), Biome.Desert);
            var s = tb.Build();
            ExploreAll(s, A);
            var ranked = Rank(s, A);
            int Score(Hex h) => ranked.Single(c => c.Command.Equals(new MoveCommand(A, guardian, h))).Score;
            Assert.That(Score(D(7, 5)), Is.GreaterThan(Score(D(5, 5))));
            Assert.That(Choose(s, A, AiLevel.Normal), Is.Not.EqualTo(new MoveCommand(A, guardian, D(5, 5))));
        }

        [Test]
        public void AI02_EndsTheTurnWhenNothingBeatsIt()
        {
            var tb = new TestBoard();
            int guardian = tb.Unit(UnitClass.Guardian, A, D(6, 6));
            tb.Energy(A, 0); // no action possible
            var s = tb.Build();
            Assert.That(Engine.GetLegalCommands(s, A), Is.EqualTo(new ICommand[] { new EndTurnCommand(A) }));
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new EndTurnCommand(A)));
            Assert.That(guardian, Is.GreaterThan(0));
        }

        [Test]
        public void AI01_PlayerViewRefusesALightState()
        {
            var tb = new TestBoard();
            tb.Unit(UnitClass.Guardian, A, D(6, 6));
            var belief = BeliefState.From(PlayerView.For(tb.Build(), A));
            Assert.Throws<InvalidOperationException>(() => PlayerView.For(belief, A));
        }

        [Test]
        public void AI02_AssumesEnemyRidersCanCharge()
        {
            var tb = new TestBoard();
            tb.Unit(UnitClass.Guardian, A, D(6, 6));
            int rider = tb.Unit(UnitClass.Rider, B, D(7, 5));
            var s = tb.Build();
            var r = BeliefState.From(PlayerView.For(s, A)).GetUnit(rider);
            // U-02 (v3.2): the risk estimate assumes the Rider comes as a move-attack, i.e. with its Charge.
            int plain = Combat.AttackDamage(s, r, false);
            Assert.That(GreedyAi.ThreatDamage(r, plain), Is.EqualTo(plain + Catalog.RiderChargeBonus));
            Assert.That(GreedyAi.ThreatDamage(s.Units.First(u => u.Class == UnitClass.Guardian), 2), Is.EqualTo(2));
        }

        /// <summary>P-04 revealed and the enemy tower below full Health: the wall is used up (towers never heal), so the
        /// AI keeps attacking the tower. At full Health the belief takes a middle estimate.</summary>
        [Test]
        public void AI02_KeepsAttackingAThickWallTowerOnceTheWallIsUsedUp()
        {
            int Wall = Catalog.Passives.Single(p => p.Id == "P-04").Amount; // 5 (v3.1)
            var tb = new TestBoard();
            int archer = tb.Unit(UnitClass.Archer, A, H(3, -3)); // straight line, distance 3 from B's tower (3,-6): out of its range
            tb.Passive(B, "P-04");
            tb.Tower(B, TestBoard.DefaultTowerB, 9);
            var s = tb.Build();
            s.GetProgress(B).PassiveRevealed = true;
            s.GetProgress(B).WallBlocked = Wall;
            ExploreAll(s, A);
            Assert.That(BeliefState.From(PlayerView.For(s, A)).GetProgress(B).WallBlocked, Is.EqualTo(Wall));
            var attack = new AttackCommand(A, archer, TestBoard.DefaultTowerB);
            Assert.That(Engine.GetLegalCommands(s, A), Does.Contain(attack));
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(attack));

            s.GetTower(B).Health = Catalog.Tower.Health; // revealed by a smaller hit (real amount hidden): estimate
            s.GetProgress(B).WallBlocked = 2;
            Assert.That(BeliefState.From(PlayerView.For(s, A)).GetProgress(B).WallBlocked, Is.EqualTo(1));
        }

        [Test]
        public void AI02_BlocksTheOpenEnemyPortal()
        {
            var tb = new TestBoard();
            tb.Quests(B, "Q-10", "Q-11", "Q-12").QuestStatus(B, "Q-10", QuestStatus.Completed).QuestStatus(B, "Q-11", QuestStatus.Completed);
            int guardian = tb.Unit(UnitClass.Guardian, A, D(5, 5));
            var s = tb.Build();
            Assert.That(PlayerView.For(s, A).OpponentCompletedQuests.Count, Is.EqualTo(2));
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new MoveCommand(A, guardian, Board.Portal)));
        }

        [Test]
        public void AI02_AttacksAnEnemyWaitingOnTheOpenPortal()
        {
            var tb = new TestBoard();
            tb.Quests(B, "Q-10", "Q-11", "Q-12").QuestStatus(B, "Q-10", QuestStatus.Completed).QuestStatus(B, "Q-11", QuestStatus.Completed);
            int mage = tb.Unit(UnitClass.Mage, A, D(6, 6));      // range 2 to both Guardians, non-lethal hits
            tb.Unit(UnitClass.Guardian, B, Board.Portal);
            tb.Unit(UnitClass.Guardian, B, D(10, 6));
            var s = tb.Build();
            Assert.That(HitTarget(Choose(s, A, AiLevel.Normal), mage), Is.EqualTo(Board.Portal)); // plain or move-attack (v3.2)
        }

        /// <summary>Equal scores break the same way for both seats: the 180° mirror of a position (B to act) gets the mirror
        /// of A's ranking, command by command, with the same scores.</summary>
        [Test]
        public void AI02_MirroredPositionGivesTheMirroredChoice()
        {
            GameState Build(PlayerId p)
            {
                Hex M(int x, int y) => p == A ? D(x, y) : D(x, y).Mirror();
                Hex MA(int q, int r) => p == A ? H(q, r) : H(q, r).Mirror();
                var tb = new TestBoard();
                tb.Unit(UnitClass.Guardian, p, M(6, 6)); // two equal moves (5,5) and (7,5): a tie
                tb.Unit(UnitClass.Archer, p, MA(-5, 0));
                // In the Control Zone: a C-18 target. On the left corner (-6,0), pushes 2, 3, 4 (NW, W, SW) leave the board,
                // push 0 hits the Archer: they tie; their mirrors 5, 0, 1 do not keep that order, so the seat-rotated
                // direction order is really checked.
                tb.Unit(UnitClass.Healer, p.Opponent(), MA(-6, 0));
                foreach (var r in new[] { M(4, 6), M(8, 6), M(5, 7), M(7, 7) }) tb.Rock(r);
                tb.Hand(p, "U-05-Desert"); // deploy: many Control Zone cells, several with equal scores
                tb.Hand(p, "C-18");
                tb.Active(p).Mana(p, 3);
                var st = tb.Build();
                ExploreAll(st, p);
                return st;
            }
            ICommand Mirror(ICommand c)
            {
                switch (c)
                {
                    case MoveCommand m: return new MoveCommand(B, m.UnitId, m.Dest.Mirror());
                    case AttackCommand a: return new AttackCommand(B, a.UnitId, a.Target.Mirror());
                    case MoveAttackCommand ma: return new MoveAttackCommand(B, ma.UnitId, ma.Approach.Mirror(), ma.Target.Mirror());
                    case OverwatchCommand o: return new OverwatchCommand(B, o.UnitId);
                    case DeployCommand d: return new DeployCommand(B, d.CardId, d.Cell.Mirror());
                    case PlayCardCommand pc when pc.Direction >= 0: // Mirror negates a direction: index + 3 (Hex.Directions)
                        return new PlayCardCommand(B, pc.CardId, pc.Target.Mirror(), (pc.Direction + 3) % 6);
                    case EndTurnCommand _: return new EndTurnCommand(B);
                    default: throw new InvalidOperationException(c.ToString());
                }
            }
            var ra = Rank(Build(A), A);
            var rb = Rank(Build(B), B);
            Assert.That(ra.Count, Is.EqualTo(rb.Count));
            Assert.That(ra.Select(c => c.Score).Distinct().Count(), Is.LessThan(ra.Count), "the scenario has ties");
            bool Tied<T>() => ra.Where(c => c.Command is T).GroupBy(c => c.Score).Any(g => g.Count() > 1);
            Assert.That(Tied<DeployCommand>(), "deploys with equal scores");
            Assert.That(ra.Count(c => c.Command is PlayCardCommand pc && pc.Direction >= 0), Is.EqualTo(6), "a push in each direction");
            Assert.That(Tied<PlayCardCommand>(), "pushes with equal scores: "
                + string.Join(" ", ra.Where(c => c.Command is PlayCardCommand).Select(c => ((PlayCardCommand)c.Command).Direction + ":" + c.Score)));
            for (int i = 0; i < ra.Count; i++)
            {
                Assert.That(rb[i].Command, Is.EqualTo(Mirror(ra[i].Command)), "rank " + i);
                Assert.That(rb[i].Score, Is.EqualTo(ra[i].Score), "rank " + i);
            }
            Assert.That(Choose(Build(B), B, AiLevel.Normal), Is.EqualTo(Mirror(Choose(Build(A), A, AiLevel.Normal))));
        }

        // ---------- AI-03: Easy / Normal and the EndTurn guards ----------

        static GameState BiomeScenario(out int guardian)
        {
            var tb = new TestBoard();
            guardian = tb.Unit(UnitClass.Guardian, A, D(6, 6));
            foreach (var r in new[] { D(4, 6), D(8, 6), D(5, 7), D(7, 7) }) tb.Rock(r);
            tb.Biome(D(7, 5), Biome.Desert);
            var s = tb.Build();
            ExploreAll(s, A);
            return s;
        }

        [Test]
        public void AI03_NormalPicksTheBestAndEasyPicksAmongTheTopThree()
        {
            var s = BiomeScenario(out _);
            var ranked = Rank(s, A);
            Assert.That(ranked.Count, Is.EqualTo(4)); // two moves, overwatch, EndTurn
            var top = ranked.Take(Catalog.AiEasyTopMoves).Select(c => c.Command).ToList();
            var easyPicks = new HashSet<ICommand>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Assert.That(Choose(s, A, AiLevel.Normal, seed), Is.EqualTo(ranked[0].Command));
                var e = Choose(s, A, AiLevel.Easy, seed);
                Assert.That(top, Does.Contain(e));
                easyPicks.Add(e);
            }
            Assert.That(easyPicks.Count, Is.GreaterThan(1), "Easy never varied");
        }

        [Test]
        public void AI03_NeverEndsTheTurnWhileALethalAttackIsAvailable()
        {
            var tb = new TestBoard();
            int guardian = tb.Unit(UnitClass.Guardian, A, D(6, 6));           // Attack 2
            tb.Unit(UnitClass.Archer, B, D(5, 5), health: 2);
            foreach (var r in new[] { D(4, 6), D(8, 6), D(5, 7), D(7, 7), D(7, 5) }) tb.Rock(r);
            var s = tb.Build();
            var legal = Engine.GetLegalCommands(s, A);
            Assert.That(legal.Count, Is.EqualTo(3)); // attack, overwatch, EndTurn: EndTurn is in the top 3
            var ranked = Rank(s, A);
            Assert.That(ranked.Single(c => c.Command is AttackCommand).Lethal, Is.True);
            for (ulong seed = 1; seed <= 60; seed++)
                foreach (var level in new[] { AiLevel.Easy, AiLevel.Normal })
                    Assert.That(Choose(s, A, level, seed), Is.Not.InstanceOf<EndTurnCommand>(), level + " seed " + seed);
            Assert.That(guardian, Is.GreaterThan(0));
        }

        [Test]
        public void AI03_NeverEndsTheTurnWhileAnOpenPortalStepIsAvailable()
        {
            var tb = new TestBoard();
            tb.Quests(A, "Q-10", "Q-11", "Q-12").QuestStatus(A, "Q-10", QuestStatus.Completed).QuestStatus(A, "Q-11", QuestStatus.Completed);
            tb.Unit(UnitClass.Guardian, A, D(5, 5));
            foreach (var r in new[] { D(7, 5), D(4, 4), D(3, 5), D(4, 6), D(6, 6) }) tb.Rock(r);
            var s = tb.Build();
            Assert.That(Engine.GetLegalCommands(s, A).Count, Is.EqualTo(3)); // portal move, overwatch, EndTurn
            Assert.That(Rank(s, A).Single(c => c.Command is MoveCommand).PortalStep, Is.True);
            for (ulong seed = 1; seed <= 60; seed++)
                foreach (var level in new[] { AiLevel.Easy, AiLevel.Normal })
                    Assert.That(Choose(s, A, level, seed), Is.Not.InstanceOf<EndTurnCommand>(), level + " seed " + seed);
        }

        // ---------- AI-04: setup ----------

        [Test]
        public void AI04_SetupPutsTheTowerInTheBackRowAndTheGuardianNextToIt_Normal() => SetupChecks(AiLevel.Normal);

        [Test]
        public void AI04_SetupPutsTheTowerInTheBackRowAndTheGuardianNextToIt_Easy() => SetupChecks(AiLevel.Easy);

        /// <summary>AI-04 with PM S3: the pieces are a combination that fits the 8 setup Mana (≥ 1 character), the Guardian
        /// stands next to the tower when it is placed, a placed Spike Trap stands in front of the tower, and the class mix
        /// varies between games.</summary>
        static void SetupChecks(AiLevel level)
        {
            var mixes = new HashSet<string>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var s = Match.Create(seed);
                var dealt = Players.ToDictionary(p => p, p => s.GetHand(p).ToList());
                var rngs = Players.ToDictionary(p => p, p => AiHelper.AiRng(seed, p));
                for (int k = 0; k < 40 && s.Phase == GamePhase.Setup; k++)
                    foreach (var p in Players)
                        if (s.Phase == GamePhase.Setup && !s.IsSetupFinished(p))
                        {
                            var legal = Engine.GetLegalCommands(s, p);
                            var c = GreedyAi.Choose(PlayerView.For(s, p), legal, level, rngs[p]);
                            Assert.That(legal, Does.Contain(c));
                            Assert.That(c, Is.Not.InstanceOf<SetupTimeoutCommand>());
                            if (c is ChooseQuestsCommand q)
                            {
                                var view = PlayerView.For(s, p);
                                int Sum(IEnumerable<string> ids) => ids.Sum(id => AiSetup.QuestScore(view, id));
                                int best = legal.OfType<ChooseQuestsCommand>().Max(x => Sum(x.QuestIds));
                                Assert.That(Sum(q.QuestIds), Is.EqualTo(best), "quests by feasibility");
                            }
                            Engine.Apply(s, c);
                        }
                Assert.That(s.Phase, Is.EqualTo(GamePhase.Playing), "seed " + seed);
                foreach (var p in Players)
                {
                    var tower = s.GetTower(p).Pos;
                    Assert.That(tower.R, Is.EqualTo(p == A ? 6 : -6), "back row");
                    Assert.That(Math.Abs(2 * tower.Q + tower.R), Is.LessThanOrEqualTo(2), "back-row centre (3 cells)");
                    var own = s.Units.Where(u => u.Owner == p).ToList();
                    // S-05 (v3.0), PM S3: a combination within the 8 setup Mana, at least one character.
                    Assert.That(own.Count, Is.GreaterThanOrEqualTo(Catalog.MinPlacedCharacters));
                    Assert.That(own.Sum(u => u.Def.Cost) + s.Traps.Where(t => t.Owner == p).Sum(t => t.Card.Support.Cost),
                        Is.LessThanOrEqualTo(Catalog.SetupMana));
                    if (own.Any(u => u.Class == UnitClass.Guardian))
                        Assert.That(own.Any(u => u.Class == UnitClass.Guardian && Hex.Distance(u.Pos, tower) == 1), "Guardian next to the tower");
                    foreach (var trap in s.Traps.Where(t => t.Owner == p))
                    {
                        Assert.That(trap.Card.DefId, Is.EqualTo("C-20"));
                        Assert.That(Hex.Distance(trap.Pos, tower), Is.EqualTo(1));
                        Assert.That(Hex.Distance(trap.Pos, Board.Portal), Is.LessThan(Hex.Distance(tower, Board.Portal)), "in front of the tower");
                        Assert.That(s.UnitAt(trap.Pos), Is.Null);
                    }
                    mixes.Add(string.Join(",", own.Select(u => u.Class.ToString()).OrderBy(x => x)));
                    // Spread: at most one adjacent pair among the non-Guardian units.
                    var others = own.Where(u => u.Class != UnitClass.Guardian).ToList();
                    int pairs = 0;
                    for (int i = 0; i < others.Count; i++)
                        for (int j = i + 1; j < others.Count; j++)
                            if (Hex.Distance(others[i].Pos, others[j].Pos) == 1) pairs++;
                    Assert.That(pairs, Is.LessThanOrEqualTo(1), "units are spread: seed " + seed + " " + p + " tower " + Doubled(tower)
                        + " units " + string.Join(" ", own.Select(u => u.Class + Doubled(u.Pos)))
                        + " traps " + string.Join(" ", s.Traps.Where(t => t.Owner == p).Select(t => Doubled(t.Pos))));
                }
            }
            Assert.That(mixes.Count, Is.GreaterThan(1), level + ": the class mix never varied");
        }

        /// <summary>PM S3: every planned combination fits the budget and holds a character while none stands; the plan is
        /// deterministic for the same AI Rng.</summary>
        [Test]
        public void AI04_SetupPlanFitsTheBudget_DeterministicPerRng()
        {
            foreach (var level in new[] { AiLevel.Normal, AiLevel.Easy })
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    var s = Match.Create(seed);
                    foreach (var c in new ICommand[] {
                        Engine.GetLegalCommands(s, A).OfType<ChooseQuestsCommand>().First() })
                        Engine.Apply(s, c);
                    Engine.Apply(s, Engine.GetLegalCommands(s, A).OfType<ChoosePassiveCommand>().First());
                    Engine.Apply(s, Engine.GetLegalCommands(s, A).OfType<PlaceTowerCommand>().First());
                    var view = PlayerView.For(s, A);
                    var legal = Engine.GetLegalCommands(s, A);
                    var plan = AiSetup.Plan(view, legal, level, new Rng(seed));
                    Assert.That(plan.Any(c => c.IsCharacter), Is.True);
                    Assert.That(plan.Sum(c => c.IsCharacter ? Unit.DefOf(c.Character.Class).Cost : c.Support.Cost), Is.LessThanOrEqualTo(Catalog.SetupMana));
                    Assert.That(AiSetup.Plan(view, legal, level, new Rng(seed)).Select(c => c.Id), Is.EqualTo(plan.Select(c => c.Id)));
                }
        }

        // ---------- AI-06 / T-04: draws and pre-picks ----------

        [Test]
        public void AI06_PrePicksTheSameSlotItWouldDraw()
        {
            var tb = new TestBoard();
            tb.Unit(UnitClass.Guardian, A, D(6, 6));
            tb.Unit(UnitClass.Guardian, B, D(6, 2));
            tb.Market("U-02-Forest");
            tb.Market("C-14");
            for (int i = 0; i < 4; i++) tb.PoolCard("C-14");
            var s = tb.Build();
            var pre = Choose(s, B, AiLevel.Normal); // A is active: B pre-picks
            Assert.That(pre, Is.EqualTo(new PrePickCommand(B, (int)CardPool.Character)));

            s.SetDrawPending(A, true);
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new DrawCommand(A, (int)CardPool.Character)));
            Assert.That(Choose(s, A, AiLevel.Easy), Is.EqualTo(new DrawCommand(A, (int)CardPool.Character)));
        }

        [Test]
        public void AI06_BlindDrawWhenThePoolsAreWorthMoreThanTheMarket()
        {
            var tb = new TestBoard();
            tb.Unit(UnitClass.Guardian, A, D(6, 6));
            tb.Market("C-14");
            for (int i = 0; i < 6; i++) tb.PoolCard("U-02-Snow");
            var s = tb.Build();
            s.SetDrawPending(A, true);
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new DrawCommand(A, DrawCommand.Blind)));
            Assert.That(Choose(s, B, AiLevel.Normal), Is.EqualTo(new PrePickCommand(B, DrawCommand.Blind)));
        }

        // ---------- AI-01…AI-06: whole matches ----------

        /// <summary>50 AI-vs-AI matches (25 Normal vs Normal, 25 Easy vs Normal with Easy on alternating seats): no
        /// exception, only legal commands (never a clock command), each ends with a W-* result; a replay of the same seed
        /// gives the same final state.</summary>
        [Test]
        public void AI_AiVsAi50MatchesFinishWithAResult()
        {
            var results = new SortedDictionary<string, int>();
            int winsA = 0, winsB = 0, draws = 0, rounds = 0, commands = 0, easyWins = 0, easyGames = 0;
            var kinds = new HashSet<Type>();
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var la = AiLevel.Normal;
                var lb = AiLevel.Normal;
                bool easy = seed > 25;
                PlayerId? easySeat = null;
                if (easy)
                {
                    easySeat = seed % 2 == 0 ? A : B;
                    if (easySeat == A) la = AiLevel.Easy;
                    else lb = AiLevel.Easy;
                    easyGames++;
                }
                var log = new List<ICommand>();
                var s = AiHelper.Play(seed, la, lb, null, log);
                foreach (var c in log) kinds.Add(c.GetType());
                commands += log.Count;
                var r = s.Result;
                Assert.That(r, Is.Not.Null);
                string key = AiHelper.ResultKey(r);
                results[key] = results.TryGetValue(key, out int n) ? n + 1 : 1;
                if (r.Winner == A) winsA++;
                else if (r.Winner == B) winsB++;
                else draws++;
                if (easySeat.HasValue && r.Winner == easySeat) easyWins++;
                rounds += s.Round;
                Assert.That(log.Any(c => c is TurnTimeoutCommand || c is SetupTimeoutCommand), Is.False, "AI never uses clock commands");
                if (seed % 10 == 0)
                {
                    var again = AiHelper.Play(seed, la, lb);
                    Assert.That(StateHash.Compute(again), Is.EqualTo(StateHash.Compute(s)), "deterministic per seed " + seed);
                }
            }
            TestContext.Out.WriteLine("results: " + string.Join(", ", results.Select(kv => kv.Key + " = " + kv.Value)));
            TestContext.Out.WriteLine("A wins " + winsA + ", B wins " + winsB + ", draws " + draws + ", average final round "
                + (rounds / 50.0).ToString("0.0") + ", commands " + commands + ", Easy won " + easyWins + "/" + easyGames);
            TestContext.Out.WriteLine("command kinds: " + string.Join(",", kinds.Select(t => t.Name).OrderBy(x => x)));
            Assert.That(results.Values.Sum(), Is.EqualTo(50));
            foreach (var k in new[] { typeof(MoveCommand), typeof(AttackCommand), typeof(DeployCommand), typeof(PlayCardCommand),
                         typeof(DrawCommand), typeof(PrePickCommand), typeof(EndTurnCommand), typeof(PlaceTowerCommand) })
                Assert.That(kinds, Does.Contain(k), k.Name + " never chosen");
        }
    }
}
