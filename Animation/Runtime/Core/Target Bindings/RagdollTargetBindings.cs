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
