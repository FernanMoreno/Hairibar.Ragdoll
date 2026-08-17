using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollBipedStepFoot
    {
        Left,
        Right
    }

    public enum RagdollBipedStepDirection
    {
        Forward,
        Backward,
        Left,
        Right
    }

    public enum RagdollBipedStaggerOutcome
    {
        Continue,
        Succeeded,
        Failed
    }

    /// <summary>
    /// Pure recovery-step math, self-contained (no dependency outside this
    /// package). Sibling to <see cref="RagdollBipedBalanceMath"/>: that module
    /// decides *whether* a step is needed, this one decides *which foot*, *how
    /// far*, and *which of the four Animator step clips* to crossfade.
    /// </summary>
    public static class RagdollBipedStaggerMath
    {
        /// <summary>
        /// The trailing foot -- farther (on the horizontal plane) from the
        /// capture point -- becomes the swing foot, widening the support base
        /// under where the puppet is about to overbalance.
        /// </summary>
        public static RagdollBipedStepFoot SelectStepFoot(
            Vector3 capturePoint,
            Vector3 leftFoot,
            Vector3 rightFoot)
        {
            return SelectStepFoot(capturePoint, leftFoot, rightFoot, Vector3.up);
        }

        public static RagdollBipedStepFoot SelectStepFoot(
            Vector3 capturePoint,
            Vector3 leftFoot,
            Vector3 rightFoot,
            Vector3 supportUp)
        {
            Vector3 up = ResolveSupportUp(supportUp);
            Vector3 p = Vector3.ProjectOnPlane(capturePoint, up);
            Vector3 a = Vector3.ProjectOnPlane(leftFoot, up);
            Vector3 b = Vector3.ProjectOnPlane(rightFoot, up);

            float distanceToLeft = Vector3.Distance(a, p);
            float distanceToRight = Vector3.Distance(b, p);
            return distanceToLeft >= distanceToRight
                ? RagdollBipedStepFoot.Left
                : RagdollBipedStepFoot.Right;
        }

        /// <summary>
        /// Caps how far the swing foot may reach in one step, preventing a large
        /// capture-point overshoot from producing an anatomically impossible
        /// lunge. Returns landingTarget unchanged when already within range.
        /// </summary>
        public static Vector3 ClampStepLength(
            Vector3 landingTarget,
            Vector3 stanceFootPosition,
            float maxStepLength)
        {
            Vector3 offset = landingTarget - stanceFootPosition;
            float distance = offset.magnitude;
            float safeMax = Mathf.Max(0f, maxStepLength);
            if (distance <= safeMax || distance <= 0.0001f) return landingTarget;

            return stanceFootPosition + offset / distance * safeMax;
        }

        /// <summary>
        /// Classifies a world-space offset into one of four body-relative step
        /// directions, picking whichever of the front/right body axes the
        /// offset aligns with more strongly.
        /// </summary>
        public static RagdollBipedStepDirection ClassifyStepDirection(
            Vector3 offset,
            Vector3 bodyFrontAxis,
            Vector3 bodyUpAxis)
        {
            Vector3 up = bodyUpAxis.sqrMagnitude > 0.0001f
                ? bodyUpAxis.normalized
                : Vector3.up;
            Vector3 front = Vector3.ProjectOnPlane(bodyFrontAxis, up);
            front = front.sqrMagnitude > 0.0001f
                ? front.normalized
                : Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, front);

            Vector3 flatOffset = Vector3.ProjectOnPlane(offset, up);
            float forwardAmount = Vector3.Dot(flatOffset, front);
            float rightAmount = Vector3.Dot(flatOffset, right);

            if (Mathf.Abs(forwardAmount) >= Mathf.Abs(rightAmount))
            {
                return forwardAmount >= 0f
                    ? RagdollBipedStepDirection.Forward
                    : RagdollBipedStepDirection.Backward;
            }
            return rightAmount >= 0f
                ? RagdollBipedStepDirection.Right
                : RagdollBipedStepDirection.Left;
        }

        /// <summary>
        /// Decides what a completed step cycle (LiftOff-Swing-Replant-Settling,
        /// back to Idle) should do next, given the balance classification measured
        /// at that moment. Already balanced -> Succeeded. Still needs a step and
        /// budget remains -> Continue (begin another step). Otherwise -> Failed.
        /// Unrecoverable always fails immediately, regardless of remaining budget.
        /// </summary>
        public static RagdollBipedStaggerOutcome ResolveOutcome(
            RagdollBipedBalanceState classification,
            int stepCount,
            int maxSteps)
        {
            if (classification == RagdollBipedBalanceState.Stable
                || classification == RagdollBipedBalanceState.RecoverableWithoutStep)
            {
                return RagdollBipedStaggerOutcome.Succeeded;
            }
            if (classification == RagdollBipedBalanceState.Unrecoverable)
            {
                return RagdollBipedStaggerOutcome.Failed;
            }
            return stepCount < Mathf.Max(0, maxSteps)
                ? RagdollBipedStaggerOutcome.Continue
                : RagdollBipedStaggerOutcome.Failed;
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
