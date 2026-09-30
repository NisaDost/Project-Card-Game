using System.Collections.Generic;
using HexPortal.Core.Data;

namespace HexPortal.Core
{
    /// <summary>A unit as the viewer knows it. Live own units: every field. Live enemy units (Visible cells): identity,
    /// Health, effects and overwatch (V-05); action flags stay default. Ghosts (Explored cells): identity and the Health
    /// seen then, no effects or overwatch (v2.8 V2).</summary>
    public sealed class UnitView
    {
        public int Id { get; private set; }
        public PlayerId Owner { get; private set; }
        public UnitClass Class { get; private set; }
        public Biome Biome { get; private set; }
        public Hex Pos { get; private set; }
        public int Health { get; private set; }
        public bool IsGhost { get; private set; }
        public string BuffId { get; private set; }
        public int BuffTurnsLeft { get; private set; }
        public string DebuffId { get; private set; }
        public int DebuffTurnsLeft { get; private set; }
        public bool OnOverwatch { get; private set; }
        // Own units only:
        public bool ActedThisTurn { get; private set; }
        public bool MovedThisTurn { get; private set; }
        public bool MovedLastOwnTurn { get; private set; }
        public int MoveBonus { get; private set; }
        public int RevealedUntilTurn { get; private set; } = -1;

        internal static UnitView Live(Unit u, bool own)
        {
            var v = new UnitView
            {
                Id = u.Id, Owner = u.Owner, Class = u.Class, Biome = u.Biome, Pos = u.Pos, Health = u.Health,
                BuffId = u.Buff == null ? null : u.Buff.Def.Id, BuffTurnsLeft = u.Buff == null ? 0 : u.Buff.TurnsLeft,
                DebuffId = u.Debuff == null ? null : u.Debuff.Def.Id, DebuffTurnsLeft = u.Debuff == null ? 0 : u.Debuff.TurnsLeft,
                OnOverwatch = u.OnOverwatch,
            };
            if (own)
            {
                v.ActedThisTurn = u.ActedThisTurn;
                v.MovedThisTurn = u.MovedThisTurn;
                v.MovedLastOwnTurn = u.MovedLastOwnTurn;
                v.MoveBonus = u.MoveBonus;
                v.RevealedUntilTurn = u.RevealedUntilTurn;
            }
            return v;
        }

        internal static UnitView Ghost(LastSeen s, Hex pos) => new UnitView
        {
            Id = s.UnitId, Owner = s.UnitOwner, Class = s.UnitClass, Biome = s.UnitBiome, Pos = pos, Health = s.UnitHealth,
            IsGhost = true,
        };
    }

    /// <summary>One cell as the viewer sees it (V-01). Hidden: no data. Explored: last-seen terrain and ghosts.
    /// Visible: live terrain and contents. The viewer's own units and tower are always shown live.</summary>
    public sealed class CellView
    {
        public Hex Cell { get; internal set; }
        public CellVisibility Visibility { get; internal set; }
        public bool TerrainKnown { get; internal set; }
        public Tile Tile { get; internal set; }
        public UnitView Unit { get; internal set; }
        public bool HasTower { get; internal set; }
        public PlayerId TowerOwner { get; internal set; }
        public int TowerHealth { get; internal set; }
        public bool TowerIsGhost { get; internal set; }
    }

    /// <summary>An own face-down trap.</summary>
    public sealed class TrapView
    {
        public int CardId { get; internal set; }
        public string DefId { get; internal set; }
        public Hex Pos { get; internal set; }
    }

    /// <summary>
    /// Everything one player may know (V-05, V-09, V-10, S-08), as a self-contained read model. The AI (AI-01) and the
    /// client read only this. Never contains: the opponent's hand contents, quests, passive, traps, pre-pick, Mana or
    /// Energy, pool order, the Rng, or enemy units/tower outside the viewer's Visible cells (ghosts excepted).
    /// </summary>
    public sealed class PlayerView
    {
        public PlayerId Viewer { get; private set; }
        public GamePhase Phase { get; private set; }
        public int Round { get; private set; }
        public int TurnIndex { get; private set; }
        public PlayerId ActivePlayer { get; private set; }
        public GameResult Result { get; private set; }

        /// <summary>Board.Cells order.</summary>
        public IReadOnlyList<CellView> Cells { get; private set; }
        public IReadOnlyList<UnitView> OwnUnits { get; private set; }
        /// <summary>Live enemy units on Visible cells (V-05).</summary>
        public IReadOnlyList<UnitView> EnemyUnits { get; private set; }

        public Hex? OwnTowerPos { get; private set; }
        public int OwnTowerHealth { get; private set; }
        public bool OwnTowerShotAvailable { get; private set; }
        public int OwnTowerRevealedUntilTurn { get; private set; }
        /// <summary>Only while the enemy tower's cell is Visible (V-05).</summary>
        public Hex? EnemyTowerPos { get; private set; }
        /// <summary>V-09: public.</summary>
        public int EnemyTowerHealth { get; private set; }
        /// <summary>U-27: false only after it shot at the viewer's unit this turn (which the viewer saw).</summary>
        public bool EnemyTowerShotAvailable { get; private set; }

        public IReadOnlyList<CardInstance> Hand { get; private set; }
        public int Mana { get; private set; }
        public int Energy { get; private set; }
        public bool DrawPending { get; private set; }
        public int PrePickSlot { get; private set; }
        public int PrePickCardId { get; private set; }

        public SetupStep SetupStep { get; private set; }
        public bool OpponentSetupFinished { get; private set; }
        public IReadOnlyList<string> QuestOffer { get; private set; }
        public IReadOnlyList<string> QuestChoices { get; private set; }
        public IReadOnlyList<string> PassiveOffer { get; private set; }
        public string PassiveChoice { get; private set; }
        public IReadOnlyList<TrapView> OwnTraps { get; private set; }

