using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure recovery-rate and pin-to-muscle authority composition.</summary>
    internal static class RagdollMuscleRecoveryMath
    {
        internal static float ResolvePositionRecoveryRate(
            float controllerBaseRate,
            float behaviourMultiplier,
            float groupMultiplier)
        {
            float safeBase = SanitizeNonNegative(controllerBaseRate, 0f);
            float safeBehaviour =
                SanitizeRecoveryMultiplier(behaviourMultiplier, 1f);
            float safeGroup = SanitizeRecoveryMultiplier(groupMultiplier, 1f);

            double product = (double) safeBase * safeBehaviour * safeGroup;
            if (product >= float.MaxValue) return float.MaxValue;
            return (float) product;
        }

        internal static float ResolveMinimumPositionAuthority(
            float configuredMinimum,
            float runtimeMultiplier)
        {
            return SanitizeWeight(configuredMinimum, 0f)
                * SanitizeWeight(runtimeMultiplier, 1f);
        }

        internal static float ResolveEffectivePositionAuthority(
            float persistentAuthority,
            float positionSuppression,
            float minimumSuppressionAuthority)
        {
            float authority = SanitizeWeight(persistentAuthority, 0f);
            float suppression = SanitizeWeight(positionSuppression, 0f);
            float minimum = SanitizeWeight(minimumSuppressionAuthority, 0f);
            float suppressionAuthority = Mathf.Max(1f - suppression, minimum);
            return Mathf.Clamp01(authority * suppressionAuthority);
        }

        internal static float ResolveRelativeMuscleWeight(
            float effectivePinAuthority,
            float relativeToPinWeight)
        {
            float pinAuthority = SanitizeWeight(effectivePinAuthority, 1f);
            float coupling = SanitizeWeight(relativeToPinWeight, 0f);
            return Mathf.Lerp(1f, pinAuthority, coupling);
        }

        internal static float SanitizeRecoveryMultiplier(
            float value,
            float fallback)
        {
            return SanitizeNonNegative(value, fallback);
        }

        internal static float SanitizeWeight(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return Mathf.Clamp01(fallback);
            }

            return Mathf.Clamp01(value);
        }

        static float SanitizeNonNegative(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return Mathf.Max(0f, fallback);
            }

            return Mathf.Max(0f, value);
        }
    }
}
