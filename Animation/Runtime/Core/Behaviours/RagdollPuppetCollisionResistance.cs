using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Global collision resistance used by RagdollPuppetBehaviour. The optional curve maps
    /// sampled Target speed to an absolute resistance value, not to an additional multiplier.
    /// </summary>
    [Serializable]
    public sealed class RagdollPuppetCollisionResistance
    {
        [Tooltip("Constant resistance used when the Target-speed curve is disabled or invalid.")]
        [Min(0.001f)] public float constantResistance = 3f;

        [Tooltip("Evaluate Target Speed Resistance instead of using Constant Resistance.")]
        public bool useTargetSpeedCurve;

        [Tooltip("X is the impacted Target bone's sampled linear speed. Y is resistance.")]
        public AnimationCurve targetSpeedResistance = new AnimationCurve(
            new Keyframe(0f, 3f),
            new Keyframe(10f, 3f));

        public float Evaluate(float targetSpeed)
        {
            return RagdollPuppetCollisionResponseMath.EvaluateGlobalResistance(
                constantResistance,
                useTargetSpeedCurve,
                targetSpeedResistance,
                targetSpeed);
        }

        internal void Normalize()
        {
            constantResistance =
                RagdollPuppetCollisionResponseMath.SanitizePositive(
                    constantResistance,
                    3f);

            if (targetSpeedResistance == null)
            {
                targetSpeedResistance = new AnimationCurve(
                    new Keyframe(0f, constantResistance),
                    new Keyframe(10f, constantResistance));
            }
        }
    }
}
