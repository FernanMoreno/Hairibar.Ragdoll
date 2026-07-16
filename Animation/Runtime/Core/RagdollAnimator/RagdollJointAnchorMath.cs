using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Pure geometry used to align a child joint anchor with its connected body in a
    /// desired ragdoll pose. Scale is handled component-wise so flat and tree
    /// hierarchies, non-zero child anchors and non-uniform rig scale share one path.
    /// </summary>
    public static class RagdollJointAnchorMath
    {
        const float MinimumScaleMagnitude = 0.000001f;
        const float MinimumQuaternionSquareMagnitude = 0.0000001f;

        public static bool ShouldUpdateAnchor(
            bool updateJointAnchors,
            bool supportTranslationAnimation,
            bool directTargetParent)
        {
            return updateJointAnchors
                && (supportTranslationAnimation || !directTargetParent);
        }

        public static bool TryResolveConnectedAnchor(
            Vector3 childBodyPosition,
            Quaternion childBodyRotation,
            Vector3 childBodyScale,
            Vector3 childAnchor,
            Vector3 connectedBodyPosition,
            Quaternion connectedBodyRotation,
            Vector3 connectedBodyScale,
            out Vector3 connectedAnchor)
        {
            connectedAnchor = Vector3.zero;

            if (!IsFinite(childBodyPosition)
                || !IsFinite(childBodyScale)
                || !IsFinite(childAnchor)
                || !IsFinite(connectedBodyPosition)
                || !IsFinite(connectedBodyScale)
                || !TryNormalize(childBodyRotation, out childBodyRotation)
                || !TryNormalize(
                    connectedBodyRotation,
                    out connectedBodyRotation)
                || !HasInvertibleScale(connectedBodyScale))
            {
                return false;
            }

            Vector3 scaledChildAnchor = Vector3.Scale(
                childAnchor,
                childBodyScale);
            Vector3 worldAnchor = childBodyPosition
                + childBodyRotation * scaledChildAnchor;
            Vector3 connectedLocalScaled =
                Quaternion.Inverse(connectedBodyRotation)
                * (worldAnchor - connectedBodyPosition);

            connectedAnchor = new Vector3(
                connectedLocalScaled.x / connectedBodyScale.x,
                connectedLocalScaled.y / connectedBodyScale.y,
                connectedLocalScaled.z / connectedBodyScale.z);

            if (!IsFinite(connectedAnchor))
            {
                connectedAnchor = Vector3.zero;
                return false;
            }

            return true;
        }

        static bool HasInvertibleScale(Vector3 scale)
        {
            return Mathf.Abs(scale.x) > MinimumScaleMagnitude
                && Mathf.Abs(scale.y) > MinimumScaleMagnitude
                && Mathf.Abs(scale.z) > MinimumScaleMagnitude;
        }

        static bool TryNormalize(
            Quaternion value,
            out Quaternion normalized)
        {
            normalized = Quaternion.identity;
            if (!IsFinite(value)) return false;

            float squareMagnitude =
                value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;
            if (squareMagnitude <= MinimumQuaternionSquareMagnitude)
            {
                return false;
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(squareMagnitude);
            normalized = new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
            return true;
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        static bool IsFinite(Quaternion value)
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
}
