using System;
using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;

namespace HexPortal.Game
{
    public enum MatchMode { Hotseat, VsAi }

    /// <summary>
    /// The ONLY client class that holds the GameState. Views get the viewer's PlayerView, legal commands and filtered
    /// events, never the state; the AI gets PlayerView.For(aiSeat) and its legal list, never the state (AI-01).
    /// Hotseat (S-08): setup A → handoff → setup B → handoff → turns, a handoff between every turn.
    /// Vs AI: the human is always the viewer, no handoff; the AI sets up at once, acts one command per StepAi call
    /// (the caller paces it, AI-05) and pre-picks during the human's turn (AI-06).
    /// T-09 client clock: setup 45 s, turn 30 s + 60 s bank; it runs only while a human must act.
    /// </summary>
    public sealed class MatchController
    {
        GameState state;
        PlayerView view;
        List<ICommand> legal = new List<ICommand>();
        readonly float[] bank = new float[2];
        Rng aiRng;
        int aiPrePickTurn = -1;

        public MatchMode Mode { get; private set; }
        public PlayerId AiSeat { get; private set; }
        public AiLevel AiLevel { get; private set; }
        public bool TimerEnabled { get; private set; }
        public bool HasMatch => state != null;
        /// <summary>Who holds the device: hotseat = the player setting up / the active player; vs AI = the human.</summary>
        public PlayerId Viewer { get; private set; }
        public PlayerView View => view;
        public IReadOnlyList<ICommand> Legal => legal;
        /// <summary>S-08: the board stays hidden until the next player taps (hotseat only).</summary>
        public bool HandoffPending { get; private set; }
        public bool IsOver => state != null && state.IsOver;
        public float SetupSecondsLeft { get; private set; }
        public float TurnSecondsLeft { get; private set; }
        public float BankSecondsLeft(PlayerId p) => bank[(int)p];
        public string LastError { get; private set; }

        /// <summary>Events filtered for the viewer (EventFilter.For), after the view was refreshed.</summary>
        public event Action<IReadOnlyList<GameEvent>> EventsApplied;
        /// <summary>Viewer, handoff, phase or view changed.</summary>
        public event Action Changed;

        public void NewMatch(ulong seed, bool timer)
        {
            Start(seed, timer, MatchMode.Hotseat);
            Viewer = PlayerId.A;
            HandoffPending = true;
            Refresh();
            Changed?.Invoke();
        }

        public void NewAiMatch(ulong seed, PlayerId humanSeat, AiLevel level, bool timer)
        {
            Start(seed, timer, MatchMode.VsAi);
            Viewer = humanSeat;
            AiSeat = humanSeat.Opponent();
            AiLevel = level;
            aiRng = new Rng(seed * 0x9E3779B97F4A7C15UL + 0xA1);
            HandoffPending = false;
            while (state.Phase == GamePhase.Setup && !state.IsSetupFinished(AiSeat)) Act(AiChoice()); // instant AI setup
            Refresh();
            Changed?.Invoke();
        }

        void Start(ulong seed, bool timer, MatchMode mode)
        {
            state = Match.Create(seed);
            Mode = mode;
            TimerEnabled = timer;
            bank[0] = bank[1] = Catalog.TimeBankSeconds;
            aiPrePickTurn = -1;
            LastError = null;
            ResetClock();
        }

        public void AcknowledgeHandoff()
        {
            if (!HandoffPending) return;
            HandoffPending = false;
            ResetClock();
            Changed?.Invoke();
        }

        /// <summary>True while it is the AI's turn and the game is running (call StepAi).</summary>
        public bool AiToAct => Mode == MatchMode.VsAi && state != null && !state.IsOver
                               && state.Phase == GamePhase.Playing && state.ActivePlayer == AiSeat;

        /// <summary>One AI command (AI-05: the caller paces calls). Returns false when the AI has nothing to do.</summary>
        public bool StepAi()
        {
            if (!AiToAct) return false;
            return Act(AiChoice());
        }

