using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Runtime-only references shared with behaviours. Behaviour components do not need
    /// serialized references to the Target, Puppet, collision hub or muscle controller.
    /// </summary>
    public sealed class RagdollBehaviourContext
    {
        readonly RagdollAnimator.AnimatedPair[] pairs;
        readonly IReadOnlyList<RagdollAnimator.AnimatedPair> pairsView;
        readonly RagdollAnimator.AnimatedPair[] pairsByHandleIndex;

        public RagdollBehaviourController Controller { get; }
        public RagdollAnimator Animator { get; }
        public RagdollDefinitionBindings Bindings { get; }
        public RagdollMuscleController Muscles { get; }
        public RagdollCollisionHub CollisionHub { get; }
        public RagdollBoneTopology Topology => Bindings.Topology;
        public IReadOnlyList<RagdollAnimator.AnimatedPair> Pairs => pairsView;

        internal RagdollBehaviourContext(
            RagdollBehaviourController controller,
            RagdollAnimator animator,
            RagdollMuscleController muscles,
            RagdollCollisionHub collisionHub,
            IEnumerable<RagdollAnimator.AnimatedPair> sourcePairs)
        {
            if (!controller) throw new ArgumentNullException(nameof(controller));
            if (!animator) throw new ArgumentNullException(nameof(animator));
            if (!muscles) throw new ArgumentNullException(nameof(muscles));
            if (!collisionHub) throw new ArgumentNullException(nameof(collisionHub));
            if (sourcePairs == null) throw new ArgumentNullException(nameof(sourcePairs));

            Controller = controller;
            Animator = animator;
            Bindings = animator.Bindings;
            Muscles = muscles;
            CollisionHub = collisionHub;

            List<RagdollAnimator.AnimatedPair> collected =
                new List<RagdollAnimator.AnimatedPair>(Bindings.BoneCount);
            foreach (RagdollAnimator.AnimatedPair pair in sourcePairs)
            {
                if (pair == null)
                {
                    throw new ArgumentException(
                        "A behaviour context cannot contain a null animated pair.",
                        nameof(sourcePairs));
                }

                collected.Add(pair);
            }

            pairs = collected.ToArray();
            pairsView = pairs;
            pairsByHandleIndex = new RagdollAnimator.AnimatedPair[Bindings.BoneCount];

            for (int index = 0; index < pairs.Length; index++)
            {
                RagdollAnimator.AnimatedPair pair = pairs[index];
                if (!Bindings.Topology.Contains(pair.Handle))
                {
                    throw new ArgumentException(
                        "A behaviour context received an animated pair from another registry generation.",
                        nameof(sourcePairs));
                }

                if (pairsByHandleIndex[pair.Handle.Index] != null)
                {
                    throw new ArgumentException(
                        "A behaviour context cannot contain duplicate ragdoll bone handles.",
                        nameof(sourcePairs));
                }

                pairsByHandleIndex[pair.Handle.Index] = pair;
            }
        }

        public bool TryGetPair(
            RagdollBoneHandle bone,
            out RagdollAnimator.AnimatedPair pair)
        {
            if (!Bindings.Topology.Contains(bone))
            {
                pair = null;
                return false;
            }

            pair = pairsByHandleIndex[bone.Index];
            return pair != null;
        }

        public RagdollAnimator.AnimatedPair GetPair(RagdollBoneHandle bone)
        {
            RagdollAnimator.AnimatedPair pair;
            if (TryGetPair(bone, out pair))
            {
                return pair;
            }

            throw new ArgumentException(
                "The supplied bone has no animated pair in this behaviour context.",
                nameof(bone));
        }
    }
}
