using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Requested and active lifecycle states of the ragdoll core.</summary>
    [Serializable]
    public enum RagdollLifecycleState
    {
        Alive = 0,
        Dead = 1,
        Frozen = 2
    }
}
