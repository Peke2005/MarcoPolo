using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Core
{
    /// <summary>Procedural pickup icon sprites cached per PickupType.</summary>
    public static class PickupIconCache
    {
        private static Sprite _health;
        private static Sprite _ammo;
        private static Sprite _armor;

        public static Sprite Get(PickupType t)
        {
            switch (t)
            {
                case PickupType.Health: return _health ??= BuildHealthCross();
                case PickupType.Ammo:   return _ammo   ??= BuildAmmoBullet();
                case PickupType.Armor:  return _armor  ??= BuildArmorShield();
            }
            return null;
        }

        private static Sprite BuildHealthCross()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            Color bg = new Color(0.95f, 0.95f, 0.95f, 1f);
            Color cross = new Color(0.85f, 0.15f, 0.2f, 1f);
            Color border = new Color(0.1f, 0.1f, 0.1f, 1f);
            int margin = 6;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                // rounded square background
                float dx = Mathf.Max(Mathf.Abs(x - s / 2f) - (s / 2f - margin - 4), 0);
                float dy = Mathf.Max(Mathf.Abs(y - s / 2f) - (s / 2f - margin - 4), 0);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < 6f) c = bg;
                if (r > 4f && r < 6f) c = border;
                // red cross inside
                bool inH = x > s * 0.25f && x < s * 0.75f && y > s * 0.42f && y < s * 0.58f;
                bool inV = y > s * 0.25f && y < s * 0.75f && x > s * 0.42f && x < s * 0.58f;
                if ((inH || inV) && c.a > 0) c = cross;
                px[y * s + x] = c;
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Sprite BuildAmmoBullet()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            Color brass = new Color(0.9f, 0.75f, 0.25f, 1f);
            Color tip = new Color(0.65f, 0.65f, 0.7f, 1f);
            Color border = new Color(0.1f, 0.1f, 0.1f, 1f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                // vertical bullet shape: case bottom 0-0.6, tip 0.6-0.95, width ~0.35-0.65
                float nx = x / (float)s;
                float ny = y / (float)s;
                bool inCase = nx > 0.35f && nx < 0.65f && ny > 0.1f && ny < 0.6f;
                float tipT = Mathf.InverseLerp(0.6f, 0.95f, ny);
                float tipHalfW = Mathf.Lerp(0.15f, 0.02f, tipT);
                bool inTip = ny > 0.6f && ny < 0.95f && Mathf.Abs(nx - 0.5f) < tipHalfW;
                if (inCase) c = brass;
                if (inTip) c = tip;
                // rim line
                if (inCase && (ny > 0.55f && ny < 0.6f)) c *= 0.7f;
                // outline
                if (c.a > 0)
                {
                    if (nx < 0.36f || nx > 0.64f || ny < 0.11f) c = border;
                }
                px[y * s + x] = c;
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Sprite BuildArmorShield()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            Color plate = new Color(0.35f, 0.55f, 0.95f, 1f);
            Color light = new Color(0.65f, 0.8f, 1f, 1f);
            Color border = new Color(0.05f, 0.1f, 0.25f, 1f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float nx = (x - s / 2f) / (s * 0.4f);
                float ny = (y - s / 2f) / (s * 0.5f);
                // shield: flat top, rounded bottom
                float top = 0.85f;
                float r = nx * nx + (ny > 0 ? ny * ny * 0.9f : ny * ny * 0.2f);
                bool inside = r < 1f && ny < top;
                Color c = new Color(0, 0, 0, 0);
                if (inside)
                {
                    // gradient: top lighter
                    float t = Mathf.InverseLerp(-1f, 1f, ny);
                    c = Color.Lerp(light, plate, t);
                }
                // border
                if (inside && (r > 0.85f || Mathf.Abs(ny - top) < 0.05f)) c = border;
                // chevron detail
                if (inside && Mathf.Abs(nx) < 0.08f && ny < 0.2f) c = border;
                if (inside && Mathf.Abs(Mathf.Abs(nx) - ny * 0.5f) < 0.08f && ny > -0.3f && ny < 0.3f) c = border;
                px[y * s + x] = c;
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
