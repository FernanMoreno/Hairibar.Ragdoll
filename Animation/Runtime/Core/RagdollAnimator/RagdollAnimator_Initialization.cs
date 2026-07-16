using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    //Initialization
    public partial class RagdollAnimator
    {
        void CreateRagdollToTargetMapper()
        {
            string error;
            RagdollTargetBinding[] resolvedBindings =
                ResolveCurrentTargetBindings(out error);
            if (resolvedBindings == null)
            {
                throw new InvalidOperationException(
                    "Could not resolve Target bindings: " + error);
            }

            if (UsesLegacyTargetBindingFallback)
            {
                Debug.LogWarning(
                    "RagdollAnimator is using the legacy name-based Target binding fallback. "
                    + "Create and assign a RagdollTargetBindings component to make the dual-rig references explicit.",
                    this);
            }

            mapper = new RagdollToTargetMapper(
                _ragdollBindings,
                resolvedBindings);
        }

        void CreateAnimatedPairs(IReadOnlyCollection<RagdollBoneTargetBonePair> bonePairs)
        {
            animatedPairs = new AnimatedPair[bonePairs.Count];

            int i = 0;
            foreach (RagdollBoneTargetBonePair bonePair in bonePairs)
            {
                RagdollBoneHandle handle;
                if (!Bindings.TryGetBoneHandle(bonePair.RagdollBone.Rigidbody, out handle))
                {
                    throw new InvalidOperationException(
                        "An animated ragdoll pair references a bone that is not registered in its bindings.");
                }

                animatedPairs[i] = new AnimatedPair(
                    bonePair,
                    handle,
                    GetConfiguredMappingWeights(bonePair.RagdollBone.Name));
                i++;
            }

            Array.Sort(animatedPairs, CompareAnimatedPairTopologyOrder);
            InitializeAnimatedPairLookup();
        }

        int CompareAnimatedPairTopologyOrder(AnimatedPair first, AnimatedPair second)
        {
            int depthComparison = Bindings.Topology.GetDepth(first.Handle)
                .CompareTo(Bindings.Topology.GetDepth(second.Handle));

            return depthComparison != 0
                ? depthComparison
                : first.Handle.Index.CompareTo(second.Handle.Index);
        }

        void GatherBoneProfileModifiers()
        {
            // Runtime muscle state is part of the advanced ragdoll pipeline. Its neutral
            // defaults preserve legacy behaviour while making collision and behaviour
            // systems available even on upgraded prefabs that did not serialize it yet.
            if (!GetComponent<RagdollMuscleController>())
            {
                gameObject.AddComponent<RagdollMuscleController>();
            }

            // Alive/Dead transitions must always be able to force the physical rig back
            // to Active before releasing pins, even when the user never configured global
            // Kinematic/Disabled mode switching explicitly.
            if (!GetComponent<RagdollSimulationModeController>())
            {
                gameObject.AddComponent<RagdollSimulationModeController>();
            }

            boneProfileModifiers = GetComponents<IBoneProfileModifier>();
            RagdollModifierOrdering.StableSort(boneProfileModifiers);
        }

        void InitializeBoneProfileModifiers(IBoneProfileModifier[] boneProfileModifiers, AnimatedPair[] pairs)
        {
            foreach (IBoneProfileModifier modifier in boneProfileModifiers)
            {
                modifier.Initialize(pairs);
            }
        }


        void GatherTargetPoseModifiers()
        {
            targetPoseModifiers = GetComponents<ITargetPoseModifier>();
            RagdollModifierOrdering.StableSort(targetPoseModifiers);
        }

        void InitializeTargetPoseModifiers(ITargetPoseModifier[] targetPoseModifiers, AnimatedPair[] pairs)
        {
            foreach (ITargetPoseModifier modifier in targetPoseModifiers)
            {
                modifier.Initialize(pairs);
            }
        }


        void InitializeProfileTransitioning()
        {
            profileTransitioner = new ValueTransitioner(0, 1);
            profileTransitioner.EndTransition();

            previousProfile = currentProfile;
        }

    }
}
