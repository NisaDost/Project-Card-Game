using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace HexPortal.Game
{
    /// <summary>
    /// Screens, HUD (UX-04) and touch interaction (UX-02, UX-03). Reads only the controller's PlayerView, legal
    /// commands and filtered events; every action is a command from the legal list sent through the controller.
    /// Pointer input comes from UI Toolkit, which reads the Input System (touch and mouse); two-finger pinch also
    /// reads EnhancedTouch directly. Buttons are never hard-disabled: an "off" button explains on tap why it is off.
    /// </summary>
    public sealed class GameUi
    {
        public enum Screen { Menu, Quests, Passive, Handoff, Board, Result }

        const float LongPressMs = 500f;
        const float DragThreshold = 30f;   // panel units (1080-high reference)
        const float DoubleTapSeconds = 0.32f;
        const float DoubleTapDistance = 90f;
        const float ToastSeconds = 1.8f;
        const float NoticeSeconds = 1.8f;
        const int LogLines = 4;
        const float HandWidth = 1170f;     // .hand width at the 1920 reference (left 330, right 420)
        const float CardStride = 196f;     // card width + margins

        readonly MatchController match;
        readonly VisualElement root;
        readonly BoardView board;
        readonly UnitsView units;
        readonly Camera cam;

        readonly VisualElement screenMenu, screenQuests, screenPassive, screenHandoff, screenResult, hud, questPanel, cardDetail, dragCard, confirmPanel;
        readonly VisualElement hand, questOptions, passiveOptions, manaPips, energyPips, oppInfo;
        readonly Label timerBank, ownTower, roundLabel, oppTower, oppHand, oppQuests, turnBanner, eventBanner, manaText, energyText,
            timer, prompt, questText, detailText, confirmText, toast, notice, eventLog;
        readonly Button[] market = new Button[3];
        readonly Button blindBtn, endBtn, overwatchBtn, questsConfirm, menuTimer;
        readonly List<string> pickedQuests = new List<string>();
        readonly Dictionary<VisualElement, string> offReason = new Dictionary<VisualElement, string>();
        readonly List<string> log = new List<string>();
        readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

        bool timerOn = true;
        bool aiTimerOn;
        PlayerId humanSeat = PlayerId.A;
        int selectedUnit = -1;
        int selectedCard = -1;
        readonly List<ICommand> secondPick = new List<ICommand>();
        PlayerId? facing;
        int shownMatch = -1;
        int shownTurn = -1, shownBank = -1;
        System.Action confirmAction;
        float toastUntil, noticeUntil;

        // Card pointer state.
        int pressCard = -1;
        Vector2 pressPos;
        bool dragging, longPressed;
        IVisualElementScheduledItem longPressJob;

        // Board pointer state (tap / pan / pinch / double tap).
        int boardPointer = -1;
        Vector2 boardPress, boardLast;
        bool boardDragged, boardRightDrag;
        int boardPointers;
        bool pinchActive;
        float pinchSpan;
        Vector2 pinchMid;
        float lastTapTime = -10f;
        Vector2 lastTapPos;

        public Screen Current { get; private set; } = Screen.Menu;
        public int SelectedUnit => selectedUnit;
        public int SelectedCard => selectedCard;
        public int HighlightCount => board.HighlightCount;
        public string LastTap { get; private set; } = "";
        public VisualElement Root => root;
        /// <summary>Last reason shown for an off control (tests/CLI).</summary>
        public string LastReason { get; private set; } = "";
        public string ConfirmText => confirmAction != null ? confirmText.text : null;
        public string EventLogText => eventLog.text;
        public string LastNotice { get; private set; } = "";

        public GameUi(MatchController match, VisualElement root, BoardView board, UnitsView units, Camera cam)
        {
            this.match = match; this.root = root; this.board = board; this.units = units; this.cam = cam;
            screenMenu = root.Q("screen-menu"); screenQuests = root.Q("screen-quests"); screenPassive = root.Q("screen-passive");
            screenHandoff = root.Q("screen-handoff"); screenResult = root.Q("screen-result"); hud = root.Q("hud");
            questPanel = root.Q("quest-panel"); cardDetail = root.Q("card-detail"); dragCard = root.Q("drag-card"); confirmPanel = root.Q("confirm-panel");
            hand = root.Q("hand"); questOptions = root.Q("quest-options"); passiveOptions = root.Q("passive-options");
            manaPips = root.Q("mana-pips"); energyPips = root.Q("energy-pips"); oppInfo = root.Q("opp-info");
            ownTower = root.Q<Label>("own-tower"); roundLabel = root.Q<Label>("round");
            oppTower = root.Q<Label>("opp-tower"); oppHand = root.Q<Label>("opp-hand"); oppQuests = root.Q<Label>("opp-quests");
            turnBanner = root.Q<Label>("turn-banner");
            eventBanner = root.Q<Label>("event-banner"); manaText = root.Q<Label>("mana-text"); energyText = root.Q<Label>("energy-text");
            timer = root.Q<Label>("timer"); timerBank = root.Q<Label>("timer-bank"); prompt = root.Q<Label>("prompt"); questText = root.Q<Label>("quest-text");
            detailText = root.Q<Label>("detail-text"); confirmText = root.Q<Label>("confirm-text");
            toast = root.Q<Label>("toast"); notice = root.Q<Label>("notice"); eventLog = root.Q<Label>("event-log");
            root.Q<Label>("mana-icon").text = Strings.ManaIcon;
            root.Q<Label>("energy-icon").text = Strings.EnergyIcon;
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                market[i] = root.Q<Button>("market-" + i);
                Guard(market[i], () => OnDrawSlot(slot));
            }
            blindBtn = root.Q<Button>("blind-btn"); blindBtn.text = Strings.Blind; Guard(blindBtn, () => OnDrawSlot(DrawCommand.Blind));
            endBtn = root.Q<Button>("end-btn"); Guard(endBtn, OnEnd);
            overwatchBtn = root.Q<Button>("overwatch-btn"); overwatchBtn.text = Strings.OverwatchIcon + " " + Strings.Overwatch; Guard(overwatchBtn, OnOverwatch);
            questsConfirm = root.Q<Button>("quests-confirm"); questsConfirm.text = Strings.Confirm; Guard(questsConfirm, OnConfirmQuests);
            var questBtn = root.Q<Button>("quest-btn"); questBtn.text = Strings.Quests; questBtn.clicked += () => questPanel.style.display = DisplayStyle.Flex;
            oppInfo.RegisterCallback<PointerUpEvent>(_ => questPanel.style.display = DisplayStyle.Flex); // details live in the panel
            var qc = root.Q<Button>("quest-close"); qc.text = Strings.Close; qc.clicked += () => questPanel.style.display = DisplayStyle.None;
            var dc = root.Q<Button>("detail-close"); dc.text = Strings.Close; dc.clicked += () => cardDetail.style.display = DisplayStyle.None;
            var cc = root.Q<Button>("confirm-cancel"); cc.text = Strings.Cancel; cc.clicked += CancelConfirm;
            var ok = root.Q<Button>("confirm-ok"); ok.text = Strings.Confirm; ok.clicked += AcceptConfirm;

            root.Q<Label>("menu-title").text = Strings.Title;
            var hot = root.Q<Button>("menu-hotseat"); hot.text = Strings.Hotseat; hot.clicked += StartHotseat;
            menuTimer = root.Q<Button>("menu-timer"); menuTimer.text = Strings.TimerOn;
            menuTimer.clicked += () => { timerOn = !timerOn; menuTimer.text = timerOn ? Strings.TimerOn : Strings.TimerOff; };
            var easy = root.Q<Button>("menu-ai-easy"); easy.text = Strings.AiEasy; easy.clicked += () => StartAi(AiLevel.Easy);
            var normal = root.Q<Button>("menu-ai-normal"); normal.text = Strings.AiNormal; normal.clicked += () => StartAi(AiLevel.Normal);
            var seat = root.Q<Button>("menu-seat"); seat.text = Strings.SeatA;
            seat.clicked += () => { humanSeat = humanSeat.Opponent(); seat.text = humanSeat == PlayerId.A ? Strings.SeatA : Strings.SeatB; };
            var aiTimer = root.Q<Button>("menu-ai-timer"); aiTimer.text = Strings.AiTimerOff; // T-09: off by default vs AI
            aiTimer.clicked += () => { aiTimerOn = !aiTimerOn; aiTimer.text = aiTimerOn ? Strings.AiTimerOn : Strings.AiTimerOff; };
            root.Q<Label>("quests-title").text = Strings.ChooseQuests;
            root.Q<Label>("passive-title").text = Strings.ChoosePassive;
            root.Q<Label>("handoff-title").text = Strings.HandoffTitle;
            root.Q<Label>("handoff-tap").text = Strings.HandoffTap;
            var rm = root.Q<Button>("result-menu"); rm.text = Strings.NewMatch; rm.clicked += () => Show(Screen.Menu);
            screenHandoff.RegisterCallback<PointerUpEvent>(_ => match.AcknowledgeHandoff());

            var hit = root.Q("board-hit");
            hit.RegisterCallback<PointerDownEvent>(e => OnBoardDown(hit, e));
            hit.RegisterCallback<PointerMoveEvent>(OnBoardMove);
            hit.RegisterCallback<PointerUpEvent>(e => OnBoardUp(hit, e));
            hit.RegisterCallback<PointerCancelEvent>(e => { if (hit.HasPointerCapture(e.pointerId)) hit.ReleasePointer(e.pointerId); boardPointers = 0; boardPointer = -1; });
            hit.RegisterCallback<WheelEvent>(OnWheel);
            hud.RegisterCallback<PointerMoveEvent>(OnCardMove, TrickleDown.TrickleDown);
            hud.RegisterCallback<PointerUpEvent>(OnCardUp, TrickleDown.TrickleDown);

            match.Changed += Refresh;
            match.EventsApplied += OnEvents;
            Show(Screen.Menu);
        }

        void StartHotseat()
        {
            ulong seed = (ulong)System.Environment.TickCount;
            facing = null;
            ClearLog();
            match.NewMatch(seed, timerOn);
        }

        void StartAi(AiLevel level)
        {
            ulong seed = (ulong)System.Environment.TickCount;
            facing = null;
            ClearLog();
            match.NewAiMatch(seed, humanSeat, level, aiTimerOn);
        }

        // ---------- On / off controls ----------

        /// <summary>Wires a button so that an "off" press shows its reason instead of acting (UX: clear states).</summary>
        void Guard(Button b, System.Action act)
        {
            b.clicked += () =>
            {
                if (offReason.TryGetValue(b, out var why) && why != null) ShowToast(why);
                else act();
            };
        }

        void SetOn(VisualElement el, bool on, string reason)
        {
            offReason[el] = on ? null : (reason ?? Strings.ReasonNotNow);
            el.EnableInClassList("off", !on);
        }

        public bool IsOn(VisualElement el) => !offReason.TryGetValue(el, out var why) || why == null;

        public string ReasonFor(VisualElement el) => offReason.TryGetValue(el, out var why) && why != null ? why : "";

        void ShowToast(string text)
        {
            LastReason = text;
            toast.text = text;
            toast.style.display = DisplayStyle.Flex;
            toastUntil = Time.unscaledTime + ToastSeconds;
        }

        void ShowNotice(string text, bool bad)
        {
            LastNotice = text;
            notice.text = text;
            notice.EnableInClassList("bad", bad);
            notice.EnableInClassList("good", !bad);
            notice.style.display = DisplayStyle.Flex;
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        // ---------- Screens ----------

        void Show(Screen s)
        {
            Current = s;
            screenMenu.style.display = s == Screen.Menu ? DisplayStyle.Flex : DisplayStyle.None;
            screenQuests.style.display = s == Screen.Quests ? DisplayStyle.Flex : DisplayStyle.None;
            screenPassive.style.display = s == Screen.Passive ? DisplayStyle.Flex : DisplayStyle.None;
            screenHandoff.style.display = s == Screen.Handoff ? DisplayStyle.Flex : DisplayStyle.None;
            screenResult.style.display = s == Screen.Result ? DisplayStyle.Flex : DisplayStyle.None;
            bool boardOn = s == Screen.Board || s == Screen.Quests || s == Screen.Passive;
            hud.style.display = s == Screen.Board ? DisplayStyle.Flex : DisplayStyle.None;
            board.gameObject.SetActive(boardOn); // S-08: no board behind the handoff screen
            units.gameObject.SetActive(boardOn);
            if (!boardOn) units.Hide();
            if (s != Screen.Board)
            {
                questPanel.style.display = DisplayStyle.None;
                cardDetail.style.display = DisplayStyle.None;
                CancelConfirm();
                notice.style.display = DisplayStyle.None;
                toast.style.display = DisplayStyle.None;
            }
        }

        void Refresh()
        {
            if (!match.HasMatch) { Show(Screen.Menu); return; }
            var v = match.View;
            ClearSelection();
            if (match.IsOver)
            {
                var r = v.Result;
                root.Q<Label>("result-title").text = r.IsDraw ? Strings.Draw : Strings.Player(r.Winner.Value) + Strings.Wins;
                root.Q<Label>("result-reason").text = Strings.Reason(r);
                Show(Screen.Result);
                return;
            }
            if (match.HandoffPending)
            {
                root.Q<Label>("handoff-player").text = Strings.Player(match.Viewer);
                Show(Screen.Handoff);
                return;
            }
            if (shownMatch != match.MatchNumber) { shownMatch = match.MatchNumber; facing = null; ClearLog(); }
            if (facing != v.Viewer) { CameraRig.Face(cam, v.Viewer); facing = v.Viewer; }
            board.Render(v);
            units.Render(v);
            if (v.Phase == GamePhase.Setup && v.SetupStep == SetupStep.Quests) { BuildQuestOptions(v); Show(Screen.Quests); return; }
            if (v.Phase == GamePhase.Setup && v.SetupStep == SetupStep.Passive) { BuildPassiveOptions(v); Show(Screen.Passive); return; }
            Show(Screen.Board);
            RefreshHud(v);
            ShowDefaultHighlights();
        }

        void BuildQuestOptions(PlayerView v)
        {
            pickedQuests.Clear();
            questOptions.Clear();
            foreach (var id in v.QuestOffer)
            {
                var qid = id;
                var b = new Button { text = Strings.Name(qid) + "\n" + Strings.Text(qid), userData = qid };
                b.AddToClassList("btn"); b.AddToClassList("option");
                b.clicked += () =>
                {
                    if (pickedQuests.Remove(qid)) b.RemoveFromClassList("selected");
                    else { pickedQuests.Add(qid); b.AddToClassList("selected"); }
                    SetOn(questsConfirm, QuestPickIsLegal, Strings.ReasonPickThree);
                };
                questOptions.Add(b);
            }
            SetOn(questsConfirm, QuestPickIsLegal, Strings.ReasonPickThree);
        }

        /// <summary>S-03: Confirm is on only when the current selection is a legal ChooseQuestsCommand.</summary>
        public bool QuestPickIsLegal
        {
            get
            {
                var cmd = new ChooseQuestsCommand(match.Viewer, pickedQuests);
                foreach (var c in match.Legal)
                    if (cmd.Equals(c)) return true;
                return false;
            }
        }

        /// <summary>Test/CLI hook: toggles a quest option as a tap on its button would.</summary>
        public void ToggleQuest(string questId)
        {
            foreach (var el in questOptions.Children())
                if (el is Button b && b.userData as string == questId)
                    using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        public bool QuestConfirmEnabled => IsOn(questsConfirm);
        /// <summary>Compact opponent strip (V-09): tower Health, hand count, completed ✓ and failed ✗ quests.</summary>
        public string OpponentInfoText => oppTower.text + " " + oppHand.text + " " + oppQuests.text;
        public string QuestPanelText => questText.text;

        void OnConfirmQuests() => match.Apply(new ChooseQuestsCommand(match.Viewer, pickedQuests));

        void BuildPassiveOptions(PlayerView v)
        {
            passiveOptions.Clear();
            foreach (var id in v.PassiveOffer)
            {
                var pid = id;
                var b = new Button { text = Strings.Name(pid) + "\n" + Strings.Text(pid) };
                b.AddToClassList("btn"); b.AddToClassList("option");
                b.clicked += () => match.Apply(new ChoosePassiveCommand(match.Viewer, pid));
                passiveOptions.Add(b);
            }
        }

        // ---------- HUD ----------

        void RefreshHud(PlayerView v)
        {
            bool setup = v.Phase == GamePhase.Setup;
            bool myTurn = !setup && v.ActivePlayer == v.Viewer;
            ownTower.text = Strings.TowerIcon + " " + v.OwnTowerHealth;
            roundLabel.text = setup ? Strings.Setup : Strings.RoundShort + v.Round;
            oppTower.text = Strings.TowerIcon + " " + v.EnemyTowerHealth;
            oppHand.text = Strings.HandIcon + " " + v.OpponentHandCount;
            oppQuests.text = Strings.QuestDone + v.OpponentCompletedQuests.Count + " " + Strings.QuestFail + v.OpponentFailedQuests.Count; // Q-03, Q-04, V-09
            turnBanner.text = setup ? Strings.Setup : myTurn ? Strings.YourTurn : Strings.OpponentTurn;
            turnBanner.EnableInClassList("mine", myTurn);
            turnBanner.EnableInClassList("theirs", !setup && !myTurn);
            eventBanner.style.display = v.AnnouncedEvent != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (v.AnnouncedEvent != null) eventBanner.text = Strings.EventIcon + " " + Strings.Name(v.AnnouncedEvent) + " · " + Strings.RoundShort + v.AnnouncedEventRound;

            manaText.text = v.Mana.ToString();
            energyText.text = v.Energy.ToString();
            Pips(manaPips, v.Mana, Mathf.Max(v.Mana, Catalog.ManaCap), "mana-pip");
            Pips(energyPips, v.Energy, Catalog.EnergyPerTurn, "energy-pip");

            for (int i = 0; i < 3; i++)
            {
                var card = v.Market[i];
                market[i].text = card == null ? Strings.SlotName(i) + "\n" + Strings.Empty
                    : CardIcon(card) + " " + card.Cost + "\n" + CardName(card);
                SetOn(market[i], !setup && HasDraw(i), DrawReason(v, card == null));
                market[i].EnableInClassList("picked", v.PrePickSlot == i);
            }
            SetOn(blindBtn, !setup && HasDraw(DrawCommand.Blind), DrawReason(v, false));
            blindBtn.EnableInClassList("picked", v.PrePickSlot == DrawCommand.Blind);

            endBtn.text = setup ? Strings.FinishSetup : Strings.EndTurn;
            bool canEnd = setup ? Find<FinishSetupCommand>() != null : Find<EndTurnCommand>() != null;
            SetOn(endBtn, canEnd, setup ? Strings.ReasonFinishSetup : !myTurn ? Strings.ReasonOpponentTurn
                : v.DrawPending ? Strings.ReasonDrawFirst : Strings.ReasonNotNow);

            var q = new System.Text.StringBuilder();
            q.Append(Strings.Quests).Append(":\n");
            foreach (var qp in v.QuestProgress)
            {
                q.Append("• ").Append(Strings.Name(qp.Id)).Append(" — ").Append(Strings.Text(qp.Id)).Append(" [");
                // PM: the counter only for Active quests; Completed/Failed show the status alone.
                if (qp.Status == QuestStatus.Active) q.Append(qp.Current).Append('/').Append(qp.Target);
                else q.Append(Strings.Status(qp.Status));
                q.Append("]\n");
            }
            q.Append('\n').Append(Strings.Passive).Append(v.PassiveChoice == null ? "-" : Strings.Name(v.PassiveChoice) + " — " + Strings.Text(v.PassiveChoice));
            q.Append("\n\n").Append(Strings.Player(v.Viewer.Opponent())).Append(": ").Append(Strings.Tower).Append(' ').Append(v.EnemyTowerHealth)
                .Append(" · ").Append(Strings.Hand).Append(' ').Append(v.OpponentHandCount);
            if (v.OpponentPassive != null) q.Append('\n').Append(Strings.OppPassive).Append(Strings.Name(v.OpponentPassive));
            q.Append('\n').Append(Strings.OppQuests).Append(v.OpponentCompletedQuests.Count);
            foreach (var id in v.OpponentCompletedQuests) q.Append(' ').Append(Strings.Name(id));
            q.Append('\n').Append(Strings.OppFailed).Append(v.OpponentFailedQuests.Count); // Q-04, V-09
            foreach (var id in v.OpponentFailedQuests) q.Append(' ').Append(Strings.Name(id));
            if (v.AnnouncedEvent != null)
                q.Append("\n\n").Append(Strings.Event).Append(Strings.Name(v.AnnouncedEvent)).Append(" (").Append(Strings.Round).Append(v.AnnouncedEventRound).Append(')');
            questText.text = q.ToString();

            BuildHand(v);
            UpdatePrompt();
        }

        static string DrawReason(PlayerView v, bool emptySlot)
        {
            if (v.Phase == GamePhase.Setup) return Strings.ReasonSetupNoDraw;
            if (emptySlot) return Strings.ReasonSlotEmpty;
            if (v.Hand.Count >= Catalog.HandLimit) return Strings.ReasonHandFull;
            if (v.ActivePlayer == v.Viewer && !v.DrawPending) return Strings.ReasonAlreadyDrew;
            return Strings.ReasonCannotDraw;
        }

        static void Pips(VisualElement row, int on, int total, string cls)
        {
            row.Clear();
            for (int i = 0; i < total; i++)
            {
                var p = new VisualElement();
                p.AddToClassList("pip"); p.AddToClassList(cls);
                if (i >= on) p.AddToClassList("off");
                row.Add(p);
            }
        }

        /// <summary>Per frame: clock text (cached strings), toast/notice expiry and two-finger pinch. No allocations.</summary>
        public void Tick()
        {
            float now = Time.unscaledTime;
            if (toastUntil > 0f && now > toastUntil) { toastUntil = 0f; toast.style.display = DisplayStyle.None; }
            if (noticeUntil > 0f && now > noticeUntil) { noticeUntil = 0f; notice.style.display = DisplayStyle.None; }
            TickPinch();
            TickClock();
        }

        public void TickClock()
        {
            if (Current != Screen.Board || !match.HasMatch) return;
            if (!match.TimerEnabled) { timer.text = "∞"; timerBank.style.display = DisplayStyle.None; return; }
            var v = match.View;
            int t = Mathf.CeilToInt(v.Phase == GamePhase.Setup ? match.SetupSecondsLeft : match.TurnSecondsLeft);
            int b = Mathf.CeilToInt(match.BankSecondsLeft(match.Viewer));
            if (v.Phase == GamePhase.Setup) b = -1;
            if (t == shownTurn && b == shownBank) return;
            shownTurn = t; shownBank = b;
            timer.text = Seconds(t); // cached strings: no allocation per second
            timerBank.text = b < 0 ? "" : BankText(b);
            timerBank.style.display = b < 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static readonly string[] secondsText = new string[Mathf.Max(Catalog.PlacementSeconds, Catalog.TurnSeconds) + 1];
        static readonly string[] bankText = new string[Catalog.TimeBankSeconds + 1];

        static string Seconds(int s)
        {
            s = Mathf.Clamp(s, 0, secondsText.Length - 1);
            return secondsText[s] ?? (secondsText[s] = s + " sn");
        }

        static string BankText(int s)
        {
            s = Mathf.Clamp(s, 0, bankText.Length - 1);
            return bankText[s] ?? (bankText[s] = Strings.Bank + s);
        }

        static string CardName(CardInstance c) =>
            c.IsCharacter ? Strings.ClassName(c.Character.Class) : Strings.Name(c.Support.Id);

        static string CardIcon(CardInstance c) =>
            c.IsCharacter ? Strings.ClassIcon(c.Character.Class) : Strings.CardIcon(c.Support.Category);

        void BuildHand(PlayerView v)
        {
            hand.Clear();
            int n = v.Hand.Count;
            // More cards than fit (setup deals more than the play-time limit): overlap them, keep each card full size.
            float overlap = n > 1 ? Mathf.Max(0f, (n * CardStride - HandWidth) / (n - 1)) : 0f;
            for (int i = 0; i < n; i++)
            {
                var card = v.Hand[i];
                var el = MakeCard(card);
                int id = card.Id;
                el.EnableInClassList("unplayable", !HasCardCommand(id));
                el.EnableInClassList("selected", id == selectedCard);
                if (i > 0 && overlap > 0f) el.style.marginLeft = -overlap;
                el.RegisterCallback<PointerDownEvent>(e => OnCardDown(id, e));
                hand.Add(el);
            }
        }

        /// <summary>UX-07 minimal card: icon, name, cost. Stats and the description are in the long-press detail.</summary>
        static VisualElement MakeCard(CardInstance card)
        {
            var el = new VisualElement();
            el.AddToClassList("card");
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = FrameColor(card);
            var cost = new Label(card.Cost.ToString()) { pickingMode = PickingMode.Ignore };
            cost.AddToClassList("card-cost");
            var icon = new Label(CardIcon(card)) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("card-icon");
            if (card.IsCharacter) icon.style.color = Color.Lerp(Gfx.BiomeColor(card.Character.Biome), Color.black, 0.35f);
            var name = new Label(CardName(card)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("card-name");
            el.Add(cost); el.Add(icon); el.Add(name);
            return el;
        }

        /// <summary>UX-07: support cards use the rarity colour, character cards the biome colour (A-02).</summary>
        static Color FrameColor(CardInstance c)
        {
            if (c.IsCharacter) return Gfx.BiomeColor(c.Character.Biome);
            switch (c.Support.Rarity)
            {
                case Rarity.Rare: return new Color(0.2f, 0.45f, 1f);
                case Rarity.Epic: return new Color(0.7f, 0.25f, 0.95f);
                default: return new Color(0.55f, 0.55f, 0.55f);
            }
        }

        void UpdatePrompt()
        {
            var v = match.View;
            string text = null;
            if (secondPick.Count > 0) text = secondPick[0] is PlayCardCommand pc && pc.Dest.HasValue ? Strings.PickDestination : Strings.PickDirection;
            else if (v.Phase == GamePhase.Setup) text = v.SetupStep == SetupStep.Tower ? Strings.PlaceTower : Strings.PlaceUnits;
            else if (v.DrawPending && v.ActivePlayer == v.Viewer) text = Strings.DrawFirst;
            prompt.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
            prompt.text = text ?? "";
            overwatchBtn.style.display = selectedUnit >= 0 && FindOverwatch(selectedUnit) != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------- Commands from the legal list ----------

        T Find<T>() where T : class, ICommand
        {
            foreach (var c in match.Legal)
                if (c is T t) return t;
            return null;
        }

        bool HasDraw(int slot)
        {
            foreach (var c in match.Legal)
                if ((c is DrawCommand d && d.Slot == slot) || (c is PrePickCommand p && p.Slot == slot)) return true;
            return false;
        }

        static int CardOf(ICommand c)
        {
            switch (c)
            {
                case DeployCommand d: return d.CardId;
                case PlayCardCommand p: return p.CardId;
                case PlaceUnitCommand u: return u.CardId;
                case PlaceTrapCommand t: return t.CardId;
                default: return -1;
            }
        }

        static bool CellOf(ICommand c, out Hex h)
        {
            switch (c)
            {
                case DeployCommand d: h = d.Cell; return true;
                case PlayCardCommand p: h = p.Target; return true;
                case PlaceUnitCommand u: h = u.Cell; return true;
                case PlaceTrapCommand t: h = t.Cell; return true;
                case PlaceTowerCommand w: h = w.Cell; return true;
                case MoveCommand m: h = m.Dest; return true;
                case AttackCommand a: h = a.Target; return true;
                default: h = default; return false;
            }
        }

        bool HasCardCommand(int cardId)
        {
            foreach (var c in match.Legal)
                if (CardOf(c) == cardId) return true;
            return false;
        }

        OverwatchCommand FindOverwatch(int unitId)
        {
            foreach (var c in match.Legal)
                if (c is OverwatchCommand o && o.UnitId == unitId) return o;
            return null;
        }

        void OnDrawSlot(int slot)
        {
            foreach (var c in match.Legal)
                if ((c is DrawCommand d && d.Slot == slot) || (c is PrePickCommand p && p.Slot == slot)) { match.Apply(c); return; }
        }

        void OnEnd()
        {
            var cmd = (ICommand)Find<FinishSetupCommand>() ?? Find<EndTurnCommand>();
            if (cmd != null) match.Apply(cmd);
        }

        void OnOverwatch()
        {
            var o = FindOverwatch(selectedUnit);
            if (o != null) match.Apply(o);
        }

        // ---------- Selection and highlights ----------

        void ClearSelection()
        {
            selectedUnit = -1;
            selectedCard = -1;
            secondPick.Clear();
            if (match.HasMatch && !match.HandoffPending && Current == Screen.Board) ShowDefaultHighlights();
        }

        void ShowDefaultHighlights()
        {
            board.ClearHighlights();
            foreach (var c in match.Legal)
                if (c is PlaceTowerCommand t) board.Highlight(t.Cell, Gfx.MoveHl);
            foreach (var el in hand.Children()) el.RemoveFromClassList("selected");
            UpdatePrompt();
        }

        void SelectUnit(int unitId)
        {
            selectedUnit = unitId;
            selectedCard = -1;
            secondPick.Clear();
            board.ClearHighlights();
            int n = 0;
            foreach (var c in match.Legal)
            {
                if (c is MoveCommand m && m.UnitId == unitId) { board.Highlight(m.Dest, Gfx.MoveHl); n++; }
                else if (c is AttackCommand a && a.UnitId == unitId) { board.Highlight(a.Target, Gfx.AttackHl); n++; }
                else if (c is OverwatchCommand o && o.UnitId == unitId) n++;
            }
            if (n == 0) ShowToast(UnitReason(unitId));
            UpdatePrompt();
        }

        string UnitReason(int unitId)
        {
            var v = match.View;
            if (v.Phase != GamePhase.Playing) return Strings.ReasonNotNow;
            if (v.ActivePlayer != v.Viewer) return Strings.ReasonOpponentTurn;
            if (v.DrawPending) return Strings.ReasonDrawFirst;
            if (v.Energy <= 0) return Strings.ReasonNoEnergy;
            foreach (var u in v.OwnUnits)
                if (u.Id == unitId && u.ActedThisTurn) return Strings.ReasonActed;
            return Strings.ReasonNoAction;
        }

        string CardReason(int cardId)
        {
            var v = match.View;
            if (v.Phase == GamePhase.Playing && v.ActivePlayer != v.Viewer) return Strings.ReasonOpponentTurn;
            if (v.Phase == GamePhase.Playing && v.DrawPending) return Strings.ReasonDrawFirst;
            foreach (var c in v.Hand)
                if (c.Id == cardId && v.Phase == GamePhase.Playing && c.Cost > v.Mana) return Strings.ReasonNoMana;
            return Strings.ReasonNoTarget;
        }

        void SelectCard(int cardId)
        {
            selectedCard = cardId;
            selectedUnit = -1;
            secondPick.Clear();
            board.ClearHighlights();
            if (match.View.Phase == GamePhase.Playing) // C-02, UX-03 (setup uses the home zone, shown by the targets)
                foreach (var z in match.View.ControlZone) board.Highlight(z, Gfx.ZoneHl);
            foreach (var c in match.Legal)
                if (CardOf(c) == cardId && CellOf(c, out var h)) board.Highlight(h, Gfx.TargetHl);
            int i = 0;
            foreach (var el in hand.Children())
                el.EnableInClassList("selected", i < match.View.Hand.Count && match.View.Hand[i++].Id == cardId);
            if (!HasCardCommand(cardId)) ShowToast(CardReason(cardId));
            UpdatePrompt();
        }

        /// <summary>Test/CLI hook: taps a board cell as a finger would.</summary>
        public void TapCell(Hex cell)
        {
            LastTap = cell.ToString();
            OnCellTapped(cell);
        }

        /// <summary>Test/CLI hook: taps a hand card (select, or deselect when already selected).</summary>
        public void TapCard(int cardId)
        {
            if (selectedCard == cardId) ClearSelection();
            else SelectCard(cardId);
        }

        void OnCellTapped(Hex cell)
        {
            if (confirmAction != null) return;
            if (secondPick.Count > 0)
            {
                foreach (var c in secondPick)
                    if (SecondPickCell(c) == cell) { match.Apply(c); return; }
                ClearSelection();
                return;
            }
            if (selectedCard >= 0 && TryCardOnCell(selectedCard, cell)) return;
            foreach (var c in match.Legal)
                if (c is PlaceTowerCommand t && t.Cell == cell) { match.Apply(c); return; }
            if (selectedUnit >= 0)
                foreach (var c in match.Legal)
                    if ((c is MoveCommand m && m.UnitId == selectedUnit && m.Dest == cell)
                        || (c is AttackCommand a && a.UnitId == selectedUnit && a.Target == cell)) { match.Apply(c); return; }
            foreach (var u in match.View.OwnUnits)
                if (u.Pos == cell) { SelectUnit(u.Id); return; }
            ClearSelection();
        }

        /// <summary>Plays the card on the cell if legal. Push (C-18) and Teleport (C-15) need a second pick.
        /// C-05/C-06: if a lasting buff (debuff) would replace the unit's current one, confirm first.</summary>
        bool TryCardOnCell(int cardId, Hex cell)
        {
            var options = new List<ICommand>();
            foreach (var c in match.Legal)
                if (CardOf(c) == cardId && CellOf(c, out var h) && h == cell) options.Add(c);
            if (options.Count == 0) { ClearSelection(); return false; }
            string replaced = ReplacedEffect(cardId, cell);
            if (replaced != null)
            {
                AskConfirm(Strings.Replaces(replaced), () => PlayOptions(options, cell));
                return true;
            }
            PlayOptions(options, cell);
            return true;
        }

        void PlayOptions(List<ICommand> options, Hex cell)
        {
            if (options.Count == 1) { match.Apply(options[0]); return; }
            secondPick.Clear();
            board.ClearHighlights();
            board.Highlight(cell, Gfx.TargetHl);
            foreach (var c in options)
            {
                var h = SecondPickCell(c);
                if (Board.IsOnBoard(h)) { secondPick.Add(c); board.Highlight(h, Gfx.MoveHl); }
            }
            if (secondPick.Count == 0) match.Apply(options[0]);
            UpdatePrompt();
        }

        /// <summary>The id of the buff/debuff a lasting support card would replace on the unit at the cell (C-05), from
        /// the card's own definition and the PlayerView; null when nothing is replaced. Instant cards never replace.</summary>
        string ReplacedEffect(int cardId, Hex cell)
        {
            var v = match.View;
            CardInstance card = null;
            foreach (var c in v.Hand)
                if (c.Id == cardId) card = c;
            if (card == null || card.IsCharacter) return null;
            var def = card.Support;
            if (def.Duration == DurationKind.Instant || def.Category == CardCategory.Trap) return null;
            int i = Board.IndexOf(cell);
            if (i < 0 || v.Cells[i].Unit == null || v.Cells[i].Unit.IsGhost) return null;
            var u = v.Cells[i].Unit;
            return def.Pool == SupportPool.Buff ? u.BuffId : u.DebuffId;
        }

        void AskConfirm(string text, System.Action onOk)
        {
            confirmAction = onOk;
            confirmText.text = text;
            confirmPanel.style.display = DisplayStyle.Flex;
        }

        /// <summary>Test/CLI hook and the Confirm button of the C-05 dialog.</summary>
        public void AcceptConfirm()
        {
            var a = confirmAction;
            confirmAction = null;
            confirmPanel.style.display = DisplayStyle.None;
            a?.Invoke();
        }

        void CancelConfirm()
        {
            bool had = confirmAction != null;
            confirmAction = null;
            confirmPanel.style.display = DisplayStyle.None;
            if (had) ClearSelection();
        }

        static Hex SecondPickCell(ICommand c)
        {
            var p = (PlayCardCommand)c;
            if (p.Dest.HasValue) return p.Dest.Value;
            return p.Direction >= 0 ? p.Target.Neighbor(p.Direction) : p.Target;
        }

        // ---------- Board pointer: tap, pan, pinch, wheel, double tap ----------

        void OnBoardDown(VisualElement hit, PointerDownEvent e)
        {
            boardPointers++;
            hit.CapturePointer(e.pointerId);
            if (boardPointers > 1) { boardDragged = true; return; } // second finger: a pinch, never a tap
            boardPointer = e.pointerId;
            boardPress = boardLast = e.position;
            boardDragged = false;
            boardRightDrag = e.button == 1;
        }

        void OnBoardMove(PointerMoveEvent e)
        {
            if (e.pointerId != boardPointer || boardPointers != 1 || pinchActive) return;
            Vector2 p = e.position;
            if (!boardDragged && (p - boardPress).sqrMagnitude > DragThreshold * DragThreshold) boardDragged = true;
            if (boardDragged && CameraRig.Instance != null) CameraRig.Instance.Pan(PanelToScreen(boardLast), PanelToScreen(p));
            boardLast = p;
        }

        void OnBoardUp(VisualElement hit, PointerUpEvent e)
        {
            if (hit.HasPointerCapture(e.pointerId)) hit.ReleasePointer(e.pointerId);
            boardPointers = Mathf.Max(0, boardPointers - 1);
            if (e.pointerId != boardPointer) return;
            boardPointer = -1;
            if (boardDragged || boardRightDrag || pinchActive) return;
            Vector2 p = e.position;
            float now = Time.unscaledTime;
            if (now - lastTapTime < DoubleTapSeconds && (p - lastTapPos).sqrMagnitude < DoubleTapDistance * DoubleTapDistance)
            {
                lastTapTime = -10f;
                CameraRig.Instance?.ResetView(); // double tap: default framing
                return;
            }
            lastTapTime = now;
            lastTapPos = p;
            if (TryPickPanel(p, out var cell)) { LastTap = cell.ToString(); OnCellTapped(cell); }
            else ClearSelection();
        }

        void OnWheel(WheelEvent e)
        {
            if (CameraRig.Instance == null || e.delta.y == 0f) return;
            CameraRig.Instance.ZoomBy(e.delta.y < 0f ? 1.15f : 1f / 1.15f, PanelToScreen(e.mousePosition));
            e.StopPropagation();
        }

        /// <summary>Two-finger pinch zoom + pan from EnhancedTouch (works even if UI Toolkit routes only one finger).
        /// Only while a board gesture is in progress, so pinching over the HUD does nothing.</summary>
        void TickPinch()
        {
            if (Current != Screen.Board || CameraRig.Instance == null) { pinchActive = false; return; }
            var touches = ETouch.activeTouches;
            if (touches.Count < 2 || (boardPointers == 0 && !pinchActive)) { pinchActive = false; return; }
            Vector2 a = touches[0].screenPosition, b = touches[1].screenPosition;
            float span = Vector2.Distance(a, b);
            Vector2 mid = (a + b) * 0.5f;
            if (!pinchActive)
            {
                pinchActive = true;
                boardDragged = true;
                pinchSpan = span;
                pinchMid = mid;
                return;
            }
            if (pinchSpan > 1f && span > 1f) CameraRig.Instance.ZoomBy(span / pinchSpan, mid);
            CameraRig.Instance.Pan(pinchMid, mid);
            pinchSpan = span;
            pinchMid = mid;
        }

        Vector2 PanelToScreen(Vector2 panelPos)
        {
            var size = root.layout.size;
            return new Vector2(panelPos.x / size.x * UnityEngine.Screen.width, (1f - panelPos.y / size.y) * UnityEngine.Screen.height);
        }

        bool TryPickPanel(Vector2 panelPos, out Hex cell) => CameraRig.TryPick(cam, PanelToScreen(panelPos), out cell);

        // ---------- Card drag (UX-03) ----------

        void OnCardDown(int cardId, PointerDownEvent e)
        {
            pressCard = cardId;
            pressPos = e.position;
            dragging = false;
            longPressed = false;
            longPressJob?.Pause();
            longPressJob = root.schedule.Execute(() =>
            {
                if (pressCard != cardId || dragging) return;
                longPressed = true;
                ShowDetail(cardId);
            }).StartingIn((long)LongPressMs);
        }

        void OnCardMove(PointerMoveEvent e)
        {
            if (pressCard < 0 || longPressed) return;
            if (!dragging && (((Vector2)e.position) - pressPos).sqrMagnitude > DragThreshold * DragThreshold)
            {
                dragging = true;
                SelectCard(pressCard);
                dragCard.Clear();
                foreach (var c in match.View.Hand)
                    if (c.Id == pressCard) { var el = MakeCard(c); el.style.marginLeft = 0; dragCard.Add(el); }
                dragCard.style.display = DisplayStyle.Flex;
            }
            if (dragging)
            {
                dragCard.style.left = e.position.x - 93;
                dragCard.style.top = e.position.y - 230; // above the finger, so the target cell stays visible
            }
        }

        void OnCardUp(PointerUpEvent e)
        {
            if (pressCard < 0) return;
            int card = pressCard;
            pressCard = -1;
            longPressJob?.Pause();
            dragCard.style.display = DisplayStyle.None;
            if (longPressed) return;
            if (dragging)
            {
                dragging = false;
                if (TryPickPanel(e.position, out var cell)) TryCardOnCell(card, cell);
                else ClearSelection();
                return;
            }
            TapCard(card);
        }

        void ShowDetail(int cardId)
        {
            foreach (var c in match.View.Hand)
                if (c.Id == cardId)
                {
                    string text = CardName(c) + "  (" + Strings.Mana + " " + c.Cost + ")\n";
                    if (c.IsCharacter)
                    {
                        var d = Unit.DefOf(c.Character.Class);
                        text += Strings.BiomeName(c.Character.Biome) + "\n" + Strings.AttackIcon + " " + d.Attack + "   ♥ " + d.Health
                            + "   ➜ " + d.Move + "   👁 " + d.Sight;
                    }
                    else text += Strings.Text(c.Support.Id);
                    detailText.text = text;
                    cardDetail.style.display = DisplayStyle.Flex;
                }
        }

        // ---------- Event feedback: notices and the compact log (filtered events only, UX-10) ----------

        void ClearLog()
        {
            log.Clear();
            eventLog.text = "";
            eventLog.style.display = DisplayStyle.None;
        }

        void OnEvents(IReadOnlyList<GameEvent> events)
        {
            units.Play(events);
            if (!match.HasMatch) return;
            var v = match.View;
            bool changed = false, batchBad = false;
            foreach (var e in events)
            {
                string line = Describe(e, v, out string noticeText, out bool bad);
                if (line != null)
                {
                    log.Add(line);
                    if (log.Count > LogLines) log.RemoveAt(0);
                    changed = true;
                }
                if (noticeText != null && (bad || !batchBad)) { ShowNotice(noticeText, bad); batchBad |= bad; } // bad news is not hidden by a later notice
            }
            if (!changed) return;
            sb.Clear();
            for (int i = 0; i < log.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(log[i]);
            }
            eventLog.text = sb.ToString();
            eventLog.style.display = DisplayStyle.Flex;
        }

        string UnitName(int unitId, PlayerView v)
        {
            if (units.TryGetSeen(unitId, out var s)) return Strings.UnitRef(s.Class, s.Owner == v.Viewer);
            return Strings.SomeUnit;
        }

        string Describe(GameEvent e, PlayerView v, out string noticeText, out bool bad)
        {
            noticeText = null;
            bad = false;
            switch (e)
            {
                case DamageDealt d:
                {
                    string who;
                    if (d.TargetUnitId == DamageDealt.Tower)
                        who = v.OwnTowerPos.HasValue && v.OwnTowerPos.Value == d.Target ? Strings.OwnTowerRef : Strings.EnemyTowerRef;
                    else who = UnitName(d.TargetUnitId, v);
                    return who + " −" + d.Amount + " (" + Strings.DamageKindName(d.Kind) + ")";
                }
                case UnitDied u:
                {
                    bool own = u.Owner == v.Viewer;
                    string text = (units.TryGetSeen(u.UnitId, out var s) ? Strings.UnitRef(s.Class, own) : Strings.SomeUnit) + Strings.Died;
                    noticeText = text;
                    bad = own;
                    return text;
                }
                case UnitHealed h: return UnitName(h.UnitId, v) + " +" + h.Amount;
                case ShieldBlocked sb2: return UnitName(sb2.UnitId, v) + Strings.ShieldUsed;
                case TowerDamageBlocked tb: return (tb.Owner == v.Viewer ? Strings.OwnTowerRef : Strings.EnemyTowerRef) + Strings.WallUsed;
                case TrapTriggered tr: noticeText = Strings.TrapOn; bad = tr.Owner != v.Viewer; return Strings.TrapOn;
                case TowerShot ts: noticeText = Strings.TowerFired; bad = ts.Owner != v.Viewer; return Strings.TowerFired;
                case OverwatchFired of:
                {
                    bool mine = units.TryGetSeen(of.UnitId, out var s) && s.Owner == v.Viewer;
                    noticeText = Strings.OverwatchFiredText;
                    bad = !mine;
                    return Strings.OverwatchFiredText;
                }
                case CardPlayed cp: return (cp.Player == v.Viewer ? Strings.YouPlayed : Strings.OppPlayed) + Strings.Name(cp.DefId);
                case UnitDeployed ud: return v.Phase == GamePhase.Playing ? UnitName(ud.UnitId, v) + Strings.Deployed : null;
                case UnitRevived ur: return Strings.UnitRef(ur.Class, ur.Owner == v.Viewer) + Strings.Revived;
                case QuestCompleted qc:
                {
                    bool mine = qc.Player == v.Viewer;
                    noticeText = (mine ? Strings.QuestDoneText : Strings.OppQuestDoneText) + Strings.Name(qc.QuestId);
                    bad = !mine;
                    return noticeText;
                }
                case QuestFailed qf:
                    return (qf.Player == v.Viewer ? Strings.QuestFailText : Strings.OppQuestFailText) + Strings.Name(qf.QuestId);
                case PassiveRevealed pr: return Strings.PassiveOpened + Strings.Name(pr.PassiveId);
                case MapEventResolved me: noticeText = Strings.EventHappened + Strings.Name(me.DefId); return noticeText;
                case TurnStarted t:
                {
                    bool mine = t.Player == v.Viewer;
                    noticeText = mine ? Strings.YourTurn : Strings.OpponentTurn;
                    return "— " + Strings.RoundShort + t.Round + " · " + noticeText + " —";
                }
                default: return null;
            }
        }
    }
}
