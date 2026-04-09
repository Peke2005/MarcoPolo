using Unity.Netcode;
using UnityEngine;

namespace FrentePartido.Player
{
    /// <summary>
    /// Handles aim rotation toward the mouse cursor.
    /// Syncs aim angle via NetworkVariable so remote clients see correct aim direction.
    /// </summary>
    public class PlayerAimController : NetworkBehaviour
    {
        [SerializeField] private Transform weaponPivot;

        /// <summary>
        /// Aim angle in degrees, synced to all clients.
        /// </summary>
        public NetworkVariable<float> AimAngle = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private PlayerInputReader _input;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            AimAngle.OnValueChanged += OnAimAngleChanged;

            // Apply initial angle for late joiners
            ApplyRotation(AimAngle.Value);
        }

        public override void OnNetworkDespawn()
        {
            AimAngle.OnValueChanged -= OnAimAngleChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            if (_input == null || !_input.IsInputEnabled) return;

            Vector2 aimTarget = _input.AimWorldPosition;
            Vector2 origin = (Vector2)transform.position;
            Vector2 direction = aimTarget - origin;

            if (direction.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            AimAngle.Value = angle;
            ApplyRotation(angle);
        }

        private void OnAimAngleChanged(float oldValue, float newValue)
        {
            // Remote clients apply rotation when the NetworkVariable updates
            if (!IsOwner)
                ApplyRotation(newValue);
        }

        private void ApplyRotation(float angleDeg)
        {
            if (weaponPivot != null)
            {
                weaponPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
            else
            {
                // Fallback: rotate the whole object
                transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }
    }
}
