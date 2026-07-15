using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure NormalMode mapping policy and rate-limited blend calculations.</summary>
    internal static class RagdollPuppetNormalModeMath
    {
        internal static float ResolveMappingTarget(
            RagdollPuppetNormalMode mode,
            RagdollPuppetState state,
            bool hasRecentContact)
        {
            if (state != RagdollPuppetState.Puppet) return 1f;

            switch (mode)
            {
                case RagdollPuppetNormalMode.Active:
                    return 1f;
                case RagdollPuppetNormalMode.Unmapped:
                    return hasRecentContact ? 1f : 0f;
                default:
                    return 1f;
            }
        }

        internal static float StepMappingWeight(
            float current,
            float target,
            float speed,
            float deltaTime)
        {
            float safeCurrent = SanitizeWeight(current, 0f);
            float safeTarget = SanitizeWeight(target, safeCurrent);
            float safeSpeed = SanitizeNonNegative(speed);
            float safeDeltaTime = SanitizeNonNegative(deltaTime);

            return Mathf.MoveTowards(
                safeCurrent,
                safeTarget,
                safeSpeed * safeDeltaTime);
        }

        static float SanitizeWeight(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return Mathf.Clamp01(fallback);
            }

            return Mathf.Clamp01(value);
        }

        static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }
}
