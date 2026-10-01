using System;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;
using static HexPortal.Tests.MatchHelper;

namespace HexPortal.Tests
{
    // Whole-match checks: fog leak tests (V-05, V-09, V-10, V-11, S-08, Q-03, P-00, E-05), replay determinism, illegal
    // commands, GameState.Clone and StateHash coverage of the M4 state, and the 200-match full-play test.
    public class MatchTests
    {
        // Matches end at round 15 at the latest (W-03), so six seeds keep the command counts of the checks below.
        static readonly ulong[] LeakSeeds = { 101, 202, 303, 404, 505, 606 };
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
                checkedEvents += ScanCommand(before, c, after, ev);
                foreach (var p in Players)
                {
                    ghosts += PlayerView.For(after, p).Cells.Count(x => x.Unit != null && x.Unit.IsGhost);
                    foreach (var e in EventFilter.For(ev, p))
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

        /// <summary>Leak test A for one command: both players' views and filtered events. Returns the events checked.</summary>
        static int ScanCommand(GameState before, ICommand c, GameState after, List<GameEvent> ev)
        {
            int n = 0;
            foreach (var p in Players)
            {
                ScanView(after, p);
                n += ScanEvents(before, after, ev, EventFilter.For(ev, p), p, c);
            }
            return n;
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
            // Q-03, Q-04, P-00: of the opponent only completed/failed quests and a revealed passive; own statuses in full.
            var own = s.GetProgress(p);
            var opp = s.GetProgress(o);
            Assert.That(v.QuestStatuses, Is.EqualTo(Enumerable.Range(0, s.GetQuestChoices(p).Count).Select(i => own.GetQuestStatus(i))));
            var oq = s.GetQuestChoices(o);
            Assert.That(v.OpponentCompletedQuests, Is.EqualTo(oq.Where((q, i) => opp.GetQuestStatus(i) == QuestStatus.Completed).Select(q => q.Id)));
            Assert.That(v.OpponentFailedQuests, Is.EqualTo(oq.Where((q, i) => opp.GetQuestStatus(i) == QuestStatus.Failed).Select(q => q.Id)));
            Assert.That(v.PassiveRevealed, Is.EqualTo(own.PassiveRevealed));
            Assert.That(v.OpponentPassive, Is.EqualTo(opp.PassiveRevealed ? s.GetPassiveChoice(o).Id : null));
            // E-05: the announced event is public.
            Assert.That(v.AnnouncedEvent, Is.EqualTo(s.PendingEvent == null ? null : s.PendingEvent.Id));
            Assert.That(v.AnnouncedEventCells, Is.EqualTo(s.PendingEventCells));

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
        /// the cell of a V-08 reveal for events about that piece (checked separately: the revealed piece really attacks in
        /// this list). The only exception is TrapTriggered.Cell (C-33). (The V-11 push collision is an inference, handled
        /// in Leak test B; a push stop cell is only ever emitted when Visible.)</summary>
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
            var revealedCells = new Dictionary<Hex, int>(); // V-08 reveal cell -> the revealed piece (unit id or DamageDealt.Tower)
            bool Enemy(int id) => !owner.TryGetValue(id, out var ow) || ow == o;
            // A reveal cell only covers events about the revealed piece itself.
            bool AboutRevealed(Hex h, GameEvent ev) =>
                revealedCells.TryGetValue(h, out int piece)
                && (piece == DamageDealt.Tower
                    ? ev is TowerShot || (ev is DamageDealt td && td.TargetUnitId == DamageDealt.Tower)
                    : UnitIds(ev).Contains(piece));
            void Seen(Hex h, GameEvent ev) =>
                Assert.That(visBefore.Contains(h) || visAfter.Contains(h) || AboutRevealed(h, ev), Is.True,
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
                        revealedCells[r.Cell] = r.UnitId;
                        break;
                    case UnitDeployed ud:
                        if (Enemy(ud.UnitId)) Seen(ud.Cell, e);
                        known.Add(ud.UnitId);
                        break;
                    case UnitRevived ur: // P-01: seen like a deploy (it may die on a trap in the same command)
                        owner[ur.UnitId] = ur.Owner;
                        if (ur.Owner == o) Seen(ur.Cell, e);
                        known.Add(ur.UnitId);
                        break;
                    // Q-03, Q-04, P-00: public, and only ever about a quest/passive that really is resolved/revealed now.
                    case QuestCompleted q: Assert.That(after.GetQuestStatus(q.Player, q.QuestId), Is.EqualTo(QuestStatus.Completed)); break;
                    case QuestFailed q: Assert.That(after.GetQuestStatus(q.Player, q.QuestId), Is.EqualTo(QuestStatus.Failed)); break;
                    case PassiveRevealed pr:
                        Assert.That(after.GetProgress(pr.Player).PassiveRevealed && after.GetPassiveChoice(pr.Player).Id == pr.PassiveId, Is.True, Serialize(e));
                        break;
                    // MapEventAnnounced/MapEventResolved cells, TowerDamageBlocked: public (E-05, V-09).
                    case UnitMoved m when Enemy(m.UnitId): Seen(m.From, e); Seen(m.To, e); break;
                    case UnitTeleported m when Enemy(m.UnitId): Seen(m.From, e); Seen(m.To, e); break;
                    case UnitPushed m when Enemy(m.UnitId): Seen(m.From, e); Seen(m.To, e); break;
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
            var st = new LeakBStats();
            RandomMatches((before, c, after, ev) => CheckIndistinguishable(before, c, after, ev, rng, st));
            TestContext.Out.WriteLine(st);
            Assert.That(st.Compared, Is.GreaterThan(1000));
            Assert.That(st.ObserverCompares, Is.GreaterThan(500));
            Assert.That(st.ObserverSkipped, Is.LessThan(st.ObserverCompares / 10));
            Assert.That(st.ActorCompares, Is.GreaterThan(500));
            Assert.That(st.Pushes, Is.GreaterThan(0));
            Assert.That(st.QuestScrambles, Is.GreaterThan(300));
            Assert.That(st.PassiveScrambles, Is.GreaterThan(300));
        }

        sealed class LeakBStats
        {
            public int Compared, ObserverCompares, ObserverSkipped, ActorCompares, Pushes, QuestScrambles, PassiveScrambles;
            public override string ToString() =>
                "static compares " + Compared + ", observer compares " + ObserverCompares + " (redrawn out " + ObserverSkipped
                + "), actor compares " + ActorCompares + ", pushes " + Pushes + ", event compares with hidden quests scrambled "
                + QuestScrambles + ", with the hidden passive scrambled " + PassiveScrambles;
        }

        static readonly string[] PositionQuests = { "Q-10", "Q-11", "Q-15", "Q-18", "Q-19" };

        static bool HasActiveQuest(GameState s, PlayerId p, string[] ids) =>
            s.GetQuestChoices(p).Where((q, i) => s.GetProgress(p).GetQuestStatus(i) == QuestStatus.Active).Any(q => ids.Contains(q.Id));

        /// <summary>Leak test B for one command: p's view and legal list do not depend on anything hidden from p, and the
        /// events each side sees of the command do not depend on the other side's hidden data, except where the rules
        /// let hidden things have public consequences (each exception is commented).</summary>
        static void CheckIndistinguishable(GameState before, ICommand c, GameState after, List<GameEvent> ev, Rng rng, LeakBStats st)
        {
            foreach (var p in Players)
            {
                // The view and the legal list for p must not depend on anything hidden from p.
                var scrambled = Scramble(after, p, rng);
                Assert.That(Serialize(PlayerView.For(scrambled, p)), Is.EqualTo(Serialize(PlayerView.For(after, p))), "view of " + p + " after " + c);
                Assert.That(Engine.GetLegalCommands(scrambled, p), Is.EqualTo(Engine.GetLegalCommands(after, p)), "legal of " + p + " after " + c);
                st.Compared++;
            }
            var actor = c.Player;
            var observer = actor.Opponent();
            bool playing = before.Phase == GamePhase.Playing;
            bool turnEnd = playing && (c is EndTurnCommand || c is TurnTimeoutCommand);
            bool roundEnd = turnEnd && actor == B;
            // E-04 (v2.9): the announcement uses terrain only, but when the event happens a pair is skipped if a unit or tower
            // stands on it (seen or not), and E-10/E-12 then remove ghosts there (v2.10).
            bool mapResolve = roundEnd && before.PendingEvent != null && before.PendingEventRound == before.Round + 1;
            // W-03 criterion 3: the total Health of all units on the board, seen or not.
            bool roundLimit = roundEnd && before.Round >= Catalog.RoundLimit;
            // P-01: the observer (next to start a turn) may get its unit back on a random empty cell of its home zone,
            // where it can trigger the actor's trap.
            var obsPassive = before.GetPassiveChoice(observer);
            bool lastBreath = turnEnd && obsPassive != null && obsPassive.Id == "P-01" && before.GetProgress(observer).LastBreathPending;
            bool isPush = IsPush(before, c);
            var obsTraps = new HashSet<Hex>(before.Traps.Where(t => t.Owner == observer).Select(t => t.Pos));
            var obsTower = before.GetTower(observer);
            // C-21: an untriggered Mirror Trap of the observer can send an actor unit to a random empty cell of the
            // actor's home zone, so only then does home-zone occupancy (units, the actor's tower) legitimately matter.
            bool mirror = before.Traps.Any(t => t.Owner == observer && t.Card.DefId == "C-21");
            bool Home(Hex h) => (mirror && Board.IsHomeZone(h, actor)) || (lastBreath && Board.IsHomeZone(h, observer));
            // V-11 physical collision: a push stops before any piece on its line, seen or not. Pieces on the line
            // stay, and nothing is moved onto it; everything else is scrambled as usual.
            var pushLine = PushLine(before, c);
            bool unitAction = c is MoveCommand || c is AttackCommand || c is OverwatchCommand;
            bool setupChoice = c is ChooseQuestsCommand || c is ChoosePassiveCommand; // c names ids from the actor's own offer

            // Observer side: what the observer sees of the actor's command must not depend on the actor's hidden data.
            var keepUnits = UnitsOf(before, c);
            var actorTower = before.GetTower(actor);
            // U-04 with U-28: an overwatching observer Mage that can fire at the arriving / attacking unit splashes the cells
            // next to it. Hits there are public: a death through the U-23 draw count (accepted, M4a), the tower through its
            // Health (V-09) and a P-04 block (P-00).
            Hex? shotCell = c is MoveCommand m0 ? m0.Dest : c is DeployCommand d0 ? d0.Cell
                : c is AttackCommand a0 ? before.GetUnit(a0.UnitId).Pos : (Hex?)null;
            bool mageWatch = shotCell.HasValue && before.Units.Any(w => w.Owner == observer && w.OnOverwatch
                && w.Class == UnitClass.Mage && Combat.InRange(w.Def, w.Pos, shotCell.Value));
            bool Splash(Hex h) => mageWatch && Hex.Distance(h, shotCell.Value) <= 1;
            bool towerSplash = actorTower.IsPlaced && Splash(actorTower.Pos);
            var observerSpec = new ScrambleSpec
            {
                Full = false,   // D-05: a Market refill after a Market draw is a public outcome of the pools and the Rng
                MinMana = before.GetMana(actor),     // T-01: never below what c costs (keeps c legal)
                MinEnergy = unitAction ? 1 : 0,      // T-05: a unit action needs 1 Energy
                // Q-02, Q-03, Q-04: the actor's quests (and the hidden Q-16 loss count, v2.10) are judged at its turn end
                // and at the round end; the result is public.
                Quests = !setupChoice && !turnEnd,
                // P-00: the actor's passive shows when it first takes effect: P-05 on a Market draw (DrawCommand, timeout
                // draw), P-02/P-06 when a C-18 push lands an observer unit, P-02 when a P-01 return lands on its trap.
                // P-04 when the overwatch splash reaches the actor's tower.
                Passive = !setupChoice && !(c is DrawCommand || c is TurnTimeoutCommand) && !isPush && !lastBreath && !towerSplash,
                PassiveState = false,
                MoveTower = !mapResolve && !towerSplash && !(actorTower.IsPlaced && (Home(actorTower.Pos) || pushLine.Contains(actorTower.Pos))),
                TowerNoEntry = Splash,
                MoveTraps = pushLine.Count == 0 && !lastBreath, // C-32: a pushed / returning unit stops on the actor's trap
                // Q-02: the actor's position quests (Q-10, Q-11, Q-15, Q-18, Q-19) are judged at its turn end.
                MoveUnits = !mapResolve && !(turnEnd && HasActiveQuest(before, actor, PositionQuests)),
                UnitState = !roundLimit,
                KeepUnit = u => keepUnits.Contains(u.Id) || Home(u.Pos) || pushLine.Contains(u.Pos) || Splash(u.Pos),
                NoEntry = h => Home(h) || pushLine.Contains(h) || Splash(h),
                KeepCards = CardsOf(c),
            };
            bool done = false;
            for (int k = 0; k < 5 && !done; k++)
            {
                // The actor's own hidden units give the actor sight and a Control Zone, so a scramble can make c
                // illegal for the actor (its own information). Such samples are redrawn, not compared.
                var scr = Scramble(before, observer, rng, observerSpec);
                if (!StillLegal(scr, c)) continue;
                var scrEvents = Engine.Apply(scr, c);
                string got = Serialize(EventFilter.For(scrEvents, observer)), want = Serialize(EventFilter.For(ev, observer));
                if (got != want)
                    Assert.Fail("observer " + observer + " events of " + c + "\nWANT (all events):\n" + Serialize(ev)
                        + "\nGOT (all events):\n" + Serialize(scrEvents) + "\nactor passive real/scrambled: "
                        + before.GetPassiveChoice(actor)?.Id + "/" + scr.GetPassiveChoice(actor)?.Id);
                done = true;
            }
            if (done)
            {
                st.ObserverCompares++;
                if (observerSpec.Quests) st.QuestScrambles++;
                if (observerSpec.Passive) st.PassiveScrambles++;
            }
            else st.ObserverSkipped++;

            // Actor side: the actor's own command, with everything hidden from the actor scrambled except what the
            // rules let hidden pieces do to it.
            Hex? arrival = c is MoveCommand m ? m.Dest : c is DeployCommand d ? d.Cell
                : c is AttackCommand a ? before.GetUnit(a.UnitId).Pos : (Hex?)null;
            bool couldTrigger = arrival.HasValue && CouldTrigger(before, observer, arrival.Value);
            var mageTarget = c is AttackCommand ma && before.GetUnit(ma.UnitId).Class == UnitClass.Mage ? ma.Target : (Hex?)null;
            bool NearMage(Hex h) => mageTarget.HasValue && Hex.Distance(h, mageTarget.Value) <= 1; // U-04 splash cells
            // U-27: a hidden tower that could reach the arrival cell still shoots (the shot reveals it, V-08).
            bool TowerReach(Hex h) => arrival.HasValue && Hex.Distance(h, arrival.Value) <= Catalog.Tower.MaxRange;
            var enemyTower = before.GetTower(observer);
            var actorSpec = new ScrambleSpec
            {
                Full = false,      // the actor's own blind draws and public Market refills come from the pools and the Rng
                PrePick = !turnEnd, // T-11: at a turn end the opponent's pre-picked draw follows (a Market draw is public)
                // Q-02, Q-14, Q-16: only the round end judges the observer's quests during the actor's command.
                Quests = !roundEnd,
                Passive = !ObserverPassiveCanMatter(before, c, observer, obsTraps, obsTower, isPush, turnEnd, lastBreath),
                PassiveState = false,
                // The tower stays when it could shoot (U-27), when its sight decides an overwatch shot (V-11, couldTrigger),
                // when splash reaches it (V-09: public tower Health), or when it stands on the push line (V-11).
                MoveTower = !couldTrigger && !mapResolve && !(enemyTower.IsPlaced && (TowerReach(enemyTower.Pos) || NearMage(enemyTower.Pos)
                    || pushLine.Contains(enemyTower.Pos) || Home(enemyTower.Pos))),
                TowerNoEntry = h => TowerReach(h) || NearMage(h),
                MoveTraps = false, // C-32, C-33: trap stops are the listed exception
                Overwatch = false, // U-28: hidden overwatch still fires
                MoveUnits = !couldTrigger && !mapResolve, // U-27/U-28 with V-11: whether a shot fires depends on the shooter side's sight
                UnitState = !roundLimit,
                KeepUnit = u => u.OnOverwatch                  // U-28
                    || Home(u.Pos)                             // C-21 candidates, P-01 return cells
                    || pushLine.Contains(u.Pos)                // V-11 push collision
                    || NearMage(u.Pos)                         // U-04 splash: a death shows as a public U-23 draw count
                    || (turnEnd && u.Class == UnitClass.Healer) // U-05: a hidden Healer heals visible neighbours
                    || (turnEnd && u.Debuff != null && u.Debuff.Def.Id == "C-17"), // C-17: a poison death shows as a U-23 draw
                NoEntry = h => Home(h) || pushLine.Contains(h) || NearMage(h),
            };
            var scrA = Scramble(before, actor, rng, actorSpec);
            Assert.That(StillLegal(scrA, c), Is.True, "legality of " + c + " depended on hidden data");
            Assert.That(Serialize(EventFilter.For(Engine.Apply(scrA, c), actor)), Is.EqualTo(Serialize(EventFilter.For(ev, actor))),
                "actor events of " + c);
            st.ActorCompares++;
            if (actorSpec.Quests) st.QuestScrambles++;
            if (actorSpec.Passive) st.PassiveScrambles++;
            if (pushLine.Count > 0) st.Pushes++;
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

        /// <summary>C-18: the cells a Push may move its target through (up to the card's Amount), empty if c is no Push.</summary>
        static HashSet<Hex> PushLine(GameState s, ICommand c)
        {
            var line = new HashSet<Hex>();
            if (!(c is PlayCardCommand pc)) return line;
            var card = s.FindInHand(pc.Player, pc.CardId);
            if (card == null || card.IsCharacter || card.Support.Effect != EffectKind.Push) return line;
            var h = pc.Target;
            for (int k = 0; k < card.Support.Amount; k++)
            {
                h = h.Neighbor(pc.Direction);
                line.Add(h);
            }
            return line;
        }

        static bool IsPush(GameState s, ICommand c)
        {
            if (!(c is PlayCardCommand pc)) return false;
            var card = s.FindInHand(pc.Player, pc.CardId);
            return card != null && !card.IsCharacter && card.Support.Effect == EffectKind.Push;
        }

        /// <summary>P-00: can the observer's (hidden) passive take effect during the actor's command c? Only then is it kept
        /// in the actor-side scramble. P-02: the actor's unit stops on an observer trap (move, deploy, C-15 teleport;
        /// a Mirror chain starts there). P-06: it stops on the Portal. P-04: an attack or Mage splash can reach the observer's
        /// tower. C-18 pushes (reviewer rule). Turn ends: the observer's turn start (P-01 return pending, P-03 in its round,
        /// P-05 through a Market pre-pick).</summary>
        static bool ObserverPassiveCanMatter(GameState s, ICommand c, PlayerId observer, HashSet<Hex> obsTraps, Tower obsTower,
            bool isPush, bool turnEnd, bool lastBreath)
        {
            bool Stop(Hex h) => h == Board.Portal || obsTraps.Contains(h);
            switch (c)
            {
                case MoveCommand m: return Stop(m.Dest);
                case DeployCommand d: return Stop(d.Cell);
                case AttackCommand a:
                    if (!obsTower.IsPlaced) return false;
                    if (a.Target == obsTower.Pos) return true;
                    return s.GetUnit(a.UnitId).Class == UnitClass.Mage && Hex.Distance(a.Target, obsTower.Pos) <= 1;
                case PlayCardCommand pc:
                    return isPush || (pc.Dest.HasValue && Stop(pc.Dest.Value));
            }
            if (!turnEnd) return false;
            int round = observer == PlayerId.B ? s.Round : s.Round + 1; // the round of the observer's next turn start
            var quick = Catalog.Passives.First(p => p.Id == "P-03");
            return lastBreath || round == quick.Round || s.GetPrePickSlot(observer) >= 0;
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

        /// <summary>Light mode (AI simulations) skips fog-memory updates and per-event visibility tagging only: the same
        /// random legal command sequences give the same legal lists, events (types, in order) and state hash without fog
        /// memory, with and without it.</summary>
        [Test]
        public void LightMode_RulesOutcomesMatchFullMode()
        {
            int commands = 0;
            for (ulong seed = 31; seed <= 40; seed++)
            {
                var rng = new Rng(seed * 13);
                var full = Match.Create(seed);
                var light = Match.Create(seed);
                light.Light = true;
                Assert.That(light.Clone().Light, Is.True, "Clone keeps the mode");
                for (int i = 0; i < 2000 && !full.IsOver; i++)
                {
                    var c = NextUniform(full, rng);
                    var evFull = Engine.Apply(full, c);
                    var evLight = Engine.Apply(light, c);
                    commands++;
                    Assert.That(evLight.Select(e => e.GetType()), Is.EqualTo(evFull.Select(e => e.GetType())), "seed " + seed + ": " + c);
                    Assert.That(evLight.All(e => e.ViewFor(A) == null && e.ViewFor(B) == null), "light events are not tagged");
                    Assert.That(StateHash.Compute(light, false), Is.EqualTo(StateHash.Compute(full, false)), "seed " + seed + ": " + c);
                    foreach (var p in Players)
                        Assert.That(Engine.GetLegalCommands(light, p), Is.EqualTo(Engine.GetLegalCommands(full, p)));
                }
                Assert.That(full.IsOver, Is.True);
                Assert.That(StateHash.Compute(light), Is.Not.EqualTo(StateHash.Compute(full)), "light mode really skips the fog memory");
            }
            Assert.That(commands, Is.GreaterThan(1000));
        }

        // ---------- Full matches (M4 done criterion) ----------

        const int FullMatches = 200;
        const int HeavyCheckEvery = 20; // Leak test B (indistinguishability) on every 20th match: runtime
        const int MaxCommandsPerMatch = 5000;

        /// <summary>200 seeded matches, both players choosing uniformly from GetLegalCommands (setup included) with
        /// occasional clock commands: no legal command is rejected, every match ends with a W-* result, the replay of the
        /// seed and command list gives the same StateHash, and the leak checks run after every command
        /// (field scan on all matches, indistinguishability on every HeavyCheckEvery-th).</summary>
        [Test]
        public void FullMatch_RandomLegalPlayEndsWithAResult_ReplaysAndNeverLeaks()
        {
            var results = new SortedDictionary<string, int>();
            var kinds = new HashSet<Type>();
            var st = new LeakBStats();
            var heavyRng = new Rng(4242);
            int commands = 0, extraLegal = 0, maxRound = 0;
            // Deterministic by default (offset 0). Set HEXPORTAL_HEAVY_OFFSET=0..19 to run the heavy check on another
            // subset; running all 20 offsets covers every match (done for M4b, all green).
            var env = Environment.GetEnvironmentVariable("HEXPORTAL_HEAVY_OFFSET");
            int offset = env != null ? int.Parse(env) % HeavyCheckEvery : 0;
            TestContext.Out.WriteLine("heavy leak check on seeds with (seed + " + offset + ") % " + HeavyCheckEvery + " == 0");
            for (ulong seed = 1; seed <= FullMatches; seed++)
            {
                var rng = new Rng(seed * 104729 + 7);
                var s = Match.Create(seed);
                var list = new List<ICommand>();
                bool heavy = ((int)seed + offset) % HeavyCheckEvery == 0;
                while (!s.IsOver && list.Count < MaxCommandsPerMatch)
                {
                    var c = NextUniform(s, rng);
                    var before = s.Clone();
                    List<GameEvent> ev;
                    try { ev = Engine.Apply(s, c); }
                    catch (IllegalCommandException x) { Assert.Fail("seed " + seed + ": legal " + c + " rejected: " + x.Message); throw; }
                    list.Add(c);
                    ScanCommand(before, c, s, ev);
                    if (heavy) CheckIndistinguishable(before, c, s, ev, heavyRng, st);
                    foreach (var e in ev) kinds.Add(e.GetType());
                    // Every 10th command: a few other legal commands of each player must be accepted too.
                    if (list.Count % 10 == 0 && !s.IsOver)
                        foreach (var p in Players)
                        {
                            var legal = Engine.GetLegalCommands(s, p);
                            for (int k = 0; k < 2 && legal.Count > 0; k++)
                            {
                                var other = legal[rng.NextInt(legal.Count)];
                                try { Engine.Apply(s.Clone(), other); }
                                catch (IllegalCommandException x) { Assert.Fail("seed " + seed + ": legal " + other + " rejected: " + x.Message); }
                                extraLegal++;
                            }
                        }
                }
                commands += list.Count;
                maxRound = Math.Max(maxRound, s.Round);
                Assert.That(s.IsOver, Is.True, "seed " + seed + " did not end in " + MaxCommandsPerMatch + " commands");
                var r = s.Result;
                string key = r.Reason == WinReason.RoundLimit ? "W-03 criterion " + r.Criterion
                    : r.Reason == WinReason.Portal ? "W-01" : r.Reason == WinReason.Tower ? "W-02" : "W-04";
                results[key] = results.TryGetValue(key, out int n) ? n + 1 : 1;

                var replay = Match.Create(seed);
                foreach (var c in list) Engine.Apply(replay, c);
                Assert.That(StateHash.Compute(replay), Is.EqualTo(StateHash.Compute(s)), "replay of seed " + seed);
            }
            TestContext.Out.WriteLine("matches " + FullMatches + ", commands " + commands + ", extra legal commands applied " + extraLegal
                + ", max round " + maxRound);
            TestContext.Out.WriteLine("results: " + string.Join(", ", results.Select(kv => kv.Key + " = " + kv.Value)));
            TestContext.Out.WriteLine("heavy leak check: " + st);
            TestContext.Out.WriteLine("event kinds: " + string.Join(",", kinds.Select(t => t.Name).OrderBy(x => x)));
            Assert.That(results.Values.Sum(), Is.EqualTo(FullMatches));
            Assert.That(maxRound, Is.LessThanOrEqualTo(Catalog.RoundLimit));
            Assert.That(st.ObserverCompares, Is.GreaterThan(1000));
            foreach (var k in new[] { typeof(QuestCompleted), typeof(QuestFailed), typeof(PassiveRevealed), typeof(MapEventAnnounced),
                         typeof(MapEventResolved), typeof(UnitRevived), typeof(TowerDamageBlocked), typeof(GameOver) })
                Assert.That(kinds, Does.Contain(k), k.Name + " never happened");
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
                // M4b: quests, passives, W-01 watch, map events.
                s => s.GetProgress(A).SetQuestStatus(1, QuestStatus.Completed),
                s => s.GetProgress(B).SetQuestStatus(0, QuestStatus.Failed),
                s => s.GetProgress(A).Kills = 1,
                s => s.GetProgress(B).TowerDamage = 2,
                s => s.GetProgress(A).TrapsSprung = 1,
                s => s.GetProgress(B).SetHoldStreak(Board.Cells[12], 1),
                s => s.GetProgress(A).PassiveRevealed = true,
                s => s.GetProgress(B).LastBreathUsed = true,
                s => s.GetProgress(A).LastBreathPending = true,
                s => s.GetProgress(B).LastBreathClass = UnitClass.Mage,
                s => s.GetProgress(A).LastBreathBiome = Biome.Snow,
                s => s.GetProgress(B).WallBlocked = 1,
                s => s.GetProgress(A).MerchantUsed = true,
                s => s.GetProgress(B).PortalUnitId = 2,
                s => s.SetPendingEvent(Catalog.MapEvents[1], 3, new[] { Board.Cells[10] }),
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
