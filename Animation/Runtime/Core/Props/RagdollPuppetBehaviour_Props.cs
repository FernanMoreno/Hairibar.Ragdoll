using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public sealed partial class RagdollPuppetBehaviour
    {
        [Header("Props")]
        [SerializeField]
        [Tooltip("Drop every held prop whenever BehaviourPuppet enters Unpinned.")]
        bool dropProps = true;

        [SerializeField]
        [Tooltip("Include active RagdollPropMuscle slots registered to this RagdollAnimator.")]
        bool autoDiscoverPropMuscles = true;

        [SerializeField]
        RagdollPropMuscle[] propMuscles = new RagdollPropMuscle[0];

        readonly List<RagdollPropMuscle> resolvedPropMuscles =
            new List<RagdollPropMuscle>();

        public bool DropProps
        {
            get => dropProps;
            set
            {
                bool previous = dropProps;
                dropProps = value;
                if (!previous && value && State == RagdollPuppetState.Unpinned)
                {
                    DropPropsNow();
                }
            }
        }

        public bool AutoDiscoverPropMuscles
        {
            get => autoDiscoverPropMuscles;
            set => autoDiscoverPropMuscles = value;
        }

        public RagdollPropMuscle[] PropMuscles
        {
            get => propMuscles ?? new RagdollPropMuscle[0];
            set => propMuscles = value ?? new RagdollPropMuscle[0];
        }
        public int LastRequestedPropDropCount { get; private set; }
        public int FailedPropDropRequestCount { get; private set; }

        public event Action<int> PropsDropRequested;
        public event Action<RagdollPropMuscle, string> PropDropRequestFailed;

        /// <summary>
        /// Queues a drop on every held or pending prop slot. Physical changes remain
        /// asynchronous and are committed by each RagdollPropMuscle in FixedUpdate.
        /// </summary>
        public int DropPropsNow()
        {
            ResolvePropMuscles();
            int requested = 0;
            for (int index = 0; index < resolvedPropMuscles.Count; index++)
            {
                RagdollPropMuscle muscle = resolvedPropMuscles[index];
                if (!muscle || (!muscle.CurrentProp && !muscle.RequestedProp))
                {
                    continue;
                }

                string error;
                try
                {
                    if (muscle.TryDrop(out error))
                    {
                        requested++;
                    }
                    else
                    {
                        FailedPropDropRequestCount++;
                        InvokePropDropFailureSafely(muscle, error);
                    }
                }
                catch (Exception exception)
                {
                    FailedPropDropRequestCount++;
                    error = "Drop request threw: " + exception.Message;
                    Debug.LogException(exception, muscle);
                    InvokePropDropFailureSafely(muscle, error);
                }
            }

            LastRequestedPropDropCount = requested;
            InvokePropsDropRequestedSafely(requested);
            return requested;
        }

        void ApplyPropDropPolicy(
            RagdollPuppetState previous,
            RagdollPuppetState current)
        {
            if (dropProps && ShouldDropProps(previous, current))
            {
                DropPropsNow();
            }
        }

        void ApplyPropDropPolicyForCurrentState()
        {
            if (dropProps && State == RagdollPuppetState.Unpinned)
            {
                DropPropsNow();
            }
        }

        internal static bool ShouldDropProps(
            RagdollPuppetState previous,
            RagdollPuppetState current)
        {
            return previous != RagdollPuppetState.Unpinned
                && current == RagdollPuppetState.Unpinned;
        }

        void ResolvePropMuscles()
        {
            resolvedPropMuscles.Clear();
            HashSet<RagdollPropMuscle> unique =
                new HashSet<RagdollPropMuscle>();
            RagdollAnimator owner = ResolvePropOwnerAnimator();

            if (propMuscles != null)
            {
                for (int index = 0; index < propMuscles.Length; index++)
                {
                    RagdollPropMuscle muscle = propMuscles[index];
                    if (!muscle || !BelongsToPropOwner(muscle, owner)
                        || !unique.Add(muscle))
                    {
                        continue;
                    }
                    resolvedPropMuscles.Add(muscle);
                }
            }

            if (autoDiscoverPropMuscles && owner)
            {
                RagdollPropMuscle.GetRegistered(
                    owner,
                    resolvedPropMuscles,
                    unique);
            }
        }

        RagdollAnimator ResolvePropOwnerAnimator()
        {
            if (IsInitialized) return Context.Animator;
            RagdollAnimator owner = GetComponent<RagdollAnimator>();
            return owner ? owner : GetComponentInParent<RagdollAnimator>();
        }

        static bool BelongsToPropOwner(
            RagdollPropMuscle muscle,
            RagdollAnimator owner)
        {
            if (!owner) return true;
            RagdollAnimator muscleOwner = muscle.Animator
                ? muscle.Animator
                : muscle.GetComponentInParent<RagdollAnimator>();
            return !muscleOwner || muscleOwner == owner;
        }

        void InvokePropsDropRequestedSafely(int count)
        {
            if (PropsDropRequested == null) return;
            Delegate[] subscribers = PropsDropRequested.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<int>)subscribers[index])(count);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        void InvokePropDropFailureSafely(
            RagdollPropMuscle muscle,
            string error)
        {
            if (PropDropRequestFailed == null) return;
            Delegate[] subscribers = PropDropRequestFailed.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollPropMuscle, string>)subscribers[index])(
                        muscle,
                        error);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        internal void ConfigurePropDropForTesting(
            RagdollPropMuscle[] muscles,
            bool drop,
            bool autoDiscover)
        {
            propMuscles = muscles ?? new RagdollPropMuscle[0];
            dropProps = drop;
            autoDiscoverPropMuscles = autoDiscover;
        }

        internal void HandlePropStateChangeForTesting(
            RagdollPuppetState previous,
            RagdollPuppetState current)
        {
            ApplyPropDropPolicy(previous, current);
        }

        internal void ApplyCurrentPropStateForTesting(
            RagdollPuppetState current)
        {
            if (dropProps && current == RagdollPuppetState.Unpinned)
            {
                DropPropsNow();
            }
        }
    }
}
