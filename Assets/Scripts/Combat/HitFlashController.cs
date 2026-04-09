using System.Collections;
using UnityEngine;

namespace FrentePartido.Combat
{
    /// <summary>
    /// Visual feedback when entity takes damage.
    /// Uses MaterialPropertyBlock to avoid creating material instances.
    /// Client-side only, can be triggered by anyone.
    /// </summary>
    public class HitFlashController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;

        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _flashCoroutine;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();

            if (targetRenderer == null)
                Debug.LogWarning($"[HitFlashController] No SpriteRenderer on {gameObject.name}");
        }

        /// <summary>
        /// Trigger a hit flash. Safe to call multiple times; resets active flash.
        /// </summary>
        public void Flash()
        {
            if (targetRenderer == null) return;

            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        /// <summary>
        /// Flash with a custom color override.
        /// </summary>
        public void Flash(Color overrideColor)
        {
            if (targetRenderer == null) return;

            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            _flashCoroutine = StartCoroutine(FlashRoutine(overrideColor));
        }

        private IEnumerator FlashRoutine()
        {
            yield return FlashRoutine(flashColor);
        }

        private IEnumerator FlashRoutine(Color color)
        {
            // Get current property block state
            targetRenderer.GetPropertyBlock(_propertyBlock);
            Color originalColor = _propertyBlock.GetColor(ColorProperty);

            // Set flash color
            _propertyBlock.SetColor(ColorProperty, color);
            targetRenderer.SetPropertyBlock(_propertyBlock);

            yield return new WaitForSeconds(flashDuration);

            // Restore original color
            _propertyBlock.SetColor(ColorProperty, originalColor);
            targetRenderer.SetPropertyBlock(_propertyBlock);

            _flashCoroutine = null;
        }

        private void OnDisable()
        {
            // Clean up: restore original color if flash was active
            if (_flashCoroutine != null && targetRenderer != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;

                _propertyBlock.SetColor(ColorProperty, Color.white);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
