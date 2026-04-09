using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Player;

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

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _playerHealth = GetComponent<PlayerHealth>();

            if (firePoint == null)
                firePoint = transform;
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

            // Hitscan raycast
            RaycastHit2D hit = Physics2D.Raycast(origin, spreadDir, weaponData.range, hitLayers | obstacleLayer);

            if (hit.collider != null)
            {
                PlayerHealth targetHealth = hit.collider.GetComponent<PlayerHealth>();
                if (targetHealth == null)
                    targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();

                if (targetHealth != null && !targetHealth.IsDead)
                {
                    float distance = hit.distance;
                    int damage = DamageDealer.CalculateDamage(weaponData.damage, distance, weaponData.range);
                    ulong sourceId = rpcParams.Receive.SenderClientId;
                    targetHealth.TakeDamageServerRpc(damage, sourceId);
                }
            }

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

            // Spawn muzzle flash
            if (weaponData != null && weaponData.muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(
                    weaponData.muzzleFlashPrefab,
                    firePoint.position,
                    Quaternion.identity
                );
                Destroy(flash, 0.15f);
            }

            // Play fire sound
            if (weaponData != null && weaponData.fireSound != null)
            {
                AudioSource.PlayClipAtPoint(weaponData.fireSound, origin);
            }
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
    }
}
