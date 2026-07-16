using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Describes why RagdollPuppetBehaviour changed state.</summary>
    [Serializable]
    public enum RagdollPuppetTransitionReason
    {
        Manual,
        TargetDrift,
        GetUpStarted,
        GetUpCompleted,
        GetUpInterrupted,
        LifecycleDeath
    }
}
