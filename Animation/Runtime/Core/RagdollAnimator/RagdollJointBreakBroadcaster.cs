using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Converts Unity's joint-break callback into a generation-safe request owned by
    /// RagdollAnimator. The structural mutation is committed at the next fixed boundary.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class RagdollJointBreakBroadcaster : MonoBehaviour
    {
        RagdollAnimator animator;
        BoneName bone;

        internal void Initialize(RagdollAnimator owner, BoneName boneName)
        {
            animator = owner;
            bone = boneName;
            enabled = true;
        }

        internal bool IsOwnedBy(RagdollAnimator owner)
        {
            return animator == owner;
        }

        internal void Release(RagdollAnimator owner)
        {
            if (animator != owner) return;
            animator = null;
            enabled = false;
        }

        void OnJointBreak(float breakForce)
        {
            if (!enabled || !animator) return;
            animator.ScheduleJointBreak(bone, breakForce);
            enabled = false;
        }
    }
}
