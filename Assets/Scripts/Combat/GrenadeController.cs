using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Player;

namespace FrentePartido.Combat
{
    /// <summary>
    /// Server-spawned grenade NetworkObject. Physics-driven on server,
    /// explodes after fuse time dealing area damage with linear falloff.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class GrenadeController : NetworkBehaviour
    {
        [Header("Balance")]
        [SerializeField] private BalanceTuningData balanceData;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField] private AudioClip explosionSound;

        [Header("Flash Warning")]
        [SerializeField] private Color normalColor = Color.gray;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private float flashStartRatio = 0.5f;

        [Header("Explosion")]
        [SerializeField] private LayerMask damageableLayers;
        [SerializeField] private LayerMask obstacleLayer;

        // --- Network State ---
        public NetworkVariable<ulong> OwnerClientId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // --- Cached ---
        private Rigidbody2D _rb;
        private float _fuseTimer;
        private bool _hasExploded;
        private float _fuseTime;
        private float _flashTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f; // Top-down 2D, no gravity

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _fuseTime = balanceData != null ? balanceData.grenadeFuseTime : 1.2f;
            _fuseTimer = 0f;
            _hasExploded = false;

            if (!IsServer)
            {
                // Clients: kinematic, server drives position via NetworkTransform
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.drag = 3f; // Grenade slows down over time
            }
        }

        /// <summary>
        /// Initialize grenade velocity. Call on server immediately after spawn.
        /// </summary>
        public void Launch(Vector2 direction, ulong ownerClientId)
        {
            if (!IsServer) return;

            OwnerClientId.Value = ownerClientId;

            float throwForce = balanceData != null ? balanceData.grenadeThrowForce : 12f;
            _rb.velocity = direction.normalized * throwForce;
        }

        private void Update()
        {
            if (_hasExploded) return;

            _fuseTimer += Time.deltaTime;

            // Visual countdown flash on all clients
            UpdateFlashWarning();

            // Server handles explosion timing
            if (IsServer && _fuseTimer >= _fuseTime)
            {
                Explode();
            }
        }

        private void UpdateFlashWarning()
        {
            if (spriteRenderer == null) return;

            float flashThreshold = _fuseTime * flashStartRatio;
            if (_fuseTimer < flashThreshold)
            {
                spriteRenderer.color = normalColor;
                return;
            }

            // Flash frequency increases as fuse runs out
            float remaining = _fuseTime - _fuseTimer;
            float urgency = 1f - (remaining / (_fuseTime * (1f - flashStartRatio)));
            float flashFrequency = Mathf.Lerp(4f, 20f, urgency);

            _flashTimer += Time.deltaTime * flashFrequency;
            bool flashOn = Mathf.Sin(_flashTimer * Mathf.PI) > 0f;
            spriteRenderer.color = flashOn ? warningColor : normalColor;
        }

        private void Explode()
        {
            if (_hasExploded) return;
            _hasExploded = true;

            Vector2 explosionPos = _rb.position;
            float radius = balanceData != null ? balanceData.grenadeRadius : 2.5f;
            int baseDamage = balanceData != null ? balanceData.grenadeDamage : 40;

            // Overlap check for all damageable objects in radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionPos, radius, damageableLayers);

            foreach (Collider2D col in hits)
            {
                PlayerHealth targetHealth = col.GetComponent<PlayerHealth>();
                if (targetHealth == null)
                    targetHealth = col.GetComponentInParent<PlayerHealth>();

                if (targetHealth == null || targetHealth.IsDead) continue;

                Vector2 targetPos = (Vector2)col.transform.position;
                float distance = Vector2.Distance(explosionPos, targetPos);

                // Optional: line-of-sight check (grenades can damage through thin cover or not)
                if (!DamageDealer.IsInLineOfSight(explosionPos, targetPos, obstacleLayer))
                    continue;

                int damage = DamageDealer.CalculateGrenadeDamage(baseDamage, distance, radius);
                if (damage > 0)
                {
                    targetHealth.TakeDamageServerRpc(damage, OwnerClientId.Value);
                }
            }

            // Notify clients for VFX/SFX
            ExplodeClientRpc(explosionPos);

            // Despawn after a short delay to let ClientRpc arrive
            StartCoroutine(DespawnAfterDelay(0.1f));
        }

        [ClientRpc]
        private void ExplodeClientRpc(Vector2 position)
        {
            // Spawn explosion effect
            if (explosionEffectPrefab != null)
            {
                GameObject fx = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
                Destroy(fx, 2f);
            }

            // Play explosion sound
            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, position);
            }

            // Hide grenade sprite immediately on all clients
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (IsServer && IsSpawned)
            {
                GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }
}
