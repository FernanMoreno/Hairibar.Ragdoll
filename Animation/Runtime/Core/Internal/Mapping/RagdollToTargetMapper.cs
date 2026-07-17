using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Maps the simulated ragdoll pose back to explicitly bound Target Transforms using
    /// independent position and rotation weights.
    /// </summary>
    internal sealed class RagdollToTargetMapper
    {
        public IReadOnlyCollection<RagdollBoneTargetBonePair> BonePairs => bonePairs;

        readonly RagdollBoneTargetBonePair[] bonePairs;

        public void MapRagdollToTarget(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs,
            float masterMappingWeight,
            IReadOnlyList<IRagdollMappingModifier> modifiers)
        {
            MapRagdollToTarget(
                pairs,
                masterMappingWeight,
                modifiers,
                null);
        }

        internal void MapRagdollToTarget(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs,
            float masterMappingWeight,
            IReadOnlyList<IRagdollMappingModifier> modifiers,
            bool[] suppressedBones)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (suppressedBones != null
                && suppressedBones.Length != pairs.Count)
            {
                throw new ArgumentException(
                    "The suppressed-bone mask must match the mapped pair count.",
                    nameof(suppressedBones));
            }

            // Restore the exact sampled visual animation first. This prevents additive
            // drift when a Target bone is not overwritten by its Animator every frame.
            for (int i = 0; i < pairs.Count; i++)
            {
                pairs[i].RestoreSampledPoseToTarget();
            }

            float masterWeight = Mathf.Clamp01(masterMappingWeight);
            for (int i = 0; i < pairs.Count; i++)
            {
                RagdollAnimator.AnimatedPair pair = pairs[i];
                RagdollMappingWeights weights = pair.MappingWeights;
                weights.Multiply(masterWeight, masterWeight);

                if (modifiers != null)
                {
                    for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                    {
                        modifiers[modifierIndex].ModifyMapping(ref weights, pair);
                    }
                }

                weights.Clamp();
                if (suppressedBones != null
                    && suppressedBones[pair.Handle.Index])
                {
                    continue;
                }
                MapPair(pair, weights);
            }
        }

        static void MapPair(
            RagdollAnimator.AnimatedPair pair,
            RagdollMappingWeights weights)
        {
            Vector3 targetWorldPosition;
            Quaternion targetWorldRotation;
            pair.GetMappedTargetWorldPose(
                out targetWorldPosition,
                out targetWorldRotation);

            MapTransform(
                pair.TargetBone,
                targetWorldPosition,
                targetWorldRotation,
                weights);
        }

        internal static void MapTransform(
            Transform target,
            Vector3 simulatedPosition,
            Quaternion simulatedRotation,
            RagdollMappingWeights weights)
        {
            if (!target) throw new ArgumentNullException(nameof(target));

            float positionWeight = weights.PositionWeight;
            float rotationWeight = weights.RotationWeight;

            if (positionWeight > 0f)
            {
                target.position = BlendPosition(
                    target.position,
                    simulatedPosition,
                    positionWeight);
            }

            if (rotationWeight > 0f)
            {
                target.rotation = BlendRotation(
                    target.rotation,
                    simulatedRotation,
                    rotationWeight);
            }
        }

        internal static Vector3 BlendPosition(
            Vector3 animatedPosition,
            Vector3 simulatedPosition,
            float weight)
        {
            return Vector3.Lerp(
                animatedPosition,
                simulatedPosition,
                Mathf.Clamp01(weight));
        }

        internal static Quaternion BlendRotation(
            Quaternion animatedRotation,
            Quaternion simulatedRotation,
            float weight)
        {
            return Quaternion.Slerp(
                animatedRotation,
                simulatedRotation,
                Mathf.Clamp01(weight));
        }

        #region Initialization
        public RagdollToTargetMapper(
            RagdollDefinitionBindings bindings,
            IReadOnlyList<RagdollTargetBinding> targetBindings)
        {
            if (!bindings) throw new ArgumentNullException(nameof(bindings));
            if (targetBindings == null) throw new ArgumentNullException(nameof(targetBindings));

            bonePairs = CreateBonePairs(bindings, targetBindings);
        }

        static RagdollBoneTargetBonePair[] CreateBonePairs(
            RagdollDefinitionBindings bindings,
            IReadOnlyList<RagdollTargetBinding> targetBindings)
        {
            if (targetBindings.Count != bindings.BoneCount)
            {
                throw new InvalidOperationException(
                    "The target binding count does not match the registered ragdoll bone count.");
            }

            RagdollBoneTargetBonePair[] pairs =
                new RagdollBoneTargetBonePair[targetBindings.Count];

            for (int index = 0; index < targetBindings.Count; index++)
            {
                RagdollTargetBinding targetBinding = targetBindings[index];
                if (targetBinding == null)
                {
                    throw new InvalidOperationException(
                        "The target binding at index " + index + " is null.");
                }

                RagdollBone ragdollBone;
                if (!bindings.TryGetBone(targetBinding.Bone, out ragdollBone))
                {
                    throw new InvalidOperationException(
                        "The explicit target binding references an unknown ragdoll bone '"
                        + targetBinding.Bone + "'.");
                }

                pairs[index] = new RagdollBoneTargetBonePair(
                    ragdollBone,
                    targetBinding);
            }

            return pairs;
        }
        #endregion
    }
}
