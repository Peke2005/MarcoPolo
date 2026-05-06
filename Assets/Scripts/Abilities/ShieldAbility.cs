using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Player;

namespace FrentePartido.Abilities
{
    /// <summary>
    /// Server-authoritative frontal shield. Spawns a collider in front of the player
    /// that blocks/reduces incoming damage for a set duration.
    /// Shield has limited HP and despawns when HP runs out or duration expires.
    /// </summary>
    public class ShieldAbility : NetworkBehaviour
    {
        [Header("Shield Settings")]
        [SerializeField] private GameObject shieldPrefab;
        [SerializeField] private float shieldOffsetDistance = 0.8f;
        [SerializeField] private int shieldMaxHp = 60;
        [SerializeField] private Color shieldColor = new Color(0.3f, 0.6f, 1f, 0.5f);

        private GameObject _activeShield;
        private int _currentShieldHp;
        private Coroutine _shieldCoroutine;

        // ── Server Execution ────────────────────────────────────────

        /// <summary>
        /// Activate shield. Called by AbilityController on server.
        /// </summary>
        /// <param name="facingDirection">Direction the player is facing.</param>
        /// <param name="duration">How long the shield lasts (seconds).</param>
        /// <param name="damageReduction">Damage reduction percentage from AbilityDefinition.value1.</param>
        [ServerRpc]
        public void ActivateServerRpc(Vector2 facingDirection, float duration, float damageReduction, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // Destroy existing shield if any
            DestroyActiveShield();

            if (facingDirection.sqrMagnitude < 0.01f)
                facingDirection = Vector2.right;

            facingDirection = facingDirection.normalized;

            // Calculate shield position and rotation
            Vector2 shieldPos = (Vector2)transform.position + facingDirection * shieldOffsetDistance;
            float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;

            // Spawn shield object
            _activeShield = SpawnShieldObject(shieldPos, angle);
            _currentShieldHp = shieldMaxHp;

            // Notify clients for visuals
            ShowShieldClientRpc(shieldPos, angle);

            // Start duration timer
            if (_shieldCoroutine != null)
                StopCoroutine(_shieldCoroutine);

            _shieldCoroutine = StartCoroutine(ShieldDurationRoutine(duration));

            Debug.Log($"[ShieldAbility] Shield activated. Duration: {duration}s, HP: {shieldMaxHp}");
        }

        private GameObject SpawnShieldObject(Vector2 position, float angle)
        {
            // Build a bubble around the player: visible blue sphere + circle collider.
            GameObject shield = new GameObject("PlayerShield");
            shield.transform.SetParent(transform, false);
            shield.transform.localPosition = Vector3.zero;
            shield.transform.localRotation = Quaternion.identity;
            shield.transform.localScale = Vector3.one * 1.6f;

            CircleCollider2D col = shield.AddComponent<CircleCollider2D>();
            col.isTrigger = false;
            col.radius = 0.5f; // local — visible bubble of ~0.8u world radius

            SpriteRenderer sr = shield.AddComponent<SpriteRenderer>();
            sr.sprite = GetBubbleSprite();
            sr.color = shieldColor;
            sr.sortingOrder = 12;

            ShieldDamageReceiver receiver = shield.AddComponent<ShieldDamageReceiver>();
            receiver.Initialize(this);

            shield.layer = gameObject.layer;
            return shield;
        }

        private static Sprite _bubbleSprite;
        private static Sprite GetBubbleSprite()
        {
            if (_bubbleSprite != null) return _bubbleSprite;
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = size * 0.48f;
            float inner = size * 0.36f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a;
                if (d > outer) a = 0f;
                else if (d > inner) a = Mathf.Clamp01((outer - d) / (outer - inner)) * 0.95f;
                else a = Mathf.Clamp01(0.18f + (1f - d / inner) * 0.10f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            _bubbleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            return _bubbleSprite;
        }

        private IEnumerator ShieldDurationRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            DestroyActiveShield();
            HideShieldClientRpc();
            Debug.Log("[ShieldAbility] Shield expired.");
        }

        /// <summary>
        /// Called by ShieldDamageReceiver when shield takes a hit.
        /// Returns remaining damage that passes through (0 if fully blocked).
        /// </summary>
        public int AbsorbDamage(int incomingDamage)
        {
            if (_activeShield == null || _currentShieldHp <= 0)
                return incomingDamage;

            int absorbed = Mathf.Min(incomingDamage, _currentShieldHp);
            _currentShieldHp -= absorbed;
            int passthrough = incomingDamage - absorbed;

            Debug.Log($"[ShieldAbility] Absorbed {absorbed} damage. Shield HP: {_currentShieldHp}");

            if (_currentShieldHp <= 0)
            {
                DestroyActiveShield();
                HideShieldClientRpc();
                Debug.Log("[ShieldAbility] Shield destroyed by damage.");
            }

            return passthrough;
        }

        private void DestroyActiveShield()
        {
            if (_shieldCoroutine != null)
            {
                StopCoroutine(_shieldCoroutine);
                _shieldCoroutine = null;
            }

            if (_activeShield != null)
            {
                Destroy(_activeShield);
                _activeShield = null;
            }

            _currentShieldHp = 0;
        }

        public bool IsShieldActive => _activeShield != null && _currentShieldHp > 0;

        // ── Client Visuals ──────────────────────────────────────────

        [ClientRpc]
        private void ShowShieldClientRpc(Vector2 position, float angle)
        {
            // Server already has its shield object; clients create their own bubble visual.
            if (IsServer) return;

            DestroyActiveShield();

            _activeShield = new GameObject("ShieldVisual");
            _activeShield.transform.SetParent(transform, false);
            _activeShield.transform.localPosition = Vector3.zero;
            _activeShield.transform.localRotation = Quaternion.identity;
            _activeShield.transform.localScale = Vector3.one * 1.6f;

            SpriteRenderer sr = _activeShield.AddComponent<SpriteRenderer>();
            sr.sprite = GetBubbleSprite();
            sr.color = shieldColor;
            sr.sortingOrder = 12;
        }

        [ClientRpc]
        private void HideShieldClientRpc()
        {
            if (IsServer) return;
            DestroyActiveShield();
        }

        public override void OnNetworkDespawn()
        {
            DestroyActiveShield();
            base.OnNetworkDespawn();
        }
    }

    /// <summary>
    /// Attached to the shield collider object. Detects projectile hits and
    /// routes damage absorption through ShieldAbility.
    /// </summary>
    public class ShieldDamageReceiver : MonoBehaviour
    {
        private ShieldAbility _owner;

        public void Initialize(ShieldAbility owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Called externally (e.g., by Projectile) to check if damage should be absorbed.
        /// Returns damage that passes through the shield.
        /// </summary>
        public int ProcessHit(int damage)
        {
            if (_owner == null) return damage;
            return _owner.AbsorbDamage(damage);
        }
    }
}
