using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        /// <summary>
        /// Global multiplier applied to position and rotation mapping for every bone.
        /// A value of zero leaves the target fully animated; one uses the configured
        /// per-bone mapping weights.
        /// </summary>
        public float MasterMappingWeight
        {
            get => _masterMappingWeight;
            set => _masterMappingWeight = Mathf.Clamp01(value);
        }

        [SerializeField, Range(0f, 1f)] float _masterMappingWeight = 1f;
        [SerializeField] RagdollMappingWeights _defaultMappingWeights = new RagdollMappingWeights(1f, 1f);
        [SerializeField] BoneMappingOverride[] _mappingOverrides = new BoneMappingOverride[0];

        AnimatedPair[] animatedPairsByHandleIndex;
        IRagdollMappingModifier[] mappingModifiers;

        /// <summary>
        /// Gets the authored mapping weights currently assigned to a bone.
        /// Runtime mapping modifiers are not included in the returned value.
        /// </summary>
        public RagdollMappingWeights GetBoneMappingWeights(RagdollBoneHandle bone)
        {
            return GetAnimatedPair(bone).MappingWeights;
        }

        /// <summary>
        /// Replaces the authored mapping weights for a bone at runtime.
        /// </summary>
        public void SetBoneMappingWeights(RagdollBoneHandle bone, RagdollMappingWeights mappingWeights)
        {
            mappingWeights.Clamp();
            GetAnimatedPair(bone).MappingWeights = mappingWeights;
        }

        /// <summary>
        /// Reapplies the serialized defaults and overrides to all initialized pairs.
        /// Useful after changing mapping settings through scripts at runtime.
        /// </summary>
        public void RebuildMappingWeights()
        {
            if (animatedPairs == null) return;

            foreach (AnimatedPair pair in animatedPairs)
            {
                pair.MappingWeights = GetConfiguredMappingWeights(pair.Name);
            }
        }

        void MapRagdollToTarget()
        {
            mapper.MapRagdollToTarget(
                animatedPairs,
                _masterMappingWeight,
                mappingModifiers);
        }

        void GatherMappingModifiers()
        {
            mappingModifiers = GetComponents<IRagdollMappingModifier>();
        }

        internal RagdollMappingWeights GetConfiguredMappingWeights(BoneName bone)
        {
            RagdollMappingWeights result = _defaultMappingWeights;
            result.Clamp();

            if (_mappingOverrides == null) return result;

            for (int i = 0; i < _mappingOverrides.Length; i++)
            {
                if (_mappingOverrides[i].bone != bone) continue;

                result = _mappingOverrides[i].weights;
                result.Clamp();
            }

            return result;
        }

        internal void InitializeAnimatedPairLookup()
        {
            animatedPairsByHandleIndex = new AnimatedPair[Bindings.BoneCount];

            foreach (AnimatedPair pair in animatedPairs)
            {
                if (animatedPairsByHandleIndex[pair.Handle.Index] != null)
                {
                    throw new InvalidOperationException(
                        "Multiple animated pairs reference the same registered ragdoll bone.");
                }

                animatedPairsByHandleIndex[pair.Handle.Index] = pair;
            }
        }

        AnimatedPair GetAnimatedPair(RagdollBoneHandle bone)
        {
            if (animatedPairsByHandleIndex == null)
            {
                throw new InvalidOperationException(
                    "RagdollAnimator mapping is not initialized yet.");
            }

            if (!Bindings.Topology.Contains(bone))
            {
                throw new ArgumentException(
                    "The supplied bone handle does not belong to this RagdollAnimator.",
                    nameof(bone));
            }

            AnimatedPair pair = animatedPairsByHandleIndex[bone.Index];
            if (pair == null)
            {
                throw new ArgumentException(
                    "The supplied registered bone has no animated target pair.",
                    nameof(bone));
            }

            return pair;
        }
    }
}
