using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure predicates shared by the runtime behaviour and tests.</summary>
    internal static class RagdollPuppetBehaviourMath
    {
        internal static float ResolveConfiguredPinWeight(
            float authoredPositionAlpha,
            float masterAlpha,
            float persistentPositionAuthority)
        {
            return Mathf.Clamp01(authoredPositionAlpha)
                * Mathf.Clamp01(masterAlpha)
                * Mathf.Clamp01(persistentPositionAuthority);
        }

        internal static float ResolveEffectivePinWeight(
            float configuredPinWeight,
            float positionSuppression,
            float minimumPositionAuthority,
            float statePositionAuthority)
        {
            float suppressionAuthority = Mathf.Max(
                1f - Mathf.Clamp01(positionSuppression),
                Mathf.Clamp01(minimumPositionAuthority));

            return Mathf.Clamp01(
                Mathf.Clamp01(configuredPinWeight)
                * suppressionAuthority
                * Mathf.Clamp01(statePositionAuthority));
        }

        internal static Vector3 LimitVelocity(
            Vector3 velocity,
            float maximumVelocity)
        {
            return maximumVelocity == Mathf.Infinity
                ? velocity
                : Vector3.ClampMagnitude(velocity, maximumVelocity);
        }

        internal static bool ShouldLoseBalance(
            float targetDistance,
            float knockOutDistance,
            float effectivePositionAuthority,
            float pinWeightThreshold,
            float knockOutDistanceMultiplier)
        {
            return ShouldLoseBalance(
                targetDistance,
                knockOutDistance,
                effectivePositionAuthority,
                effectivePositionAuthority,
                pinWeightThreshold,
                knockOutDistanceMultiplier,
                true);
        }

        internal static bool ShouldLoseBalance(
            float targetDistance,
            float knockOutDistance,
            float effectivePinWeight,
            float configuredPinWeight,
            float pinWeightThreshold,
            float knockOutDistanceMultiplier,
            bool unpinnedMuscleKnockout)
        {
            float distance = Mathf.Max(0f, targetDistance);
            float threshold = Mathf.Max(0f, knockOutDistance)
                * Mathf.Max(0f, knockOutDistanceMultiplier);

            if (!unpinnedMuscleKnockout
                && Mathf.Clamp01(configuredPinWeight) <= 0f)
            {
                return false;
            }

            if (distance <= threshold)
            {
                return false;
            }

            return Mathf.Clamp01(effectivePinWeight)
                <= Mathf.Clamp01(pinWeightThreshold);
        }

        internal static bool IsGetUpReady(
            float unpinnedElapsedTime,
            float getUpDelay,
            float rootSpeed,
            float maximumGetUpVelocity)
        {
            return Mathf.Max(0f, unpinnedElapsedTime)
                    >= Mathf.Max(0f, getUpDelay)
                && Mathf.Max(0f, rootSpeed)
                    <= Mathf.Max(0f, maximumGetUpVelocity);
        }
    }
}
