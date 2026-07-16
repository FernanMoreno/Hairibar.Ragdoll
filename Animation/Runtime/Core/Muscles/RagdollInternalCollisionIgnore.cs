using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Authored forced internal-collision ignores for one muscle. The rule is symmetric:
    /// a match authored on either muscle keeps all collider pairs between both muscles
    /// ignored even while the global internal-collision toggle is enabled.
    /// </summary>
    [Serializable]
    public sealed class RagdollInternalCollisionIgnore
    {
        [SerializeField] BoneName bone;
        [SerializeField]
        [Tooltip("Keeps this muscle ignored against every other registered muscle.")]
        bool ignoreAll;
        [SerializeField]
        [Tooltip("Specific muscles that must remain ignored against this muscle.")]
        BoneName[] muscles = new BoneName[0];
        [SerializeField]
        [Tooltip("Semantic muscle groups that must remain ignored against this muscle.")]
        RagdollMuscleGroup[] groups = new RagdollMuscleGroup[0];

        public BoneName Bone
        {
            get => bone;
            set => bone = value;
        }

        public bool IgnoreAll
        {
            get => ignoreAll;
            set => ignoreAll = value;
        }

        public BoneName[] Muscles
        {
            get => muscles;
            set => muscles = value ?? new BoneName[0];
        }

        public RagdollMuscleGroup[] Groups
        {
            get => groups;
            set => groups = value ?? new RagdollMuscleGroup[0];
        }

        public RagdollInternalCollisionIgnore()
        {
        }

        public RagdollInternalCollisionIgnore(
            BoneName bone,
            bool ignoreAll = false,
            BoneName[] muscles = null,
            RagdollMuscleGroup[] groups = null)
        {
            this.bone = bone;
            this.ignoreAll = ignoreAll;
            this.muscles = muscles ?? new BoneName[0];
            this.groups = groups ?? new RagdollMuscleGroup[0];
        }

        internal void Normalize()
        {
            if (muscles == null) muscles = new BoneName[0];
            if (groups == null) groups = new RagdollMuscleGroup[0];
        }
    }
}
