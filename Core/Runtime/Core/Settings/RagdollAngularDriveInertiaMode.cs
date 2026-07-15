using System;

namespace Hairibar.Ragdoll
{
    /// <summary>Scalar approximation used to configure the rotational SLERP drive.</summary>
    [Serializable]
    public enum RagdollAngularDriveInertiaMode
    {
        /// <summary>Preserves the pre-0014 behaviour by using Rigidbody.mass.</summary>
        RigidbodyMass,

        /// <summary>Uses the arithmetic mean of the positive principal inertia values.</summary>
        AverageInertia,

        /// <summary>Uses the largest positive principal inertia value.</summary>
        MaximumInertia
    }
}