        /// <summary>The AI decides from its PlayerView and legal list only (AI-01).</summary>
        ICommand AiChoice()
        {
            var aiLegal = Engine.GetLegalCommands(state, AiSeat);
            return aiLegal.Count == 0 ? null : GreedyAi.Choose(PlayerView.For(state, AiSeat), aiLegal, AiLevel, aiRng);
        }

        /// <summary>Applies a command for the human viewer. Illegal commands are logged and change nothing.</summary>
        public bool Apply(ICommand cmd)
        {
            if (state == null || HandoffPending || cmd == null) return false;
            if (Mode == MatchMode.VsAi && cmd.Player != Viewer) return false;
            return Act(cmd);
        }

        bool Act(ICommand cmd)
        {
            if (cmd == null) return false;
            List<GameEvent> all;
            try
            {
                all = Engine.Apply(state, cmd);
            }
            catch (IllegalCommandException e)
            {
                LastError = e.Message;
                Debug.LogWarning("[MatchController] Illegal command: " + e.Message);
                return false;
            }
            var mine = EventFilter.For(all, Viewer);
            int turnBefore = view == null ? -1 : view.TurnIndex;
            if (Mode == MatchMode.Hotseat)
            {
                var next = NextViewer();
                bool handoff = !state.IsOver && next != Viewer;
                Refresh();
                EventsApplied?.Invoke(mine);
                if (handoff)
                {
                    Viewer = next;
                    HandoffPending = true;
                    Refresh();
                }
            }
            else
            {
                foreach (var e in all)
                    if (e is MarketRefilled) aiPrePickTurn = -1; // the Market changed: the AI re-evaluates its pre-pick
                mine.AddRange(AiPrePick());
                Refresh();
                if (view.TurnIndex != turnBefore) ResetClock();
                EventsApplied?.Invoke(mine);
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>AI-06: once per human turn, the waiting AI sets its pre-pick (T-11).</summary>
        List<GameEvent> AiPrePick()
        {
            var none = new List<GameEvent>();
            if (state.IsOver || state.Phase != GamePhase.Playing || state.ActivePlayer == AiSeat || aiPrePickTurn == state.TurnIndex) return none;
            aiPrePickTurn = state.TurnIndex;
            var aiLegal = Engine.GetLegalCommands(state, AiSeat);
            if (aiLegal.Count == 0) return none;
            var c = GreedyAi.Choose(PlayerView.For(state, AiSeat), aiLegal, AiLevel, aiRng);
            try { return EventFilter.For(Engine.Apply(state, c), Viewer); } // PrePickSet is owner-only: normally empty
            catch (IllegalCommandException e) { Debug.LogWarning("[MatchController] AI pre-pick rejected: " + e.Message); return none; }
        }

        /// <summary>T-09 / S-06 clock. Call once per frame with the frame time.</summary>
        public void Tick(float dt)
        {
            if (!TimerEnabled || state == null || HandoffPending || state.IsOver) return;
            if (state.Phase == GamePhase.Setup)
            {
                if (state.IsSetupFinished(Viewer)) return;
                SetupSecondsLeft = Mathf.Max(0f, SetupSecondsLeft - dt);
                if (SetupSecondsLeft <= 0f) Act(new SetupTimeoutCommand(Viewer));
                return;
            }
            if (state.ActivePlayer != Viewer) return; // the AI has no clock
            if (TurnSecondsLeft > 0f) TurnSecondsLeft = Mathf.Max(0f, TurnSecondsLeft - dt);
            else bank[(int)Viewer] = Mathf.Max(0f, bank[(int)Viewer] - dt);
            if (TurnSecondsLeft <= 0f && bank[(int)Viewer] <= 0f) Act(new TurnTimeoutCommand(Viewer));
        }

        PlayerId NextViewer()
        {
            if (state.Phase == GamePhase.Setup) return state.IsSetupFinished(PlayerId.A) ? PlayerId.B : PlayerId.A;
            return state.ActivePlayer;
        }

        void ResetClock()
        {
            SetupSecondsLeft = Catalog.PlacementSeconds;
            TurnSecondsLeft = Catalog.TurnSeconds;
        }

        void Refresh()
        {
            view = PlayerView.For(state, Viewer);
            legal = Engine.GetLegalCommands(state, Viewer);
        }
    }
}
