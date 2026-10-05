using System.Collections;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>M7b: event-driven tweens are purely visual, skippable and pooled.</summary>
    public class M7AnimationTests
    {
        static int ObjectCount() =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        /// <summary>A full AI-vs-scripted match with frames between commands (tweens run and get skipped mid-way)
        /// completes with no errors, no extra GameObjects beyond the pools, and bounded pools.</summary>
        [UnityTest]
        public IEnumerator M7_AiMatchWithAnimationsCompletesBounded()
        {
            if (GameApp.Instance == null) GameApp.Create();
            var app = GameApp.Instance;
            yield return null;
            var m = app.Match;
            var units = app.Units;
            int errorsBefore = app.LoggedErrors;
            int fixedObjects = ObjectCount() - units.PoolSize * UnitsView.ObjectsPerPiece - units.BurstPoolSize;
            m.NewAiMatch(123, PlayerId.A, AiLevel.Normal, timer: false);
            var rng = new System.Random(123);
            int steps = 0, maxShown = 0;
            while (!m.IsOver && steps++ < 50000)
            {
                if (m.AiToAct) m.StepAi();
                else
                {
                    var legal = m.Legal;
                    var cmd = legal.FirstOrDefault(c => c is FinishSetupCommand) ?? legal.FirstOrDefault(c => c is DrawCommand)
                              ?? legal.FirstOrDefault(c => c is AttackCommand || c is MoveAttackCommand)
                              ?? (rng.Next(4) == 0 ? legal.FirstOrDefault(c => c is EndTurnCommand) : null)
                              ?? legal[rng.Next(legal.Count)];
                    Assert.That(m.Apply(cmd), Is.True);
                }
                maxShown = Mathf.Max(maxShown, units.ShownCount);
                if (steps % 3 == 0) yield return null; // some tweens advance, others are skipped by the next batch
                Assert.That(ObjectCount(), Is.EqualTo(fixedObjects + units.PoolSize * UnitsView.ObjectsPerPiece + units.BurstPoolSize));
            }
            Assert.That(m.IsOver, Is.True);
            yield return new WaitForSeconds(0.6f); // every tween ends on its own
            Assert.That(units.PoolSize, Is.LessThanOrEqualTo(maxShown + 4));
            Assert.That(units.FxPoolSize, Is.LessThanOrEqualTo(12));
            Assert.That(app.LoggedErrors, Is.EqualTo(errorsBefore));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
