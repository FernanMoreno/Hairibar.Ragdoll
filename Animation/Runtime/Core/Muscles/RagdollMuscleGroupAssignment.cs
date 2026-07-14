using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    [Serializable]
    public sealed class RagdollMuscleGroupAssignment
    {
        [SerializeField] BoneName bone;
        [SerializeField] RagdollMuscleGroup group;

        public BoneName Bone => bone;
        public RagdollMuscleGroup Group => group;

        public RagdollMuscleGroupAssignment()
        {
        }

        internal RagdollMuscleGroupAssignment(
            BoneName bone,
            RagdollMuscleGroup group)
        {
            this.bone = bone;
            this.group = group;
        }
    }
}
