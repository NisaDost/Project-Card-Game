namespace HexPortal.Core
{
    /// <summary>Setup (§6) → Playing → Over (a GameResult exists).</summary>
    public enum GamePhase { Setup, Playing, Over }

    /// <summary>W-01 Portal, W-02 Tower, W-03 RoundLimit (Criterion 1–4; 4 = draw), W-04 Timeout.</summary>
    public enum WinReason { Portal, Tower, RoundLimit, Timeout }

    /// <summary>How the match ended. Immutable. Winner is null only for a W-03 draw.</summary>
    public sealed class GameResult
    {
        public readonly PlayerId? Winner;
        public readonly WinReason Reason;
        /// <summary>W-03: the deciding criterion (1 tower Health, 2 quests, 3 unit Health, 4 draw). 0 for other reasons.</summary>
        public readonly int Criterion;

        public GameResult(PlayerId? winner, WinReason reason, int criterion = 0)
        {
            Winner = winner;
            Reason = reason;
            Criterion = criterion;
        }

        public bool IsDraw => !Winner.HasValue;

        public override string ToString() => (IsDraw ? "Draw" : Winner + " wins") + " (" + Reason + (Criterion > 0 ? " " + Criterion : "") + ")";
    }
}
