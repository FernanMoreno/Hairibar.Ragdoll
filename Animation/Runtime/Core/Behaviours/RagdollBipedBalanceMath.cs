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
