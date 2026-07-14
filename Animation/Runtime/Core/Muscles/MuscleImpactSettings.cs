using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Temporary authority loss caused by a localized impact.
    /// Propagation uses topological edge distance, not Transform traversal.
    /// </summary>
    [Serializable]
    public struct MuscleImpactSettings
    {
        [Range(0f, 1f)] public float positionSuppression;
        [Range(0f, 1f)] public float rotationSuppression;
        [Min(0)] public int maximumPropagationDistance;
        [Range(0f, 1f)] public float propagationFalloff;

        internal float GetPropagationWeight(int distance)
        {
            if (distance < 0 || distance > Mathf.Max(0, maximumPropagationDistance))
            {
                return 0f;
            }

            if (distance == 0)
            {
                return 1f;
            }

            return Mathf.Pow(Mathf.Clamp01(propagationFalloff), distance);
        }
    }
}
