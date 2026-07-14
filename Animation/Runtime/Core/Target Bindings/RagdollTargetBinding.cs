using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Explicitly links one registered ragdoll bone to its animated target Transform.
    /// The captured offset converts poses in both directions without requiring matching
    /// Transform names or matching local bone axes.
    /// </summary>
    [Serializable]
    public sealed class RagdollTargetBinding
    {
        [SerializeField] BoneName bone;
        [SerializeField] Transform target;

        // Position is stored in the ragdoll bone's local space. Rotation converts a
        // ragdoll world rotation into the target bone's world rotation.
        [SerializeField, HideInInspector] Vector3 targetPositionOffset;
        [SerializeField, HideInInspector] Quaternion targetRotationOffset = Quaternion.identity;
        [SerializeField, HideInInspector] bool offsetsCaptured;

        public BoneName Bone => bone;
        public Transform Target => target;
        public Vector3 TargetPositionOffset => targetPositionOffset;
        public Quaternion TargetRotationOffset => GetNormalizedRotationOffset();
        public bool OffsetsCaptured => offsetsCaptured;

        public RagdollTargetBinding()
        {
            targetRotationOffset = Quaternion.identity;
        }

        internal RagdollTargetBinding(
            BoneName bone,
            Transform target,
            Transform ragdollBone)
            : this()
        {
            this.bone = bone;
            this.target = target;
            CaptureOffsets(ragdollBone);
        }

        internal void CaptureOffsets(Transform ragdollBone)
        {
            if (!target)
            {
                throw new InvalidOperationException(
                    "Cannot capture a target binding offset without a target Transform.");
            }

            if (!ragdollBone)
            {
                throw new ArgumentNullException(nameof(ragdollBone));
            }

            // Transform.InverseTransformPoint stores the target position in the ragdoll
            // bone's local space. Quaternion.Inverse produces the relative orientation.
            targetPositionOffset = ragdollBone.InverseTransformPoint(target.position);
            targetRotationOffset = NormalizeOrIdentity(
                Quaternion.Inverse(ragdollBone.rotation) * target.rotation);
            offsetsCaptured = true;
        }

        internal void InvalidateOffsets()
        {
            offsetsCaptured = false;
        }

        internal RagdollAnimator.AnimatedPose ConvertTargetPoseToRagdoll(
            RagdollAnimator.AnimatedPose targetPose,
            Transform ragdollReference)
        {
            ValidateRuntimeReferences(ragdollReference);

            Quaternion ragdollWorldRotation = NormalizeOrIdentity(
                targetPose.worldRotation * Quaternion.Inverse(GetNormalizedRotationOffset()));

            // TransformPoint applies scale to the captured local position. Reversing that
            // operation uses the current ragdoll scale and the desired ragdoll rotation.
            Vector3 scaledOffset = Vector3.Scale(
                targetPositionOffset,
                ragdollReference.lossyScale);

            Vector3 ragdollWorldPosition = targetPose.worldPosition
                - (ragdollWorldRotation * scaledOffset);

            return new RagdollAnimator.AnimatedPose
            {
                worldPosition = ragdollWorldPosition,
                worldRotation = ragdollWorldRotation,
                localRotation = ragdollWorldRotation
            };
        }

        internal void GetTargetWorldPose(
            Transform ragdollBone,
            out Vector3 targetWorldPosition,
            out Quaternion targetWorldRotation)
        {
            ValidateRuntimeReferences(ragdollBone);

            targetWorldPosition = ragdollBone.TransformPoint(targetPositionOffset);
            targetWorldRotation = NormalizeOrIdentity(
                ragdollBone.rotation * GetNormalizedRotationOffset());
        }

        void ValidateRuntimeReferences(Transform ragdollBone)
        {
            if (!target)
            {
                throw new InvalidOperationException(
                    "A RagdollTargetBinding has no target Transform.");
            }

            if (!ragdollBone)
            {
                throw new ArgumentNullException(nameof(ragdollBone));
            }

            if (!offsetsCaptured)
            {
                throw new InvalidOperationException(
                    "A RagdollTargetBinding must capture its offsets before it is used.");
            }
        }

        Quaternion GetNormalizedRotationOffset()
        {
            return NormalizeOrIdentity(targetRotationOffset);
        }

        static Quaternion NormalizeOrIdentity(Quaternion value)
        {
            float squareMagnitude =
                value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;

            if (squareMagnitude <= 0.0000001f)
            {
                return Quaternion.identity;
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(squareMagnitude);
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }
    }
}
