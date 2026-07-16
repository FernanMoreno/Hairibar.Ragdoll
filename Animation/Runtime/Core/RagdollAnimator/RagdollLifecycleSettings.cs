using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Settings used when blending Alive to Dead and optionally suspending a settled
    /// Dead ragdoll as Frozen.
    /// </summary>
    [Serializable]
    public struct RagdollLifecycleSettings
    {
        const int CurrentSerializationVersion = 1;

        [SerializeField, Min(0f)] float killDuration;
        [SerializeField, Range(0f, 1f)] float deadMuscleWeight;
        [SerializeField, Min(0f)] float deadMuscleDamper;
        [SerializeField, Min(0f)] float maxFreezeSqrVelocity;
        [SerializeField] bool freezePermanently;
        [SerializeField] bool enableAngularLimitsOnKill;
        [SerializeField] bool enableInternalCollisionsOnKill;
        [SerializeField, HideInInspector] int serializationVersion;

        public float KillDuration => killDuration;
        public float DeadMuscleWeight => deadMuscleWeight;
        public float DeadMuscleDamper => deadMuscleDamper;
        public float MaxFreezeSqrVelocity => maxFreezeSqrVelocity;
        public bool FreezePermanently => freezePermanently;
        public bool EnableAngularLimitsOnKill => enableAngularLimitsOnKill;
        public bool EnableInternalCollisionsOnKill =>
            enableInternalCollisionsOnKill;

        public RagdollLifecycleSettings(
            float killDuration,
            float deadMuscleWeight = 0.01f,
            float deadMuscleDamper = 2f,
            float maxFreezeSqrVelocity = 0.02f,
            bool freezePermanently = false,
            bool enableAngularLimitsOnKill = true,
            bool enableInternalCollisionsOnKill = true)
        {
            this.killDuration = killDuration;
            this.deadMuscleWeight = deadMuscleWeight;
            this.deadMuscleDamper = deadMuscleDamper;
            this.maxFreezeSqrVelocity = maxFreezeSqrVelocity;
            this.freezePermanently = freezePermanently;
            this.enableAngularLimitsOnKill = enableAngularLimitsOnKill;
            this.enableInternalCollisionsOnKill =
                enableInternalCollisionsOnKill;
            serializationVersion = CurrentSerializationVersion;
            Normalize();
        }

        public static RagdollLifecycleSettings Default =>
            new RagdollLifecycleSettings(
                1f,
                0.01f,
                2f,
                0.02f,
                false,
                true,
                true);

        internal void Normalize()
        {
            // Sprint 0026 serialized only the first three fields. Missing fields must
            // migrate to the published lifecycle defaults without changing intentional
            // zero values created through the new constructor.
            if (serializationVersion < CurrentSerializationVersion)
            {
                maxFreezeSqrVelocity = 0.02f;
                freezePermanently = false;
                enableAngularLimitsOnKill = true;
                enableInternalCollisionsOnKill = true;
                serializationVersion = CurrentSerializationVersion;
            }

            killDuration = RagdollLifecycleMath.SanitizeNonNegative(
                killDuration,
                1f);
            deadMuscleWeight = RagdollLifecycleMath.SanitizeWeight(
                deadMuscleWeight,
                0.01f);
            deadMuscleDamper = RagdollLifecycleMath.SanitizeNonNegative(
                deadMuscleDamper,
                2f);
            maxFreezeSqrVelocity =
                RagdollLifecycleMath.SanitizeNonNegative(
                    maxFreezeSqrVelocity,
                    0.02f);
        }
    }
}
