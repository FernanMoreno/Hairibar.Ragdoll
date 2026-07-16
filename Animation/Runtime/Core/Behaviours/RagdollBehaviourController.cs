using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Owns a set of modular ragdoll behaviours and delegates the existing animation,
    /// mapping, target-pose and collision pipelines to exactly one active behaviour.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Behaviour Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollAnimator), typeof(RagdollMuscleController))]
    public sealed class RagdollBehaviourController : MonoBehaviour,
        IBoneProfileModifier,
        IRagdollMappingModifier,
        ITargetPoseModifier,
        IOrderedRagdollModifier
    {
        enum LifecycleNotification
        {
            KillStarted,
            KillEnded,
            Resurrected,
            Frozen,
            Unfrozen
        }

        [Tooltip("Optional root containing the behaviour components. If left empty, the RagdollAnimator hierarchy is searched.")]
        [SerializeField] Transform behaviourRoot;

        RagdollAnimator animator;
        RagdollMuscleController muscles;
        RagdollCollisionHub collisionHub;
        RagdollBehaviourContext context;
        RagdollBehaviourCollection collection;
        bool collisionSubscribed;
        bool isInitialized;
        bool isChangingBehaviour;
        bool lifecycleFrozen;

        public bool IsInitialized => isInitialized;
        public RagdollBehaviourBase ActiveBehaviour =>
            collection != null ? collection.Active : null;
        public IReadOnlyList<RagdollBehaviourBase> Behaviours =>
            collection != null
                ? collection.Behaviours
                : EmptyBehaviours;
        public Transform BehaviourRoot => behaviourRoot ? behaviourRoot : transform;
        public RagdollBehaviourContext Context => context;
        public bool IsLifecycleFrozen => lifecycleFrozen;

        public RagdollModifierStage Stage => RagdollModifierStage.Behaviour;
        public int Priority => 0;

        /// <summary>Raised after a switch with the previous and current behaviours.</summary>
        public event Action<RagdollBehaviourBase, RagdollBehaviourBase>
            ActiveBehaviourChanged;

        static readonly RagdollBehaviourBase[] EmptyBehaviours =
            new RagdollBehaviourBase[0];

        public void SetBehaviourRoot(Transform root)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException(
                    "The behaviour root cannot be changed after initialization.");
            }

            behaviourRoot = root;
        }

        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            // The controller implements two initialized modifier interfaces, so the
            // RagdollAnimator can legitimately call this method more than once.
            if (IsInitialized) return;
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));

            animator = GetComponent<RagdollAnimator>();
            muscles = GetComponent<RagdollMuscleController>();
            if (!muscles || !muscles.IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollBehaviourController must initialize after RagdollMuscleController.");
            }

            RagdollDefinitionBindings bindings = animator.Bindings;
            collisionHub = bindings.GetComponent<RagdollCollisionHub>();
            if (!collisionHub)
            {
                collisionHub = bindings.gameObject.AddComponent<RagdollCollisionHub>();
            }

            context = new RagdollBehaviourContext(
                this,
                animator,
                muscles,
                collisionHub,
                pairs);

            RagdollBehaviourBase[] discovered =
                BehaviourRoot.GetComponentsInChildren<RagdollBehaviourBase>(true);
            collection = new RagdollBehaviourCollection(discovered);

            for (int index = 0; index < discovered.Length; index++)
            {
                discovered[index].InitializeInternal(context);
            }

            isInitialized = true;
            SubscribeCollisionEvents();

            int enabledCount;
            RagdollBehaviourBase initial =
                collection.FindInitiallyEnabled(out enabledCount);
            if (enabledCount > 1)
            {
                Debug.LogWarning(
                    "Multiple ragdoll behaviours were enabled at initialization. "
                    + "The first behaviour in the configured root was activated and the others were disabled.",
                    this);
            }

            if (initial)
            {
                Activate(initial);
            }
        }

        public bool Activate(RagdollBehaviourBase behaviour)
        {
            EnsureInitialized();
            if (isChangingBehaviour)
            {
                throw new InvalidOperationException(
                    "A ragdoll behaviour switch is already in progress.");
            }

            RagdollBehaviourBase previous;
            if (!collection.TrySetActive(behaviour, out previous))
            {
                return false;
            }

            isChangingBehaviour = true;
            try
            {
                if (previous)
                {
                    previous.SetActiveInternal(false);
                }

                IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
                for (int index = 0; index < registered.Count; index++)
                {
                    RagdollBehaviourBase candidate = registered[index];
                    candidate.enabled = ShouldEnableBehaviour(
                        lifecycleFrozen,
                        ReferenceEquals(candidate, behaviour));
                }

                if (behaviour)
                {
                    behaviour.SetActiveInternal(true);
                }
            }
            finally
            {
                isChangingBehaviour = false;
            }

            ActiveBehaviourChanged?.Invoke(previous, behaviour);
            return true;
        }

        public bool Activate<T>() where T : RagdollBehaviourBase
        {
            EnsureInitialized();

            IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                T behaviour = registered[index] as T;
                if (behaviour)
                {
                    return Activate(behaviour);
                }
            }

            return false;
        }

        public bool TryGetBehaviour<T>(out T behaviour)
            where T : RagdollBehaviourBase
        {
            if (collection != null)
            {
                IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
                for (int index = 0; index < registered.Count; index++)
                {
                    behaviour = registered[index] as T;
                    if (behaviour) return true;
                }
            }

            behaviour = null;
            return false;
        }

        public bool Deactivate(RagdollBehaviourBase behaviour)
        {
            EnsureInitialized();
            return ReferenceEquals(ActiveBehaviour, behaviour)
                && Activate(null);
        }

        public bool DeactivateActiveBehaviour()
        {
            EnsureInitialized();
            return Activate(null);
        }

        internal void NotifyKillStarted()
        {
            NotifyLifecycle(LifecycleNotification.KillStarted);
        }

        internal void NotifyKillEnded()
        {
            NotifyLifecycle(LifecycleNotification.KillEnded);
        }

        internal void NotifyResurrected()
        {
            NotifyLifecycle(LifecycleNotification.Resurrected);
        }

        internal void NotifyFrozen()
        {
            EnsureInitialized();
            if (lifecycleFrozen) return;

            NotifyLifecycle(LifecycleNotification.Frozen);
            lifecycleFrozen = true;
            UnsubscribeCollisionEvents();
            SetBehaviourComponentsEnabled(false);
        }

        internal void NotifyUnfrozen()
        {
            EnsureInitialized();
            if (!lifecycleFrozen) return;

            NotifyLifecycle(LifecycleNotification.Unfrozen);
            lifecycleFrozen = false;
            SetBehaviourComponentsEnabled(true);
            SubscribeCollisionEvents();
        }

        internal void DestroyBehavioursForPermanentFreeze()
        {
            EnsureInitialized();
            lifecycleFrozen = true;
            IReadOnlyList<RagdollBehaviourBase> registered =
                collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                if (registered[index])
                {
                    Destroy(registered[index]);
                }
            }
        }

        void SetBehaviourComponentsEnabled(bool enableSelected)
        {
            IReadOnlyList<RagdollBehaviourBase> registered =
                collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                RagdollBehaviourBase behaviour = registered[index];
                if (!behaviour) continue;

                behaviour.enabled = ShouldEnableBehaviour(
                    lifecycleFrozen || !enableSelected,
                    ReferenceEquals(behaviour, ActiveBehaviour));
            }
        }

        void NotifyLifecycle(LifecycleNotification phase)
        {
            EnsureInitialized();
            IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                RagdollBehaviourBase behaviour = registered[index];
                if (!behaviour) continue;

                switch (phase)
                {
                    case LifecycleNotification.KillStarted:
                        behaviour.KillStartedInternal();
                        break;
                    case LifecycleNotification.KillEnded:
                        behaviour.KillEndedInternal();
                        break;
                    case LifecycleNotification.Resurrected:
                        behaviour.ResurrectedInternal();
                        break;
                    case LifecycleNotification.Frozen:
                        behaviour.FrozenInternal();
                        break;
                    case LifecycleNotification.Unfrozen:
                        behaviour.UnfrozenInternal();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(phase));
                }
            }
        }

        /// <summary>
        /// Notifies all initialized behaviours after an external system has teleported the
        /// Target and Puppet. This method does not move Transforms or Rigidbodies itself.
        /// The caller must supply the exact world-space delta used by the completed teleport.
        /// </summary>
        public void NotifyTeleported(
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot,
            bool moveToTarget)
        {
            EnsureInitialized();

            IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                RagdollBehaviourBase behaviour = registered[index];
                if (behaviour)
                {
                    behaviour.TeleportInternal(
                        deltaRotation,
                        deltaPosition,
                        pivot,
                        moveToTarget);
                }
            }
        }

        internal void ReactivateAfterAnimator()
        {
            if (!IsInitialized || !isActiveAndEnabled || lifecycleFrozen)
            {
                return;
            }

            IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                RagdollBehaviourBase behaviour = registered[index];
                if (behaviour)
                {
                    behaviour.ReactivateInternal();
                }
            }
        }

        internal float ResolveLifecycleMuscleWeight(
            RagdollAnimator.AnimatedPair pair)
        {
            if (!isActiveAndEnabled
                || lifecycleFrozen
                || !ActiveBehaviour
                || !ActiveBehaviour.isActiveAndEnabled
                || pair == null)
            {
                return 1f;
            }

            return ActiveBehaviour.GetLifecycleMuscleWeightInternal(pair);
        }

        public void Modify(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
            if (!CanDispatch) return;

            ActiveBehaviour.ModifyBoneProfileInternal(
                ref boneProfile,
                pair,
                deltaTime);
        }

        public void ModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
            if (!CanDispatch) return;

            ActiveBehaviour.ModifyMappingInternal(
                ref mappingWeights,
                pair);
        }

        public void ModifyPose(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            if (!CanDispatch) return;

            // This callback is invoked by RagdollAnimator from its own FixedUpdate,
            // before animation matching. It avoids depending on Unity's component
            // execution order for a separate behaviour FixedUpdate.
            ActiveBehaviour.FixedUpdateInternal(Time.fixedDeltaTime);
            ActiveBehaviour.ModifyTargetPoseInternal(context.Pairs);
        }

        void HandleCollision(RagdollCollisionEvent collisionEvent)
        {
            if (!CanDispatch) return;
            ActiveBehaviour.CollisionInternal(collisionEvent);
        }

        bool CanDispatch =>
            isActiveAndEnabled
            && animator
            && LifecycleAllowsDispatch(
                animator.State,
                animator.ActiveState,
                animator.IsKilling,
                lifecycleFrozen)
            && ActiveBehaviour
            && ActiveBehaviour.isActiveAndEnabled;

        internal static bool LifecycleAllowsDispatch(
            RagdollLifecycleState requestedState,
            RagdollLifecycleState activeState,
            bool isKilling,
            bool isFrozen)
        {
            return !isFrozen
                && !isKilling
                && requestedState == RagdollLifecycleState.Alive
                && activeState == RagdollLifecycleState.Alive;
        }

        internal static bool ShouldEnableBehaviour(
            bool isFrozen,
            bool isSelected)
        {
            return !isFrozen && isSelected;
        }

        void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollBehaviourController has not been initialized by a RagdollAnimator.");
            }
        }

        void SubscribeCollisionEvents()
        {
            if (collisionSubscribed
                || lifecycleFrozen
                || !collisionHub
                || !isActiveAndEnabled)
            {
                return;
            }

            collisionHub.CollisionReported += HandleCollision;
            collisionSubscribed = true;
        }

        void UnsubscribeCollisionEvents()
        {
            if (!collisionSubscribed || !collisionHub) return;

            collisionHub.CollisionReported -= HandleCollision;
            collisionSubscribed = false;
        }

        void OnEnable()
        {
            if (IsInitialized)
            {
                SubscribeCollisionEvents();
            }
        }

        void OnDisable()
        {
            UnsubscribeCollisionEvents();
        }

        void OnDestroy()
        {
            UnsubscribeCollisionEvents();

            if (collection == null) return;

            isInitialized = false;
            IReadOnlyList<RagdollBehaviourBase> registered = collection.Behaviours;
            for (int index = 0; index < registered.Count; index++)
            {
                if (registered[index])
                {
                    registered[index].ShutdownInternal();
                }
            }

            context = null;
            collection = null;
        }
    }
}
