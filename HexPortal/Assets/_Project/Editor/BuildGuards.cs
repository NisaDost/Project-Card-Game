using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace HexPortal.Editor
{
    /// <summary>M6 gate: an Android build fails if the com.unity.pipeline runtime server is enabled for builds, or if
    /// ENABLE_RUNTIME_PIPELINE is defined. Runs before the package's own build processor.</summary>
    sealed class BuildGuards : IPreprocessBuildWithReport
    {
        public const string RuntimeConfigPath = "ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if (File.Exists(RuntimeConfigPath) && Regex.IsMatch(File.ReadAllText(RuntimeConfigPath), @"""enableInBuilds""\s*:\s*true"))
                throw new BuildFailedException("HexPortal M6 gate: " + RuntimeConfigPath + " enables the Pipeline runtime server in builds.");
            var defines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android);
            if (defines.Contains("ENABLE_RUNTIME_PIPELINE"))
                throw new BuildFailedException("HexPortal M6 gate: ENABLE_RUNTIME_PIPELINE must never be defined.");
        }
    }
}
