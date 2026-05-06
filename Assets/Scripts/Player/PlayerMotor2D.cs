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
        private Collider2D _bodyCollider;
        private PlayerInputReader _input;
        private bool _movementEnabled = true;
        private Vector2 _rawKeyboardDir;
        private Vector2 _serverMoveDir;
        private Vector2 _externalMoveDir;
        private Vector2 _lastSentMoveDir;
        private float _moveRpcTimer;
        private readonly RaycastHit2D[] _moveHits = new RaycastHit2D[12];
        private const float MoveRpcInterval = 1f / 30f;

        public bool IsMovementEnabled => _movementEnabled;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _bodyCollider = GetComponent<Collider2D>();
            _input = GetComponent<PlayerInputReader>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                // Non-owners: kinematic, NetworkTransform handles sync
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                if (_input != null)
                    _input.IsInputEnabled = false;
            }
            else
            {
                _rb.bodyType = IsServer ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
                _rb.interpolation = RigidbodyInterpolation2D.None;
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
            if (kb != null)
            {
                Vector2 dir = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir.x -= 1f;
                _rawKeyboardDir = dir;
            }

            float speed = balanceData != null ? balanceData.moveSpeed : 5f;
            Vector2 moveDir = GetLocalMoveInput();

            if (IsServer)
            {
                // Host owns server authority, so move immediately in Update.
                // This avoids FixedUpdate + MovePosition render delay on the host PC.
                MoveServerAuthoritative(moveDir, speed, Time.deltaTime, immediate: true);
                return;
            }

            if (ShouldSendMove(moveDir))
            {
                SubmitMoveInputServerRpc(moveDir);
                _lastSentMoveDir = moveDir;
                _moveRpcTimer = MoveRpcInterval;
            }

            MoveServerAuthoritative(moveDir, speed, Time.deltaTime, immediate: true);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            float speed = balanceData != null ? balanceData.moveSpeed : 5f;
            Vector2 moveDir = GetLocalMoveInput();

            if (IsOwner)
            {
                return;
            }

            if (!IsServer) return;
            if (!IsOwner) moveDir = _serverMoveDir;

            MoveServerAuthoritative(moveDir, speed, Time.fixedDeltaTime);
        }

        private bool ShouldSendMove(Vector2 moveDir)
        {
            _moveRpcTimer -= Time.deltaTime;
            if ((moveDir - _lastSentMoveDir).sqrMagnitude > 0.0025f)
                return true;

            return _moveRpcTimer <= 0f;
        }

        private Vector2 GetLocalMoveInput()
        {
            if (_externalMoveDir.sqrMagnitude > 0.01f)
                return _externalMoveDir;

            Vector2 moveDir = _input != null ? _input.MoveInput : Vector2.zero;

            if (moveDir.sqrMagnitude < 0.01f)
                moveDir = _rawKeyboardDir;

            return moveDir;
        }

        private void MoveServerAuthoritative(Vector2 moveDir, float speed, float deltaTime, bool immediate = false)
        {
            if (!_movementEnabled) return;

            // Normalize to prevent diagonal speed boost
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            Vector2 currentPos = _rb != null ? _rb.position : (Vector2)transform.position;
            Vector2 delta = moveDir * speed * deltaTime;

            // Clamp to map bounds if available
            if (mapDefinition != null)
            {
                Vector2 clampedPos = currentPos + delta;
                clampedPos.x = Mathf.Clamp(clampedPos.x, mapDefinition.boundsMin.x, mapDefinition.boundsMax.x);
                clampedPos.y = Mathf.Clamp(clampedPos.y, mapDefinition.boundsMin.y, mapDefinition.boundsMax.y);
                delta = clampedPos - currentPos;
            }

            delta = ResolveBlockingDelta(delta);
            Vector2 targetPos = currentPos + delta;

            ApplyPosition(targetPos, immediate);
        }

        public Vector2 ForceMoveDelta(Vector2 delta)
        {
            if (delta.sqrMagnitude < 0.000001f) return Vector2.zero;

            Vector2 currentPos = _rb != null ? _rb.position : (Vector2)transform.position;
            if (mapDefinition != null)
            {
                Vector2 clampedPos = currentPos + delta;
                clampedPos.x = Mathf.Clamp(clampedPos.x, mapDefinition.boundsMin.x, mapDefinition.boundsMax.x);
                clampedPos.y = Mathf.Clamp(clampedPos.y, mapDefinition.boundsMin.y, mapDefinition.boundsMax.y);
                delta = clampedPos - currentPos;
            }

            delta = ResolveBlockingDelta(delta);
            Vector2 targetPos = currentPos + delta;
            ApplyPosition(targetPos, true);
            return delta;
        }

        private void ApplyPosition(Vector2 targetPos, bool immediate)
        {
            // Try physics-aware MovePosition first; if the rigidbody is kinematic
            // or somehow frozen, fall back to a raw transform move so the owner
            // always gets visual feedback.
            if (_rb != null && immediate)
            {
                _rb.position = targetPos;
                _rb.velocity = Vector2.zero;
                transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
            }
            else if (_rb != null && _rb.bodyType != RigidbodyType2D.Kinematic)
            {
                _rb.MovePosition(targetPos);
            }
            else
            {
                transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
            }
        }

        private Vector2 ResolveBlockingDelta(Vector2 delta)
        {
            if (_bodyCollider == null || !_bodyCollider.enabled || delta.sqrMagnitude < 0.000001f)
                return delta;

            Vector2 dir = delta.normalized;
            float distance = delta.magnitude;
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = Physics2D.DefaultRaycastLayers
            };

            int count = _bodyCollider.Cast(dir, filter, _moveHits, distance + 0.04f);
            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = _moveHits[i].collider;
                if (col == null || col == _bodyCollider) continue;
                if (col.attachedRigidbody != null && col.attachedRigidbody == _rb) continue;
                if (!IsBlockingCollider(col)) continue;
                if (Vector2.Dot(_moveHits[i].normal, -dir) < 0.35f) continue;

                allowed = Mathf.Min(allowed, Mathf.Max(0f, _moveHits[i].distance - 0.04f));
            }

            return dir * allowed;
        }

        private static bool IsBlockingCollider(Collider2D col)
        {
            string n = col.gameObject.name;
            return n.StartsWith("Wall_") || n.StartsWith("Cover_") || n.StartsWith("Decor_Crate") || n.StartsWith("Decor_Barrel");
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitMoveInputServerRpc(Vector2 moveDir, ServerRpcParams rpcParams = default)
        {
            if (OwnerClientId != rpcParams.Receive.SenderClientId) return;
            _serverMoveDir = moveDir.sqrMagnitude > 1f ? moveDir.normalized : moveDir;
        }

        public void SetExternalMoveInput(Vector2 moveDir)
        {
            _externalMoveDir = moveDir.sqrMagnitude > 1f ? moveDir.normalized : moveDir;
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
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
