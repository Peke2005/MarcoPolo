using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using FrentePartido.Core;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Singleton that manages the full session lifecycle: Relay allocation,
    /// Lobby creation, host/client startup, and graceful shutdown.
    /// Persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class NetworkSessionManager : MonoBehaviour
    {
        public static NetworkSessionManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null) return;

            var existingSessionManager = FindFirstObjectByType<NetworkSessionManager>();
            if (existingSessionManager != null)
            {
                Instance = existingSessionManager;
                return;
            }

            var existingNetworkManager = NetworkManager.Singleton ?? FindFirstObjectByType<NetworkManager>();
            if (existingNetworkManager != null)
            {
                existingNetworkManager.gameObject.AddComponent<NetworkSessionManager>();
                return;
            }

            var go = new GameObject("NetworkManager");
            var transport = go.AddComponent<UnityTransport>();
            var netManager = go.AddComponent<NetworkManager>();
            netManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport
            };
            go.AddComponent<NetworkSessionManager>();

            Debug.LogWarning("[Session] No NetworkManager found in first loaded scene. Created runtime fallback NetworkManager.");
        }

        public string JoinCode { get; private set; }
        public bool IsHost { get; private set; }

        // ── Events ──────────────────────────────────────────────────
        public event Action OnSessionCreated;
        public event Action OnSessionJoined;
        public event Action OnSessionEnded;
        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;

        private NetworkManager _networkManager;
        private float _heartbeatTimer;
        private const float HEARTBEAT_INTERVAL = 15f;

        // ── Singleton Setup ─────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton ?? FindFirstObjectByType<NetworkManager>();

                if (_networkManager == null)
                {
                    _networkManager = gameObject.AddComponent<NetworkManager>();
                    var transport = GetComponent<UnityTransport>() ?? gameObject.AddComponent<UnityTransport>();
                    _networkManager.NetworkConfig = new NetworkConfig
                    {
                        NetworkTransport = transport
                    };

                    Debug.LogWarning("[Session] NetworkManager was missing. Created fallback NetworkManager + UnityTransport.");
                }
            }
        }

        private bool EnsureNetworkConfigReady()
        {
            if (_networkManager == null)
            {
                Debug.LogError("[Session] NetworkManager reference is null.");
                return false;
            }

            if (_networkManager.NetworkConfig == null)
            {
                _networkManager.NetworkConfig = new NetworkConfig();
            }

            if (_networkManager.NetworkConfig.NetworkTransport == null)
            {
                var transport = _networkManager.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("[Session] UnityTransport is missing on NetworkManager.");
                    return false;
                }

                _networkManager.NetworkConfig.NetworkTransport = transport;
                Debug.LogWarning("[Session] NetworkTransport was null in NetworkConfig. Assigned UnityTransport automatically.");
            }

            if (_networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[Session] NetworkPrefabsLists is empty. Spawned NetworkObjects may fail on clients.");
            }

            return true;
        }

        private void OnEnable()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback += HandleClientConnected;
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        private void OnDisable()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= HandleClientConnected;
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        private void Update()
        {
            if (!IsHost || !LobbyManager.IsInLobby) return;

            _heartbeatTimer += Time.unscaledDeltaTime;
            if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                _heartbeatTimer = 0f;
                _ = LobbyManager.HeartbeatLobby();
            }
        }

        // ── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Host flow: allocate Relay, create Lobby, start NetworkManager as host.
        /// </summary>
        public async Task CreateSession()
        {
            try
            {
                if (!ServiceInitializer.IsInitialized)
                {
                    await ServiceInitializer.InitializeAsync();
                }

                if (!EnsureNetworkConfigReady())
                {
                    return;
                }

                // 1. Relay allocation
                var (joinCode, _) = await RelayConnectionManager.CreateRelayAllocation(2);
                JoinCode = joinCode;

                // 2. Lobby creation with relay join code
                string lobbyName = $"{GameConfig.Preferences.playerName}'s Match";
                await LobbyManager.CreateLobby(lobbyName, joinCode, 2);

                // 3. Start host
                if (!_networkManager.StartHost())
                {
                    Debug.LogError("[Session] NetworkManager.StartHost failed.");
                    await LobbyManager.LeaveLobby();
                    return;
                }

                IsHost = true;
                _heartbeatTimer = 0f;

                Debug.Log($"[Session] Host session created. JoinCode: {JoinCode}");
                OnSessionCreated?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Session] CreateSession failed: {e.Message}");
                await CleanupOnFailure();
                throw;
            }
        }

        /// <summary>
        /// Client flow: join Relay via code, start NetworkManager as client.
        /// The joinCode is the Relay join code (obtained from lobby data or shared directly).
        /// </summary>
        public async Task JoinSession(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogError("[Session] Join code is null or empty.");
                return;
            }

            try
            {
                if (!ServiceInitializer.IsInitialized)
                {
                    await ServiceInitializer.InitializeAsync();
                }

                if (!EnsureNetworkConfigReady())
                {
                    return;
                }

                // 1. Join Relay allocation
                await RelayConnectionManager.JoinRelayAllocation(joinCode);
                JoinCode = joinCode;

                // 2. Start client
                if (!_networkManager.StartClient())
                {
                    Debug.LogError("[Session] NetworkManager.StartClient failed.");
                    return;
                }

                IsHost = false;

                Debug.Log($"[Session] Client joining session. JoinCode: {JoinCode}");
                OnSessionJoined?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Session] JoinSession failed: {e.Message}");
                await CleanupOnFailure();
                throw;
            }
        }

        /// <summary>
        /// Shuts down networking, leaves the lobby, and returns to main menu.
        /// Safe to call from either host or client.
        /// </summary>
        public void LeaveSession()
        {
            Debug.Log("[Session] Leaving session...");

            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }

            // Fire-and-forget lobby cleanup — we're about to change scenes anyway
            _ = LobbyManager.LeaveLobby();

            JoinCode = null;
            IsHost = false;
            _heartbeatTimer = 0f;

            OnSessionEnded?.Invoke();

            SceneFlowController.LoadScene(SceneFlowController.SCENE_MAIN_MENU);
        }

        // ── Callbacks ───────────────────────────────────────────────
        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[Session] Client connected: {clientId}");
            OnPlayerConnected?.Invoke(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Session] Client disconnected: {clientId}");
            OnPlayerDisconnected?.Invoke(clientId);

            // If we are the client and got disconnected, leave session
            if (!IsHost && clientId == _networkManager.LocalClientId)
            {
                Debug.LogWarning("[Session] Local client was disconnected from host.");
                LeaveSession();
            }
        }

        private async Task CleanupOnFailure()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }

            await LobbyManager.LeaveLobby();

            JoinCode = null;
            IsHost = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
