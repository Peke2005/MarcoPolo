using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
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
                _movementEnabled = true;
                if (_input != null) _input.IsInputEnabled = true;
            }
        }

        private void Update()
        {
            // Owner-side WASD read every frame so we don't depend on the
            // InputAction being properly bound. Runs in Update to capture
            // key state reliably; the actual move is applied in FixedUpdate.
            if (!IsSpawned || !IsOwner) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir.x -= 1f;
            _rawKeyboardDir = dir;
        }

        private Vector2 _rawKeyboardDir;

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner) return;

            float speed = balanceData != null ? balanceData.moveSpeed : 5f;
            Vector2 moveDir = _input != null ? _input.MoveInput : Vector2.zero;

            if (moveDir.sqrMagnitude < 0.01f)
                moveDir = _rawKeyboardDir;

            if (!_movementEnabled && moveDir.sqrMagnitude < 0.01f) return;

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

            // Try physics-aware MovePosition first; if the rigidbody is kinematic
            // or somehow frozen, fall back to a raw transform move so the owner
            // always gets visual feedback.
            if (_rb != null && _rb.bodyType != RigidbodyType2D.Kinematic)
                _rb.MovePosition(targetPos);
            else
                transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
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
