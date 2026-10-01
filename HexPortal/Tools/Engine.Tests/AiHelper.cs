using System;
using System.Collections.Generic;
using HexPortal.Core;
using NUnit.Framework;

namespace HexPortal.Tests
{
    /// <summary>One AI decision as the driver made it: the real state before the command, the deciding player, the AI
    /// level, a copy of that player's AI Rng before the decision, the legal list and the command chosen.</summary>
    public sealed class AiDecision
    {
        public GameState Before;
        public PlayerId Player;
        public AiLevel Level;
        public Rng RngBefore;
        public List<ICommand> Legal;
        public ICommand Chosen;
    }

    /// <summary>Drives AI-vs-AI matches the way a client would: setup for both players, then the active player's
    /// decisions and, once per opponent turn, the waiting player's pre-pick (AI-06). The AI gets only
    /// PlayerView.For(state, p) and Engine.GetLegalCommands(state, p) (AI-01).</summary>
    public static class AiHelper
    {
        public const int MaxCommands = 4000;

        public static Rng AiRng(ulong seed, PlayerId p) => new Rng(seed * 6364136223846793005UL + (ulong)p + 1);

        /// <summary>Plays one match to its end. <paramref name="onDecision"/> runs before each chosen command is applied.</summary>
        public static GameState Play(ulong seed, AiLevel levelA, AiLevel levelB, Action<AiDecision> onDecision = null,
            List<ICommand> log = null)
        {
            var s = Match.Create(seed);
            var rngs = new[] { AiRng(seed, PlayerId.A), AiRng(seed, PlayerId.B) };
            var levels = new[] { levelA, levelB };
            int count = 0;
            int prePickedTurn = -1;

            void Decide(PlayerId p)
            {
                var legal = Engine.GetLegalCommands(s, p);
                var rng = rngs[(int)p];
                var d = new AiDecision
                {
                    Before = s.Clone(), Player = p, Level = levels[(int)p], RngBefore = rng.Clone(), Legal = legal,
                };
                d.Chosen = GreedyAi.Choose(PlayerView.For(s, p), legal, levels[(int)p], rng);
                Assert.That(legal, Does.Contain(d.Chosen), "seed " + seed + ": the AI chose a command outside the legal list");
                onDecision?.Invoke(d);
                Engine.Apply(s, d.Chosen);
                log?.Add(d.Chosen);
                count++;
            }

            while (s.Phase == GamePhase.Setup && count < MaxCommands)
                foreach (var p in MatchHelper.Players)
                    if (s.Phase == GamePhase.Setup && !s.IsSetupFinished(p)) Decide(p);
            while (!s.IsOver && count < MaxCommands)
            {
                if (prePickedTurn != s.TurnIndex) // AI-06: once per opponent turn, at its start
                {
                    prePickedTurn = s.TurnIndex;
                    if (Engine.GetLegalCommands(s, s.ActivePlayer.Opponent()).Count > 0) Decide(s.ActivePlayer.Opponent());
                }
                Decide(s.ActivePlayer);
            }
            Assert.That(s.IsOver, Is.True, "seed " + seed + " did not end in " + MaxCommands + " commands");
            return s;
        }

        public static string ResultKey(GameResult r) =>
            r.Reason == WinReason.RoundLimit ? "W-03 criterion " + r.Criterion
            : r.Reason == WinReason.Portal ? "W-01" : r.Reason == WinReason.Tower ? "W-02" : "W-04";
    }
}
