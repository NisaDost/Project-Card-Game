using System;
using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>
    /// The ONLY client class that holds the GameState. Views get the current viewer's PlayerView, legal commands and
    /// filtered events, never the state. Hotseat flow (S-08): setup A → handoff → setup B → handoff → turns, with a
    /// handoff between every turn. T-09 client clock: setup 45 s, turn 30 s + 60 s bank, paused while handing off.
    /// </summary>
    public sealed class MatchController
    {
        GameState state;
        PlayerView view;
        List<ICommand> legal = new List<ICommand>();
        readonly float[] bank = new float[2];

        public bool TimerEnabled { get; private set; }
        public bool HasMatch => state != null;
        /// <summary>The player holding the device (setup: who is setting up; play: the active player).</summary>
        public PlayerId Viewer { get; private set; }
        public PlayerView View => view;
        public IReadOnlyList<ICommand> Legal => legal;
        /// <summary>S-08: the board stays hidden until the next player taps.</summary>
        public bool HandoffPending { get; private set; }
        public bool IsOver => state != null && state.IsOver;
        public float SetupSecondsLeft { get; private set; }
        public float TurnSecondsLeft { get; private set; }
        public float BankSecondsLeft(PlayerId p) => bank[(int)p];
        public string LastError { get; private set; }

        /// <summary>Filtered events (EventFilter.For the viewer who issued the command), after the view was refreshed.</summary>
        public event Action<IReadOnlyList<GameEvent>> EventsApplied;
        /// <summary>Viewer, handoff, phase or view changed.</summary>
        public event Action Changed;

        public void NewMatch(ulong seed, bool timer)
        {
            state = Match.Create(seed);
            TimerEnabled = timer;
            bank[0] = bank[1] = Catalog.TimeBankSeconds;
            Viewer = PlayerId.A;
            HandoffPending = true;
            ResetClock();
            Refresh();
            Changed?.Invoke();
        }

        public void AcknowledgeHandoff()
        {
            if (!HandoffPending) return;
            HandoffPending = false;
            ResetClock();
            Changed?.Invoke();
        }

        /// <summary>Applies a command for the viewer. Illegal commands are logged and change nothing.</summary>
        public bool Apply(ICommand cmd)
        {
            if (state == null || HandoffPending) return false;
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
            Changed?.Invoke();
            return true;
        }

        /// <summary>T-09 / S-06 clock. Call once per frame with the frame time.</summary>
        public void Tick(float dt)
        {
            if (!TimerEnabled || state == null || HandoffPending || state.IsOver) return;
            if ((state.Phase == GamePhase.Setup))
            {
                SetupSecondsLeft = Mathf.Max(0f, SetupSecondsLeft - dt);
                if (SetupSecondsLeft <= 0f) Apply(new SetupTimeoutCommand(Viewer));
                return;
            }
            if (TurnSecondsLeft > 0f) TurnSecondsLeft = Mathf.Max(0f, TurnSecondsLeft - dt);
            else bank[(int)Viewer] = Mathf.Max(0f, bank[(int)Viewer] - dt);
            if (TurnSecondsLeft <= 0f && bank[(int)Viewer] <= 0f) Apply(new TurnTimeoutCommand(Viewer));
        }

        PlayerId NextViewer()
        {
            if ((state.Phase == GamePhase.Setup)) return state.IsSetupFinished(PlayerId.A) ? PlayerId.B : PlayerId.A;
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
