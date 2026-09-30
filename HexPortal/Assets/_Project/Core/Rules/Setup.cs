using System;
using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>Where a player is in the fixed setup order (v2.8 S1): quests → passive → tower → units/traps → done.</summary>
    public enum SetupStep { Quests, Passive, Tower, Placement, Done }

    /// <summary>§6 setup: S-01…S-08. Both players act independently (ActivePlayer is ignored); IsLegal is the single
    /// check shared by GetLegalCommands and Apply. All randomness comes from the match Rng.</summary>
    public static class Setup
    {
        public static SetupStep StepOf(GameState s, PlayerId p)
        {
            if (s.IsSetupFinished(p)) return SetupStep.Done;
            if (s.GetQuestChoices(p).Count == 0) return SetupStep.Quests;
            if (s.GetPassiveChoice(p) == null) return SetupStep.Passive;
            if (!s.GetTower(p).IsPlaced) return SetupStep.Tower;
            return SetupStep.Placement;
        }

        internal static bool IsSetupCommand(ICommand c) =>
            c is ChooseQuestsCommand || c is ChoosePassiveCommand || c is PlaceTowerCommand || c is PlaceUnitCommand
            || c is PlaceTrapCommand || c is FinishSetupCommand || c is SetupTimeoutCommand;

        /// <summary>S-03, S-04: QuestOffer distinct quests and PassiveOffer distinct passives per player (A first), each
        /// kept in Catalog order. The two players' offers are independent and may overlap.</summary>
        internal static void Offer(GameState s)
        {
            foreach (var p in new[] { PlayerId.A, PlayerId.B })
            {
                s.SetQuestOffer(p, PickDistinct(s.Rng, Catalog.Quests, Catalog.QuestOffer));
                s.SetPassiveOffer(p, PickDistinct(s.Rng, Catalog.Passives, Catalog.PassiveOffer));
            }
        }

        // n distinct items chosen uniformly, returned in their source order.
        static List<T> PickDistinct<T>(Rng rng, IReadOnlyList<T> source, int n)
        {
            var left = new List<int>();
            for (int i = 0; i < source.Count; i++) left.Add(i);
            var picked = new List<int>();
            for (int k = 0; k < n && left.Count > 0; k++)
            {
                int j = rng.NextInt(left.Count);
                picked.Add(left[j]);
                left.RemoveAt(j);
            }
            picked.Sort();
            var result = new List<T>();
            foreach (int i in picked) result.Add(source[i]);
            return result;
        }

        // ---------- Legality ----------

        static bool IsTowerCell(GameState s, PlayerId p, Hex h) =>
            Board.IsHomeZone(h, p) && h != Board.Portal && Movement.IsEmpty(s, h);

        static bool IsUnitCell(GameState s, PlayerId p, Hex h) => IsTowerCell(s, p, h);

        // S-05: own half (B-06) and the Control Zone (C-02) of the pieces placed so far; C-30 cell rules.
        static bool IsTrapCell(GameState s, PlayerId p, Hex h, ISet<Hex> zone) =>
            Board.IsInHalf(h, p) && Traps.IsLegalCell(s, p, h, zone, zone);

        static bool CanFinish(GameState s, PlayerId p)
        {
            int n = 0;
            foreach (var u in s.Units)
                if (u.Owner == p) n++;
            return n >= Catalog.MinPlacedCharacters;
        }

        static bool IsTrapCard(CardInstance c) => c != null && !c.IsCharacter && c.Support.Category == CardCategory.Trap;

        /// <summary>Every setup command except SetupTimeoutCommand (see CanTimeout).</summary>
        static bool IsLegal(GameState s, ICommand c)
        {
            var p = c.Player;
            var step = StepOf(s, p);
            switch (c)
            {
                case ChooseQuestsCommand q:
                    if (step != SetupStep.Quests || q.QuestIds.Count != Catalog.QuestPick) return false;
                    for (int i = 0; i < q.QuestIds.Count; i++)
                    {
                        if (FindQuest(s.GetQuestOffer(p), q.QuestIds[i]) == null) return false;
                        if (i > 0 && q.QuestIds[i] == q.QuestIds[i - 1]) return false; // sorted, so duplicates are adjacent
                    }
                    return true;
                case ChoosePassiveCommand d:
                    return step == SetupStep.Passive && FindPassive(s.GetPassiveOffer(p), d.PassiveId) != null;
                case PlaceTowerCommand t:
                    return step == SetupStep.Tower && IsTowerCell(s, p, t.Cell);
                case PlaceUnitCommand u:
                    var card = s.FindInHand(p, u.CardId);
                    return step == SetupStep.Placement && card != null && card.IsCharacter && IsUnitCell(s, p, u.Cell);
                case PlaceTrapCommand t:
                    return step == SetupStep.Placement && IsTrapCard(s.FindInHand(p, t.CardId))
                           && Traps.ActiveCount(s, p) < Catalog.MaxActiveTraps && IsTrapCell(s, p, t.Cell, ControlZone.Cells(s, p));
                case FinishSetupCommand _:
                    return step == SetupStep.Placement && CanFinish(s, p);
                default:
                    return false;
            }
        }

        static bool CanTimeout(GameState s, PlayerId p) => s.InSetup && !s.IsSetupFinished(p);

        /// <summary>The player's options at their current step, in a fixed order: quest combinations (offer order),
        /// passives (offer order), tower cells (Board.Cells order), then units (hand × cells), traps (hand × cells), Finish.</summary>
        internal static void AddLegal(GameState s, PlayerId p, List<ICommand> list)
        {
            switch (StepOf(s, p))
            {
                case SetupStep.Quests:
                    var offer = s.GetQuestOffer(p);
                    foreach (var combo in Combinations(offer.Count, Catalog.QuestPick))
                    {
                        var ids = new List<string>();
                        foreach (int i in combo) ids.Add(offer[i].Id);
                        list.Add(new ChooseQuestsCommand(p, ids));
                    }
                    break;
                case SetupStep.Passive:
                    foreach (var d in s.GetPassiveOffer(p)) list.Add(new ChoosePassiveCommand(p, d.Id));
                    break;
                case SetupStep.Tower:
                    foreach (var h in Board.Cells)
                        if (IsTowerCell(s, p, h)) list.Add(new PlaceTowerCommand(p, h));
                    break;
                case SetupStep.Placement:
                    foreach (var card in s.GetHand(p))
                        if (card.IsCharacter)
                            foreach (var h in Board.Cells)
                                if (IsUnitCell(s, p, h)) list.Add(new PlaceUnitCommand(p, card.Id, h));
                    if (Traps.ActiveCount(s, p) < Catalog.MaxActiveTraps)
                    {
                        var zone = ControlZone.Cells(s, p);
                        foreach (var card in s.GetHand(p))
                            if (IsTrapCard(card))
                                foreach (var h in Board.Cells)
                                    if (IsTrapCell(s, p, h, zone)) list.Add(new PlaceTrapCommand(p, card.Id, h));
                    }
                    if (CanFinish(s, p)) list.Add(new FinishSetupCommand(p));
                    break;
            }
        }

        // Index combinations of size k from 0..n-1, lexicographic.
        static List<int[]> Combinations(int n, int k)
        {
            var result = new List<int[]>();
            var cur = new int[k];
            void Rec(int start, int depth)
            {
                if (depth == k)
                {
                    result.Add((int[])cur.Clone());
                    return;
                }
                for (int i = start; i < n; i++)
                {
                    cur[depth] = i;
                    Rec(i + 1, depth + 1);
                }
            }
            Rec(0, 0);
            return result;
        }

        static QuestDef FindQuest(IReadOnlyList<QuestDef> list, string id)
        {
            foreach (var q in list)
                if (q.Id == id) return q;
            return null;
        }

        static PassiveDef FindPassive(IReadOnlyList<PassiveDef> list, string id)
        {
            foreach (var d in list)
                if (d.Id == id) return d;
            return null;
        }

        // ---------- Apply ----------

        /// <summary>Validates fully, then applies. Throws IllegalCommandException with the state unchanged.</summary>
        internal static void Apply(GameState s, ICommand c, EventLog events)
        {
            if (c is SetupTimeoutCommand)
            {
                if (!CanTimeout(s, c.Player)) throw new IllegalCommandException("Nothing to time out: " + c);
                AutoComplete(s, c.Player, events);
                return;
            }
            if (!IsLegal(s, c)) throw new IllegalCommandException("Illegal setup command: " + c);
            var p = c.Player;
            switch (c)
            {
                case ChooseQuestsCommand q:
                    var chosen = new List<QuestDef>();
                    foreach (var d in s.GetQuestOffer(p))
                        if (Contains(q.QuestIds, d.Id)) chosen.Add(d);
                    ChooseQuests(s, p, chosen, events);
                    break;
                case ChoosePassiveCommand d:
                    ChoosePassive(s, p, FindPassive(s.GetPassiveOffer(p), d.PassiveId), events);
                    break;
                case PlaceTowerCommand t:
                    PlaceTower(s, p, t.Cell, events);
                    break;
                case PlaceUnitCommand u:
                    PlaceUnit(s, p, s.FindInHand(p, u.CardId), u.Cell, events);
                    break;
                case PlaceTrapCommand t:
                    var card = s.FindInHand(p, t.CardId);
                    s.HandList(p).Remove(card);
                    Traps.Place(s, p, card, t.Cell, events); // S-05: no Mana
                    break;
                case FinishSetupCommand _:
                    Finish(s, p, events);
                    break;
            }
        }

        static bool Contains(IReadOnlyList<string> ids, string id)
        {
            foreach (var x in ids)
                if (x == id) return true;
            return false;
        }

        static void ChooseQuests(GameState s, PlayerId p, List<QuestDef> chosen, EventLog events)
        {
            s.SetQuestChoices(p, chosen);
            var ids = new List<string>();
            foreach (var d in chosen) ids.Add(d.Id);
            events.Add(new QuestsChosen(p, ids));
        }

        static void ChoosePassive(GameState s, PlayerId p, PassiveDef d, EventLog events)
        {
            s.SetPassiveChoice(p, d);
            events.Add(new PassiveChosen(p, d.Id));
        }

        static void PlaceTower(GameState s, PlayerId p, Hex h, EventLog events)
        {
            var t = s.GetTower(p);
            t.Pos = h;
            t.IsPlaced = true;
            events.Add(new TowerPlaced(p, h));
        }

        // S-05: no Mana; not a deploy (no traps, tower shots or overwatch during setup).
        static void PlaceUnit(GameState s, PlayerId p, CardInstance card, Hex h, EventLog events)
        {
            s.HandList(p).Remove(card);
            var u = s.AddUnit(p, card.Character.Class, card.Character.Biome, h);
            events.Add(new UnitDeployed(u.Id, card.Id, h));
        }

        /// <summary>S-06 (v2.8 S2): random offered quests and passive, a random tower cell, random character cards on
        /// random cells until MinPlacedCharacters stand, then Finish. Choices already made are kept; no traps.</summary>
        static void AutoComplete(GameState s, PlayerId p, EventLog events)
        {
            if (StepOf(s, p) == SetupStep.Quests)
                ChooseQuests(s, p, PickDistinct(s.Rng, s.GetQuestOffer(p), Catalog.QuestPick), events);
            if (StepOf(s, p) == SetupStep.Passive)
            {
                var offer = s.GetPassiveOffer(p);
                ChoosePassive(s, p, offer[s.Rng.NextInt(offer.Count)], events);
            }
            if (StepOf(s, p) == SetupStep.Tower)
                PlaceTower(s, p, RandomCell(s, p), events);
            while (!CanFinish(s, p))
            {
                var chars = new List<CardInstance>();
                foreach (var c in s.GetHand(p))
                    if (c.IsCharacter) chars.Add(c);
                var cells = UnitCells(s, p);
                if (chars.Count == 0 || cells.Count == 0) break; // unreachable with the Catalog: 5 characters are dealt
                var card = chars[s.Rng.NextInt(chars.Count)];
                PlaceUnit(s, p, card, cells[s.Rng.NextInt(cells.Count)], events);
            }
            Finish(s, p, events);
        }

        static Hex RandomCell(GameState s, PlayerId p)
        {
            var cells = UnitCells(s, p);
            if (cells.Count == 0) throw new InvalidOperationException("No free home cell for " + p);
            return cells[s.Rng.NextInt(cells.Count)];
        }

        static List<Hex> UnitCells(GameState s, PlayerId p)
        {
            var cells = new List<Hex>();
            foreach (var h in Board.Cells)
                if (IsUnitCell(s, p, h)) cells.Add(h);
            return cells;
        }

        // S-07: when both are done, round 1 starts under fog (V-04) with A's turn and A's draw (T-04, v2.6).
        static void Finish(GameState s, PlayerId p, EventLog events)
        {
            s.SetSetupFinished(p, true);
            events.Add(new SetupFinished(p));
            if (!s.IsSetupFinished(p.Opponent())) return;
            s.InSetup = false;
            s.Round = 1;
            s.ActivePlayer = PlayerId.A;
            Visibility.UpdateMemory(s);
            Turn.StartTurn(s, events);
        }
    }
}
