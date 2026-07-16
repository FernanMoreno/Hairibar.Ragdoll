using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure lifecycle blend and drive composition shared by runtime and tests.</summary>
    internal static class RagdollLifecycleMath
    {
        internal static float EvaluateKillMuscleWeight(
            float startingWeight,
            float deadWeight,
            float elapsedTime,
            float killDuration)
        {
            float start = SanitizeWeight(startingWeight, 1f);
            float target = SanitizeWeight(deadWeight, 0.01f);
            float duration = SanitizeNonNegative(killDuration, 0f);
            float elapsed = SanitizeNonNegative(elapsedTime, 0f);

            if (duration <= Mathf.Epsilon || start <= target)
            {
                return target;
            }

            return Mathf.Lerp(
                start,
                target,
                Mathf.Clamp01(elapsed / duration));
        }

        internal static bool IsKillComplete(
            float startingWeight,
            float deadWeight,
            float elapsedTime,
            float killDuration)
        {
            float start = SanitizeWeight(startingWeight, 1f);
            float target = SanitizeWeight(deadWeight, 0.01f);
            float duration = SanitizeNonNegative(killDuration, 0f);
            float elapsed = SanitizeNonNegative(elapsedTime, 0f);
            return duration <= Mathf.Epsilon
                || start <= target
                || elapsed >= duration;
        }

        internal static void ApplyDeadDrive(
            ref BoneProfile profile,
            float positionAuthorityMultiplier,
            float muscleWeightMultiplier,
            float muscleDamperAdd)
        {
            profile.positionAlpha *= SanitizeWeight(
                positionAuthorityMultiplier,
                1f);
            profile.rotationAlpha *= SanitizeWeight(
                muscleWeightMultiplier,
                1f);
            profile.rotationDampingRatio = Mathf.Max(
                0f,
                profile.rotationDampingRatio
                    + SanitizeNonNegative(muscleDamperAdd, 0f));
        }

        internal static float SanitizeWeight(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }
            return Mathf.Clamp01(value);
        }

        internal static float SanitizeNonNegative(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }
            return Mathf.Max(0f, value);
        }
    }
}
