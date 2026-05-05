using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Player;
using FrentePartido.Core;

namespace FrentePartido.Combat
{
    /// <summary>
    /// Server-authoritative weapon system. Hitscan on server, visual tracers on clients.
    /// Manages ammo, fire rate, reloading, and damage application.
    /// </summary>
    public class WeaponController : NetworkBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private WeaponData weaponData;

        public float ReloadTime => weaponData != null ? weaponData.reloadTime : 1.4f;

        [Header("Hitscan")]
        [SerializeField] private LayerMask hitLayers;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private Transform firePoint;

        // --- Network State ---
        public NetworkVariable<int> CurrentAmmo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> IsReloading = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // --- Events (client-side, for UI/VFX) ---
        public event Action<int, int> OnAmmoChanged;
        public event Action OnReloadStarted;
        public event Action OnReloadFinished;
        public event Action OnWeaponFired;

        // --- Cached Components ---
        private PlayerInputReader _input;
        private PlayerHealth _playerHealth;

        // --- Internal State ---
        private float _fireCooldown;
        private bool _fireHeld;
        private Coroutine _reloadCoroutine;
        private readonly RaycastHit2D[] _shotHits = new RaycastHit2D[16];
        private const float ShotRadius = 0.14f;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _playerHealth = GetComponent<PlayerHealth>();

            if (firePoint == null)
                firePoint = transform;

