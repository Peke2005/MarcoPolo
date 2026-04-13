using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrentePartido.Core
{
    public static class GameplayVisualNormalizer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void NormalizeGameplaySceneVisuals()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.name.Contains("Game"))
            {
                return;
            }

            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name;

                if (objectName == "Floor")
                {
                    Apply(renderer, new Vector2(10f, 10f), false);
                    continue;
                }

                if (objectName == "SpawnA" || objectName == "SpawnB")
                {
                    Apply(renderer, new Vector2(0.8f, 0.8f), false);
                    continue;
                }

                if (objectName.StartsWith("Wall_") || objectName.StartsWith("Cover_"))
                {
                    Apply(renderer, Vector2.one, true);
                }
            }
        }

        private static void Apply(SpriteRenderer renderer, Vector2 size, bool syncCollider)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;

            if (!syncCollider)
            {
                return;
            }

            BoxCollider2D collider = renderer.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.size = size;
            }
        }
    }
}