        /// <summary>V-09: public. During setup: the dealt count (S-08, PM decision), not the live count.</summary>
        public int OpponentHandCount { get; private set; }
        /// <summary>D-05, V-09 (v2.8 V3): index = (int)CardPool; null = empty slot.</summary>
        public IReadOnlyList<CardInstance> Market { get; private set; }
        /// <summary>V-09 (v2.8 V3): cards left per pool, index = (int)CardPool. The order is hidden.</summary>
        public IReadOnlyList<int> PoolCounts { get; private set; }
        public int OwnConsecutiveTimeouts { get; private set; }
        /// <summary>Public: every automatic turn end is announced (TurnTimedOut).</summary>
        public int OpponentConsecutiveTimeouts { get; private set; }

        static readonly CardPool[] Slots = { CardPool.Character, CardPool.Buff, CardPool.DebuffTrap };

        public static PlayerView For(GameState s, PlayerId p)
        {
            var o = p.Opponent();
            bool setup = s.InSetup;
            var fog = s.GetFog(p);
            var ownTower = s.GetTower(p);
            var enemyTower = s.GetTower(o);

            var cells = new List<CellView>();
            var enemies = new List<UnitView>();
            Hex? enemyTowerPos = null;
            foreach (var h in Board.Cells)
            {
                var c = new CellView { Cell = h, Visibility = fog.Get(h) };
                var live = s.UnitAt(h);
                if (c.Visibility == CellVisibility.Visible)
                {
                    c.TerrainKnown = true;
                    c.Tile = s.Map.Get(h);
                    if (live != null && (live.Owner == p || !setup))
                    {
                        c.Unit = UnitView.Live(live, live.Owner == p);
                        if (live.Owner == o) enemies.Add(c.Unit);
                    }
                    var t = s.TowerAt(h);
                    if (t != null && (t.Owner == p || !setup))
                    {
                        SetTower(c, t.Owner, t.Health, false);
                        if (t.Owner == o) enemyTowerPos = h;
                    }
                }
                else if (c.Visibility == CellVisibility.Explored)
                {
                    var seen = fog.GetLastSeen(h);
                    c.TerrainKnown = true;
                    c.Tile = seen.Tile;
                    if (live != null && live.Owner == p) c.Unit = UnitView.Live(live, true); // own: always live (setup)
                    else if (seen.HasUnit) c.Unit = UnitView.Ghost(seen, h);
                    if (ownTower.IsPlaced && ownTower.Pos == h) SetTower(c, p, ownTower.Health, false);
                    else if (seen.HasTower) SetTower(c, seen.TowerOwner, seen.TowerHealth, true);
                }
                cells.Add(c);
            }

            var own = new List<UnitView>();
            foreach (var u in s.Units)
                if (u.Owner == p) own.Add(UnitView.Live(u, true));

            var traps = new List<TrapView>();
            foreach (var t in s.Traps)
                if (t.Owner == p) traps.Add(new TrapView { CardId = t.Card.Id, DefId = t.Card.DefId, Pos = t.Pos });

            var market = new List<CardInstance>();
            var counts = new List<int>();
            foreach (var slot in Slots)
            {
                market.Add(s.GetMarket(slot));
                counts.Add(s.GetPool(slot).Count);
            }

            var questOffer = new List<string>();
            foreach (var q in s.GetQuestOffer(p)) questOffer.Add(q.Id);
            var questChoices = new List<string>();
            foreach (var q in s.GetQuestChoices(p)) questChoices.Add(q.Id);
            var passiveOffer = new List<string>();
            foreach (var d in s.GetPassiveOffer(p)) passiveOffer.Add(d.Id);

            return new PlayerView
            {
                Viewer = p, Phase = s.Phase, Round = s.Round, TurnIndex = s.TurnIndex, ActivePlayer = s.ActivePlayer, Result = s.Result,
                Cells = cells, OwnUnits = own, EnemyUnits = enemies,
                OwnTowerPos = ownTower.IsPlaced ? ownTower.Pos : (Hex?)null, OwnTowerHealth = ownTower.Health,
                OwnTowerShotAvailable = s.IsTowerShotAvailable(p), OwnTowerRevealedUntilTurn = ownTower.RevealedUntilTurn,
                EnemyTowerPos = enemyTowerPos, EnemyTowerHealth = enemyTower.Health, EnemyTowerShotAvailable = s.IsTowerShotAvailable(o),
                Hand = new List<CardInstance>(s.GetHand(p)), Mana = s.GetMana(p), Energy = s.GetEnergy(p),
                DrawPending = s.IsDrawPending(p), PrePickSlot = s.GetPrePickSlot(p), PrePickCardId = s.GetPrePickCardId(p),
                SetupStep = Setup.StepOf(s, p), OpponentSetupFinished = s.IsSetupFinished(o),
                QuestOffer = questOffer, QuestChoices = questChoices, PassiveOffer = passiveOffer,
                PassiveChoice = s.GetPassiveChoice(p) == null ? null : s.GetPassiveChoice(p).Id,
                OwnTraps = traps, OpponentHandCount = setup ? s.GetDealtHandCount(o) : s.GetHand(o).Count, Market = market, PoolCounts = counts,
                OwnConsecutiveTimeouts = s.GetConsecutiveTimeouts(p), OpponentConsecutiveTimeouts = s.GetConsecutiveTimeouts(o),
            };
        }

        static void SetTower(CellView c, PlayerId owner, int health, bool ghost)
        {
            c.HasTower = true;
            c.TowerOwner = owner;
            c.TowerHealth = health;
            c.TowerIsGhost = ghost;
        }
    }
}
