using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Player;

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
            if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject cached) && cached != null && cached.IsSpawned)
                return;

            _spawnedPlayers.Remove(clientId);

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
            NetworkObject existing = FindExistingPlayerForClient(clientId);
            if (existing != null)
            {
                MoveNetworkPlayer(existing, worldPos);
                _spawnedPlayers[clientId] = existing;
                RespawnPlayerClientRpc(clientId, spawnPos);
                Debug.Log($"[Spawn] Reused existing player for client {clientId} at {worldPos} | Faction: {faction}");
                return;
            }

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

            // Owner-authoritative NetworkTransform: tell the new owner explicitly to
            // snap to its spawn point. Without this, the client's own NT writes
            // overwrite the server's initial position with the prefab's local (0,0)
            // and the player ends up in the middle of the arena on round 1.
            RespawnPlayerClientRpc(clientId, spawnPos);

            Debug.Log($"[Spawn] Spawned player for client {clientId} at {worldPos} | Faction: {faction}");
        }

        private NetworkObject FindExistingPlayerForClient(ulong clientId)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
                return null;

            NetworkObject chosen = null;
            List<NetworkObject> duplicates = new List<NetworkObject>();
            foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                NetworkObject obj = kv.Value;
                if (obj == null || !obj.IsSpawned) continue;
                if (obj.OwnerClientId != clientId) continue;
                if (obj.GetComponent<PlayerHealth>() == null && obj.GetComponentInChildren<PlayerHealth>() == null)
                    continue;

                if (chosen == null)
                {
                    chosen = obj;
                    continue;
                }

                duplicates.Add(obj);
            }

            for (int i = 0; i < duplicates.Count; i++)
            {
                NetworkObject duplicate = duplicates[i];
                if (duplicate != null && duplicate.IsSpawned)
                {
                    Debug.LogWarning($"[Spawn] Duplicate player for client {clientId} removed: {duplicate.NetworkObjectId}");
                    duplicate.Despawn(true);
                }
            }

            return chosen;
        }

        private static void MoveNetworkPlayer(NetworkObject netObj, Vector3 worldPos)
        {
            netObj.transform.position = worldPos;
            Rigidbody2D rb = netObj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = worldPos;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
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

                MoveNetworkPlayer(netObj, new Vector3(spawnPos.x, spawnPos.y, 0f));

                // Notify clients of the teleport
                RespawnPlayerClientRpc(clientId, spawnPos);
            }

            Debug.Log("[Spawn] All players respawned to start positions.");
        }

        /// <summary>
        /// Respawn a single player (used when killed mid-deathmatch).
        /// Resets HP, state and teleports to the player's slot spawn point.
        /// </summary>
        public void RespawnSinglePlayer(ulong clientId)
        {
            if (!IsServer) return;
            if (!_spawnedPlayers.TryGetValue(clientId, out NetworkObject netObj)) return;
            if (netObj == null || !netObj.IsSpawned) return;

            int slotIndex = _joinOrder.IndexOf(clientId);
            Vector2 spawnPos = slotIndex == 0
                ? _mapDefinition.spawnPointA
                : _mapDefinition.spawnPointB;

            MoveNetworkPlayer(netObj, new Vector3(spawnPos.x, spawnPos.y, 0f));

            var health = netObj.GetComponent<PlayerHealth>();
            if (health != null) health.ResetHealth();

            var state = netObj.GetComponent<PlayerStateController>();
            if (state != null) state.ForceState(PlayerState.Idle);

            var weapon = netObj.GetComponent<FrentePartido.Combat.WeaponController>();
            if (weapon != null) weapon.ResetWeapon();

            RespawnPlayerClientRpc(clientId, spawnPos);
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
            StartCoroutine(SnapPlayerWhenSpawned(clientId, position));
        }

        private IEnumerator SnapPlayerWhenSpawned(ulong clientId, Vector2 position)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) yield break;

            bool found = false;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                foreach (var kv in nm.SpawnManager.SpawnedObjects)
                {
                    NetworkObject obj = kv.Value;
                    if (obj == null || !obj.IsPlayerObject) continue;
                    if (obj.OwnerClientId != clientId) continue;

                    // Restore visuals and snap on every client. The first ClientRpc
                    // can arrive before the spawned player exists locally, so this
                    // coroutine retries during scene load.
                    var presentation = obj.GetComponent<PlayerPresentation>();
                    if (presentation != null) presentation.ResetVisuals();

                    MoveNetworkPlayer(obj, new Vector3(position.x, position.y, 0f));
                    found = true;
                    break;
                }

                if (found)
                {
                    // Keep snapping briefly. Owner-authoritative NetworkTransform can
                    // push the old prefab position (0,0) for a few frames after spawn.
                    yield return new WaitForSeconds(0.05f);
                    continue;
                }

                yield return new WaitForSeconds(0.05f);
            }

            if (!found)
                Debug.LogWarning($"[Spawn] Could not snap spawned player {clientId} after scene load.");
        }
    }
}
