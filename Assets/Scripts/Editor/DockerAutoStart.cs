#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace FrentePartido.Editor
{
    /// <summary>
    /// Automatically starts the auth backend Docker containers when the Unity project opens.
    /// Runs once per editor session via InitializeOnLoad.
    /// </summary>
    [InitializeOnLoad]
    public static class DockerAutoStart
    {
        private const string SESSION_KEY = "DockerAutoStart_Done";

        static DockerAutoStart()
        {
            // Only run once per editor session
            if (SessionState.GetBool(SESSION_KEY, false)) return;
            SessionState.SetBool(SESSION_KEY, true);

            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend"));
            string composePath = Path.Combine(backendPath, "docker-compose.yml");

            if (!File.Exists(composePath))
            {
                UnityEngine.Debug.LogWarning("[DockerAutoStart] docker-compose.yml not found in Backend/");
                return;
            }

            StartBackend(backendPath);
        }

        [MenuItem("FrentePartido/Backend/Start Docker")]
        public static void StartFromMenu()
        {
            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend"));
            StartBackend(backendPath);
        }

        [MenuItem("FrentePartido/Backend/Stop Docker")]
        public static void StopFromMenu()
        {
            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend"));
            RunDockerCompose(backendPath, "down", "Backend stopped.");
        }

        [MenuItem("FrentePartido/Backend/View Logs")]
        public static void ViewLogs()
        {
            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend"));
            RunDockerCompose(backendPath, "logs --tail=50", null);
        }

        private static void StartBackend(string backendPath)
        {
            if (IsPortOpen("127.0.0.1", 3001))
            {
                UnityEngine.Debug.Log("[BackendAutoStart] Auth backend already running on http://localhost:3001");
                return;
            }

            UnityEngine.Debug.Log("[DockerAutoStart] Starting auth backend...");
            if (RunDockerCompose(backendPath, "up -d --build", "Auth backend running on http://localhost:3001"))
            {
                return;
            }

            StartNodeBackend(backendPath);
        }

        private static bool RunDockerCompose(string workingDir, string args, string successMsg)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"compose {args}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                process.WaitForExit(60000); // 60s timeout

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (process.ExitCode == 0)
                {
                    if (!string.IsNullOrEmpty(successMsg))
                        UnityEngine.Debug.Log($"[DockerAutoStart] {successMsg}");
                    if (!string.IsNullOrEmpty(output))
                        UnityEngine.Debug.Log($"[Docker] {output}");
                    return true;
                }
                else
                {
                    // docker compose often writes normal output to stderr
                    UnityEngine.Debug.LogWarning($"[DockerAutoStart] Exit code {process.ExitCode}\n{error}");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[DockerAutoStart] Could not run docker compose: {e.Message}");
                return false;
            }
        }

        private static void StartNodeBackend(string backendPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npm install --omit=dev && npm start",
                    WorkingDirectory = backendPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                UnityEngine.Debug.Log("[BackendAutoStart] Docker unavailable. Starting local Node auth backend on http://localhost:3001");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BackendAutoStart] Could not start local Node backend: {e.Message}");
            }
        }

        private static bool IsPortOpen(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                bool connected = result.AsyncWaitHandle.WaitOne(250);
                if (connected)
                {
                    client.EndConnect(result);
                }
                return connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
