using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Networking
{
    /// <summary>
    /// INetworkSerializable struct holding per-player session data.
    /// Synchronized via NetworkList so both clients can read all player info.
    /// </summary>
    public struct PlayerSessionData : INetworkSerializable, IEquatable<PlayerSessionData>
    {
        public ulong ClientId;
        public FixedString64Bytes PlayerName;
        public byte Faction;
        public FixedString32Bytes AbilityId;
        public bool IsReady;

        public PlayerFaction PlayerFaction => (PlayerFaction)Faction;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref Faction);
            serializer.SerializeValue(ref AbilityId);
            serializer.SerializeValue(ref IsReady);
        }

        public bool Equals(PlayerSessionData other)
        {
            return ClientId == other.ClientId &&
                   PlayerName.Equals(other.PlayerName) &&
                   Faction == other.Faction &&
                   AbilityId.Equals(other.AbilityId) &&
                   IsReady == other.IsReady;
        }

        public override bool Equals(object obj) => obj is PlayerSessionData other && Equals(other);
        public override int GetHashCode() => ClientId.GetHashCode();
    }

    /// <summary>
    /// Authoritative game state that lives on the server and is replicated to all clients.
    /// Tracks match state, scores, player IDs, and per-player session data.
    /// </summary>
    public class NetworkGameState : NetworkBehaviour
    {
        public static NetworkGameState Instance { get; private set; }

        // ── Network Variables ───────────────────────────────────────
        public NetworkVariable<MatchState> CurrentMatchState = new NetworkVariable<MatchState>(
            MatchState.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<byte> Player1Score = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<byte> Player2Score = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<ulong> Player1ClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<ulong> Player2ClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkList<PlayerSessionData> Players;

        // ── Events ──────────────────────────────────────────────────
        public event Action<MatchState, MatchState> OnMatchStateChanged;
        public event Action<byte, byte> OnScoreChanged; // (p1Score, p2Score)

        // ── Lifecycle ───────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Players = new NetworkList<PlayerSessionData>();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            CurrentMatchState.OnValueChanged += HandleMatchStateChanged;
            Player1Score.OnValueChanged += HandleScoreChanged;
            Player2Score.OnValueChanged += HandleScoreChanged;

            // If server, initialize state
            if (IsServer)
            {
                CurrentMatchState.Value = MatchState.WaitingForPlayers;
                Player1Score.Value = 0;
                Player2Score.Value = 0;
            }

            Debug.Log($"[GameState] Spawned. IsServer: {IsServer}, MatchState: {CurrentMatchState.Value}");
        }

        public override void OnNetworkDespawn()
        {
            CurrentMatchState.OnValueChanged -= HandleMatchStateChanged;
            Player1Score.OnValueChanged -= HandleScoreChanged;
            Player2Score.OnValueChanged -= HandleScoreChanged;
        }

        // ── Server RPCs ─────────────────────────────────────────────

        /// <summary>
        /// Called by a client to register or update their session data (name, ability, ready state).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void UpdatePlayerDataServerRpc(PlayerSessionData data, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            data.ClientId = senderId; // Server authoritative over client ID

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == senderId)
                {
                    Players[i] = data;
                    Debug.Log($"[GameState] Updated player data for client {senderId}");
                    return;
                }
            }

            // New player — add to list
            Players.Add(data);
            Debug.Log($"[GameState] Registered new player: {data.PlayerName} (client {senderId})");
        }

        /// <summary>
        /// Called by a client to toggle their ready state.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerReadyServerRpc(bool isReady, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == senderId)
                {
                    PlayerSessionData updated = Players[i];
                    updated.IsReady = isReady;
                    Players[i] = updated;
                    Debug.Log($"[GameState] Client {senderId} ready: {isReady}");
                    return;
                }
            }

            Debug.LogWarning($"[GameState] SetPlayerReady — client {senderId} not found in Players list.");
        }

        /// <summary>
        /// Called by a client to select their ability.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerAbilityServerRpc(FixedString32Bytes abilityId, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == senderId)
                {
                    PlayerSessionData updated = Players[i];
                    updated.AbilityId = abilityId;
                    Players[i] = updated;
                    Debug.Log($"[GameState] Client {senderId} selected ability: {abilityId}");
                    return;
                }
            }

            Debug.LogWarning($"[GameState] SetPlayerAbility — client {senderId} not found in Players list.");
        }

        // ── Server-Only Methods ─────────────────────────────────────

        /// <summary>
        /// Assigns client IDs to player slots. Call from server when both players connect.
        /// </summary>
        public void AssignPlayerSlots(ulong player1Id, ulong player2Id)
        {
            if (!IsServer) return;

            Player1ClientId.Value = player1Id;
            Player2ClientId.Value = player2Id;

            // Assign factions in the Players list
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerSessionData data = Players[i];
                if (data.ClientId == player1Id)
                {
                    data.Faction = (byte)PlayerFaction.Blue;
                    Players[i] = data;
                }
                else if (data.ClientId == player2Id)
                {
                    data.Faction = (byte)PlayerFaction.Red;
                    Players[i] = data;
                }
            }

            Debug.Log($"[GameState] Slots assigned — P1: {player1Id} (Blue), P2: {player2Id} (Red)");
        }

        /// <summary>
        /// Server sets the match state. Propagated automatically via NetworkVariable.
        /// </summary>
        public void SetMatchState(MatchState state)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameState] Only server can set match state.");
                return;
            }

            CurrentMatchState.Value = state;
        }

        /// <summary>
        /// Server awards a point to a player (by slot index 1 or 2).
        /// </summary>
        public void AddScore(int playerSlot, byte points = 1)
        {
            if (!IsServer) return;

            if (playerSlot == 1)
                Player1Score.Value = (byte)Mathf.Min(Player1Score.Value + points, 255);
            else if (playerSlot == 2)
                Player2Score.Value = (byte)Mathf.Min(Player2Score.Value + points, 255);
            else
                Debug.LogWarning($"[GameState] Invalid player slot: {playerSlot}");
        }

        /// <summary>
        /// Resets scores to zero. Call between matches or on rematch.
        /// </summary>
        public void ResetScores()
        {
            if (!IsServer) return;

            Player1Score.Value = 0;
            Player2Score.Value = 0;
        }

        /// <summary>
        /// Returns the PlayerSessionData for a given client ID, or null if not found.
        /// </summary>
        public PlayerSessionData? GetPlayerData(ulong clientId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == clientId)
                    return Players[i];
            }
            return null;
        }

        /// <summary>
        /// Returns the player slot (1 or 2) for a given client ID, or 0 if not assigned.
        /// </summary>
        public int GetPlayerSlot(ulong clientId)
        {
            if (Player1ClientId.Value == clientId) return 1;
            if (Player2ClientId.Value == clientId) return 2;
            return 0;
        }

        /// <summary>
        /// Checks if all registered players are marked as ready.
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (Players.Count == 0) return false;

            for (int i = 0; i < Players.Count; i++)
            {
                if (!Players[i].IsReady) return false;
            }

            return true;
        }

        // ── Callbacks ───────────────────────────────────────────────
        private void HandleMatchStateChanged(MatchState previous, MatchState current)
        {
            Debug.Log($"[GameState] MatchState: {previous} -> {current}");
            OnMatchStateChanged?.Invoke(previous, current);
        }

        private void HandleScoreChanged(byte previous, byte current)
        {
            OnScoreChanged?.Invoke(Player1Score.Value, Player2Score.Value);
        }
    }
}
