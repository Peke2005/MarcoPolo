using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Server-authoritative manager that spawns player objects when clients connect
    /// and handles respawning between rounds.
    /// Requires a NetworkGameState on the same object or discoverable in scene.
    /// </summary>
    public class PlayerSpawnManager : NetworkBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private MapDefinition _mapDefinition;

        private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new Dictionary<ulong, NetworkObject>();
        private readonly List<ulong> _joinOrder = new List<ulong>();
        private NetworkGameState _gameState;

        public GameObject PlayerPrefab => _playerPrefab;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            _gameState = GetComponent<NetworkGameState>();
            if (_gameState == null)
            {
                _gameState = FindFirstObjectByType<NetworkGameState>();
            }

            if (_playerPrefab == null)
            {
                Debug.LogError("[Spawn] Player prefab is not assigned.");
                return;
            }

            if (_mapDefinition == null)
            {
                Debug.LogError("[Spawn] MapDefinition is not assigned.");
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            Debug.Log("[Spawn] PlayerSpawnManager ready (server).");

            // Scene just loaded with clients already connected from the lobby —
            // the connect callback won't fire for them, so spawn them now.
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                HandleClientConnected(clientId);
            }

        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        // ── Spawning ────────────────────────────────────────────────
        private void HandleClientConnected(ulong clientId)
        {
            if (!_joinOrder.Contains(clientId))
            {
                _joinOrder.Add(clientId);
            }

            SpawnPlayerForClient(clientId);

            // When both players are in, assign slots in game state
            if (_joinOrder.Count == 2 && _gameState != null)
            {
                _gameState.AssignPlayerSlots(_joinOrder[0], _joinOrder[1]);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject playerObj))
            {
                if (playerObj != null && playerObj.IsSpawned)
                {
                    playerObj.Despawn(true);
                }

                _spawnedPlayers.Remove(clientId);
            }

            _joinOrder.Remove(clientId);
            Debug.Log($"[Spawn] Cleaned up player object for client {clientId}.");
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (_spawnedPlayers.ContainsKey(clientId)) return;

            // Determine spawn point based on join order
            int slotIndex = _joinOrder.IndexOf(clientId);
            Vector2 spawnPos;
            PlayerFaction faction;

            if (slotIndex == 0)
            {
                spawnPos = _mapDefinition.spawnPointA;
                faction = PlayerFaction.Blue;
            }
            else
            {
                spawnPos = _mapDefinition.spawnPointB;
                faction = PlayerFaction.Red;
            }

            Vector3 worldPos = new Vector3(spawnPos.x, spawnPos.y, 0f);
            GameObject playerGO = Instantiate(_playerPrefab, worldPos, Quaternion.identity);

            NetworkObject netObj = playerGO.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[Spawn] Player prefab is missing NetworkObject component.");
                Destroy(playerGO);
                return;
            }

            // Keep player ownership under Netcode control across scene transitions.
            // If destroyWithScene is true, a non-host client can destroy its local
            // Player(Clone) during a scene unload and NGO reports Invalid Destroy.
            netObj.SpawnAsPlayerObject(clientId, false);

            _spawnedPlayers[clientId] = netObj;

            Debug.Log($"[Spawn] Spawned player for client {clientId} at {worldPos} | Faction: {faction}");
        }

        // ── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Resets all spawned players to their designated spawn positions.
        /// Call at the start of a new round.
        /// </summary>
        public void RespawnPlayers()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[Spawn] Only server can respawn players.");
                return;
            }

            foreach (var kvp in _spawnedPlayers)
            {
                ulong clientId = kvp.Key;
                NetworkObject netObj = kvp.Value;

                if (netObj == null || !netObj.IsSpawned) continue;

                int slotIndex = _joinOrder.IndexOf(clientId);
                Vector2 spawnPos = slotIndex == 0
                    ? _mapDefinition.spawnPointA
                    : _mapDefinition.spawnPointB;

                netObj.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

                // Notify clients of the teleport
                RespawnPlayerClientRpc(clientId, spawnPos);
            }

            Debug.Log("[Spawn] All players respawned to start positions.");
        }

        /// <summary>
        /// Returns the NetworkObject for a given client, or null if not spawned.
        /// </summary>
        public NetworkObject GetPlayerObject(ulong clientId)
        {
            _spawnedPlayers.TryGetValue(clientId, out NetworkObject obj);
            return obj;
        }

        /// <summary>
        /// Returns the faction assigned to a client based on join order.
        /// </summary>
        public PlayerFaction GetFaction(ulong clientId)
        {
            int slot = _joinOrder.IndexOf(clientId);
            return slot == 0 ? PlayerFaction.Blue : PlayerFaction.Red;
        }

        // ── Client RPCs ─────────────────────────────────────────────

        [ClientRpc]
        private void RespawnPlayerClientRpc(ulong clientId, Vector2 position)
        {
            // Each client snaps the relevant player object to the respawn position.
            // This ensures client-side position is correct even if NetworkTransform
            // hasn't replicated yet.
            if (NetworkManager.Singleton.LocalClientId != clientId) return;

            if (NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() is NetworkObject localPlayer)
            {
                localPlayer.transform.position = new Vector3(position.x, position.y, 0f);
            }
        }
    }
}
