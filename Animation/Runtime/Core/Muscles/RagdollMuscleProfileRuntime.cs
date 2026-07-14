using System;

namespace Hairibar.Ragdoll.Animation
{
    internal sealed class RagdollMuscleProfileRuntime
    {
        readonly RagdollMuscleGroup[] groups;
        readonly RagdollMuscleBehaviourSettings[] settings;

        public int BoneCount => groups.Length;

        internal RagdollMuscleProfileRuntime(
            RagdollMuscleGroup[] groups,
            RagdollMuscleBehaviourSettings[] settings)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (groups.Length != settings.Length)
            {
                throw new ArgumentException(
                    "Muscle group and behaviour setting arrays must have the same length.");
            }

            this.groups = groups;
            this.settings = settings;
        }

        internal RagdollMuscleGroup GetGroup(int index)
        {
            ValidateIndex(index);
            return groups[index];
        }

        internal RagdollMuscleBehaviourSettings GetSettings(int index)
        {
            ValidateIndex(index);
            return settings[index];
        }

        void ValidateIndex(int index)
        {
            if (index < 0 || index >= groups.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
