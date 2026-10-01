using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexPortal.Game
{
    /// <summary>
    /// Screens, HUD (UX-04) and touch interaction (UX-02, UX-03). Reads only the controller's PlayerView, legal
    /// commands and filtered events; every action is a command from the legal list sent through the controller.
    /// Pointer input comes from UI Toolkit, which reads the Input System (touch and mouse).
    /// </summary>
    public sealed class GameUi
    {
        public enum Screen { Menu, Quests, Passive, Handoff, Board, Result }

        const float LongPressMs = 500f;
        const float DragThreshold = 18f;

        readonly MatchController match;
        readonly VisualElement root;
        readonly BoardView board;
        readonly UnitsView units;
        readonly Camera cam;

        readonly VisualElement screenMenu, screenQuests, screenPassive, screenHandoff, screenResult, hud, questPanel, cardDetail, dragCard;
        readonly VisualElement hand, questOptions, passiveOptions, manaPips, energyPips;
        readonly Label ownInfo, oppInfo, turnBanner, eventBanner, manaText, energyText, timer, prompt, questText, detailText;
        readonly Button[] market = new Button[3];
        readonly Button blindBtn, endBtn, overwatchBtn, questsConfirm, menuTimer;
        readonly List<string> pickedQuests = new List<string>();

        bool timerOn = true;
        bool aiTimerOn;
        PlayerId humanSeat = PlayerId.A;
        int selectedUnit = -1;
        int selectedCard = -1;
        readonly List<ICommand> secondPick = new List<ICommand>();
        Hex secondPickTarget;
        PlayerId? facing;
        int shownTurn = -1, shownBank = -1;

        // Card pointer state.
        int pressCard = -1;
        Vector2 pressPos;
        bool dragging, longPressed;
        IVisualElementScheduledItem longPressJob;
        Vector2 boardPress;

        public Screen Current { get; private set; } = Screen.Menu;
        public int SelectedUnit => selectedUnit;
        public int SelectedCard => selectedCard;
        public int HighlightCount => board.HighlightCount;
        public string LastTap { get; private set; } = "";
        public VisualElement Root => root;

        public GameUi(MatchController match, VisualElement root, BoardView board, UnitsView units, Camera cam)
        {
            this.match = match; this.root = root; this.board = board; this.units = units; this.cam = cam;
            screenMenu = root.Q("screen-menu"); screenQuests = root.Q("screen-quests"); screenPassive = root.Q("screen-passive");
            screenHandoff = root.Q("screen-handoff"); screenResult = root.Q("screen-result"); hud = root.Q("hud");
            questPanel = root.Q("quest-panel"); cardDetail = root.Q("card-detail"); dragCard = root.Q("drag-card");
            hand = root.Q("hand"); questOptions = root.Q("quest-options"); passiveOptions = root.Q("passive-options");
            manaPips = root.Q("mana-pips"); energyPips = root.Q("energy-pips");
            ownInfo = root.Q<Label>("own-info"); oppInfo = root.Q<Label>("opp-info"); turnBanner = root.Q<Label>("turn-banner");
            eventBanner = root.Q<Label>("event-banner"); manaText = root.Q<Label>("mana-text"); energyText = root.Q<Label>("energy-text");
            timer = root.Q<Label>("timer"); prompt = root.Q<Label>("prompt"); questText = root.Q<Label>("quest-text");
            detailText = root.Q<Label>("detail-text");
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                market[i] = root.Q<Button>("market-" + i);
                market[i].clicked += () => OnDrawSlot(slot);
            }
            blindBtn = root.Q<Button>("blind-btn"); blindBtn.text = Strings.Blind; blindBtn.clicked += () => OnDrawSlot(DrawCommand.Blind);
            endBtn = root.Q<Button>("end-btn"); endBtn.clicked += OnEnd;
            overwatchBtn = root.Q<Button>("overwatch-btn"); overwatchBtn.text = Strings.Overwatch; overwatchBtn.clicked += OnOverwatch;
            questsConfirm = root.Q<Button>("quests-confirm"); questsConfirm.text = Strings.Confirm; questsConfirm.clicked += OnConfirmQuests;
            var questBtn = root.Q<Button>("quest-btn"); questBtn.text = Strings.Quests; questBtn.clicked += () => questPanel.style.display = DisplayStyle.Flex;
            var qc = root.Q<Button>("quest-close"); qc.text = Strings.Close; qc.clicked += () => questPanel.style.display = DisplayStyle.None;
            var dc = root.Q<Button>("detail-close"); dc.text = Strings.Close; dc.clicked += () => cardDetail.style.display = DisplayStyle.None;

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
            hit.RegisterCallback<PointerDownEvent>(e => boardPress = e.position);
            hit.RegisterCallback<PointerUpEvent>(e =>
            {
                if ((((Vector2)e.position) - boardPress).sqrMagnitude > DragThreshold * DragThreshold) return;
                if (TryPickPanel(e.position, out var cell)) { LastTap = cell.ToString(); OnCellTapped(cell); }
                else ClearSelection();
            });
            hud.RegisterCallback<PointerMoveEvent>(OnCardMove, TrickleDown.TrickleDown);
            hud.RegisterCallback<PointerUpEvent>(OnCardUp, TrickleDown.TrickleDown);

            match.Changed += Refresh;
            match.EventsApplied += evts => units.Play(evts);
            Show(Screen.Menu);
        }

        void StartHotseat()
        {
            ulong seed = (ulong)System.Environment.TickCount;
            facing = null;
            match.NewMatch(seed, timerOn);
        }

        void StartAi(AiLevel level)
        {
            ulong seed = (ulong)System.Environment.TickCount;
            facing = null;
            match.NewAiMatch(seed, humanSeat, level, aiTimerOn);
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
            if (s != Screen.Board) { questPanel.style.display = DisplayStyle.None; cardDetail.style.display = DisplayStyle.None; }
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
                var b = new Button { text = Strings.Name(qid) + "\n" + Strings.Text(qid) };
                b.AddToClassList("btn"); b.AddToClassList("option");
                b.clicked += () =>
                {
                    if (pickedQuests.Remove(qid)) b.RemoveFromClassList("selected");
                    else if (pickedQuests.Count < Catalog.QuestPick) { pickedQuests.Add(qid); b.AddToClassList("selected"); }
                    questsConfirm.SetEnabled(pickedQuests.Count == Catalog.QuestPick);
                };
                questOptions.Add(b);
            }
            questsConfirm.SetEnabled(false);
        }

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
            ownInfo.text = Strings.Player(v.Viewer) + "  ·  " + Strings.Tower + " " + v.OwnTowerHealth
                + "  ·  " + (setup ? Strings.Setup : Strings.Round + v.Round);
            oppInfo.text = Strings.Player(v.Viewer.Opponent()) + "  ·  " + Strings.Tower + " " + v.EnemyTowerHealth
                + "  ·  " + Strings.Hand + " " + v.OpponentHandCount + "  ·  " + Strings.OppQuests + v.OpponentCompletedQuests.Count;
            turnBanner.text = setup ? Strings.Setup : myTurn ? Strings.YourTurn : Strings.OpponentTurn;
            eventBanner.style.display = v.AnnouncedEvent != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (v.AnnouncedEvent != null) eventBanner.text = Strings.Event + Strings.Name(v.AnnouncedEvent) + " (" + Strings.Round + v.AnnouncedEventRound + ")";

            manaText.text = Strings.Mana + " " + v.Mana;
            energyText.text = Strings.Energy + " " + v.Energy;
            Pips(manaPips, v.Mana, Mathf.Max(v.Mana, Catalog.ManaCap), "mana-pip");
            Pips(energyPips, v.Energy, Catalog.EnergyPerTurn, "energy-pip");

            for (int i = 0; i < 3; i++)
            {
                var card = v.Market[i];
                market[i].text = Strings.SlotName(i) + "\n" + (card == null ? Strings.Empty : CardName(card) + " (" + card.Cost + ")");
                market[i].SetEnabled(!setup && HasDraw(i));
                market[i].EnableInClassList("picked", v.PrePickSlot == i);
            }
            blindBtn.SetEnabled(!setup && HasDraw(DrawCommand.Blind));
            blindBtn.EnableInClassList("picked", v.PrePickSlot == DrawCommand.Blind);

            endBtn.text = setup ? Strings.FinishSetup : Strings.EndTurn;
            endBtn.SetEnabled(setup ? Find<FinishSetupCommand>() != null : Find<EndTurnCommand>() != null);

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
            if (v.OpponentPassive != null) q.Append('\n').Append(Strings.OppPassive).Append(Strings.Name(v.OpponentPassive));
            if (v.OpponentCompletedQuests.Count > 0)
            {
                q.Append('\n').Append(Strings.OppQuests);
                foreach (var id in v.OpponentCompletedQuests) q.Append(Strings.Name(id)).Append(' ');
            }
            questText.text = q.ToString();

            BuildHand(v);
            UpdatePrompt();
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

        public void TickClock()
        {
            if (Current != Screen.Board || !match.HasMatch) return;
            if (!match.TimerEnabled) { timer.text = "∞"; return; }
            var v = match.View;
            int t = Mathf.CeilToInt(v.Phase == GamePhase.Setup ? match.SetupSecondsLeft : match.TurnSecondsLeft);
            int b = Mathf.CeilToInt(match.BankSecondsLeft(match.Viewer));
            if (t == shownTurn && b == shownBank) return; // rebuild the text only when a second changes
            shownTurn = t; shownBank = b;
            timer.text = v.Phase == GamePhase.Setup ? t + " sn" : t + " sn  ·  " + Strings.Bank + b;
        }

        static string CardName(CardInstance c) =>
            c.IsCharacter ? Strings.ClassName(c.Character.Class) : Strings.Name(c.Support.Id);

        void BuildHand(PlayerView v)
        {
            hand.Clear();
            foreach (var card in v.Hand)
            {
                var el = MakeCard(card);
                int id = card.Id;
                el.EnableInClassList("unplayable", !HasCardCommand(id));
                el.EnableInClassList("selected", id == selectedCard);
                el.RegisterCallback<PointerDownEvent>(e => OnCardDown(id, e));
                hand.Add(el);
            }
        }

        static VisualElement MakeCard(CardInstance card)
        {
            var el = new VisualElement();
            el.AddToClassList("card");
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = FrameColor(card);
            var cost = new Label(card.Cost.ToString()) { pickingMode = PickingMode.Ignore };
            cost.AddToClassList("card-cost");
            var name = new Label(CardName(card)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("card-name");
            el.Add(cost); el.Add(name);
            if (card.IsCharacter)
            {
                var d = Unit.DefOf(card.Character.Class);
                var bio = new Label(Strings.BiomeName(card.Character.Biome)) { pickingMode = PickingMode.Ignore };
                bio.AddToClassList("card-text");
                var stats = new Label("⚔" + d.Attack + " ♥" + d.Health + " ➜" + d.Move) { pickingMode = PickingMode.Ignore };
                stats.AddToClassList("card-stats");
                el.Add(bio); el.Add(stats);
            }
            else
            {
                var text = new Label(Strings.Text(card.Support.Id)) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("card-text");
                el.Add(text);
            }
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
            foreach (var c in match.Legal)
            {
                if (c is MoveCommand m && m.UnitId == unitId) board.Highlight(m.Dest, Gfx.MoveHl);
                else if (c is AttackCommand a && a.UnitId == unitId) board.Highlight(a.Target, Gfx.AttackHl);
            }
            UpdatePrompt();
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
            UpdatePrompt();
        }

        void OnCellTapped(Hex cell)
        {
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

        /// <summary>Plays the card on the cell if legal. Push (C-18) and Teleport (C-15) need a second pick.</summary>
        bool TryCardOnCell(int cardId, Hex cell)
        {
            var options = new List<ICommand>();
            foreach (var c in match.Legal)
                if (CardOf(c) == cardId && CellOf(c, out var h) && h == cell) options.Add(c);
            if (options.Count == 0) { ClearSelection(); return false; }
            if (options.Count == 1) { match.Apply(options[0]); return true; }
            secondPick.Clear();
            secondPickTarget = cell;
            board.ClearHighlights();
            board.Highlight(cell, Gfx.TargetHl);
            foreach (var c in options)
            {
                var h = SecondPickCell(c);
                if (Board.IsOnBoard(h)) { secondPick.Add(c); board.Highlight(h, Gfx.MoveHl); }
            }
            if (secondPick.Count == 0) match.Apply(options[0]);
            UpdatePrompt();
            return true;
        }

        static Hex SecondPickCell(ICommand c)
        {
            var p = (PlayCardCommand)c;
            if (p.Dest.HasValue) return p.Dest.Value;
            return p.Direction >= 0 ? p.Target.Neighbor(p.Direction) : p.Target;
        }

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
                dragCard.style.left = e.position.x - 64;
                dragCard.style.top = e.position.y - 75;
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
            if (selectedCard == card) ClearSelection();
            else SelectCard(card);
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
                        text += Strings.BiomeName(c.Character.Biome) + "\n⚔ " + d.Attack + "  ♥ " + d.Health + "  ➜ " + d.Move + "  👁 " + d.Sight;
                    }
                    else text += Strings.Text(c.Support.Id);
                    detailText.text = text;
                    cardDetail.style.display = DisplayStyle.Flex;
                }
        }

        bool TryPickPanel(Vector2 panelPos, out Hex cell)
        {
            var size = root.layout.size;
            var screen = new Vector2(panelPos.x / size.x * UnityEngine.Screen.width, (1f - panelPos.y / size.y) * UnityEngine.Screen.height);
            return CameraRig.TryPick(cam, screen, out cell);
        }
    }
}
