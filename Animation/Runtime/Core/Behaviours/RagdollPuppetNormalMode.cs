using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Balanced-state mapping policy used by RagdollPuppetBehaviour.</summary>
    [Serializable]
    public enum RagdollPuppetNormalMode
    {
        /// <summary>Preserves the mapping authored by the animation profile and modifiers.</summary>
        Active,

        /// <summary>Maps the physical Puppet to the Target only while accepted contact is recent.</summary>
        Unmapped,

        /// <summary>Keeps the Puppet kinematic until an accepted activation contact wakes it.</summary>
        Kinematic
    }
}
