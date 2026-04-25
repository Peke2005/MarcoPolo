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
        private Vector3 _lastPos;
        private float _bobT;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            EnsureSpriteRenderers();
            ApplyBaseSprites();
        }

        private void EnsureSpriteRenderers()
        {
            if (mainSprite == null)
            {
                Transform body = transform.Find("Body");
                if (body == null)
                {
                    var go = new GameObject("Body");
                    go.transform.SetParent(transform, false);
                    body = go.transform;
                }
                mainSprite = body.GetComponent<SpriteRenderer>();
                if (mainSprite == null) mainSprite = body.gameObject.AddComponent<SpriteRenderer>();
            }
            if (weaponSprite == null)
            {
                Transform gun = transform.Find("Weapon");
                if (gun == null)
                {
                    var go = new GameObject("Weapon");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0.35f, 0f, 0f);
                    gun = go.transform;
                }
                weaponSprite = gun.GetComponent<SpriteRenderer>();
                if (weaponSprite == null) weaponSprite = gun.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private static Sprite _blueBodySprite;
        private static Sprite _redBodySprite;
        private static Sprite _bodySprite;
        private static Sprite _gunSprite;

        private void ApplyBaseSprites()
        {
            if (_blueBodySprite == null) _blueBodySprite = LoadSprite("RuntimeArt/Characters/manBlue_stand", 70f);
            if (_redBodySprite == null) _redBodySprite = LoadSprite("RuntimeArt/Characters/manBrown_stand", 70f);
            if (_gunSprite == null) _gunSprite = LoadSprite("RuntimeArt/Characters/weapon_gun", 48f);

            if (mainSprite != null)
            {
                if (_blueBodySprite == null && _bodySprite == null) _bodySprite = BuildBodySprite();
                mainSprite.sprite = _blueBodySprite != null ? _blueBodySprite : _bodySprite;
                mainSprite.drawMode = SpriteDrawMode.Simple;
                mainSprite.transform.localScale = Vector3.one;
                mainSprite.sortingOrder = 10;
            }
            if (weaponSprite != null)
            {
                if (_gunSprite == null) _gunSprite = BuildGunSprite();
                weaponSprite.sprite = _gunSprite;
                weaponSprite.drawMode = SpriteDrawMode.Simple;
                weaponSprite.transform.localPosition = new Vector3(0.38f, -0.02f, 0f);
                weaponSprite.transform.localScale = Vector3.one * 1.05f;
                weaponSprite.color = new Color(0.9f, 0.95f, 1f, 1f);
                weaponSprite.sortingOrder = 11;
            }
        }

        private static Sprite LoadSprite(string resourcePath, float pixelsPerUnit)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        private static Sprite BuildBodySprite()
        {
            int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float bodyR = size * 0.42f;
            float helmetR = size * 0.28f;
            Vector2 helmetCenter = c + new Vector2(size * 0.06f, 0f); // slight forward offset
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float db = Vector2.Distance(p, c);
                float dh = Vector2.Distance(p, helmetCenter);
                Color color = new Color(0, 0, 0, 0);
                if (db <= bodyR)
                {
                    // body disc with radial shading
                    float t = 1f - db / bodyR;
                    float shade = 0.55f + 0.45f * Mathf.Pow(t, 0.6f);
                    color = new Color(shade, shade, shade, 1f); // tinted by SpriteRenderer.color
                    // outline
                    if (db > bodyR - 2f) color *= 0.35f;
                }
                if (dh <= helmetR)
                {
                    float t = 1f - dh / helmetR;
                    float shade = 0.35f + 0.3f * Mathf.Pow(t, 0.8f);
                    color = new Color(shade * 0.35f, shade * 0.35f, shade * 0.35f, 1f);
                    if (dh > helmetR - 2f) color = new Color(0.08f, 0.08f, 0.08f, 1f);
                }
                px[y * size + x] = color;
            }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildGunSprite()
        {
            int w = 64, h = 20;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                // barrel (long thin)
                if (x >= w * 0.35f && y >= h * 0.4f && y <= h * 0.6f) c = new Color(0.9f, 0.9f, 0.9f, 1f);
                // body/grip
                if (x >= w * 0.1f && x <= w * 0.45f && y >= h * 0.15f && y <= h * 0.85f) c = new Color(0.9f, 0.9f, 0.9f, 1f);
                // outline
                if (c.a > 0 && (x == 0 || y == 0 || x == w - 1 || y == h - 1)) c *= 0.3f;
                px[y * w + x] = c;
            }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.2f, 0.5f), 48f);
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
                if (_blueBodySprite != null && _redBodySprite != null)
                {
                    mainSprite.sprite = faction == PlayerFaction.Blue ? _blueBodySprite : _redBodySprite;
                    mainSprite.color = Color.white;
                    _baseSpriteColor = Color.white;
                }
                else
                {
                    mainSprite.color = factionColor;
                    _baseSpriteColor = factionColor;
                }
            }

            if (weaponSprite != null)
            {
                Color gunMetal = new Color(0.9f, 0.95f, 1f, 1f);
                weaponSprite.color = gunMetal;
                _baseWeaponColor = gunMetal;
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

        private void LateUpdate()
        {
            if (mainSprite == null) return;
            float speed = (transform.position - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPos = transform.position;
            bool moving = speed > 0.05f;
            _bobT += Time.deltaTime * (moving ? 14f : 0f);
            float bob = moving ? Mathf.Sin(_bobT) * 0.06f : 0f;
            float squash = moving ? 1f + Mathf.Abs(Mathf.Sin(_bobT)) * 0.05f : 1f;
            Vector3 baseScale = Vector3.one;
            mainSprite.transform.localPosition = new Vector3(0f, bob, 0f);
            mainSprite.transform.localScale = new Vector3(baseScale.x * squash, baseScale.y / squash, 1f);
        }
    }
}
