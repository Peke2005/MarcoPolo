using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
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
                NetworkTransport = transport,
                ForceSamePrefabs = false
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
        public event Action OnLobbyPlayersChanged;

        private NetworkManager _networkManager;
        private float _heartbeatTimer;
        private const float HEARTBEAT_INTERVAL = 15f;
        private const string LOBBY_UPDATE_MESSAGE = "FP_LOBBY_UPDATE";
        private const string LOBBY_STATE_MESSAGE = "FP_LOBBY_STATE";
        private readonly Dictionary<ulong, LobbyPlayerInfo> _lobbyPlayers = new Dictionary<ulong, LobbyPlayerInfo>();
        private LobbyPlayerInfo _localLobbyInfo;
        private bool _messagesRegistered;

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
                        NetworkTransport = transport,
                        ForceSamePrefabs = false
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

            // Dev/editor and build clients must be allowed to connect while prefabs are repaired.
            // Registered prefab list still must contain valid NetworkObjects for spawned prefabs.
            _networkManager.NetworkConfig.ForceSamePrefabs = false;

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
                    ClearLobbyState();
                    await LobbyManager.LeaveLobby();
                    return;
                }

                IsHost = true;
                _heartbeatTimer = 0f;
                RegisterLobbyMessages();
                SubmitLobbyPlayerState(GameConfig.Preferences.playerName, GameConfig.Preferences.abilityIndex, GameConfig.Preferences.colorIndex, false);

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
                RegisterLobbyMessages();
                SubmitLobbyPlayerState(GameConfig.Preferences.playerName, GameConfig.Preferences.abilityIndex, GameConfig.Preferences.colorIndex, false);

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

            ClearLobbyState();

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
            RegisterLobbyMessages();

            if (_networkManager != null && _networkManager.IsServer)
            {
                EnsureLobbyPlayer(clientId);
                BroadcastLobbyState();
            }

            if (_networkManager != null && clientId == _networkManager.LocalClientId)
            {
                SendLocalLobbyInfo();
            }

            OnPlayerConnected?.Invoke(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Session] Client disconnected: {clientId}");
            if (_networkManager != null && _networkManager.IsServer)
            {
                _lobbyPlayers.Remove(clientId);
                BroadcastLobbyState();
            }

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

            ClearLobbyState();

            await LobbyManager.LeaveLobby();

            JoinCode = null;
            IsHost = false;
        }

        private void OnDestroy()
        {
            UnregisterLobbyMessages();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public readonly struct LobbyPlayerInfo
        {
            public readonly ulong ClientId;
            public readonly string PlayerName;
            public readonly int AbilityIndex;
            public readonly int FactionIndex;
            public readonly bool IsReady;

            public LobbyPlayerInfo(ulong clientId, string playerName, int abilityIndex, int factionIndex, bool isReady)
            {
                ClientId = clientId;
                PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Jugador" : playerName;
                AbilityIndex = Mathf.Clamp(abilityIndex, 0, 2);
                FactionIndex = Mathf.Clamp(factionIndex, 0, 1);
                IsReady = isReady;
            }
        }

        public IReadOnlyList<LobbyPlayerInfo> GetLobbyPlayers()
        {
            var players = new List<LobbyPlayerInfo>(_lobbyPlayers.Values);
            players.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
            return players;
        }

        public bool AreLobbyPlayersReady(int expectedPlayers = 2)
        {
            if (_lobbyPlayers.Count < expectedPlayers) return false;
            foreach (var player in _lobbyPlayers.Values)
            {
                if (!player.IsReady) return false;
            }
            return true;
        }

        public void SubmitLobbyPlayerState(string playerName, int abilityIndex, int factionIndex, bool isReady)
        {
            ulong localId = _networkManager != null ? _networkManager.LocalClientId : 0;
            _localLobbyInfo = new LobbyPlayerInfo(localId, playerName, abilityIndex, factionIndex, isReady);

            if (_networkManager == null || !_networkManager.IsListening)
            {
                return;
            }

            if (_networkManager.IsServer)
            {
                _lobbyPlayers[_networkManager.LocalClientId] = _localLobbyInfo;
                BroadcastLobbyState();
            }
            else
            {
                SendLocalLobbyInfo();
            }
        }

        private void RegisterLobbyMessages()
        {
            if (_messagesRegistered || _networkManager == null || _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            if (_networkManager.IsServer)
            {
                _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(LOBBY_UPDATE_MESSAGE, HandleLobbyUpdateMessage);
            }
            else
            {
                _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(LOBBY_STATE_MESSAGE, HandleLobbyStateMessage);
            }

            _messagesRegistered = true;
        }

        private void UnregisterLobbyMessages()
        {
            if (!_messagesRegistered || _networkManager == null || _networkManager.CustomMessagingManager == null)
            {
                _messagesRegistered = false;
                return;
            }

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(LOBBY_UPDATE_MESSAGE);
            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(LOBBY_STATE_MESSAGE);
            _messagesRegistered = false;
        }

        private void SendLocalLobbyInfo()
        {
            if (_networkManager == null || !_networkManager.IsListening || _networkManager.IsServer ||
                _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            WriteLobbyPlayer(writer, _localLobbyInfo);
            _networkManager.CustomMessagingManager.SendNamedMessage(LOBBY_UPDATE_MESSAGE, NetworkManager.ServerClientId, writer);
        }

        private void HandleLobbyUpdateMessage(ulong senderClientId, FastBufferReader reader)
        {
            LobbyPlayerInfo info = ReadLobbyPlayer(reader, senderClientId);
            _lobbyPlayers[senderClientId] = info;
            BroadcastLobbyState();
        }

        private void BroadcastLobbyState()
        {
            if (_networkManager == null || !_networkManager.IsServer || _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            EnsureLobbyPlayer(_networkManager.LocalClientId);

            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (clientId == _networkManager.LocalClientId) continue;

                using var writer = new FastBufferWriter(1024, Allocator.Temp);
                WriteLobbyState(writer);
                _networkManager.CustomMessagingManager.SendNamedMessage(LOBBY_STATE_MESSAGE, clientId, writer);
            }

            OnLobbyPlayersChanged?.Invoke();
        }

        private void HandleLobbyStateMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            _lobbyPlayers.Clear();

            for (int i = 0; i < count; i++)
            {
                LobbyPlayerInfo info = ReadLobbyPlayer(reader, 0);
                _lobbyPlayers[info.ClientId] = info;
            }

            OnLobbyPlayersChanged?.Invoke();
        }

        private void EnsureLobbyPlayer(ulong clientId)
        {
            if (_lobbyPlayers.ContainsKey(clientId)) return;

            string fallbackName = clientId == (_networkManager != null ? _networkManager.LocalClientId : 0)
                ? GameConfig.Preferences.playerName
                : $"Jugador {clientId + 1}";

            _lobbyPlayers[clientId] = new LobbyPlayerInfo(clientId, fallbackName, 0, clientId == 0 ? 0 : 1, false);
        }

        private void ClearLobbyState()
        {
            UnregisterLobbyMessages();
            _lobbyPlayers.Clear();
            _localLobbyInfo = default;
            OnLobbyPlayersChanged?.Invoke();
        }

        private void WriteLobbyState(FastBufferWriter writer)
        {
            var players = GetLobbyPlayers();
            writer.WriteValueSafe(players.Count);
            foreach (var player in players)
            {
                WriteLobbyPlayer(writer, player);
            }
        }

        private static void WriteLobbyPlayer(FastBufferWriter writer, LobbyPlayerInfo info)
        {
            ulong clientId = info.ClientId;
            var name = new FixedString64Bytes(string.IsNullOrWhiteSpace(info.PlayerName) ? "Jugador" : info.PlayerName);
            int ability = info.AbilityIndex;
            int faction = info.FactionIndex;
            bool ready = info.IsReady;

            writer.WriteValueSafe(clientId);
            writer.WriteValueSafe(name);
            writer.WriteValueSafe(ability);
            writer.WriteValueSafe(faction);
            writer.WriteValueSafe(ready);
        }

        private static LobbyPlayerInfo ReadLobbyPlayer(FastBufferReader reader, ulong senderClientId)
        {
            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out FixedString64Bytes name);
            reader.ReadValueSafe(out int ability);
            reader.ReadValueSafe(out int faction);
            reader.ReadValueSafe(out bool ready);

            if (senderClientId != 0)
            {
                clientId = senderClientId;
            }

            return new LobbyPlayerInfo(clientId, name.ToString(), ability, faction, ready);
        }
    }
}
