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

        readonly CollisionChannel collisionReported = new CollisionChannel();
        readonly CollisionChannel collisionEntered = new CollisionChannel();
        readonly CollisionChannel collisionStayed = new CollisionChannel();
        readonly CollisionChannel collisionExited = new CollisionChannel();

        public event Action<RagdollCollisionEvent> CollisionReported
        {
            add { collisionReported.Add(value); RefreshRelayEnabledState(); }
            remove { collisionReported.Remove(value); RefreshRelayEnabledState(); }
        }
        public event Action<RagdollCollisionEvent> CollisionEntered
        {
            add { collisionEntered.Add(value); RefreshRelayEnabledState(); }
            remove { collisionEntered.Remove(value); RefreshRelayEnabledState(); }
        }
        public event Action<RagdollCollisionEvent> CollisionStayed
        {
            add { collisionStayed.Add(value); RefreshRelayEnabledState(); }
            remove { collisionStayed.Remove(value); RefreshRelayEnabledState(); }
        }
        public event Action<RagdollCollisionEvent> CollisionExited
        {
            add { collisionExited.Add(value); RefreshRelayEnabledState(); }
            remove { collisionExited.Remove(value); RefreshRelayEnabledState(); }
        }

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
            if (!isActiveAndEnabled) return;
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

            collisionReported.Invoke(collisionEvent, this);

            switch (phase)
            {
                case RagdollCollisionPhase.Enter:
                    collisionEntered.Invoke(collisionEvent, this);
                    break;
                case RagdollCollisionPhase.Stay:
                    collisionStayed.Invoke(collisionEvent, this);
                    break;
                case RagdollCollisionPhase.Exit:
                    collisionExited.Invoke(collisionEvent, this);
                    break;
            }
        }

        bool HasSubscribers(RagdollCollisionPhase phase)
        {
            if (collisionReported.HasSubscribers) return true;

            switch (phase)
            {
                case RagdollCollisionPhase.Enter:
                    return collisionEntered.HasSubscribers;
                case RagdollCollisionPhase.Stay:
                    return collisionStayed.HasSubscribers;
                case RagdollCollisionPhase.Exit:
                    return collisionExited.HasSubscribers;
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
            ownedRelays.Clear();

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
                    UnityEngine.Debug.LogError(
                        "A Rigidbody cannot be registered with more than one RagdollCollisionHub.",
                        bone.Rigidbody);
                    continue;
                }

                relay.Initialize(this, bindings.GetHandleAt(index));
                relay.enabled = HasAnySubscribers;
                if (!ownedRelays.Contains(relay))
                {
                    ownedRelays.Add(relay);
                }
            }
        }

        bool HasAnySubscribers => collisionReported.HasSubscribers
            || collisionEntered.HasSubscribers
            || collisionStayed.HasSubscribers
            || collisionExited.HasSubscribers;

        /// <summary>
        /// Copy-on-write preserves multicast ordering and snapshot semantics without
        /// allocating in the physics callback. Each listener is isolated so one gameplay
        /// exception cannot suppress the remaining collision consumers.
        /// </summary>
        sealed class CollisionChannel
        {
            static readonly Action<RagdollCollisionEvent>[] Empty =
                new Action<RagdollCollisionEvent>[0];

            Action<RagdollCollisionEvent>[] subscribers = Empty;

            internal bool HasSubscribers => subscribers.Length > 0;

            internal void Add(Action<RagdollCollisionEvent> callback)
            {
                if (callback == null) return;

                Action<RagdollCollisionEvent>[] previous = subscribers;
                Action<RagdollCollisionEvent>[] next =
                    new Action<RagdollCollisionEvent>[previous.Length + 1];
                Array.Copy(previous, next, previous.Length);
                next[previous.Length] = callback;
                subscribers = next;
            }

            internal void Remove(Action<RagdollCollisionEvent> callback)
            {
                if (callback == null) return;

                Action<RagdollCollisionEvent>[] previous = subscribers;
                for (int index = previous.Length - 1; index >= 0; index--)
                {
                    if (previous[index] != callback) continue;

                    if (previous.Length == 1)
                    {
                        subscribers = Empty;
                        return;
                    }

                    Action<RagdollCollisionEvent>[] next =
                        new Action<RagdollCollisionEvent>[previous.Length - 1];
                    if (index > 0)
                    {
                        Array.Copy(previous, 0, next, 0, index);
                    }
                    if (index < previous.Length - 1)
                    {
                        Array.Copy(
                            previous,
                            index + 1,
                            next,
                            index,
                            previous.Length - index - 1);
                    }
                    subscribers = next;
                    return;
                }
            }

            internal void Invoke(
                RagdollCollisionEvent collisionEvent,
                UnityEngine.Object context)
            {
                Action<RagdollCollisionEvent>[] snapshot = subscribers;
                for (int index = 0; index < snapshot.Length; index++)
                {
                    try
                    {
                        snapshot[index](collisionEvent);
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogException(exception, context);
                    }
                }
            }
        }

        void RefreshRelayEnabledState()
        {
            bool enabledState = HasAnySubscribers;
            for (int index = 0; index < ownedRelays.Count; index++)
            {
                if (ownedRelays[index]) ownedRelays[index].enabled = enabledState;
            }
        }

        void OnEnable()
        {
            bindings = GetComponent<RagdollDefinitionBindings>();
            bindings.SubscribeToOnBonesCreated(RebuildRelays);
            bindings.SubscribeToRuntimeHierarchyChanged(RebuildRelays);
            RebuildRelays();
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
