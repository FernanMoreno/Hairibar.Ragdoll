using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Deterministic calculations used by <see cref="RagdollFallBehaviour"/>.
    /// Kept independent from Physics and Animator so thresholds can be tested without
    /// relying on a rendered or simulated frame.
    /// </summary>
    public static class RagdollFallMath
    {
        public static float ResolveWritheBlend(
            float height,
            float verticalVelocity,
            float writheHeight,
            float writheVerticalVelocity)
        {
            float heightBlend = NormalizePositive(height, writheHeight);
            float velocityBlend = NormalizePositive(
                verticalVelocity,
                writheVerticalVelocity);
            return Mathf.Max(heightBlend, velocityBlend);
        }

        public static float MoveBlend(
            float current,
            float target,
            float speed,
            float deltaTime)
        {
            current = SanitizeUnit(current);
            target = SanitizeUnit(target);
            speed = SanitizeNonNegative(speed);
            deltaTime = SanitizeNonNegative(deltaTime);
            return Mathf.MoveTowards(current, target, speed * deltaTime);
        }

        public static bool CanEnd(
            bool enabled,
            bool alreadyEnded,
            float elapsedTime,
            float minimumTime,
            float pelvisSpeed,
            float maximumEndVelocity)
        {
            if (!enabled || alreadyEnded) return false;

            elapsedTime = SanitizeNonNegative(elapsedTime);
            minimumTime = SanitizeNonNegative(minimumTime);
            pelvisSpeed = SanitizeNonNegative(pelvisSpeed);
            maximumEndVelocity = SanitizeNonNegative(maximumEndVelocity);

            return elapsedTime >= minimumTime
                && pelvisSpeed < maximumEndVelocity;
        }

        public static Vector3 ResolveUp(Vector3 gravity)
        {
            return gravity.sqrMagnitude > Mathf.Epsilon
                ? -gravity.normalized
                : Vector3.up;
        }

        static float NormalizePositive(float value, float threshold)
        {
            value = SanitizeFinite(value);
            threshold = SanitizeNonNegative(threshold);
            if (threshold <= Mathf.Epsilon)
            {
                return value > 0f ? 1f : 0f;
            }

            return Mathf.Clamp01(value / threshold);
        }

        static float SanitizeUnit(float value)
        {
            return Mathf.Clamp01(SanitizeFinite(value));
        }

        static float SanitizeNonNegative(float value)
        {
            return Mathf.Max(0f, SanitizeFinite(value));
        }

        static float SanitizeFinite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : value;
        }
    }
}
