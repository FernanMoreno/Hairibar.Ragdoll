using Hairibar.NaughtyExtensions;
using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649
namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Matches a target rig's animation by applying appropiate forces to a ragdoll.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Animator"), DisallowMultipleComponent]
    public partial class RagdollAnimator : MonoBehaviour, ISerializationCallbackReceiver
    {
        #region Public Properties
        public RagdollAnimationProfile Profile
        {
            get => currentProfile;
            set
            {
                RagdollProfile.ValidateAsArgument(value, Bindings.Definition, true, "Tried to set a null AnimationProfile at RagdollAnimator.");

                if (Application.isPlaying)
                {
                    TransitionTo(value);
                }
                else
                {
                    currentProfile = value;
                }
            }
        }

        public float ProfileTransitionLength
        {
            get => _profileTransitionLength;
            set => _profileTransitionLength = Mathf.Max(0, value);
        }

        public RagdollSettings RagdollSettings { get; private set; }
        public RagdollDefinitionBindings Bindings => _ragdollBindings;
        public RagdollTargetBindings TargetBindings => _targetBindings;
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public Animator TargetAnimator
        {
            get => targetAnimator;
            set => AssignTargetAnimationComponents(value, targetAnimation);
        }
        [RagdollCompatibilityApi("Lifecycle and animation", "https://docs.unity3d.com/ScriptReference/Animation.html")]
        public UnityEngine.Animation TargetAnimation
        {
            get => targetAnimation;
            set => AssignTargetAnimationComponents(targetAnimator, value);
        }
        /// <summary>
        /// True while this component owns evaluation of an Animator configured for
        /// fixed-time animation updates.
        /// </summary>
        public bool ControlsAnimator => ownsFixedAnimatorUpdate;
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public RagdollSimulationMode Mode
        {
            get
            {
                RagdollSimulationModeController controller =
                    GetComponent<RagdollSimulationModeController>();
                return controller && controller.IsInitialized
                    ? controller.TargetMode
                    : RagdollSimulationMode.Active;
            }
            set => GetSimulationModeController().SetMode(value);
        }
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public bool IsSwitchingMode
        {
            get
            {
                RagdollSimulationModeController controller =
                    GetComponent<RagdollSimulationModeController>();
                return controller && controller.IsInitialized
                    && controller.IsTransitioning;
            }
        }
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public bool IsActive
        {
            get
            {
                RagdollSimulationModeController controller =
                    GetComponent<RagdollSimulationModeController>();
                if (!controller || !controller.IsInitialized) return enabled;
                return controller.CurrentMode == RagdollSimulationMode.Active
                    || (controller.IsTransitioning
                        && (controller.CurrentMode == RagdollSimulationMode.Active
                            || controller.TargetMode == RagdollSimulationMode.Active));
            }
        }
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public bool IsBlending => IsSwitchingMode || IsSwitchingState;
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public bool Initiated => animatedPairs != null
            && Bindings && Bindings.IsInitialized;

        /// <summary>
        /// Gets the exact initialized target/ragdoll associations used by the
        /// animation matching pipeline. The returned collection is read-only;
        /// hierarchy rebuilds may replace its contents at a runtime boundary.
        /// </summary>
        public IReadOnlyList<AnimatedPair> AnimatedPairs =>
            animatedPairs ?? Array.Empty<AnimatedPair>();
        [RagdollCompatibilityApi("Lifecycle and animation", "https://docs.unity3d.com/ScriptReference/Animator-updateMode.html")]
        public AnimatorUpdateMode EffectiveUpdateMode
        {
            get
            {
                if (targetAnimator && !usesLegacyTargetAnimation)
                {
                    return targetAnimator.updateMode;
                }
#if UNITY_6000_0_OR_NEWER
                return targetAnimation && targetAnimation.animatePhysics
                    ? AnimatorUpdateMode.Fixed
                    : AnimatorUpdateMode.Normal;
#else
                return targetAnimation && targetAnimation.animatePhysics
                    ? AnimatorUpdateMode.AnimatePhysics
                    : AnimatorUpdateMode.Normal;
#endif
            }
        }

        /// <summary>
        /// True when this instance had to build a temporary name-based binding table for
        /// backwards compatibility. Migrate the component to explicit target bindings.
        /// </summary>
        public bool UsesLegacyTargetBindingFallback { get; private set; }

        [Obsolete("Use MasterPinWeight and MasterMuscleWeight independently.")]
        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public float MasterAlpha
        {
            get => _masterMuscleWeight;
            set
            {
                float weight = SanitizeUnit(value, 1f);
                _masterAlpha = weight;
                _masterPinWeight = weight;
                _masterMuscleWeight = weight;
            }
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public float MasterPinWeight
        {
            get => _masterPinWeight;
            set => _masterPinWeight = SanitizeUnit(value, 1f);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public float MasterMuscleWeight
        {
            get => _masterMuscleWeight;
            set => _masterMuscleWeight = SanitizeUnit(value, 1f);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public float MasterMuscleDamper
        {
            get => _masterMuscleDamper;
            set => _masterMuscleDamper = SanitizeNonNegative(value, 0f);
        }

        /// <summary>
        /// Hairibar compatibility multiplier for authored rotational damping.
        /// PuppetMaster's documented muscleDamper is the separate absolute channel.
        /// </summary>
        [RagdollCompatibilityApi("Master authority", "https://docs.unity3d.com/ScriptReference/ConfigurableJoint-angularXDrive.html")]
        public float MasterMuscleDamperMultiplier
        {
            get => _masterMuscleDamperMultiplier;
            set => _masterMuscleDamperMultiplier =
                SanitizeNonNegative(value, 1f);
        }

        [Obsolete("Use MasterMuscleDamperMultiplier. This compatibility property also affects positional damping.")]
        [RagdollCompatibilityApi("Master authority", "https://docs.unity3d.com/ScriptReference/ConfigurableJoint-angularXDrive.html")]
        public float MasterDampingRatio
        {
            get => _masterMuscleDamperMultiplier;
            set
            {
                float damper = SanitizeNonNegative(value, 1f);
                _masterDampingRatio = damper;
                _masterMuscleDamperMultiplier = damper;
            }
        }

        public bool FixTargetTransforms
        {
            get => fixTargetTransforms;
            set => fixTargetTransforms = value;
        }

        public bool HasPendingTeleport => teleportPending;

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeights(
            int muscleIndex,
            float muscleWeight,
            float pinWeight,
            float mappingWeight,
            float muscleDamper)
        {
            RequireMuscles().SetAuthorityWeights(
                GetHandleByIndex(muscleIndex),
                mappingWeight,
                pinWeight,
                muscleWeight,
                muscleDamper);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeights(
            Transform target,
            float muscleWeight,
            float pinWeight = 1f,
            float mappingWeight = 1f,
            float muscleDamper = 1f)
        {
            RequireMuscles().SetAuthorityWeights(
                GetHandleByTarget(target),
                mappingWeight,
                pinWeight,
                muscleWeight,
                muscleDamper);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeights(
            HumanBodyBones humanBodyBone,
            float muscleWeight,
            float pinWeight = 1f,
            float mappingWeight = 1f,
            float muscleDamper = 1f)
        {
            SetMuscleWeights(
                GetHumanoidTarget(humanBodyBone),
                muscleWeight,
                pinWeight,
                mappingWeight,
                muscleDamper);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeights(
            RagdollMuscleGroup group,
            float muscleWeight,
            float pinWeight = 1f,
            float mappingWeight = 1f,
            float muscleDamper = 1f)
        {
            RequireMuscles().SetAuthorityWeights(
                group,
                mappingWeight,
                pinWeight,
                muscleWeight,
                muscleDamper);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeightsRecursive(
            int muscleIndex,
            float muscleWeight,
            float pinWeight = 1f,
            float mappingWeight = 1f,
            float muscleDamper = 1f)
        {
            RequireMuscles().SetAuthorityWeightsRecursive(
                GetHandleByIndex(muscleIndex),
                mappingWeight,
                pinWeight,
                muscleWeight,
                muscleDamper);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeightsRecursive(
            Transform target,
            float muscleWeight,
            float pinWeight = 1f,
            float mappingWeight = 1f,
            float muscleDamper = 1f)
        {
            RequireMuscles().SetAuthorityWeightsRecursive(
                GetHandleByTarget(target),
                mappingWeight,
                pinWeight,
                muscleWeight,
                muscleDamper);
        }

        [RagdollCompatibilityApi("Master authority", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void SetMuscleWeightsRecursive(
            HumanBodyBones humanBodyBone,
            float muscleWeight,
            float pinWeight = 1f,
            float mappingWeight = 1f,
            float muscleDamper = 1f)
        {
            SetMuscleWeightsRecursive(
                GetHumanoidTarget(humanBodyBone),
                muscleWeight,
                pinWeight,
                mappingWeight,
                muscleDamper);
        }

        RagdollMuscleController RequireMuscles()
        {
            RagdollMuscleController muscles =
                GetComponent<RagdollMuscleController>();
            if (!muscles || !muscles.IsInitialized)
                throw new InvalidOperationException(
                    "Muscle weights require an initialized RagdollAnimator.");
            return muscles;
        }

        RagdollBoneHandle GetHandleByIndex(int muscleIndex)
        {
            if (!Initiated || muscleIndex < 0
                || muscleIndex >= Bindings.BoneCount)
                throw new ArgumentOutOfRangeException(nameof(muscleIndex));
            return Bindings.GetHandleAt(muscleIndex);
        }

        RagdollBoneHandle GetHandleByTarget(Transform target)
        {
            if (!target) throw new ArgumentNullException(nameof(target));
            if (!Initiated)
                throw new InvalidOperationException(
                    "Target lookup requires an initialized RagdollAnimator.");
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                if (animatedPairs[index].TargetBone == target)
                    return animatedPairs[index].Handle;
            }
            throw new ArgumentException(
                "No muscle uses the supplied Target Transform.",
                nameof(target));
        }

        Transform GetHumanoidTarget(HumanBodyBones humanBodyBone)
        {
            if (!Enum.IsDefined(typeof(HumanBodyBones), humanBodyBone)
                || humanBodyBone == HumanBodyBones.LastBone)
                throw new ArgumentOutOfRangeException(nameof(humanBodyBone));
            if (!targetAnimator || !targetAnimator.isHuman
                || !targetAnimator.avatar || !targetAnimator.avatar.isValid)
                throw new InvalidOperationException(
                    "HumanBodyBones lookup requires a valid Humanoid TargetAnimator.");
            Transform target = targetAnimator.GetBoneTransform(humanBodyBone);
            if (!target)
                throw new ArgumentException(
                    "The Humanoid avatar does not map the requested bone.",
                    nameof(humanBodyBone));
            return target;
        }

        public RagdollPinSettings PinSettings
        {
            get
            {
                RagdollPinSettings settings = pinSettings;
                settings.Normalize();
                return settings;
            }
            set
            {
                value.Normalize();
                pinSettings = value;
            }
        }

        public float PinPow
        {
            get => PinSettings.PinPow;
            set
            {
                RagdollPinSettings settings = pinSettings;
                settings.Normalize();
                settings.PinPow = value;
                pinSettings = settings;
            }
        }

        public float PinDistanceFalloff
        {
            get => PinSettings.PinDistanceFalloff;
            set
            {
                RagdollPinSettings settings = pinSettings;
                settings.Normalize();
                settings.PinDistanceFalloff = value;
                pinSettings = settings;
            }
        }

        public bool AngularPinning
        {
            get => PinSettings.AngularPinning;
            set
            {
                RagdollPinSettings settings = pinSettings;
                settings.Normalize();
                settings.AngularPinning = value;
                pinSettings = settings;
            }
        }

        public bool forceTargetPose = false;

        /// <summary>
        /// Supplies runtime setup references before initialization. Add this component to
        /// an inactive GameObject, configure it, then activate the hierarchy.
        /// </summary>
        public void ConfigureBeforeInitialization(
            RagdollDefinitionBindings ragdollBindings,
            RagdollTargetBindings targetBindings,
            RagdollAnimationProfile profile)
        {
            if (animatedPairs != null)
            {
                throw new InvalidOperationException(
                    "An initialized RagdollAnimator cannot be reconfigured.");
            }
            if (!ragdollBindings) throw new ArgumentNullException(nameof(ragdollBindings));
            if (!targetBindings) throw new ArgumentNullException(nameof(targetBindings));
            RagdollProfile.ValidateAsArgument(
                profile,
                ragdollBindings.Definition,
                true,
                "Runtime setup requires an AnimationProfile compatible with the Puppet definition.");
            _ragdollBindings = ragdollBindings;
            _targetBindings = targetBindings;
            currentProfile = profile;
            previousProfile = profile;
        }

        void AssignTargetAnimationComponents(
            Animator animator,
            UnityEngine.Animation animation)
        {
            if (animator == targetAnimator && animation == targetAnimation) return;
            if (manualSimulationPrepared)
            {
                throw new InvalidOperationException(
                    "Target animation components cannot change during a manual simulation step.");
            }

            Animator previousAnimator = targetAnimator;
            UnityEngine.Animation previousAnimation = targetAnimation;
            bool previousUsesLegacy = usesLegacyTargetAnimation;
            bool previousLifecycleEnabled = targetAnimatorLifecycleEnabled;

            ReleaseFixedAnimatorOwnership();
            try
            {
                if (animator && animation
                    && animator.enabled && animation.enabled)
                {
                    throw new InvalidOperationException(
                        "Animator and legacy Animation cannot control the Target simultaneously.");
                }

                targetAnimator = animator;
                targetAnimation = animation;
                usesLegacyTargetAnimation = animation && animation.enabled
                    && (!animator || !animator.enabled);
                targetAnimatorLifecycleEnabled = animator
                    ? animator.enabled
                    : true;
                ReconcileFixedAnimatorOwnership();
            }
            catch
            {
                targetAnimator = previousAnimator;
                targetAnimation = previousAnimation;
                usesLegacyTargetAnimation = previousUsesLegacy;
                targetAnimatorLifecycleEnabled = previousLifecycleEnabled;
                ReconcileFixedAnimatorOwnership();
                throw;
            }
        }
        #endregion

        #region Serialized Fields
        [SerializeField] RagdollDefinitionBindings _ragdollBindings;
        [SerializeField] RagdollTargetBindings _targetBindings;

        [SerializeField, UsePropertySetter("Profile")] RagdollAnimationProfile currentProfile;

        [SerializeField] float _masterAlpha = 1;
        [SerializeField] float _masterDampingRatio = 1;
        [SerializeField] float _masterPinWeight = 1f;
        [SerializeField] float _masterMuscleWeight = 1f;
        [SerializeField] float _masterMuscleDamper;
        [SerializeField] float _masterMuscleDamperMultiplier = 1f;
        [SerializeField, HideInInspector] int masterAuthoritySerializationVersion;
        [SerializeField] float _profileTransitionLength = 1;
        [SerializeField] RagdollPinSettings pinSettings = RagdollPinSettings.Default;
        [SerializeField] bool fixTargetTransforms = true;
        #endregion

        #region Private State
        ValueTransitioner profileTransitioner;
        RagdollAnimationProfile previousProfile;

        RagdollToTargetMapper mapper;
        AnimatedPair[] animatedPairs;
        Animator targetAnimator;
        UnityEngine.Animation targetAnimation;
        bool usesLegacyTargetAnimation;
        Transform authoredPuppetContainer;

        ITargetPoseModifier[] targetPoseModifiers;
        IBoneProfileModifier[] boneProfileModifiers;
        #endregion

        #region Unity Update Messages
        void Update()
        {
            ReconcileFixedAnimatorOwnership();
            if (!isActiveAndEnabled || animatedPairs is null
                || manualSimulationPrepared) return;
            FixTargetTransformsAtUpdateBoundary();
        }

        void FixedUpdate()
        {
            if (!isActiveAndEnabled || animatedPairs is null
                || manualSimulationPrepared) return;

            if (UsesFixedAnimatorUpdate()
                && LifecycleAllowsAnimationSampling())
            {
                EvaluateControlledAnimator(Time.fixedDeltaTime);
                ReadAnimatedPose();
            }
            else
            {
                ProcessPendingTeleportAtFixedBoundary();
            }
            ProcessPendingMuscleConnectionOperations();
            if (LifecycleIsFrozenStable()) return;

            RestoreAnimatedPose();
            ModifyTargetPose();
            UpdateJointRuntimeBeforeSimulation();
            ReapplyDisconnectedPhysicalPolicies();
            UpdateInternalCollisionsBeforeSimulation();
            DoAnimationMatching(Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled || animatedPairs is null
                || manualSimulationPrepared) return;

            if (!UsesFixedAnimatorUpdate()
                && LifecycleAllowsAnimationSampling())
            {
                ReadAnimatedPose();
            }
            else
            {
                ProcessPendingTeleportAtLateBoundary();
            }

            if (LifecycleIsFrozenStable())
            {
                UpdateLifecycle(Time.deltaTime);
                InvokePostLateUpdateHook();
                return;
            }

            if (!forceTargetPose)
            {
                MapRagdollToTarget();
            }

            UpdateLifecycle(Time.deltaTime);
            InvokePostLateUpdateHook();
        }
        #endregion

        #region Lifetime
        void Awake()
        {
            pinSettings.Normalize();
            jointRuntimeSettings.Normalize();
            internalCollisionSettings.Normalize();

            if (!_ragdollBindings)
            {
                throw new UnassignedReferenceException("A RagdollDefinitionBindings must be assigned in RagdollAnimator.");
            }

            RagdollProfile.ValidateAsInspectorField(currentProfile, Bindings.Definition, true, "A RagdollAnimationProfile must be assigned at RagdollAnimator.");

            RagdollSettings = _ragdollBindings.GetComponent<RagdollSettings>();
            // RagdollDefinitionBindings initializes in OnEnable. Unity does not
            // guarantee Awake order between components, so Root is not safe here.
            // Cache the authored container after every OnEnable has completed.
            targetAnimator = GetComponent<Animator>();
            targetAnimation = GetComponent<UnityEngine.Animation>();
            if (targetAnimator && targetAnimation
                && targetAnimator.enabled && targetAnimation.enabled)
            {
                throw new InvalidOperationException(
                    "Animator and legacy Animation cannot control the Target simultaneously.");
            }
            usesLegacyTargetAnimation = targetAnimation && targetAnimation.enabled
                && (!targetAnimator || !targetAnimator.enabled);

            InitializeProfileTransitioning();
        }

        void Start()
        {
            if (!_ragdollBindings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollDefinitionBindings did not initialize before RagdollAnimator.Start.");
            }
            authoredPuppetContainer = _ragdollBindings.Root.Transform.parent;
            CreateRagdollToTargetMapper();
            CreateAnimatedPairs(mapper.BonePairs);

            ForceAnimatorUpdate();
            ReadAnimatedPose(false, false);

            GatherBoneProfileModifiers();
            InitializeBoneProfileModifiers(boneProfileModifiers, animatedPairs);

            GatherTargetPoseModifiers();
            InitializeTargetPoseModifiers(targetPoseModifiers, animatedPairs);

            GatherMappingModifiers();
            InitializeLifecycle();
            InitializeInternalCollisions();
            InitializeJointRuntime();
            InitializeMuscleConnections();

            SnapToTargetPose();
            ReconcileFixedAnimatorOwnership();
            InvokePostInitializedHook();
        }

        void OnEnable()
        {
            if (LifecycleAllowsEnableSnap())
            {
                SnapToTargetPose();
            }
            RestoreLifecycleAfterEnable();
            ReconcileFixedAnimatorOwnership();
            RefreshJointRuntimeConfiguration();
            ApplyDisconnectedMasksToPhysicalOwners();
            ReapplyDisconnectedPhysicalPolicies();
            ReapplyInternalCollisionPolicy();

            RagdollBehaviourController behaviourController =
                GetComponent<RagdollBehaviourController>();
            if (behaviourController && behaviourController.IsInitialized)
            {
                behaviourController.ReactivateAfterAnimator();
            }
        }

        void OnApplicationQuit()
        {
            lifecycleApplicationQuitting = true;
        }

        void OnDestroy()
        {
            CancelPreparedManualSimulation();
            ReleaseFixedAnimatorOwnership();
            ShutdownLifecycle();
            ShutdownMuscleConnections();
            ShutdownInternalCollisions();
            ShutdownJointRuntime();
        }

        void OnDisable()
        {
            CancelPreparedManualSimulation();
            SettleLifecycleBeforeDisable();
            ReleaseFixedAnimatorOwnership();
            UnpowerAllJoints();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (masterAuthoritySerializationVersion < 1)
            {
                float legacyWeight = SanitizeUnit(_masterAlpha, 1f);
                float legacyDamper = SanitizeNonNegative(
                    _masterDampingRatio,
                    1f);
                _masterPinWeight = legacyWeight;
                _masterMuscleWeight = legacyWeight;
                _masterMuscleDamper = legacyDamper;
                masterAuthoritySerializationVersion = 1;
            }

            if (masterAuthoritySerializationVersion < 2)
            {
                _masterMuscleDamperMultiplier = SanitizeNonNegative(
                    _masterMuscleDamper,
                    1f);
                _masterMuscleDamper = 0f;
                masterAuthoritySerializationVersion = 2;
            }

            _masterPinWeight = SanitizeUnit(_masterPinWeight, 1f);
            _masterMappingWeight = SanitizeUnit(_masterMappingWeight, 1f);
            _masterMuscleWeight = SanitizeUnit(_masterMuscleWeight, 1f);
            _masterDampingRatio = SanitizeNonNegative(
                _masterDampingRatio,
                1f);
            _masterMuscleDamper = SanitizeNonNegative(
                _masterMuscleDamper,
                0f);
            _masterMuscleDamperMultiplier = SanitizeNonNegative(
                _masterMuscleDamperMultiplier,
                1f);
        }

        static float SanitizeUnit(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Clamp01(value);
        }

        static float SanitizeNonNegative(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Max(0f, value);
        }
        #endregion
    }
}
