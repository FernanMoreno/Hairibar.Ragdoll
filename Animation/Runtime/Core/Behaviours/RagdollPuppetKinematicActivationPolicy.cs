using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Pure classification and state policy for BehaviourPuppet Kinematic normal mode.
    /// </summary>
    internal static class RagdollPuppetKinematicActivationPolicy
    {
        internal static RagdollPuppetKinematicActivationSource ResolveSource(
            bool hasOtherRigidbody,
            bool otherRigidbodyIsKinematic)
        {
            if (!hasOtherRigidbody)
            {
                return RagdollPuppetKinematicActivationSource.StaticCollider;
            }

            return otherRigidbodyIsKinematic
                ? RagdollPuppetKinematicActivationSource.KinematicRigidbody
                : RagdollPuppetKinematicActivationSource.DynamicRigidbody;
        }

        internal static bool ShouldQueueActivation(
            RagdollPuppetNormalMode mode,
            RagdollPuppetState state,
            RagdollPuppetKinematicActivationSource source,
            float impulse,
            float minimumImpulse,
            bool activateOnStaticCollisions,
            bool activateOnDynamicCollisions)
        {
            if (mode != RagdollPuppetNormalMode.Kinematic
                || state != RagdollPuppetState.Puppet)
            {
                return false;
            }

            if (float.IsNaN(impulse)
                || float.IsInfinity(impulse)
                || impulse < 0f)
            {
                return false;
            }

            float safeImpulse = impulse;
            float safeMinimumImpulse = SanitizeNonNegative(minimumImpulse);
            if (safeImpulse < safeMinimumImpulse)
            {
                return false;
            }

            switch (source)
            {
                case RagdollPuppetKinematicActivationSource.StaticCollider:
                case RagdollPuppetKinematicActivationSource.KinematicRigidbody:
                    return activateOnStaticCollisions;
                case RagdollPuppetKinematicActivationSource.DynamicRigidbody:
                    return activateOnDynamicCollisions;
                default:
                    return false;
            }
        }

        internal static bool ShouldReturnToKinematic(
            RagdollPuppetNormalMode mode,
            RagdollPuppetState state,
            RagdollSimulationMode simulationMode,
            bool simulationTransitioning,
            bool hasRecentContact,
            bool hasPendingActivation,
            bool temporarySuppressionRecovered)
        {
            return mode == RagdollPuppetNormalMode.Kinematic
                && state == RagdollPuppetState.Puppet
                && simulationMode == RagdollSimulationMode.Active
                && !simulationTransitioning
                && !hasRecentContact
                && !hasPendingActivation
                && temporarySuppressionRecovered;
        }

        static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }
}
