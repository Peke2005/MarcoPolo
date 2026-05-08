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
        private static Sprite _solidSprite;

        public GameObject PlayerPrefab => _playerPrefab;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            RuntimeMatchSettings.ApplyMode(NetworkSessionManager.Instance != null
                ? NetworkSessionManager.Instance.SelectedGameMode
                : GameMode.Rounds1v1);
            if (IsDeathmatchMode())
                BuildDeathmatchArenaClientRpc();

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

            if (IsDeathmatchMode())
            {
                spawnPos = GetDeathmatchSpawn(slotIndex);
                faction = slotIndex % 2 == 0 ? PlayerFaction.Blue : PlayerFaction.Red;
            }
            else if (slotIndex == 0)
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
                Vector2 spawnPos = IsDeathmatchMode()
                    ? GetDeathmatchSpawn(slotIndex)
                    : (slotIndex == 0 ? _mapDefinition.spawnPointA : _mapDefinition.spawnPointB);

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
            Vector2 spawnPos = IsDeathmatchMode()
                ? GetRandomDeathmatchSpawn(clientId)
                : (slotIndex == 0 ? _mapDefinition.spawnPointA : _mapDefinition.spawnPointB);

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

        private bool IsDeathmatchMode()
        {
            return NetworkSessionManager.Instance != null &&
                   NetworkSessionManager.Instance.SelectedGameMode == GameMode.Deathmatch;
        }

        // Static visualization helper used by the arena builder ClientRpc to show all
        // 10 spawn markers regardless of current player count.
        private static Vector2 GetDeathmatchSpawnVisual(int slotIndex)
        {
            int slot = Mathf.Clamp(slotIndex, 0, 9);
            float angle = (Mathf.PI * 2f * slot) / 10f;
            return new Vector2(Mathf.Cos(angle) * 16.5f, Mathf.Sin(angle) * 9.0f);
        }

        // Random spawn for DM respawns. Picks from the curated DM points and prefers
        // the one furthest from every other living player so respawning never drops
        // you right next to a killer.
        private Vector2 GetRandomDeathmatchSpawn(ulong selfClientId)
        {
            Vector2[] options = (_mapDefinition != null && _mapDefinition.deathmatchSpawnPoints != null && _mapDefinition.deathmatchSpawnPoints.Length > 0)
                ? _mapDefinition.deathmatchSpawnPoints
                : new[] { GetDeathmatchSpawnVisual(0), GetDeathmatchSpawnVisual(2), GetDeathmatchSpawnVisual(5), GetDeathmatchSpawnVisual(7) };

            // Collect living opponent positions on the server.
            var others = new List<Vector2>();
            foreach (var kv in _spawnedPlayers)
            {
                if (kv.Key == selfClientId) continue;
                if (kv.Value == null || !kv.Value.IsSpawned) continue;
                var hp = kv.Value.GetComponent<PlayerHealth>();
                if (hp != null && hp.IsDead) continue;
                others.Add((Vector2)kv.Value.transform.position);
            }

            // Score each candidate by min distance to any opponent. Pick best two and
            // randomize between them so spawns aren't deterministic.
            int bestA = 0, bestB = 0;
            float bestADist = -1f, bestBDist = -1f;
            for (int i = 0; i < options.Length; i++)
            {
                float d = float.PositiveInfinity;
                foreach (var o in others)
                {
                    float dd = Vector2.Distance(options[i], o);
                    if (dd < d) d = dd;
                }
                if (d > bestADist) { bestBDist = bestADist; bestB = bestA; bestADist = d; bestA = i; }
                else if (d > bestBDist) { bestBDist = d; bestB = i; }
            }

            int pick = (UnityEngine.Random.value < 0.6f || bestADist == bestBDist) ? bestA : bestB;
            return options[pick];
        }

        private Vector2 GetDeathmatchSpawn(int slotIndex)
        {
            // Use the curated DeathmatchSpawnPoints from the map when present, picking
            // the entries that spread the actual player count apart instead of always
            // dividing by 10 (which put 2 players almost on top of each other).
            int total = Mathf.Max(2, _joinOrder.Count);
            int slot = Mathf.Clamp(slotIndex, 0, total - 1);

            if (_mapDefinition != null && _mapDefinition.deathmatchSpawnPoints != null && _mapDefinition.deathmatchSpawnPoints.Length >= 2)
            {
                var pts = _mapDefinition.deathmatchSpawnPoints;
                int len = pts.Length;
                int picked = (slot * len) / total; // even stride across the array
                return pts[Mathf.Clamp(picked, 0, len - 1)];
            }

            // Procedural fallback: ring scaled by actual player count, larger radius
            // for the bigger deathmatch arena.
            float angle = (Mathf.PI * 2f * slot) / total;
            float radius = total <= 2 ? 16.5f : Mathf.Lerp(16.5f, 18f, (total - 2) / 8f);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * 9.0f);
        }

        [ClientRpc]
        private void BuildDeathmatchArenaClientRpc()
        {
            RuntimeMatchSettings.ApplyMode(GameMode.Deathmatch);
            BuildDeathmatchArena();
        }

        private static void BuildDeathmatchArena()
        {
            DestroyIfExists("~ArenaDecor");
            DestroyIfExists("~ArenaAccents");
            DestroyIfExists("~DeathmatchArena");
            DestroyDeathmatchBlockingScenePieces();

            var root = new GameObject("~DeathmatchArena");

            // Layered floor: dark grass base + lighter inset for depth.
            AddPiece(root, "Floor", Vector2.zero, new Vector2(44f, 26f), new Color(0.16f, 0.30f, 0.18f, 1f), false, -100);
            AddPiece(root, "Floor_Inset", Vector2.zero, new Vector2(42f, 24f), new Color(0.24f, 0.42f, 0.26f, 1f), false, -99);
            // Subtle radial vignette in the corners using soft tinted overlays.
            AddPiece(root, "Tint_TL", new Vector2(-15f,  9f), new Vector2(14f, 10f), new Color(0.35f, 0.55f, 0.32f, 0.18f), false, -97);
            AddPiece(root, "Tint_TR", new Vector2( 15f,  9f), new Vector2(14f, 10f), new Color(0.35f, 0.55f, 0.32f, 0.18f), false, -97);
            AddPiece(root, "Tint_BL", new Vector2(-15f, -9f), new Vector2(14f, 10f), new Color(0.35f, 0.55f, 0.32f, 0.18f), false, -97);
            AddPiece(root, "Tint_BR", new Vector2( 15f, -9f), new Vector2(14f, 10f), new Color(0.35f, 0.55f, 0.32f, 0.18f), false, -97);
            // Faint grid lines for spatial reference.
            for (int x = -18; x <= 18; x += 6)
                AddPiece(root, "Grid_V_" + x, new Vector2(x, 0f), new Vector2(0.06f, 24f), new Color(0f, 0f, 0f, 0.12f), false, -96);
            for (int y = -9; y <= 9; y += 6)
                AddPiece(root, "Grid_H_" + y, new Vector2(0f, y), new Vector2(42f, 0.06f), new Color(0f, 0f, 0f, 0.12f), false, -96);

            // Outer brick walls.
            Color wallTone = new Color(0.55f, 0.34f, 0.16f, 1f);
            AddPiece(root, "Wall_Top",    new Vector2(0f,  13.3f), new Vector2(44f, 0.7f), wallTone, true, 5);
            AddPiece(root, "Wall_Bottom", new Vector2(0f, -13.3f), new Vector2(44f, 0.7f), wallTone, true, 5);
            AddPiece(root, "Wall_Left",   new Vector2(-22.3f, 0f), new Vector2(0.7f, 26f), wallTone, true, 5);
            AddPiece(root, "Wall_Right",  new Vector2( 22.3f, 0f), new Vector2(0.7f, 26f), wallTone, true, 5);

            // Central plaza: no walls, fully open from every direction. Just a
            // tinted floor and a soft glow so the area reads as a focal point.
            AddPiece(root, "Center_Floor", Vector2.zero, new Vector2(7f, 5.6f), new Color(0.18f, 0.30f, 0.20f, 1f), false, -95);
            AddPiece(root, "Center_Glow",  Vector2.zero, new Vector2(9.0f, 7.5f), new Color(1f, 0.78f, 0.18f, 0.10f), false, -94);
            // Marker dot in the middle so the plaza is recognizable on the minimap.
            AddPiece(root, "Center_Marker", Vector2.zero, new Vector2(0.9f, 0.9f), new Color(1f, 0.86f, 0.30f, 0.85f), false, -90);

            // 10 spawn marker discs around the play area.
            for (int i = 0; i < 10; i++)
            {
                Vector2 p = GetDeathmatchSpawnVisual(i);
                AddPiece(root, "DM_Spawn_" + i, p, new Vector2(1.7f, 1.7f),
                    i % 2 == 0 ? new Color(0.2f, 0.55f, 1f, 0.55f) : new Color(1f, 0.3f, 0.25f, 0.55f),
                    false, -50);
            }

            // Cover crates spread around the perimeter, away from spawns and center.
            Color crateTone = new Color(0.50f, 0.27f, 0.12f, 1f);
            (Vector2 pos, Vector2 size)[] cover =
            {
                (new Vector2(-14f,  6f), new Vector2(2.6f, 0.9f)),
                (new Vector2(-14f, -6f), new Vector2(2.6f, 0.9f)),
                (new Vector2( 14f,  6f), new Vector2(2.6f, 0.9f)),
                (new Vector2( 14f, -6f), new Vector2(2.6f, 0.9f)),
                (new Vector2( -8f,  9f), new Vector2(0.9f, 2.4f)),
                (new Vector2(  8f,  9f), new Vector2(0.9f, 2.4f)),
                (new Vector2( -8f, -9f), new Vector2(0.9f, 2.4f)),
                (new Vector2(  8f, -9f), new Vector2(0.9f, 2.4f)),
                (new Vector2(-18f,  3f), new Vector2(0.9f, 2.0f)),
                (new Vector2( 18f, -3f), new Vector2(0.9f, 2.0f)),
            };
            for (int i = 0; i < cover.Length; i++)
            {
                AddPiece(root, "Decor_Crate_DM_" + i, cover[i].pos, cover[i].size, crateTone, true, 8);
                // Drop shadow under each crate for depth.
                AddPiece(root, "CrateShadow_" + i,
                    new Vector2(cover[i].pos.x + 0.12f, cover[i].pos.y - 0.18f),
                    cover[i].size * 1.05f,
                    new Color(0f, 0f, 0f, 0.30f), false, 7);
            }

            Camera cam = Camera.main;
            if (cam != null)
                cam.orthographicSize = 9.5f;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Destroy(go);
        }

        private static void DestroyDeathmatchBlockingScenePieces()
        {
            // The 1v1 scene contains static walls/cover around the middle. In DM
            // those colliders must be removed, otherwise the larger runtime arena
            // looks open but the old invisible/underlaid colliders still block it.
            var all = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (go == null) continue;
                string n = go.name;
                if (n.StartsWith("Wall_") ||
                    n.StartsWith("Cover_") ||
                    n.StartsWith("Decor_Crate") ||
                    n.StartsWith("Decor_Barrel") ||
                    n.StartsWith("Obstacle_"))
                {
                    Destroy(go);
                }
            }
        }

        private static void AddPiece(GameObject root, string name, Vector2 pos, Vector2 size, Color color, bool collider, int sorting)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSolidSprite();
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingOrder = sorting;
            if (collider)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = size;
            }
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _solidSprite;
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
            for (int attempt = 0; attempt < 12; attempt++)
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
                    yield break;
                }

                yield return new WaitForSeconds(0.05f);
            }

            if (!found)
                Debug.LogWarning($"[Spawn] Could not snap spawned player {clientId} after scene load.");
        }
    }
}
