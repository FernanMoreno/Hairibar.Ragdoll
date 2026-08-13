namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Whether a held/carried Prop-group muscle drifting from its target can
    /// knock the whole puppet out of balance, the same way a body muscle does.
    /// RootMotion's own Prop system treats props as semi-independent physical
    /// objects (picked up/dropped via PropMuscle.currentProp) rather than core
    /// anatomy, so the default excludes them from TryFindKnockOutBone.
    /// </summary>
    public enum RagdollPropDriftPolicy
    {
        /// <summary>Prop-group muscles never trigger a knockout by themselves.</summary>
        Ignore,

        /// <summary>Prop-group muscles are treated like any other muscle for knockout.</summary>
        CountsTowardKnockout
    }
}
