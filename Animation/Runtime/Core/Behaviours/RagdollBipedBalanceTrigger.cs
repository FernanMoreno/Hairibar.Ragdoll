using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Hysteresis wrapper around RagdollBipedBalanceMath's RequiresStep classification --
    /// fires exactly once when RequiresStep has been sustained for minimumRequiresStepDuration,
    /// so a single unstable frame doesn't trigger a recovery step.
    /// </summary>
    internal sealed class RagdollBipedBalanceTrigger
    {
        float requiresStepElapsed;

        internal void Reset()
        {
            requiresStepElapsed = 0f;
        }

        internal bool Evaluate(RagdollBipedBalanceState classification, float deltaTime, float minimumRequiresStepDuration)
        {
            if (classification != RagdollBipedBalanceState.RequiresStep)
            {
                requiresStepElapsed = 0f;
                return false;
            }

            bool wasBelow = requiresStepElapsed < minimumRequiresStepDuration;
            requiresStepElapsed += Mathf.Max(0f, deltaTime);
            bool isAtOrAbove = requiresStepElapsed >= minimumRequiresStepDuration;
            return wasBelow && isAtOrAbove;
        }
    }
}
