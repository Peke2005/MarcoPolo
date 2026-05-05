#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace FrentePartido.Editor
{
    public static class StandaloneSmokeBuild
    {
        private static readonly string[] ReleaseScenes =
        {
            "Assets/Scenes/00_Boot.unity",
            "Assets/Scenes/01_Auth.unity",
            "Assets/Scenes/02_MainMenu.unity",
            "Assets/Scenes/03_Lobby.unity",
            "Assets/Scenes/04_Game.unity",
            "Assets/Scenes/05_PostMatch.unity"
        };

        private static readonly string[] SmokeScenes =
        {
            "Assets/Scenes/04_Game.unity",
            "Assets/Scenes/02_MainMenu.unity",
            "Assets/Scenes/03_Lobby.unity",
            "Assets/Scenes/05_PostMatch.unity"
        };

        public static void BuildGameSceneWindows()
        {
            string outputDir = Path.GetFullPath("Builds/Smoke/MarcoPoloSmoke");
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = SmokeScenes,
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

        public static void BuildWindowsRelease()
        {
            string outputDir = Path.GetFullPath("Builds/Release/FrentePartido");
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = ReleaseScenes,
                locationPathName = Path.Combine(outputDir, "FrentePartido.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Release build failed: {report.summary.result}");
            }

            UnityEngine.Debug.Log($"[StandaloneSmokeBuild] Release built {options.locationPathName}");
        }
    }
}
#endif
