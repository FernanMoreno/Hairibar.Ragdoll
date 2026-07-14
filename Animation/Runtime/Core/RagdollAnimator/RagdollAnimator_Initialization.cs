using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.Animation
{
    //Initialization
    public partial class RagdollAnimator
    {
        void CreateRagdollToTargetMapper()
        {
            mapper = new RagdollToTargetMapper(_ragdollBindings, transform);
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
            boneProfileModifiers = GetComponents<IBoneProfileModifier>();
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
