using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // GDD v4.0 §2.1 terrain: B-14 Mountain (was Rock), B-15 River, B-16 Lava; C-18 push collision damage; E-10…E-12 on the
    // new terrain; B-25 templates with River (TwoCorridors) and Lava (RockyRing).
    public class TerrainTests
    {
        static readonly PlayerId A = PlayerId.A, B = PlayerId.B;
        static Hex H(int q, int r) => new Hex(q, r);
        static int Hp(UnitClass c) => Unit.DefOf(c).Health;
        static System.Collections.Generic.List<GameEvent> End(GameState s) => Engine.Apply(s, new EndTurnCommand(s.ActivePlayer));

        // ---------- B-14 ----------

        [Test]
        public void B14_MountainIsTheOldRockAndBlocksMovement()
        {
            Assert.That(Marker.Rock, Is.EqualTo(Marker.Mountain)); // compatibility alias for the client
            var b = new TestBoard().Mountain(H(1, 0));
            int g = b.Unit(UnitClass.Guardian, A, Board.Portal);
            var s = b.Build();
            Assert.That(Movement.Destinations(s, s.GetUnit(g)), Does.Not.Contain(H(1, 0)));
            Assert.That(s.Map.IsPassable(H(1, 0)), Is.False);
        }

        // ---------- B-15 ----------

        [Test]
        public void B15_RiverCannotBeEnteredOrCrossedByGroundUnits()
        {
            // A River line across q = 1 for r = -1..1 next to the Portal; a Rider (passes units) still cannot cross it.
            var b = new TestBoard();
            foreach (var h in new[] { H(1, -1), H(1, 0), H(1, 1) }) b.River(h);
            int g = b.Unit(UnitClass.Guardian, A, Board.Portal);
            int r = b.Unit(UnitClass.Rider, A, H(0, 1));
            var s = b.Build();
            Assert.That(Movement.Destinations(s, s.GetUnit(g)), Does.Not.Contain(H(1, 0)));
            var riderDests = Movement.Destinations(s, s.GetUnit(r));
            Assert.That(riderDests, Does.Not.Contain(H(1, 1)));
            Assert.That(riderDests, Does.Not.Contain(H(2, 0)), "the straight crossing (0,1)->(1,0)/(1,1)->(2,0) is blocked");
        }

        [Test]
        public void B15_RiverBlocksNeitherSightNorArrows()
        {
            var b = new TestBoard().River(H(1, 0)).River(H(2, 0));
            int archer = b.Unit(UnitClass.Archer, A, Board.Portal);
            b.Unit(UnitClass.Guardian, B, H(3, 0));
            var s = b.Build();
            Assert.That(Visibility.VisibleCells(s, A), Does.Contain(H(3, 0)));
            Assert.That(Combat.LegalTargets(s, s.GetUnit(archer)), Does.Contain(H(3, 0)));
        }

        [Test]
        public void B15_C18_RiverStopsAPushAndDealsCollisionDamage()
        {
            var b = new TestBoard().River(H(3, 0)).Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(2, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(Hp(UnitClass.Guardian) - Catalog.PushCollisionDamage));
        }

        // ---------- B-16 ----------

        [Test]
        public void B16_EnteringLavaDealsLavaDamage()
        {
            var b = new TestBoard().Lava(H(1, 0));
            int g = b.Unit(UnitClass.Guardian, A, Board.Portal);
            var s = b.Build();
            Assert.That(Movement.Destinations(s, s.GetUnit(g)), Does.Contain(H(1, 0)), "Lava is passable");
            var ev = Engine.Apply(s, new MoveCommand(A, g, H(1, 0)));
            var d = ev.OfType<DamageDealt>().Single();
            Assert.That((d.TargetUnitId, d.Amount, d.Kind), Is.EqualTo((g, Catalog.LavaDamage, DamageKind.Lava)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(Hp(UnitClass.Guardian) - Catalog.LavaDamage));
        }

        [Test]
        public void B16_EndingTheOwnTurnOnLavaDealsLavaDamage_NotTheOpponentsTurnEnd()
        {
            var b = new TestBoard().Lava(H(2, 2)).Round(3);
            int a = b.Unit(UnitClass.Guardian, A, H(2, 2));
            int bb = b.Unit(UnitClass.Guardian, B, H(-2, -2));
            var s = b.Build();
            var ev = End(s); // A's turn end: A's unit burns
            Assert.That(ev.OfType<DamageDealt>().Where(x => x.Kind == DamageKind.Lava).Select(x => x.TargetUnitId), Is.EqualTo(new[] { a }));
            int afterA = s.GetUnit(a).Health;
            ev = End(s); // B's turn end: nothing on Lava for B; A's unit is not burned again
            Assert.That(ev.OfType<DamageDealt>().Any(x => x.Kind == DamageKind.Lava && x.TargetUnitId == a), Is.False);
        }

        [Test]
        public void B16_PassingThroughLavaDoesNotDamage()
        {
            // Interim (GDD silent on paths): only the stop cell counts, like traps (C-32).
            var b = new TestBoard().Lava(H(1, 0));
            int r = b.Unit(UnitClass.Rider, A, Board.Portal);
            var s = b.Build();
            var ev = Engine.Apply(s, new MoveCommand(A, r, H(2, 0)));
            Assert.That(ev.OfType<DamageDealt>(), Is.Empty);
        }

        [Test]
        public void B16_C18_PushedOntoLavaTakesLavaDamage()
        {
            var b = new TestBoard().Lava(H(3, 0)).Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(3, 0)));
            Assert.That(ev.OfType<DamageDealt>().Select(x => x.Kind), Is.EqualTo(new[] { DamageKind.Lava }));
        }

        // ---------- C-18 (v4.0) ----------

        [Test]
        public void C18_PushIntoAnObstacleDealsCollisionDamage()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Unit(UnitClass.Healer, A, H(3, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(2, 0)));
            var d = ev.OfType<DamageDealt>().Single();
            Assert.That((d.SourcePlayer, d.TargetUnitId, d.Amount, d.Kind), Is.EqualTo((A, g, Catalog.PushCollisionDamage, DamageKind.Collision)));
        }

        [Test]
        public void C18_AZeroCellPushIntoAnObstacleAlsoDealsCollisionDamage()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Mountain(H(2, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(s.GetUnit(g).Pos, Is.EqualTo(H(1, 0)));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(Hp(UnitClass.Guardian) - Catalog.PushCollisionDamage));
        }

        [Test]
        public void C18_TheBoardEdgeIsAnObstacle_AFullPushIsNot()
        {
            var b = new TestBoard().Mana(A, 2);
            b.Unit(UnitClass.Archer, A, H(5, 1));
            int edge = b.Unit(UnitClass.Guardian, B, H(6, 0));
            int p1 = b.Hand(A, "C-18");
            var s = b.Build();
            Engine.Apply(s, new PlayCardCommand(A, p1, H(6, 0), 0)); // (7,0) then off the board
            Assert.That(s.GetUnit(edge).Pos, Is.EqualTo(H(7, 0)));
            Assert.That(s.GetUnit(edge).Health, Is.EqualTo(Hp(UnitClass.Guardian) - Catalog.PushCollisionDamage));
        }

        [Test]
        public void C18_U20_AShieldBlocksTheCollisionDamage()
        {
            var b = new TestBoard().Mana(A, 1);
            b.Unit(UnitClass.Archer, A, H(0, 1));
            int g = b.Unit(UnitClass.Guardian, B, H(1, 0));
            b.Effect(g, "C-12");
            b.Mountain(H(2, 0));
            int push = b.Hand(A, "C-18");
            var s = b.Build();
            var ev = Engine.Apply(s, new PlayCardCommand(A, push, H(1, 0), 0));
            Assert.That(ev.OfType<ShieldBlocked>().Single().Kind, Is.EqualTo(DamageKind.Collision));
            Assert.That(s.GetUnit(g).Health, Is.EqualTo(Hp(UnitClass.Guardian)));
        }

        // ---------- E-10…E-12 on the new terrain ----------

        [Test]
        public void E10_E11_E12_NeverTargetRiverOrLava()
        {
            var b = new TestBoard();
            foreach (var h in Board.Cells.Where(h => Board.IsInUpperHalf(h) && Hex.Distance(h, Board.Portal) >= 3 && !Board.IsHomeZone(h, B)))
            {
                if ((h.Q + h.R) % 2 == 0) { b.River(h); b.River(h.Mirror()); }
                else { b.Lava(h); b.Lava(h.Mirror()); }
            }
            var s = b.Build();
            foreach (var def in Catalog.MapEvents)
                foreach (var h in MapEvents.Candidates(s, def))
                {
                    var m = s.Map.Get(h).Marker;
                    Assert.That(m, Is.Not.EqualTo(Marker.River).And.Not.EqualTo(Marker.Lava), def.Id + " " + h);
                    if (def.Id == "E-11")
                        foreach (var c in MapEvents.ClusterCells(s, h))
                            Assert.That(s.Map.Get(c).Marker, Is.Not.EqualTo(Marker.River).And.Not.EqualTo(Marker.Lava), def.Id + " cluster " + c);
                }
        }

        // ---------- B-25 templates ----------

        [Test]
        public void B25_TwoCorridorsHasARiverAndRockyRingHasLava_Mirrored()
        {
            var rocky = Catalog.MapTemplates.Single(t => t.Name == "RockyRing");
            var corridors = Catalog.MapTemplates.Single(t => t.Name == "TwoCorridors");
            Assert.That(rocky.UpperLavas, Is.Not.Empty);
            Assert.That(corridors.UpperRivers, Is.Not.Empty);
            foreach (var t in Catalog.MapTemplates)
                foreach (var h in t.UpperRivers.Concat(t.UpperLavas))
                {
                    Assert.That(Board.IsInUpperHalf(h), Is.True, t.Name + " " + h);
                    Assert.That(Board.IsHomeZone(h, B), Is.False, t.Name + " " + h);
                    Assert.That(Board.GddRow(h), Is.Not.InRange(Catalog.SpecialTileRowMin, Catalog.SpecialTileRowMax), t.Name + " " + h);
                    Assert.That(t.UpperRocks.Concat(t.UpperVoids), Does.Not.Contain(h));
                }
            var map = MapGenerator.Generate(1); // seed 1 -> template 1 RockyRing
            foreach (var h in rocky.UpperLavas)
            {
                Assert.That(map.Get(h), Is.EqualTo(new Tile(Biome.None, Marker.Lava)));
                Assert.That(map.Get(h.Mirror()), Is.EqualTo(new Tile(Biome.None, Marker.Lava)));
            }
            map = MapGenerator.Generate(3); // template 3 TwoCorridors
            foreach (var h in corridors.UpperRivers)
                Assert.That(map.Get(h.Mirror()), Is.EqualTo(new Tile(Biome.None, Marker.River)));
        }

        [Test]
        public void B25_EveryTemplateStaysValidOverSeeds1To1000()
        {
            // Per template: first-attempt validity, Portal reachable from every home cell and a connected home zone (both in
            // MapValidator), and the minimum number of event targets at the start.
            int n = Catalog.MapTemplates.Count;
            var first = new int[n];
            var total = new int[n];
            var minTargets = new int[n, 3];
            for (int t = 0; t < n; t++) for (int e = 0; e < 3; e++) minTargets[t, e] = int.MaxValue;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var map = MapGenerator.Generate(seed);
                int t = map.TemplateId;
                total[t]++;
                if (map.Attempts == 1) first[t]++;
                Assert.That(MapValidator.Validate(map), Is.Empty, "seed " + seed);
                var s = new GameState(map, TestBoard.DefaultTowerA, TestBoard.DefaultTowerB, seed);
                for (int e = 0; e < 3; e++)
                    minTargets[t, e] = System.Math.Min(minTargets[t, e], MapEvents.Candidates(s, Catalog.MapEvents[e]).Count);
            }
            for (int t = 0; t < n; t++)
            {
                TestContext.WriteLine(Catalog.MapTemplates[t].Name + ": first attempt " + first[t] + "/" + total[t]
                    + ", min targets E-10 " + minTargets[t, 0] + " E-11 " + minTargets[t, 1] + " E-12 " + minTargets[t, 2]);
                Assert.That(first[t] * 100, Is.GreaterThanOrEqualTo(total[t] * 90), Catalog.MapTemplates[t].Name);
                for (int e = 0; e < 3; e++) Assert.That(minTargets[t, e], Is.GreaterThan(0));
            }
        }
    }
}
