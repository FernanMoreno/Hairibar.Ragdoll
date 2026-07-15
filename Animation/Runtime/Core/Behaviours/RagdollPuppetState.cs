using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// High-level balance states used by RagdollPuppetBehaviour.
    /// Puppet is normally pinned, Unpinned follows animation in muscle space only,
    /// and GetUp progressively restores pinning and animation authority.
    /// </summary>
    [Serializable]
    public enum RagdollPuppetState
    {
        Puppet,
        Unpinned,
        GetUp
    }
}
