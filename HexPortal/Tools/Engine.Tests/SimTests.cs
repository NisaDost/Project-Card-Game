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
            Assert.That(j1, Does.Not.Contain("timing"));
        }
    }
}
