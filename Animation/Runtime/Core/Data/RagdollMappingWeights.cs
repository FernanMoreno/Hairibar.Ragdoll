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

        public float PositionWeight => Mathf.Clamp01(positionWeight);
        public float RotationWeight => Mathf.Clamp01(rotationWeight);

        public static RagdollMappingWeights Full => new RagdollMappingWeights(1f, 1f);
        public static RagdollMappingWeights None => new RagdollMappingWeights(0f, 0f);

        public RagdollMappingWeights(float positionWeight, float rotationWeight)
        {
            this.positionWeight = Mathf.Clamp01(positionWeight);
            this.rotationWeight = Mathf.Clamp01(rotationWeight);
        }

        internal void Multiply(float positionMultiplier, float rotationMultiplier)
        {
            positionWeight = Mathf.Clamp01(PositionWeight * Mathf.Clamp01(positionMultiplier));
            rotationWeight = Mathf.Clamp01(RotationWeight * Mathf.Clamp01(rotationMultiplier));
        }

        internal void Clamp()
        {
            positionWeight = PositionWeight;
            rotationWeight = RotationWeight;
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
