using UnityEngine;

namespace FrentePartido.Combat
{
    /// <summary>
    /// Static utility class for centralized damage calculations and line-of-sight checks.
    /// All damage math goes through here so balancing is one-stop.
    /// </summary>
    public static class DamageDealer
    {
        /// <summary>
        /// Calculate weapon damage with optional distance falloff.
        /// When maxRange is 0 or distance is 0, returns full baseDamage.
        /// </summary>
        public static int CalculateDamage(int baseDamage, float distance = 0f, float maxRange = 0f)
        {
            if (baseDamage <= 0) return 0;
            if (maxRange <= 0f || distance <= 0f) return baseDamage;

            float t = Mathf.Clamp01(distance / maxRange);
            // Linear falloff: full damage at 0, 50% at max range
            float multiplier = Mathf.Lerp(1f, 0.5f, t);
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        /// <summary>
        /// Calculate grenade damage with linear falloff from center to edge.
        /// Full damage at center, zero at radius edge.
        /// </summary>
        public static int CalculateGrenadeDamage(int baseDamage, float distance, float radius)
        {
            if (baseDamage <= 0 || radius <= 0f) return 0;
            if (distance >= radius) return 0;

            float t = Mathf.Clamp01(distance / radius);
            float multiplier = 1f - t;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        /// <summary>
        /// Returns true if there is a clear line of sight (no obstacles) between two points.
        /// </summary>
        public static bool IsInLineOfSight(Vector2 from, Vector2 to, LayerMask obstacleLayer)
        {
            RaycastHit2D hit = Physics2D.Linecast(from, to, obstacleLayer);
            return hit.collider == null;
        }

        /// <summary>
        /// Apply random spread to a direction vector within the given angle (degrees).
        /// </summary>
        public static Vector2 ApplySpread(Vector2 direction, float spreadAngle)
        {
            if (spreadAngle <= 0f) return direction.normalized;

            float halfSpread = spreadAngle * 0.5f;
            float randomAngle = Random.Range(-halfSpread, halfSpread);
            float radians = randomAngle * Mathf.Deg2Rad;

            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 result = new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos
            );

            return result.normalized;
        }
    }
}
