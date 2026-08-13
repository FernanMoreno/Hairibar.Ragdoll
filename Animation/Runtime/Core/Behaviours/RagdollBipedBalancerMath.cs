using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Pure reactive-torque math for the SubBehaviourBalancer port. RootMotion's
    /// real implementation is closed-source -- only the public settings surface
    /// (RagdollBipedBalancerSettings) and its documented meaning are public. This
    /// correction model is Hairibar-owned test design on Unity PhysX: a horizontal
    /// ankle torque proportional to how far the (velocity-predicted) capture point
    /// has drifted from the center-of-pressure target, scaled/clamped by settings.
    /// </summary>
    public static class RagdollBipedBalancerMath
    {
        public static Vector3 ResolveCenterOfPressureTarget(
            Vector3 supportCenter,
            Vector3 copOffset)
        {
            return supportCenter + copOffset;
        }

        public static Vector3 ResolveReactiveTorque(
            Vector3 capturePoint,
            Vector3 captureVelocity,
            Vector3 centerOfPressureTarget,
            Vector3 up,
            RagdollBipedBalancerSettings settings)
        {
            return ResolveReactiveTorque(capturePoint, captureVelocity,
                centerOfPressureTarget, up, Vector3.zero, settings);
        }

        /// <summary>
        /// Hairibar's observable interpretation of PuppetMaster's public settings:
        /// MaxForceMlp scales the effective correction limit and DamperForSpring
        /// subtracts correction already travelling along the ankle torque axis.
        /// This is not a claim about RootMotion's closed implementation.
        /// </summary>
        public static Vector3 ResolveReactiveTorque(
            Vector3 capturePoint,
            Vector3 captureVelocity,
            Vector3 centerOfPressureTarget,
            Vector3 up,
            Vector3 ankleAngularVelocity,
            RagdollBipedBalancerSettings settings)
        {
            float safeMaxTorque = Mathf.Max(0f, settings.MaxTorqueMag)
                * Mathf.Max(0f, settings.MaxForceMlp);
            float safeTorqueMlp = Mathf.Max(0f, settings.TorqueMlp);
            if (safeTorqueMlp <= 0f || safeMaxTorque <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 safeUp = up.sqrMagnitude > Mathf.Epsilon ? up.normalized : Vector3.up;
            Vector3 offset = Vector3.ProjectOnPlane(
                capturePoint - centerOfPressureTarget, safeUp);
            Vector3 predictedVelocity = Vector3.ProjectOnPlane(captureVelocity, safeUp)
                * Mathf.Max(0f, settings.VelocityF);
            Vector3 lean = offset + predictedVelocity;
            if (lean.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            Vector3 axis = Vector3.Cross(safeUp, lean);
            if (axis.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            Vector3 axisDirection = axis.normalized;
            float magnitude = lean.magnitude
                * safeTorqueMlp
                * Mathf.Max(0f, settings.IMlp);
            magnitude = Mathf.Min(magnitude, safeMaxTorque);
            float damping = Mathf.Max(0f, Vector3.Dot(
                Vector3.ProjectOnPlane(ankleAngularVelocity, safeUp), axisDirection))
                * Mathf.Max(0f, settings.DamperForSpring);
            magnitude = Mathf.Max(0f, magnitude - damping);

            return axisDirection * magnitude;
        }
    }
}
