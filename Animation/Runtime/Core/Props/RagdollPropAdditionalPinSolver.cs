using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Diagnostic snapshot for one additional-pin evaluation. All vectors are in world
    /// space and Impulse is the value submitted through ForceMode.Impulse.
    /// </summary>
    public struct RagdollPropAdditionalPinStep
    {
        public bool Applied { get; }
        public Vector3 TargetPoint { get; }
        public Vector3 PhysicalPoint { get; }
        public Vector3 PositionError { get; }
        public Vector3 TargetPointVelocity { get; }
        public Vector3 PhysicalPointVelocity { get; }
        public Vector3 DesiredVelocityChange { get; }
        public Vector3 Impulse { get; }
        public float AppliedWeight { get; }

        internal RagdollPropAdditionalPinStep(
            bool applied,
            Vector3 targetPoint,
            Vector3 physicalPoint,
            Vector3 positionError,
            Vector3 targetPointVelocity,
            Vector3 physicalPointVelocity,
            Vector3 desiredVelocityChange,
            Vector3 impulse,
            float appliedWeight)
        {
            Applied = applied;
            TargetPoint = targetPoint;
            PhysicalPoint = physicalPoint;
            PositionError = positionError;
            TargetPointVelocity = targetPointVelocity;
            PhysicalPointVelocity = physicalPointVelocity;
            DesiredVelocityChange = desiredVelocityChange;
            Impulse = impulse;
            AppliedWeight = appliedWeight;
        }

        internal static RagdollPropAdditionalPinStep Empty =>
            new RagdollPropAdditionalPinStep(
                false,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                0f);
    }

    /// <summary>
    /// Stateful Target-point sampler and impulse solver for the virtual second pin.
    /// The desired point is recomputed from the absolute local offset every step, so
    /// pickup/drop cycles and generation changes cannot accumulate positional offset.
    /// </summary>
    internal sealed class RagdollPropAdditionalPinSolver
    {
        Vector3 previousTargetPoint;
        bool hasPreviousTargetPoint;

        internal bool HasPreviousTargetPoint => hasPreviousTargetPoint;

        internal void Reset()
        {
            previousTargetPoint = Vector3.zero;
            hasPreviousTargetPoint = false;
        }

        internal bool TryApply(
            Rigidbody body,
            Transform targetSlot,
            RagdollPropAdditionalPinSnapshot settings,
            float effectivePositionAuthority,
            float deltaTime,
            out RagdollPropAdditionalPinStep step,
            out string error)
        {
            return TryApply(
                body,
                targetSlot,
                settings,
                effectivePositionAuthority,
                1f,
                deltaTime,
                out step,
                out error);
        }

        internal bool TryApply(
            Rigidbody body,
            Transform targetSlot,
            RagdollPropAdditionalPinSnapshot settings,
            float effectivePositionAuthority,
            float weightMultiplier,
            float deltaTime,
            out RagdollPropAdditionalPinStep step,
            out string error)
        {
            step = RagdollPropAdditionalPinStep.Empty;
            error = null;
            if (!body || !targetSlot)
            {
                error = "Additional pin requires a live slot Rigidbody and Target slot.";
                Reset();
                return false;
            }
            if (!RagdollPropAdditionalPinSettings.IsFinite(deltaTime)
                || deltaTime <= Mathf.Epsilon)
            {
                error = "Additional pin requires a positive finite fixed delta time.";
                Reset();
                return false;
            }

            Vector3 targetPoint = targetSlot.TransformPoint(settings.LocalOffset);
            Vector3 physicalPoint = body.transform.TransformPoint(
                settings.LocalOffset);
            Vector3 targetVelocity = hasPreviousTargetPoint
                ? (targetPoint - previousTargetPoint) / deltaTime
                : Vector3.zero;
            previousTargetPoint = targetPoint;
            hasPreviousTargetPoint = true;

            Vector3 positionError = targetPoint - physicalPoint;
            Vector3 pointVelocity = body.GetPointVelocity(physicalPoint);
            float safeWeightMultiplier =
                RagdollPropAdditionalPinSettings.IsFinite(weightMultiplier)
                    ? Mathf.Max(0f, weightMultiplier)
                    : 0f;
            float appliedWeight = settings.Enabled
                ? settings.Weight
                    * Mathf.Clamp01(effectivePositionAuthority)
                    * safeWeightMultiplier
                : 0f;
            Vector3 desiredVelocityChange = (
                targetVelocity
                + positionError / deltaTime
                - pointVelocity) * appliedWeight;
            Vector3 impulse = desiredVelocityChange * settings.Mass;
            bool canApply = appliedWeight > 0f
                && body.gameObject.activeInHierarchy
                && !body.isKinematic
                && impulse.sqrMagnitude > 0f;

            if (canApply)
            {
                body.AddForceAtPosition(
                    impulse,
                    physicalPoint,
                    ForceMode.Impulse);
            }

            step = new RagdollPropAdditionalPinStep(
                canApply,
                targetPoint,
                physicalPoint,
                positionError,
                targetVelocity,
                pointVelocity,
                desiredVelocityChange,
                impulse,
                appliedWeight);
            return true;
        }
    }
}
