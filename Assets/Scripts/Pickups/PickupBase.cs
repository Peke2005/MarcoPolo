using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Core;

namespace FrentePartido.Pickups
{
    public abstract class PickupBase : NetworkBehaviour
    {
        [SerializeField] private PickupType _type;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _bobAmplitude = 0.1f;
        [SerializeField] private float _bobFrequency = 2f;

        public PickupType Type => _type;

        public NetworkVariable<bool> IsAvailable = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Vector3 _basePosition;
        private float _bobOffset;

        public override void OnNetworkSpawn()
        {
            _basePosition = transform.position;
            _bobOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ApplyProceduralIcon();

            IsAvailable.OnValueChanged += (_, available) =>
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.enabled = available;
            };
        }

        private void ApplyProceduralIcon()
        {
            if (_spriteRenderer == null) return;
            var sprite = PickupIconCache.Get(_type);
            if (sprite != null)
            {
                _spriteRenderer.sprite = sprite;
                _spriteRenderer.drawMode = SpriteDrawMode.Simple;
                _spriteRenderer.transform.localScale = Vector3.one * 0.9f;
                _spriteRenderer.sortingOrder = 6;
                _spriteRenderer.color = Color.white;
            }
        }

        private void Update()
        {
            if (!IsAvailable.Value) return;

            float yOffset = Mathf.Sin(Time.time * _bobFrequency + _bobOffset) * _bobAmplitude;
            transform.position = _basePosition + Vector3.up * yOffset;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsServer || !IsAvailable.Value) return;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null) return;

            var health = other.GetComponentInParent<Player.PlayerHealth>();
            if (health == null || health.IsDead) return;

            ApplyEffect(other.gameObject, netObj.OwnerClientId);
            IsAvailable.Value = false;
            PlayPickupEffectClientRpc();
        }

        protected abstract void ApplyEffect(GameObject player, ulong clientId);

        [ClientRpc]
        private void PlayPickupEffectClientRpc()
        {
            FxManager.PlayPickup(transform.position);
            FxManager.SpawnExplosionRing(transform.position, 1.2f);
        }

        public void MakeAvailable()
        {
            if (!IsServer) return;
            IsAvailable.Value = true;
            _basePosition = transform.position;
        }
    }
}
