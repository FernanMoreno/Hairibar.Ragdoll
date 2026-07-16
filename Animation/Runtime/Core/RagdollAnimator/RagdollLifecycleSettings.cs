using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Settings used when blending an Alive ragdoll into Dead.</summary>
    [Serializable]
    public struct RagdollLifecycleSettings
    {
        [SerializeField, Min(0f)] float killDuration;
        [SerializeField, Range(0f, 1f)] float deadMuscleWeight;
        [SerializeField, Min(0f)] float deadMuscleDamper;

        public float KillDuration => killDuration;
        public float DeadMuscleWeight => deadMuscleWeight;
        public float DeadMuscleDamper => deadMuscleDamper;

        public RagdollLifecycleSettings(
            float killDuration,
            float deadMuscleWeight = 0.01f,
            float deadMuscleDamper = 2f)
        {
            this.killDuration = killDuration;
            this.deadMuscleWeight = deadMuscleWeight;
            this.deadMuscleDamper = deadMuscleDamper;
            Normalize();
        }

        public static RagdollLifecycleSettings Default =>
            new RagdollLifecycleSettings(1f, 0.01f, 2f);

        internal void Normalize()
        {
            killDuration = RagdollLifecycleMath.SanitizeNonNegative(
                killDuration,
                1f);
            deadMuscleWeight = RagdollLifecycleMath.SanitizeWeight(
                deadMuscleWeight,
                0.01f);
            deadMuscleDamper = RagdollLifecycleMath.SanitizeNonNegative(
                deadMuscleDamper,
                2f);
        }
    }
}
