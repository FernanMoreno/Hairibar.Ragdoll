using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    [Serializable]
    public sealed class RagdollMuscleGroupOverride
    {
        [SerializeField] RagdollMuscleGroup group;
        [SerializeField] RagdollMuscleBehaviourSettings settings =
            RagdollMuscleBehaviourSettings.Default;

        public RagdollMuscleGroup Group => group;
        public RagdollMuscleBehaviourSettings Settings => settings;

        internal void Normalize()
        {
            settings.Normalize();
        }
    }
}
