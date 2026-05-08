using System.Collections;
using UnityEngine;
using FrentePartido.Player;

namespace FrentePartido.Abilities
{
    /// <summary>
    /// Executes a dash: rapidly moves the player in a direction.
    /// Pure MonoBehaviour -- owner/server calls Execute(), coroutine handles movement.
    /// Collision uses PlayerMotor2D's same blocker resolver as normal movement.
    /// </summary>
    public class DashAbility : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Color dashTintColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private int ghostCount = 3;

        private SpriteRenderer _spriteRenderer;
        private Coroutine _dashCoroutine;
        private bool _isDashing;

        public bool IsDashing => _isDashing;

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Execute dash. Called on server by AbilityController.
        /// </summary>
        /// <param name="motor">Player motor to move.</param>
        /// <param name="direction">Normalized dash direction.</param>
        /// <param name="distance">Total dash distance (AbilityDefinition.value1).</param>
        /// <param name="speed">Dash speed (AbilityDefinition.value2).</param>
        public void Execute(PlayerMotor2D motor, Vector2 direction, float distance, float speed)
        {
            if (_isDashing)
            {
                Debug.Log("[DashAbility] Already dashing, ignoring.");
                return;
            }

            if (motor == null)
            {
                Debug.LogWarning("[DashAbility] PlayerMotor2D is null.");
                return;
            }

            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;

            direction = direction.normalized;

            if (speed <= 0f) speed = 15f;
            if (distance <= 0f) distance = 4f;

            if (_dashCoroutine != null)
                StopCoroutine(_dashCoroutine);

            _dashCoroutine = StartCoroutine(DashRoutine(motor, direction, distance, speed));
        }

        private IEnumerator DashRoutine(PlayerMotor2D motor, Vector2 direction, float distance, float speed)
        {
            _isDashing = true;

            // Disable normal movement during dash
            motor.SetMovementEnabled(false);

            float duration = distance / speed;
            float elapsed = 0f;
            float movedTotal = 0f;
            int ghostsSpawned = 0;
            float ghostInterval = duration / Mathf.Max(1, ghostCount);

            while (elapsed < duration && movedTotal < distance)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                float remaining = distance - movedTotal;
                Vector2 requestedStep = direction * Mathf.Min(speed * dt, remaining);
                Vector2 appliedStep = motor.ForceMoveDelta(requestedStep);
                if (appliedStep.sqrMagnitude < 0.0001f)
                {
                    Debug.Log("[DashAbility] Dash blocked.");
                    break;
                }
                movedTotal += appliedStep.magnitude;

                // Spawn ghost trail at intervals
                if (_spriteRenderer != null && ghostsSpawned < ghostCount)
                {
                    if (elapsed >= ghostInterval * (ghostsSpawned + 1))
                    {
                        SpawnGhostTrail(transform.position);
                        ghostsSpawned++;
                    }
                }

                yield return null;
            }

            // Push out of any wall the dash partially clipped before re-enabling
            // normal movement so the player can't end up stuck inside a crate.
            motor.DepenetrateFromBlockers();

            // Re-enable movement
            motor.SetMovementEnabled(true);
            _isDashing = false;
            _dashCoroutine = null;
        }

        /// <summary>
        /// Simple ghost trail: spawn a fading sprite copy at position.
        /// </summary>
        private void SpawnGhostTrail(Vector2 position)
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

            GameObject ghost = new GameObject("DashGhost");
            ghost.transform.position = position;
            ghost.transform.rotation = transform.rotation;
            ghost.transform.localScale = transform.localScale;

            SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
            ghostSr.sprite = _spriteRenderer.sprite;
            ghostSr.color = dashTintColor;
            ghostSr.sortingLayerID = _spriteRenderer.sortingLayerID;
            ghostSr.sortingOrder = _spriteRenderer.sortingOrder - 1;

            // Fade out and destroy
            StartCoroutine(FadeAndDestroy(ghost, ghostSr, 0.3f));
        }

        private IEnumerator FadeAndDestroy(GameObject obj, SpriteRenderer sr, float fadeTime)
        {
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / fadeTime);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            Destroy(obj);
        }

    }
}
