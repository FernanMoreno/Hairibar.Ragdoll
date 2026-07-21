using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Forced internal-collision ignores owned by one held prop. A match against a
    /// muscle name or semantic group remains ignored regardless of the Puppet's global
    /// internal-collision toggle. The rule is evaluated against the current runtime
    /// generation every time the prop is picked up.
    /// </summary>
    [Serializable]
    public sealed class RagdollPropInternalCollisionSettings
    {
        [SerializeField]
        [Tooltip("Force-ignore the held prop against every other registered Puppet muscle.")]
        bool ignoreAll;

        [SerializeField]
        [Tooltip("Specific registered muscles that must not collide with this held prop.")]
        BoneName[] muscles = new BoneName[0];

        [SerializeField]
        [Tooltip("Semantic muscle groups that must not collide with this held prop.")]
        RagdollMuscleGroup[] groups = new RagdollMuscleGroup[0];

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

        public bool HasRules => ignoreAll
            || (muscles != null && muscles.Length > 0)
            || (groups != null && groups.Length > 0);

        public RagdollPropInternalCollisionSettings()
        {
        }

        public RagdollPropInternalCollisionSettings(
            bool ignoreAll,
            BoneName[] muscles = null,
            RagdollMuscleGroup[] groups = null)
        {
            this.ignoreAll = ignoreAll;
            this.muscles = muscles ?? new BoneName[0];
            this.groups = groups ?? new RagdollMuscleGroup[0];
        }

        public bool Matches(BoneName bone, RagdollMuscleGroup group)
        {
            return Matches(bone, group, true);
        }

        internal bool Matches(
            BoneName bone,
            RagdollMuscleGroup group,
            bool hasSemanticGroup)
        {
            Normalize();
            if (ignoreAll) return true;

            for (int index = 0; index < muscles.Length; index++)
            {
                if (muscles[index] == bone) return true;
            }
            if (!hasSemanticGroup) return false;
            for (int index = 0; index < groups.Length; index++)
            {
                if (groups[index] == group) return true;
            }
            return false;
        }

        internal void Normalize()
        {
            if (muscles == null) muscles = new BoneName[0];
            if (groups == null) groups = new RagdollMuscleGroup[0];
        }
    }
}
