using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Physical collider surface policy selected by BehaviourPuppet state.</summary>
    [Serializable]
    public enum RagdollPuppetColliderSurfaceState
    {
        Puppet,
        Unpinned,
        GetUp
    }
}
