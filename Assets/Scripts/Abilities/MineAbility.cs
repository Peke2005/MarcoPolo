using Unity.Netcode;
using UnityEngine;
using FrentePartido.Player;

namespace FrentePartido.Abilities
{
    /// <summary>
    /// Server-authoritative proximity mine. Places a mine NetworkObject on the ground.
    /// Mine triggers on enemy proximity, deals damage, and despawns.
    /// Max 1 active mine per player -- placing a new mine destroys the old one.
    /// </summary>
    public class MineAbility : NetworkBehaviour
    {
        [Header("Mine Settings")]
        [SerializeField] private GameObject minePrefab;
        [SerializeField] private Color mineColor = new Color(1f, 0.3f, 0.1f, 0.8f);

        private NetworkObject _activeMineNetObj;
        private ulong _activeMineNetId;

        // ── Server Execution ────────────────────────────────────────

        /// <summary>
        /// Place a mine at position. Called by AbilityController on server.
        /// </summary>
        /// <param name="position">World position to place mine.</param>
        /// <param name="damage">Explosion damage (AbilityDefinition.value1 cast to int).</param>
        /// <param name="detectionRadius">Trigger radius (AbilityDefinition.value2).</param>
        [ServerRpc]
        public void PlaceMineServerRpc(Vector2 position, int damage, float detectionRadius, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // Destroy existing mine if any
            DestroyActiveMine();

            // Spawn mine
            GameObject mineObj;

            if (minePrefab != null)
            {
                mineObj = Instantiate(minePrefab, position, Quaternion.identity);
            }
            else
            {
                // Create runtime mine if no prefab
                mineObj = CreateRuntimeMine(position, detectionRadius);
            }

            // Ensure NetworkObject exists
            NetworkObject netObj = mineObj.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                netObj = mineObj.AddComponent<NetworkObject>();
            }

            // Configure mine logic
            ProximityMine mine = mineObj.GetComponent<ProximityMine>();
            if (mine == null)
                mine = mineObj.AddComponent<ProximityMine>();

            mine.Initialize(OwnerClientId, damage, detectionRadius);

            // Spawn on network
            netObj.Spawn(true);
            _activeMineNetObj = netObj;
            _activeMineNetId = netObj.NetworkObjectId;

            // Notify clients
            ShowMinePlacedClientRpc(position);

            Debug.Log($"[MineAbility] Mine placed at {position} by player {OwnerClientId}. Damage: {damage}, Radius: {detectionRadius}");
        }

        private GameObject CreateRuntimeMine(Vector2 position, float detectionRadius)
        {
            GameObject mineObj = new GameObject($"Mine_Player{OwnerClientId}");
            mineObj.transform.position = position;

            // Visual sprite
            SpriteRenderer sr = mineObj.AddComponent<SpriteRenderer>();
            sr.color = mineColor;
            sr.sortingOrder = -1;
            // Scale to represent mine size
            mineObj.transform.localScale = Vector3.one * 0.4f;

            // Trigger collider for detection
            CircleCollider2D trigger = mineObj.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = detectionRadius / 0.4f; // Compensate for localScale

            // Physics body for trigger detection
            Rigidbody2D rb = mineObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            return mineObj;
        }

        private void DestroyActiveMine()
        {
            if (_activeMineNetObj != null && _activeMineNetObj.IsSpawned)
            {
                _activeMineNetObj.Despawn(true);
                Debug.Log("[MineAbility] Previous mine destroyed.");
            }

            _activeMineNetObj = null;
            _activeMineNetId = 0;
        }

        /// <summary>
        /// Called by ProximityMine when it explodes, to clear our reference.
        /// </summary>
        public void NotifyMineExploded(ulong mineNetId)
        {
            if (mineNetId == _activeMineNetId)
            {
                _activeMineNetObj = null;
                _activeMineNetId = 0;
            }
        }

        // ── Client Notifications ────────────────────────────────────

        [ClientRpc]
        private void ShowMinePlacedClientRpc(Vector2 position)
        {
            Debug.Log($"[MineAbility] Mine placed visual at {position}");
            // Actual visual is on the NetworkObject itself, synced via spawn.
            // Additional particle/audio feedback can be added here.
        }

        [ClientRpc]
        public void ShowMineExplodedClientRpc(Vector2 position)
        {
            Debug.Log($"[MineAbility] Mine exploded at {position}");
            // Spawn explosion VFX / play audio here.
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                DestroyActiveMine();

            base.OnNetworkDespawn();
        }
    }

    /// <summary>
    /// Attached to the mine NetworkObject. Detects enemy proximity and explodes.
    /// Server authoritative -- only server processes triggers.
    /// </summary>
    public class ProximityMine : NetworkBehaviour
    {
        private ulong _ownerClientId;
        private int _damage;
        private float _detectionRadius;
        private bool _exploded;
        private MineAbility _ownerAbility;

        [Header("Mine Timing")]
        [SerializeField] private float armDelay = 0.5f;

        private float _spawnTime;

        public void Initialize(ulong ownerClientId, int damage, float detectionRadius)
        {
            _ownerClientId = ownerClientId;
            _damage = damage;
            _detectionRadius = detectionRadius;
            _exploded = false;
            _spawnTime = Time.time;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _spawnTime = Time.time;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsServer) return;
            if (_exploded) return;

            // Don't trigger during arm delay
            if (Time.time - _spawnTime < armDelay) return;

            // Check if it's an enemy player
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null) return;

            // Don't trigger on owner
            if (netObj.OwnerClientId == _ownerClientId) return;

            // Check if it has health (is a player)
            PlayerHealth targetHealth = netObj.GetComponent<PlayerHealth>();
            if (targetHealth == null) return;
            if (targetHealth.IsDead) return;

            Explode(targetHealth, netObj.OwnerClientId);
        }

        private void Explode(PlayerHealth targetHealth, ulong targetClientId)
        {
            _exploded = true;

            // Deal damage
            targetHealth.TakeDamageServerRpc(_damage, _ownerClientId);

            Debug.Log($"[ProximityMine] Exploded! Dealt {_damage} damage to player {targetClientId}");

            // Notify owner ability
            MineAbility ownerAbility = FindOwnerAbility();
            if (ownerAbility != null)
            {
                ownerAbility.NotifyMineExploded(NetworkObjectId);
                ownerAbility.ShowMineExplodedClientRpc((Vector2)transform.position);
            }

            // Despawn mine
            if (IsSpawned)
                NetworkObject.Despawn(true);
        }

        private MineAbility FindOwnerAbility()
        {
            if (_ownerAbility != null) return _ownerAbility;

            // Find owner player's MineAbility
            foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == _ownerClientId && client.PlayerObject != null)
                {
                    _ownerAbility = client.PlayerObject.GetComponent<MineAbility>();
                    return _ownerAbility;
                }
            }

            return null;
        }
    }
}
