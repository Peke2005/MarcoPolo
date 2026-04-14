using System.Collections;
using UnityEngine;

namespace FrentePartido.Player
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class HitFlashController : MonoBehaviour
    {
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private int flashCount = 2;

        private SpriteRenderer sr;
        private Color originalColor;
        private Coroutine running;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            originalColor = sr.color;
        }

        public void Flash()
        {
            if (!gameObject.activeInHierarchy) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            for (int i = 0; i < flashCount; i++)
            {
                sr.color = flashColor;
                yield return new WaitForSeconds(flashDuration);
                sr.color = originalColor;
                yield return new WaitForSeconds(flashDuration);
            }
            sr.color = originalColor;
            running = null;
        }
    }
}
