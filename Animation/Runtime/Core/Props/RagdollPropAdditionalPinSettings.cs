using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Authoring contract for the optional second prop pin. The offset is expressed in
    /// prop-slot local space, weight composes with the effective Prop muscle position
    /// authority and mass controls the impulse strength independently from slot mass.
    /// Values are captured at pickup so a held transaction remains deterministic.
    /// </summary>
    [Serializable]
    public sealed class RagdollPropAdditionalPinSettings
    {
        [SerializeField]
        [Tooltip("Enables the virtual second pin while the prop is held.")]
        bool enabled;

        [SerializeField]
        [Tooltip("Additional pin point in prop-slot local space. A non-zero offset lets the pin generate torque as well as linear correction.")]
        Vector3 localOffset;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Additional pin authority before the current Prop muscle position authority is applied.")]
        float weight = 1f;

        [SerializeField, Min(0.0001f)]
        [Tooltip("Virtual mass used to convert desired point velocity change into an impulse. This does not change the Rigidbody mass.")]
        float mass = 1f;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public Vector3 LocalOffset
        {
            get => localOffset;
            set => localOffset = SanitizeVector(value);
        }

        public float Weight
        {
            get => weight;
            set => weight = SanitizeWeight(value);
        }

        public float Mass
        {
            get => mass;
            set => mass = SanitizeMass(value);
        }

        public RagdollPropAdditionalPinSettings()
        {
        }

        public RagdollPropAdditionalPinSettings(
            bool enabled,
            Vector3 localOffset,
            float weight,
            float mass)
        {
            this.enabled = enabled;
            this.localOffset = SanitizeVector(localOffset);
            this.weight = SanitizeWeight(weight);
            this.mass = SanitizeMass(mass);
        }

        internal RagdollPropAdditionalPinSnapshot Capture()
        {
            Normalize();
            return new RagdollPropAdditionalPinSnapshot(
                enabled,
                localOffset,
                weight,
                mass);
        }

        internal void Normalize()
        {
            localOffset = SanitizeVector(localOffset);
            weight = SanitizeWeight(weight);
            mass = SanitizeMass(mass);
        }

        internal static float SanitizeWeight(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        internal static float SanitizeMass(float value)
        {
            return IsFinite(value) && value > 0f ? value : 1f;
        }

        internal static Vector3 SanitizeVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z)
                ? value
                : Vector3.zero;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal struct RagdollPropAdditionalPinSnapshot
    {
        internal readonly bool Enabled;
        internal readonly Vector3 LocalOffset;
        internal readonly float Weight;
        internal readonly float Mass;

        internal RagdollPropAdditionalPinSnapshot(
            bool enabled,
            Vector3 localOffset,
            float weight,
            float mass)
        {
            Enabled = enabled;
            LocalOffset = RagdollPropAdditionalPinSettings
                .SanitizeVector(localOffset);
            Weight = RagdollPropAdditionalPinSettings.SanitizeWeight(weight);
            Mass = RagdollPropAdditionalPinSettings.SanitizeMass(mass);
        }

        internal static RagdollPropAdditionalPinSnapshot Disabled =>
            new RagdollPropAdditionalPinSnapshot(
                false,
                Vector3.zero,
                0f,
                1f);
    }
}
