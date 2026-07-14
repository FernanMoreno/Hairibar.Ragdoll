using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Authored behaviour properties for one semantic muscle group. Runtime state remains
    /// owned by RagdollMuscleController and multiplies these authored limits.
    /// </summary>
    [Serializable]
    public struct RagdollMuscleBehaviourSettings
    {
        [Range(0f, 1f)] public float unpinParents;
        [Range(0f, 1f)] public float unpinChildren;
        [Range(0f, 1f)] public float unpinGroup;
        [Range(0f, 1f)] public float minimumPositionAuthority;
        [Range(0f, 1f)] public float minimumMappingAuthority;
        [Range(0f, 1f)] public float maximumMappingAuthority;
        [Min(0f)] public float regainPositionAuthorityMultiplier;
        [Min(0.001f)] public float collisionResistance;
        [Min(0f)] public float knockOutDistance;

        public static RagdollMuscleBehaviourSettings Default
        {
            get
            {
                return new RagdollMuscleBehaviourSettings
                {
                    unpinParents = 1f,
                    unpinChildren = 1f,
                    unpinGroup = 1f,
                    minimumPositionAuthority = 0f,
                    minimumMappingAuthority = 1f,
                    maximumMappingAuthority = 1f,
                    regainPositionAuthorityMultiplier = 1f,
                    collisionResistance = 1f,
                    knockOutDistance = 1f
                };
            }
        }

        internal void Normalize()
        {
            unpinParents = Mathf.Clamp01(unpinParents);
            unpinChildren = Mathf.Clamp01(unpinChildren);
            unpinGroup = Mathf.Clamp01(unpinGroup);
            minimumPositionAuthority = Mathf.Clamp01(minimumPositionAuthority);
            minimumMappingAuthority = Mathf.Clamp01(minimumMappingAuthority);
            maximumMappingAuthority = Mathf.Clamp(
                maximumMappingAuthority,
                minimumMappingAuthority,
                1f);
            regainPositionAuthorityMultiplier = Mathf.Max(
                0f,
                regainPositionAuthorityMultiplier);
            collisionResistance = Mathf.Max(0.001f, collisionResistance);
            knockOutDistance = Mathf.Max(0f, knockOutDistance);
        }

        internal float ScaleCollisionSuppression(float suppression)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(suppression)
                / Mathf.Max(0.001f, collisionResistance));
        }

        internal float EvaluateMappingAuthority(float unpinAmount)
        {
            return Mathf.Lerp(
                Mathf.Clamp01(minimumMappingAuthority),
                Mathf.Clamp(
                    maximumMappingAuthority,
                    Mathf.Clamp01(minimumMappingAuthority),
                    1f),
                Mathf.Clamp01(unpinAmount));
        }

        internal float GetPropagationMultiplier(RagdollMuscleRelation relation)
        {
            switch (relation)
            {
                case RagdollMuscleRelation.Self:
                    return 1f;
                case RagdollMuscleRelation.Parent:
                    return Mathf.Clamp01(unpinParents);
                case RagdollMuscleRelation.Child:
                    return Mathf.Clamp01(unpinChildren);
                case RagdollMuscleRelation.SameGroup:
                    return Mathf.Clamp01(unpinGroup);
                default:
                    return 0f;
            }
        }
    }
}
