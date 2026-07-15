using System;

namespace Hairibar.Ragdoll
{
    /// <summary>Controls how RagdollSettings treats each Rigidbody inertia tensor.</summary>
    [Serializable]
    public enum RagdollInertiaTensorMode
    {
        /// <summary>Leaves the current tensor untouched.</summary>
        PreserveAuthored,

        /// <summary>Recalculates the tensor from attached colliders and current mass.</summary>
        ResetFromColliders,

        /// <summary>Recalculates, then raises very small positive principal values.</summary>
        ResetAndStabilize
    }
}
