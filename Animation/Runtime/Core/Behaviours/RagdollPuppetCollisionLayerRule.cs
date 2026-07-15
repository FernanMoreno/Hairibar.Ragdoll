using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// First-match layer override for BehaviourPuppet collision resistance and threshold.
    /// Array order is intentional when layer masks overlap.
    /// </summary>
    [Serializable]
    public sealed class RagdollPuppetCollisionLayerRule
    {
        public LayerMask layers;
        [Min(0.001f)] public float resistanceMultiplier = 1f;
        public bool overrideCollisionThreshold;
        [Min(0f)] public float collisionThreshold;

        internal bool Matches(int layer)
        {
            return layer >= 0
                && layer < 32
                && (layers.value & (1 << layer)) != 0;
        }

        internal void Normalize()
        {
            resistanceMultiplier =
                RagdollPuppetCollisionResponseMath.SanitizePositive(
                    resistanceMultiplier,
                    1f);
            collisionThreshold =
                RagdollPuppetCollisionResponseMath.SanitizeNonNegative(
                    collisionThreshold);
        }
    }
}
