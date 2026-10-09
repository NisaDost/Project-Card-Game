using System;
using System.Linq;
using HexPortal.Core;
using HexPortal.Sim;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // Tools/Sim (M5b): the report is a deterministic function of the arguments (timing excluded), serial or parallel.
    public class SimTests
    {
        [Test]
        public void Sim_SeatSwapReportIsDeterministicSerialOrParallel()
        {
            var serial = new SimOptions { Games = 3, Seed = 5, LevelA = AiLevel.Easy, LevelB = AiLevel.Normal, Swap = true, Timing = false };
            var parallel = new SimOptions { Games = 3, Seed = 5, LevelA = AiLevel.Easy, LevelB = AiLevel.Normal, Swap = true, Timing = false, Parallel = true };
            var g1 = SimReport.Run(serial);
            var g2 = SimReport.Run(parallel);
            Assert.That(g1.Count, Is.EqualTo(6), "every seed twice");
            for (int i = 0; i < g1.Count; i += 2)
            {
                Assert.That(g1[i].Seed, Is.EqualTo(g1[i + 1].Seed));
                Assert.That(g1[i].Seats[0].Config, Is.EqualTo("a"));
                Assert.That(g1[i + 1].Seats[0].Config, Is.EqualTo("b"), "configurations swap seats");
                Assert.That(g1[i].ResultKey, Is.Not.Null);
            }
            serial.Parallel = true; // the options text is part of the report
            string j1 = SimReport.Json(SimReport.Build(serial, g1, false, 0));
            string j2 = SimReport.Json(SimReport.Build(parallel, g2, false, 0));
            Assert.That(j2, Is.EqualTo(j1));
            Assert.That(j1, Does.Contain("seat swap"));
            Assert.That(j1, Does.Contain("portal open round"));
            Assert.That(j1, Does.Contain("quest completion rates"));
            foreach (var g in g1)
                for (int seat = 0; seat < 2; seat++)
                {
                    var st = g.Seats[seat];
                    Assert.That(st.PortalOpenRound > 0, Is.EqualTo(st.QuestsCompleted.Count >= Core.Data.Catalog.PortalQuestsRequired));
                    if (g.ResultKey == "W-01" && g.Winner == (seat == 0 ? "A" : "B"))
                        Assert.That(st.PortalOpenRound, Is.InRange(1, g.Rounds - 1), "a Portal win needs the Portal open a round before");
                    // Every turn start draws (explicitly; T-11 pre-pick removed in v4.0) unless the hand is full or there is nothing to draw.
                    Assert.That(st.Draws.Keys.All(k => k.StartsWith("explicit ")));
                }
            Assert.That(g1.Sum(g => g.Seats.Sum(x => x.Draws.Where(kv => kv.Key.StartsWith("explicit ")).Sum(kv => kv.Value))),
                Is.GreaterThan(0), "turn-start draws are counted");
            Assert.That(j1, Does.Not.Contain("timing"));
            // v3b report additions: CI, round-limit share, W-01 share, kills, hits to kill per class.
            foreach (var key in new[] { "A win rate 95% CI", "reached round limit share", "W-01 share", "units killed per game", "hits to kill" })
                Assert.That(j1, Does.Contain(key), key);
            foreach (var g in g1)
            {
                Assert.That(g.UnitsKilled, Is.EqualTo(g.HitsToKill.Values.Sum(l => l.Count)));
                Assert.That(g.HitsToKill.Values.SelectMany(l => l).All(h => h >= 1), Is.True);
            }
        }

        [Test]
        public void Sim_RejectsSeedOverflowTooManyGamesAndFirstWithSwap()
        {
            Assert.Throws<ArgumentException>(() => new SimOptions { Seed = SimOptions.MaxSeed + 1 }.Validate());
            Assert.Throws<ArgumentException>(() => new SimOptions { Games = SimOptions.MaxGames + 1 }.Validate());
            Assert.Throws<ArgumentException>(() => new SimOptions { Games = 0 }.Validate());
            Assert.DoesNotThrow(() => new SimOptions { Seed = SimOptions.MaxSeed, Games = SimOptions.MaxGames }.Validate());
            var last = new SimOptions { Seed = SimOptions.MaxSeed, Games = SimOptions.MaxGames }.SeedFor(SimOptions.MaxGames - 1);
            Assert.That(last, Is.GreaterThan(SimOptions.MaxSeed * SimOptions.SeedStride), "no overflow");
            Assert.Throws<ArgumentException>(() => new SimOptions { FirstSet = true, FirstIsA = false, Swap = true }.Validate());
        }
    }
}
