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
        static Hex D(int x, int y) => Hex.FromDoubled(x, y);

        static string Doubled(Hex h)
        {
            h.ToDoubled(out int x, out int y);
            return "(" + x + "," + y + ")";
        }

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
            tb.Unit(UnitClass.Archer, B, D(5, 5));                            // Health 3: lethal
            tb.Unit(UnitClass.Guardian, B, D(8, 6));                          // Health 6, not next to the Archer (no cover)
            var s = tb.Build();
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new AttackCommand(A, rider, D(5, 5))));
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
            // Two equal lethal targets; one stands next to A's tower (6,8).
            var tb = new TestBoard();
            int mage = tb.Unit(UnitClass.Mage, A, D(6, 6));
            tb.Unit(UnitClass.Rider, B, D(5, 7), health: 2);   // next to the tower
            tb.Unit(UnitClass.Rider, B, D(7, 5), health: 2);   // away from it
            var s = tb.Build();
            Assert.That(Choose(s, A, AiLevel.Normal), Is.EqualTo(new AttackCommand(A, mage, D(5, 7))));
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
        public void AI04_SetupPutsTheTowerInTheBackRowAndTheGuardianNextToIt()
        {
            int spikeChecks = 0;
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
                            var c = GreedyAi.Choose(PlayerView.For(s, p), legal, AiLevel.Normal, rngs[p]);
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
                    tower.ToDoubled(out int tx, out int ty);
                    Assert.That(ty, Is.EqualTo(p == A ? 8 : 0), "back row");
                    Assert.That(Math.Abs(tx - 6), Is.LessThanOrEqualTo(2));
                    var own = s.Units.Where(u => u.Owner == p).ToList();
                    Assert.That(own.Count, Is.EqualTo(Catalog.Units.Count), "every dealt character is placed");
                    Assert.That(own.Any(u => u.Class == UnitClass.Guardian && Hex.Distance(u.Pos, tower) == 1), "Guardian next to the tower");
                    if (dealt[p].Any(c => c.DefId == "C-20"))
                    {
                        var trap = s.Traps.Single(t => t.Owner == p);
                        Assert.That(trap.Card.DefId, Is.EqualTo("C-20"));
                        Assert.That(Hex.Distance(trap.Pos, tower), Is.EqualTo(1));
                        Assert.That(Hex.Distance(trap.Pos, Board.Portal), Is.LessThan(Hex.Distance(tower, Board.Portal)), "in front of the tower");
                        Assert.That(s.UnitAt(trap.Pos), Is.Null);
                        spikeChecks++;
                    }
                    else Assert.That(s.Traps.Where(t => t.Owner == p), Is.Empty);
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
            Assert.That(spikeChecks, Is.GreaterThan(0), "no seed dealt a Spike Trap");
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
