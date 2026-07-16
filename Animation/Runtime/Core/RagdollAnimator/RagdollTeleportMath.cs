using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal static class RagdollTeleportMath
    {
        const float MinimumQuaternionSquareMagnitude = 0.0000001f;

        internal static Quaternion NormalizeRotation(Quaternion value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentException(
                    "Teleport rotation must contain only finite values.",
                    nameof(value));
            }

            float squareMagnitude = value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;
            if (squareMagnitude <= MinimumQuaternionSquareMagnitude)
            {
                throw new ArgumentException(
                    "Teleport rotation must have a non-zero magnitude.",
                    nameof(value));
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(squareMagnitude);
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }

        internal static Quaternion CalculateDeltaRotation(
            Quaternion currentRotation,
            Quaternion destinationRotation)
        {
            Quaternion current = NormalizeRotation(currentRotation);
            Quaternion destination = NormalizeRotation(destinationRotation);
            return NormalizeRotation(destination * Quaternion.Inverse(current));
        }

        internal static Vector3 CalculateDeltaPosition(
            Vector3 currentPosition,
            Vector3 destinationPosition,
            Vector3 pivot,
            Quaternion deltaRotation)
        {
            ValidateFinite(currentPosition, nameof(currentPosition));
            ValidateFinite(destinationPosition, nameof(destinationPosition));
            ValidateFinite(pivot, nameof(pivot));
            Quaternion rotation = NormalizeRotation(deltaRotation);

            Vector3 rotatedPosition = pivot
                + rotation * (currentPosition - pivot);
            return destinationPosition - rotatedPosition;
        }

        internal static Vector3 TransformPoint(
            Vector3 point,
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot)
        {
            ValidateFinite(point, nameof(point));
            ValidateFinite(deltaPosition, nameof(deltaPosition));
            ValidateFinite(pivot, nameof(pivot));
            Quaternion rotation = NormalizeRotation(deltaRotation);

            return pivot + rotation * (point - pivot) + deltaPosition;
        }

        internal static Quaternion TransformRotation(
            Quaternion rotation,
            Quaternion deltaRotation)
        {
            return NormalizeRotation(
                NormalizeRotation(deltaRotation)
                * NormalizeRotation(rotation));
        }

        internal static RagdollAnimator.AnimatedPose TransformPose(
            RagdollAnimator.AnimatedPose pose,
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot)
        {
            pose.worldPosition = TransformPoint(
                pose.worldPosition,
                deltaRotation,
                deltaPosition,
                pivot);
            pose.worldRotation = TransformRotation(
                pose.worldRotation,
                deltaRotation);

            // A rigid teleport rotates the whole hierarchy in world space. Local joint
            // rotations relative to their transformed parents remain unchanged.
            return pose;
        }

        internal static void ValidateFinite(Vector3 value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentException(
                    "Teleport vectors must contain only finite values.",
                    parameterName);
            }
        }

        internal static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        internal static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }


    internal static class RagdollTeleportHierarchy
    {
        internal static void ApplyRootTransforms(
            Transform targetRoot,
            Transform puppetRoot,
            Vector3 targetDestinationPosition,
            Quaternion targetDestinationRotation,
            Vector3 puppetOriginalPosition,
            Quaternion puppetOriginalRotation,
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot)
        {
            if (!targetRoot) throw new ArgumentNullException(nameof(targetRoot));
            if (!puppetRoot) throw new ArgumentNullException(nameof(puppetRoot));

            bool targetIsBelowPuppet = targetRoot != puppetRoot
                && IsDescendantOf(targetRoot, puppetRoot);
            if (targetIsBelowPuppet)
            {
                puppetRoot.SetPositionAndRotation(
                    RagdollTeleportMath.TransformPoint(
                        puppetOriginalPosition,
                        deltaRotation,
                        deltaPosition,
                        pivot),
                    RagdollTeleportMath.TransformRotation(
                        puppetOriginalRotation,
                        deltaRotation));

                // The ancestor move already transformed the Target. Assigning the requested
                // absolute pose removes only accumulated floating-point error.
                targetRoot.SetPositionAndRotation(
                    targetDestinationPosition,
                    targetDestinationRotation);
                return;
            }

            targetRoot.SetPositionAndRotation(
                targetDestinationPosition,
                targetDestinationRotation);

            bool puppetMovedWithTarget = puppetRoot == targetRoot
                || IsDescendantOf(puppetRoot, targetRoot);
            if (puppetMovedWithTarget) return;

            puppetRoot.SetPositionAndRotation(
                RagdollTeleportMath.TransformPoint(
                    puppetOriginalPosition,
                    deltaRotation,
                    deltaPosition,
                    pivot),
                RagdollTeleportMath.TransformRotation(
                    puppetOriginalRotation,
                    deltaRotation));
        }

        internal static void RestoreRootTransforms(
            Transform targetRoot,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Transform puppetRoot,
            Vector3 puppetPosition,
            Quaternion puppetRotation)
        {
            if (!targetRoot || !puppetRoot) return;

            if (targetRoot != puppetRoot
                && IsDescendantOf(targetRoot, puppetRoot))
            {
                puppetRoot.SetPositionAndRotation(
                    puppetPosition,
                    puppetRotation);
                targetRoot.SetPositionAndRotation(
                    targetPosition,
                    targetRotation);
                return;
            }

            targetRoot.SetPositionAndRotation(
                targetPosition,
                targetRotation);
            if (puppetRoot != targetRoot)
            {
                puppetRoot.SetPositionAndRotation(
                    puppetPosition,
                    puppetRotation);
            }
        }

        internal static bool IsDescendantOf(
            Transform candidate,
            Transform ancestor)
        {
            if (!candidate || !ancestor) return false;

            for (Transform current = candidate.parent;
                current;
                current = current.parent)
            {
                if (current == ancestor) return true;
            }

            return false;
        }
    }

    internal struct RagdollTeleportRequest
    {
        internal Vector3 Position { get; private set; }
        internal Quaternion Rotation { get; private set; }
        internal bool MoveToTarget { get; private set; }

        internal static RagdollTeleportRequest Create(
            Vector3 position,
            Quaternion rotation,
            bool moveToTarget)
        {
            RagdollTeleportMath.ValidateFinite(position, nameof(position));
            return new RagdollTeleportRequest
            {
                Position = position,
                Rotation = RagdollTeleportMath.NormalizeRotation(rotation),
                MoveToTarget = moveToTarget
            };
        }
    }
}
