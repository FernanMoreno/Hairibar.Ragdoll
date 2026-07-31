using Hairibar.NaughtyExtensions;
using System;
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
        public Animator TargetAnimator => targetAnimator;
        public UnityEngine.Animation TargetAnimation => targetAnimation;
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

        public float MasterPinWeight
        {
            get => _masterPinWeight;
            set => _masterPinWeight = SanitizeUnit(value, 1f);
        }

        public float MasterMuscleWeight
        {
            get => _masterMuscleWeight;
            set => _masterMuscleWeight = SanitizeUnit(value, 1f);
        }

        public float MasterMuscleDamper
        {
            get => _masterMuscleDamper;
            set => _masterMuscleDamper = SanitizeNonNegative(value, 1f);
        }

        [Obsolete("Use MasterMuscleDamper. This compatibility property also affects positional damping.")]
        public float MasterDampingRatio
        {
            get => _masterMuscleDamper;
            set
            {
                float damper = SanitizeNonNegative(value, 1f);
                _masterDampingRatio = damper;
                _masterMuscleDamper = damper;
            }
        }

        public bool FixTargetTransforms
        {
            get => fixTargetTransforms;
            set => fixTargetTransforms = value;
        }

        public bool HasPendingTeleport => teleportPending;

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
        #endregion

        #region Serialized Fields
        [SerializeField] RagdollDefinitionBindings _ragdollBindings;
        [SerializeField] RagdollTargetBindings _targetBindings;

        [SerializeField, UsePropertySetter("Profile")] RagdollAnimationProfile currentProfile;

        [SerializeField] float _masterAlpha = 1;
        [SerializeField] float _masterDampingRatio = 1;
        [SerializeField] float _masterPinWeight = 1f;
        [SerializeField] float _masterMuscleWeight = 1f;
        [SerializeField] float _masterMuscleDamper = 1f;
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

        ITargetPoseModifier[] targetPoseModifiers;
        IBoneProfileModifier[] boneProfileModifiers;
        #endregion

        #region Unity Update Messages
        void Update()
        {
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
            InvokePostInitializedHook();
        }

        void OnEnable()
        {
            if (LifecycleAllowsEnableSnap())
            {
                SnapToTargetPose();
            }
            RestoreLifecycleAfterEnable();
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
            ShutdownLifecycle();
            ShutdownMuscleConnections();
            ShutdownInternalCollisions();
            ShutdownJointRuntime();
        }

        void OnDisable()
        {
            CancelPreparedManualSimulation();
            SettleLifecycleBeforeDisable();
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

            _masterPinWeight = SanitizeUnit(_masterPinWeight, 1f);
            _masterMappingWeight = SanitizeUnit(_masterMappingWeight, 1f);
            _masterMuscleWeight = SanitizeUnit(_masterMuscleWeight, 1f);
            _masterDampingRatio = SanitizeNonNegative(
                _masterDampingRatio,
                1f);
            _masterMuscleDamper = SanitizeNonNegative(
                _masterMuscleDamper,
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
