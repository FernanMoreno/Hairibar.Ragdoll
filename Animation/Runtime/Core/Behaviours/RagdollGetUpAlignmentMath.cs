using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure orientation and rigid-root alignment helpers.</summary>
    internal static class RagdollGetUpAlignmentMath
    {
        internal static RagdollGetUpOrientation Classify(
            Quaternion physicalRootRotation,
            Vector3 bodyFrontAxis,
            Vector3 groundUp,
            float minimumOrientationDot)
        {
            Vector3 localFront = NormalizeOrFallback(bodyFrontAxis, Vector3.forward);
            Vector3 up = NormalizeOrFallback(groundUp, Vector3.up);
            float dot = Vector3.Dot(physicalRootRotation * localFront, up);

            if (Mathf.Abs(dot) < Mathf.Clamp01(minimumOrientationDot))
            {
                return RagdollGetUpOrientation.Unknown;
            }

            return dot < 0f
                ? RagdollGetUpOrientation.Prone
                : RagdollGetUpOrientation.Supine;
        }

        internal static Vector3 CalculateHeading(
            Quaternion physicalRootRotation,
            Vector3 bodyUpAxis,
            RagdollGetUpOrientation orientation,
            Vector3 groundUp,
            Vector3 fallbackForward)
        {
            Vector3 up = NormalizeOrFallback(groundUp, Vector3.up);
            Vector3 localUp = NormalizeOrFallback(bodyUpAxis, Vector3.up);
            Vector3 bodyUp = physicalRootRotation * localUp;

            Vector3 candidate = orientation == RagdollGetUpOrientation.Supine
                ? -bodyUp
                : bodyUp;
            Vector3 heading = Vector3.ProjectOnPlane(candidate, up);

            if (heading.sqrMagnitude <= 0.000001f)
            {
                heading = Vector3.ProjectOnPlane(fallbackForward, up);
            }

            if (heading.sqrMagnitude <= 0.000001f)
            {
                heading = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (heading.sqrMagnitude <= 0.000001f)
            {
                heading = Vector3.Cross(up, Vector3.right);
            }

            return heading.normalized;
        }

        internal static void CalculateTargetRootPose(
            Vector3 currentTargetRootPosition,
            Quaternion currentTargetRootRotation,
            Vector3 currentTargetHipPosition,
            Vector3 puppetHipPosition,
            Quaternion physicalRootRotation,
            Vector3 bodyUpAxis,
            RagdollGetUpOrientation orientation,
            Vector3 groundUp,
            Vector3 fallbackForward,
            Vector3 characterSpaceOffset,
            out Vector3 desiredTargetRootPosition,
            out Quaternion desiredTargetRootRotation)
        {
            Vector3 up = NormalizeOrFallback(groundUp, Vector3.up);
            Vector3 heading = CalculateHeading(
                physicalRootRotation,
                bodyUpAxis,
                orientation,
                up,
                fallbackForward);

            desiredTargetRootRotation = Quaternion.LookRotation(heading, up);
            Vector3 desiredHipPosition = puppetHipPosition
                + desiredTargetRootRotation * characterSpaceOffset;

            Quaternion rotationDelta = desiredTargetRootRotation
                * Quaternion.Inverse(currentTargetRootRotation);
            Vector3 currentRootToHip = currentTargetHipPosition
                - currentTargetRootPosition;

            desiredTargetRootPosition = desiredHipPosition
                - rotationDelta * currentRootToHip;
        }

        internal static Vector3 ApplyPositionDelta(
            Vector3 position,
            Vector3 previousRootPosition,
            Vector3 nextRootPosition,
            Quaternion rotationDelta)
        {
            return nextRootPosition
                + rotationDelta * (position - previousRootPosition);
        }

        static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.000001f
                ? value.normalized
                : fallback;
        }
    }
}
