using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Independent position and rotation weights used when mapping the simulated ragdoll
    /// back to the animated target hierarchy.
    /// </summary>
    [Serializable]
    public struct RagdollMappingWeights
    {
        [Range(0f, 1f)] public float positionWeight;
        [Range(0f, 1f)] public float rotationWeight;

        public float PositionWeight => Sanitize(positionWeight);
        public float RotationWeight => Sanitize(rotationWeight);

        public static RagdollMappingWeights Full => new RagdollMappingWeights(1f, 1f);
        public static RagdollMappingWeights None => new RagdollMappingWeights(0f, 0f);

        public RagdollMappingWeights(float positionWeight, float rotationWeight)
        {
            this.positionWeight = Sanitize(positionWeight);
            this.rotationWeight = Sanitize(rotationWeight);
        }

        internal void Multiply(float positionMultiplier, float rotationMultiplier)
        {
            positionWeight = Sanitize(PositionWeight * Sanitize(positionMultiplier));
            rotationWeight = Sanitize(RotationWeight * Sanitize(rotationMultiplier));
        }

        internal void Clamp()
        {
            positionWeight = PositionWeight;
            rotationWeight = RotationWeight;
        }

        static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
        }
    }

    /// <summary>
    /// Overrides the default mapping weights for a single registered ragdoll bone.
    /// </summary>
    [Serializable]
    public struct BoneMappingOverride
    {
        public BoneName bone;
        public RagdollMappingWeights weights;
    }
}
