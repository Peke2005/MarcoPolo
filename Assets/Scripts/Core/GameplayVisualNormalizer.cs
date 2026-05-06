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
        private static Sprite _softCircleSprite;
        private static Sprite _lineSprite;

        private static readonly Color FloorTint  = new Color(0.78f, 0.94f, 0.58f, 1f);
        private static readonly Color WallTint   = new Color(0.92f, 0.73f, 0.48f, 1f);
        private static readonly Color CoverTint  = new Color(0.72f, 0.48f, 0.25f, 1f);
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
            BuildArenaIfMissing();
            ReskinArena();
            PopulateDecor();
            BuildArenaAccents();
            ConfigureCamera();
            BuildCrosshair();
            BuildMinimap();
            // No vignette: dark borders reduce gameplay readability.
            RemoveVignette();
            EnsurePlayerVisualWatcher();
            SkinHud();
            FixLayerMasks();
            Debug.Log("[VisualNormalizer] Applied v2: sudden-death cover, HUD bottom-right, cyan crosshair.");
        }

#if UNITY_EDITOR
        public static void RebuildActiveSceneForEditorPreview()
        {
            Build(SceneManager.GetActiveScene());
        }
#endif

        /// <summary>Re-create cover crates, barrels, grass for a fresh round.</summary>
        public static void RebuildDecor()
        {
            EnsureTextures();
            PopulateDecor();
        }

        /// <summary>Remove breakable runtime cover for sudden death; next round rebuilds it.</summary>
        public static int BreakSuddenDeathCover()
        {
            GameObject root = GameObject.Find("~ArenaDecor");
            if (root == null) return 0;

            var toDestroy = new List<GameObject>();
            foreach (Transform child in root.transform)
            {
                string n = child.gameObject.name;
                if (n.StartsWith("Decor_Crate") || n.StartsWith("Decor_Barrel"))
                    toDestroy.Add(child.gameObject);
            }

            foreach (GameObject go in toDestroy)
                Object.Destroy(go);

            return toDestroy.Count;
        }

        // ── Arena builder (runs only if scene has no Floor/Walls) ────
        private static void BuildArenaIfMissing()
        {
            if (GameObject.Find("Floor") != null) return;

            var root = new GameObject("~Arena");

            MakeArenaPiece(root, "Floor", Vector2.zero, Vector2.zero, addCollider: false);
            MakeArenaPiece(root, "Wall_Top",    new Vector2( 0f,  7.0f), new Vector2(24f, 0.6f), true);
            MakeArenaPiece(root, "Wall_Bottom", new Vector2( 0f, -7.0f), new Vector2(24f, 0.6f), true);
            MakeArenaPiece(root, "Wall_Left",  new Vector2(-11.7f, 0f), new Vector2(0.6f, 14f), true);
            MakeArenaPiece(root, "Wall_Right", new Vector2( 11.7f, 0f), new Vector2(0.6f, 14f), true);
            MakeArenaPiece(root, "SpawnA", new Vector2(-9f, 0f), new Vector2(1.8f, 1.8f), false);
            MakeArenaPiece(root, "SpawnB", new Vector2( 9f, 0f), new Vector2(1.8f, 1.8f), false);
        }

        private static void MakeArenaPiece(GameObject root, string name, Vector2 pos, Vector2 size, bool addCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.AddComponent<SpriteRenderer>();
            if (addCollider)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = size;
            }
        }

        // ── Vignette (screen-space dark edges) ───────────────────────
        private static void BuildVignette()
        {
            if (GameObject.Find("~Vignette") != null) return;

            var go = new GameObject("~Vignette", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;

            var img = new GameObject("Vig", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var image = img.GetComponent<Image>();
            image.sprite = MakeSprite(GenerateVignetteTexture(256), 32f);
            image.color = new Color(0f, 0f, 0f, 0.85f);
            image.raycastTarget = false;
        }

        private static void RemoveVignette()
        {
            var vignette = GameObject.Find("~Vignette");
            if (vignette != null) Object.Destroy(vignette);
        }

        private static Texture2D GenerateVignetteTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.55f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / r;
                float a = Mathf.Clamp01(Mathf.Pow(d, 2.4f));
                px[y * size + x] = new Color(0f, 0f, 0f, a);
            }
            tex.SetPixels(px);
            return tex;
        }

        // ── Ambient dust particles ───────────────────────────────────
        private static void BuildAmbientParticles()
        {
            if (GameObject.Find("~AmbientDust") != null) return;
            var go = new GameObject("~AmbientDust");
            go.transform.position = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3.5f;
            main.startSize = 0.08f;
            main.startSpeed = 0.15f;
            main.startColor = new Color(1f, 0.95f, 0.75f, 0.35f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 25f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 14f, 0.1f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sr = ps.GetComponent<ParticleSystemRenderer>();
            sr.sortingOrder = 20;
        }

        // ── Player visual enhancer watcher ───────────────────────────
        private static void EnsurePlayerVisualWatcher()
        {
            if (GameObject.Find("~PlayerVisuals") != null) return;
            var go = new GameObject("~PlayerVisuals");
            go.AddComponent<PlayerVisualWatcher>();
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
            if (_wallSprite  == null) _wallSprite  = LoadResourceSprite("RuntimeArt/Environment/planks", 70f) ?? MakeSprite(GenerateBrickTexture(128), 32f);
            if (_coverSprite == null) _coverSprite = MakeSprite(GenerateCrateTexture(64),  32f);
            if (_spawnSprite == null) _spawnSprite = MakeSprite(GenerateRadialTexture(128), 32f);
            if (_crateSprite == null) _crateSprite = _coverSprite;
            if (_barrelSprite== null) _barrelSprite= MakeSprite(GenerateBarrelTexture(64),  64f);
            if (_grassSprite == null) _grassSprite = MakeSprite(GenerateGrassTuft(32),      64f);
            if (_softCircleSprite == null) _softCircleSprite = MakeSprite(GenerateRadialTexture(128), 64f);
            if (_lineSprite == null) _lineSprite = MakeSprite(GenerateSolidTexture(8, 8, Color.white), 8f);
        }

        private static Sprite LoadResourceSprite(string resourcePath, float ppu)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
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
                Color c = new Color(0.34f + n + g * 0.3f, 0.48f + n + g, 0.25f + n * 0.35f, 1f);
                float scar = Mathf.PerlinNoise(x * 0.06f + 31f, y * 0.11f + 7f);
                if (scar > 0.78f) c = Color.Lerp(c, new Color(0.34f, 0.27f, 0.18f, 1f), 0.18f);
                if (y % 32 == 0) c *= 0.94f;
                if (x % 32 == 0) c *= 0.97f;
                pixels[y * size + x] = c;
            }
            Random.state = old;
            tex.SetPixels(pixels);
            return tex;
        }

        private static Texture2D GenerateSolidTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
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
                    r.transform.localScale = Vector3.one;
                }
                else if (n == "SpawnA")
                {
                    r.sprite = _spawnSprite;
                    r.drawMode = SpriteDrawMode.Sliced;
                    r.size = new Vector2(1.8f, 1.8f);
                    r.color = SpawnAColor;
                    r.sortingOrder = -50;
                    r.transform.localScale = Vector3.one;
                }
                else if (n == "SpawnB")
                {
                    r.sprite = _spawnSprite;
                    r.drawMode = SpriteDrawMode.Sliced;
                    r.size = new Vector2(1.8f, 1.8f);
                    r.color = SpawnBColor;
                    r.sortingOrder = -50;
                    r.transform.localScale = Vector3.one;
                }
                else if (n.StartsWith("Wall_"))
                {
                    r.sprite = _wallSprite;
                    r.drawMode = SpriteDrawMode.Tiled;
                    r.tileMode = SpriteTileMode.Continuous;
                    r.size = WallSize(n);
                    r.color = WallTint;
                    r.sortingOrder = 5;
                    r.transform.localScale = Vector3.one;
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
                    r.transform.localScale = Vector3.one;
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
            AddCrate(root, new Vector2(-6.9f, 0f),   0.9f, 2.4f);
            AddCrate(root, new Vector2( 6.9f, 0f),   0.9f, 2.4f);

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

        private static void BuildArenaAccents()
        {
            GameObject root = GameObject.Find("~ArenaAccents");
            if (root != null) Object.Destroy(root);
            root = new GameObject("~ArenaAccents");

            AddBackdrop(root);
            AddGlow(root, "BlueBaseGlow", new Vector2(-9f, 0f), new Vector2(3.4f, 3.4f), new Color(0.15f, 0.55f, 1f, 0.28f), -70);
            AddGlow(root, "RedBaseGlow", new Vector2(9f, 0f), new Vector2(3.4f, 3.4f), new Color(1f, 0.2f, 0.12f, 0.28f), -70);
            AddGlow(root, "BeaconHalo", Vector2.zero, new Vector2(4.2f, 4.2f), new Color(1f, 0.78f, 0.18f, 0.22f), -65);

            AddLine(root, "Midline", new Vector2(0f, 0f), new Vector2(0.08f, 11.8f), new Color(1f, 0.86f, 0.45f, 0.18f), -60);
            AddLine(root, "TopLane", new Vector2(0f, 3.15f), new Vector2(17f, 0.06f), new Color(0f, 0f, 0f, 0.16f), -61);
            AddLine(root, "BottomLane", new Vector2(0f, -3.15f), new Vector2(17f, 0.06f), new Color(0f, 0f, 0f, 0.16f), -61);

            for (int i = -5; i <= 5; i++)
            {
                AddLine(root, "Dash_" + i, new Vector2(i * 1.6f, 0f), new Vector2(0.55f, 0.06f), new Color(1f, 0.95f, 0.7f, 0.35f), -59);
            }
        }

        private static void AddBackdrop(GameObject root)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(0f, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _floorSprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(44f, 28f);
            sr.color = new Color(0.30f, 0.43f, 0.22f, 1f);
            sr.sortingOrder = -130;
        }

        private static void AddGlow(GameObject root, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _softCircleSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
        }

        private static void AddLine(GameObject root, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _lineSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
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
            cam.orthographicSize = 5.1f;
            cam.backgroundColor = new Color(0.12f, 0.18f, 0.11f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            if (cam.GetComponent<CameraFollow2D>() == null)
                cam.gameObject.AddComponent<CameraFollow2D>();
        }

        private static void SkinHud()
        {
            var hud = GameObject.Find("HUDCanvas");
            if (hud == null) return;

            // Anchor timer + score to top-left so they stay there at any window size.
            AnchorTopLeft(hud, "RoundTimerText", new Vector2(20f, -20f), TMPro.TextAlignmentOptions.TopLeft);
            AnchorTopLeft(hud, "RoundScoreText", new Vector2(20f, -70f), TMPro.TextAlignmentOptions.TopLeft);
            AnchorBottomLeft(hud, "HealthText", new Vector2(30f, 66f), new Vector2(90f, 28f), TMPro.TextAlignmentOptions.BottomLeft);
            AnchorBottomLeftImage(hud, "HealthBar", new Vector2(30f, 48f), new Vector2(220f, 18f));
            AnchorBottomLeft(hud, "ArmorText", new Vector2(30f, 22f), new Vector2(90f, 24f), TMPro.TextAlignmentOptions.BottomLeft);
            AnchorBottomLeftImage(hud, "ArmorBar", new Vector2(30f, 18f), new Vector2(220f, 10f));
            AnchorBottomRight(hud, "AmmoText", new Vector2(-30f, 46f), new Vector2(110f, 34f), TMPro.TextAlignmentOptions.BottomRight);
            AnchorBottomRightImage(hud, "ReloadBar", new Vector2(-30f, 24f), new Vector2(120f, 10f));
            AnchorBottomRightImage(hud, "GrenadeIcon", new Vector2(-180f, 42f), new Vector2(34f, 34f));
            AnchorBottomRightImage(hud, "AbilityCooldown", new Vector2(-232f, 42f), new Vector2(42f, 42f));

            // Anchor the dark _BG frames behind the bars so the original middle-screen
            // ghosts are gone. Sized slightly bigger than the bar for a clean border.
            AnchorBottomLeftImage(hud, "HealthBar_BG", new Vector2(28f, 46f), new Vector2(224f, 22f));
            AnchorBottomLeftImage(hud, "ArmorBar_BG",  new Vector2(28f, 16f), new Vector2(224f, 14f));
            AnchorBottomRightImage(hud, "ReloadBar_BG", new Vector2(-28f, 22f), new Vector2(124f, 14f));

            // Force the bars into Filled mode horizontal-left every load. Without this
            // a saved scene with Image.Type.Simple would render a full bar regardless
            // of fillAmount, so HP/armor changes never seemed to apply visually.
            ConfigureFillBar(FindDeepChild(hud.transform, "HealthBar")?.GetComponent<Image>(), 1f);
            ConfigureFillBar(FindDeepChild(hud.transform, "ArmorBar")?.GetComponent<Image>(), 0f);
            ConfigureFillBar(FindDeepChild(hud.transform, "ReloadBar")?.GetComponent<Image>(), 0f);
            ConfigureFillBar(FindDeepChild(hud.transform, "BeaconCaptureBar")?.GetComponent<Image>(), 0f);
            var armorTxt = FindDeepChild(hud.transform, "ArmorText")?.GetComponent<TMPro.TMP_Text>();
            if (armorTxt != null) armorTxt.text = "0";

            foreach (var image in hud.GetComponentsInChildren<Image>(true))
            {
                string n = image.gameObject.name;
                if (n.EndsWith("_BG"))
                {
                    image.color = new Color(0.015f, 0.02f, 0.025f, 0.78f);
                }
                else if (n == "HealthBar")
                {
                    image.color = new Color(0.95f, 0.18f, 0.12f, 0.95f);
                }
                else if (n == "ArmorBar")
                {
                    image.color = new Color(0.2f, 0.65f, 1f, 0.95f);
                }
                else if (n == "ReloadBar" || n == "BeaconCaptureBar")
                {
                    image.color = new Color(1f, 0.74f, 0.18f, 0.95f);
                }
                else if (n == "GrenadeIcon")
                {
                    image.color = new Color(1f, 0.86f, 0.35f, 0.95f);
                }
            }
        }

        private static Sprite _solidWhiteUISprite;

        private static Sprite GetSolidWhiteUISprite()
        {
            if (_solidWhiteUISprite != null) return _solidWhiteUISprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            _solidWhiteUISprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _solidWhiteUISprite;
        }

        private static void ConfigureFillBar(Image img, float initialFill)
        {
            if (img == null) return;
            // CRITICAL: an Image with sprite=null renders as a flat rectangle and
            // IGNORES fillAmount in standalone builds. Assign a runtime-generated
            // solid sprite so Image.Type.Filled actually clips.
            img.sprite = GetSolidWhiteUISprite();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = initialFill;
            img.preserveAspect = false;
        }

        private static void AnchorTopLeft(GameObject hud, string childName, Vector2 anchoredPos, TMPro.TextAlignmentOptions align)
        {
            Transform t = FindDeepChild(hud.transform, childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;

            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) tmp.alignment = align;
        }

        private static void AnchorBottomRight(GameObject hud, string childName, Vector2 anchoredPos, Vector2 size, TMPro.TextAlignmentOptions align)
        {
            Transform t = FindDeepChild(hud.transform, childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            if (t.parent != hud.transform) t.SetParent(hud.transform, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.alignment = align;
                tmp.fontSize = Mathf.Max(tmp.fontSize, 26f);
                tmp.color = new Color(0.94f, 1f, 0.96f, 0.98f);
            }
        }

        private static void AnchorBottomLeft(GameObject hud, string childName, Vector2 anchoredPos, Vector2 size, TMPro.TextAlignmentOptions align)
        {
            Transform t = FindDeepChild(hud.transform, childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            if (t.parent != hud.transform) t.SetParent(hud.transform, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.alignment = align;
                tmp.fontSize = Mathf.Max(tmp.fontSize, 20f);
                tmp.color = new Color(0.94f, 1f, 0.96f, 0.98f);
            }
        }

        private static void AnchorBottomRightImage(GameObject hud, string childName, Vector2 anchoredPos, Vector2 size)
        {
            Transform t = FindDeepChild(hud.transform, childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            if (t.parent != hud.transform) t.SetParent(hud.transform, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static void AnchorBottomLeftImage(GameObject hud, string childName, Vector2 anchoredPos, Vector2 size)
        {
            Transform t = FindDeepChild(hud.transform, childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            if (t.parent != hud.transform) t.SetParent(hud.transform, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ── Crosshair UI ─────────────────────────────────────────────
        private static void BuildCrosshair()
        {
            GameObject go = GameObject.Find("~Crosshair");
            if (go == null)
            {
                go = new GameObject("~Crosshair", typeof(Canvas), typeof(CanvasScaler));
            }
            else
            {
                foreach (var image in go.GetComponentsInChildren<Image>(true))
                    image.enabled = false;
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                    Object.Destroy(go.transform.GetChild(i).gameObject);
            }

            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var outline = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outline.transform.SetParent(go.transform, false);
            var outlineRt = outline.GetComponent<RectTransform>();
            outlineRt.sizeDelta = new Vector2(24f, 24f);
            var outlineImage = outline.GetComponent<Image>();
            outlineImage.sprite = _spawnSprite;
            outlineImage.color = new Color(0f, 0f, 0f, 0.72f);
            outlineImage.raycastTarget = false;

            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(go.transform, false);
            var dotRt = dot.GetComponent<RectTransform>();
            dotRt.sizeDelta = new Vector2(14f, 14f);
            var dotImage = dot.GetComponent<Image>();
            dotImage.sprite = _spawnSprite;
            dotImage.color = new Color(0f, 1f, 1f, 1f);
            dotImage.raycastTarget = false;

            if (go.GetComponent<CrosshairFollow>() == null)
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
        private RectTransform[] _parts;

        private void Awake()
        {
            _parts = GetComponentsInChildren<RectTransform>(true);
        }

        private void Update()
        {
            if (_parts == null || _parts.Length == 0) return;
            Vector2 pos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null && _parts[i].transform != transform)
                    _parts[i].position = pos;
            }
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
        }
    }

    /// <summary>Runtime watcher that styles player NetworkObjects with circle body,
    /// outline and drop shadow. Also pulses the beacon.</summary>
    public class PlayerVisualWatcher : MonoBehaviour
    {
        private static Sprite _circleSprite;
        private static Sprite _ringSprite;
        private readonly HashSet<int> _styled = new HashSet<int>();
        private float _pulseT;

        private void Awake()
        {
            if (_circleSprite == null) _circleSprite = MakeCircle(128, 0f);
            if (_ringSprite == null)   _ringSprite   = MakeCircle(128, 0.78f);
        }

        private void LateUpdate()
        {
            _pulseT += Time.deltaTime;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null)
            {
                foreach (var kv in nm.SpawnManager.SpawnedObjects)
                {
                    var no = kv.Value;
                    if (no == null || !no.IsPlayerObject) continue;
                    StylePlayer(no.gameObject, no.OwnerClientId == nm.LocalClientId);
                }
            }
            else
            {
                foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                {
                    if (rb.gameObject.name.Contains("Player")) StylePlayer(rb.gameObject, false);
                }
            }

            PulseBeacon();
        }

        private void StylePlayer(GameObject player, bool isLocal)
        {
            int id = player.GetInstanceID();
            if (_styled.Contains(id) && player.transform.Find("~VRing") != null) return;
            _styled.Add(id);

            bool isBlue = DetectFactionBlue(player);
            Color body = isBlue ? new Color(0.28f, 0.55f, 1f, 1f) : new Color(1f, 0.35f, 0.30f, 1f);

            AddSprite(player.transform, "~VShadow", _circleSprite,
                new Color(0f, 0f, 0f, 0.42f), new Vector3(0.12f, -0.16f, 0f), 1.1f, 2);
            AddSprite(player.transform, "~VRing", _ringSprite,
                isLocal ? new Color(1f, 0.92f, 0.22f, 0.95f) : new Color(body.r, body.g, body.b, 0.45f),
                Vector3.zero, 0.95f, 8);

        }

        private static bool DetectFactionBlue(GameObject player)
        {
            // Left side of arena = Blue, right side = Red. Matches PlayerSpawnManager slot logic.
            return player.transform.position.x <= 0f;
        }

        private static void AddSprite(Transform parent, string name, Sprite sprite,
            Color color, Vector3 localPos, float scale, int order)
        {
            Transform existing = parent.Find(name);
            if (existing != null) { existing.localScale = Vector3.one * scale; return; }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }

        private static Sprite MakeCircle(int size, float innerRadiusNorm)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                if (d > r) { px[y * size + x] = new Color(0, 0, 0, 0); continue; }
                if (innerRadiusNorm > 0f && d < r * innerRadiusNorm)
                    { px[y * size + x] = new Color(0, 0, 0, 0); continue; }
                float edge = Mathf.SmoothStep(1f, 0f, (d - (r - 2f)) / 2f);
                px[y * size + x] = new Color(1f, 1f, 1f, edge);
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        private void PulseBeacon()
        {
            var beacon = GameObject.Find("Beacon");
            if (beacon == null) return;
            float s = 1f + Mathf.Sin(_pulseT * 3f) * 0.12f;
            var renderers = beacon.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in renderers)
            {
                if (sr.transform == beacon.transform)
                {
                    sr.transform.localScale = new Vector3(s, s, 1f);
                    sr.color = Color.Lerp(new Color(1f, 0.85f, 0.25f, 1f),
                                          new Color(1f, 1f, 0.55f, 1f),
                                          (Mathf.Sin(_pulseT * 3f) + 1f) * 0.5f);
                }
            }
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
