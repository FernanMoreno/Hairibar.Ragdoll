using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Owns the global Active/Kinematic/Disabled mode of a dual-rig ragdoll. The
    /// controller snapshots authored per-bone power and collision settings, applies a
    /// global override, and restores the authored state before returning to Active.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Ragdoll/Ragdoll Simulation Mode Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollAnimator))]
    public sealed class RagdollSimulationModeController : MonoBehaviour,
        IBoneProfileModifier,
        IOrderedRagdollModifier
    {
        enum TransitionKind
        {
            None,
            FadeOutActive,
            FadeInActive
        }

        internal sealed class HierarchySnapshot
        {
            internal readonly RagdollSimulationMode Mode;
            internal readonly Dictionary<BoneName, ActiveConfiguration> Active;

            internal HierarchySnapshot(
                RagdollSimulationMode mode,
                Dictionary<BoneName, ActiveConfiguration> active)
            {
                Mode = mode;
                Active = active;
            }
        }

        internal sealed class ActiveConfiguration
        {
            internal readonly PowerSetting PowerSetting;
            internal readonly bool DetectCollisions;
            internal readonly Dictionary<Collider, bool> ColliderEnabled;

            ActiveConfiguration(BoneSnapshot snapshot)
            {
                PowerSetting = snapshot.ActivePowerSetting;
                DetectCollisions = snapshot.ActiveDetectCollisions;
                ColliderEnabled = new Dictionary<Collider, bool>();
                for (int index = 0; index < snapshot.Colliders.Length; index++)
                {
                    Collider collider = snapshot.Colliders[index];
                    if (collider)
                    {
                        ColliderEnabled[collider] =
                            snapshot.ColliderEnabled[index];
                    }
                }
            }
        }

        sealed class BoneSnapshot
        {
            internal readonly RagdollBone Bone;
            internal readonly Rigidbody Rigidbody;
            internal readonly Collider[] Colliders;
            internal readonly bool[] ColliderEnabled;
            internal PowerSetting ActivePowerSetting;
            internal bool ActiveDetectCollisions;
            internal RagdollBone.OnPowerSettingChangedHandler PowerChangedHandler;

            internal BoneSnapshot(RagdollBone bone)
            {
                Bone = bone;
                Rigidbody = bone.Rigidbody;
                ActivePowerSetting = bone.PowerSetting;
                ActiveDetectCollisions = Rigidbody.detectCollisions;

                List<Collider> colliders = new List<Collider>();
                foreach (Collider collider in bone.Colliders)
                {
                    if (collider) colliders.Add(collider);
                }

                Colliders = colliders.ToArray();
                ColliderEnabled = new bool[Colliders.Length];
                CaptureCollisionConfiguration();
            }

            internal void CaptureCollisionConfiguration()
            {
                ActiveDetectCollisions = Rigidbody.detectCollisions;
                for (int index = 0; index < Colliders.Length; index++)
                {
                    ColliderEnabled[index] = Colliders[index]
                        && Colliders[index].enabled;
                }
            }

            internal void RestoreCollisionConfiguration()
            {
                Rigidbody.detectCollisions = ActiveDetectCollisions;
                for (int index = 0; index < Colliders.Length; index++)
                {
                    if (Colliders[index])
                    {
                        Colliders[index].enabled = ColliderEnabled[index];
                    }
                }
            }
        }

        [Header("Mode")]
        [SerializeField] RagdollSimulationMode initialMode =
            RagdollSimulationMode.Active;
        [SerializeField, Min(0f)] float transitionDuration = 0.25f;
        [SerializeField] bool zeroVelocitiesOnTransition = true;

        [Header("Lifetime")]
        [Tooltip("When this component is disabled directly, restore the Puppet to Active. Parent hierarchy deactivation is ignored.")]
        [SerializeField] bool restoreActiveWhenComponentDisabled = true;

        RagdollAnimator animator;
        RagdollDefinitionBindings bindings;
        RagdollSettings ragdollSettings;
        BoneSnapshot[] boneSnapshots;
        bool isInitialized;
        bool hasStarted;
        bool isChangingMode;
        bool isQuitting;
        bool suppressPowerSettingEvents;
        bool animatorEnabledBeforeDisabled;
        bool puppetActiveBeforeDisabled;
        bool lifecycleFreezeSuspended;
        bool lifecyclePermanentDestruction;

        RagdollSimulationMode currentMode = RagdollSimulationMode.Active;
        RagdollSimulationMode targetMode = RagdollSimulationMode.Active;
        TransitionKind transitionKind;
        RagdollSimulationTransition transition;
        float activeDriveWeight = 1f;

        public bool IsInitialized => isInitialized;
        public RagdollSimulationMode CurrentMode => currentMode;
        public RagdollSimulationMode TargetMode => IsTransitioning
            ? targetMode
            : currentMode;
        public bool IsTransitioning => transitionKind != TransitionKind.None;
        public float TransitionProgress => IsTransitioning
            ? transition.Progress
            : 1f;
        public float ActiveDriveWeight => activeDriveWeight;
        public Transform PuppetRoot => bindings ? bindings.transform : null;
        public bool IsLifecycleFreezeSuspended => lifecycleFreezeSuspended;

        public RagdollModifierStage Stage => RagdollModifierStage.Final;
        public int Priority => 1000;

        public event Action<RagdollSimulationMode, RagdollSimulationMode>
            TransitionStarted;
        public event Action<RagdollSimulationMode, RagdollSimulationMode>
            ModeChanged;
        public event Action<RagdollSimulationMode> TransitionCompleted;

        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            if (isInitialized) return;
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));

            animator = GetComponent<RagdollAnimator>();
            bindings = animator.Bindings;
            if (!bindings)
            {
                throw new InvalidOperationException(
                    "RagdollSimulationModeController requires RagdollDefinitionBindings.");
            }

            ragdollSettings = bindings.GetComponent<RagdollSettings>();
            ValidatePuppetRootOwnership();
            CreateBoneSnapshots(pairs);

            currentMode = RagdollSimulationMode.Active;
            targetMode = currentMode;
            activeDriveWeight = 1f;
            transitionKind = TransitionKind.None;
            isInitialized = true;
        }

        internal HierarchySnapshot CaptureHierarchySnapshot()
        {
            EnsureInitialized();
            Dictionary<BoneName, ActiveConfiguration> active =
                new Dictionary<BoneName, ActiveConfiguration>();
            for (int index = 0; index < boneSnapshots.Length; index++)
            {
                BoneSnapshot snapshot = boneSnapshots[index];
                active[snapshot.Bone.Name] =
                    new ActiveConfiguration(snapshot);
            }
            return new HierarchySnapshot(currentMode, active);
        }

        internal void RebuildHierarchy(
            IEnumerable<RagdollAnimator.AnimatedPair> pairs,
            HierarchySnapshot snapshot)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (IsTransitioning)
            {
                throw new InvalidOperationException(
                    "Simulation mode hierarchy cannot rebuild during a transition.");
            }

            UnsubscribeBoneSnapshots();
            bindings = animator.Bindings;
            ragdollSettings = bindings.GetComponent<RagdollSettings>();
            CreateBoneSnapshots(pairs);

            for (int index = 0; index < boneSnapshots.Length; index++)
            {
                BoneSnapshot bone = boneSnapshots[index];
                ActiveConfiguration active;
                if (snapshot.Active.TryGetValue(
                    bone.Bone.Name,
                    out active))
                {
                    bone.ActivePowerSetting = active.PowerSetting;
                    bone.ActiveDetectCollisions = active.DetectCollisions;
                    for (int colliderIndex = 0;
                        colliderIndex < bone.Colliders.Length;
                        colliderIndex++)
                    {
                        bool enabled;
                        if (bone.Colliders[colliderIndex]
                            && active.ColliderEnabled.TryGetValue(
                                bone.Colliders[colliderIndex],
                                out enabled))
                        {
                            bone.ColliderEnabled[colliderIndex] = enabled;
                        }
                    }
                    continue;
                }

                RagdollBoneHandle parentHandle;
                if (bindings.Topology.TryGetParent(
                    bindings.GetHandleAt(index),
                    out parentHandle))
                {
                    bone.ActivePowerSetting =
                        boneSnapshots[parentHandle.Index].ActivePowerSetting;
                }
            }

            currentMode = snapshot.Mode;
            targetMode = snapshot.Mode;
            transitionKind = TransitionKind.None;
            activeDriveWeight = snapshot.Mode == RagdollSimulationMode.Active
                ? 1f
                : 0f;

            if (snapshot.Mode == RagdollSimulationMode.Kinematic)
            {
                ForceAllBonesKinematic();
            }
            else
            {
                RestoreActivePowerSettings();
            }
        }

        public void Modify(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
            if (!isInitialized || currentMode != RagdollSimulationMode.Active)
            {
                return;
            }

            float multiplier = Mathf.Clamp01(activeDriveWeight);
            boneProfile.MultiplyPositionPinWeight(multiplier);
            boneProfile.rotationAlpha *= multiplier;
        }

        public bool SetMode(RagdollSimulationMode mode)
        {
            return SetMode(mode, transitionDuration);
        }

        public bool SetMode(RagdollSimulationMode mode, float duration)
        {
            EnsureInitialized();
            ValidateMode(mode);
            if (animator
                && RagdollSimulationModePolicy.LifecycleOwnsSimulation(
                    animator.State,
                    animator.IsKilling,
                    lifecycleFreezeSuspended))
            {
                return false;
            }
            if (isChangingMode)
            {
                throw new InvalidOperationException(
                    "A ragdoll simulation mode change is already in progress.");
            }

            if (!IsTransitioning && mode == currentMode)
            {
                return false;
            }

            if (IsTransitioning && mode == targetMode)
            {
                return false;
            }

            isChangingMode = true;
            try
            {
                RagdollSimulationMode transitionSource = currentMode;
                targetMode = mode;
                TransitionStarted?.Invoke(transitionSource, mode);

                if (mode == RagdollSimulationMode.Active)
                {
                    BeginTransitionToActive(Mathf.Max(0f, duration));
                }
                else
                {
                    BeginTransitionToNonActive(mode, Mathf.Max(0f, duration));
                }
            }
            finally
            {
                isChangingMode = false;
            }

            return true;
        }

        public bool SetModeImmediate(RagdollSimulationMode mode)
        {
            return SetMode(mode, 0f);
        }

        internal void ForceActiveForLifecycle()
        {
            EnsureInitialized();
            if (lifecycleFreezeSuspended)
            {
                throw new InvalidOperationException(
                    "Lifecycle Frozen must be resumed before forcing Active simulation.");
            }

            bool wasTransitioning = IsTransitioning;
            if (currentMode != RagdollSimulationMode.Active)
            {
                PrepareActiveMode();
            }

            activeDriveWeight = 1f;
            transitionKind = TransitionKind.None;
            targetMode = RagdollSimulationMode.Active;
            if (wasTransitioning)
            {
                TransitionCompleted?.Invoke(RagdollSimulationMode.Active);
            }
        }

        internal bool SuspendForLifecycleFreeze()
        {
            EnsureInitialized();
            if (lifecyclePermanentDestruction)
            {
                throw new InvalidOperationException(
                    "A permanently frozen simulation cannot be resumed or suspended again.");
            }
            if (lifecycleFreezeSuspended) return false;

            ForceActiveForLifecycle();

            lifecycleFreezeSuspended = true;
            bindings.gameObject.SetActive(false);
            return true;
        }

        internal bool ResumeFromLifecycleFreeze()
        {
            EnsureInitialized();
            if (!lifecycleFreezeSuspended) return false;
            if (lifecyclePermanentDestruction)
            {
                throw new InvalidOperationException(
                    "A permanently frozen simulation cannot be resumed.");
            }

            bindings.gameObject.SetActive(true);
            ForceAllBonesKinematic();
            RestoreCollisionConfiguration();
            animator.SnapToTargetPose();
            ZeroVelocities(true);
            RestoreActivePowerSettings();
            lifecycleFreezeSuspended = false;
            return true;
        }

        internal void AbandonLifecycleFreezeForPermanentDestruction()
        {
            lifecyclePermanentDestruction = true;
        }

        /// <summary>
        /// Captures current per-bone power and collision values as the configuration that
        /// will be restored after Kinematic or Disabled mode.
        /// </summary>
        public void RefreshActiveConfiguration()
        {
            EnsureInitialized();
            if (currentMode != RagdollSimulationMode.Active || IsTransitioning)
            {
                throw new InvalidOperationException(
                    "Active configuration can only be refreshed from a stable Active mode.");
            }

            CaptureActiveConfiguration();
        }

        void Start()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollSimulationModeController was not initialized by RagdollAnimator.");
            }

            hasStarted = true;
            if (animator.State != RagdollLifecycleState.Alive)
            {
                ForceActiveForLifecycle();
                return;
            }

            if (initialMode != RagdollSimulationMode.Active
                && currentMode == RagdollSimulationMode.Active
                && !IsTransitioning)
            {
                SetModeImmediate(initialMode);
            }
        }

        void FixedUpdate()
        {
            if (!isInitialized || !hasStarted) return;

            if (animator
                && RagdollSimulationModePolicy.RequiresActiveForLifecycle(
                    animator.State,
                    animator.IsKilling,
                    lifecycleFreezeSuspended,
                    currentMode,
                    IsTransitioning,
                    activeDriveWeight))
            {
                ForceActiveForLifecycle();
                return;
            }

            if (lifecycleFreezeSuspended) return;

            if (!IsTransitioning) return;

            activeDriveWeight = transition.Value;
            if (!transition.Advance(Time.fixedDeltaTime))
            {
                activeDriveWeight = transition.Value;
                return;
            }

            activeDriveWeight = transition.Value;
            isChangingMode = true;
            try
            {
                switch (transitionKind)
                {
                    case TransitionKind.FadeOutActive:
                        ApplyStableNonActiveMode(targetMode);
                        break;
                    case TransitionKind.FadeInActive:
                        activeDriveWeight = 1f;
                        CompleteTransition();
                        break;
                }
            }
            finally
            {
                isChangingMode = false;
            }
        }

        void BeginTransitionToActive(float duration)
        {
            if (currentMode != RagdollSimulationMode.Active)
            {
                PrepareActiveMode();
            }

            float remainingDuration =
                RagdollSimulationTransition.ScaleRemainingDuration(
                    duration,
                    activeDriveWeight,
                    1f);
            if (remainingDuration <= Mathf.Epsilon)
            {
                activeDriveWeight = 1f;
                CompleteTransition();
                return;
            }

            transitionKind = TransitionKind.FadeInActive;
            transition = new RagdollSimulationTransition(
                activeDriveWeight,
                1f,
                remainingDuration);
        }

        void BeginTransitionToNonActive(
            RagdollSimulationMode mode,
            float duration)
        {
            if (currentMode != RagdollSimulationMode.Active)
            {
                ApplyStableNonActiveMode(mode);
                return;
            }

            CaptureActiveConfiguration();
            float remainingDuration =
                RagdollSimulationTransition.ScaleRemainingDuration(
                    duration,
                    activeDriveWeight,
                    0f);
            if (remainingDuration <= Mathf.Epsilon)
            {
                activeDriveWeight = 0f;
                ApplyStableNonActiveMode(mode);
                return;
            }

            transitionKind = TransitionKind.FadeOutActive;
            transition = new RagdollSimulationTransition(
                activeDriveWeight,
                0f,
                remainingDuration);
        }

        void PrepareActiveMode()
        {
            if (currentMode == RagdollSimulationMode.Disabled)
            {
                RestorePuppetHierarchy();
            }

            ForceAllBonesKinematic();
            RestoreCollisionConfiguration();
            animator.SnapToTargetPose();
            ZeroVelocities();
            RestoreActivePowerSettings();

            RagdollSimulationMode previous = currentMode;
            currentMode = RagdollSimulationMode.Active;
            activeDriveWeight = 0f;
            ModeChanged?.Invoke(previous, currentMode);
        }

        void ApplyStableNonActiveMode(RagdollSimulationMode mode)
        {
            switch (mode)
            {
                case RagdollSimulationMode.Kinematic:
                    EnterKinematicMode();
                    break;
                case RagdollSimulationMode.Disabled:
                    EnterDisabledMode();
                    break;
                default:
                    throw new InvalidOperationException(
                        "A non-active transition must target Kinematic or Disabled mode.");
            }

            CompleteTransition();
        }

        void EnterKinematicMode()
        {
            if (currentMode == RagdollSimulationMode.Disabled)
            {
                RestorePuppetHierarchy();
            }

            ForceAllBonesKinematic();
            animator.SnapToTargetPose();
            RestoreCollisionConfiguration();
            ZeroVelocities();

            RagdollSimulationMode previous = currentMode;
            currentMode = RagdollSimulationMode.Kinematic;
            activeDriveWeight = 0f;
            if (previous != currentMode)
            {
                ModeChanged?.Invoke(previous, currentMode);
            }
        }

        void EnterDisabledMode()
        {
            if (currentMode == RagdollSimulationMode.Disabled) return;

            ForceAllBonesKinematic();
            ZeroVelocities();

            animatorEnabledBeforeDisabled = animator.enabled;
            puppetActiveBeforeDisabled = bindings.gameObject.activeSelf;
            animator.enabled = false;
            bindings.gameObject.SetActive(false);

            RagdollSimulationMode previous = currentMode;
            currentMode = RagdollSimulationMode.Disabled;
            activeDriveWeight = 0f;
            ModeChanged?.Invoke(previous, currentMode);
        }

        void RestorePuppetHierarchy()
        {
            bindings.gameObject.SetActive(true);
            RestoreCollisionConfiguration();
            animator.enabled = animatorEnabledBeforeDisabled;
            ForceAllBonesKinematic();
            animator.SnapToTargetPose();
            ZeroVelocities();

            if (!puppetActiveBeforeDisabled)
            {
                Debug.LogWarning(
                    "The Puppet root was already inactive before Disabled mode. It was activated so the requested simulation mode could run.",
                    this);
            }
        }

        void ForceAllBonesKinematic()
        {
            suppressPowerSettingEvents = true;
            try
            {
                for (int index = 0; index < boneSnapshots.Length; index++)
                {
                    BoneSnapshot snapshot = boneSnapshots[index];
                    snapshot.Bone.PowerSetting = PowerSetting.Kinematic;
                    snapshot.Rigidbody.isKinematic = true;
                }
            }
            finally
            {
                suppressPowerSettingEvents = false;
            }

            ReapplyRigidbodySettings();
        }

        void RestoreActivePowerSettings()
        {
            suppressPowerSettingEvents = true;
            try
            {
                for (int index = 0; index < boneSnapshots.Length; index++)
                {
                    BoneSnapshot snapshot = boneSnapshots[index];
                    snapshot.Bone.PowerSetting = snapshot.ActivePowerSetting;
                    snapshot.Rigidbody.isKinematic =
                        snapshot.ActivePowerSetting == PowerSetting.Kinematic;
                }
            }
            finally
            {
                suppressPowerSettingEvents = false;
            }

            ReapplyRigidbodySettings();
        }

        void CaptureActiveConfiguration()
        {
            for (int index = 0; index < boneSnapshots.Length; index++)
            {
                BoneSnapshot snapshot = boneSnapshots[index];
                snapshot.ActivePowerSetting = snapshot.Bone.PowerSetting;
                snapshot.CaptureCollisionConfiguration();
            }
        }

        void RestoreCollisionConfiguration()
        {
            for (int index = 0; index < boneSnapshots.Length; index++)
            {
                boneSnapshots[index].RestoreCollisionConfiguration();
            }
        }

        void ZeroVelocities(bool force = false)
        {
            if (!force && !zeroVelocitiesOnTransition) return;

            for (int index = 0; index < boneSnapshots.Length; index++)
            {
                Rigidbody rigidbody = boneSnapshots[index].Rigidbody;
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                if (!rigidbody.isKinematic)
                {
                    rigidbody.Sleep();
                }
            }
        }

        void CreateBoneSnapshots(
            IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            boneSnapshots = new BoneSnapshot[bindings.BoneCount];
            int count = 0;

            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                if (pair == null || !bindings.Topology.Contains(pair.Handle))
                {
                    throw new ArgumentException(
                        "Simulation mode initialization received an invalid animated pair.",
                        nameof(pairs));
                }

                if (boneSnapshots[pair.Handle.Index] != null)
                {
                    throw new ArgumentException(
                        "Simulation mode initialization received a duplicate bone handle.",
                        nameof(pairs));
                }

                BoneSnapshot snapshot = new BoneSnapshot(pair.RagdollBone);
                snapshot.PowerChangedHandler =
                    (previous, next) => HandlePowerSettingChanged(snapshot, next);
                snapshot.Bone.OnPowerSettingChanged += snapshot.PowerChangedHandler;
                boneSnapshots[pair.Handle.Index] = snapshot;
                count++;
            }

            if (count != bindings.BoneCount)
            {
                throw new ArgumentException(
                    "Simulation mode initialization requires one animated pair per registered bone.",
                    nameof(pairs));
            }
        }

        void HandlePowerSettingChanged(
            BoneSnapshot snapshot,
            PowerSetting next)
        {
            if (suppressPowerSettingEvents) return;

            snapshot.ActivePowerSetting = next;
            if (currentMode == RagdollSimulationMode.Active) return;

            suppressPowerSettingEvents = true;
            try
            {
                snapshot.Bone.PowerSetting = PowerSetting.Kinematic;
                snapshot.Rigidbody.isKinematic = true;
            }
            finally
            {
                suppressPowerSettingEvents = false;
            }

            ReapplyRigidbodySettings();
        }

        void ReapplyRigidbodySettings()
        {
            if (ragdollSettings)
            {
                ragdollSettings.ReapplyRigidbodySettings();
            }
        }

        void ValidatePuppetRootOwnership()
        {
            Transform puppetRoot = bindings.transform;
            if (transform == puppetRoot || transform.IsChildOf(puppetRoot))
            {
                throw new InvalidOperationException(
                    "RagdollSimulationModeController must live on the Target side of the dual rig, outside the Puppet hierarchy, so Disabled mode can be reversed.");
            }
        }

        void CompleteTransition()
        {
            transitionKind = TransitionKind.None;
            targetMode = currentMode;
            TransitionCompleted?.Invoke(currentMode);
        }

        void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollSimulationModeController has not been initialized by RagdollAnimator.");
            }
        }

        static void ValidateMode(RagdollSimulationMode mode)
        {
            if (mode != RagdollSimulationMode.Active
                && mode != RagdollSimulationMode.Kinematic
                && mode != RagdollSimulationMode.Disabled)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        void OnDisable()
        {
            if (!Application.isPlaying
                || isQuitting
                || !isInitialized
                || lifecycleFreezeSuspended
                || lifecyclePermanentDestruction
                || !restoreActiveWhenComponentDisabled
                || !gameObject.activeInHierarchy)
            {
                return;
            }

            SetModeImmediate(RagdollSimulationMode.Active);
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        void UnsubscribeBoneSnapshots()
        {
            if (boneSnapshots == null) return;
            for (int index = 0; index < boneSnapshots.Length; index++)
            {
                BoneSnapshot snapshot = boneSnapshots[index];
                if (snapshot != null && snapshot.Bone != null)
                {
                    snapshot.Bone.OnPowerSettingChanged -=
                        snapshot.PowerChangedHandler;
                }
            }
        }

        void OnDestroy()
        {
            UnsubscribeBoneSnapshots();
        }

        void OnValidate()
        {
            transitionDuration = Mathf.Max(0f, transitionDuration);
        }
    }
}
