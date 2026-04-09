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
            GameObject shield;

            if (shieldPrefab != null)
            {
                shield = Instantiate(shieldPrefab, position, Quaternion.Euler(0f, 0f, angle));
            }
            else
            {
                // Create a simple shield collider if no prefab assigned
                shield = new GameObject("PlayerShield");
                shield.transform.position = position;
                shield.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                // Box collider as shield surface
                BoxCollider2D col = shield.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.3f, 1.5f);
                col.isTrigger = false;

                // Visual
                SpriteRenderer sr = shield.AddComponent<SpriteRenderer>();
                sr.color = shieldColor;
                sr.sortingOrder = 5;
            }

            // Attach shield damage handler
            ShieldDamageReceiver receiver = shield.GetComponent<ShieldDamageReceiver>();
            if (receiver == null)
                receiver = shield.AddComponent<ShieldDamageReceiver>();

            receiver.Initialize(this);

            // Parent to player so it moves with them
            shield.transform.SetParent(transform);

            shield.layer = gameObject.layer;

            return shield;
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
            // Server already has the shield object; clients create a local visual
            if (IsServer) return;

            DestroyActiveShield();

            _activeShield = new GameObject("ShieldVisual");
            _activeShield.transform.position = position;
            _activeShield.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _activeShield.transform.SetParent(transform);

            SpriteRenderer sr = _activeShield.AddComponent<SpriteRenderer>();
            sr.color = shieldColor;
            sr.sortingOrder = 5;
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
