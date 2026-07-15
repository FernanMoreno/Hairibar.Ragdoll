using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Reason a collision callback did not enter BehaviourPuppet processing.</summary>
    [Serializable]
    public enum RagdollPuppetCollisionRejectionReason
    {
        None,
        UnsupportedPhase,
        InvalidLayer,
        LayerFiltered,
        InvalidImpulse,
        BelowThreshold,
        BudgetExceeded
    }
}
