using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Global Puppet simulation modes. Active restores authored per-bone power settings,
    /// Kinematic follows the Target without muscle forces, and Disabled deactivates the
    /// Puppet hierarchy completely.
    /// </summary>
    [Serializable]
    public enum RagdollSimulationMode
    {
        Active,
        Kinematic,
        Disabled
    }
}
