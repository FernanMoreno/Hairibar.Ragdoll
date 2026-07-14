using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Persistent per-bone state layered on top of authored animation and mapping values.
    /// Neutral values preserve the authored configuration exactly.
    /// </summary>
    [Serializable]
    public struct MuscleRuntimeState
    {
        [SerializeField] float positionAuthority;
        [SerializeField] float rotationAuthority;
        [SerializeField] float positionMappingAuthority;
        [SerializeField] float rotationMappingAuthority;
        [SerializeField] float positionSuppression;
        [SerializeField] float rotationSuppression;
        [SerializeField] float positionDampingMultiplier;
        [SerializeField] float rotationDampingMultiplier;
        [SerializeField] float maxLinearAccelerationMultiplier;
        [SerializeField] float maxAngularAccelerationMultiplier;

        public float PositionAuthority => positionAuthority;
        public float RotationAuthority => rotationAuthority;
        public float PositionMappingAuthority => positionMappingAuthority;
        public float RotationMappingAuthority => rotationMappingAuthority;
        public float PositionSuppression => positionSuppression;
        public float RotationSuppression => rotationSuppression;
        public float EffectivePositionAuthority => positionAuthority * (1f - positionSuppression);
        public float EffectiveRotationAuthority => rotationAuthority * (1f - rotationSuppression);
        public float PositionDampingMultiplier => positionDampingMultiplier;
        public float RotationDampingMultiplier => rotationDampingMultiplier;
        public float MaxLinearAccelerationMultiplier => maxLinearAccelerationMultiplier;
        public float MaxAngularAccelerationMultiplier => maxAngularAccelerationMultiplier;

        public static MuscleRuntimeState Default
        {
            get
            {
                return new MuscleRuntimeState
                {
                    positionAuthority = 1f,
                    rotationAuthority = 1f,
                    positionMappingAuthority = 1f,
                    rotationMappingAuthority = 1f,
                    positionSuppression = 0f,
                    rotationSuppression = 0f,
                    positionDampingMultiplier = 1f,
                    rotationDampingMultiplier = 1f,
                    maxLinearAccelerationMultiplier = 1f,
                    maxAngularAccelerationMultiplier = 1f
                };
            }
        }

        internal void SetAuthorities(float position, float rotation)
        {
            positionAuthority = Mathf.Clamp01(position);
            rotationAuthority = Mathf.Clamp01(rotation);
        }

        internal void SetMappingAuthorities(float position, float rotation)
        {
            positionMappingAuthority = Mathf.Clamp01(position);
            rotationMappingAuthority = Mathf.Clamp01(rotation);
        }

        internal void SetDriveMultipliers(
            float positionDamping,
            float rotationDamping,
            float maxLinearAcceleration,
            float maxAngularAcceleration)
        {
            positionDampingMultiplier = Mathf.Max(0f, positionDamping);
            rotationDampingMultiplier = Mathf.Max(0f, rotationDamping);
            maxLinearAccelerationMultiplier = Mathf.Max(0f, maxLinearAcceleration);
            maxAngularAccelerationMultiplier = Mathf.Max(0f, maxAngularAcceleration);
        }

        internal void AccumulateSuppression(float position, float rotation)
        {
            positionSuppression = Accumulate(positionSuppression, position);
            rotationSuppression = Accumulate(rotationSuppression, rotation);
        }

        internal void Recover(float positionRecoveryRate, float rotationRecoveryRate, float dt)
        {
            if (dt <= 0f) return;

            positionSuppression = Mathf.MoveTowards(
                positionSuppression,
                0f,
                Mathf.Max(0f, positionRecoveryRate) * dt);

            rotationSuppression = Mathf.MoveTowards(
                rotationSuppression,
                0f,
                Mathf.Max(0f, rotationRecoveryRate) * dt);
        }

        internal void ApplyTo(ref BoneProfile profile)
        {
            ApplyTo(ref profile, 0f);
        }

        internal void ApplyTo(
            ref BoneProfile profile,
            float minimumSuppressionAuthority)
        {
            // The minimum applies only to temporary suppression. Explicit persistent
            // authority remains authoritative and can still disable the channel.
            float suppressionAuthority = Mathf.Max(
                1f - positionSuppression,
                Mathf.Clamp01(minimumSuppressionAuthority));

            profile.positionAlpha *= positionAuthority * suppressionAuthority;
            profile.rotationAlpha *= EffectiveRotationAuthority;

            profile.positionDampingRatio *= positionDampingMultiplier;
            profile.rotationDampingRatio *= rotationDampingMultiplier;

            profile.maxLinearAcceleration = ScaleLimit(
                profile.maxLinearAcceleration,
                maxLinearAccelerationMultiplier);

            profile.maxAngularAcceleration = ScaleLimit(
                profile.maxAngularAcceleration,
                maxAngularAccelerationMultiplier);
        }

        internal void ApplyTo(ref RagdollMappingWeights mappingWeights)
        {
            mappingWeights.Multiply(
                positionMappingAuthority,
                rotationMappingAuthority);
        }

        internal static float Accumulate(float current, float incoming)
        {
            current = Mathf.Clamp01(current);
            incoming = Mathf.Clamp01(incoming);
            return 1f - ((1f - current) * (1f - incoming));
        }

        static float ScaleLimit(float value, float multiplier)
        {
            multiplier = Mathf.Max(0f, multiplier);
            if (multiplier == 0f) return 0f;
            if (float.IsInfinity(value)) return value;
            return value * multiplier;
        }
    }
}
