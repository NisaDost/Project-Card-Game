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

        // M6 Android player settings (user decisions): product name, application id, min API 26, target = latest
        // installed, IL2CPP, ARM64 only. Landscape is set separately (a28e36f). Pass confirm=true to apply.
        [CliCommand("hexportal_android_settings", "HexPortal: read (or with confirm=true apply) the M6 Android PlayerSettings")]
        static string AndroidSettings([CliArg("confirm", "Apply the M6 values")] bool confirm = false)
        {
            if (confirm)
            {
                PlayerSettings.productName = "HexPortal";
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.hexportal.prototype");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                AssetDatabase.SaveAssets();
            }
            return "{\"applied\":" + (confirm ? "true" : "false")
                + ",\"productName\":\"" + PlayerSettings.productName + "\""
                + ",\"applicationIdentifier\":\"" + PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) + "\""
                + ",\"minSdk\":\"" + PlayerSettings.Android.minSdkVersion + "\""
                + ",\"targetSdk\":\"" + PlayerSettings.Android.targetSdkVersion + "\""
                + ",\"backend\":\"" + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) + "\""
                + ",\"architectures\":\"" + PlayerSettings.Android.targetArchitectures + "\""
                + ",\"orientation\":\"" + PlayerSettings.defaultInterfaceOrientation + "\""
                + ",\"defines\":\"" + PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android) + "\"}";
        }

        // Play-mode verification: presses a named UI Toolkit button (menu-*, end-btn, blind-btn, market-0..2, ...)
        // through its own click handler, like a tap. Used when simulated pointer input cannot reach an unfocused Game view.
        [CliCommand("hexportal_client_press", "HexPortal: press a named UI button in the running client (Play mode only)")]
        static string Press([CliArg("button", "UXML name of the button", Required = true)] string button)
        {
            var app = HexPortal.Game.GameApp.Instance;
            if (app == null) return "{\"pressed\":false,\"error\":\"not playing\"}";
            var b = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(app.Ui.Root, button);
            if (b == null) return "{\"pressed\":false,\"error\":\"no button " + button + "\"}";
            if (!b.enabledInHierarchy || b.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None)
                return "{\"pressed\":false,\"error\":\"button disabled or hidden\"}";
            using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
            return "{\"pressed\":true,\"button\":\"" + button + "\"}";
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
                + ",\"mode\":\"" + m.Mode + "\""
                + ",\"aiToAct\":" + (m.AiToAct ? "true" : "false")
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
