#if UNITY_EDITOR
using System.IO;
using FrentePartido.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FrentePartido.Editor
{
    public static class VisualSmokeTest
    {
        public static void CaptureGameScene()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/04_Game.unity");
                GameplayVisualNormalizer.RebuildActiveSceneForEditorPreview();
                AddPreviewPlayer("PreviewBlue", new Vector2(-2.3f, -0.4f), "RuntimeArt/Characters/manBlue_stand", new Color(0.2f, 0.55f, 1f, 0.8f));
                AddPreviewPlayer("PreviewRed", new Vector2(2.3f, 0.4f), "RuntimeArt/Characters/manBrown_stand", new Color(1f, 0.3f, 0.2f, 0.8f));

                var camera = Camera.main;
                if (camera == null) throw new System.InvalidOperationException("Main Camera missing.");
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 6.2f;

                Directory.CreateDirectory("Temp");
                string outputPath = Path.GetFullPath("Temp/visual-smoke-04-game.png");
                RenderCamera(camera, outputPath, 1280, 720);
                Debug.Log("[VisualSmokeTest] Captured " + outputPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[VisualSmokeTest] " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void AddPreviewPlayer(string name, Vector2 position, string bodyResource, Color ringColor)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(position.x, position.y, 0f);

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0.1f, -0.16f, 0f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = MakeCircle(96, false);
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.42f);
            shadowRenderer.sortingOrder = 8;

            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = MakeCircle(128, true);
            ringRenderer.color = ringColor;
            ringRenderer.sortingOrder = 9;
            ring.transform.localScale = Vector3.one * 0.95f;

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = LoadSprite(bodyResource, 70f);
            bodyRenderer.sortingOrder = 10;

            var gun = new GameObject("Weapon");
            gun.transform.SetParent(root.transform, false);
            gun.transform.localPosition = new Vector3(0.38f, -0.02f, 0f);
            var gunRenderer = gun.AddComponent<SpriteRenderer>();
            gunRenderer.sprite = LoadSprite("RuntimeArt/Characters/weapon_gun", 48f);
            gunRenderer.color = new Color(0.9f, 0.95f, 1f, 1f);
            gunRenderer.sortingOrder = 11;
        }

        private static Sprite LoadSprite(string resourcePath, float pixelsPerUnit)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) throw new FileNotFoundException("Resource missing: " + resourcePath);
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeCircle(int size, bool ring)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                bool inside = d <= radius;
                bool hole = ring && d < radius * 0.76f;
                pixels[y * size + x] = inside && !hole ? Color.white : new Color(0f, 0f, 0f, 0f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        private static void RenderCamera(Camera camera, string outputPath, int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());

            camera.targetTexture = previous;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
        }
    }
}
#endif
