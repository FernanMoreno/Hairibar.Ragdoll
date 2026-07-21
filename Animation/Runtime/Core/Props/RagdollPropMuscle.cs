using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollPropMuscleState
    {
        Uninitialized,
        PrimingEmptySlot,
        Empty,
        PreparingPickup,
        CancellingPickup,
        Reconnecting,
        Holding,
        Disconnecting,
        RestoringStandaloneBody,
        Recovering,
        Faulted
    }

    /// <summary>
    /// Permanent Puppet prop slot. It is registered as a runtime muscle in the Prop
    /// semantic group, deactivated while empty and reconnected only after the selected
    /// prop has surrendered its standalone Rigidbody.
    ///
    /// Assigning <see cref="CurrentProp"/> is asynchronous. The latest request wins and
    /// switches always complete the old drop before starting the new pickup.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    [AddComponentMenu("Ragdoll/Props/Ragdoll Prop Muscle")]
    [DisallowMultipleComponent]
    public sealed class RagdollPropMuscle : MonoBehaviour
    {
        [Header("Ragdoll")]
        [SerializeField] RagdollAnimator animator;
        [SerializeField] ConfigurableJoint propJoint;
        [SerializeField] Transform targetSlot;
        [SerializeField] Transform targetParent;
        [SerializeField] BoneName propBone = "Prop";
        [SerializeField] bool forceTreeHierarchy;
        [SerializeField] bool forceLayers = true;

        [Header("Startup")]
        [SerializeField] bool autoInitialize = true;
        [SerializeField] RagdollProp initialProp;

        RagdollBoneHandle handle = RagdollBoneHandle.Invalid;
        RagdollPropMuscleState state = RagdollPropMuscleState.Uninitialized;
        RagdollProp currentProp;
        RagdollProp requestedProp;
        RagdollProp transitionProp;
        RagdollPropReleaseState releaseState;
        bool releaseStateCaptured;
        bool currentPropReported;
        string lastError;
        string collisionPolicyError;
        string additionalPinError;
        bool slotRegistered;
        bool applicationQuitting;

        IRagdollPropMuscleRuntime runtime;

        static readonly List<RagdollPropMuscle> RegisteredMuscles =
            new List<RagdollPropMuscle>();

        public RagdollAnimator Animator => animator;
        public ConfigurableJoint Joint => propJoint;
        public Transform TargetSlot => targetSlot;
        public RagdollBoneHandle Handle => handle;
        public RagdollPropMuscleState State => state;
        public string LastError => lastError;
        public string CollisionPolicyError => collisionPolicyError;
        public string AdditionalPinError => additionalPinError;
        public bool IsInitialized => slotRegistered
            && state != RagdollPropMuscleState.Uninitialized
            && state != RagdollPropMuscleState.Faulted;
        public bool IsHoldingProp => currentProp;
        public bool IsTransitioning => state != RagdollPropMuscleState.Empty
            && state != RagdollPropMuscleState.Holding
            && state != RagdollPropMuscleState.Faulted
            && state != RagdollPropMuscleState.Uninitialized;

        /// <summary>
        /// Committed held prop. Assigning queues a pickup, drop or switch and does not
        /// mutate physics until a FixedUpdate boundary.
        /// </summary>
        public RagdollProp CurrentProp
        {
            get => currentProp;
            set => SetCurrentProp(value);
        }

        public RagdollProp RequestedProp => requestedProp;
        internal IRagdollPropMuscleRuntime RuntimeForCleanup => runtime;

        /// <summary>
        /// Clears additional-pin Target sampling after an external teleport or any
        /// discontinuous transform move. Call this after the completed teleport and
        /// after RagdollBehaviourController.NotifyTeleported.
        /// </summary>
        public void ResetAdditionalPinSampling()
        {
            RagdollProp held = currentProp ? currentProp : transitionProp;
            if (held) held.SuspendAdditionalPin();
            additionalPinError = null;
        }

        public event Action<RagdollProp> PropPickedUp;
        public event Action<RagdollProp> PropDropped;
        public event Action<RagdollProp, RagdollProp> PropChanged;
        public event Action<string> TransitionFailed;

        void Reset()
        {
            animator = GetComponentInParent<RagdollAnimator>();
            targetSlot = transform;
        }

        void Awake()
        {
            requestedProp = initialProp;
        }

        void OnEnable()
        {
            if (!RegisteredMuscles.Contains(this))
            {
                RegisteredMuscles.Add(this);
            }
        }

        void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        void FixedUpdate()
        {
            if (!autoInitialize
                && state == RagdollPropMuscleState.Uninitialized)
            {
                return;
            }
            TickSafely();
        }

        void OnDisable()
        {
            RegisteredMuscles.Remove(this);
            if (applicationQuitting) return;
            requestedProp = null;
            collisionPolicyError = null;
            additionalPinError = null;
            if (currentProp) currentProp.SuspendAdditionalPin();
            StabilizeInterruptedTransition(false);
        }

        void OnDestroy()
        {
            RegisteredMuscles.Remove(this);
            if (applicationQuitting) return;
            requestedProp = null;
            collisionPolicyError = null;
            additionalPinError = null;

            RagdollProp prop = transitionProp ? transitionProp : currentProp;
            if (prop) prop.SuspendAdditionalPin();
            if (prop && prop.CurrentMuscle == this)
            {
                bool restoreOriginal = !prop.IsPickupCommitted;
                RagdollPropReleaseState release = restoreOriginal
                    ? default(RagdollPropReleaseState)
                    : prop.CaptureReleaseState(
                        propJoint ? propJoint.GetComponent<Rigidbody>() : null);
                prop.RequestEmergencyStandaloneRestore(
                    this,
                    release,
                    restoreOriginal);
            }
            RequestEmptySlotForCleanup();
            currentProp = null;
            transitionProp = null;
        }

        public void Initialize()
        {
            autoInitialize = true;
        }

        public bool TryValidateConfiguration(out string error)
        {
            error = null;
            if (!propJoint)
            {
                error = "A ConfigurableJoint prop slot must be assigned.";
                return false;
            }
            Rigidbody body = propJoint.GetComponent<Rigidbody>();
            if (!body)
            {
                error = "The prop slot ConfigurableJoint requires a Rigidbody on the same GameObject.";
                return false;
            }
            if (!propJoint.connectedBody)
            {
                error = "The prop slot must connect to an existing Puppet Rigidbody.";
                return false;
            }
            if (!targetSlot)
            {
                error = "A Target prop slot must be assigned.";
                return false;
            }
            if (targetSlot == propJoint.transform
                || targetSlot.IsChildOf(propJoint.transform))
            {
                error = "The Target prop slot must live outside the physical Puppet hierarchy.";
                return false;
            }
            if (targetParent
                && (targetParent == targetSlot
                    || targetParent.IsChildOf(targetSlot)))
            {
                error = "The Target parent cannot be the Target slot or its descendant.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(propBone.ToString()))
            {
                error = "The prop slot requires a non-empty BoneName.";
                return false;
            }
            return true;
        }

        public void SetCurrentProp(RagdollProp prop)
        {
            string error;
            if (!TrySetCurrentProp(prop, out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public bool TrySetCurrentProp(
            RagdollProp prop,
            out string error)
        {
            error = null;
            if (state == RagdollPropMuscleState.Faulted)
            {
                error = "The prop muscle is faulted: " + lastError;
                return false;
            }
            if (prop && !prop.CanBePickedUpBy(this, out error)) return false;

            requestedProp = prop;
            return true;
        }

        public void Drop()
        {
            SetCurrentProp(null);
        }

        public bool TryDrop(out string error)
        {
            return TrySetCurrentProp(null, out error);
        }

        public void ClearFaultAndRetry()
        {
            string error;
            if (!TryRecoverFromFault(out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public bool TryRecoverFromFault(out string error)
        {
            error = null;
            if (state != RagdollPropMuscleState.Faulted)
            {
                return true;
            }

            lastError = null;
            collisionPolicyError = null;
            additionalPinError = null;
            if (currentProp && currentProp.IsPickupCommitted)
            {
                releaseState = currentProp.CaptureReleaseState(
                    propJoint ? propJoint.GetComponent<Rigidbody>() : null);
                releaseStateCaptured = true;
            }
            state = RagdollPropMuscleState.Recovering;
            return true;
        }

        void TickSafely()
        {
            try
            {
                TickStateMachine();
                ApplyAdditionalPinAfterAnimationMatching();

                RagdollProp held = currentProp ? currentProp : transitionProp;
                if (held && held.IsPickupCommitted)
                {
                    string collisionError;
                    collisionPolicyError = held.TryReapplyHeldCollisionIgnores(
                        animator,
                        handle,
                        out collisionError)
                            ? null
                            : collisionError;
                }
                else
                {
                    collisionPolicyError = null;
                }
            }
            catch (Exception exception)
            {
                Fail("Unhandled prop-muscle transition error: "
                    + exception.Message);
                Debug.LogException(exception, this);
            }
        }

        void ApplyAdditionalPinAfterAnimationMatching()
        {
            if (state != RagdollPropMuscleState.Holding
                || !currentProp
                || !currentProp.IsPickupCommitted
                || runtime == null
                || !propJoint
                || !targetSlot)
            {
                if (currentProp) currentProp.SuspendAdditionalPin();
                additionalPinError = null;
                return;
            }

            Rigidbody slotBody = propJoint.GetComponent<Rigidbody>();
            float authority;
            string contextError;
            if (!runtime.TryGetAdditionalPinAuthority(
                handle,
                slotBody,
                out authority,
                out contextError))
            {
                currentProp.SuspendAdditionalPin();
                additionalPinError = contextError;
                return;
            }

            string solverError;
            additionalPinError = currentProp.TryApplyAdditionalPin(
                targetSlot,
                slotBody,
                authority,
                Time.fixedDeltaTime,
                out solverError)
                    ? null
                    : solverError;
        }

        void TickStateMachine()
        {
            if (!slotRegistered)
            {
                TryRegisterSlot();
                return;
            }
            if (!RefreshHandle())
            {
                Fail("The prop muscle is no longer registered in the current ragdoll generation.");
                return;
            }

            switch (state)
            {
                case RagdollPropMuscleState.PrimingEmptySlot:
                    TickPrimingEmptySlot();
                    break;
                case RagdollPropMuscleState.Empty:
                    TickEmpty();
                    break;
                case RagdollPropMuscleState.PreparingPickup:
                    TickPreparingPickup();
                    break;
                case RagdollPropMuscleState.CancellingPickup:
                    TickCancellingPickup();
                    break;
                case RagdollPropMuscleState.Reconnecting:
                    TickReconnecting();
                    break;
                case RagdollPropMuscleState.Holding:
                    TickHolding();
                    break;
                case RagdollPropMuscleState.Disconnecting:
                    TickDisconnecting();
                    break;
                case RagdollPropMuscleState.RestoringStandaloneBody:
                    TickRestoringStandaloneBody();
                    break;
                case RagdollPropMuscleState.Recovering:
                    TickRecovering();
                    break;
            }
        }

        void TryRegisterSlot()
        {
            lastError = null;
            collisionPolicyError = null;
            additionalPinError = null;
            if (!TryValidateConfiguration(out lastError)) return;

            if (runtime == null)
            {
                if (!animator)
                {
                    animator = GetComponentInParent<RagdollAnimator>();
                }
                if (!animator)
                {
                    lastError = "A RagdollAnimator must be assigned.";
                    return;
                }
                runtime = new RagdollPropMuscleRuntimeAdapter(animator);
            }
            if (!runtime.IsReady)
            {
                lastError = "Waiting for RagdollAnimator and RagdollDefinitionBindings initialization.";
                return;
            }

            if (runtime.TryResolveSlot(propJoint, out handle))
            {
                if (!runtime.TryValidatePropGroup(handle, out lastError))
                {
                    Fail(lastError);
                    return;
                }
                slotRegistered = true;
                state = RagdollPropMuscleState.PrimingEmptySlot;
                return;
            }

            RagdollRuntimeMuscleRegistration registration =
                new RagdollRuntimeMuscleRegistration(
                    propBone,
                    propJoint,
                    targetSlot,
                    RagdollMuscleGroup.Prop,
                    targetParent ? targetParent : targetSlot.parent,
                    forceTreeHierarchy,
                    forceLayers);
            if (!runtime.TryRegisterSlot(
                registration,
                out handle,
                out lastError))
            {
                return;
            }
            if (!runtime.TryValidatePropGroup(handle, out lastError))
            {
                Fail(lastError);
                return;
            }

            slotRegistered = true;
            state = RagdollPropMuscleState.PrimingEmptySlot;
        }

        void TickPrimingEmptySlot()
        {
            RagdollMuscleConnectionState connection =
                runtime.GetConnectionState(handle);
            if (connection == RagdollMuscleConnectionState.Deactivated)
            {
                if (runtime.IsReconnecting(handle)) return;
                state = RagdollPropMuscleState.Empty;
                lastError = null;
                return;
            }

            string error;
            if (connection == RagdollMuscleConnectionState.Disconnected)
            {
                if (!runtime.IsReconnecting(handle)
                    && !runtime.TryReconnect(handle, out error))
                {
                    lastError = error;
                }
                return;
            }

            if (!runtime.IsDisconnecting(handle)
                && !runtime.TryDisconnect(handle, true, out error))
            {
                lastError = error;
            }
        }

        void TickEmpty()
        {
            RagdollMuscleConnectionState connection =
                runtime.GetConnectionState(handle);
            if (connection != RagdollMuscleConnectionState.Deactivated)
            {
                state = RagdollPropMuscleState.PrimingEmptySlot;
                return;
            }
            if (!requestedProp) return;

            string error;
            if (!requestedProp.CanBePickedUpBy(this, out error))
            {
                Fail(error);
                return;
            }
            transitionProp = requestedProp;
            if (!transitionProp.TryPreparePickup(
                this,
                propJoint.transform,
                targetSlot,
                out error))
            {
                Fail(error);
                return;
            }
            state = RagdollPropMuscleState.PreparingPickup;
        }

        void TickPreparingPickup()
        {
            if (!transitionProp)
            {
                Fail("The prop being prepared was destroyed.");
                return;
            }

            transitionProp.RefreshPendingBodyDestruction();
            if (!transitionProp.IsStandaloneBodyRemoved) return;

            if (requestedProp != transitionProp)
            {
                state = RagdollPropMuscleState.CancellingPickup;
                return;
            }

            string error;
            if (!runtime.IsReconnecting(handle)
                && !runtime.TryReconnect(handle, out error))
            {
                lastError = error;
                return;
            }
            state = RagdollPropMuscleState.Reconnecting;
        }

        void TickCancellingPickup()
        {
            if (!transitionProp)
            {
                state = RagdollPropMuscleState.Empty;
                return;
            }

            bool pending;
            string error;
            if (!transitionProp.TryCancelPreparedPickup(
                this,
                out pending,
                out error))
            {
                if (pending) return;
                Fail(error);
                return;
            }
            transitionProp = null;
            state = RagdollPropMuscleState.Empty;
            lastError = null;
        }

        void TickReconnecting()
        {
            if (!transitionProp)
            {
                Fail("The prop being connected was destroyed.");
                return;
            }
            RagdollMuscleConnectionState connection =
                runtime.GetConnectionState(handle);
            if (connection != RagdollMuscleConnectionState.Connected) return;

            string error;
            if (!transitionProp.TryCommitPickup(
                this,
                propJoint.GetComponent<Rigidbody>(),
                animator,
                handle,
                out error))
            {
                Fail(error);
                return;
            }
            currentProp = transitionProp;
            transitionProp = null;
            lastError = null;
            releaseStateCaptured = false;

            if (requestedProp != currentProp)
            {
                currentPropReported = false;
                BeginDisconnect(currentProp);
                return;
            }

            currentPropReported = true;
            state = RagdollPropMuscleState.Holding;
            InvokeSafely(PropPickedUp, currentProp);
            InvokeChangedSafely(null, currentProp);
        }

        void TickHolding()
        {
            if (!currentProp)
            {
                currentPropReported = false;
                BeginDisconnect(null);
                return;
            }
            if (requestedProp == currentProp) return;
            BeginDisconnect(currentProp);
        }

        void BeginDisconnect(RagdollProp prop)
        {
            if (prop)
            {
                releaseState = prop.CaptureReleaseState(
                    propJoint.GetComponent<Rigidbody>());
                releaseStateCaptured = true;
            }
            else
            {
                releaseState = default(RagdollPropReleaseState);
                releaseStateCaptured = false;
            }

            string error;
            if (!runtime.IsDisconnecting(handle)
                && !runtime.TryDisconnect(handle, true, out error))
            {
                lastError = error;
                return;
            }
            state = RagdollPropMuscleState.Disconnecting;
        }

        void TickDisconnecting()
        {
            RagdollMuscleConnectionState connection =
                runtime.GetConnectionState(handle);
            bool disabledSlotUnavailable = runtime.IsSimulationDisabled
                && propJoint
                && !propJoint.gameObject.activeInHierarchy;
            if (connection != RagdollMuscleConnectionState.Deactivated
                && !disabledSlotUnavailable)
            {
                return;
            }

            transitionProp = currentProp;
            currentProp = null;
            state = RagdollPropMuscleState.RestoringStandaloneBody;
        }

        void TickRestoringStandaloneBody()
        {
            if (!transitionProp)
            {
                FinishDrop(null);
                return;
            }

            bool pending;
            string error;
            if (!transitionProp.TryCompleteDrop(
                this,
                releaseState,
                out pending,
                out error))
            {
                if (pending) return;
                Fail(error);
                return;
            }
            RagdollProp dropped = transitionProp;
            transitionProp = null;
            FinishDrop(dropped);
        }

        void TickRecovering()
        {
            RagdollProp prop = transitionProp ? transitionProp : currentProp;
            bool reported = currentPropReported && prop == currentProp;
            if (prop && prop.CurrentMuscle == this)
            {
                if (prop.IsPickupCommitted && !releaseStateCaptured)
                {
                    releaseState = prop.CaptureReleaseState(
                        propJoint ? propJoint.GetComponent<Rigidbody>() : null);
                    releaseStateCaptured = true;
                }

                bool pending;
                string error;
                if (!prop.TryRecoverStandalone(
                    this,
                    releaseState,
                    out pending,
                    out error))
                {
                    if (pending) return;
                    Fail(error);
                    return;
                }
                if (reported)
                {
                    InvokeSafely(PropDropped, prop);
                    InvokeChangedSafely(prop, null);
                }
            }

            currentProp = null;
            transitionProp = null;
            currentPropReported = false;
            releaseStateCaptured = false;
            state = slotRegistered
                ? RagdollPropMuscleState.PrimingEmptySlot
                : RagdollPropMuscleState.Uninitialized;
            lastError = null;
        }

        void FinishDrop(RagdollProp dropped)
        {
            state = RagdollPropMuscleState.Empty;
            lastError = null;
            releaseStateCaptured = false;

            if (currentPropReported && dropped)
            {
                InvokeSafely(PropDropped, dropped);
                InvokeChangedSafely(dropped, null);
            }
            currentPropReported = false;
        }

        bool RefreshHandle()
        {
            return runtime != null
                && runtime.TryResolveSlot(propJoint, out handle);
        }

        void StabilizeInterruptedTransition(bool destroying)
        {
            if (!slotRegistered) return;
            if (state == RagdollPropMuscleState.Empty
                || state == RagdollPropMuscleState.PrimingEmptySlot
                || state == RagdollPropMuscleState.Uninitialized)
            {
                return;
            }

            RagdollProp prop = transitionProp ? transitionProp : currentProp;
            if (prop && prop.CurrentMuscle == this)
            {
                bool originalPose = !prop.IsPickupCommitted;
                RagdollPropReleaseState release = originalPose
                    ? default(RagdollPropReleaseState)
                    : prop.CaptureReleaseState(
                        propJoint ? propJoint.GetComponent<Rigidbody>() : null);
                prop.RequestEmergencyStandaloneRestore(
                    this,
                    release,
                    originalPose);
            }
            RequestEmptySlotForCleanup();
            currentProp = null;
            transitionProp = null;
            currentPropReported = false;
            releaseStateCaptured = false;
            state = RagdollPropMuscleState.PrimingEmptySlot;
        }

        void RequestEmptySlotForCleanup()
        {
            if (!slotRegistered || runtime == null || !propJoint) return;
            if (!runtime.TryResolveSlot(propJoint, out handle)) return;

            try
            {
                RagdollMuscleConnectionState connection =
                    runtime.GetConnectionState(handle);
                if (connection == RagdollMuscleConnectionState.Connected
                    && !runtime.IsDisconnecting(handle))
                {
                    string ignored;
                    runtime.TryDisconnect(handle, true, out ignored);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        void Fail(string error)
        {
            RagdollProp held = currentProp ? currentProp : transitionProp;
            if (held) held.SuspendAdditionalPin();
            additionalPinError = null;
            lastError = string.IsNullOrEmpty(error)
                ? "Unknown prop transition failure."
                : error;
            state = RagdollPropMuscleState.Faulted;
            InvokeFailureSafely(lastError);
        }

        void InvokeSafely(
            Action<RagdollProp> handlers,
            RagdollProp prop)
        {
            if (handlers == null) return;
            Delegate[] subscribers = handlers.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollProp>)subscribers[index])(prop);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        void InvokeChangedSafely(
            RagdollProp previous,
            RagdollProp next)
        {
            if (PropChanged == null) return;
            Delegate[] subscribers = PropChanged.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollProp, RagdollProp>)subscribers[index])(
                        previous,
                        next);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        void InvokeFailureSafely(string error)
        {
            if (TransitionFailed == null) return;
            Delegate[] subscribers = TransitionFailed.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<string>)subscribers[index])(error);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        internal void SetRuntimeForTesting(IRagdollPropMuscleRuntime value)
        {
            runtime = value;
        }

        internal static int GetRegistered(
            RagdollAnimator ownerAnimator,
            List<RagdollPropMuscle> results,
            HashSet<RagdollPropMuscle> unique = null)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            int added = 0;
            for (int index = RegisteredMuscles.Count - 1; index >= 0; index--)
            {
                RagdollPropMuscle muscle = RegisteredMuscles[index];
                if (!muscle)
                {
                    RegisteredMuscles.RemoveAt(index);
                    continue;
                }
                if (!muscle.animator)
                {
                    muscle.animator =
                        muscle.GetComponentInParent<RagdollAnimator>();
                }
                if (muscle.animator != ownerAnimator) continue;
                if (unique != null && !unique.Add(muscle)) continue;
                results.Add(muscle);
                added++;
            }
            return added;
        }

        internal void ConfigureForTesting(
            ConfigurableJoint joint,
            Transform target,
            BoneName bone)
        {
            propJoint = joint;
            targetSlot = target;
            targetParent = target ? target.parent : null;
            propBone = bone;
            autoInitialize = true;
        }

        internal void SetAnimatorForTesting(RagdollAnimator value)
        {
            animator = value;
        }

        internal void TickForTesting()
        {
            TickSafely();
        }

        internal void ApplyAdditionalPinForTesting()
        {
            ApplyAdditionalPinAfterAnimationMatching();
        }

        internal void EnterFaultForTesting(string error)
        {
            Fail(error);
        }
    }
}
