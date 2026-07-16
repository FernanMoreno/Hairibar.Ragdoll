using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Central collision stream for every registered Rigidbody in a ragdoll.
    /// One relay is reused per Rigidbody, while all consumers subscribe to this hub.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Collision Hub")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollDefinitionBindings))]
    public sealed class RagdollCollisionHub : MonoBehaviour
    {
        [SerializeField, Min(0)] int maxEventsPerFixedStep = 32;

        public event Action<RagdollCollisionEvent> CollisionReported;
        public event Action<RagdollCollisionEvent> CollisionEntered;
        public event Action<RagdollCollisionEvent> CollisionStayed;
        public event Action<RagdollCollisionEvent> CollisionExited;

        public int MaxEventsPerFixedStep
        {
            get => maxEventsPerFixedStep;
            set => maxEventsPerFixedStep = Mathf.Max(0, value);
        }

        /// <summary>
        /// Total number of callbacks discarded by the per-step event budget.
        /// </summary>
        public int DroppedEventCount { get; private set; }

        RagdollDefinitionBindings bindings;
        readonly List<RagdollCollisionRelay> ownedRelays = new List<RagdollCollisionRelay>();
        RagdollCollisionEventBudget eventBudget;
        long sequence;

        internal void Dispatch(
            RagdollBoneHandle bone,
            RagdollCollisionPhase phase,
            Collision collision)
        {
            if (!bindings || !bindings.IsInitialized) return;
            if (!bindings.Topology.Contains(bone)) return;
            if (!HasSubscribers(phase)) return;

            float fixedTime = Time.fixedTime;
            if (!eventBudget.TryConsume(fixedTime, maxEventsPerFixedStep))
            {
                DroppedEventCount++;
                return;
            }

            RagdollCollisionEvent collisionEvent = new RagdollCollisionEvent(
                bone,
                phase,
                collision,
                fixedTime,
                ++sequence);

            CollisionReported?.Invoke(collisionEvent);

            switch (phase)
            {
                case RagdollCollisionPhase.Enter:
                    CollisionEntered?.Invoke(collisionEvent);
                    break;
                case RagdollCollisionPhase.Stay:
                    CollisionStayed?.Invoke(collisionEvent);
                    break;
                case RagdollCollisionPhase.Exit:
                    CollisionExited?.Invoke(collisionEvent);
                    break;
            }
        }

        bool HasSubscribers(RagdollCollisionPhase phase)
        {
            if (CollisionReported != null) return true;

            switch (phase)
            {
                case RagdollCollisionPhase.Enter:
                    return CollisionEntered != null;
                case RagdollCollisionPhase.Stay:
                    return CollisionStayed != null;
                case RagdollCollisionPhase.Exit:
                    return CollisionExited != null;
                default:
                    return false;
            }
        }

        void RebuildRelays()
        {
            for (int i = 0; i < ownedRelays.Count; i++)
            {
                RagdollCollisionRelay relay = ownedRelays[i];
                if (relay) relay.Detach(this);
            }

            if (!bindings || !bindings.IsInitialized) return;

            for (int index = 0; index < bindings.BoneCount; index++)
            {
                RagdollBone bone = bindings.GetBoneAt(index);
                RagdollCollisionRelay relay = bone.Rigidbody.GetComponent<RagdollCollisionRelay>();
                if (!relay)
                {
                    relay = bone.Rigidbody.gameObject.AddComponent<RagdollCollisionRelay>();
                }
                else if (relay.Owner && relay.Owner != this)
                {
                    Debug.LogError(
                        "A Rigidbody cannot be registered with more than one RagdollCollisionHub.",
                        bone.Rigidbody);
                    continue;
                }

                relay.Initialize(this, bindings.GetHandleAt(index));
                if (!ownedRelays.Contains(relay))
                {
                    ownedRelays.Add(relay);
                }
            }
        }

        void OnEnable()
        {
            bindings = GetComponent<RagdollDefinitionBindings>();
            bindings.SubscribeToOnBonesCreated(RebuildRelays);
            bindings.SubscribeToRuntimeHierarchyChanged(RebuildRelays);
        }

        void OnDisable()
        {
            if (bindings)
            {
                bindings.UnsubscribeFromOnBonesCreated(RebuildRelays);
                bindings.UnsubscribeFromRuntimeHierarchyChanged(RebuildRelays);
            }
        }

        void OnValidate()
        {
            maxEventsPerFixedStep = Mathf.Max(0, maxEventsPerFixedStep);
        }

        void OnDestroy()
        {
            if (bindings)
            {
                bindings.UnsubscribeFromOnBonesCreated(RebuildRelays);
                bindings.UnsubscribeFromRuntimeHierarchyChanged(RebuildRelays);
            }

            for (int i = 0; i < ownedRelays.Count; i++)
            {
                RagdollCollisionRelay relay = ownedRelays[i];
                if (!relay) continue;

                relay.Detach(this);
                if (Application.isPlaying)
                {
                    Destroy(relay);
                }
                else
                {
                    DestroyImmediate(relay);
                }
            }
        }
    }
}
