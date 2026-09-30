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
    }
}
