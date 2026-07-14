using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Lightweight Rigidbody-local bridge used internally by RagdollCollisionHub.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class RagdollCollisionRelay : MonoBehaviour
    {
        RagdollCollisionHub owner;
        RagdollBoneHandle bone;

        internal RagdollCollisionHub Owner => owner;

        internal void Initialize(RagdollCollisionHub newOwner, RagdollBoneHandle newBone)
        {
            owner = newOwner;
            bone = newBone;
            hideFlags |= HideFlags.HideInInspector;
        }

        internal void Detach(RagdollCollisionHub expectedOwner)
        {
            if (owner == expectedOwner)
            {
                owner = null;
                bone = RagdollBoneHandle.Invalid;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            Dispatch(RagdollCollisionPhase.Enter, collision);
        }

        void OnCollisionStay(Collision collision)
        {
            Dispatch(RagdollCollisionPhase.Stay, collision);
        }

        void OnCollisionExit(Collision collision)
        {
            Dispatch(RagdollCollisionPhase.Exit, collision);
        }

        void Dispatch(RagdollCollisionPhase phase, Collision collision)
        {
            if (owner && owner.isActiveAndEnabled)
            {
                owner.Dispatch(bone, phase, collision);
            }
        }
    }
}
