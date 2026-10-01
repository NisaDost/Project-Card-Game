using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HexPortal.Core;
using NUnit.Framework;

namespace HexPortal.Game.Tests
{
    public class MatchControllerTests
    {
        /// <summary>S-08, T-04…T-10, W-*: a whole hotseat match driven only through MatchController (legal commands,
        /// handoffs) reaches a result.</summary>
        [Test]
        public void M6_HotseatMatchRunsToResult()
        {
            var m = new MatchController();
            int handoffs = 0, events = 0;
            m.EventsApplied += e => events += e.Count;
            m.NewMatch(12345, timer: false);
            var rng = new Random(7);
            int steps = 0, actionsThisTurn = 0;
            while (!m.IsOver && steps++ < 20000)
            {
                if (m.HandoffPending)
                {
                    Assert.That(m.View.Viewer, Is.EqualTo(m.Viewer));
                    m.AcknowledgeHandoff();
                    handoffs++;
                    actionsThisTurn = 0;
                    continue;
                }
                var legal = m.Legal;
                Assert.That(legal, Is.Not.Empty, "a player with no legal command would block hotseat");
                ICommand cmd;
                if (m.View.Phase == GamePhase.Setup)
                    cmd = legal.FirstOrDefault(c => c is FinishSetupCommand)
                          ?? (m.View.SetupStep == SetupStep.Placement ? legal.First(c => c is PlaceUnitCommand) : legal[rng.Next(legal.Count)]);
                else
                {
                    var end = legal.FirstOrDefault(c => c is EndTurnCommand);
                    var others = legal.Where(c => !(c is EndTurnCommand)).ToList();
                    cmd = end != null && (others.Count == 0 || actionsThisTurn >= 4) ? end : others[rng.Next(others.Count)];
                    actionsThisTurn++;
                }
                Assert.That(m.Apply(cmd), Is.True, "legal command rejected: " + cmd);
            }
            Assert.That(m.IsOver, Is.True, "match did not finish");
            Assert.That(m.View.Result, Is.Not.Null);
            Assert.That(handoffs, Is.GreaterThanOrEqualTo(3), "setup A, setup B and at least one turn handoff");
            Assert.That(events, Is.GreaterThan(0));
        }

        [Test]
        public void M6_IllegalCommandIsRejectedWithoutChange()
        {
            var m = new MatchController();
            m.NewMatch(1, timer: false);
            m.AcknowledgeHandoff();
            int round = m.View.Round;
            Assert.That(m.Apply(new EndTurnCommand(PlayerId.A)), Is.False);
            Assert.That(m.LastError, Is.Not.Null);
            Assert.That(m.View.Round, Is.EqualTo(round));
        }

        [Test]
        public void M6_ClockTimeoutFinishesSetup()
        {
            var m = new MatchController();
            m.NewMatch(3, timer: true);
            m.AcknowledgeHandoff();
            m.Tick(1000f); // S-06: A's setup clock runs out
            Assert.That(m.HandoffPending, Is.True);
            Assert.That(m.Viewer, Is.EqualTo(PlayerId.B));
        }

        /// <summary>UX-09, V-10: no client type except MatchController touches GameState, and MatchController does not
        /// expose it. Views only see PlayerView, events and commands.</summary>
        [Test]
        public void M6_ViewsNeverSeeGameState()
        {
            var asm = typeof(MatchController).Assembly;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var hits = new List<string>();
            foreach (var t in asm.GetTypes())
            {
                bool controller = t == typeof(MatchController);
                foreach (var f in t.GetFields(all))
                    if (Uses(f.FieldType) && !(controller && f.IsPrivate)) hits.Add(t.Name + "." + f.Name);
                foreach (var p in t.GetProperties(all))
                    if (Uses(p.PropertyType)) hits.Add(t.Name + "." + p.Name);
                foreach (var mi in t.GetMethods(all))
                    if (Uses(mi.ReturnType) || mi.GetParameters().Any(x => Uses(x.ParameterType))) hits.Add(t.Name + "." + mi.Name + "()");
                foreach (var c in t.GetConstructors(all))
                    if (c.GetParameters().Any(x => Uses(x.ParameterType))) hits.Add(t.Name + ".ctor");
            }
            Assert.That(hits, Is.Empty);
        }

        static bool Uses(Type t)
        {
            if (t == null) return false;
            if (t == typeof(GameState)) return true;
            if (t.HasElementType && Uses(t.GetElementType())) return true;
            return t.IsGenericType && t.GetGenericArguments().Any(Uses);
        }
    }
}
