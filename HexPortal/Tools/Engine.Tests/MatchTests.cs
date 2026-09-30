using System;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // Whole-match checks: fog leak tests (V-05, V-10, V-11, S-08), replay determinism, illegal commands,
    // GameState.Clone and StateHash coverage of the M4 state.
    public class MatchTests
    {
        static readonly ulong[] LeakSeeds = { 101, 202, 303 };
        const int MaxCommands = 700;
        const int MaxRounds = 30;

        /// <summary>Plays random legal matches (setup + up to MaxRounds rounds) and calls check(before, command, after, events).</summary>
        static void RandomMatches(Action<GameState, ICommand, GameState, List<GameEvent>> check)
        {
            foreach (var seed in LeakSeeds)
            {
                var rng = new Rng(seed * 7919);
                var s = Match.Create(seed);
                for (int i = 0; i < MaxCommands && !s.IsOver && s.Round <= MaxRounds; i++)
                {
                    var c = NextRandom(s, rng);
                    var before = s.Clone();
                    var ev = Engine.Apply(s, c);
                    check(before, c, s, ev);
                }
            }
        }

        // ---------- Leak test A: field scan ----------

        [Test]
        public void Leak_A_ViewsAndFilteredEventsHoldNoForbiddenItems()
        {
            int checkedEvents = 0, commands = 0, maxRound = 0, ghosts = 0;
            var kinds = new HashSet<Type>();
            var seenKinds = new HashSet<Type>();
            RandomMatches((before, c, after, ev) =>
            {
                commands++;
                kinds.Add(c.GetType());
                maxRound = Math.Max(maxRound, after.Round);
                foreach (var p in Players)
                {
                    ScanView(after, p);
                    ghosts += PlayerView.For(after, p).Cells.Count(x => x.Unit != null && x.Unit.IsGhost);
                    var filtered = EventFilter.For(ev, p);
                    checkedEvents += ScanEvents(before, after, ev, filtered, p, c);
                    foreach (var e in filtered)
                        if (e != ev.FirstOrDefault(x => x.ViewFor(p) == e) || e is Revealed) seenKinds.Add(e.GetType()); // redacted or reveal
                }
            });
            TestContext.Out.WriteLine("commands " + commands + ", max round " + maxRound + ", ghosts " + ghosts
                + ", redacted/reveal kinds: " + string.Join(",", seenKinds.Select(t => t.Name)));
            Assert.That(commands, Is.GreaterThan(500));
            Assert.That(checkedEvents, Is.GreaterThan(500));
            Assert.That(maxRound, Is.GreaterThanOrEqualTo(8));
            Assert.That(ghosts, Is.GreaterThan(0));
            foreach (var k in new[] { typeof(UnitAppeared), typeof(UnitVanished), typeof(Revealed), typeof(CardDrawn) })
                Assert.That(seenKinds, Does.Contain(k), k.Name + " never produced");
            foreach (var k in new[] { typeof(ChooseQuestsCommand), typeof(PlaceUnitCommand), typeof(FinishSetupCommand),
                         typeof(SetupTimeoutCommand), typeof(MoveCommand), typeof(AttackCommand), typeof(PlayCardCommand), typeof(DeployCommand),
                         typeof(EndTurnCommand), typeof(TurnTimeoutCommand), typeof(PrePickCommand), typeof(DrawCommand) })
                Assert.That(kinds, Does.Contain(k), k.Name + " never played");
        }

        static void ScanView(GameState s, PlayerId p)
        {
            var o = p.Opponent();
            var v = PlayerView.For(s, p);
            var visible = Visibility.VisibleCells(s, p);
            bool setup = s.Phase == GamePhase.Setup;
            var enemyHand = new HashSet<int>(s.GetHand(o).Select(c => c.Id));
            var enemyTraps = new HashSet<int>(s.Traps.Where(t => t.Owner == o).Select(t => t.Card.Id));
            var poolIds = new HashSet<int>(new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }.SelectMany(x => s.GetPool(x)).Select(c => c.Id));

            // Cards: only own hand, own traps and the public Market.
            var shown = v.Hand.Select(c => c.Id).Concat(v.OwnTraps.Select(t => t.CardId)).Concat(v.Market.Where(c => c != null).Select(c => c.Id)).ToList();
            Assert.That(shown.Where(id => enemyHand.Contains(id) || enemyTraps.Contains(id) || poolIds.Contains(id)), Is.Empty);
            Assert.That(v.Hand.Select(c => c.Id), Is.EqualTo(s.GetHand(p).Select(c => c.Id)));
            Assert.That(v.OwnTraps.All(t => s.Traps.Any(x => x.Owner == p && x.Card.Id == t.CardId)), Is.True);
            Assert.That(v.PrePickSlot, Is.EqualTo(s.GetPrePickSlot(p)));

            // Quests/passives: own only (S-03, S-04).
            Assert.That(v.QuestOffer, Is.EqualTo(s.GetQuestOffer(p).Select(q => q.Id)));
            Assert.That(v.PassiveChoice, Is.EqualTo(s.GetPassiveChoice(p) == null ? null : s.GetPassiveChoice(p).Id));

            // Units: live only on Visible cells; nothing of the opponent during setup (S-08).
            foreach (var u in v.EnemyUnits)
            {
                Assert.That(setup, Is.False);
                Assert.That(visible.Contains(u.Pos), Is.True, "enemy unit shown off sight: " + u.Id);
                var real = s.GetUnit(u.Id);
                Assert.That((real.Owner, real.Pos, real.Health), Is.EqualTo((o, u.Pos, u.Health)));
                Assert.That((u.ActedThisTurn, u.MovedThisTurn, u.MovedLastOwnTurn, u.MoveBonus), Is.EqualTo((false, false, false, 0)));
            }
            Assert.That(v.EnemyUnits.Count, Is.EqualTo(setup ? 0 : s.Units.Count(u => u.Owner == o && visible.Contains(u.Pos))));
            Assert.That(v.OwnUnits.Select(u => u.Id), Is.EqualTo(s.Units.Where(u => u.Owner == p).Select(u => u.Id)));
            var tower = s.GetTower(o);
            if (v.EnemyTowerPos.HasValue) Assert.That(!setup && visible.Contains(tower.Pos) && v.EnemyTowerPos.Value == tower.Pos, Is.True);
            else Assert.That(setup || !tower.IsPlaced || !visible.Contains(tower.Pos), Is.True);
            Assert.That(v.EnemyTowerHealth, Is.EqualTo(tower.Health));
            // S-08 (PM decision): during setup the opponent's hand count stays the dealt count; afterwards the live one.
            Assert.That(v.OpponentHandCount, Is.EqualTo(setup ? DealtHandSize : s.GetHand(o).Count));

            var fog = s.GetFog(p);
            foreach (var c in v.Cells)
            {
                Assert.That(c.Visibility, Is.EqualTo(fog.Get(c.Cell)));
                switch (c.Visibility)
                {
                    case CellVisibility.Hidden:
                        Assert.That(c.TerrainKnown || c.Unit != null || c.HasTower, Is.False, c.Cell.ToString());
                        break;
                    case CellVisibility.Explored:
                        var seen = fog.GetLastSeen(c.Cell);
                        if (c.Unit != null && c.Unit.Owner == p) // own units are always live (only on Explored cells during setup)
                            Assert.That(setup && !c.Unit.IsGhost && s.UnitAt(c.Cell).Id == c.Unit.Id, Is.True, c.Cell.ToString());
                        else if (c.Unit != null)
                        {
                            Assert.That(c.Unit.IsGhost && c.Unit.Id == seen.UnitId && c.Unit.Owner == o, Is.True, c.Cell.ToString());
                            Assert.That((c.Unit.BuffId, c.Unit.DebuffId, c.Unit.OnOverwatch), Is.EqualTo(((string)null, (string)null, false)));
                        }
                        break;
                    default:
                        if (!setup) Assert.That(visible.Contains(c.Cell), Is.True);
                        var live = s.UnitAt(c.Cell);
                        if (c.Unit != null) Assert.That(live != null && live.Id == c.Unit.Id && !c.Unit.IsGhost, Is.True);
                        else if (!setup) Assert.That(live, Is.Null);
                        break;
                }
            }
        }

        /// <summary>Card secrets never appear; every enemy unit id mentioned was Visible to p before or after the
        /// command, or was introduced earlier in the same filtered list (UnitAppeared, Revealed, UnitDeployed).
        /// Every cell that locates an enemy unit or the enemy tower is Visible to p before or after the command, or is
        /// the cell of a V-08 reveal (checked separately: the revealed piece really attacks in this list). The only
        /// exceptions are TrapTriggered.Cell (C-33) and the stop cell of a push (V-11 physical collision).</summary>
        static int ScanEvents(GameState before, GameState after, List<GameEvent> full, List<GameEvent> events, PlayerId p, ICommand c)
        {
            var o = p.Opponent();
            var owner = new Dictionary<int, PlayerId>();
            foreach (var u in before.Units.Concat(after.Units)) owner[u.Id] = u.Owner;
            var known = new HashSet<int>();
            foreach (var st in new[] { before, after })
            {
                if (st.Phase == GamePhase.Setup) continue;
                var vis = Visibility.VisibleCells(st, p);
                foreach (var u in st.Units) if (u.Owner == o && vis.Contains(u.Pos)) known.Add(u.Id);
            }
            var visBefore = before.Phase == GamePhase.Setup ? new HashSet<Hex>() : Visibility.VisibleCells(before, p);
            var visAfter = after.Phase == GamePhase.Setup ? new HashSet<Hex>() : Visibility.VisibleCells(after, p);
            var revealedCells = new HashSet<Hex>();
            bool Enemy(int id) => !owner.TryGetValue(id, out var ow) || ow == o;
            void Seen(Hex h, GameEvent ev) =>
                Assert.That(visBefore.Contains(h) || visAfter.Contains(h) || revealedCells.Contains(h), Is.True,
                    "hidden cell " + h + " in " + Serialize(ev) + " after " + c);
            bool setup = before.Phase == GamePhase.Setup;
            var enemyHand = new HashSet<int>(before.GetHand(o).Concat(after.GetHand(o)).Select(x => x.Id));
            var market = new HashSet<int>(new[] { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap }
                .Select(x => before.GetMarket(x)).Where(x => x != null).Select(x => x.Id));

            foreach (var e in events)
            {
                if (setup) Assert.That(e is SetupFinished || OwnedBy(after, e) == p, Is.True, "setup event leaked: " + Serialize(e));
                if (setup && e is SetupFinished sf && sf.Player == c.Player && after.Phase != GamePhase.Setup) setup = false; // round 1 begins
                switch (e)
                {
                    case TrapPlaced t: Assert.That(t.Owner, Is.EqualTo(p)); break;
                    case TrapRemoved t: Assert.That(t.Owner, Is.EqualTo(p)); break;
                    case PrePickSet pp: Assert.That(pp.Player, Is.EqualTo(p)); break;
                    case QuestsChosen q: Assert.That(q.Player, Is.EqualTo(p)); break;
                    case PassiveChosen q: Assert.That(q.Player, Is.EqualTo(p)); break;
                    case TowerPlaced q: Assert.That(q.Player, Is.EqualTo(p)); break;
                    case CardDrawn d:
                        if (d.Player == o) Assert.That(d.CardId == 0 || (d.Source == DrawSource.Market && market.Contains(d.CardId)), Is.True, Serialize(e));
                        break;
                    case CardPlayed cp:
                        if (cp.Player == o)
                        {
                            Assert.That(Catalog.SupportCards.First(x => x.Id == cp.DefId).Category, Is.Not.EqualTo(CardCategory.Trap));
                            Seen(cp.Target, e);
                        }
                        break;
                    case UnitAppeared ua:
                        Seen(ua.Cell, e);
                        AssertAppearedHealth(after, full, e, ua, p);
                        known.Add(ua.UnitId);
                        break;
                    case UnitVanished uv: Seen(uv.Cell, e); break;
                    case Revealed r:
                        // V-08: the piece that attacked. It must be the opponent's, stand on that cell before the command
                        // (attackers do not move first) and really attack in this list; its cell is then Visible (V-08).
                        Assert.That(r.Owner, Is.EqualTo(o));
                        if (r.UnitId == DamageDealt.Tower)
                        {
                            Assert.That(r.Cell, Is.EqualTo(before.GetTower(o).Pos));
                            Assert.That(events.OfType<TowerShot>().Any(t => t.Owner == o), Is.True, "tower revealed without a shot");
                        }
                        else
                        {
                            Assert.That(before.GetUnit(r.UnitId).Pos, Is.EqualTo(r.Cell));
                            Assert.That(events.OfType<DamageDealt>().Any(d => d.SourceUnitId == r.UnitId && d.Kind != DamageKind.Splash)
                                || (c is AttackCommand ac && ac.UnitId == r.UnitId) // e.g. the hit was absorbed by a Shield
                                || events.OfType<OverwatchFired>().Any(f => f.UnitId == r.UnitId),
                                Is.True, "revealed without attacking: " + Serialize(e));
                            known.Add(r.UnitId);
                        }
                        revealedCells.Add(r.Cell);
                        break;
                    case UnitDeployed ud:
                        if (Enemy(ud.UnitId)) Seen(ud.Cell, e);
                        known.Add(ud.UnitId);
                        break;
                    case UnitMoved m when Enemy(m.UnitId): Seen(m.From, e); Seen(m.To, e); break;
                    case UnitTeleported m when Enemy(m.UnitId): Seen(m.From, e); Seen(m.To, e); break;
                    case UnitPushed m when Enemy(m.UnitId): Seen(m.From, e); break; // To: the push stop cell (V-11)
                    case DamageDealt dd:
                        if (dd.TargetUnitId == DamageDealt.Tower ? dd.Target == after.GetTower(o).Pos : Enemy(dd.TargetUnitId)) Seen(dd.Target, e);
                        break;
                    // TrapTriggered.Cell: C-33, a triggered trap is shown to both players wherever it is.
                }
                foreach (var id in UnitIds(e))
                    if (!owner.TryGetValue(id, out var ow) || ow == o)
                        Assert.That(known.Contains(id), Is.True, "hidden enemy unit " + id + " in " + Serialize(e) + " after " + c);
                if (e is CardDrawn cd && cd.Player == o) Assert.That(!enemyHand.Contains(cd.CardId) || cd.Source == DrawSource.Market, Is.True);
            }
            return events.Count;
        }

        /// <summary>UnitAppeared carries the unit's live Health at that moment: its Health after the command plus the
        /// damage minus the healing it received later in the same command (or: it died later).</summary>
        static void AssertAppearedHealth(GameState after, List<GameEvent> full, GameEvent seen, UnitAppeared ua, PlayerId p)
        {
            int i = full.FindIndex(x => x.ViewFor(p) == seen);
            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            var later = full.Skip(i + 1).ToList();
            int damage = later.OfType<DamageDealt>().Where(d => d.TargetUnitId == ua.UnitId).Sum(d => d.Amount);
            int healed = later.OfType<UnitHealed>().Where(h => h.UnitId == ua.UnitId).Sum(h => h.Amount);
            var u = after.GetUnit(ua.UnitId);
            if (u != null) Assert.That(ua.Health, Is.EqualTo(u.Health + damage - healed), Serialize(seen));
            else Assert.That(ua.Health, Is.LessThanOrEqualTo(damage), "appeared, then died: " + Serialize(seen));
        }

        static PlayerId? OwnedBy(GameState after, GameEvent e)
        {
            switch (e)
            {
                case QuestsChosen q: return q.Player;
                case PassiveChosen q: return q.Player;
                case TowerPlaced q: return q.Player;
                case TrapPlaced q: return q.Owner;
                case UnitDeployed u: return after.GetUnit(u.UnitId).Owner; // setup placements never die during setup
                default: return null;
            }
        }

        // ---------- Leak test B: indistinguishability ----------

        [Test]
        public void Leak_B_ScramblingHiddenDataChangesNothingThePlayerSees()
        {
            var rng = new Rng(99);
            int compared = 0, observerCompares = 0, observerSkipped = 0, actorCompares = 0, skippedPush = 0;
            RandomMatches((before, c, after, ev) =>
            {
                foreach (var p in Players)
                {
                    // The view and the legal list for p must not depend on anything hidden from p.
                    var scrambled = Scramble(after, p, rng);
                    Assert.That(Serialize(PlayerView.For(scrambled, p)), Is.EqualTo(Serialize(PlayerView.For(after, p))), "view of " + p + " after " + c);
                    Assert.That(Engine.GetLegalCommands(scrambled, p), Is.EqualTo(Engine.GetLegalCommands(after, p)), "legal of " + p + " after " + c);
                    compared++;
                }
                var actor = c.Player;
                var observer = actor.Opponent();
                // Excluded: Push. V-11 physical collision: the stop cell depends on units the pusher cannot see.
                if (c is PlayCardCommand pc && before.FindInHand(actor, pc.CardId).Support.Effect == EffectKind.Push)
                {
                    skippedPush++;
                    return;
                }

                // Observer side: what the observer sees of the actor's command must not depend on the actor's hidden data.
                var keepUnits = UnitsOf(before, c);
                var observerSpec = new ScrambleSpec
                {
                    Full = false,   // D-05: a Market refill after a Market draw is a public outcome of the pools and the Rng
                    Money = false,  // T-01, T-05: the actor's own Mana/Energy decide whether c is legal
                    Quests = !(c is ChooseQuestsCommand || c is ChoosePassiveCommand), // c names ids from the actor's own offer
                    MoveTower = false, // C-21: the Mirror Trap picks among the empty cells of the actor's home zone
                    KeepUnit = u => keepUnits.Contains(u.Id) || Board.IsHomeZone(u.Pos, actor), // c's own unit; C-21 candidates
                    NoEntry = h => Board.IsHomeZone(h, actor),                                   // C-21 candidates
                    KeepCards = CardsOf(c),
                };
                bool done = false;
                for (int k = 0; k < 5 && !done; k++)
                {
                    // The actor's own hidden units give the actor sight and a Control Zone, so a scramble can make c
                    // illegal for the actor (its own information). Such samples are redrawn, not compared.
                    var scr = Scramble(before, observer, rng, observerSpec);
                    if (!StillLegal(scr, c)) continue;
                    Assert.That(Serialize(EventFilter.For(Engine.Apply(scr, c), observer)), Is.EqualTo(Serialize(EventFilter.For(ev, observer))),
                        "observer " + observer + " events of " + c);
                    done = true;
                }
                if (done) observerCompares++;
                else observerSkipped++;

                // Actor side: the actor's own command, with everything hidden from the actor scrambled except what the
                // rules let hidden pieces do to it.
                Hex? arrival = c is MoveCommand m ? m.Dest : c is DeployCommand d ? d.Cell
                    : c is AttackCommand a ? before.GetUnit(a.UnitId).Pos : (Hex?)null;
                bool couldTrigger = arrival.HasValue && CouldTrigger(before, observer, arrival.Value);
                var mageTarget = c is AttackCommand ma && before.GetUnit(ma.UnitId).Class == UnitClass.Mage ? ma.Target : (Hex?)null;
                bool turnEnd = c is EndTurnCommand || c is TurnTimeoutCommand;
                var actorSpec = new ScrambleSpec
                {
                    Full = false,      // the actor's own blind draws and public Market refills come from the pools and the Rng
                    PrePick = false,   // T-11: at a turn end the opponent's pre-picked draw follows (a Market draw is public)
                    MoveTower = false, // U-27: a hidden tower in range still shoots (the shot reveals it, V-08)
                    MoveTraps = false, // C-32, C-33: trap stops are the listed exception
                    Overwatch = false, // U-28: hidden overwatch still fires
                    MoveUnits = !couldTrigger, // U-27/U-28 with V-11: whether a shot fires depends on the shooter side's sight
                    UnitState = !turnEnd,      // C-17, C-12: poison/Shield on hidden units decide a death whose U-23 draw count is public
                    KeepUnit = u => u.OnOverwatch                                    // U-28
                        || Board.IsHomeZone(u.Pos, actor)                           // C-21 candidates
                        || (mageTarget.HasValue && Hex.Distance(u.Pos, mageTarget.Value) <= 1) // U-04 splash: public U-23 draw count
                        || (turnEnd && u.Class == UnitClass.Healer),                // U-05: a hidden Healer heals visible neighbours
                    NoEntry = h => Board.IsHomeZone(h, actor) || (mageTarget.HasValue && Hex.Distance(h, mageTarget.Value) <= 1),
                };
                var scrA = Scramble(before, actor, rng, actorSpec);
                Assert.That(StillLegal(scrA, c), Is.True, "legality of " + c + " depended on hidden data");
                Assert.That(Serialize(EventFilter.For(Engine.Apply(scrA, c), actor)), Is.EqualTo(Serialize(EventFilter.For(ev, actor))),
                    "actor events of " + c);
                actorCompares++;
            });
            TestContext.Out.WriteLine("observer compares " + observerCompares + " (redrawn out " + observerSkipped + "), actor compares "
                + actorCompares + ", push skipped " + skippedPush);
            Assert.That(compared, Is.GreaterThan(1000));
            Assert.That(observerCompares, Is.GreaterThan(500));
            Assert.That(observerSkipped, Is.LessThan(observerCompares / 10));
            Assert.That(actorCompares, Is.GreaterThan(500));
        }

        static bool StillLegal(GameState s, ICommand c)
        {
            if (c is TurnTimeoutCommand) return s.Phase == GamePhase.Playing && s.ActivePlayer == c.Player;
            if (c is SetupTimeoutCommand) return s.Phase == GamePhase.Setup && !s.IsSetupFinished(c.Player);
            return Engine.GetLegalCommands(s, c.Player).Contains(c);
        }

        static HashSet<int> UnitsOf(GameState s, ICommand c)
        {
            var ids = new HashSet<int>();
            if (c is MoveCommand m) ids.Add(m.UnitId);
            if (c is AttackCommand a) ids.Add(a.UnitId);
            if (c is OverwatchCommand w) ids.Add(w.UnitId);
            if (c is PlayCardCommand pc && s.UnitAt(pc.Target) != null) ids.Add(s.UnitAt(pc.Target).Id);
            return ids;
        }

        static HashSet<int> CardsOf(ICommand c)
        {
            var ids = new HashSet<int>();
            if (c is DeployCommand d) ids.Add(d.CardId);
            if (c is PlayCardCommand pc) ids.Add(pc.CardId);
            if (c is PlaceUnitCommand pu) ids.Add(pu.CardId);
            if (c is PlaceTrapCommand pt) ids.Add(pt.CardId);
            return ids;
        }

        /// <summary>Could a tower shot or an overwatch shot of <paramref name="side"/> hit a unit arriving on / attacking
        /// from <paramref name="cell"/> (U-27, U-28)? Positions only; visibility is what the scramble would change.</summary>
        static bool CouldTrigger(GameState s, PlayerId side, Hex cell)
        {
            var t = s.GetTower(side);
            int dist = Hex.Distance(t.Pos, cell);
            if (t.IsPlaced && s.IsTowerShotAvailable(side) && dist >= Catalog.Tower.MinRange && dist <= Catalog.Tower.MaxRange) return true;
            return s.Units.Any(w => w.Owner == side && w.OnOverwatch && Combat.InRange(w.Def, w.Pos, cell));
        }

        // ---------- Determinism and legality ----------

        [Test]
        public void Replay_MatchFromSeedIsDeterministic()
        {
            const ulong seed = 77;
            var rng = new Rng(4);
            var s1 = Match.Create(seed);
            var commands = new List<ICommand>();
            for (int i = 0; i < 400 && !s1.IsOver; i++)
            {
                var c = NextRandom(s1, rng);
                commands.Add(c);
                Engine.Apply(s1, c);
            }
            Assert.That(commands.OfType<FinishSetupCommand>().Count() + commands.OfType<SetupTimeoutCommand>().Count(), Is.GreaterThanOrEqualTo(2));
            Assert.That(commands.OfType<AttackCommand>().Count(), Is.GreaterThan(0));
            var s2 = Match.Create(seed);
            foreach (var c in commands) Engine.Apply(s2, c);
            Assert.That(StateHash.Compute(s2), Is.EqualTo(StateHash.Compute(s1)));
            foreach (var p in Players) Assert.That(Serialize(PlayerView.For(s2, p)), Is.EqualTo(Serialize(PlayerView.For(s1, p))));
        }

        [Test]
        public void Legal_MatchIllegalCommandsAreRejected()
        {
            int rejected = 0, setupRejected = 0;
            foreach (ulong seed in new ulong[] { 3, 4 })
            {
                var rng = new Rng(seed + 50);
                var s = Match.Create(seed);
                for (int step = 0; step < 200 && !s.IsOver; step++)
                {
                    for (int k = 0; k < 15; k++)
                    {
                        var c = RandomCommand(s, rng);
                        if (Engine.GetLegalCommands(s, c.Player).Contains(c)) continue;
                        if (c is SetupTimeoutCommand && s.Phase == GamePhase.Setup && !s.IsSetupFinished(c.Player)) continue;
                        if (c is TurnTimeoutCommand && s.Phase == GamePhase.Playing && c.Player == s.ActivePlayer) continue;
                        var before = StateHash.Compute(s);
                        Assert.Throws<IllegalCommandException>(() => Engine.Apply(s, c), c.ToString());
                        Assert.That(StateHash.Compute(s), Is.EqualTo(before), c.ToString());
                        rejected++;
                        if (IsSetupCommand(c)) setupRejected++;
                    }
                    Engine.Apply(s, NextRandom(s, rng));
                }
            }
            Assert.That(rejected, Is.GreaterThan(3000));
            Assert.That(setupRejected, Is.GreaterThan(500));
        }

        static bool IsSetupCommand(ICommand c) =>
            c is ChooseQuestsCommand || c is ChoosePassiveCommand || c is PlaceTowerCommand || c is PlaceUnitCommand
            || c is PlaceTrapCommand || c is FinishSetupCommand || c is SetupTimeoutCommand;

        static ICommand RandomCommand(GameState s, Rng rng)
        {
            var player = rng.NextInt(2) == 0 ? A : B;
            var cell = Board.Cells[rng.NextInt(Board.Cells.Count)];
            var cards = s.GetHand(A).Concat(s.GetHand(B)).Select(c => c.Id).ToList();
            int cardId = cards.Count > 0 && rng.NextInt(4) != 0 ? cards[rng.NextInt(cards.Count)] : rng.NextInt(0, s.NextCardId + 1);
            var quests = Catalog.Quests.Select(q => q.Id).ToList();
            rng.Shuffle(quests);
            switch (rng.NextInt(14))
            {
                case 0: return new ChooseQuestsCommand(player, quests.Take(rng.NextInt(2, 5)).ToArray());
                case 1: return new ChoosePassiveCommand(player, Catalog.Passives[rng.NextInt(Catalog.Passives.Count)].Id);
                case 2: return new PlaceTowerCommand(player, cell);
                case 3: return new PlaceUnitCommand(player, cardId, cell);
                case 4: return new PlaceTrapCommand(player, cardId, cell);
                case 5: return new FinishSetupCommand(player);
                case 6: return new SetupTimeoutCommand(player);
                case 7: return new TurnTimeoutCommand(player);
                case 8: return new MoveCommand(player, rng.NextInt(0, s.NextUnitId + 1), cell);
                case 9: return new AttackCommand(player, rng.NextInt(0, s.NextUnitId + 1), cell);
                case 10: return new DeployCommand(player, cardId, cell);
                case 11: return new PlayCardCommand(player, cardId, cell);
                case 12: return new DrawCommand(player, rng.NextInt(-1, 3));
                default: return new EndTurnCommand(player);
            }
        }

        // ---------- Clone and hash ----------

        [Test]
        public void Clone_IsADeepIndependentCopy()
        {
            var s = StartedMatch(55);
            var rng = new Rng(8);
            for (int i = 0; i < 60; i++) Engine.Apply(s, NextRandom(s, rng));
            var copy = s.Clone();
            Assert.That(StateHash.Compute(copy), Is.EqualTo(StateHash.Compute(s)));
            foreach (var p in Players) Assert.That(Serialize(PlayerView.For(copy, p)), Is.EqualTo(Serialize(PlayerView.For(s, p))));
            var hash = StateHash.Compute(s);
            var rng2 = new Rng(9);
            for (int i = 0; i < 60 && !copy.IsOver; i++) Engine.Apply(copy, NextRandom(copy, rng2));
            Assert.That(StateHash.Compute(s), Is.EqualTo(hash));
            // Same commands on original and clone give the same result (the Rng position is copied).
            var c1 = s.Clone();
            var c2 = s.Clone();
            var r1 = new Rng(10);
            var r2 = new Rng(10);
            for (int i = 0; i < 60 && !c1.IsOver; i++)
            {
                Engine.Apply(c1, NextRandom(c1, r1));
                Engine.Apply(c2, NextRandom(c2, r2));
            }
            Assert.That(StateHash.Compute(c2), Is.EqualTo(StateHash.Compute(c1)));
        }

        [Test]
        public void Replay_StateHashCoversMatchState()
        {
            GameState Make()
            {
                var s = Match.Create(61);
                SetupPlayer(s, A, 3, finish: false);
                return s;
            }
            var baseHash = StateHash.Compute(Make());
            Assert.That(StateHash.Compute(Make()), Is.EqualTo(baseHash));
            var changes = new List<Action<GameState>>
            {
                s => Engine.Apply(s, new FinishSetupCommand(A)),
                s => s.SetQuestOffer(B, Catalog.Quests.Reverse().Take(5).ToList()), // offers are kept in Catalog order
                s => s.SetQuestChoices(A, s.GetQuestOffer(A).Skip(2).Take(3).ToList()),
                s => s.SetPassiveOffer(B, Catalog.Passives.Reverse().Take(3).ToList()),
                s => s.SetPassiveChoice(A, s.GetPassiveOffer(A).First(d => d != s.GetPassiveChoice(A))),
                s => s.GetTower(A).RevealedUntilTurn = 3,
                s => s.Units[0].RevealedUntilTurn = 3,
                s => s.Result = new GameResult(B, WinReason.Timeout),
                s => s.SetConsecutiveTimeouts(B, 1),
                s => s.SetDealtHandCount(A, 3),
                s => s.GetFog(A).Set(Board.Portal, CellVisibility.Explored, s.GetFog(A).GetLastSeen(Board.Portal)),
                s => s.GetFog(B).Set(Board.Cells[50], CellVisibility.Explored, s.GetFog(B).GetLastSeen(Board.Cells[50])), // A's half: Hidden for B
                s => s.GetFog(A).Set(Board.Cells[40], CellVisibility.Explored, new LastSeen(new Tile(Biome.Snow, Marker.None), 99, B, UnitClass.Mage, Biome.Snow, 2, false, A, 0)),
            };
            foreach (var change in changes)
            {
                var s = Make();
                change(s);
                Assert.That(StateHash.Compute(s), Is.Not.EqualTo(baseHash));
            }
        }
    }
}
