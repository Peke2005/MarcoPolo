using Unity.Netcode;
using UnityEngine;
using FrentePartido.Player;
using FrentePartido.Core;

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
        [SerializeField] private float visualScale = 0.46f;

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

            // No renderer here: armed mines are invisible until triggered.
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
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = size * 0.40f;
            float mid = size * 0.30f;
            float core = size * 0.15f;
            float lineHalfWidth = size * 0.022f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float d = Vector2.Distance(p, c);
                Color col = new Color(0, 0, 0, 0);
                if (d <= outer)
                {
                    float t = 1f - d / outer;
                    col = new Color(0.20f + 0.20f * t, 0.12f + 0.08f * t, 0.06f, 1f);
                    if (d < mid) col = new Color(0.95f, 0.34f + 0.20f * t, 0.08f, 1f);
                    if (d < core) col = new Color(1.0f, 0.78f, 0.14f, 1f);

                    bool crossX = Mathf.Abs(p.x - c.x) < lineHalfWidth && d < mid;
                    bool crossY = Mathf.Abs(p.y - c.y) < lineHalfWidth && d < mid;
                    bool ring = d > mid - 3f && d < mid + 2f;
                    bool outerRing = d > outer - 4f;
                    if (crossX || crossY || ring || outerRing)
                        col = new Color(0.08f, 0.04f, 0.02f, 1f);
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

        private static Sprite _mineGlowSprite;
        private static Sprite GetMineGlowSprite()
        {
            if (_mineGlowSprite != null) return _mineGlowSprite;
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = size * 0.48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = d <= outer ? Mathf.Pow(1f - d / outer, 2.1f) * 0.55f : 0f;
                px[y * size + x] = new Color(1f, 0.38f, 0.05f, a);
            }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            _mineGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
            return _mineGlowSprite;
        }

        private static void CreateMineVisual(Transform parent, Color tint, float scale, bool addPulse)
        {
            GameObject holder = new GameObject("MineVisualRoot");
            holder.transform.SetParent(parent, false);

            GameObject glow = new GameObject("MineGlow");
            glow.transform.SetParent(holder.transform, false);
            glow.transform.localScale = Vector3.one * (scale * 1.45f);
            var glowSr = glow.AddComponent<SpriteRenderer>();
            glowSr.sprite = GetMineGlowSprite();
            glowSr.color = new Color(tint.r, tint.g, tint.b, 0.85f);
            glowSr.sortingOrder = 3;

            GameObject core = new GameObject("MineCore");
            core.transform.SetParent(holder.transform, false);
            core.transform.localScale = Vector3.one * scale;
            var sr = core.AddComponent<SpriteRenderer>();
            sr.sprite = GetMineSprite();
            sr.color = tint;
            sr.sortingOrder = 4;

            if (addPulse)
                holder.AddComponent<MineVisualPulse>().Initialize();
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
            Debug.Log($"[MineAbility] Invisible mine armed at {position}");
            DestroyMineVisual();
            _activeMineVisualId = mineId;
        }

        [ClientRpc]
        private void HideMineClientRpc(ulong mineId)
        {
            if (_activeMineVisualId != mineId) return;
            DestroyMineVisual();
        }

        [ClientRpc]
        public void ShowMineExplodedClientRpc(Vector2 position, ulong targetClientId)
        {
            Debug.Log($"[MineAbility] Mine exploded at {position}");
            FxManager.SpawnExplosionRing(position, 1.35f);
            FxManager.PlayExplosion(position, 0.45f);
            bool localVictim = NetworkManager.Singleton != null && targetClientId == NetworkManager.Singleton.LocalClientId;
            SpawnMineTriggeredFx(position, localVictim);
        }

        private static void SpawnMineTriggeredFx(Vector2 position, bool localVictim)
        {
            var fx = new GameObject("FX_MineTriggered");
            fx.transform.position = position;

            CreateMineVisual(fx.transform, new Color(1f, 0.34f, 0.06f, 1f), 0.62f, false);

            var label = new GameObject("MineTriggerLabel");
            label.transform.SetParent(fx.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.text = localVictim ? "TE COMISTE UNA MINA" : "MINA ACTIVADA";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.13f;
            text.fontSize = 72;
            text.color = localVictim ? new Color(1f, 0.92f, 0.35f, 1f) : new Color(1f, 0.64f, 0.20f, 1f);
            var mr = text.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 45;

            fx.AddComponent<MineTriggeredFx>().Initialize(localVictim);
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
                ownerAbility.ShowMineExplodedClientRpc((Vector2)transform.position, targetClientId);
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

    public class MineVisualPulse : MonoBehaviour
    {
        private float _phase;

        public void Initialize()
        {
            _phase = Random.value * 10f;
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * 5.5f + _phase) * 0.055f;
            transform.localScale = Vector3.one * pulse;
        }
    }

    public class MineTriggeredFx : MonoBehaviour
    {
        private SpriteRenderer[] _renderers;
        private TextMesh _label;
        private Color[] _baseColors;
        private float _time;
        private bool _localVictim;

        public void Initialize(bool localVictim)
        {
            _localVictim = localVictim;
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _label = GetComponentInChildren<TextMesh>(true);
            _baseColors = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i].color;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            const float life = 0.9f;
            float k = Mathf.Clamp01(_time / life);
            float pop = Mathf.Sin(k * Mathf.PI);
            float scale = Mathf.Lerp(0.45f, _localVictim ? 1.25f : 1.05f, Mathf.Sqrt(k));
            transform.localScale = Vector3.one * (scale + pop * 0.08f);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Color c = _baseColors[i];
                _renderers[i].color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            }

            if (_label != null)
            {
                Color c = _label.color;
                _label.transform.localPosition = new Vector3(0f, 0.82f + k * 0.42f, 0f);
                _label.color = new Color(c.r, c.g, c.b, 1f - k);
            }

            if (_time >= life)
                Destroy(gameObject);
        }
    }
}
