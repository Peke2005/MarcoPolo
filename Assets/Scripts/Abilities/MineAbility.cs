using Unity.Netcode;
using UnityEngine;
using FrentePartido.Player;

namespace FrentePartido.Abilities
{
    /// <summary>
    /// Server-authoritative proximity mine. Trigger exists on host/server;
    /// clients get a synced visual via ClientRpc.
    /// Max 1 active mine per player -- placing a new mine destroys the old one.
    /// </summary>
    public class MineAbility : NetworkBehaviour
    {
        [Header("Mine Settings")]
        [SerializeField] private Color mineColor = new Color(1f, 0.3f, 0.1f, 0.9f);

        private GameObject _activeMineObj;
        private ulong _activeMineId;
        private GameObject _activeMineVisual;
        private ulong _activeMineVisualId;
        private static ulong _nextMineId = 1;

        // ── Server Execution ────────────────────────────────────────

        /// <summary>
        /// Place a mine at position. Called by AbilityController on server.
        /// </summary>
        /// <param name="position">World position to place mine.</param>
        /// <param name="damage">Explosion damage (AbilityDefinition.value1 cast to int).</param>
        /// <param name="detectionRadius">Trigger radius (AbilityDefinition.value2).</param>
        [ServerRpc(RequireOwnership = false)]
        public void PlaceMineServerRpc(Vector2 position, int damage, float detectionRadius, ServerRpcParams rpcParams = default)
        {
            PlaceMineServer(position, damage, detectionRadius);
        }

        public void PlaceMineServer(Vector2 position, int damage, float detectionRadius)
        {
            if (!IsServer) return;

            // Destroy existing mine if any
            DestroyActiveMine();

            GameObject mineObj = CreateRuntimeMine(position, detectionRadius);
            ulong mineId = _nextMineId++;

            // Configure mine logic
            ProximityMine mine = mineObj.GetComponent<ProximityMine>();
            if (mine == null)
                mine = mineObj.AddComponent<ProximityMine>();

            mine.Initialize(OwnerClientId, mineId, damage, detectionRadius);
            _activeMineObj = mineObj;
            _activeMineId = mineId;

            // Notify clients
            ShowMinePlacedClientRpc(position, mineId, detectionRadius);

            Debug.Log($"[MineAbility] Mine placed at {position} by player {OwnerClientId}. Damage: {damage}, Radius: {detectionRadius}");
        }

        private GameObject CreateRuntimeMine(Vector2 position, float detectionRadius)
        {
            GameObject mineObj = new GameObject($"Mine_Player{OwnerClientId}");
            mineObj.transform.position = position;
            mineObj.transform.localScale = Vector3.one;

            // Visual sprite — visible orange disc with darker center so it reads as a hazard.
            SpriteRenderer sr = mineObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetMineSprite();
            sr.color = mineColor;
            sr.sortingOrder = 4;

            // Trigger collider for detection
            CircleCollider2D trigger = mineObj.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = detectionRadius;

            // Kinematic body so OnTriggerEnter2D fires.
            Rigidbody2D rb = mineObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            return mineObj;
        }

        private static Sprite _mineSprite;
        private static Sprite GetMineSprite()
        {
            if (_mineSprite != null) return _mineSprite;
            int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = size * 0.42f;
            float inner = size * 0.18f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                Color col = new Color(0, 0, 0, 0);
                if (d <= outer)
                {
                    float t = 1f - d / outer;
                    col = new Color(1f, 0.45f + 0.2f * t, 0.18f * t, 1f);
                    if (d < inner) col = new Color(0.85f, 0.10f, 0.08f, 1f);
                    if (d > outer - 2f) col = new Color(0.10f, 0.06f, 0.04f, 1f); // border
                }
                px[y * size + x] = col;
            }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            _mineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
            return _mineSprite;
        }

        private void DestroyActiveMine()
        {
            ulong oldId = _activeMineId;

            if (_activeMineObj != null)
            {
                Destroy(_activeMineObj);
                Debug.Log("[MineAbility] Previous mine destroyed.");
            }

            _activeMineObj = null;
            _activeMineId = 0;

            if (IsServer && oldId != 0)
                HideMineClientRpc(oldId);
        }

        /// <summary>
        /// Called by ProximityMine when it explodes, to clear our reference.
        /// </summary>
        public void NotifyMineExploded(ulong mineNetId)
        {
            if (mineNetId == _activeMineId)
            {
                _activeMineObj = null;
                _activeMineId = 0;
                HideMineClientRpc(mineNetId);
            }
        }

        // ── Client Notifications ────────────────────────────────────

        [ClientRpc]
        private void ShowMinePlacedClientRpc(Vector2 position, ulong mineId, float detectionRadius)
        {
            Debug.Log($"[MineAbility] Mine placed visual at {position}");
            if (IsServer) return;
            DestroyMineVisual();

            _activeMineVisual = new GameObject($"MineVisual_{OwnerClientId}");
            _activeMineVisual.transform.position = position;
            var sr = _activeMineVisual.AddComponent<SpriteRenderer>();
            sr.sprite = GetMineSprite();
            sr.color = mineColor;
            sr.sortingOrder = 4;
            _activeMineVisualId = mineId;
        }

        [ClientRpc]
        private void HideMineClientRpc(ulong mineId)
        {
            if (_activeMineVisualId != mineId) return;
            DestroyMineVisual();
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
            DestroyMineVisual();

            base.OnNetworkDespawn();
        }

        private void DestroyMineVisual()
        {
            if (_activeMineVisual != null)
                Destroy(_activeMineVisual);
            _activeMineVisual = null;
            _activeMineVisualId = 0;
        }
    }

    /// <summary>
    /// Server-only trigger. Detects enemy proximity and explodes.
    /// </summary>
    public class ProximityMine : MonoBehaviour
    {
        private ulong _ownerClientId;
        private ulong _mineId;
        private int _damage;
        private float _detectionRadius;
        private bool _exploded;
        private MineAbility _ownerAbility;

        [Header("Mine Timing")]
        [SerializeField] private float armDelay = 0.5f;

        private float _spawnTime;

        public void Initialize(ulong ownerClientId, ulong mineId, int damage, float detectionRadius)
        {
            _ownerClientId = ownerClientId;
            _mineId = mineId;
            _damage = damage;
            _detectionRadius = detectionRadius;
            _exploded = false;
            _spawnTime = Time.time;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
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
            targetHealth.ApplyDamageServer(_damage, _ownerClientId);

            Debug.Log($"[ProximityMine] Exploded! Dealt {_damage} damage to player {targetClientId}");

            // Notify owner ability
            MineAbility ownerAbility = FindOwnerAbility();
            if (ownerAbility != null)
            {
                ownerAbility.NotifyMineExploded(_mineId);
                ownerAbility.ShowMineExplodedClientRpc((Vector2)transform.position);
            }

            Destroy(gameObject);
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
