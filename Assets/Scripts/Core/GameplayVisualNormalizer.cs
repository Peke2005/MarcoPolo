using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FrentePartido.Core
{
    /// <summary>
    /// Procedural scene dresser: generates textures, reskins arena sprites,
    /// adds decorative cover, wires camera follow and crosshair at runtime.
    /// Runs on every Game scene load without needing editor setup.
    /// </summary>
    public static class GameplayVisualNormalizer
    {
        private static Sprite _floorSprite;
        private static Sprite _wallSprite;
        private static Sprite _coverSprite;
        private static Sprite _spawnSprite;
        private static Sprite _crateSprite;
        private static Sprite _barrelSprite;
        private static Sprite _grassSprite;

        private static readonly Color FloorTint  = new Color(0.55f, 0.50f, 0.40f, 1f);
        private static readonly Color WallTint   = new Color(0.72f, 0.68f, 0.55f, 1f);
        private static readonly Color CoverTint  = new Color(0.58f, 0.42f, 0.26f, 1f);
        private static readonly Color SpawnAColor = new Color(0.30f, 0.60f, 1.00f, 0.95f);
        private static readonly Color SpawnBColor = new Color(1.00f, 0.35f, 0.35f, 0.95f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Build(SceneManager.GetActiveScene());
        }

        private static void OnLoaded(Scene s, LoadSceneMode m) => Build(s);

        private static void Build(Scene scene)
        {
            if (!scene.IsValid() || !scene.name.Contains("Game")) return;
            EnsureTextures();
            ReskinArena();
            PopulateDecor();
            ConfigureCamera();
            BuildCrosshair();
            BuildMinimap();
            FixLayerMasks();
        }

        // ── Minimap ──────────────────────────────────────────────────
        private static void BuildMinimap()
        {
            if (GameObject.Find("~Minimap") != null) return;

            var rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT";
            rt.Create();

            var camGo = new GameObject("~MinimapCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
            cam.targetTexture = rt;
            cam.depth = -5;
            cam.transform.position = new Vector3(0f, 0f, -20f);
            camGo.AddComponent<MinimapFollow>();

            var canvasGo = new GameObject("~Minimap", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(canvasGo.transform, false);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = new Vector2(1f, 1f);
            frt.pivot = new Vector2(1f, 1f);
            frt.anchoredPosition = new Vector2(-12f, -12f);
            frt.sizeDelta = new Vector2(180f, 180f);
            var frameImg = frame.GetComponent<Image>();
            frameImg.color = new Color(0f, 0f, 0f, 0.55f);
            frameImg.raycastTarget = false;

            var img = new GameObject("MapView", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(frame.transform, false);
            var irt = img.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(4f, 4f);
            irt.offsetMax = new Vector2(-4f, -4f);
            var raw = img.GetComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
        }

        // ── Texture generation ────────────────────────────────────────
        private static void EnsureTextures()
        {
            if (_floorSprite == null) _floorSprite = MakeSprite(GenerateFloorTexture(128), 32f);
            if (_wallSprite  == null) _wallSprite  = MakeSprite(GenerateBrickTexture(128), 32f);
            if (_coverSprite == null) _coverSprite = MakeSprite(GenerateCrateTexture(64),  32f);
            if (_spawnSprite == null) _spawnSprite = MakeSprite(GenerateRadialTexture(128), 32f);
            if (_crateSprite == null) _crateSprite = _coverSprite;
            if (_barrelSprite== null) _barrelSprite= MakeSprite(GenerateBarrelTexture(64),  32f);
            if (_grassSprite == null) _grassSprite = MakeSprite(GenerateGrassTuft(32),      32f);
        }

        private static Sprite MakeSprite(Texture2D tex, float ppu)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        private static Texture2D GenerateFloorTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Random.State old = Random.state; Random.InitState(1337);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.15f;
                float g = Mathf.PerlinNoise(x * 0.3f + 10, y * 0.3f + 10) * 0.08f;
                Color c = new Color(0.42f + n + g, 0.36f + n + g * 0.7f, 0.27f + n * 0.5f, 1f);
                // crack-like dark streaks
                if ((x + y * 2) % 37 == 0) c *= 0.78f;
                // grid lines (subtle)
                if (x % 32 == 0 || y % 32 == 0) c *= 0.88f;
                pixels[y * size + x] = c;
            }
            Random.state = old;
            tex.SetPixels(pixels);
            return tex;
        }

        private static Texture2D GenerateBrickTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            int brickH = 16, brickW = 32, mortar = 2;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / brickH;
                int offset = (row % 2 == 0) ? 0 : brickW / 2;
                int xo = (x + offset) % brickW;
                int yo = y % brickH;
                bool isMortar = yo < mortar || xo < mortar;
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.2f;
                Color c = isMortar
                    ? new Color(0.30f, 0.28f, 0.24f, 1f)
                    : new Color(0.78f + n * 0.15f, 0.68f + n * 0.1f, 0.55f + n * 0.1f, 1f);
                // top highlight
                if (!isMortar && yo < 4) c *= 1.1f;
                // bottom shadow
                if (!isMortar && yo > brickH - 4) c *= 0.82f;
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            return tex;
        }

        private static Texture2D GenerateCrateTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            int border = 3;
            int plank = size / 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < border || y < border || x >= size - border || y >= size - border;
                bool isPlank  = (y % plank) < 2;
                bool isDiag   = (Mathf.Abs((x - size / 2f) + (y - size / 2f)) < 2) || (Mathf.Abs((x - size / 2f) - (y - size / 2f)) < 2);
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.15f;
                Color wood = new Color(0.55f + n, 0.38f + n * 0.8f, 0.22f + n * 0.5f, 1f);
                Color dark = new Color(0.28f, 0.18f, 0.10f, 1f);
                Color c = isBorder || isPlank || isDiag ? dark : wood;
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            return tex;
        }

        private static Texture2D GenerateBarrelTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float r = size * 0.48f;
            Vector2 c = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                if (d > r) { pixels[y * size + x] = new Color(0, 0, 0, 0); continue; }
                float t = 1f - d / r;
                float shade = 0.5f + 0.5f * Mathf.Pow(t, 0.7f);
                Color col = new Color(0.45f * shade, 0.18f * shade, 0.12f * shade, 1f);
                int ring = y % (size / 4);
                if (ring < 3) col *= 0.55f;
                pixels[y * size + x] = col;
            }
            tex.SetPixels(pixels);
            return tex;
        }

        private static Texture2D GenerateRadialTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float t = Mathf.Clamp01(1f - d / r);
                float ring = Mathf.Abs(d / r - 0.8f) < 0.04f ? 1f : 0f;
                float a = Mathf.Pow(t, 1.8f) * 0.9f + ring * 0.5f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(pixels);
            return tex;
        }

        private static Texture2D GenerateGrassTuft(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0, 0, 0, 0);
            Random.State old = Random.state; Random.InitState(99);
            for (int i = 0; i < 12; i++)
            {
                int x = Random.Range(4, size - 4);
                int y = Random.Range(4, size - 4);
                int h = Random.Range(3, 8);
                for (int k = 0; k < h; k++)
                {
                    int px = x, py = y + k;
                    if (py >= size) break;
                    Color g = new Color(0.25f + Random.value * 0.2f, 0.45f + Random.value * 0.2f, 0.18f, 1f);
                    pixels[py * size + px] = g;
                }
            }
            Random.state = old;
            tex.SetPixels(pixels);
            return tex;
        }

        // ── Arena reskin ──────────────────────────────────────────────
        private static void ReskinArena()
        {
            foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string n = r.gameObject.name;

                if (n == "Floor")
                {
                    r.sprite = _floorSprite;
                    r.drawMode = SpriteDrawMode.Tiled;
                    r.tileMode = SpriteTileMode.Continuous;
                    r.size = new Vector2(24f, 14f);
                    r.color = FloorTint;
                    r.sortingOrder = -100;
                }
                else if (n == "SpawnA")
                {
                    r.sprite = _spawnSprite;
                    r.drawMode = SpriteDrawMode.Sliced;
                    r.size = new Vector2(1.8f, 1.8f);
                    r.color = SpawnAColor;
                    r.sortingOrder = -50;
                }
                else if (n == "SpawnB")
                {
                    r.sprite = _spawnSprite;
                    r.drawMode = SpriteDrawMode.Sliced;
                    r.size = new Vector2(1.8f, 1.8f);
                    r.color = SpawnBColor;
                    r.sortingOrder = -50;
                }
                else if (n.StartsWith("Wall_"))
                {
                    r.sprite = _wallSprite;
                    r.drawMode = SpriteDrawMode.Tiled;
                    r.tileMode = SpriteTileMode.Continuous;
                    r.size = WallSize(n);
                    r.color = WallTint;
                    r.sortingOrder = 5;
                    var box = r.GetComponent<BoxCollider2D>();
                    if (box != null) box.size = WallSize(n);
                }
                else if (n.StartsWith("Cover_"))
                {
                    r.sprite = _coverSprite;
                    r.drawMode = SpriteDrawMode.Tiled;
                    r.size = new Vector2(1.6f, 0.6f);
                    r.color = CoverTint;
                    r.sortingOrder = 4;
                    var box = r.GetComponent<BoxCollider2D>();
                    if (box != null) box.size = new Vector2(1.6f, 0.6f);
                }
            }
        }

        private static Vector2 WallSize(string name)
        {
            switch (name)
            {
                case "Wall_Top":
                case "Wall_Bottom": return new Vector2(24f, 0.6f);
                case "Wall_Left":
                case "Wall_Right":  return new Vector2(0.6f, 14f);
                default:            return Vector2.one;
            }
        }

        // ── Decor props ───────────────────────────────────────────────
        private static void PopulateDecor()
        {
            GameObject root = GameObject.Find("~ArenaDecor");
            if (root != null) Object.Destroy(root);
            root = new GameObject("~ArenaDecor");

            // Crate/cover layout: symmetric 1v1 arena
            AddCrate(root, new Vector2(-4.5f, -2f), 1.4f, 1.0f);
            AddCrate(root, new Vector2( 4.5f,  2f), 1.4f, 1.0f);
            AddCrate(root, new Vector2(-4.5f,  2f), 1.0f, 1.0f);
            AddCrate(root, new Vector2( 4.5f, -2f), 1.0f, 1.0f);
            AddCrate(root, new Vector2( 0f,   3.2f), 2.2f, 0.8f);
            AddCrate(root, new Vector2( 0f,  -3.2f), 2.2f, 0.8f);
            AddCrate(root, new Vector2(-8f,   0f),   0.9f, 2.4f);
            AddCrate(root, new Vector2( 8f,   0f),   0.9f, 2.4f);

            AddBarrel(root, new Vector2(-2f,  0.7f));
            AddBarrel(root, new Vector2( 2f, -0.7f));

            // Grass tufts (no collider, decorative)
            Random.State old = Random.state; Random.InitState(42);
            for (int i = 0; i < 30; i++)
            {
                Vector2 p = new Vector2(Random.Range(-10.5f, 10.5f), Random.Range(-5.5f, 5.5f));
                AddGrass(root, p);
            }
            Random.state = old;
        }

        private static void AddCrate(GameObject root, Vector2 pos, float w, float h)
        {
            var go = new GameObject("Decor_Crate");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _crateSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w, h);
            sr.color = CoverTint;
            sr.sortingOrder = 3;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(w, h);
            AddDropShadow(go, new Vector2(w, h) * 0.92f);
        }

        private static void AddBarrel(GameObject root, Vector2 pos)
        {
            var go = new GameObject("Decor_Barrel");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _barrelSprite;
            sr.sortingOrder = 3;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            AddDropShadow(go, new Vector2(0.95f, 0.95f));
        }

        private static void AddGrass(GameObject root, Vector2 pos)
        {
            var go = new GameObject("Decor_Grass");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _grassSprite;
            sr.sortingOrder = 1;
            sr.color = new Color(0.7f, 0.9f, 0.5f, 0.85f);
        }

        private static void AddDropShadow(GameObject parent, Vector2 size)
        {
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(parent.transform, false);
            sh.transform.localPosition = new Vector3(0.1f, -0.12f, 0f);
            var sr = sh.AddComponent<SpriteRenderer>();
            sr.sprite = _spawnSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size * 1.1f;
            sr.color = new Color(0f, 0f, 0f, 0.35f);
            sr.sortingOrder = 2;
        }

        // ── Camera ───────────────────────────────────────────────────
        private static void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = 4.5f;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            if (cam.GetComponent<CameraFollow2D>() == null)
                cam.gameObject.AddComponent<CameraFollow2D>();
        }

        // ── Crosshair UI ─────────────────────────────────────────────
        private static void BuildCrosshair()
        {
            if (GameObject.Find("~Crosshair") != null) return;
            var go = new GameObject("~Crosshair", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var img = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var rt = img.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);
            var image = img.GetComponent<Image>();
            image.sprite = _spawnSprite;
            image.color = new Color(1f, 0.9f, 0.3f, 0.85f);
            image.raycastTarget = false;

            go.AddComponent<CrosshairFollow>();
            Cursor.visible = false;
        }

        // ── Layer masks (so bullets/obstacles collide) ───────────────
        private static void FixLayerMasks()
        {
            // Ensure walls & cover & decor sit on "Default" so ray/phys hits work out-of-box.
            foreach (var r in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                string n = r.gameObject.name;
                if (n.StartsWith("Wall_") || n.StartsWith("Cover_") || n.StartsWith("Decor_"))
                    r.gameObject.layer = 0;
            }
        }
    }

    /// <summary>Smoothly follows the local player's transform.</summary>
    public class CameraFollow2D : MonoBehaviour
    {
        private Transform _target;
        private float _searchTimer;

        private void LateUpdate()
        {
            if (_target == null)
            {
                _searchTimer -= Time.deltaTime;
                if (_searchTimer > 0f) return;
                _searchTimer = 0.25f;
                _target = FindLocalPlayer();
                if (_target == null) return;
            }

            Vector3 desired = new Vector3(_target.position.x, _target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, 10f * Time.deltaTime);
        }

        private static Transform FindLocalPlayer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsClient && nm.SpawnManager != null)
            {
                var obj = nm.SpawnManager.GetLocalPlayerObject();
                if (obj != null) return obj.transform;
            }
            foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
            {
                if (rb.gameObject.name == "Player") return rb.transform;
            }
            return null;
        }
    }

    /// <summary>Moves the crosshair Image to the mouse position each frame.</summary>
    public class CrosshairFollow : MonoBehaviour
    {
        private RectTransform _dot;

        private void Awake()
        {
            _dot = transform.GetChild(0) as RectTransform;
        }

        private void Update()
        {
            if (_dot == null) return;
            Vector2 pos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            _dot.position = pos;
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
        }
    }

    /// <summary>Minimap camera that tracks the local player with a bird's-eye view.</summary>
    public class MinimapFollow : MonoBehaviour
    {
        private Transform _target;
        private float _searchTimer;

        private void LateUpdate()
        {
            if (_target == null)
            {
                _searchTimer -= Time.deltaTime;
                if (_searchTimer > 0f) return;
                _searchTimer = 0.25f;
                _target = FindLocalPlayer();
            }

            if (_target != null)
            {
                transform.position = new Vector3(_target.position.x, _target.position.y, -20f);
            }
        }

        private static Transform FindLocalPlayer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsClient && nm.SpawnManager != null)
            {
                var obj = nm.SpawnManager.GetLocalPlayerObject();
                if (obj != null) return obj.transform;
            }
            return null;
        }
    }
}
