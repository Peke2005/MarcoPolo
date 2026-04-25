#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace FrentePartido.Editor
{
    public static class StandaloneSmokeBuild
    {
        public static void BuildGameSceneWindows()
        {
            string outputDir = Path.GetFullPath("Builds/Smoke/MarcoPoloSmoke");
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/04_Game.unity" },
                locationPathName = Path.Combine(outputDir, "MarcoPoloSmoke.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Smoke build failed: {report.summary.result}");
            }

            UnityEngine.Debug.Log($"[StandaloneSmokeBuild] Built {options.locationPathName}");
        }
    }
}
#endif
