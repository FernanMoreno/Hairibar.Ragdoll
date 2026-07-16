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

        public bool forceTargetPose = false;
        #endregion

        #region Serialized Fields
        [SerializeField] RagdollDefinitionBindings _ragdollBindings;
        [SerializeField] RagdollTargetBindings _targetBindings;

        [SerializeField, UsePropertySetter("Profile")] RagdollAnimationProfile currentProfile;

        [SerializeField] float _masterAlpha = 1;
        [SerializeField] float _masterDampingRatio = 1;
        [SerializeField] float _profileTransitionLength = 1;
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
        void FixedUpdate()
        {
            if (!isActiveAndEnabled || animatedPairs is null) return;

            if (UsesFixedAnimatorUpdate()
                && LifecycleAllowsAnimationSampling())
            {
                ReadAnimatedPose();
            }

            RestoreAnimatedPose();
            ModifyTargetPose();
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

            if (!forceTargetPose)
            {
                MapRagdollToTarget();
            }

            UpdateLifecycle(Time.deltaTime);
        }
        #endregion

        #region Lifetime
        void Awake()
        {
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
            ReadAnimatedPose();

            GatherBoneProfileModifiers();
            InitializeBoneProfileModifiers(boneProfileModifiers, animatedPairs);

            GatherTargetPoseModifiers();
            InitializeTargetPoseModifiers(targetPoseModifiers, animatedPairs);

            GatherMappingModifiers();
            InitializeLifecycle();

            SnapToTargetPose();
        }

        void OnEnable()
        {
            if (LifecycleAllowsEnableSnap())
            {
                SnapToTargetPose();
            }
            RestoreLifecycleAfterEnable();

            RagdollBehaviourController behaviourController =
                GetComponent<RagdollBehaviourController>();
            if (behaviourController && behaviourController.IsInitialized)
            {
                behaviourController.ReactivateAfterAnimator();
            }
        }

        void OnDisable()
        {
            SettleLifecycleBeforeDisable();
            UnpowerAllJoints();
        }
        #endregion
    }
}