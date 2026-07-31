using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollAuthoringColliderShape
    {
        Capsule,
        Box,
        Sphere
    }

    public enum RagdollAuthoringMassDistribution
    {
        Biometric,
        ColliderVolume
    }

    [Serializable]
    public struct RagdollAuthoringOptions
    {
        [Min(0.001f)] public float totalMass;
        public RagdollAuthoringMassDistribution massDistribution;
        [Min(0.01f)] public float colliderRadiusScale;
        public float colliderLengthOverlap;
        [Min(0f)] public float minimumColliderSize;
        public bool includeSpine;
        public bool includeChest;
        public bool includeHands;
        public bool includeFeet;
        public RagdollAuthoringColliderShape torsoColliders;
        public RagdollAuthoringColliderShape headCollider;
        public RagdollAuthoringColliderShape armColliders;
        public RagdollAuthoringColliderShape handColliders;
        public RagdollAuthoringColliderShape legColliders;
        public RagdollAuthoringColliderShape footColliders;
        [Range(0.01f, 2f)] public float jointRangeMultiplier;
        [Range(0f, 177f)] public float angularXLimit;
        [Range(0f, 177f)] public float angularYZLimit;
        public bool enablePreprocessing;
        public bool enableProjection;

        public static RagdollAuthoringOptions Default => new RagdollAuthoringOptions
        {
            totalMass = 70f,
            massDistribution = RagdollAuthoringMassDistribution.Biometric,
            colliderRadiusScale = 0.22f,
            colliderLengthOverlap = 0.1f,
            minimumColliderSize = 0.01f,
            includeSpine = true,
            includeChest = true,
            includeHands = true,
            includeFeet = true,
            torsoColliders = RagdollAuthoringColliderShape.Box,
            headCollider = RagdollAuthoringColliderShape.Capsule,
            armColliders = RagdollAuthoringColliderShape.Capsule,
            handColliders = RagdollAuthoringColliderShape.Box,
            legColliders = RagdollAuthoringColliderShape.Capsule,
            footColliders = RagdollAuthoringColliderShape.Box,
            jointRangeMultiplier = 1f,
            angularXLimit = 45f,
            angularYZLimit = 30f,
            enablePreprocessing = false,
            enableProjection = false
        };

        public void Normalize()
        {
            totalMass = FinitePositive(totalMass, 70f, 0.001f);
            if (!Enum.IsDefined(typeof(RagdollAuthoringMassDistribution), massDistribution))
            {
                massDistribution = RagdollAuthoringMassDistribution.Biometric;
            }
            colliderRadiusScale = FinitePositive(colliderRadiusScale, 0.22f, 0.01f);
            colliderLengthOverlap = Finite(colliderLengthOverlap, 0.1f);
            minimumColliderSize = FinitePositive(minimumColliderSize, 0.01f, 0.001f);
            jointRangeMultiplier = Mathf.Clamp(jointRangeMultiplier, 0.01f, 2f);
            angularXLimit = Mathf.Clamp(angularXLimit, 0f, 177f);
            angularYZLimit = Mathf.Clamp(angularYZLimit, 0f, 177f);
        }

        static float Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }

        static float FinitePositive(float value, float fallback, float minimum)
        {
            return Mathf.Max(minimum, Finite(value, fallback));
        }
    }
}
