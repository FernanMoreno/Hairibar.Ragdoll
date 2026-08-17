using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Serializable Target-to-Puppet binding table for a dual-rig animated ragdoll.
    /// References are explicit; Transform names are used only by the optional migration tool.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Target Bindings")]
    [DisallowMultipleComponent]
    public sealed class RagdollTargetBindings : MonoBehaviour
    {
        [SerializeField] RagdollDefinitionBindings ragdollBindings;
        [SerializeField] RagdollTargetBinding[] bindings = new RagdollTargetBinding[0];

        Dictionary<BoneName, RagdollTargetBinding> lookup;

        public RagdollDefinitionBindings RagdollBindings => ragdollBindings;
        public IReadOnlyList<RagdollTargetBinding> Bindings => bindings;

        public void SetRagdollBindings(RagdollDefinitionBindings value)
        {
            if (ragdollBindings == value) return;

            ragdollBindings = value;
            InvalidateCapturedOffsets();
            InvalidateLookup();
        }

        /// <summary>
        /// Migrates a legacy hierarchy by requiring exactly one target Transform with the
        /// same name as each registered ragdoll Transform. The resulting runtime table no
        /// longer depends on those names.
        /// </summary>
        public bool TryAutoBindByName(out string error)
        {
            RagdollTargetBinding[] generated;
            if (!RagdollTargetBindingUtility.TryCreateByUniqueName(
                ragdollBindings,
                transform,
                out generated,
                out error))
            {
                return false;
            }

            bindings = generated;
            InvalidateLookup();
            return true;
        }

        /// <summary>
        /// Replaces the table from an explicit semantic map and captures offsets.
        /// This enables sharing a Puppet with Targets whose names and bone axes differ.
        /// Call before the owning RagdollAnimator initializes.
        /// </summary>
        public bool TryAssignTargets(
            IReadOnlyDictionary<BoneName, Transform> targets,
            out string error)
        {
            if (!ragdollBindings || !ragdollBindings.IsInitialized)
            {
                error = "Ragdoll bindings must be initialized first.";
                return false;
            }
            if (targets == null)
            {
                error = "An explicit target map is required.";
                return false;
            }

            RagdollTargetBinding[] generated =
                new RagdollTargetBinding[ragdollBindings.BoneCount];
            HashSet<Transform> unique = new HashSet<Transform>();
            for (int index = 0; index < generated.Length; index++)
            {
                RagdollBone bone = ragdollBindings.GetBoneAt(index);
                Transform target;
                if (!targets.TryGetValue(bone.Name, out target) || !target)
                {
                    error = "No target was supplied for ragdoll bone '"
                        + bone.Name + "'.";
                    return false;
                }
                if (!unique.Add(target))
                {
                    error = "Target Transform '" + target.name
                        + "' was assigned more than once.";
                    return false;
                }
                generated[index] = new RagdollTargetBinding(
                    bone.Name,
                    target,
                    bone.Transform);
            }

            bindings = generated;
            InvalidateLookup();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Recalculates the bidirectional bind-pose offsets using the currently assigned
        /// Target and ragdoll Transforms.
        /// </summary>
        public bool TryCaptureOffsets(out string error)
        {
            RagdollTargetBinding[] ordered;
            if (!TryBuildOrderedBindings(
                ragdollBindings,
                false,
                out ordered,
                out error))
            {
                return false;
            }

            for (int index = 0; index < ordered.Length; index++)
            {
                RagdollBone ragdollBone = ragdollBindings.GetBoneAt(index);
                ordered[index].CaptureOffsets(ragdollBone.Transform);
            }

            InvalidateLookup();
            return true;
        }

        public bool TryValidate(out string error)
        {
            RagdollTargetBinding[] ignored;
            return TryBuildOrderedBindings(
                ragdollBindings,
                true,
                out ignored,
                out error);
        }

        public bool TryGetBinding(
            BoneName bone,
            out RagdollTargetBinding binding)
        {
            EnsureLookup();
            return lookup.TryGetValue(bone, out binding);
        }

        internal bool TryGetOrderedBindings(
            RagdollDefinitionBindings expectedRagdollBindings,
            out RagdollTargetBinding[] orderedBindings,
            out string error)
        {
            return TryBuildOrderedBindings(
                expectedRagdollBindings,
                true,
                out orderedBindings,
                out error);
        }

        public void InvalidateCapturedOffsets()
        {
            if (bindings == null) return;

            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index] != null)
                {
                    bindings[index].InvalidateOffsets();
                }
            }
        }

        bool TryBuildOrderedBindings(
            RagdollDefinitionBindings expectedRagdollBindings,
            bool requireCapturedOffsets,
            out RagdollTargetBinding[] orderedBindings,
            out string error)
        {
            orderedBindings = null;
            error = null;

            if (!expectedRagdollBindings)
            {
                error = "No RagdollDefinitionBindings was supplied.";
                return false;
            }

            if (!expectedRagdollBindings.IsInitialized)
            {
                error = "The RagdollDefinitionBindings is not initialized.";
                return false;
            }

            if (ragdollBindings != expectedRagdollBindings)
            {
                error = "The target bindings reference a different RagdollDefinitionBindings component.";
                return false;
            }

            if (bindings == null)
            {
                error = "The target binding table is null.";
                return false;
            }

            int expectedCount = expectedRagdollBindings.BoneCount;

            Dictionary<BoneName, RagdollTargetBinding> byBone =
                new Dictionary<BoneName, RagdollTargetBinding>(expectedCount);
            HashSet<Transform> usedTargets = new HashSet<Transform>();

            for (int index = 0; index < bindings.Length; index++)
            {
                RagdollTargetBinding binding = bindings[index];
                if (binding == null)
                {
                    error = "Target binding entry " + index + " is null.";
                    return false;
                }

                if (!binding.Target)
                {
                    error = "Target binding '" + binding.Bone + "' has no target Transform.";
                    return false;
                }

                if (requireCapturedOffsets && !binding.OffsetsCaptured)
                {
                    error = "Target binding '" + binding.Bone
                        + "' has not captured its bind-pose offsets.";
                    return false;
                }

                if (byBone.ContainsKey(binding.Bone))
                {
                    error = "Ragdoll bone '" + binding.Bone + "' is bound more than once.";
                    return false;
                }

                if (!usedTargets.Add(binding.Target))
                {
                    error = "Target Transform '" + binding.Target.name + "' is bound more than once.";
                    return false;
                }

                byBone.Add(binding.Bone, binding);
            }

            orderedBindings = new RagdollTargetBinding[expectedCount];
            for (int index = 0; index < expectedCount; index++)
            {
                BoneName expectedBone = expectedRagdollBindings.GetBoneAt(index).Name;
                RagdollTargetBinding binding;
                if (!byBone.TryGetValue(expectedBone, out binding))
                {
                    error = "No target Transform is assigned for ragdoll bone '"
                        + expectedBone + "'.";
                    orderedBindings = null;
                    return false;
                }

                orderedBindings[index] = binding;
            }


            HashSet<Transform> registeredTargets = new HashSet<Transform>();
            for (int index = 0; index < orderedBindings.Length; index++)
                registeredTargets.Add(orderedBindings[index].Target);
            HashSet<Transform> animatedChildren = new HashSet<Transform>();
            for (int index = 0; index < orderedBindings.Length; index++)
            {
                RagdollTargetBinding binding = orderedBindings[index];
                IReadOnlyList<Transform> children = binding.AnimatedTargetChildren;
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    Transform child = children[childIndex];
                    if (!child || child == binding.Target || !child.IsChildOf(binding.Target))
                    {
                        error = "Animated child entry " + childIndex + " for '"
                            + binding.Bone + "' must be below its Target.";
                        orderedBindings = null;
                        return false;
                    }
                    if (registeredTargets.Contains(child))
                    {
                        error = "Animated child '" + child.name
                            + "' is itself a registered muscle Target.";
                        orderedBindings = null;
                        return false;
                    }
                    if (!animatedChildren.Add(child))
                    {
                        error = "Animated child '" + child.name
                            + "' is assigned to more than one muscle.";
                        orderedBindings = null;
                        return false;
                    }
                }
            }

            return true;
        }

        void EnsureLookup()
        {
            if (lookup != null) return;

            lookup = new Dictionary<BoneName, RagdollTargetBinding>();
            if (bindings == null) return;

            for (int index = 0; index < bindings.Length; index++)
            {
                RagdollTargetBinding binding = bindings[index];
                if (binding == null || lookup.ContainsKey(binding.Bone)) continue;
                lookup.Add(binding.Bone, binding);
            }
        }

        void InvalidateLookup()
        {
            lookup = null;
        }

        void OnValidate()
        {
            InvalidateLookup();
        }
    }
}
