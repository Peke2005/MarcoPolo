using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Player
{
    /// <summary>
    /// Visual presentation: faction colors, damage flash, death effects.
    /// Subscribes to PlayerHealth events for reactive visuals.
    /// </summary>
    public class PlayerPresentation : NetworkBehaviour
    {
        [Header("Sprite References")]
        [SerializeField] private SpriteRenderer mainSprite;
        [SerializeField] private SpriteRenderer weaponSprite;

        [Header("Faction Colors")]
        [SerializeField] private Color blueColor = new Color(0.2f, 0.4f, 1f, 1f);
        [SerializeField] private Color redColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Header("Damage Flash")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;

        /// <summary>
        /// Faction as byte: 0 = Blue, 1 = Red. Synced to all clients.
        /// </summary>
        public NetworkVariable<byte> Faction = new NetworkVariable<byte>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PlayerHealth _health;
        private Color _baseSpriteColor;
        private Color _baseWeaponColor;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Faction.OnValueChanged += OnFactionChanged;

            // Apply initial faction color
            ApplyFactionColor((PlayerFaction)Faction.Value);

            // Subscribe to health events
            if (_health != null)
            {
                _health.OnDamageReceived += FlashDamage;
                _health.OnPlayerDied += OnPlayerDied;
            }
        }

        public override void OnNetworkDespawn()
        {
            Faction.OnValueChanged -= OnFactionChanged;

            if (_health != null)
            {
                _health.OnDamageReceived -= FlashDamage;
                _health.OnPlayerDied -= OnPlayerDied;
            }

            base.OnNetworkDespawn();
        }

        private void OnFactionChanged(byte oldValue, byte newValue)
        {
            ApplyFactionColor((PlayerFaction)newValue);
        }

        private void ApplyFactionColor(PlayerFaction faction)
        {
            Color factionColor = faction == PlayerFaction.Blue ? blueColor : redColor;

            if (mainSprite != null)
            {
                mainSprite.color = factionColor;
                _baseSpriteColor = factionColor;
            }

            if (weaponSprite != null)
            {
                weaponSprite.color = factionColor;
                _baseWeaponColor = factionColor;
            }
        }

        /// <summary>
        /// Brief flash effect when taking damage.
        /// </summary>
        public void FlashDamage()
        {
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            _flashCoroutine = StartCoroutine(FlashDamageCoroutine());
        }

        private IEnumerator FlashDamageCoroutine()
        {
            if (mainSprite != null)
                mainSprite.color = flashColor;
            if (weaponSprite != null)
                weaponSprite.color = flashColor;

            yield return new WaitForSeconds(flashDuration);

            if (mainSprite != null)
                mainSprite.color = _baseSpriteColor;
            if (weaponSprite != null)
                weaponSprite.color = _baseWeaponColor;

            _flashCoroutine = null;
        }

        /// <summary>
        /// Disable sprites or play death visual.
        /// </summary>
        public void ShowDeathEffect()
        {
            if (mainSprite != null)
            {
                // Fade to half-transparent as simple death visual
                Color deathColor = _baseSpriteColor;
                deathColor.a = 0.3f;
                mainSprite.color = deathColor;
            }

            if (weaponSprite != null)
                weaponSprite.enabled = false;
        }

        /// <summary>
        /// Restore visuals for a new round.
        /// </summary>
        public void ResetVisuals()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            ApplyFactionColor((PlayerFaction)Faction.Value);

            if (mainSprite != null)
                mainSprite.enabled = true;

            if (weaponSprite != null)
                weaponSprite.enabled = true;
        }

        private void OnPlayerDied(ulong killerClientId)
        {
            ShowDeathEffect();
        }
    }
}
