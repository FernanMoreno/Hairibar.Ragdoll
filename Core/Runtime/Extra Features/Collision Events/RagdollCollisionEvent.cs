using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Allocation-free collision data associated with a registered ragdoll bone.
    /// The Collision reference is valid for immediate callback processing and should not
    /// be retained by consumers after the callback has completed.
    /// </summary>
    public struct RagdollCollisionEvent
    {
        public RagdollBoneHandle Bone { get; private set; }
        public RagdollCollisionPhase Phase { get; private set; }
        public Vector3 Impulse { get; private set; }
        public float ImpulseMagnitude { get; private set; }
        public Vector3 RelativeVelocity { get; private set; }
        public bool HasContact { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public Vector3 ContactNormal { get; private set; }
        public int OtherLayer { get; private set; }
        public Rigidbody OtherRigidbody { get; private set; }
        public Collider OtherCollider { get; private set; }
        public float FixedTime { get; private set; }
        public long Sequence { get; private set; }
        public Collision Collision { get; private set; }

        internal RagdollCollisionEvent(
            RagdollBoneHandle bone,
            RagdollCollisionPhase phase,
            Collision collision,
            float fixedTime,
            long sequence)
        {
            Bone = bone;
            Phase = phase;
            FixedTime = fixedTime;
            Sequence = sequence;
            Collision = collision;

            Impulse = collision != null ? collision.impulse : Vector3.zero;
            ImpulseMagnitude = Impulse.magnitude;
            RelativeVelocity = collision != null ? collision.relativeVelocity : Vector3.zero;

            HasContact = collision != null && collision.contactCount > 0;
            if (HasContact)
            {
                UnityEngine.ContactPoint contact = collision.GetContact(0);
                ContactPoint = contact.point;
                ContactNormal = contact.normal;
            }
            else
            {
                ContactPoint = Vector3.zero;
                ContactNormal = Vector3.zero;
            }

            GameObject otherObject = collision != null ? collision.gameObject : null;
            OtherLayer = otherObject ? otherObject.layer : 0;
            OtherRigidbody = collision != null ? collision.rigidbody : null;
            OtherCollider = collision != null ? collision.collider : null;
        }
    }
}
