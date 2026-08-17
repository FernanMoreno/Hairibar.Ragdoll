using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollBipedBalanceState
    {
        Stable,
        RecoverableWithoutStep,
        RequiresStep,
        Unrecoverable
    }

    /// <summary>
    /// Pure capture-point balance math, self-contained (no dependency outside this
    /// package). The capture point projects the center of mass forward along its
    /// current horizontal velocity using an inverted-pendulum approximation; how
    /// far it lands outside the foot-to-foot support segment classifies whether
    /// the puppet can absorb a push through joints/muscles alone, needs a step,
    /// or should give up and fall.
    /// </summary>
    public static class RagdollBipedBalanceMath
    {
        public static Vector3 CapturePoint(
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            float pendulumLength,
            float gravity)
        {
            return CapturePoint(
                centerOfMass,
                centerOfMassVelocity,
                pendulumLength,
                gravity,
                Vector3.up);
        }

        public static Vector3 CapturePoint(
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            float pendulumLength,
            float gravity,
            Vector3 supportUp)
        {
            float omega = Mathf.Sqrt(
                Mathf.Max(0.01f, gravity) / Mathf.Max(0.05f, pendulumLength));
            Vector3 up = ResolveSupportUp(supportUp);
            return centerOfMass
                + Vector3.ProjectOnPlane(centerOfMassVelocity, up) / omega;
        }

        /// <summary>Positive when inside the supportRadius around the foot-to-foot
        /// segment, negative by the overshoot distance when outside it.</summary>
        public static float SignedSupportMargin(
            Vector3 point,
            Vector3 leftFoot,
            Vector3 rightFoot,
            float supportRadius)
        {
            return SignedSupportMargin(
                point, leftFoot, rightFoot, supportRadius, Vector3.up);
        }

        public static float SignedSupportMargin(
            Vector3 point,
            Vector3 leftFoot,
            Vector3 rightFoot,
            float supportRadius,
            Vector3 supportUp)
        {
            Vector3 up = ResolveSupportUp(supportUp);
            Vector3 a = Vector3.ProjectOnPlane(leftFoot, up);
            Vector3 b = Vector3.ProjectOnPlane(rightFoot, up);
            Vector3 p = Vector3.ProjectOnPlane(point, up);
            Vector3 segment = b - a;
            float t = segment.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(p - a, segment) / segment.sqrMagnitude)
                : 0f;
            Vector3 nearest = a + segment * t;
            return Mathf.Max(0f, supportRadius) - Vector3.Distance(p, nearest);
        }

        /// <summary>
        /// Computes support margin from the feet that have current physical
        /// contact. One valid foot is a disk; two valid feet are a capsule around
        /// their projected segment. An unavailable point never becomes an
        /// endpoint merely because its Rigidbody is present.
        /// </summary>
        public static float SignedSupportMargin(
            Vector3 point,
            bool hasLeftFootSupport,
            Vector3 leftFoot,
            bool hasRightFootSupport,
            Vector3 rightFoot,
            float supportRadius,
            Vector3 supportUp)
        {
            Vector3 up = ResolveSupportUp(supportUp);
            bool leftAvailable = hasLeftFootSupport && IsFinite(leftFoot);
            bool rightAvailable = hasRightFootSupport && IsFinite(rightFoot);
            float radius = Mathf.Max(0f, supportRadius);

            if (!IsFinite(point)) return -radius;

            if (!leftAvailable && !rightAvailable)
            {
                // Keep the observable margin finite for telemetry. The
                // support-aware Classify overload separately turns zero support
                // into Unrecoverable instead of treating this as a step range.
                return -radius;
            }

            Vector3 projectedPoint = Vector3.ProjectOnPlane(point, up);
            if (leftAvailable && !rightAvailable)
            {
                return radius - Vector3.Distance(
                    projectedPoint,
                    Vector3.ProjectOnPlane(leftFoot, up));
            }

            if (!leftAvailable)
            {
                return radius - Vector3.Distance(
                    projectedPoint,
                    Vector3.ProjectOnPlane(rightFoot, up));
            }

            return SignedSupportMargin(
                point,
                leftFoot,
                rightFoot,
                radius,
                up);
        }

        public static float SignedCaptureMargin(
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            Vector3 leftFoot,
            Vector3 rightFoot,
            float pendulumLength,
            float gravity,
            float supportRadius)
        {
            return SignedCaptureMargin(
                centerOfMass,
                centerOfMassVelocity,
                leftFoot,
                rightFoot,
                pendulumLength,
                gravity,
                supportRadius,
                Vector3.up);
        }

        public static float SignedCaptureMargin(
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            Vector3 leftFoot,
            Vector3 rightFoot,
            float pendulumLength,
            float gravity,
            float supportRadius,
            Vector3 supportUp)
        {
            Vector3 capturePoint = CapturePoint(
                centerOfMass,
                centerOfMassVelocity,
                pendulumLength,
                gravity,
                supportUp);
            return SignedSupportMargin(
                capturePoint,
                leftFoot,
                rightFoot,
                supportRadius,
                supportUp);
        }

        public static RagdollBipedBalanceState Classify(
            float signedSupportMargin,
            float stableMargin,
            float requiresStepMargin)
        {
            float safeStableMargin = Mathf.Max(0f, stableMargin);
            float safeRequiresStepMargin = Mathf.Max(0f, requiresStepMargin);

            if (signedSupportMargin >= safeStableMargin)
                return RagdollBipedBalanceState.Stable;
            if (signedSupportMargin >= 0f)
                return RagdollBipedBalanceState.RecoverableWithoutStep;
            if (signedSupportMargin >= -safeRequiresStepMargin)
                return RagdollBipedBalanceState.RequiresStep;
            return RagdollBipedBalanceState.Unrecoverable;
        }

        public static RagdollBipedBalanceState Classify(
            float signedSupportMargin,
            int supportPointCount,
            float stableMargin,
            float requiresStepMargin)
        {
            if (supportPointCount <= 0)
            {
                return RagdollBipedBalanceState.Unrecoverable;
            }

            return Classify(
                signedSupportMargin,
                stableMargin,
                requiresStepMargin);
        }

        static Vector3 ResolveSupportUp(Vector3 supportUp)
        {
            return IsFinite(supportUp) && supportUp.sqrMagnitude > 0.000001f
                ? supportUp.normalized
                : Vector3.up;
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
