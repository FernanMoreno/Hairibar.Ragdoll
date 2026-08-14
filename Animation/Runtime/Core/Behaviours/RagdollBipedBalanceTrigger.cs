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
        bool firedThisEpisode;

        internal float RequiresStepElapsed => requiresStepElapsed;
        internal bool FiredThisEpisode => firedThisEpisode;

        internal void Reset()
        {
            requiresStepElapsed = 0f;
            firedThisEpisode = false;
        }

        internal bool Evaluate(RagdollBipedBalanceState classification, float deltaTime, float minimumRequiresStepDuration)
        {
            if (classification != RagdollBipedBalanceState.RequiresStep)
            {
                Reset();
                return false;
            }

            if (firedThisEpisode) return false;

            // A latch, not a strict "was below now at/above" comparison: with
            // minimumRequiresStepDuration == 0 the strict form never fires
            // (0 < 0 is false on the very first frame), even though a zero
            // duration should mean "immediate".
            requiresStepElapsed += Mathf.Max(0f, deltaTime);
            if (requiresStepElapsed < minimumRequiresStepDuration) return false;

            firedThisEpisode = true;
            return true;
        }
    }
}
