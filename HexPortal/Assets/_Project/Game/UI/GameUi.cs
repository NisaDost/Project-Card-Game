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
        const float TurnNoticeSeconds = 0.8f;
        const int LogLines = 4;
        const float HandWidth = 1318f;     // .hand width at the 1920 reference (left 276, right 326); fallback before layout
        const float CardStride = 154f;     // .card width + margins
        const float GroupGap = 20f;        // .hand-group horizontal margins (UX-04 three groups)
        const float DragOffsetX = 73f;     // half a card: the ghost is centred over the finger...
        const float DragOffsetY = 200f;    // ...and above it, so the target cell stays visible

        readonly MatchController match;
        readonly VisualElement root;
        readonly BoardView board;
        readonly UnitsView units;
        readonly Camera cam;

        readonly VisualElement screenMenu, screenQuests, screenPassive, screenHandoff, screenResult, hud, questPanel, dragCard, confirmPanel, infoPanel;
        readonly VisualElement hand, handTraps, handChars, handBuffs, questOptions, passiveOptions, manaPips, energyPips, oppInfo;
        readonly VisualElement marketBox;
        readonly Label drawCall;
        readonly Label timerBank, ownTower, roundLabel, oppTower, oppHand, oppQuests, turnBanner, eventBanner, manaText, manaCaption, energyText,
            timer, prompt, questText, confirmText, toast, notice, eventLog, infoIcon, infoTitle, infoText;
        readonly List<VisualElement> handCards = new List<VisualElement>();
        readonly HashSet<Hex> redCells = new HashSet<Hex>();
        readonly List<Hex> previewOnly = new List<Hex>();
        readonly Button[] market = new Button[3];
        readonly Button blindBtn, endBtn, overwatchBtn, questsConfirm, menuTimer;
        readonly Button torchBtn, refreshBtn; // v4.0: V-12 torch, D-05 Market refresh
        readonly Label bearerHint;            // U-44: "Taşıyıcı bekleniyor"
        /// <summary>D-05: Market slot refresh animations started (tests).</summary>
        public int MarketRefreshAnimations { get; private set; }
        /// <summary>U-44: the "Taşıyıcı bekleniyor" hint is shown (tests).</summary>
        public bool BearerHintVisible => bearerHint.style.display == DisplayStyle.Flex;
        readonly List<string> pickedQuests = new List<string>();
        readonly Dictionary<VisualElement, string> offReason = new Dictionary<VisualElement, string>();
        readonly List<string> log = new List<string>();
        readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

        bool timerOn = true;
        bool aiTimerOn;
        PlayerId humanSeat = PlayerId.A;
        int selectedUnit = -1;
        int selectedCard = -1;
        readonly List<ICommand> secondPick = new List<ICommand>(); // card second picks (C-15, C-18) or move-attack approaches (T-05)
        bool rangePreview; // UX-10: the selected unit only shows its ranges (opponent turn), no commands
        PlayerId? facing;
        int shownMatch = -1;
        int shownTurn = -1, shownBank = -1;
        System.Action confirmAction;
        float toastUntil, noticeUntil;

        // Card pointer state.
        int pressCard = -1;
        int pressPointer = -1;
        Vector2 pressPos;
        bool dragging, longPressed;
        IVisualElementScheduledItem longPressJob;

        // Board pointer state (tap / pan / pinch / double tap).
        // UX-10 plan arrows (vs AI, opponent turn): a board press that starts on an own unit draws an arrow instead of panning.
        readonly PlanArrows plans;
        bool planning;
        Hex planFrom;
        public PlanArrows Plans => plans;
        int boardPointer = -1;
        Vector2 boardPress, boardLast;
        bool boardDragged, boardRightDrag;
        int boardPointers; // == boardIds.Count
        readonly List<int> boardIds = new List<int>(10); // pointer ids currently pressed on the board (no duplicates)
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
        /// <summary>T-04: the "Kart çek" call and the Market glow are shown (own turn, draw pending).</summary>
        public bool DrawCallVisible { get; private set; }
        /// <summary>Test hook: the colour a cell is highlighted with (clear = none).</summary>
        public Color HighlightAt(Hex h) => board.HighlightAt(h);

        public GameUi(MatchController match, VisualElement root, BoardView board, UnitsView units, Camera cam)
        {
            this.match = match; this.root = root; this.board = board; this.units = units; this.cam = cam;
            plans = new PlanArrows(board.transform);
            screenMenu = root.Q("screen-menu"); screenQuests = root.Q("screen-quests"); screenPassive = root.Q("screen-passive");
            screenHandoff = root.Q("screen-handoff"); screenResult = root.Q("screen-result"); hud = root.Q("hud");
            questPanel = root.Q("quest-panel"); infoPanel = root.Q("info-panel"); dragCard = root.Q("drag-card"); confirmPanel = root.Q("confirm-panel");
            hand = root.Q("hand"); handTraps = root.Q("hand-traps"); handChars = root.Q("hand-chars"); handBuffs = root.Q("hand-buffs");
            manaCaption = root.Q<Label>("mana-caption");
            infoIcon = root.Q<Label>("info-icon"); infoTitle = root.Q<Label>("info-title"); infoText = root.Q<Label>("info-text");
            // UX-11 panel is display-only: it overlaps the left board cells, so it must never take taps meant for them
            // (it used to close on tap, which swallowed the first tap on a target under it). It closes on deselect.
            infoPanel.Query<VisualElement>().ForEach(x => x.pickingMode = PickingMode.Ignore); questOptions = root.Q("quest-options"); passiveOptions = root.Q("passive-options");
            manaPips = root.Q("mana-pips"); energyPips = root.Q("energy-pips"); oppInfo = root.Q("opp-info");
            ownTower = root.Q<Label>("own-tower"); roundLabel = root.Q<Label>("round");
            oppTower = root.Q<Label>("opp-tower"); oppHand = root.Q<Label>("opp-hand"); oppQuests = root.Q<Label>("opp-quests");
            turnBanner = root.Q<Label>("turn-banner");
            eventBanner = root.Q<Label>("event-banner"); manaText = root.Q<Label>("mana-text"); energyText = root.Q<Label>("energy-text");
            timer = root.Q<Label>("timer"); timerBank = root.Q<Label>("timer-bank"); prompt = root.Q<Label>("prompt"); questText = root.Q<Label>("quest-text");
            confirmText = root.Q<Label>("confirm-text");
            toast = root.Q<Label>("toast"); notice = root.Q<Label>("notice");
            notice.pickingMode = PickingMode.Position; // a tap dismisses the notice (it sits in the top strip, never over the board)
            notice.RegisterCallback<PointerDownEvent>(e => { noticeUntil = 0f; notice.style.display = DisplayStyle.None; e.StopPropagation(); }); eventLog = root.Q<Label>("event-log");
            root.Q<Label>("mana-icon").text = Strings.ManaIcon;
            root.Q<Label>("energy-icon").text = Strings.EnergyIcon;
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                market[i] = root.Q<Button>("market-" + i);
                Guard(market[i], () => OnDrawSlot(slot));
            }
            root.Q<Label>("market-title").text = Strings.MarketTitle;
            marketBox = root.Q("market-box");
            drawCall = root.Q<Label>("draw-call"); drawCall.text = Strings.DrawCall;
            blindBtn = root.Q<Button>("blind-btn"); blindBtn.text = Strings.Blind; Guard(blindBtn, () => OnDrawSlot(DrawCommand.Blind));
            endBtn = root.Q<Button>("end-btn"); Guard(endBtn, OnEnd);
            overwatchBtn = root.Q<Button>("overwatch-btn"); overwatchBtn.text = Strings.OverwatchIcon + " " + Strings.Overwatch; Guard(overwatchBtn, OnOverwatch);
            torchBtn = root.Q<Button>("torch-btn"); torchBtn.text = Strings.TorchIcon + " " + Strings.Torch; Guard(torchBtn, OnTorch);
            refreshBtn = root.Q<Button>("refresh-btn"); refreshBtn.text = Strings.Refresh; Guard(refreshBtn, OnRefreshMarket);
            bearerHint = root.Q<Label>("bearer-hint"); bearerHint.text = Strings.BearerPending; bearerHint.style.display = DisplayStyle.None;
            questsConfirm = root.Q<Button>("quests-confirm"); questsConfirm.text = Strings.Confirm; Guard(questsConfirm, OnConfirmQuests);
            var questBtn = root.Q<Button>("quest-btn"); AddFace(questBtn, Strings.Quests); questBtn.clicked += OpenQuests;
            oppInfo.RegisterCallback<PointerUpEvent>(_ => OpenQuests()); // details live in the panel
            var qc = root.Q<Button>("quest-close"); qc.text = Strings.Close; qc.clicked += CloseQuests;
            root.Q<Label>("quest-title").text = Strings.Quests;
            var cc = root.Q<Button>("confirm-cancel"); cc.text = Strings.Cancel; cc.clicked += CancelConfirm;
            var ok = root.Q<Button>("confirm-ok"); ok.text = Strings.Confirm; ok.clicked += AcceptConfirm;

            root.Q<Label>("menu-title").text = Strings.Title;
            tutorial = new Tutorial(match, root, match.Close);
            WirePause();
            var quit = root.Q<Button>("menu-quit"); quit.text = Strings.Quit; quit.clicked += Application.Quit;
            var tut = root.Q<Button>("menu-tutorial"); tut.text = Strings.Tutorial;
            tut.clicked += StartTutorial;
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
            screenHandoff.RegisterCallback<PointerUpEvent>(_ => { ResetInput(); match.AcknowledgeHandoff(); });

            var hit = root.Q("board-hit");
            hit.RegisterCallback<PointerDownEvent>(e => OnBoardDown(hit, e));
            hit.RegisterCallback<PointerMoveEvent>(OnBoardMove);
            hit.RegisterCallback<PointerUpEvent>(e => OnBoardUp(hit, e));
            hit.RegisterCallback<PointerCancelEvent>(e => { if (hit.HasPointerCapture(e.pointerId)) hit.ReleasePointer(e.pointerId); EndBoardGesture(); });
            hit.RegisterCallback<WheelEvent>(OnWheel);
            // Card drag (UX-03): the pressed pointer is captured by the root, so its move/up/cancel reach these handlers
            // wherever the finger goes (board, HUD, off-panel). TrickleDown also sees them if the capture was refused.
            // Runs before any handler, also while the root holds a capture from a press whose up was lost.
            root.RegisterCallback<PointerDownEvent>(e => HealStalePointers(e.pointerId), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnCardMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnCardUp, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(OnCardCancel, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureOutEvent>(OnCardCaptureOut);

            match.Changed += Refresh;
            match.EventsApplied += OnEvents;
            Show(Screen.Menu);
        }

        /// <summary>Test/debug hook: the seed of the next match started from the menu (consumed once). Null = clock seed.</summary>
        public ulong? NextSeed { get; set; }

        ulong TakeSeed()
        {
            ulong s = NextSeed ?? (ulong)System.Environment.TickCount;
            NextSeed = null;
            return s;
        }

        void StartHotseat()
        {
            ulong seed = TakeSeed();
            facing = null;
            ClearLog();
            match.NewMatch(seed, timerOn);
        }

        // ---------- UX-13 turn-start bonus notice, UX-14 Portal opened, UX-15 pause menu ----------

        readonly bool[] portalNoticed = new bool[2];
        VisualElement pausePanel;
        Button pauseTimer, pauseVibration;
        const string VibrationPref = "hexportal.vibration";
        public bool Paused => match.Paused;
        public static bool VibrationOn => PlayerPrefs.GetInt(VibrationPref, 1) == 1;

        /// <summary>UX-13: "+n Enerji" / "+n Mana" from held rune stones / wellsprings (PlayerView bonuses), with the turn notice.</summary>
        void TurnStartNotice(PlayerView v)
        {
            int energy = v.TurnStartEnergyBonus, mana = v.TurnStartManaBonus; // from Core (PlayerView), no rule copy
            bool drawSkipped = !v.DrawPending && v.Hand.Count >= Catalog.HandLimit; // D-07: Core skipped the T-04 draw
            if (energy <= 0 && mana <= 0 && !drawSkipped) return;
            string text = Strings.YourTurn;
            if (energy > 0) text += " · " + Strings.EnergyBonus(energy);
            if (mana > 0) text += " · " + Strings.ManaBonus(mana);
            if (drawSkipped) text += " · " + Strings.HandFullDrawSkipped;
            ShowNotice(text, false);
        }

        /// <summary>UX-14: the Portal opens for a player who completed the W-01 number of quests (public, Q-03).</summary>
        void CheckPortal(PlayerView v)
        {
            if (v.Phase != GamePhase.Playing) return;
            var o = v.Viewer.Opponent(); // W-01 (v4.0): the Core decides when a Portal is open (PlayerView), no quest counting here
            if (v.PortalOpen && !portalNoticed[(int)v.Viewer])
            {
                portalNoticed[(int)v.Viewer] = true;
                board.SetPortalOpen(true, Gfx.OwnerColor(v.Viewer));
                ShowNotice(Strings.PortalOpenedYou, false);
            }
            if (v.OpponentPortalOpen && !portalNoticed[(int)o])
            {
                portalNoticed[(int)o] = true;
                board.SetPortalOpen(true, Gfx.OwnerColor(o));
                ShowNotice(Strings.PortalOpenedOpp, true);
            }
        }

        void WirePause()
        {
            pausePanel = root.Q("pause-panel");
            root.Q<Label>("pause-title").text = Strings.PauseTitle;
            var pb = root.Q<Button>("pause-btn"); AddFace(pb, Strings.PauseIcon); pb.clicked += OpenPause;
            var resume = root.Q<Button>("pause-resume"); resume.text = Strings.Resume; resume.clicked += ClosePause;
            pauseTimer = root.Q<Button>("pause-timer");
            pauseTimer.clicked += () => { match.SetTimer(!match.TimerEnabled); SyncPauseTexts(); };
            pauseVibration = root.Q<Button>("pause-vibration");
            pauseVibration.clicked += () => { PlayerPrefs.SetInt(VibrationPref, VibrationOn ? 0 : 1); SyncPauseTexts(); };
            var sound = root.Q<Button>("pause-sound"); sound.text = Strings.SettingSoundSoon; sound.SetEnabled(false); // audio comes later
            var menu = root.Q<Button>("pause-menu"); menu.text = Strings.BackToMenu;
            menu.clicked += () => AskConfirm(Strings.LeaveMatchConfirm, () => { ClosePause(); tutorial.Stop(); match.Close(); });
        }

        void SyncPauseTexts()
        {
            pauseTimer.text = match.TimerEnabled ? Strings.SettingTimerOn : Strings.SettingTimerOff;
            pauseVibration.text = VibrationOn ? Strings.SettingVibrationOn : Strings.SettingVibrationOff;
        }

        /// <summary>UX-15: pause menu (also the Android back button). The clock and the AI wait.</summary>
        public void OpenPause()
        {
            if (!match.HasMatch) return;
            ResetInput(); // UX-16: one panel at a time (closes info)
            questPanel.style.display = DisplayStyle.None;
            match.Paused = true;
            SyncPauseTexts();
            pausePanel.style.display = DisplayStyle.Flex;
        }

        public void ClosePause()
        {
            match.Paused = false;
            pausePanel.style.display = DisplayStyle.None;
            ResetInput();
        }

        /// <summary>UX-05/UX-16: quests panel. Opening it drops the selection, closes the info and pause panels.</summary>
        public void OpenQuests()
        {
            if (!match.HasMatch || Current != Screen.Board || match.Paused) return;
            ResetInput();
            questPanel.style.display = DisplayStyle.Flex;
        }

        public void CloseQuests()
        {
            questPanel.style.display = DisplayStyle.None;
            ResetInput();
        }

        public bool QuestsOpen => questPanel.style.display == DisplayStyle.Flex;
        public bool InfoOpen => infoPanel.style.display == DisplayStyle.Flex;
        public bool PauseOpen => pausePanel.style.display == DisplayStyle.Flex;

        /// <summary>Short vibration when an own unit or the own tower is hit (UX-15 setting).</summary>
        void VibrateOnOwnDamage(DamageDealt d, PlayerView v)
        {
            if (!VibrationOn || d.Amount <= 0) return;
            bool own = d.TargetUnitId == DamageDealt.Tower ? d.SourcePlayer != v.Viewer : false;
            if (!own) foreach (var u in v.OwnUnits) if (u.Id == d.TargetUnitId) { own = true; break; }
            if (!own) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        Tutorial tutorial;
        /// <summary>UX-08 tutorial (menu "Öğretici").</summary>
        public Tutorial Tutorial => tutorial;

        public void StartTutorial()
        {
            facing = null;
            ClearLog();
            tutorial.Start();
        }

        void StartAi(AiLevel level)
        {
            ulong seed = TakeSeed();
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

        /// <summary>A padded touch target (.hit, >= 48 dp) whose visible face is smaller (v4.0 compact HUD).</summary>
        static void AddFace(Button b, string text)
        {
            b.text = "";
            var face = new Label(text) { pickingMode = PickingMode.Ignore, name = b.name + "-face" };
            face.AddToClassList("face");
            b.Add(face);
        }

        // ---------- Safe area (notch / punch-hole camera) ----------

        /// <summary>Test hook: replaces Screen.safeArea (screen pixels, origin bottom-left). Null = the device value.</summary>
        public static Rect? SafeAreaOverride { get; set; }
        Rect appliedSafe;
        Vector2 appliedPanel, appliedScreen;

        /// <summary>Insets the HUD root by the safe area. Re-applied whenever the safe area, the screen or the panel
        /// size changes (orientation flip between Landscape Left and Right moves the notch side). No allocation.</summary>
        void ApplySafeArea()
        {
            var size = root.layout.size;
            if (float.IsNaN(size.x) || size.x < 1f || size.y < 1f) return;
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            if (sw < 1f || sh < 1f) return;
            Rect sa = SafeAreaOverride ?? UnityEngine.Screen.safeArea;
            var screen = new Vector2(sw, sh);
            if (sa == appliedSafe && size == appliedPanel && screen == appliedScreen) return;
            appliedSafe = sa; appliedPanel = size; appliedScreen = screen;
            hud.style.left = Mathf.Max(0f, sa.xMin / sw * size.x);
            hud.style.right = Mathf.Max(0f, (sw - sa.xMax) / sw * size.x);
            hud.style.top = Mathf.Max(0f, (sh - sa.yMax) / sh * size.y);
            hud.style.bottom = Mathf.Max(0f, sa.yMin / sh * size.y);
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
            noticeUntil = Time.unscaledTime + (text == Strings.YourTurn || text == Strings.OpponentTurn ? TurnNoticeSeconds : NoticeSeconds);
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
                if (pausePanel != null) { pausePanel.style.display = DisplayStyle.None; match.Paused = false; }
                questPanel.style.display = DisplayStyle.None;
                HideInfo();
                CancelPointers();
                CancelConfirm();
                notice.style.display = DisplayStyle.None;
                toast.style.display = DisplayStyle.None;
                DrawCallVisible = false;
            }
        }

        void Refresh()
        {
            if (!match.HasMatch) { Show(Screen.Menu); return; }
            var v = match.View;
            int keepPreview = rangePreview ? selectedUnit : -1; // UX-10: a range preview survives the opponent's steps
            if (!PlanningAllowed()) plans.Clear(); // UX-10: arrows are cleared at the start of the player's own turn
            ResetInput(); // after every applied command, turn change or view change: Idle (the hand is rebuilt below; UX-03)
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
            if (shownMatch != match.MatchNumber) { shownMatch = match.MatchNumber; facing = null; noticedTurn = -1; CameraRig.ForgetViews(); portalNoticed[0] = portalNoticed[1] = false; board.SetPortalOpen(false, Color.clear); ClearLog(); }
            CameraRig.SetPlayable(v.TemplateId, v.PlayableCells); // B-25: frame, clamp and pick on the template's cells
            // UX-16: zoom and pan survive turn changes. The default framing only at match start (per side in hotseat,
            // B-05) and on a double tap; a hotseat side change restores that side's last view.
            if (facing != v.Viewer) { CameraRig.Face(cam, v.Viewer); facing = v.Viewer; }
            bool myTurnStart = v.Phase == GamePhase.Playing && v.ActivePlayer == v.Viewer && v.TurnIndex != noticedTurn;
            if (myTurnStart) noticedTurn = v.TurnIndex;
            if (myTurnStart) TurnStartNotice(v);
            CheckPortal(v);
            board.Render(v);
            units.Render(v);
            if (v.Phase == GamePhase.Setup && v.SetupStep == SetupStep.Quests) { BuildQuestOptions(v); Show(Screen.Quests); return; }
            if (v.Phase == GamePhase.Setup && v.SetupStep == SetupStep.Passive) { BuildPassiveOptions(v); Show(Screen.Passive); return; }
            Show(Screen.Board);
            RefreshHud(v);
            ShowDefaultHighlights();
            if (keepPreview >= 0 && IsOpponentTurn(v))
                foreach (var u in v.OwnUnits)
                    if (u.Id == keepPreview) { SelectUnit(keepPreview); break; }
        }

        static bool IsOpponentTurn(PlayerView v) => v.Phase == GamePhase.Playing && v.ActivePlayer != v.Viewer;

        int noticedTurn = -1;

        /// <summary>UX-10 plan arrows only exist vs the AI on its turn (in hotseat the waiting player does not hold the device).</summary>
        bool PlanningAllowed() => match.HasMatch && match.Mode == MatchMode.VsAi && IsOpponentTurn(match.View);

        bool CanPlanFrom(Hex cell)
        {
            if (!PlanningAllowed()) return false;
            foreach (var u in match.View.OwnUnits)
                if (u.Pos == cell) return true;
            return false;
        }

        /// <summary>Draws a plan arrow from an own unit to any board cell (also the gesture's end). Client-only, no command.</summary>
        public bool DrawPlanArrow(Hex from, Hex to)
        {
            if (!CanPlanFrom(from) || from == to || !Board.IsOnBoard(to)) return false;
            plans.Add(from, to);
            return true;
        }

        void BuildQuestOptions(PlayerView v)
        {
            pickedQuests.Clear();
            questOptions.Clear();
            foreach (var id in v.QuestOffer)
            {
                var qid = id;
                var b = new Button { text = ArtCatalog.Name(qid) + "\n" + ArtCatalog.Text(qid), userData = qid };
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

        void OnConfirmQuests() => ApplyCmd(new ChooseQuestsCommand(match.Viewer, pickedQuests));

        void BuildPassiveOptions(PlayerView v)
        {
            passiveOptions.Clear();
            foreach (var id in v.PassiveOffer)
            {
                var pid = id;
                var b = new Button { text = ArtCatalog.Name(pid) + "\n" + ArtCatalog.Text(pid) };
                b.AddToClassList("btn"); b.AddToClassList("option");
                b.clicked += () => ApplyCmd(new ChoosePassiveCommand(match.Viewer, pid));
                passiveOptions.Add(b);
            }
        }

        // ---------- HUD ----------

        void RefreshHud(PlayerView v)
        {
            bool setup = v.Phase == GamePhase.Setup;
            bool myTurn = !setup && v.ActivePlayer == v.Viewer;
            ownTower.text = ArtCatalog.Tower.Icon + " " + v.OwnTowerHealth;
            roundLabel.text = setup ? Strings.Setup : Strings.RoundShort + v.Round;
            oppTower.text = ArtCatalog.Tower.Icon + " " + v.EnemyTowerHealth;
            oppHand.text = Strings.HandIcon + " " + v.OpponentHandCount;
            oppQuests.text = Strings.QuestDone + v.OpponentCompletedQuests.Count + " " + Strings.QuestFail + v.OpponentFailedQuests.Count; // Q-03, Q-04, V-09
            turnBanner.text = setup ? Strings.Setup + " · " + Strings.ManaIcon + " " + v.SetupMana + "/" + Catalog.SetupMana : myTurn ? Strings.YourTurn : Strings.OpponentTurn;
            turnBanner.EnableInClassList("mine", myTurn);
            turnBanner.EnableInClassList("theirs", !setup && !myTurn);
            eventBanner.style.display = v.AnnouncedEvent != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (v.AnnouncedEvent != null) eventBanner.text = Strings.EventIcon + " " + ArtCatalog.Name(v.AnnouncedEvent) + " · " + Strings.RoundShort + v.AnnouncedEventRound;

            // S-05 (v3.0): during setup the Mana meter shows the setup budget left.
            manaText.text = setup ? v.SetupMana + "/" + Catalog.SetupMana : v.Mana.ToString();
            manaCaption.text = Strings.SetupMana;
            manaCaption.style.display = setup ? DisplayStyle.Flex : DisplayStyle.None;
            energyText.text = v.Energy.ToString();
            if (setup) Pips(manaPips, v.SetupMana, Mathf.Max(v.SetupMana, Catalog.SetupMana), "mana-pip");
            else Pips(manaPips, v.Mana, Mathf.Max(v.Mana, Catalog.ManaCap), "mana-pip");
            Pips(energyPips, v.Energy, Catalog.EnergyPerTurn, "energy-pip");

            for (int i = 0; i < 3; i++)
            {
                var card = v.Market[i];
                market[i].text = card == null ? Strings.SlotName(i) + "\n" + Strings.Empty
                    : CardIcon(card) + " " + card.Cost + "\n" + CardName(card);
                SetOn(market[i], !setup && HasDraw(i), DrawReason(v, card == null));
            }
            SetOn(blindBtn, !setup && HasDraw(DrawCommand.Blind), DrawReason(v, false));
            // D-05 (v4.0): manual refresh, also before the draw; the engine decides (legal list).
            SetOn(refreshBtn, Find<RefreshMarketCommand>() != null, setup ? Strings.ReasonSetupNoDraw : !myTurn ? Strings.ReasonOpponentTurn
                : v.Mana < Catalog.MarketRefreshCost ? Strings.ReasonNoMana : Strings.ReasonNotNow);
            bearerHint.style.display = !setup && v.OwnBearerPending ? DisplayStyle.Flex : DisplayStyle.None; // U-44
            // T-04 step 4: the mandatory draw is called out and the Market/blind area glows until it is done.
            bool call = DrawCallVisible = myTurn && v.DrawPending;
            drawCall.style.display = call ? DisplayStyle.Flex : DisplayStyle.None;
            marketBox.EnableInClassList("call", call);

            bool canEnd = setup ? Find<FinishSetupCommand>() != null : Find<EndTurnCommand>() != null;
            string finishWhy = SetupFinishReason(v);
            endBtn.text = !setup ? Strings.EndTurn : canEnd ? Strings.FinishSetup : Strings.FinishSetup + "\n" + finishWhy;
            endBtn.EnableInClassList("with-reason", setup && !canEnd);
            SetOn(endBtn, canEnd, setup ? finishWhy : !myTurn ? Strings.ReasonOpponentTurn
                : v.DrawPending ? Strings.ReasonDrawFirst : Strings.ReasonNotNow);

            var q = new System.Text.StringBuilder();
            q.Append(Strings.Quests).Append(":\n");
            foreach (var qp in v.QuestProgress)
            {
                q.Append("• ").Append(ArtCatalog.Name(qp.Id)).Append(" — ").Append(ArtCatalog.Text(qp.Id)).Append(" [");
                // PM: the counter only for Active quests; Completed/Failed show the status alone.
                if (qp.Status == QuestStatus.Active) q.Append(qp.Current).Append('/').Append(qp.Target);
                else q.Append(Strings.Status(qp.Status));
                q.Append("]\n");
            }
            q.Append('\n').Append(Strings.Passive).Append(v.PassiveChoice == null ? "-" : ArtCatalog.Name(v.PassiveChoice) + " — " + ArtCatalog.Text(v.PassiveChoice));
            q.Append("\n\n").Append(Strings.Player(v.Viewer.Opponent())).Append(": ").Append(Strings.Tower).Append(' ').Append(v.EnemyTowerHealth)
                .Append(" · ").Append(Strings.Hand).Append(' ').Append(v.OpponentHandCount);
            if (v.OpponentPassive != null) q.Append('\n').Append(Strings.OppPassive).Append(ArtCatalog.Name(v.OpponentPassive));
            q.Append('\n').Append(Strings.OppQuests).Append(v.OpponentCompletedQuests.Count);
            foreach (var id in v.OpponentCompletedQuests) q.Append(' ').Append(ArtCatalog.Name(id));
            q.Append('\n').Append(Strings.OppFailed).Append(v.OpponentFailedQuests.Count); // Q-04, V-09
            foreach (var id in v.OpponentFailedQuests) q.Append(' ').Append(ArtCatalog.Name(id));
            if (v.AnnouncedEvent != null)
                q.Append("\n\n").Append(Strings.Event).Append(ArtCatalog.Name(v.AnnouncedEvent)).Append(" (").Append(Strings.Round).Append(v.AnnouncedEventRound).Append(')');
            questText.text = q.ToString();

            BuildHand(v);
            UpdatePrompt();
        }

        static string DrawReason(PlayerView v, bool emptySlot)
        {
            if (v.Phase == GamePhase.Setup) return Strings.ReasonSetupNoDraw;
            if (v.ActivePlayer != v.Viewer) return Strings.ReasonOpponentTurn; // T-11 removed (v4.0): no draw choice on the opponent's turn
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
            // UX-15: the Android back button arrives as Escape.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Current == Screen.Board && match.HasMatch) { if (Paused) ClosePause(); else OpenPause(); }
            ApplySafeArea();
            WatchdogTick();
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
            c.IsCharacter ? ArtCatalog.UnitName(c.Character.Class) : ArtCatalog.Name(c.Support.Id);

        static string CardIcon(CardInstance c) =>
            c.IsCharacter ? ArtCatalog.UnitIcon(c.Character.Class) : ArtCatalog.CardIcon(c.Support.Category);

        /// <summary>S-05/S-06 (v3.0): why "Finish setup" is off (text only; the legal list decides).</summary>
        static string SetupFinishReason(PlayerView v)
        {
            if (!v.OwnTowerPos.HasValue) return Strings.ReasonPlaceTowerFirst;
            if (v.OwnUnits.Count < Catalog.MinPlacedCharacters) return Strings.ReasonMinCharacters;
            return Strings.ReasonFinishSetup;
        }

        /// <summary>UX-04 (v3.0) hand group: 0 traps (left), 1 characters (middle), 2 buff/debuff (right).</summary>
        static int HandGroup(CardInstance c) =>
            c.IsCharacter ? 1 : c.Support.Category == CardCategory.Trap ? 0 : 2;

        void BuildHand(PlayerView v)
        {
            handTraps.Clear(); handChars.Clear(); handBuffs.Clear();
            handCards.Clear();
            int n = v.Hand.Count;
            int groups = 0;
            for (int g = 0; g < 3; g++)
                foreach (var c in v.Hand)
                    if (HandGroup(c) == g) { groups++; break; }
            // More cards than fit (setup deals more than the play-time limit): overlap them, keep each card full size.
            float width = hand.layout.width > 1f ? hand.layout.width : HandWidth; // actual width on wide/narrow screens
            float overlap = n > 1 ? Mathf.Max(0f, (n * CardStride + groups * GroupGap - width) / (n - 1)) : 0f;
            bool setup = v.Phase == GamePhase.Setup;
            for (int g = 0; g < 3; g++)
            {
                var parent = g == 0 ? handTraps : g == 1 ? handChars : handBuffs;
                foreach (var card in v.Hand)
                {
                    if (HandGroup(card) != g) continue;
                    bool overBudget = setup && card.Cost > v.SetupMana; // S-05: does not fit the setup budget
                    var el = MakeCard(card, overBudget ? Strings.CardNoteSetupMana : "");
                    int id = card.Id;
                    el.userData = id;
                    el.EnableInClassList("unplayable", !HasCardCommand(id));
                    el.EnableInClassList("selected", id == selectedCard);
                    el.EnableInClassList("over-budget", overBudget);
                    if (parent.childCount > 0 && overlap > 0f) el.style.marginLeft = -overlap;
                    el.RegisterCallback<PointerDownEvent>(e => OnCardDown(id, e));
                    parent.Add(el);
                    handCards.Add(el);
                }
            }
        }

        /// <summary>The hand card element for a card id (tests/CLI), or null.</summary>
        public VisualElement CardElement(int cardId)
        {
            foreach (var el in handCards)
                if (el.userData is int id && id == cardId) return el;
            return null;
        }

        /// <summary>Hand card ids per group, left to right (UX-04 test hook).</summary>
        public int HandGroupOf(int cardId)
        {
            var el = CardElement(cardId);
            if (el == null) return -1;
            return el.parent == handTraps ? 0 : el.parent == handChars ? 1 : 2;
        }

        /// <summary>UX-07 minimal card, one row per part so nothing can overlap in any state: [cost | icon], name,
        /// stats (characters: Attack/Health/Move), note (S-05 budget). Each row has a fixed height and clips with an
        /// ellipsis. The description is in the long-press detail (UX-11).</summary>
        static VisualElement MakeCard(CardInstance card, string note)
        {
            var el = new VisualElement();
            el.AddToClassList("card");
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = FrameColor(card);
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("card-top");
            var cost = new Label(card.Cost.ToString()) { pickingMode = PickingMode.Ignore, name = "card-cost" };
            cost.AddToClassList("card-cost");
            var icon = new Label(CardIcon(card)) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("card-icon");
            if (card.IsCharacter) icon.style.color = Color.Lerp(Gfx.BiomeColor(card.Character.Biome), Color.black, 0.35f);
            top.Add(cost); top.Add(icon);
            var name = new Label(CardName(card)) { pickingMode = PickingMode.Ignore, name = "card-name" };
            name.AddToClassList("card-name");
            string statText = "";
            if (card.IsCharacter)
            {
                var d = Unit.DefOf(card.Character.Class);
                statText = Strings.AttackIcon + d.Attack + " " + Strings.HealthIcon + d.Health + " " + Strings.MoveIcon + d.Move;
            }
            var stats = new Label(statText) { pickingMode = PickingMode.Ignore, name = "card-stats" };
            stats.AddToClassList("card-stats");
            var noteLabel = new Label(note) { pickingMode = PickingMode.Ignore, name = "card-note" };
            noteLabel.AddToClassList("card-note");
            el.Add(top); el.Add(name); el.Add(stats); el.Add(noteLabel);
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
            if (secondPick.Count > 0)
                text = secondPick[0] is PlayCardCommand pc && pc.Dest.HasValue ? Strings.PickDestination : Strings.PickDirection;
            else if (rangePreview) text = Strings.RangePreview;
            else if (v.Phase == GamePhase.Setup) text = v.SetupStep == SetupStep.Tower ? Strings.PlaceTower : Strings.PlaceUnits;
            else if (v.DrawPending && v.ActivePlayer == v.Viewer) text = Strings.DrawFirst;
            else if (selectedUnit >= 0) text = Strings.UnitLegend;
            prompt.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
            prompt.text = text ?? "";
            overwatchBtn.style.display = selectedUnit >= 0 && FindOverwatch(selectedUnit) != null ? DisplayStyle.Flex : DisplayStyle.None;
            torchBtn.style.display = selectedUnit >= 0 && FindTorch(selectedUnit) != null ? DisplayStyle.Flex : DisplayStyle.None; // V-12
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
                if (c is DrawCommand d && d.Slot == slot) return true;
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
                if (c is DrawCommand d && d.Slot == slot) { ApplyCmd(c); return; }
        }

        void OnEnd()
        {
            var cmd = (ICommand)Find<FinishSetupCommand>() ?? Find<EndTurnCommand>();
            if (cmd != null) ApplyCmd(cmd);
        }

        PlaceTorchCommand FindTorch(int unitId)
        {
            foreach (var c in match.Legal)
                if (c is PlaceTorchCommand t && t.UnitId == unitId) return t;
            return null;
        }

        void OnTorch()
        {
            var t = FindTorch(selectedUnit);
            if (t != null) ApplyCmd(t);
        }

        void OnRefreshMarket()
        {
            var r = Find<RefreshMarketCommand>();
            if (r != null) ApplyCmd(r);
        }

        /// <summary>D-05: the three slots flip (USS transition) when the Market is refreshed.</summary>
        void AnimateMarket()
        {
            MarketRefreshAnimations++;
            for (int i = 0; i < 3; i++)
            {
                var b = market[i];
                b.AddToClassList("flip");
                b.schedule.Execute(() => b.RemoveFromClassList("flip")).StartingIn(250);
            }
        }

        void OnOverwatch()
        {
            var o = FindOverwatch(selectedUnit);
            if (o != null) ApplyCmd(o);
        }

        // ---------- Selection and highlights ----------

        void ClearSelection()
        {
            selectedUnit = -1;
            selectedCard = -1;
            secondPick.Clear();
            previewOnly.Clear();
            rangePreview = false;
            HideInfo();
            SyncSelectionMode();
            if (match.HasMatch && !match.HandoffPending && Current == Screen.Board) ShowDefaultHighlights();
        }

        /// <summary>UX-12: stat bars are hidden while a card or unit is selected (or a card is dragged).</summary>
        void SyncSelectionMode()
        {
            bool on = selectedUnit >= 0 || selectedCard >= 0 || dragging;
            units.SetBarsHidden(on);
            hand.EnableInClassList("selecting", on && !dragging); // UX-12: hand out of the way (not while a card is dragged from it)
        }

        void ShowDefaultHighlights()
        {
            board.ClearHighlights();
            foreach (var c in match.Legal)
                if (c is PlaceTowerCommand t) board.Highlight(t.Cell, Gfx.MoveHl);
            foreach (var el in handCards) el.RemoveFromClassList("selected");
            UpdatePrompt();
        }

        /// <summary>UX-02 and T-05 (v3.2): blue moves, red direct attacks and orange move-attack targets, all from the
        /// legal list. If only the Energy is missing for a move-attack, its targets (Preview) are shown faint and a tap
        /// explains why. On the opponent's turn the unit only previews its ranges (UX-10).</summary>
        void SelectUnit(int unitId)
        {
            var v = match.View;
            selectedUnit = unitId;
            selectedCard = -1;
            secondPick.Clear();
            previewOnly.Clear();
            redCells.Clear();
            rangePreview = IsOpponentTurn(v);
            board.ClearHighlights();
            int n = 0;
            bool anyMoveAttack = false;
            if (rangePreview) ShowRanges(unitId);
            else
            {
                foreach (var c in match.Legal)
                {
                    if (c is MoveCommand m && m.UnitId == unitId) { board.Highlight(m.Dest, Gfx.MoveHl); n++; }
                    else if (c is AttackCommand a && a.UnitId == unitId) { board.Highlight(a.Target, Gfx.AttackHl); redCells.Add(a.Target); n++; }
                    else if (c is OverwatchCommand o && o.UnitId == unitId) n++;
                }
                foreach (var c in match.Legal) // a target that is also a direct attack stays red (1 Energy beats 2)
                    if (c is MoveAttackCommand ma && ma.UnitId == unitId && !redCells.Contains(ma.Target))
                    {
                        board.Highlight(ma.Target, Gfx.MoveAttackHl);
                        anyMoveAttack = true;
                        n++;
                    }
                if (n > 0 && !anyMoveAttack && v.Energy < Catalog.MoveAttackEnergy)
                    foreach (var o in Preview.MoveAttackTargets(v, unitId))
                        if (!redCells.Contains(o.Target)) { board.Highlight(o.Target, Gfx.MoveAttackOffHl); previewOnly.Add(o.Target); }
                if (n == 0) ShowToast(UnitReason(unitId));
            }
            foreach (var u in v.OwnUnits)
                if (u.Id == unitId) ShowUnitInfo(u, v);
            SyncSelectionMode();
            UpdatePrompt();
        }

        /// <summary>UX-10: the own unit's ranges from Core's Preview (move blue, reach orange, attack-from-here red). No commands.</summary>
        void ShowRanges(int unitId)
        {
            var r = Preview.Ranges(match.View, unitId);
            if (r == null) return;
            foreach (var h in r.MoveAttackCells) board.Highlight(h, Gfx.MoveAttackOffHl);
            foreach (var h in r.MoveCells) board.Highlight(h, Gfx.PreviewMoveHl);
            foreach (var h in r.AttackCells) board.Highlight(h, Gfx.PreviewAttackHl);
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
                if (c.Id == cardId)
                {
                    if (v.Phase == GamePhase.Playing && c.Cost > v.Mana) return Strings.ReasonNoMana;
                    if (v.Phase == GamePhase.Setup && !v.OwnTowerPos.HasValue) return Strings.ReasonPlaceTowerFirst;
                    if (v.Phase == GamePhase.Setup && c.Cost > v.SetupMana) return Strings.ReasonSetupMana;
                }
            return Strings.ReasonNoTarget;
        }

        void SelectCard(int cardId)
        {
            selectedCard = cardId;
            selectedUnit = -1;
            secondPick.Clear();
            previewOnly.Clear();
            rangePreview = false;
            board.ClearHighlights();
            if (match.View.Phase == GamePhase.Playing) // C-02, UX-03 (setup uses the home zone, shown by the targets)
                foreach (var z in match.View.ControlZone) board.Highlight(z, Gfx.ZoneHl);
            foreach (var c in match.Legal) // S-05 tower step: the tower cells stay blue while a card is looked at
                if (c is PlaceTowerCommand t) board.Highlight(t.Cell, Gfx.MoveHl);
            foreach (var c in match.Legal)
                if (CardOf(c) == cardId && CellOf(c, out var h)) board.Highlight(h, Gfx.TargetHl);
            foreach (var el in handCards)
                el.EnableInClassList("selected", el.userData is int id && id == cardId);
            foreach (var c in match.View.Hand)
                if (c.Id == cardId) ShowCardInfo(c);
            if (!HasCardCommand(cardId)) ShowToast(CardReason(cardId));
            SyncSelectionMode();
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

        /// <summary>The ONE entry point for a board tap, in every phase (setup tower/units/traps and play). It dispatches
        /// on the input mode; every branch either sends a legal command (ApplyCmd → Idle), changes the selection, or
        /// falls back to <see cref="TapIdle"/>. No other path places a tower, a unit or a card.</summary>
        void OnCellTapped(Hex cell)
        {
            switch (Mode)
            {
                case InputMode.Locked:
                    if (QuestsOpen) CloseQuests(); // UX-16: a tap outside the quests panel closes it
                    return;
                case InputMode.Confirm:
                case InputMode.Dragging:
                    return;
                case InputMode.SecondPick: // C-15 destination / C-18 direction; a tap anywhere else cancels
                    foreach (var c in secondPick)
                        if (SecondPickCell(c) == cell) { ApplyCmd(c); return; }
                    ClearSelection();
                    return;
                case InputMode.CardSelected:
                    if (TryCardOnCell(selectedCard, cell)) return;
                    break; // not a target of the card: the tap means what it means in Idle (tower cell, own unit, info)
                case InputMode.UnitSelected:
                    if (TryUnitOnCell(cell)) return;
                    break;
            }
            TapIdle(cell);
        }

        /// <summary>Idle tap: tower placement (setup), select an own unit, else drop the selection and show the cell's info.</summary>
        void TapIdle(Hex cell)
        {
            if (PlanningAllowed() && plans.RemoveEndingAt(cell)) return; // UX-10: a tap on an arrow head deletes it
            foreach (var c in match.Legal)
                if (c is PlaceTowerCommand t && t.Cell == cell) { ApplyCmd(c); return; }
            foreach (var u in match.View.OwnUnits)
                if (u.Pos == cell) { if (selectedUnit == u.Id && !rangePreview) ClearSelection(); else SelectUnit(u.Id); return; }
            ClearSelection();
            ShowCellInfo(cell); // UX-11: enemy units, ghosts and towers (only what the view holds)
        }

        /// <summary>UX-02 / T-05: move, attack or move-attack of the selected unit on the cell (legal list only).</summary>
        bool TryUnitOnCell(Hex cell)
        {
            if (rangePreview) return false;
            foreach (var c in match.Legal)
                if ((c is MoveCommand m && m.UnitId == selectedUnit && m.Dest == cell)
                    || (c is AttackCommand a && a.UnitId == selectedUnit && a.Target == cell)) { ApplyCmd(c); return true; }
            // T-05 (v3.4): one move-attack per target; the engine computes the approach. One tap on the orange target.
            foreach (var c in match.Legal)
                if (c is MoveAttackCommand ma && ma.UnitId == selectedUnit && ma.Target == cell) { ApplyCmd(c); return true; }
            if (previewOnly.Contains(cell)) { ShowToast(Strings.ReasonMoveAttackEnergy); return true; }
            return false;
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
            if (options.Count == 1) { ApplyCmd(options[0]); return; }
            secondPick.Clear();
            board.ClearHighlights();
            board.Highlight(cell, Gfx.TargetHl);
            foreach (var c in options)
            {
                var h = SecondPickCell(c);
                if (Board.IsOnBoard(h)) { secondPick.Add(c); board.Highlight(h, Gfx.MoveHl); }
            }
            if (secondPick.Count == 0) ApplyCmd(options[0]);
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
            if (pressCard >= 0) return; // a card is pressed or dragged: board taps and pans wait (UX-03)
            // Root cause (v3.4): the InputForUI provider re-sends ButtonPressed for a touch that moves (its Click action
            // fires again on the touch state change), so the same finger can deliver several PointerDowns. Counting
            // them as fingers turned the gesture into a "pinch" and left the count stuck after the up.
            if (boardIds.Contains(e.pointerId)) return;
            boardIds.Add(e.pointerId);
            boardPointers = boardIds.Count;
            hit.CapturePointer(e.pointerId);
            if (boardPointers > 1) { boardDragged = true; return; } // second finger: a pinch, never a tap
            boardPointer = e.pointerId;
            boardPress = boardLast = e.position;
            boardDragged = false;
            boardRightDrag = e.button == 1;
            planning = false;
            if (!boardRightDrag && TryPickPanel(e.position, out var pc) && CanPlanFrom(pc)) { planning = true; planFrom = pc; }
        }

        void OnBoardMove(PointerMoveEvent e)
        {
            if (e.pointerId != boardPointer || boardPointers != 1 || pinchActive) return;
            Vector2 p = e.position;
            if (!boardDragged && (p - boardPress).sqrMagnitude > DragThreshold * DragThreshold) boardDragged = true;
            if (boardDragged && CameraRig.Instance != null && !planning) CameraRig.Instance.Pan(PanelToScreen(boardLast), PanelToScreen(p));
            boardLast = p;
        }

        void OnBoardUp(VisualElement hit, PointerUpEvent e)
        {
            if (hit.HasPointerCapture(e.pointerId)) hit.ReleasePointer(e.pointerId);
            if (!boardIds.Remove(e.pointerId)) return; // an up we never counted (or a duplicate)
            boardPointers = boardIds.Count;
            if (e.pointerId != boardPointer) return;
            boardPointer = -1;
            if (planning && boardDragged && !pinchActive)
            {
                planning = false;
                if (TryPickPanel(e.position, out var end)) DrawPlanArrow(planFrom, end);
                return;
            }
            planning = false;
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
        // One card pointer at a time. The pressed pointer is captured by the root, so its move/up/cancel arrive here
        // wherever the finger goes (board, HUD, off-panel); a second finger never starts, steals or ends the drag.
        // Every way out (invalid drop, PointerCancel, capture lost, focus loss, screen change, Refresh) returns the
        // card to the hand. Root cause of the old "stuck" drag: no capture, so over the board the events went to
        // board-hit (a sibling of the HUD) and the HUD's TrickleDown handlers missed the up.

        void OnCardDown(int cardId, PointerDownEvent e)
        {
            if (pressCard >= 0 || Mode == InputMode.Locked || Mode == InputMode.Confirm) return; // second finger, a dialog, a panel or the pause menu
            if (e.pointerType == UnityEngine.UIElements.PointerType.mouse && e.button != 0) return;
            pressCard = cardId;
            pressPointer = e.pointerId;
            pressPos = e.position;
            dragging = false;
            longPressed = false;
            root.CapturePointer(e.pointerId);
            longPressJob?.Pause();
            longPressJob = root.schedule.Execute(() =>
            {
                if (pressCard != cardId || dragging) return;
                longPressed = true;
                foreach (var c in match.View.Hand)
                    if (c.Id == cardId) ShowCardInfo(c); // UX-03 long press: card detail in the UX-11 panel
            }).StartingIn((long)LongPressMs);
            e.StopPropagation();
        }

        void OnCardMove(PointerMoveEvent e)
        {
            if (pressCard < 0 || e.pointerId != pressPointer || longPressed) return;
            var holder = root.panel?.GetCapturingElement(e.pointerId) as VisualElement; // capture lost to another element: back to the hand
            if (holder != null && holder != root) { CancelCardPointer(); return; }
            if (!dragging && (((Vector2)e.position) - pressPos).sqrMagnitude > DragThreshold * DragThreshold)
            {
                dragging = true;
                SelectCard(pressCard);
                dragCard.Clear();
                foreach (var c in match.View.Hand)
                    if (c.Id == pressCard)
                    {
                        var el = MakeCard(c, "");
                        el.style.marginLeft = 0;
                        el.pickingMode = PickingMode.Ignore; // the ghost card must never take pointer events
                        dragCard.Add(el);
                    }
                dragCard.style.display = DisplayStyle.Flex;
            }
            if (dragging)
            {
                dragCard.style.left = e.position.x - DragOffsetX; // root coordinates (drag-card is outside the inset HUD)
                dragCard.style.top = e.position.y - DragOffsetY;
            }
        }

        void OnCardUp(PointerUpEvent e)
        {
            if (pressCard < 0 || e.pointerId != pressPointer) return;
            int card = pressCard;
            bool wasDragging = dragging, wasLong = longPressed;
            EndCardPointer();
            if (wasLong) return;
            if (wasDragging)
            {
                if (!(TryPickPanel(e.position, out var cell) && TryCardOnCell(card, cell))) ClearSelection(); // back to the hand
                return;
            }
            TapCard(card);
        }

        void OnCardCancel(PointerCancelEvent e)
        {
            if (pressCard >= 0 && e.pointerId == pressPointer) CancelCardPointer();
        }

        void OnCardCaptureOut(PointerCaptureOutEvent e)
        {
            if (pressCard >= 0) CancelCardPointer(); // someone else took the pointer: never leave a card hanging
        }

        /// <summary>Clears the card pointer state first, then releases the capture (so the capture-out is a no-op).</summary>
        void EndCardPointer()
        {
            int pointer = pressPointer;
            pressCard = -1;
            pressPointer = -1;
            dragging = false;
            longPressed = false;
            longPressJob?.Pause();
            dragCard.style.display = DisplayStyle.None;
            dragCard.Clear();
            if (pointer >= 0 && root.HasPointerCapture(pointer)) root.ReleasePointer(pointer);
        }

        void CancelCardPointer()
        {
            bool wasDragging = dragging;
            EndCardPointer();
            if (wasDragging) ClearSelection(); // the card returns to the hand
            else SyncSelectionMode();
        }

        /// <summary>Drops every pointer gesture (focus loss, pause, screen change, Refresh). Safe to call any time.</summary>
        public void CancelPointers()
        {
            if (pressCard >= 0) CancelCardPointer();
            EndBoardGesture();
        }

        /// <summary>Ends the board gesture (tap/pan/plan/pinch bookkeeping).</summary>
        void EndBoardGesture()
        {
            boardIds.Clear();
            boardPointers = 0;
            boardPointer = -1;
            pinchActive = false;
            planning = false;
        }

        /// <summary>
        /// Root cause of the v3.4 device reports ("first tap not on the tower bugs out", "characters can't be placed by
        /// tap or drag", "pause → resume fixes it"). The project-wide UI actions feed UI Toolkit through the InputForUI
        /// provider, whose Click action re-fires while a touch MOVES, so one finger delivers duplicate PointerDowns:
        /// <list type="bullet">
        /// <item>On the board each duplicate was counted as another finger: boardPointers became 2, the gesture was read
        /// as a pinch (no tap, no pan) and after the up the count stayed at 1, so every later tap was again "the second
        /// finger" and was dropped.</item>
        /// <item>On a card the duplicate reached the root (which holds the drag capture) and the old "same pointer id
        /// pressed again = its previous press is over" heal cancelled the drag in mid-air.</item>
        /// </list>
        /// A finger that never moves sends no duplicate, which is why a clean first tap on a tower cell worked, and only
        /// ResetInput (pause, or a successful command → Refresh) cleared the stuck count. Now board downs are counted
        /// per pointer id (duplicates ignored), a same-id down while a finger is on the screen is a duplicate (never a reset), a press on another id heals
        /// stale state only when no other finger is physically down, and <see cref="WatchdogTick"/> ends any gesture
        /// whose up was really lost once nothing touches the screen.
        /// </summary>
        void HealStalePointers(int pointerId)
        {
            bool tracked = (pressCard >= 0 && pressPointer == pointerId) || boardIds.Contains(pointerId);
            int presses = PhysicalPresses();
            if (tracked && presses > 0) return;   // duplicate down of a finger that is still on the screen
            if (!tracked && presses != 1) return; // another finger is also down (pinch, second finger): keep the gesture
            if (pressCard >= 0) CancelCardPointer();
            if (boardPointers > 0 || pinchActive) EndBoardGesture();
        }

        /// <summary>Fingers on the screen plus pressed mouse buttons, read from the Input System (no allocation).</summary>
        static int PhysicalPresses()
        {
            int n = 0;
            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var ph = touches[i].phase;
                if (ph != UnityEngine.InputSystem.TouchPhase.Ended && ph != UnityEngine.InputSystem.TouchPhase.Canceled) n++;
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) n++;
            return n;
        }

        int idleFrames;
        /// <summary>Per frame: a gesture that is still "in progress" while nothing has touched the screen for a few
        /// frames lost its up; end it (a drag returns its card to the hand, UX-03).</summary>
        void WatchdogTick()
        {
            if (pressCard < 0 && boardPointers == 0 && !pinchActive) { idleFrames = 0; return; }
            if (PhysicalPresses() > 0) { idleFrames = 0; return; }
            if (++idleFrames < 2) return;
            idleFrames = 0;
            if (pressCard >= 0) CancelCardPointer();
            EndBoardGesture();
        }

        /// <summary>Client input state, derived from the gesture/selection fields (one source of truth).</summary>
        public enum InputMode { Locked, Idle, UnitSelected, CardSelected, SecondPick, Dragging, Confirm }

        public InputMode Mode
        {
            get
            {
                if (!match.HasMatch || match.HandoffPending || match.IsOver || Current != Screen.Board) return InputMode.Locked;
                if (match.Paused) return InputMode.Locked;
                if (QuestsOpen) return InputMode.Locked;
                if (confirmAction != null) return InputMode.Confirm;
                if (dragging) return InputMode.Dragging;
                if (secondPick.Count > 0) return InputMode.SecondPick;
                if (selectedCard >= 0) return InputMode.CardSelected;
                if (selectedUnit >= 0) return InputMode.UnitSelected;
                return InputMode.Idle;
            }
        }

        /// <summary>Back to Idle: drops gestures, the confirm dialog, second picks and the selection.</summary>
        void ResetInput()
        {
            CancelPointers();
            planning = false;
            if (confirmAction != null) { confirmAction = null; confirmPanel.style.display = DisplayStyle.None; }
            ClearSelection();
        }

        /// <summary>Every command from the board/hand goes through here: whatever the result, input returns to Idle
        /// (a rejected command used to leave a half-finished selection behind).</summary>
        bool ApplyCmd(ICommand c)
        {
            bool ok = match.Apply(c);
            if (!ok) ResetInput();
            return ok;
        }

        /// <summary>A card press or drag is in progress (tests/CLI).</summary>
        public bool CardPointerActive => pressCard >= 0;
        public bool Dragging => dragging;
        public bool DragGhostVisible => dragCard.style.display == DisplayStyle.Flex;

        // ---------- UX-11 info panel (left). Only PlayerView data and class/card definitions; enemies show what is visible. ----------

        /// <summary>Text of the open info panel ("" when closed), for tests/CLI.</summary>
        public string InfoText => infoPanel.style.display == DisplayStyle.Flex ? infoTitle.text + "\n" + infoText.text : "";

        void HideInfo()
        {
            infoPanel.style.display = DisplayStyle.None;
            tutorial?.Yield(false);
        }

        void OpenInfo(string icon, Color iconColor, string title, string body)
        {
            questPanel.style.display = DisplayStyle.None; // UX-16: one panel at a time
            tutorial?.Yield(true);
            infoIcon.text = icon;
            infoIcon.style.color = iconColor;
            infoTitle.text = title;
            infoText.text = body;
            infoPanel.style.display = DisplayStyle.Flex;
        }

        void ShowUnitInfo(UnitView u, PlayerView v)
        {
            var d = Unit.DefOf(u.Class);
            bool own = u.Owner == v.Viewer;
            var s = new System.Text.StringBuilder();
            s.Append(own ? Strings.InfoMyUnit : Strings.InfoEnemyUnit).Append(" · ").Append(ArtCatalog.BiomeName(u.Biome));
            if (u.IsGhost) s.Append('\n').Append(Strings.InfoGhost);
            s.Append('\n').Append(Strings.InfoHealth).Append(' ').Append(u.Health).Append('/').Append(d.Health)
                .Append("   ").Append(Strings.InfoAttack).Append(' ').Append(u.EffectiveAttack);
            if (u.ChargeBonus > 0) s.Append(ArtCatalog.ChargeNote);
            s.Append('\n').Append(Strings.InfoMove).Append(' ').Append(d.Move);
            if (own && u.MoveBonus > 0) s.Append(" (+").Append(u.MoveBonus).Append(')');
            s.Append("   ").Append(Strings.InfoSight).Append(' ').Append(d.Sight);
            s.Append('\n').Append(Strings.InfoRange).Append(": ").Append(Strings.RangeText(d.Range, d.MinRange, d.MaxRange));
            if (!u.IsGhost) // V-05: ghosts carry no effects
            {
                AppendEffect(s, "Buff", u.BuffId, u.BuffTurnsLeft);
                AppendEffect(s, "Debuff", u.DebuffId, u.DebuffTurnsLeft);
                if (u.OnOverwatch) s.Append('\n').Append(Strings.InfoOverwatch);
            }
            if (own && u.ActedThisTurn) s.Append('\n').Append(Strings.InfoActed);
            if (own) s.Append('\n').Append(Strings.InfoActions);
            s.Append('\n').Append(Strings.InfoCost).Append(' ').Append(Strings.ManaIcon).Append(' ').Append(d.Cost);
            s.Append('\n').Append(ArtCatalog.Ability(u.Class));
            OpenInfo(ArtCatalog.UnitIcon(u.Class), Gfx.BiomeColor(u.Biome), ArtCatalog.UnitName(u.Class), s.ToString());
        }

        static void AppendEffect(System.Text.StringBuilder s, string kind, string id, int turns)
        {
            if (id == null) return;
            s.Append('\n').Append(kind).Append(": ").Append(ArtCatalog.Name(id)).Append(" (");
            if (turns > 0) s.Append(turns).Append(Strings.InfoTurnsLeft); else s.Append(Strings.InfoLasting);
            s.Append(')');
        }

        void ShowCardInfo(CardInstance c)
        {
            var s = new System.Text.StringBuilder();
            s.Append(Strings.InfoCost).Append(' ').Append(Strings.ManaIcon).Append(' ').Append(c.Cost);
            if (c.IsCharacter)
            {
                var d = Unit.DefOf(c.Character.Class);
                s.Append(" · ").Append(ArtCatalog.BiomeName(c.Character.Biome));
                s.Append('\n').Append(Strings.InfoHealth).Append(' ').Append(d.Health).Append("   ").Append(Strings.InfoAttack).Append(' ').Append(d.Attack)
                    .Append("\n").Append(Strings.InfoMove).Append(' ').Append(d.Move).Append("   ").Append(Strings.InfoSight).Append(' ').Append(d.Sight)
                    .Append('\n').Append(Strings.InfoRange).Append(": ").Append(Strings.RangeText(d.Range, d.MinRange, d.MaxRange))
                    .Append('\n').Append(ArtCatalog.Ability(c.Character.Class));
                OpenInfo(CardIcon(c), Gfx.BiomeColor(c.Character.Biome), CardName(c), s.ToString());
            }
            else
            {
                s.Append('\n').Append(ArtCatalog.Text(c.Support.Id));
                OpenInfo(CardIcon(c), FrameColor(c), CardName(c), s.ToString());
            }
        }

        /// <summary>A tap on a non-own cell: enemy unit or ghost, or a tower, as the view holds it.</summary>
        void ShowCellInfo(Hex cell)
        {
            var v = match.View;
            int i = Board.IndexOf(cell);
            if (i < 0) return;
            var c = v.Cells[i];
            if (c.Unit != null) { ShowUnitInfo(c.Unit, v); return; }
            if (!c.HasTower) return;
            bool own = c.TowerOwner == v.Viewer;
            var t = Catalog.Tower;
            var s = new System.Text.StringBuilder();
            if (c.TowerIsGhost) s.Append(Strings.InfoGhost).Append('\n');
            s.Append(Strings.InfoHealth).Append(' ').Append(c.TowerHealth).Append('/').Append(t.Health)
                .Append("   ").Append(Strings.InfoAttack).Append(' ').Append(t.Attack)
                .Append('\n').Append(Strings.InfoRange).Append(": ").Append(Strings.RangeText(RangeKind.Distance, t.MinRange, t.MaxRange))
                .Append("   ").Append(Strings.InfoSight).Append(' ').Append(t.Sight)
                .Append('\n').Append(ArtCatalog.Tower.Ability);
            OpenInfo(ArtCatalog.Tower.Icon, Gfx.OwnerColor(c.TowerOwner), own ? Strings.InfoOwnTower : Strings.InfoEnemyTower, s.ToString());
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
                if (e is DamageDealt dd) VibrateOnOwnDamage(dd, v);
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
            if (units.TryGetSeen(unitId, out var s)) return ArtCatalog.UnitRef(s.Class, s.Owner == v.Viewer);
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
                    string text = (units.TryGetSeen(u.UnitId, out var s) ? ArtCatalog.UnitRef(s.Class, own) : Strings.SomeUnit) + Strings.Died;
                    noticeText = text;
                    bad = own;
                    return text;
                }
                case UnitHealed h: return UnitName(h.UnitId, v) + " +" + h.Amount;
                case ShieldBlocked sb2: return UnitName(sb2.UnitId, v) + ": " + ArtCatalog.Name("C-12") + Strings.BlockedDamage; // C-12 Shield
                case TowerDamageBlocked tb: return (tb.Owner == v.Viewer ? Strings.OwnTowerRef : Strings.EnemyTowerRef) + ": " + ArtCatalog.Name("P-04") + Strings.BlockedDamage; // P-04 Thick Wall
                case TrapTriggered tr: noticeText = Strings.TrapOn; bad = tr.Owner != v.Viewer; return Strings.TrapOn;
                case TowerShot ts: noticeText = Strings.TowerFired; bad = ts.Owner != v.Viewer; return Strings.TowerFired;
                case OverwatchFired of:
                {
                    bool mine = units.TryGetSeen(of.UnitId, out var s) && s.Owner == v.Viewer;
                    noticeText = Strings.OverwatchFiredText;
                    bad = !mine;
                    return Strings.OverwatchFiredText;
                }
                case CardPlayed cp: return (cp.Player == v.Viewer ? Strings.YouPlayed : Strings.OppPlayed) + ArtCatalog.Name(cp.DefId);
                case UnitDeployed ud: return v.Phase == GamePhase.Playing ? UnitName(ud.UnitId, v) + Strings.Deployed : null;
                case UnitRevived ur: return ArtCatalog.UnitRef(ur.Class, ur.Owner == v.Viewer) + Strings.Revived;
                case QuestCompleted qc:
                {
                    bool mine = qc.Player == v.Viewer;
                    noticeText = (mine ? Strings.QuestDoneText : Strings.OppQuestDoneText) + ArtCatalog.Name(qc.QuestId);
                    bad = !mine;
                    return noticeText;
                }
                case QuestFailed qf:
                    return (qf.Player == v.Viewer ? Strings.QuestFailText : Strings.OppQuestFailText) + ArtCatalog.Name(qf.QuestId);
                case PassiveRevealed pr: return Strings.PassiveOpened + ArtCatalog.Name(pr.PassiveId);
                case MapEventResolved me: noticeText = Strings.EventHappened + ArtCatalog.Name(me.DefId); return noticeText;
                case MarketRefreshed mr: // D-05: null = the automatic refresh at turn start
                    AnimateMarket();
                    return mr.Player == null ? Strings.MarketRefreshedAuto : mr.Player == v.Viewer ? Strings.MarketRefreshedYou : Strings.MarketRefreshedOpp;
                case TorchPlaced tp: return tp.Owner == v.Viewer ? Strings.TorchPlacedText : null;
                case TorchExtinguished te:
                    bad = te.Owner == v.Viewer && te.Reason == TorchEndReason.EnemyStood;
                    if (bad) noticeText = Strings.TorchOutEnemy;
                    return te.Reason == TorchEndReason.Replaced ? Strings.TorchOutReplaced : Strings.TorchOutEnemy;
                case BearerSpawned bs:
                    noticeText = bs.Owner == v.Viewer ? Strings.BearerSpawnedYou : Strings.BearerSpawnedOpp;
                    bad = bs.Owner != v.Viewer;
                    return noticeText;
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
