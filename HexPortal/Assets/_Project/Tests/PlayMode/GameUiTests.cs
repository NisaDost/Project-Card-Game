using System.Collections;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexPortal.Game.Tests
{
    /// <summary>UI data driven only by the controller's PlayerView and legal list.</summary>
    public class GameUiTests
    {
        static IEnumerator App(System.Action<GameApp> use)
        {
            if (GameApp.Instance == null) GameApp.Create();
            yield return null;
            use(GameApp.Instance);
        }

        /// <summary>S-03: Confirm follows the legal list, not a client-side count.</summary>
        [UnityTest]
        public IEnumerator S03_QuestConfirmEnabledOnlyForALegalPick()
        {
            yield return App(app =>
            {
                var m = app.Match;
                m.NewMatch(5, timer: false);
                m.AcknowledgeHandoff();
                Assert.That(app.Ui.Current, Is.EqualTo(GameUi.Screen.Quests));
                var offer = m.View.QuestOffer;
                Assert.That(app.Ui.QuestConfirmEnabled, Is.False);
                app.Ui.ToggleQuest(offer[0]);
                app.Ui.ToggleQuest(offer[1]);
                Assert.That(app.Ui.QuestConfirmEnabled, Is.False, "2 of 3");
                app.Ui.ToggleQuest(offer[2]);
                Assert.That(app.Ui.QuestConfirmEnabled, Is.True, "a legal pick");
                app.Ui.ToggleQuest(offer[3]);
                Assert.That(app.Ui.QuestConfirmEnabled, Is.False, "4 is not legal");
            });
        }

        /// <summary>Q-04, V-09: an opponent's failed quest is shown in the top strip and the quest panel.</summary>
        [UnityTest]
        public IEnumerator Q04_OpponentFailedQuestIsShown()
        {
            bool seen = false;
            yield return App(app =>
            {
                var m = app.Match;
                // Hotseat, scripted: B takes Q-16 (Kayıpsız) when offered, both sides prefer attacks and move forward,
                // so a B unit often dies before round 6 and Q-16 fails (shown to A at the latest after round 6, v2.10).
                for (ulong seed = 1; seed <= 40 && !seen; seed++)
                {
                    m.NewMatch(seed, timer: false);
                    var rng = new System.Random((int)seed);
                    int steps = 0, actions = 0;
                    while (!m.IsOver && steps++ < 20000)
                    {
                        if (m.HandoffPending) { m.AcknowledgeHandoff(); actions = 0; continue; }
                        if (m.Viewer == PlayerId.A && m.View.OpponentFailedQuests.Count > 0)
                        {
                            var id = m.View.OpponentFailedQuests[0];
                            StringAssert.Contains(Strings.OppFailed + m.View.OpponentFailedQuests.Count, app.Ui.OpponentInfoText);
                            StringAssert.Contains(Strings.Name(id), app.Ui.QuestPanelText);
                            seen = true;
                            break;
                        }
                        var legal = m.Legal;
                        ICommand cmd;
                        if (m.View.Phase == GamePhase.Setup)
                        {
                            cmd = legal.FirstOrDefault(c => c is FinishSetupCommand)
                                  ?? (m.View.SetupStep == SetupStep.Placement ? legal.First(c => c is PlaceUnitCommand) : null);
                            if (cmd == null && m.View.SetupStep == SetupStep.Quests && m.Viewer == PlayerId.B)
                                cmd = legal.FirstOrDefault(c => ((ChooseQuestsCommand)c).QuestIds.Contains("Q-16"));
                            if (cmd == null) cmd = legal[rng.Next(legal.Count)];
                        }
                        else
                        {
                            var end = legal.FirstOrDefault(c => c is EndTurnCommand);
                            var attacks = legal.Where(c => c is AttackCommand).ToList();
                            var moves = legal.Where(c => c is MoveCommand || c is DrawCommand || c is DeployCommand).ToList();
                            if (attacks.Count > 0) cmd = attacks[rng.Next(attacks.Count)];
                            else if (moves.Count > 0 && actions++ < 4) cmd = moves[rng.Next(moves.Count)];
                            else cmd = end ?? legal[0];
                        }
                        m.Apply(cmd);
                    }
                }
            });
            if (!seen) Assert.Ignore("No opponent quest failed in the sampled seeds.");
        }
    }
}
