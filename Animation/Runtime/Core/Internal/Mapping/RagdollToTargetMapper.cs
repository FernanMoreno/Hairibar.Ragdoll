using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Creates target/ragdoll pairs and maps the simulated ragdoll pose back to the
    /// animated target hierarchy using independent position and rotation weights.
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
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));

            // Restore the exact sampled animation first. This prevents additive drift when
            // the target contains bones that are not overwritten by its Animator every frame.
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
                MapPair(pair, weights);
            }
        }

        static void MapPair(
            RagdollAnimator.AnimatedPair pair,
            RagdollMappingWeights weights)
        {
            Transform target = pair.TargetBone;
            Transform simulated = pair.RagdollBone.Transform;

            MapTransform(
                target,
                simulated.position,
                simulated.rotation,
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
            Transform targetParent)
        {
            if (!bindings) throw new ArgumentNullException(nameof(bindings));
            if (!targetParent) throw new ArgumentNullException(nameof(targetParent));

            bonePairs = CreateBonePairs(bindings, targetParent);
        }

        static RagdollBoneTargetBonePair[] CreateBonePairs(
            RagdollDefinitionBindings bindings,
            Transform targetParent)
        {
            List<RagdollBoneTargetBonePair> pairs = new List<RagdollBoneTargetBonePair>();
            Transform targetRoot = FindCorrespondingBone(
                bindings.Root.Transform,
                targetParent);

            if (!targetRoot)
            {
                throw new InvalidOperationException(
                    "The target hierarchy does not contain a transform matching the registered ragdoll root '" +
                    bindings.Root.Transform.name + "'.");
            }

            CreateBonePairsRecursively(targetRoot, pairs, bindings.transform, bindings);
            return pairs.ToArray();
        }

        static void CreateBonePairsRecursively(
            Transform targetBoneTransform,
            List<RagdollBoneTargetBonePair> pairs,
            Transform ragdollParentTransform,
            RagdollDefinitionBindings bindings)
        {
            Transform ragdollBoneTransform = FindCorrespondingBone(
                targetBoneTransform,
                ragdollParentTransform);

            if (ragdollBoneTransform)
            {
                RagdollBone ragdollBone = GetRagdollBoneForRagdollBoneTransform(
                    ragdollBoneTransform,
                    bindings);

                if (ragdollBone != null)
                {
                    pairs.Add(new RagdollBoneTargetBonePair(
                        ragdollBone,
                        targetBoneTransform));
                }
            }

            for (int i = 0; i < targetBoneTransform.childCount; i++)
            {
                CreateBonePairsRecursively(
                    targetBoneTransform.GetChild(i),
                    pairs,
                    ragdollParentTransform,
                    bindings);
            }
        }

        static Transform FindCorrespondingBone(
            Transform originalBone,
            Transform equivalentBoneParent)
        {
            if (!originalBone || !equivalentBoneParent) return null;

            for (int i = 0; i < equivalentBoneParent.childCount; i++)
            {
                Transform child = equivalentBoneParent.GetChild(i);

                if (child.name == originalBone.name)
                {
                    return child;
                }

                Transform nestedResult = FindCorrespondingBone(originalBone, child);
                if (nestedResult) return nestedResult;
            }

            return null;
        }

        static RagdollBone GetRagdollBoneForRagdollBoneTransform(
            Transform ragdollBoneTransform,
            RagdollDefinitionBindings bindings)
        {
            ConfigurableJoint joint = ragdollBoneTransform.GetComponent<ConfigurableJoint>();
            if (!joint) return null;

            if (!bindings.TryGetBoundBoneName(joint, out BoneName boneName))
            {
                return null;
            }

            bindings.TryGetBone(boneName, out RagdollBone bone);
            return bone;
        }
        #endregion
    }
}
