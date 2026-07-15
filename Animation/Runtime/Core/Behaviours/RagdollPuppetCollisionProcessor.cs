using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Allocation-free BehaviourPuppet collision gate. The timestamp supplied by the shared
    /// collision hub defines the accounting window, independent of component execution order.
    /// </summary>
    internal struct RagdollPuppetCollisionProcessor
    {
        bool hasStep;
        float fixedTime;
        int reportedCount;
        int acceptedCount;
        int rejectedPhaseCount;
        int rejectedLayerCount;
        int rejectedThresholdCount;
        int rejectedBudgetCount;

        internal RagdollPuppetCollisionStepSnapshot Snapshot =>
            new RagdollPuppetCollisionStepSnapshot(
                hasStep,
                fixedTime,
                reportedCount,
                acceptedCount,
                rejectedPhaseCount,
                rejectedLayerCount,
                rejectedThresholdCount,
                rejectedBudgetCount);

        internal void Reset()
        {
            this = default(RagdollPuppetCollisionProcessor);
        }

        internal bool TryAccept(
            float eventFixedTime,
            RagdollCollisionPhase phase,
            int otherLayer,
            float squaredImpulse,
            int layerMask,
            float minimumSquaredImpulse,
            int maximumCollisions,
            out RagdollPuppetCollisionRejectionReason rejectionReason)
        {
            BeginStep(eventFixedTime);
            reportedCount++;

            if (phase != RagdollCollisionPhase.Enter
                && phase != RagdollCollisionPhase.Stay)
            {
                rejectedPhaseCount++;
                rejectionReason =
                    RagdollPuppetCollisionRejectionReason.UnsupportedPhase;
                return false;
            }

            if (otherLayer < 0 || otherLayer >= 32)
            {
                rejectedLayerCount++;
                rejectionReason =
                    RagdollPuppetCollisionRejectionReason.InvalidLayer;
                return false;
            }

            if ((layerMask & (1 << otherLayer)) == 0)
            {
                rejectedLayerCount++;
                rejectionReason =
                    RagdollPuppetCollisionRejectionReason.LayerFiltered;
                return false;
            }

            if (float.IsNaN(squaredImpulse)
                || float.IsInfinity(squaredImpulse)
                || squaredImpulse < 0f)
            {
                rejectedThresholdCount++;
                rejectionReason =
                    RagdollPuppetCollisionRejectionReason.InvalidImpulse;
                return false;
            }

            float threshold = SanitizeNonNegative(minimumSquaredImpulse);
            if (squaredImpulse < threshold)
            {
                rejectedThresholdCount++;
                rejectionReason =
                    RagdollPuppetCollisionRejectionReason.BelowThreshold;
                return false;
            }

            int limit = Mathf.Max(1, maximumCollisions);
            if (acceptedCount >= limit)
            {
                rejectedBudgetCount++;
                rejectionReason =
                    RagdollPuppetCollisionRejectionReason.BudgetExceeded;
                return false;
            }

            acceptedCount++;
            rejectionReason = RagdollPuppetCollisionRejectionReason.None;
            return true;
        }

        void BeginStep(float eventFixedTime)
        {
            if (hasStep && eventFixedTime == fixedTime) return;

            hasStep = true;
            fixedTime = eventFixedTime;
            reportedCount = 0;
            acceptedCount = 0;
            rejectedPhaseCount = 0;
            rejectedLayerCount = 0;
            rejectedThresholdCount = 0;
            rejectedBudgetCount = 0;
        }

        static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }
}
