using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Physics source that requested activation while NormalMode is Kinematic.
    /// StaticCollider means no attached Rigidbody; KinematicRigidbody is grouped with
    /// static contacts by the activation policy; DynamicRigidbody is simulated.
    /// </summary>
    [Serializable]
    public enum RagdollPuppetKinematicActivationSource
    {
        None,
        StaticCollider,
        KinematicRigidbody,
        DynamicRigidbody
    }
}
