using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure predicates shared by the runtime behaviour and tests.</summary>
    internal static class RagdollPuppetBehaviourMath
    {
        internal static bool ShouldLoseBalance(
            float targetDistance,
            float knockOutDistance,
            float effectivePositionAuthority,
            float pinWeightThreshold,
            float knockOutDistanceMultiplier)
        {
            float distance = Mathf.Max(0f, targetDistance);
            float threshold = Mathf.Max(0f, knockOutDistance)
                * Mathf.Max(0f, knockOutDistanceMultiplier);

            if (distance <= threshold)
            {
                return false;
            }

            return Mathf.Clamp01(effectivePositionAuthority)
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
