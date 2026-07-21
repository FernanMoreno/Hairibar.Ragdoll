using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollPropMeleeShape
    {
        Box,
        Capsule
    }

    /// <summary>Frozen per-pickup melee configuration for a RagdollProp.</summary>
    [Serializable]
    public sealed class RagdollPropMeleeSettings
    {
        const float MinimumDimension = 0.0001f;

        [SerializeField] bool enabled = true;
        [SerializeField] RagdollPropMeleeShape shape = RagdollPropMeleeShape.Capsule;
        [SerializeField] Vector3 center;
        [SerializeField] Vector3 boxSize = Vector3.one;
        [SerializeField, Min(MinimumDimension)] float radius = 0.1f;
        [SerializeField, Min(MinimumDimension)] float height = 1f;
        [SerializeField, Range(0, 2)] int capsuleDirection = 2;
        [SerializeField, Min(MinimumDimension)] float actionColliderRadiusMultiplier = 1.5f;
        [SerializeField, Min(0f)] float actionPinWeightMultiplier = 1.5f;
        [SerializeField, Min(MinimumDimension)] float actionMassMultiplier = 1f;
        [SerializeField] Vector3 centerOfMassOffset;

        public bool Enabled { get => enabled; set => enabled = value; }
        public RagdollPropMeleeShape Shape
        {
            get => shape;
            set => shape = IsSupportedShape(value)
                ? value
                : RagdollPropMeleeShape.Capsule;
        }
        public Vector3 Center { get => center; set => center = SanitizeVector(value); }
        public Vector3 BoxSize { get => boxSize; set => boxSize = SanitizeSize(value); }
        public float Radius { get => radius; set => radius = SanitizeDimension(value); }
        public float Height
        {
            get => height;
            set => height = Mathf.Max(
                SanitizeDimension(value),
                SanitizeDimension(radius) * 2f);
        }
        public int CapsuleDirection
        {
            get => capsuleDirection;
            set => capsuleDirection = Mathf.Clamp(value, 0, 2);
        }
        public float ActionColliderRadiusMultiplier
        {
            get => actionColliderRadiusMultiplier;
            set => actionColliderRadiusMultiplier = SanitizeDimension(value);
        }
        public float ActionPinWeightMultiplier
        {
            get => actionPinWeightMultiplier;
            set => actionPinWeightMultiplier = SanitizeNonNegative(value);
        }
        public float ActionMassMultiplier
        {
            get => actionMassMultiplier;
            set => actionMassMultiplier = SanitizeDimension(value, 1f);
        }
        public Vector3 CenterOfMassOffset
        {
            get => centerOfMassOffset;
            set => centerOfMassOffset = SanitizeVector(value);
        }

        internal void Normalize()
        {
            if (!IsSupportedShape(shape))
            {
                shape = RagdollPropMeleeShape.Capsule;
            }
            center = SanitizeVector(center);
            boxSize = SanitizeSize(boxSize);
            radius = SanitizeDimension(radius);
            height = Mathf.Max(SanitizeDimension(height), radius * 2f);
            capsuleDirection = Mathf.Clamp(capsuleDirection, 0, 2);
            actionColliderRadiusMultiplier =
                SanitizeDimension(actionColliderRadiusMultiplier, 1f);
            actionPinWeightMultiplier =
                SanitizeNonNegative(actionPinWeightMultiplier);
            actionMassMultiplier = SanitizeDimension(actionMassMultiplier, 1f);
            centerOfMassOffset = SanitizeVector(centerOfMassOffset);
        }

        internal RagdollPropMeleeSnapshot Capture()
        {
            Normalize();
            return new RagdollPropMeleeSnapshot(
                enabled,
                shape,
                center,
                boxSize,
                radius,
                height,
                capsuleDirection,
                actionColliderRadiusMultiplier,
                actionPinWeightMultiplier,
                actionMassMultiplier,
                centerOfMassOffset);
        }

        internal bool TryValidate(out string error)
        {
            Normalize();
            error = null;
            if (!IsSupportedShape(shape))
            {
                error = "Unsupported melee collider shape.";
                return false;
            }
            if (!IsFinite(center) || !IsFinite(boxSize)
                || !IsFinite(centerOfMassOffset))
            {
                error = "Melee vectors must contain only finite values.";
                return false;
            }
            return true;
        }

        static bool IsSupportedShape(RagdollPropMeleeShape value)
        {
            return value == RagdollPropMeleeShape.Box
                || value == RagdollPropMeleeShape.Capsule;
        }

        static float SanitizeNonNegative(float value)
        {
            return Mathf.Max(0f, SanitizeFinite(value, 0f));
        }

        static float SanitizeDimension(float value, float fallback = MinimumDimension)
        {
            return Mathf.Max(MinimumDimension, SanitizeFinite(value, fallback));
        }

        static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }

        static Vector3 SanitizeVector(Vector3 value)
        {
            return new Vector3(
                SanitizeFinite(value.x, 0f),
                SanitizeFinite(value.y, 0f),
                SanitizeFinite(value.z, 0f));
        }

        static Vector3 SanitizeSize(Vector3 value)
        {
            value = SanitizeVector(value);
            return new Vector3(
                Mathf.Max(MinimumDimension, Mathf.Abs(value.x)),
                Mathf.Max(MinimumDimension, Mathf.Abs(value.y)),
                Mathf.Max(MinimumDimension, Mathf.Abs(value.z)));
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal struct RagdollPropMeleeSnapshot
    {
        internal readonly bool Enabled;
        internal readonly RagdollPropMeleeShape Shape;
        internal readonly Vector3 Center;
        internal readonly Vector3 BoxSize;
        internal readonly float Radius;
        internal readonly float Height;
        internal readonly int CapsuleDirection;
        internal readonly float ActionColliderRadiusMultiplier;
        internal readonly float ActionPinWeightMultiplier;
        internal readonly float ActionMassMultiplier;
        internal readonly Vector3 CenterOfMassOffset;

        internal bool HasCenterOfMassOffset =>
            CenterOfMassOffset.sqrMagnitude > 0.00000001f;

        internal RagdollPropMeleeSnapshot(
            bool enabled,
            RagdollPropMeleeShape shape,
            Vector3 center,
            Vector3 boxSize,
            float radius,
            float height,
            int capsuleDirection,
            float colliderMlp,
            float pinMlp,
            float massMlp,
            Vector3 centerOfMassOffset)
        {
            Enabled = enabled;
            Shape = shape;
            Center = center;
            BoxSize = boxSize;
            Radius = radius;
            Height = Mathf.Max(height, radius * 2f);
            CapsuleDirection = Mathf.Clamp(capsuleDirection, 0, 2);
            ActionColliderRadiusMultiplier = colliderMlp;
            ActionPinWeightMultiplier = pinMlp;
            ActionMassMultiplier = massMlp;
            CenterOfMassOffset = centerOfMassOffset;
        }

        internal static RagdollPropMeleeSnapshot Disabled =>
            new RagdollPropMeleeSnapshot(
                false,
                RagdollPropMeleeShape.Box,
                Vector3.zero,
                Vector3.one,
                0.0001f,
                0.0002f,
                2,
                1f,
                1f,
                1f,
                Vector3.zero);
    }
}
