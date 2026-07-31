using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    [Serializable]
    public struct RagdollHumanoidBoneBinding
    {
        public BoneName ragdollBone;
        public HumanBodyBones humanoidBone;
    }

    /// <summary>
    /// Avatar-portable semantic mapping. It binds a Puppet definition to any valid
    /// Humanoid Avatar without depending on Transform names or local bone axes.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Ragdoll/Humanoid Binding Profile",
        fileName = "raghum_New")]
    public sealed class RagdollHumanoidBindingProfile : ScriptableObject
    {
        [SerializeField] RagdollHumanoidBoneBinding[] bindings =
            new RagdollHumanoidBoneBinding[0];

        public IReadOnlyList<RagdollHumanoidBoneBinding> Bindings => bindings;

        public bool TryApply(
            Animator targetAnimator,
            RagdollTargetBindings targetBindings,
            out string error)
        {
            if (!targetAnimator || !targetAnimator.avatar
                || !targetAnimator.avatar.isValid || !targetAnimator.avatar.isHuman)
            {
                error = "A valid Humanoid Animator is required.";
                return false;
            }
            if (!targetBindings)
            {
                error = "RagdollTargetBindings is required.";
                return false;
            }

            Dictionary<BoneName, Transform> targets =
                new Dictionary<BoneName, Transform>(bindings.Length);
            HashSet<HumanBodyBones> semantics = new HashSet<HumanBodyBones>();
            for (int index = 0; index < bindings.Length; index++)
            {
                RagdollHumanoidBoneBinding binding = bindings[index];
                if (binding.humanoidBone < 0
                    || binding.humanoidBone >= HumanBodyBones.LastBone)
                {
                    error = "Binding " + index + " has an invalid Humanoid bone.";
                    return false;
                }
                if (!semantics.Add(binding.humanoidBone))
                {
                    error = "Humanoid bone '" + binding.humanoidBone
                        + "' is mapped more than once.";
                    return false;
                }
                if (targets.ContainsKey(binding.ragdollBone))
                {
                    error = "Ragdoll bone '" + binding.ragdollBone
                        + "' is mapped more than once.";
                    return false;
                }

                Transform target = targetAnimator.GetBoneTransform(binding.humanoidBone);
                if (!target)
                {
                    error = "The Avatar has no Transform for '"
                        + binding.humanoidBone + "'.";
                    return false;
                }
                targets.Add(binding.ragdollBone, target);
            }

            return targetBindings.TryAssignTargets(targets, out error);
        }
    }
}
