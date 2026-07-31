using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Portable semantic references used by automatic biped authoring.</summary>
    [Serializable]
    public sealed class RagdollBipedReferences
    {
        public Transform hips;
        public Transform spine;
        public Transform chest;
        public Transform head;
        public Transform leftUpperArm;
        public Transform leftLowerArm;
        public Transform leftHand;
        public Transform rightUpperArm;
        public Transform rightLowerArm;
        public Transform rightHand;
        public Transform leftUpperLeg;
        public Transform leftLowerLeg;
        public Transform leftFoot;
        public Transform rightUpperLeg;
        public Transform rightLowerLeg;
        public Transform rightFoot;

        public static bool TryFromHumanoid(
            Animator animator,
            out RagdollBipedReferences references,
            out string error)
        {
            references = null;
            if (!animator)
            {
                error = "An Animator is required.";
                return false;
            }
            if (!animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                error = "The Animator must use a valid Humanoid Avatar.";
                return false;
            }

            references = new RagdollBipedReferences
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips),
                spine = animator.GetBoneTransform(HumanBodyBones.Spine),
                chest = First(
                    animator.GetBoneTransform(HumanBodyBones.Chest),
                    animator.GetBoneTransform(HumanBodyBones.UpperChest)),
                head = animator.GetBoneTransform(HumanBodyBones.Head),
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand),
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand),
                leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot)
            };

            return references.Validate(out error);
        }

        public bool Validate(out string error)
        {
            if (!hips || !head
                || !leftUpperArm || !leftLowerArm
                || !rightUpperArm || !rightLowerArm
                || !leftUpperLeg || !leftLowerLeg
                || !rightUpperLeg || !rightLowerLeg)
            {
                error = "Hips, head, both upper/lower arms and both upper/lower legs are required.";
                return false;
            }

            HashSet<Transform> unique = new HashSet<Transform>();
            foreach (Transform bone in EnumerateAll())
            {
                if (bone && !unique.Add(bone))
                {
                    error = "Each semantic biped reference must point to a different Transform.";
                    return false;
                }
            }

            foreach (Transform bone in unique)
            {
                if (bone == hips) continue;
                if (!bone.IsChildOf(hips))
                {
                    error = bone.name + " is not below the hips hierarchy.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public IEnumerable<Transform> EnumerateAll()
        {
            yield return hips;
            yield return spine;
            yield return chest;
            yield return head;
            yield return leftUpperArm;
            yield return leftLowerArm;
            yield return leftHand;
            yield return rightUpperArm;
            yield return rightLowerArm;
            yield return rightHand;
            yield return leftUpperLeg;
            yield return leftLowerLeg;
            yield return leftFoot;
            yield return rightUpperLeg;
            yield return rightLowerLeg;
            yield return rightFoot;
        }

        static Transform First(Transform preferred, Transform fallback)
        {
            return preferred ? preferred : fallback;
        }
    }
}