            // Layer defaults: if unset in prefab, hit everything except this player.
            if (hitLayers.value == 0) hitLayers = ~0;
            if (obstacleLayer.value == 0) obstacleLayer = ~0;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                CurrentAmmo.Value = weaponData != null ? weaponData.magazineSize : 8;
            }

            CurrentAmmo.OnValueChanged += HandleAmmoChanged;

            if (IsOwner && _input != null)
            {
                _input.OnFirePressed += HandleFirePressed;
                _input.OnFireReleased += HandleFireReleased;
                _input.OnReloadPressed += HandleReloadPressed;
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentAmmo.OnValueChanged -= HandleAmmoChanged;

            if (IsOwner && _input != null)
            {
                _input.OnFirePressed -= HandleFirePressed;
                _input.OnFireReleased -= HandleFireReleased;
                _input.OnReloadPressed -= HandleReloadPressed;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;

            _fireCooldown -= Time.deltaTime;

            if (_fireHeld && CanFire())
            {
                Vector2 origin = firePoint.position;
                Vector2 aimPos = _input != null ? _input.AimWorldPosition : (Vector2)transform.up;
                Vector2 direction = (aimPos - origin).normalized;

                FireServerRpc(origin, direction);
                _fireCooldown = weaponData != null ? weaponData.FireInterval : 0.25f;
            }
        }

        private bool CanFire()
        {
            if (weaponData == null) return false;
            if (_fireCooldown > 0f) return false;
            if (IsReloading.Value) return false;
            if (CurrentAmmo.Value <= 0) return false;
            if (_playerHealth != null && _playerHealth.IsDead) return false;
            return true;
        }

        // ----- Input Handlers (Owner only) -----

        private void HandleFirePressed()
        {
            _fireHeld = true;
        }

        private void HandleFireReleased()
        {
            _fireHeld = false;
        }

        private void HandleReloadPressed()
        {
            if (IsReloading.Value) return;
            if (weaponData != null && CurrentAmmo.Value >= weaponData.magazineSize) return;

            ReloadServerRpc();
        }

        // ----- Server RPCs -----

        [ServerRpc]
        private void FireServerRpc(Vector2 origin, Vector2 direction, ServerRpcParams rpcParams = default)
        {
            if (weaponData == null) return;
            if (CurrentAmmo.Value <= 0) return;
            if (IsReloading.Value) return;

            // Apply spread on server for authoritative hit detection
            Vector2 spreadDir = DamageDealer.ApplySpread(direction, weaponData.spreadAngle);

            // Decrement ammo
            CurrentAmmo.Value--;

            ResolveServerHitscan(origin, spreadDir, rpcParams.Receive.SenderClientId);

            // Notify all clients for VFX/SFX
            OnFireClientRpc(origin, spreadDir);

            // Auto-reload when empty
            if (CurrentAmmo.Value <= 0 && _reloadCoroutine == null)
            {
                _reloadCoroutine = StartCoroutine(ReloadCoroutine());
            }
        }

        [ServerRpc]
        private void ReloadServerRpc(ServerRpcParams rpcParams = default)
        {
            if (weaponData == null) return;
            if (IsReloading.Value) return;
            if (CurrentAmmo.Value >= weaponData.magazineSize) return;

            if (_reloadCoroutine != null)
                StopCoroutine(_reloadCoroutine);

            _reloadCoroutine = StartCoroutine(ReloadCoroutine());
        }

        // ----- Client RPCs -----

        [ClientRpc]
        private void OnFireClientRpc(Vector2 origin, Vector2 direction)
        {
            OnWeaponFired?.Invoke();

            // Spawn visual tracer projectile
            if (weaponData != null && weaponData.projectilePrefab != null)
            {
                GameObject tracerObj = Instantiate(
                    weaponData.projectilePrefab,
                    origin,
                    Quaternion.identity
                );

                Projectile tracer = tracerObj.GetComponent<Projectile>();
                if (tracer != null)
                {
                    tracer.Initialize(direction);
                }
            }
            else
            {
                float tracerRange = weaponData != null ? weaponData.range : 15f;
                FxManager.SpawnBulletTracer(origin, direction, tracerRange);
            }

            // Spawn muzzle flash (configured prefab or procedural fallback)
            if (weaponData != null && weaponData.muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(
                    weaponData.muzzleFlashPrefab,
                    firePoint.position,
                    Quaternion.identity
                );
                Destroy(flash, 0.15f);
            }
            else
            {
                FxManager.SpawnMuzzleFlash(firePoint.position, direction);
            }

            // Play fire sound (configured clip or procedural fallback)
            if (weaponData != null && weaponData.fireSound != null)
                AudioSource.PlayClipAtPoint(weaponData.fireSound, origin);
            else
                FxManager.PlayGunshot(origin);

            // Client-side hit spark via visual raycast (approximation of server hit)
            float range = weaponData != null ? weaponData.range : 15f;
            RaycastHit2D visHit = FindFirstVisualHit(origin, direction, range);
            if (visHit.collider != null)
            {
                FxManager.SpawnHitSpark(visHit.point);
                if (visHit.collider.GetComponentInParent<PlayerHealth>() != null)
                    FxManager.PlayHit(visHit.point, 0.4f);
            }
        }

        private void ResolveServerHitscan(Vector2 origin, Vector2 direction, ulong sourceId)
        {
            int mask = hitLayers | obstacleLayer;
            if (direction.sqrMagnitude < 0.001f) return;
            direction.Normalize();

            // Start slightly outside own body and use a small radius so shots are readable online.
            Vector2 castOrigin = origin + direction * 0.25f;
            int count = Physics2D.CircleCastNonAlloc(castOrigin, ShotRadius, direction, _shotHits, weaponData.range, mask);
            if (count <= 0) return;

            System.Array.Sort(_shotHits, 0, count, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _shotHits[i];
                Collider2D col = hit.collider;
                if (col == null || col.isTrigger) continue;

                PlayerHealth targetHealth = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
                if (targetHealth == _playerHealth) continue;

                if (targetHealth != null)
                {
                    if (targetHealth.IsDead) return;
                    int damage = DamageDealer.CalculateDamage(weaponData.damage);
                    targetHealth.ApplyDamageServer(damage, sourceId);
                    Debug.Log($"[Combat] Hit {targetHealth.OwnerClientId} for {damage}. HP={targetHealth.CurrentHealth.Value}");
                    return;
                }

                if (IsBlockingHit(col)) return;
            }
        }

        private RaycastHit2D FindFirstVisualHit(Vector2 origin, Vector2 direction, float range)
        {
            int count = Physics2D.RaycastNonAlloc(origin, direction, _shotHits, range);
            if (count <= 0) return default;

            System.Array.Sort(_shotHits, 0, count, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _shotHits[i];
                Collider2D col = hit.collider;
                if (col == null || col.isTrigger) continue;
                PlayerHealth targetHealth = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
                if (targetHealth == _playerHealth) continue;
                return hit;
            }

            return default;
        }

        private static bool IsBlockingHit(Collider2D col)
        {
            string n = col.gameObject.name;
            return n.StartsWith("Wall_") || n.StartsWith("Cover_") || n.StartsWith("Decor_");
        }

        [ClientRpc]
        private void ReloadStartedClientRpc()
        {
            OnReloadStarted?.Invoke();

            if (weaponData != null && weaponData.reloadSound != null)
            {
                AudioSource.PlayClipAtPoint(weaponData.reloadSound, transform.position);
            }
        }

        [ClientRpc]
        private void ReloadFinishedClientRpc()
        {
            OnReloadFinished?.Invoke();
        }

        // ----- Server Coroutines -----

        private IEnumerator ReloadCoroutine()
        {
            IsReloading.Value = true;
            ReloadStartedClientRpc();

            float reloadTime = weaponData != null ? weaponData.reloadTime : 1.4f;
            yield return new WaitForSeconds(reloadTime);

            if (weaponData != null)
                CurrentAmmo.Value = weaponData.magazineSize;

            IsReloading.Value = false;
            _reloadCoroutine = null;

            ReloadFinishedClientRpc();
        }

        // ----- Public API (Server only) -----

        /// <summary>
        /// Refill ammo to full. Server only. Used by ammo pickups.
        /// </summary>
        public void RefillAmmo()
        {
            if (!IsServer) return;
            if (weaponData == null) return;

            CurrentAmmo.Value = weaponData.magazineSize;
        }

        /// <summary>
        /// Reset weapon to default state for new round. Server only.
        /// </summary>
        public void ResetWeapon()
        {
            if (!IsServer) return;

            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                _reloadCoroutine = null;
            }

            IsReloading.Value = false;
            CurrentAmmo.Value = weaponData != null ? weaponData.magazineSize : 8;
        }

        // ----- Callbacks -----

        private void HandleAmmoChanged(int oldValue, int newValue)
        {
            int max = weaponData != null ? weaponData.magazineSize : 8;
            OnAmmoChanged?.Invoke(newValue, max);
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit2D>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();

            public int Compare(RaycastHit2D a, RaycastHit2D b)
            {
                return a.distance.CompareTo(b.distance);
            }
        }
    }
}
