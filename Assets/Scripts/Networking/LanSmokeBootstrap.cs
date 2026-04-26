using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using FrentePartido.Player;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Standalone LAN smoke harness. Inactive unless launched with -fpLanHost or -fpLanClient.
    /// Used to verify real player builds without Relay/Lobby credentials.
    /// </summary>
    public static class LanSmokeBootstrap
    {
        private const string HostArg = "-fpLanHost";
        private const string ClientArg = "-fpLanClient";

        public static bool HasLanSmokeArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            return HasArg(args, HostArg) || HasArg(args, ClientArg);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryStart(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryStart(scene);

        private static void TryStart(Scene scene)
        {
            if (!scene.IsValid() || scene.name != Core.SceneFlowController.SCENE_GAME) return;
            if (!HasLanSmokeArgs()) return;
            if (GameObject.Find("~LanSmokeBootstrap") != null) return;

            var go = new GameObject("~LanSmokeBootstrap");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private bool _isHost;
            private float _logTimer;

            private IEnumerator Start()
            {
                yield return null;

                string[] args = System.Environment.GetCommandLineArgs();
                _isHost = HasArg(args, HostArg);
                string address = GetArg(args, "-fpAddress", "127.0.0.1");
                ushort port = (ushort)Mathf.Clamp(GetIntArg(args, "-fpPort", 7777), 1, 65535);
                float quitAfter = Mathf.Max(0f, GetFloatArg(args, "-fpQuitAfter", 10f));

                var nm = EnsureNetworkManager();
                var transport = nm.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();
                nm.NetworkConfig ??= new NetworkConfig();
                nm.NetworkConfig.NetworkTransport = transport;
                transport.SetConnectionData(address, port, "0.0.0.0");

                RegisterPlayerPrefab(nm);

                bool ok = _isHost ? nm.StartHost() : nm.StartClient();
                Debug.Log($"[LanSmoke] Start {(_isHost ? "host" : "client")} addr={address} port={port} ok={ok}");

                if (!ok)
                {
                    Application.Quit(2);
                    yield break;
                }

                if (_isHost && HasArg(args, "-fpCombatSmoke"))
                {
                    StartCoroutine(RunCombatSmoke());
                }

                if (quitAfter > 0f)
                {
                    StartCoroutine(CaptureAndQuit(args, quitAfter));
                }
            }

            private void Update()
            {
                _logTimer += Time.unscaledDeltaTime;
                if (_logTimer < 1f) return;
                _logTimer = 0f;

                var nm = NetworkManager.Singleton;
                if (nm == null) return;

                int spawned = nm.SpawnManager != null ? nm.SpawnManager.SpawnedObjects.Count : 0;
                Debug.Log($"[LanSmoke] role={(_isHost ? "host" : "client")} listening={nm.IsListening} connected={nm.ConnectedClientsIds.Count} spawned={spawned}");
            }

            private IEnumerator CaptureAndQuit(string[] args, float quitAfter)
            {
                yield return new WaitForSeconds(quitAfter);

                string path = GetArg(args, "-fpShot", "");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    string fullPath = Path.GetFullPath(path);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    ScreenCapture.CaptureScreenshot(fullPath);
                    Debug.Log($"[LanSmoke] Screenshot {fullPath}");
                    yield return new WaitForSeconds(0.5f);
                }

                Application.Quit(0);
            }

            private IEnumerator RunCombatSmoke()
            {
                var nm = NetworkManager.Singleton;
                float deadline = Time.realtimeSinceStartup + 12f;

                while (Time.realtimeSinceStartup < deadline)
                {
                    if (nm != null && nm.ConnectedClientsIds.Count >= 2 && GetSpawnedPlayerHealths().Count >= 2)
                    {
                        break;
                    }
                    yield return new WaitForSeconds(0.25f);
                }

                List<PlayerHealth> players = GetSpawnedPlayerHealths();
                if (players.Count < 2)
                {
                    Debug.LogError($"[LanSmoke] Combat check failed: players={players.Count}");
                    Application.Quit(3);
                    yield break;
                }

                PlayerHealth attacker = players[0];
                PlayerHealth target = players[1];
                ulong attackerId = attacker.NetworkObject != null ? attacker.NetworkObject.OwnerClientId : 0;

                Debug.Log($"[LanSmoke] Combat check start players={players.Count} targetHp={target.CurrentHealth.Value}");

                for (int i = 0; i < 5; i++)
                {
                    target.TakeDamageServerRpc(20, attackerId);
                    yield return new WaitForSeconds(0.15f);
                }

                bool damageOk = target.CurrentHealth.Value == 0;
                bool deathOk = target.IsDead;
                Debug.Log($"[LanSmoke] Combat check damageOk={damageOk} deathOk={deathOk} targetHp={target.CurrentHealth.Value}");

                if (!damageOk || !deathOk)
                {
                    Application.Quit(4);
                }
            }

            private static List<PlayerHealth> GetSpawnedPlayerHealths()
            {
                var players = new List<PlayerHealth>();
                var nm = NetworkManager.Singleton;
                if (nm == null || nm.SpawnManager == null) return players;

                foreach (NetworkObject netObj in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (netObj == null || !netObj.IsSpawned) continue;
                    if (!netObj.IsPlayerObject) continue;

                    var health = netObj.GetComponent<PlayerHealth>();
                    if (health != null) players.Add(health);
                }

                return players;
            }

            private static NetworkManager EnsureNetworkManager()
            {
                var nm = NetworkManager.Singleton ?? FindFirstObjectByType<NetworkManager>();
                if (nm != null) return nm;

                var go = new GameObject("NetworkManager");
                var transport = go.AddComponent<UnityTransport>();
                nm = go.AddComponent<NetworkManager>();
                nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
                return nm;
            }

            private static void RegisterPlayerPrefab(NetworkManager nm)
            {
                var spawn = FindFirstObjectByType<PlayerSpawnManager>();
                if (spawn == null || spawn.PlayerPrefab == null)
                {
                    Debug.LogWarning("[LanSmoke] PlayerSpawnManager/PlayerPrefab missing.");
                    return;
                }

                try
                {
                    nm.AddNetworkPrefab(spawn.PlayerPrefab);
                    Debug.Log("[LanSmoke] Registered player prefab.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LanSmoke] AddNetworkPrefab: {e.Message}");
                }
            }
        }

        private static bool HasArg(string[] args, string key)
        {
            foreach (string arg in args)
            {
                if (arg == key) return true;
            }
            return false;
        }

        private static string GetArg(string[] args, string key, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == key) return args[i + 1];
            }
            return fallback;
        }

        private static int GetIntArg(string[] args, string key, int fallback)
        {
            string value = GetArg(args, key, "");
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }

        private static float GetFloatArg(string[] args, string key, float fallback)
        {
            string value = GetArg(args, key, "");
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
        }
    }
}
