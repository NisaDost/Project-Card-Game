using Unity.Pipeline.Commands;
using UnityEditor;

namespace HexPortal.Editor
{
    // Narrow Unity CLI commands that can be allowlisted instead of `eval`.
    static class CliCommands
    {
        // Picks up file changes on disk while the Editor is unfocused. Compile with `recompile` in a separate call.
        [CliCommand("hexportal_refresh", "HexPortal: AssetDatabase.Refresh() only")]
        static string Refresh()
        {
            AssetDatabase.Refresh();
            return "refreshed";
        }

        // Read-only snapshot of the running client (Play mode) for CLI verification. Changes nothing.
        [CliCommand("hexportal_client_state", "HexPortal: read-only JSON of the client (phase, viewer, screen, selection)")]
        static string ClientState()
        {
            var app = HexPortal.Game.GameApp.Instance;
            if (app == null) return "{\"running\":false}";
            var m = app.Match;
            var ui = app.Ui;
            if (!m.HasMatch) return "{\"running\":true,\"screen\":\"" + ui.Current + "\"}";
            var v = m.View;
            return "{\"running\":true"
                + ",\"screen\":\"" + ui.Current + "\""
                + ",\"phase\":\"" + v.Phase + "\""
                + ",\"setupStep\":\"" + v.SetupStep + "\""
                + ",\"viewer\":\"" + m.Viewer + "\""
                + ",\"activePlayer\":\"" + v.ActivePlayer + "\""
                + ",\"round\":" + v.Round
                + ",\"handoffPending\":" + (m.HandoffPending ? "true" : "false")
                + ",\"selectedUnit\":" + ui.SelectedUnit
                + ",\"selectedCard\":" + ui.SelectedCard
                + ",\"highlightedCells\":" + ui.HighlightCount
                + ",\"lastTap\":\"" + ui.LastTap + "\""
                + ",\"legalCommands\":" + m.Legal.Count
                + ",\"ownUnits\":" + v.OwnUnits.Count
                + ",\"enemyUnitsVisible\":" + v.EnemyUnits.Count
                + ",\"hand\":" + v.Hand.Count
                + ",\"mana\":" + v.Mana + ",\"energy\":" + v.Energy
                + ",\"drawPending\":" + (v.DrawPending ? "true" : "false")
                + ",\"result\":\"" + (v.Result == null ? "" : v.Result.ToString()) + "\""
                + ",\"lastError\":\"" + (m.LastError ?? "").Replace("\"", "'") + "\"}";
        }
    }
}
