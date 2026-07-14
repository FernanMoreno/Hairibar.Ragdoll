using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Backwards-compatible adapter for the original collision dispatcher API.
    /// New code should subscribe directly to RagdollCollisionHub.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Collision Event Dispatcher")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollDefinitionBindings))]
    public class RagdollCollisionEventDispatcher : MonoBehaviour
    {
        public delegate void CollisionEventListener(Collision collision, RagdollBone bone);

        public event CollisionEventListener OnCollisionEnter;
        public event CollisionEventListener OnCollisionStay;
        public event CollisionEventListener OnCollisionExit;

        RagdollDefinitionBindings bindings;
        RagdollCollisionHub collisionHub;

        void OnEnable()
        {
            bindings = GetComponent<RagdollDefinitionBindings>();
            collisionHub = GetComponent<RagdollCollisionHub>();
            if (!collisionHub)
            {
                collisionHub = gameObject.AddComponent<RagdollCollisionHub>();
            }

            collisionHub.CollisionReported += Dispatch;
        }

        void OnDisable()
        {
            if (collisionHub)
            {
                collisionHub.CollisionReported -= Dispatch;
            }
        }

        void Dispatch(RagdollCollisionEvent collisionEvent)
        {
            if (!bindings || !bindings.IsInitialized) return;

            RagdollBone bone;
            if (!bindings.TryGetBone(collisionEvent.Bone, out bone)) return;

            switch (collisionEvent.Phase)
            {
                case RagdollCollisionPhase.Enter:
                    OnCollisionEnter?.Invoke(collisionEvent.Collision, bone);
                    break;
                case RagdollCollisionPhase.Stay:
                    OnCollisionStay?.Invoke(collisionEvent.Collision, bone);
                    break;
                case RagdollCollisionPhase.Exit:
                    OnCollisionExit?.Invoke(collisionEvent.Collision, bone);
                    break;
            }
        }
    }
}
