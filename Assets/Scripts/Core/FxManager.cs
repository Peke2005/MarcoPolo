using UnityEngine;

namespace FrentePartido.Core
{
    /// <summary>
    /// Runtime procedural FX: muzzle flash, hit spark, explosion ring, and synthesized SFX.
    /// All assets cached; no external dependencies.
    /// </summary>
    public static class FxManager
    {
        private static Sprite _flashSprite;
        private static Sprite _sparkSprite;
        private static Sprite _ringSprite;
        private static AudioClip _gunshotClip;
        private static AudioClip _hitClip;
        private static AudioClip _pickupClip;
        private static AudioClip _explosionClip;

        // ── Visual ────────────────────────────────────────────────────
        public static void SpawnMuzzleFlash(Vector3 position, Vector2 direction)
        {
            EnsureSprites();
            var go = new GameObject("FX_MuzzleFlash");
            go.transform.position = position;
            float ang = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, ang);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _flashSprite;
            sr.color = new Color(1f, 0.85f, 0.3f, 1f);
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            go.AddComponent<FxFade>().Init(0.08f, sr.color);
        }

        public static void SpawnHitSpark(Vector3 position)
        {
            EnsureSprites();
            var go = new GameObject("FX_HitSpark");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sparkSprite;
            sr.color = new Color(1f, 0.8f, 0.2f, 1f);
            sr.sortingOrder = 30;
            go.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            go.AddComponent<FxFade>().Init(0.2f, sr.color);
        }

        public static void SpawnExplosionRing(Vector3 position, float radius)
        {
            EnsureSprites();
            var go = new GameObject("FX_Explosion");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _ringSprite;
            sr.color = new Color(1f, 0.5f, 0.15f, 1f);
            sr.sortingOrder = 30;
            go.transform.localScale = Vector3.one * radius * 0.3f;
            go.AddComponent<FxExpand>().Init(radius, 0.4f, sr.color);
        }

        // ── Audio ────────────────────────────────────────────────────
        public static void PlayGunshot(Vector3 pos, float volume = 0.5f)
        {
            EnsureClips();
            AudioSource.PlayClipAtPoint(_gunshotClip, pos, volume);
        }

        public static void PlayHit(Vector3 pos, float volume = 0.5f)
        {
            EnsureClips();
            AudioSource.PlayClipAtPoint(_hitClip, pos, volume);
        }

        public static void PlayPickup(Vector3 pos, float volume = 0.7f)
        {
            EnsureClips();
            AudioSource.PlayClipAtPoint(_pickupClip, pos, volume);
        }

        public static void PlayExplosion(Vector3 pos, float volume = 0.8f)
        {
            EnsureClips();
            AudioSource.PlayClipAtPoint(_explosionClip, pos, volume);
        }

        // ── Sprites ──────────────────────────────────────────────────
        private static void EnsureSprites()
        {
            if (_flashSprite == null) _flashSprite = BuildFlashSprite();
            if (_sparkSprite == null) _sparkSprite = BuildSparkSprite();
            if (_ringSprite  == null) _ringSprite  = BuildRingSprite();
        }

        private static Sprite BuildFlashSprite()
        {
            int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            Vector2 c = new Vector2(0, h / 2f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - c.x) / w;
                float dy = (y - c.y) / (h / 2f);
                float falloff = Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - Mathf.Abs(dy));
                float a = Mathf.Pow(falloff, 1.6f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 48f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildSparkSprite()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float t = Mathf.Clamp01(1f - d / (size * 0.5f));
                // 4-point star shape
                float ang = Mathf.Atan2(y - c.y, x - c.x);
                float spike = Mathf.Abs(Mathf.Cos(ang * 2f));
                float a = Mathf.Pow(t, 2.2f) * (0.5f + 0.5f * spike);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildRingSprite()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / r;
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.9f) * 8f, 2f));
                float core = Mathf.Pow(Mathf.Clamp01(1f - d), 3f) * 0.6f;
                float a = Mathf.Clamp01(ring + core);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        }

        // ── Audio synthesis ──────────────────────────────────────────
        private static void EnsureClips()
        {
            if (_gunshotClip == null) _gunshotClip = BuildGunshot();
            if (_hitClip == null) _hitClip = BuildHit();
            if (_pickupClip == null) _pickupClip = BuildPickup();
            if (_explosionClip == null) _explosionClip = BuildExplosion();
        }

        private static AudioClip BuildGunshot()
        {
            int sr = 22050;
            float dur = 0.18f;
            int n = (int)(sr * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 14f);
                float noise = (Random.value * 2f - 1f);
                float tone = Mathf.Sin(2f * Mathf.PI * 120f * (i / (float)sr)) * 0.4f;
                data[i] = (noise * 0.9f + tone) * env;
            }
            var clip = AudioClip.Create("sfx_gunshot", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildHit()
        {
            int sr = 22050;
            float dur = 0.08f;
            int n = (int)(sr * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 25f);
                float noise = (Random.value * 2f - 1f) * 0.7f;
                float click = Mathf.Sin(2f * Mathf.PI * 900f * (i / (float)sr)) * 0.5f;
                data[i] = (noise + click) * env;
            }
            var clip = AudioClip.Create("sfx_hit", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildPickup()
        {
            int sr = 22050;
            float dur = 0.25f;
            int n = (int)(sr * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI);
                float freq = Mathf.Lerp(600f, 1200f, t);
                float s = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sr));
                data[i] = s * env * 0.6f;
            }
            var clip = AudioClip.Create("sfx_pickup", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildExplosion()
        {
            int sr = 22050;
            float dur = 0.6f;
            int n = (int)(sr * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 5f);
                float noise = (Random.value * 2f - 1f);
                float rumble = Mathf.Sin(2f * Mathf.PI * 55f * (i / (float)sr)) * 0.6f;
                data[i] = (noise * 0.8f + rumble) * env;
            }
            var clip = AudioClip.Create("sfx_explosion", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>Fades a SpriteRenderer's alpha and destroys its GameObject.</summary>
    public class FxFade : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _life;
        private float _t;
        private Color _base;

        public void Init(float life, Color baseColor)
        {
            _life = life;
            _base = baseColor;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_sr != null)
            {
                float a = Mathf.Clamp01(1f - _t / _life);
                _sr.color = new Color(_base.r, _base.g, _base.b, _base.a * a);
            }
            if (_t >= _life) Destroy(gameObject);
        }
    }

    /// <summary>Expands a SpriteRenderer's scale and fades it out.</summary>
    public class FxExpand : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _life;
        private float _t;
        private float _targetScale;
        private Color _base;

        public void Init(float targetScale, float life, Color baseColor)
        {
            _targetScale = targetScale;
            _life = life;
            _base = baseColor;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);
            float s = Mathf.Lerp(0.2f, _targetScale, Mathf.Sqrt(k));
            transform.localScale = new Vector3(s, s, 1f);
            if (_sr != null)
            {
                float a = 1f - k;
                _sr.color = new Color(_base.r, _base.g, _base.b, _base.a * a);
            }
            if (_t >= _life) Destroy(gameObject);
        }
    }
}
