using System;
using System.Collections.Generic;
using HexPortal.Core;
using UnityEngine.UIElements;

namespace HexPortal.Game
{
    /// <summary>
    /// UX-08 tutorial: a fixed-seed match against a passive AI (it only draws and ends its turns) with 4 hint steps:
    /// Energy (move / attack / move-attack), Mana + Control Zone (play a card), fog (explore), quests + Portal.
    /// A step advances when the player does the action, seen through filtered events or the PlayerView only.
    /// Client-only: no rules here; the match itself is a normal vs-AI match.
    /// </summary>
    public sealed class Tutorial
    {
        public const ulong Seed = 20261005UL;
        public const int Steps = 4;

        readonly MatchController match;
        readonly VisualElement root, bubble;
        readonly Label text;
        readonly Button skip;
        readonly Action exitToMenu;
        readonly List<VisualElement> glowing = new List<VisualElement>();
        int exploredAtStep, matchNumber;

        /// <summary>-1 = not running, 0..3 = step, 4 = finished (bubble shows the end text).</summary>
        public int Step { get; private set; } = -1;
        public bool Active => Step >= 0;
        public string Text => text.text;

        public Tutorial(MatchController match, VisualElement root, Action exitToMenu)
        {
            this.match = match;
            this.root = root;
            this.exitToMenu = exitToMenu;
            bubble = new VisualElement { name = "tutorial-bubble", pickingMode = PickingMode.Ignore };
            bubble.AddToClassList("tutorial-bubble");
            text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("tutorial-text");
            skip = new Button(Advance) { name = "tutorial-skip", text = Strings.TutorialSkip };
            skip.AddToClassList("btn"); skip.AddToClassList("tutorial-btn");
            var exit = new Button(Exit) { name = "tutorial-exit", text = Strings.TutorialExit };
            exit.AddToClassList("btn"); exit.AddToClassList("tutorial-btn");
            bubble.Add(text); bubble.Add(skip); bubble.Add(exit);
            bubble.style.display = DisplayStyle.None;
            root.Q("hud").Add(bubble); // lives in the HUD: hidden with it on other screens
            match.EventsApplied += OnEvents;
            match.Changed += OnChanged;
            root.Q<Button>("quest-btn").clicked += () => { if (Step == 3) Advance(); };
        }

        /// <summary>Starts the tutorial match: fixed seed, human A, passive AI, no clock; the human setup is done
        /// automatically (quests, passive, tower, a few units) so the steps start on the first turn.</summary>
        public void Start()
        {
            match.NewAiMatch(Seed, PlayerId.A, AiLevel.Easy, timer: false, passiveAi: true);
            matchNumber = match.MatchNumber;
            AutoSetup();
            Step = 0;
            Show();
        }

        void AutoSetup()
        {
            int guard = 0;
            while (match.HasMatch && match.View.Phase == GamePhase.Setup && guard++ < 200)
            {
                var legal = match.Legal;
                if (legal.Count == 0) break;
                ICommand pick = null, finish = null, place = null, tower = null;
                foreach (var c in legal)
                {
                    if (c is FinishSetupCommand) finish = c;
                    else if (c is PlaceUnitCommand && place == null) place = c;
                    else if (c is PlaceTowerCommand && tower == null) tower = c;
                }
                if (tower != null) pick = tower;
                else if (place != null && match.View.OwnUnits.Count < 3) pick = place;
                else if (finish != null) pick = finish;
                else pick = legal[0];
                if (!match.Apply(pick)) break;
            }
        }

        public void Stop()
        {
            if (!Active) return;
            Step = -1;
            bubble.style.display = DisplayStyle.None;
            Glow();
        }

        void Exit()
        {
            Stop();
            exitToMenu?.Invoke();
        }

        /// <summary>"Atla" and every completed action: next step.</summary>
        public void Advance()
        {
            if (!Active || Step >= Steps) return;
            Step++;
            Show();
        }

        void Show()
        {
            bubble.style.display = DisplayStyle.Flex;
            skip.style.display = Step < Steps ? DisplayStyle.Flex : DisplayStyle.None;
            switch (Step)
            {
                case 0: text.text = Strings.TutorialStep1; Glow(root.Q(className: "energy-meter")); break;
                case 1: text.text = Strings.TutorialStep2; Glow(root.Q(className: "mana-meter"), root.Q("hand")); break;
                case 2: text.text = Strings.TutorialStep3; exploredAtStep = Explored(); Glow(root.Q("end-btn")); break;
                case 3: text.text = Strings.TutorialStep4; Glow(root.Q("quest-btn")); break;
                default: text.text = Strings.TutorialDone; Glow(); break;
            }
        }

        void Glow(params VisualElement[] els)
        {
            foreach (var e in glowing) SetGlow(e, false);
            glowing.Clear();
            foreach (var e in els)
                if (e != null) { SetGlow(e, true); glowing.Add(e); }
        }

        /// <summary>Inline border so it wins over the element's own USS border.</summary>
        static void SetGlow(VisualElement e, bool on)
        {
            var s = e.style;
            if (on)
            {
                var c = new UnityEngine.Color(1f, 0.86f, 0.35f);
                s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = c;
                s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 10f;
            }
            else
            {
                s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = StyleKeyword.Null;
                s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = StyleKeyword.Null;
            }
        }

        int Explored()
        {
            int n = 0;
            foreach (var c in match.View.Cells)
                if (c.Visibility != CellVisibility.Hidden) n++;
            return n;
        }

        void OnEvents(IReadOnlyList<GameEvent> events)
        {
            if (!Active || !match.HasMatch) return;
            var v = match.View;
            foreach (var e in events)
            {
                bool done = false;
                switch (Step)
                {
                    case 0:
                        if (e is UnitMoved m) done = IsOwn(v, m.UnitId);
                        else if (e is DamageDealt d) done = d.SourcePlayer == v.Viewer && d.Kind == DamageKind.Attack;
                        break;
                    case 1:
                        if (e is CardPlayed cp) done = cp.Player == v.Viewer;
                        else if (e is UnitDeployed ud) done = IsOwn(v, ud.UnitId);
                        else if (e is TrapPlaced tp) done = tp.Owner == v.Viewer;
                        break;
                }
                if (done) { Advance(); return; }
            }
        }

        void OnChanged()
        {
            if (!Active) return;
            if (!match.HasMatch || match.IsOver || match.MatchNumber != matchNumber) { Stop(); return; }
            if (Step == 2 && Explored() > exploredAtStep) Advance();
        }

        static bool IsOwn(PlayerView v, int unitId)
        {
            foreach (var u in v.OwnUnits)
                if (u.Id == unitId) return true;
            return false;
        }
    }
}
