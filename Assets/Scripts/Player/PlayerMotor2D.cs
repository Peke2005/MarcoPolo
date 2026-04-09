using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Player
{
    /// <summary>
    /// Server-authoritative-ish top-down 2D movement using owner prediction.
    /// Requires Rigidbody2D and NetworkTransform on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotor2D : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData balanceData;
        [SerializeField] private MapDefinition mapDefinition;

        private Rigidbody2D _rb;
        private PlayerInputReader _input;
        private bool _movementEnabled = true;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _input = GetComponent<PlayerInputReader>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                // Non-owners: kinematic, NetworkTransform handles sync
                _rb.bodyType = RigidbodyType2D.Kinematic;
                if (_input != null)
                    _input.IsInputEnabled = false;
            }
            else
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.gravityScale = 0f; // Top-down, no gravity
                _rb.freezeRotation = true;
            }
        }

        private void FixedUpdate()
        {
            if (!IsOwner || !IsSpawned) return;
            if (!_movementEnabled || _input == null) return;

            float speed = balanceData != null ? balanceData.moveSpeed : 5f;
            Vector2 moveDir = _input.MoveInput;

            // Normalize to prevent diagonal speed boost
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            Vector2 targetPos = _rb.position + moveDir * speed * Time.fixedDeltaTime;

            // Clamp to map bounds if available
            if (mapDefinition != null)
            {
                targetPos.x = Mathf.Clamp(targetPos.x, mapDefinition.boundsMin.x, mapDefinition.boundsMax.x);
                targetPos.y = Mathf.Clamp(targetPos.y, mapDefinition.boundsMin.y, mapDefinition.boundsMax.y);
            }

            _rb.MovePosition(targetPos);
        }

        /// <summary>
        /// Enable or disable movement processing (round intro, death, etc.).
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;

            if (!enabled)
            {
                _rb.velocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Teleport player to a position. Call on server; NetworkTransform syncs it.
        /// </summary>
        public void TeleportTo(Vector2 position)
        {
            _rb.position = position;
            _rb.velocity = Vector2.zero;
        }
    }
}
