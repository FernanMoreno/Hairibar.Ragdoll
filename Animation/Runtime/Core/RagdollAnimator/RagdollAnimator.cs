using Hairibar.NaughtyExtensions;
using UnityEngine;

#pragma warning disable 649
namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Matches a target rig's animation by applying appropiate forces to a ragdoll.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Animator"), DisallowMultipleComponent]
    public partial class RagdollAnimator : MonoBehaviour
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

        /// <summary>
        /// True when this instance had to build a temporary name-based binding table for
        /// backwards compatibility. Migrate the component to explicit target bindings.
        /// </summary>
        public bool UsesLegacyTargetBindingFallback { get; private set; }

        public float MasterAlpha
        {
            get => _masterAlpha;
            set => _masterAlpha = Mathf.Clamp01(value);
        }
        public float MasterDampingRatio
        {
            get => _masterDampingRatio;
            set => _masterDampingRatio = Mathf.Clamp01(value);
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
        #endregion

        #region Serialized Fields
        [SerializeField] RagdollDefinitionBindings _ragdollBindings;
        [SerializeField] RagdollTargetBindings _targetBindings;

        [SerializeField, UsePropertySetter("Profile")] RagdollAnimationProfile currentProfile;

        [SerializeField] float _masterAlpha = 1;
        [SerializeField] float _masterDampingRatio = 1;
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

        ITargetPoseModifier[] targetPoseModifiers;
        IBoneProfileModifier[] boneProfileModifiers;
        #endregion

        #region Unity Update Messages
        void Update()
        {
            if (!isActiveAndEnabled || animatedPairs is null) return;
            FixTargetTransformsAtUpdateBoundary();
        }

        void FixedUpdate()
        {
            if (!isActiveAndEnabled || animatedPairs is null) return;

            if (UsesFixedAnimatorUpdate()
                && LifecycleAllowsAnimationSampling())
            {
                ReadAnimatedPose();
            }
            else
            {
                ProcessPendingTeleportAtFixedBoundary();
            }

            if (LifecycleIsFrozenStable()) return;

            RestoreAnimatedPose();
            ModifyTargetPose();
            UpdateJointRuntimeBeforeSimulation();
            UpdateInternalCollisionsBeforeSimulation();
            DoAnimationMatching();
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled || animatedPairs is null) return;

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

            SnapToTargetPose();
        }

        void OnEnable()
        {
            if (LifecycleAllowsEnableSnap())
            {
                SnapToTargetPose();
            }
            RestoreLifecycleAfterEnable();
            RefreshJointRuntimeConfiguration();
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
            ShutdownLifecycle();
            ShutdownInternalCollisions();
            ShutdownJointRuntime();
        }

        void OnDisable()
        {
            SettleLifecycleBeforeDisable();
            UnpowerAllJoints();
        }
        #endregion
    }
}