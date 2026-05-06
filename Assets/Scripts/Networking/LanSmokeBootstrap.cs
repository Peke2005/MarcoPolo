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
        private const string ObserveClientMoveArg = "-fpObserveClientMoveSmoke";

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
                float quitAfter = Mathf.Max(0f, GetFloatArg(args, "-fpQuitAfter", 0f));

                var nm = EnsureNetworkManager();
                var transport = nm.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();
                nm.NetworkConfig ??= new NetworkConfig();
                nm.NetworkConfig.NetworkTransport = transport;
                transport.SetConnectionData(address, port, "0.0.0.0");

                NetworkPrefabRegistry.RegisterDefaults(nm);

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

                if (_isHost && HasArg(args, "-fpPickupSmoke"))
                {
                    StartCoroutine(RunPickupSmoke());
                }

                if (_isHost && HasArg(args, "-fpAbilitySmoke"))
                {
                    StartCoroutine(RunAbilitySmoke());
                }

                if (_isHost && HasArg(args, ObserveClientMoveArg))
                {
                    StartCoroutine(RunObserveClientMoveSmoke());
                }

                if (!_isHost && HasArg(args, "-fpMoveSmoke"))
                {
                    StartCoroutine(RunClientMoveSmoke());
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
                    target.ApplyDamageServer(20, attackerId);
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

            private IEnumerator RunPickupSmoke()
            {
                float deadline = Time.realtimeSinceStartup + 12f;

                while (Time.realtimeSinceStartup < deadline)
                {
                    if (GetSpawnedPlayerHealths().Count >= 2)
                        break;

                    yield return new WaitForSeconds(0.25f);
                }

                List<PlayerHealth> players = GetSpawnedPlayerHealths();
                if (players.Count < 2)
                {
                    Debug.LogError($"[LanSmoke] Pickup check failed: players={players.Count}");
                    Application.Quit(10);
                    yield break;
                }

                bool allOk = true;
                foreach (PlayerHealth player in players)
                {
                    bool scenarioOk = false;
                    yield return StartCoroutine(RunPickupScenario(player, value => scenarioOk = value));
                    allOk &= scenarioOk;
                }

                Debug.Log($"[LanSmoke] Pickup check allOk={allOk}");
                if (!allOk)
                    Application.Quit(11);
            }

            private IEnumerator RunPickupScenario(PlayerHealth player, System.Action<bool> onComplete)
            {
                bool ok = false;
                if (player == null || player.NetworkObject == null)
                {
                    onComplete?.Invoke(false);
                    yield break;
                }

                ulong clientId = player.NetworkObject.OwnerClientId;
                player.CurrentHealth.Value = 25;
                player.CurrentArmor.Value = 0;
                yield return new WaitForSeconds(0.1f);

                ApplySmokeHealthPickup(player, 25);
                yield return new WaitForSeconds(0.25f);
                int afterHeal = player.CurrentHealth.Value;
                int armorAfterHeal = player.CurrentArmor.Value;

                player.CurrentHealth.Value = player.MaxHealthValue;
                player.CurrentArmor.Value = 0;
                yield return new WaitForSeconds(0.1f);

                ApplySmokeHealthPickup(player, 25);
                yield return new WaitForSeconds(0.25f);
                int afterFull = player.CurrentHealth.Value;
                int armorAfterFull = player.CurrentArmor.Value;

                ok = afterHeal == 50 && armorAfterHeal == 0 && afterFull == player.MaxHealthValue && armorAfterFull > 0;
                Debug.Log($"[LanSmoke] Pickup check client={clientId} healHp=25->{afterHeal} healArmor={armorAfterHeal} fullHp={afterFull} fullArmor={armorAfterFull} ok={ok}");

                onComplete?.Invoke(ok);
            }

            private static void ApplySmokeHealthPickup(PlayerHealth health, int amount)
            {
                int missingHealth = Mathf.Max(0, health.MaxHealthValue - health.CurrentHealth.Value);
                int healAmount = Mathf.Min(amount, missingHealth);
                int shieldAmount = amount - healAmount;
                if (healAmount > 0) health.HealServer(healAmount);
                if (shieldAmount > 0) health.AddArmorServer(shieldAmount);
            }

            private IEnumerator RunAbilitySmoke()
            {
                float deadline = Time.realtimeSinceStartup + 12f;

                while (Time.realtimeSinceStartup < deadline)
                {
                    if (GetSpawnedPlayerHealths().Count >= 2)
                        break;

                    yield return new WaitForSeconds(0.25f);
                }

                List<PlayerHealth> players = GetSpawnedPlayerHealths();
                if (players.Count < 2)
                {
                    Debug.LogError($"[LanSmoke] Ability check failed: players={players.Count}");
                    Application.Quit(12);
                    yield break;
                }

                bool dashOk = false;
                bool shieldOk = false;
                bool mineOk = false;

                PlayerHealth p0 = players[0];
                PlayerHealth p1 = players[1];

                var motor = p0.GetComponent<PlayerMotor2D>();
                Component dash = p0.GetComponent("DashAbility");
                if (motor != null && dash != null)
                {
                    Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.down, Vector2.left };
                    float bestMoved = 0f;
                    for (int i = 0; i < dirs.Length && !dashOk; i++)
                    {
                        Vector3 start = p0.transform.position;
                        InvokePublic(dash, "Execute", motor, dirs[i], 3f, 18f);
                        yield return new WaitForSeconds(0.6f);
                        float moved = Vector2.Distance(start, p0.transform.position);
                        bestMoved = Mathf.Max(bestMoved, moved);
                        dashOk = moved > 1.5f;
                    }
                    Debug.Log($"[LanSmoke] Ability dash bestMoved={bestMoved:0.00} ok={dashOk}");
                }

                Component shield = p1.GetComponent("ShieldAbility");
                if (shield != null)
                {
                    p1.CurrentHealth.Value = p1.MaxHealthValue;
                    InvokePublic(shield, "ActivateShieldServer", Vector2.left, 2.5f, 60f);
                    yield return new WaitForSeconds(0.1f);
                    int hpBefore = p1.CurrentHealth.Value;
                    int passthrough = InvokePublicInt(shield, "AbsorbDamage", 20);
                    yield return new WaitForSeconds(0.1f);
                    shieldOk = passthrough == 0 && p1.CurrentHealth.Value == hpBefore;
                    Debug.Log($"[LanSmoke] Ability shield hp={hpBefore}->{p1.CurrentHealth.Value} passthrough={passthrough} ok={shieldOk}");
                }

                Component mine = p0.GetComponent("MineAbility");
                if (mine != null)
                {
                    int before = CountMineObjects();
                    InvokePublic(mine, "PlaceMineServer", (Vector2)p0.transform.position + Vector2.up * 1.5f, 35, 1.5f);
                    yield return new WaitForSeconds(0.2f);
                    int after = CountMineObjects();
                    mineOk = after > before;
                    Debug.Log($"[LanSmoke] Ability mine count={before}->{after} ok={mineOk}");
                }

                bool allOk = dashOk && shieldOk && mineOk;
                Debug.Log($"[LanSmoke] Ability check allOk={allOk}");
                if (!allOk)
                    Application.Quit(13);
            }

            private static void InvokePublic(Component component, string methodName, params object[] args)
            {
                if (component == null) return;
                var method = component.GetType().GetMethod(methodName);
                method?.Invoke(component, args);
            }

            private static int InvokePublicInt(Component component, string methodName, params object[] args)
            {
                if (component == null) return 0;
                var method = component.GetType().GetMethod(methodName);
                object result = method != null ? method.Invoke(component, args) : null;
                return result is int value ? value : 0;
            }

            private static int CountMineObjects()
            {
                int count = 0;
                foreach (var mine in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mine != null && mine.GetType().Name == "ProximityMine")
                        count++;
                }
                return count;
            }

            private IEnumerator RunClientMoveSmoke()
            {
                var nm = NetworkManager.Singleton;
                float deadline = Time.realtimeSinceStartup + 10f;
                NetworkObject localPlayer = null;

                while (Time.realtimeSinceStartup < deadline)
                {
                    localPlayer = nm != null && nm.SpawnManager != null
                        ? nm.SpawnManager.GetLocalPlayerObject()
                        : null;
                    if (localPlayer != null) break;
                    yield return new WaitForSeconds(0.25f);
                }

                if (localPlayer == null)
                {
                    Debug.LogError("[LanSmoke] Move check failed: local player missing");
                    Application.Quit(5);
                    yield break;
                }

                var motor = localPlayer.GetComponent<PlayerMotor2D>();
                if (motor == null)
                {
                    Debug.LogError("[LanSmoke] Move check failed: PlayerMotor2D missing");
                    Application.Quit(6);
                    yield break;
                }

                Vector3 start = localPlayer.transform.position;
                yield return new WaitForSeconds(4f);
                start = localPlayer.transform.position;
                Debug.Log($"[LanSmoke] Move check owner={localPlayer.IsOwner} ownerId={localPlayer.OwnerClientId} localId={nm.LocalClientId} motorEnabled={motor.enabled} movementEnabled={motor.IsMovementEnabled}");
                motor.SetExternalMoveInput(Vector2.up);
                yield return new WaitForSeconds(2f);
                motor.SetExternalMoveInput(Vector2.zero);
                yield return new WaitForSeconds(0.5f);

                Vector3 end = localPlayer.transform.position;
                float moved = Vector2.Distance(start, end);
                Debug.Log($"[LanSmoke] Move check start={start} end={end} moved={moved:0.00}");

                if (moved < 0.5f)
                {
                    Debug.LogError("[LanSmoke] Move check failed: client player did not move");
                    Application.Quit(7);
                }
            }

            private IEnumerator RunObserveClientMoveSmoke()
            {
                var nm = NetworkManager.Singleton;
                float deadline = Time.realtimeSinceStartup + 12f;
                NetworkObject clientPlayer = null;

                while (Time.realtimeSinceStartup < deadline)
                {
                    if (nm != null && nm.ConnectedClientsIds.Count >= 2)
                    {
                        foreach (NetworkObject netObj in nm.SpawnManager.SpawnedObjectsList)
                        {
                            if (netObj == null || !netObj.IsSpawned || !netObj.IsPlayerObject) continue;
                            if (netObj.OwnerClientId != NetworkManager.ServerClientId)
                            {
                                clientPlayer = netObj;
                                break;
                            }
                        }
                    }

                    if (clientPlayer != null) break;
                    yield return new WaitForSeconds(0.25f);
                }

                if (clientPlayer == null)
                {
                    Debug.LogError("[LanSmoke] Host observe move failed: client player missing");
                    Application.Quit(8);
                    yield break;
                }

                Vector3 start = clientPlayer.transform.position;
                yield return new WaitForSeconds(4f);
                if (clientPlayer == null || !clientPlayer.IsSpawned)
                {
                    Debug.LogError("[LanSmoke] Host observe move failed: client disconnected before movement window");
                    Application.Quit(8);
                    yield break;
                }
                start = clientPlayer.transform.position;
                yield return new WaitForSeconds(3.5f);
                if (clientPlayer == null || !clientPlayer.IsSpawned)
                {
                    Debug.LogError("[LanSmoke] Host observe move failed: client disconnected during movement window");
                    Application.Quit(8);
                    yield break;
                }
                Vector3 end = clientPlayer.transform.position;
                float moved = Vector2.Distance(start, end);
                Debug.Log($"[LanSmoke] Host observe move start={start} end={end} moved={moved:0.00}");

                if (moved < 0.5f)
                {
                    Debug.LogError("[LanSmoke] Host observe move failed: server did not receive client movement");
                    Application.Quit(9);
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
