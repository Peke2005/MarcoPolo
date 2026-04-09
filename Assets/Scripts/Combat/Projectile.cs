using UnityEngine;

namespace FrentePartido.Combat
{
    /// <summary>
    /// Visual-only bullet tracer. NOT a NetworkObject.
    /// Spawned client-side for visual feedback; actual damage is hitscan on server.
    /// Pool-friendly: resets on enable, cleans up on disable.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 40f;
        [SerializeField] private float lifetime = 0.5f;

        [Header("Visuals")]
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private SpriteRenderer bulletSprite;

        private Vector2 _direction;
        private float _spawnTime;
        private Rigidbody2D _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;

            if (bulletSprite == null)
                bulletSprite = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _spawnTime = Time.time;

            // Reset trail to avoid artifacts from pooling
            if (trail != null)
            {
                trail.Clear();
                trail.enabled = true;
            }

            if (bulletSprite != null)
                bulletSprite.enabled = true;
        }

        private void OnDisable()
        {
            _rb.velocity = Vector2.zero;

            if (trail != null)
            {
                trail.Clear();
                trail.enabled = false;
            }
        }

        /// <summary>
        /// Initialize direction and start moving. Call immediately after instantiation.
        /// </summary>
        public void Initialize(Vector2 direction)
        {
            _direction = direction.normalized;

            // Orient sprite along movement direction
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _rb.velocity = _direction * speed;
        }

        private void Update()
        {
            // Destroy after lifetime
            if (Time.time - _spawnTime >= lifetime)
            {
                ReturnToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Visual-only collision: just destroy the tracer, no damage logic
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            // If using object pool, deactivate instead of destroy.
            // For now, Destroy. Replace with pool return when pool system exists.
            Destroy(gameObject);
        }
    }
}
