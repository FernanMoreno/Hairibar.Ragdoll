using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Puppet/Unpinned/GetUp behaviour with ground sampling, center-of-mass sampling,
    /// prone/supine selection and one-shot Target root alignment before GetUp blending.
    /// </summary>
    [AddComponentMenu("Ragdoll/Behaviours/Ragdoll Puppet Behaviour")]
    [DisallowMultipleComponent]
    public sealed partial class RagdollPuppetBehaviour : RagdollBehaviourBase
    {
        [Header("Losing Balance")]
        [SerializeField] bool loseBalanceOnTargetDrift = true;
        [SerializeField, Range(0f, 1f)] float pinWeightThreshold = 1f;
        [SerializeField, Range(0f, 1f)] float unpinnedMuscleWeightMultiplier = 0.3f;
        [Tooltip("Limits Puppet Rigidbody velocity and sampled Target velocity when entering Unpinned, preventing stored pin forces from launching the ragdoll.")]
        [SerializeField, Min(0f)] float maxRigidbodyVelocity = 10f;
        [Tooltip("When disabled, muscles whose configured pin weight is zero cannot trigger a global loss of balance.")]
        [SerializeField] bool unpinnedMuscleKnockout = true;

        [Header("Recovery")]
        [Tooltip("Global multiplier for temporary pin-authority recovery. It composes with the RagdollMuscleController base rate and each semantic group's Regain Position Authority Multiplier.")]
        [SerializeField, Range(0.001f, 10f)] float regainPinSpeed = 1f;
        [Tooltip("How strongly rotational muscle authority follows effective pin authority in the normal Puppet state. Zero preserves authored muscle strength; one makes muscle strength follow pin authority completely.")]
        [SerializeField, Range(0f, 1f)] float muscleWeightRelativeToPinWeight;
        [Tooltip("How fast temporary immunity returns to zero and outgoing impulse multipliers return to one.")]
        [SerializeField, Min(0f)] float boostFalloff = 1f;

        [Header("Normal Mode")]
        [Tooltip("Active preserves authored simulation and mapping. Unmapped suppresses Puppet-to-Target mapping outside accepted contact. Kinematic delegates Rigidbody mode changes to RagdollSimulationModeController until an eligible accepted contact activates the Puppet.")]
        [SerializeField] RagdollPuppetNormalMode normalMode =
            RagdollPuppetNormalMode.Active;
        [Tooltip("Maximum mapping-weight change per second when entering or leaving Unmapped contact mapping. Zero pauses the blend.")]
        [SerializeField, Min(0f)] float mappingBlendSpeed = 10f;
        [Tooltip("In Kinematic normal mode, contacts without a dynamic Rigidbody may activate the Puppet. Kinematic Rigidbodies are included in this category.")]
        [SerializeField] bool activateOnStaticCollisions;
        [Tooltip("In Kinematic normal mode, contacts with a non-kinematic Rigidbody may activate the Puppet.")]
        [SerializeField] bool activateOnDynamicCollisions = true;
        [Tooltip("Minimum accepted collision impulse magnitude required to leave Kinematic normal mode.")]
        [SerializeField, Min(0f)] float activateOnImpulse = 0f;

        [Header("Collision Processing")]
        [Tooltip("Only collisions with GameObjects on these layers enter the Puppet behaviour pipeline.")]
        [SerializeField] LayerMask collisionLayers = -1;
        [Tooltip("Minimum squared collision impulse accepted by the behaviour. The squared value avoids a square root per callback.")]
        [SerializeField, Min(0f)] float collisionThreshold = 0f;
        [Tooltip("Maximum number of valid Enter/Stay collisions accepted during one physics timestamp. Layer and threshold rejections do not consume this budget.")]
        [SerializeField, Range(1, 30)] int maximumCollisionsPerFixedStep = 30;
        [Tooltip("Global collision resistance. A Target-speed curve can replace the constant value.")]
        [SerializeField] RagdollPuppetCollisionResistance collisionResistance =
            new RagdollPuppetCollisionResistance();
        [Tooltip("First matching entry can multiply resistance and override the squared impulse threshold for its layers.")]
        [SerializeField] RagdollPuppetCollisionLayerRule[]
            collisionResistanceMultipliers =
                new RagdollPuppetCollisionLayerRule[0];

        [Header("Grounding")]
        [SerializeField] LayerMask groundLayers = -1;
        [SerializeField, Min(0f)] float groundProbeStartOffset = 0.1f;
        [SerializeField, Min(0.001f)] float groundProbeDistance = 1f;
        [SerializeField, Range(0f, 89.9f)] float maximumGroundAngle = 60f;

        [Header("Getting Up")]
        [Tooltip(
            "Automatically enters GetUp after Get Up Delay once the hip "
            + "Rigidbody speed is below Max Get Up Velocity.")]
        [FormerlySerializedAs("automaticGetUp")]
        [SerializeField] bool canGetUp = true;
        [Tooltip("Minimum delay after losing balance before automatic GetUp can begin.")]
        [SerializeField, Min(0f)] float getUpDelay = 5f;
        [Tooltip("Duration of blending the Target from the ragdoll pose to the get-up animation.")]
        [SerializeField, Min(0f)] float blendToAnimationTime = 0.2f;
        [Tooltip("Automatic GetUp waits until the hip Rigidbody speed is below this value.")]
        [SerializeField, Min(0f)] float maximumGetUpVelocity = 0.3f;
        [Tooltip(
            "Minimum duration of the GetUp state before returning to Puppet. "
            + "Independent from Blend To Animation Time.")]
        [SerializeField, Min(0f)] float minimumGetUpDuration = 1f;
        [Tooltip("Collision resistance multiplier while in GetUp.")]
        [SerializeField, Min(0f)] float getUpCollisionResistanceMultiplier = 2f;
        [Tooltip("Regain Pin Speed multiplier while in GetUp.")]
        [SerializeField, Min(0f)] float getUpRegainPinSpeedMultiplier = 2f;
        [Tooltip("Knock Out Distance multiplier while in GetUp.")]
        [SerializeField, Min(0f)] float getUpKnockOutDistanceMultiplier = 10f;
        [Tooltip("If disabled, this behaviour will not move the Target root while unpinned or getting up. Intended for externally synchronized or network-owned Target roots.")]
        [SerializeField, HideInInspector] bool canMoveTarget = true;

        [Header("Get Up Orientation")]
        [Tooltip("Classify prone/supine from the grounded right/left side instead of the chest direction.")]
        [SerializeField] bool quadrupedGetUp;
        [Tooltip("Root-bone local axis pointing out of the character's chest/front.")]
        [SerializeField] Vector3 bodyFrontAxis = Vector3.forward;
        [Tooltip("Root-bone local axis corresponding to character up while standing.")]
        [SerializeField] Vector3 bodyUpAxis = Vector3.up;
        [SerializeField, Range(0f, 1f)] float minimumOrientationDot = 0.2f;
        [SerializeField] RagdollGetUpOrientation fallbackOrientation =
            RagdollGetUpOrientation.Supine;
        [Tooltip("Character-root-space offset from the physical hip when starting prone GetUp.")]
        [SerializeField] Vector3 getUpOffsetProne = Vector3.zero;
        [Tooltip("Character-root-space offset from the physical hip when starting supine GetUp.")]
        [SerializeField] Vector3 getUpOffsetSupine = Vector3.zero;

        [Header("Props")]
        [Tooltip("Whether a Prop-group muscle drifting from its target can knock out the whole puppet, the same way a body muscle does.")]
        [SerializeField] RagdollPropDriftPolicy propDriftPolicy =
            RagdollPropDriftPolicy.Ignore;

        [Header("Balancer (SubBehaviourBalancer port)")]
        [Tooltip("Root-bone-relative left calf/shin bone the reactive ankle torque is applied to. Distinct from the stagger foot bones.")]
        [SerializeField] BoneName balancerLeftCalfBone = "calf_l";
        [Tooltip("Root-bone-relative right calf/shin bone the reactive ankle torque is applied to. Distinct from the stagger foot bones.")]
        [SerializeField] BoneName balancerRightCalfBone = "calf_r";
        [Tooltip("Mirrors PuppetMaster's SubBehaviourBalancer.Settings. torqueMlp=0 by default -- opt-in, matching the official product.")]
        [SerializeField] RagdollBipedBalancerSettings balancerSettings =
            RagdollBipedBalancerSettings.Default;

        [Header("Stagger")]
        [Tooltip("When true, a sustained RequiresStep balance classification invokes OnRequiresStep instead of only relying on TargetDrift knockout.")]
        [SerializeField] bool canStagger = false;
        [Tooltip("Root-bone-relative left foot bone used for the capture-point support segment.")]
        [SerializeField] BoneName staggerLeftFootBone = "foot_l";
        [Tooltip("Root-bone-relative right foot bone used for the capture-point support segment.")]
        [SerializeField] BoneName staggerRightFootBone = "foot_r";
        [SerializeField, Min(0.01f)] float staggerPendulumLength = 0.9f;
        [SerializeField, Min(0f)] float staggerSupportRadius = 0.15f;
        [SerializeField, Min(0f)] float staggerStableMargin = 0.05f;
        [SerializeField, Min(0f)] float staggerRequiresStepMargin = 0.25f;
        [Tooltip("How long RequiresStep must be sustained before OnRequiresStep fires, avoiding single-frame noise.")]
        [SerializeField, Min(0f)] float minimumRequiresStepDuration = 0.1f;

        [Header("Events")]
        [SerializeField] RagdollPuppetEvent onGetUpProne;
        [SerializeField] RagdollPuppetEvent onGetUpSupine;
        [SerializeField] RagdollPuppetEvent onLoseBalance;
        [SerializeField] RagdollPuppetEvent onLoseBalanceFromPuppet;
        [SerializeField] RagdollPuppetEvent onLoseBalanceFromGetUp;
        [SerializeField] RagdollPuppetEvent onRegainBalance;
        [SerializeField] RagdollPuppetEvent onRequiresStep;

        RagdollPuppetStateMachine stateMachine;
        [SerializeField] RagdollCenterOfMassSubBehaviour centerOfMass =
            new RagdollCenterOfMassSubBehaviour();
        readonly RagdollBipedBalanceTrigger balanceTrigger = new RagdollBipedBalanceTrigger();
        RagdollPuppetCollisionProcessor collisionProcessor;
        RagdollPuppetUnmappedContactTracker unmappedContactTracker;
        // Kinematic mode needs contact memory filtered by its own activation policy.
        RagdollPuppetUnmappedContactTracker kinematicContactTracker;
        RagdollPuppetKinematicActivationQueue kinematicActivationQueue;
        RagdollSimulationModeController simulationModeController;
        RagdollPuppetColliderSurfaceController colliderSurfaceController;
        RagdollSimulationMode lastObservedSurfaceSimulationMode;
        bool hasObservedSurfaceSimulationMode;
        RagdollAnimator.AnimatedPair rootPair;
        RagdollAnimator.AnimatedPair getUpReferencePair;
        RagdollBoneHandle lastKnockOutBone = RagdollBoneHandle.Invalid;
        float lastKnockOutDistance;
        float lastKnockOutThreshold;
        float lastKnockOutEffectivePinWeight;
        RagdollGetUpOrientation getUpOrientation = RagdollGetUpOrientation.Unknown;
        Vector3 preparedGroundNormal = Vector3.up;
        bool targetAlignmentPending;
        bool getUpBlendCompletedByTeleport;
        bool lifecycleSuspended;
        long acceptedCollisionCount;
        long rejectedCollisionCount;
        RagdollBoneHandle lastAcceptedCollisionBone = RagdollBoneHandle.Invalid;
        float lastAcceptedCollisionImpulse;
        float lastAcceptedCollisionFixedTime;
        RagdollPuppetCollisionRejectionReason lastCollisionRejectionReason;
        RagdollPuppetCollisionResponseSnapshot lastCollisionResponse =
            RagdollPuppetCollisionResponseSnapshot.Empty;
        float normalModeMappingWeight = 1f;
        bool unmappedContactActive;
        bool kinematicActivationContactActive;
        bool kinematicModeManaged;
        RagdollPuppetKinematicActivationSource lastKinematicActivationSource =
            RagdollPuppetKinematicActivationSource.None;
        float lastKinematicActivationImpulse;
        float lastKinematicActivationFixedTime;
        long kinematicActivationCount;

        const float KinematicSuppressionEpsilon = 0.0001f;

        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html")]
        public RagdollPuppetState State
        {
            get => stateMachine != null
                ? stateMachine.State
                : RagdollPuppetState.Puppet;
            set => SetState(value);
        }

        public bool LoseBalanceOnTargetDrift
        {
            get => loseBalanceOnTargetDrift;
            set => loseBalanceOnTargetDrift = value;
        }
        public RagdollPropDriftPolicy PropDriftPolicy
        {
            get => propDriftPolicy;
            set => propDriftPolicy = value;
        }
        public RagdollBipedBalancerSettings BalancerSettings
        {
            get => balancerSettings;
            set => balancerSettings = value;
        }
        public float PinWeightThreshold
        {
            get => pinWeightThreshold;
            set => pinWeightThreshold = Mathf.Clamp01(SanitizeFinite(value, 0f));
        }
        public float UnpinnedMuscleWeightMultiplier
        {
            get => unpinnedMuscleWeightMultiplier;
            set => unpinnedMuscleWeightMultiplier =
                Mathf.Clamp01(SanitizeFinite(value, 0f));
        }

        public float StateElapsedTime => stateMachine != null
            ? stateMachine.StateElapsedTime
            : 0f;

        public float GetUpProgress => stateMachine != null
            ? RagdollPuppetBehaviourMath.ResolveGetUpBlendProgress(
                State,
                stateMachine.GetUpProgress(blendToAnimationTime),
                getUpBlendCompletedByTeleport)
            : 1f;

        public RagdollBoneHandle LastKnockOutBone => lastKnockOutBone;
        public float LastKnockOutDistance => lastKnockOutDistance;
        public float LastKnockOutThreshold => lastKnockOutThreshold;
        public float LastKnockOutEffectivePinWeight =>
            lastKnockOutEffectivePinWeight;
        public RagdollGetUpOrientation GetUpOrientation => getUpOrientation;
        public bool QuadrupedGetUp
        {
            get => quadrupedGetUp;
            set => quadrupedGetUp = value;
        }
        public bool CanGetUp
        {
            get => canGetUp;
            set => canGetUp = value;
        }
        public float GetUpDelay
        {
            get => getUpDelay;
            set => getUpDelay = SanitizeNonNegative(value);
        }
        public float BlendToAnimationTime
        {
            get => blendToAnimationTime;
            set => blendToAnimationTime = SanitizeNonNegative(value);
        }
        public float MaxGetUpVelocity
        {
            get => maximumGetUpVelocity;
            set => maximumGetUpVelocity = SanitizeNonNegative(value);
        }
        public float MinGetUpDuration
        {
            get => minimumGetUpDuration;
            set => minimumGetUpDuration = SanitizeNonNegative(value);
        }
        public float GetUpCollisionResistanceMlp
        {
            get => getUpCollisionResistanceMultiplier;
            set => getUpCollisionResistanceMultiplier =
                SanitizeNonNegative(value);
        }
        public float GetUpRegainPinSpeedMlp
        {
            get => getUpRegainPinSpeedMultiplier;
            set => getUpRegainPinSpeedMultiplier =
                SanitizeNonNegative(value);
        }
        public float GetUpKnockOutDistanceMlp
        {
            get => getUpKnockOutDistanceMultiplier;
            set => getUpKnockOutDistanceMultiplier =
                SanitizeNonNegative(value);
        }
        public bool CanMoveTarget
        {
            get => canMoveTarget;
            set => canMoveTarget = value;
        }
        public bool TargetAlignmentPending => targetAlignmentPending;
        public bool GetUpBlendCompletedByTeleport =>
            getUpBlendCompletedByTeleport;
        public float RegainPinSpeed
        {
            get => regainPinSpeed;
            set => regainPinSpeed = Mathf.Clamp(SanitizeFinite(value, 1f), 0.001f, 10f);
        }
        public float MuscleWeightRelativeToPinWeight
        {
            get => muscleWeightRelativeToPinWeight;
            set => muscleWeightRelativeToPinWeight = Mathf.Clamp01(SanitizeFinite(value, 0f));
        }
        public float BoostFalloff
        {
            get => boostFalloff;
            set => boostFalloff = RagdollMuscleBoostMath.SanitizeFalloff(value);
        }
        public bool HasActiveBoosts => IsInitialized
            && Context.Muscles.HasActiveBoosts;
        public float MaximumImmunity => IsInitialized
            ? Context.Muscles.MaximumImmunity
            : 0f;
        public float MaximumImpulseMultiplier => IsInitialized
            ? Context.Muscles.MaximumImpulseMultiplier
            : 1f;
        public float MaxRigidbodyVelocity
        {
            get => maxRigidbodyVelocity;
            set => maxRigidbodyVelocity = SanitizeMaximumVelocity(value);
        }
        public bool UnpinnedMuscleKnockout
        {
            get => unpinnedMuscleKnockout;
            set => unpinnedMuscleKnockout = value;
        }
        public float AppliedRegainPinSpeed => IsInitialized
            ? Context.Muscles.PositionSuppressionRecoveryMultiplier
            : 1f;
        public bool SurfaceBaselineCaptured => colliderSurfaceController != null
            && colliderSurfaceController.BaselineCaptured;
        public int SurfaceColliderCount => colliderSurfaceController != null
            ? colliderSurfaceController.ColliderCount
            : 0;
        public int SurfaceDisabledColliderCount => colliderSurfaceController != null
            ? colliderSurfaceController.DisabledColliderCount
            : 0;
        public int SurfaceMaterialOverrideCount => colliderSurfaceController != null
            ? colliderSurfaceController.MaterialOverrideCount
            : 0;
        public RagdollPuppetColliderSurfaceState SurfaceState =>
            colliderSurfaceController != null
                && colliderSurfaceController.HasAppliedState
                ? colliderSurfaceController.CurrentState
                : RagdollPuppetColliderSurfaceState.Puppet;
        public RagdollPuppetNormalMode NormalMode
        {
            get => normalMode;
            set => SetNormalMode(value, true);
        }
        public float MappingBlendSpeed
        {
            get => mappingBlendSpeed;
            set => mappingBlendSpeed = SanitizeNonNegative(value);
        }
        public float NormalModeMappingWeight => normalModeMappingWeight;
        public bool UnmappedContactActive => unmappedContactActive;
        public bool ActivateOnStaticCollisions
        {
            get => activateOnStaticCollisions;
            set => activateOnStaticCollisions = value;
        }
        public bool ActivateOnDynamicCollisions
        {
            get => activateOnDynamicCollisions;
            set => activateOnDynamicCollisions = value;
        }
        public float ActivateOnImpulse
        {
            get => activateOnImpulse;
            set => activateOnImpulse = SanitizeNonNegative(value);
        }
        public RagdollSimulationModeController SimulationModeController =>
            simulationModeController;
        public bool KinematicSimulationAvailable =>
            simulationModeController && simulationModeController.IsInitialized;
        public bool KinematicModeManaged => kinematicModeManaged;
        public bool KinematicActivationContactActive =>
            kinematicActivationContactActive;
        public bool KinematicActivationPending =>
            kinematicActivationQueue.HasRequest;
        public RagdollPuppetKinematicActivationSource
            LastKinematicActivationSource => lastKinematicActivationSource;
        public float LastKinematicActivationImpulse =>
            lastKinematicActivationImpulse;
        public float LastKinematicActivationFixedTime =>
            lastKinematicActivationFixedTime;
        public long KinematicActivationCount => kinematicActivationCount;
        public LayerMask CollisionLayers
        {
            get => collisionLayers;
            set => collisionLayers = value;
        }
        public float CollisionThreshold
        {
            get => collisionThreshold;
            set => collisionThreshold = SanitizeNonNegative(value);
        }
        public int MaximumCollisionsPerFixedStep
        {
            get => maximumCollisionsPerFixedStep;
            set => maximumCollisionsPerFixedStep = Mathf.Clamp(value, 1, 30);
        }
        public RagdollPuppetCollisionResistance CollisionResistance =>
            collisionResistance;
        public IReadOnlyList<RagdollPuppetCollisionLayerRule>
            CollisionResistanceMultipliers => collisionResistanceMultipliers;
        public void SetCollisionResistance(
            RagdollPuppetCollisionResistance value)
        {
            collisionResistance = value
                ?? new RagdollPuppetCollisionResistance();
            collisionResistance.Normalize();
        }

        public void SetCollisionResistanceMultipliers(
            RagdollPuppetCollisionLayerRule[] values)
        {
            collisionResistanceMultipliers = values
                ?? new RagdollPuppetCollisionLayerRule[0];
            NormalizeCollisionResponseConfiguration();
        }
        public LayerMask GroundLayers
        {
            get => groundLayers;
            set
            {
                groundLayers = value;
                ReconfigureCenterOfMass();
            }
        }
        public float GroundProbeStartOffset
        {
            get => groundProbeStartOffset;
            set
            {
                groundProbeStartOffset = SanitizeNonNegative(value);
                ReconfigureCenterOfMass();
            }
        }
        public float GroundProbeDistance
        {
            get => groundProbeDistance;
            set
            {
                groundProbeDistance = Mathf.Max(0.001f, SanitizeFinite(value, 0.001f));
                ReconfigureCenterOfMass();
            }
        }
        public float MaximumGroundAngle
        {
            get => maximumGroundAngle;
            set
            {
                maximumGroundAngle = Mathf.Clamp(SanitizeFinite(value, 0f), 0f, 89.9f);
                ReconfigureCenterOfMass();
            }
        }
        public Vector3 BodyFrontAxis
        {
            get => bodyFrontAxis;
            set => bodyFrontAxis = SanitizeAxis(value, Vector3.forward);
        }
        public Vector3 BodyUpAxis
        {
            get => bodyUpAxis;
            set => bodyUpAxis = SanitizeAxis(value, Vector3.up);
        }
        public float MinimumOrientationDot
        {
            get => minimumOrientationDot;
            set => minimumOrientationDot = Mathf.Clamp01(SanitizeFinite(value, 0f));
        }
        public RagdollGetUpOrientation FallbackOrientation
        {
            get => fallbackOrientation;
            set
            {
                if (value != RagdollGetUpOrientation.Prone
                    && value != RagdollGetUpOrientation.Supine)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                fallbackOrientation = value;
            }
        }
        public Vector3 GetUpOffsetProne
        {
            get => getUpOffsetProne;
            set => getUpOffsetProne = IsFinite(value) ? value : Vector3.zero;
        }
        public Vector3 GetUpOffsetSupine
        {
            get => getUpOffsetSupine;
            set => getUpOffsetSupine = IsFinite(value) ? value : Vector3.zero;
        }
        public long AcceptedCollisionCount => acceptedCollisionCount;
        public long RejectedCollisionCount => rejectedCollisionCount;
        public RagdollBoneHandle LastAcceptedCollisionBone => lastAcceptedCollisionBone;
        public float LastAcceptedCollisionImpulse => lastAcceptedCollisionImpulse;
        public float LastAcceptedCollisionFixedTime => lastAcceptedCollisionFixedTime;
        public RagdollPuppetCollisionRejectionReason LastCollisionRejectionReason =>
            lastCollisionRejectionReason;
        public RagdollPuppetCollisionStepSnapshot CollisionStep =>
            collisionProcessor.Snapshot;
        public RagdollPuppetCollisionResponseSnapshot LastCollisionResponse =>
            lastCollisionResponse;
        public int UpstreamMaximumEventsPerFixedStep => IsInitialized
            ? Context.CollisionHub.MaxEventsPerFixedStep
            : -1;
        public RagdollCenterOfMassSubBehaviour CenterOfMass => centerOfMass;
        public RagdollGroundingSnapshot Grounding => centerOfMass != null
            ? centerOfMass.Snapshot
            : RagdollGroundingSnapshot.Empty;

        public bool CanBeginGetUp
        {
            get
            {
                if (!IsInitialized || State != RagdollPuppetState.Unpinned)
                {
                    return false;
                }

                if (!RagdollPuppetBehaviourMath.IsGetUpReady(
                    StateElapsedTime,
                    getUpDelay,
                    getUpReferencePair.RagdollBone.Rigidbody.linearVelocity.magnitude,
                    maximumGetUpVelocity))
                {
                    return false;
                }

                RagdollGroundingSnapshot grounding = Grounding;
                Vector3 groundNormal = grounding.IsGrounded
                    ? grounding.GroundNormal
                    : GetWorldUp();
                return ResolveGetUpOrientation(groundNormal)
                    != RagdollGetUpOrientation.Unknown;
            }
        }

        readonly CachedSubscribers<Action<RagdollPuppetState,
            RagdollPuppetState, RagdollPuppetTransitionReason>>
            stateChangedSubscribers = new CachedSubscribers<Action<
                RagdollPuppetState, RagdollPuppetState,
                RagdollPuppetTransitionReason>>();
        readonly CachedSubscribers<Action<RagdollGetUpOrientation>>
            getUpPoseSubscribers =
                new CachedSubscribers<Action<RagdollGetUpOrientation>>();
        readonly CachedSubscribers<Action<RagdollCollisionEvent>>
            collisionAcceptedSubscribers =
                new CachedSubscribers<Action<RagdollCollisionEvent>>();
        readonly CachedSubscribers<Action<RagdollCollisionEvent>>
            collisionObservedSubscribers =
                new CachedSubscribers<Action<RagdollCollisionEvent>>();
        readonly CachedSubscribers<Action<RagdollCollisionEvent, float>>
            collisionUnpinSubscribers =
                new CachedSubscribers<Action<RagdollCollisionEvent, float>>();
        readonly CachedSubscribers<Action<RagdollPuppetKinematicActivationSource, float>>
            kinematicActivatedSubscribers = new CachedSubscribers<
                Action<RagdollPuppetKinematicActivationSource, float>>();

        public event Action<RagdollPuppetState, RagdollPuppetState,
            RagdollPuppetTransitionReason> StateChanged
        {
            add => stateChangedSubscribers.Add(value);
            remove => stateChangedSubscribers.Remove(value);
        }
        public event Action<RagdollGetUpOrientation> GetUpPoseSelected
        {
            add => getUpPoseSubscribers.Add(value);
            remove => getUpPoseSubscribers.Remove(value);
        }

        public RagdollPuppetEvent OnGetUpProne
        {
            get => onGetUpProne;
            set => onGetUpProne = value;
        }
        public RagdollPuppetEvent OnGetUpSupine
        {
            get => onGetUpSupine;
            set => onGetUpSupine = value;
        }
        public RagdollPuppetEvent OnLoseBalance
        {
            get => onLoseBalance;
            set => onLoseBalance = value;
        }
        public RagdollPuppetEvent OnLoseBalanceFromPuppet
        {
            get => onLoseBalanceFromPuppet;
            set => onLoseBalanceFromPuppet = value;
        }
        public RagdollPuppetEvent OnLoseBalanceFromGetUp
        {
            get => onLoseBalanceFromGetUp;
            set => onLoseBalanceFromGetUp = value;
        }
        public RagdollPuppetEvent OnRegainBalance
        {
            get => onRegainBalance;
            set => onRegainBalance = value;
        }
        public RagdollPuppetEvent OnRequiresStep
        {
            get => onRequiresStep;
            set => onRequiresStep = value;
        }
        public bool CanStagger
        {
            get => canStagger;
            set => canStagger = value;
        }
        public float MinimumRequiresStepDuration
        {
            get => minimumRequiresStepDuration;
            set => minimumRequiresStepDuration = SanitizeNonNegative(value);
        }

        /// <summary>
        /// Raised immediately for each Enter/Stay collision accepted by the layer,
        /// squared-impulse threshold and per-step budget policy. The embedded Collision
        /// reference is callback-scoped and must not be retained.
        /// </summary>
        public event Action<RagdollCollisionEvent> CollisionAccepted
        {
            add => collisionAcceptedSubscribers.Add(value);
            remove => collisionAcceptedSubscribers.Remove(value);
        }

        /// <summary>
        /// Raised once for every muscular Enter, Stay or Exit event delivered by the
        /// collision hub, before lifecycle, layer, threshold and budget filters.
        /// </summary>
        public event Action<RagdollCollisionEvent> CollisionObserved
        {
            add => collisionObservedSubscribers.Add(value);
            remove => collisionObservedSubscribers.Remove(value);
        }

        /// <summary>
        /// Raised after an accepted collision has applied position-authority suppression.
        /// The float is the resolved source suppression in the range 0..1.
        /// </summary>
        public event Action<RagdollCollisionEvent, float> CollisionUnpinApplied
        {
            add => collisionUnpinSubscribers.Add(value);
            remove => collisionUnpinSubscribers.Remove(value);
        }

        /// <summary>Official BehaviourPuppet collision callback name.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html")]
        public event Action<RagdollCollisionEvent> OnCollision
        {
            add => collisionObservedSubscribers.Add(value);
            remove => collisionObservedSubscribers.Remove(value);
        }

        /// <summary>Official callback name for collisions that reduced pinning.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html")]
        public event Action<RagdollCollisionEvent, float> OnCollisionImpulse
        {
            add => collisionUnpinSubscribers.Add(value);
            remove => collisionUnpinSubscribers.Remove(value);
        }

        /// <summary>
        /// Raised after a queued Kinematic contact has safely switched the global
        /// simulation controller back to Active.
        /// </summary>
        public event Action<RagdollPuppetKinematicActivationSource, float>
            KinematicActivated
        {
            add => kinematicActivatedSubscribers.Add(value);
            remove => kinematicActivatedSubscribers.Remove(value);
        }

        /// <summary>
        /// Sets immunity and outgoing impulse multiplier for every muscle. Both values
        /// return automatically to their separate neutral values.
        /// </summary>
        public void Boost(float immunity, float impulseMlp)
        {
            RequireMuscleController().Boost(immunity, impulseMlp);
        }

        /// <summary>Sets both boost channels for one muscle.</summary>
        public void Boost(
            RagdollBoneHandle bone,
            float immunity,
            float impulseMlp)
        {
            RequireMuscleController().Boost(bone, immunity, impulseMlp);
        }

        /// <summary>Official BehaviourPuppet overload using the muscle registry index.</summary>
        public void Boost(int muscleIndex, float immunity, float impulseMlp)
        {
            Boost(ResolveMuscleIndex(muscleIndex), immunity, impulseMlp);
        }

        /// <summary>
        /// Sets both channels for one muscle and its ancestors/descendants using independent
        /// per-edge parent and child falloffs. Unrelated branches are not modified.
        /// </summary>
        public void Boost(
            RagdollBoneHandle bone,
            float immunity,
            float impulseMlp,
            float boostParents,
            float boostChildren)
        {
            RequireMuscleController().Boost(
                bone,
                immunity,
                impulseMlp,
                boostParents,
                boostChildren);
        }

        /// <summary>Official recursive boost overload using the muscle registry index.</summary>
        public void Boost(
            int muscleIndex,
            float immunity,
            float impulseMlp,
            float boostParents,
            float boostChildren)
        {
            Boost(
                ResolveMuscleIndex(muscleIndex),
                immunity,
                impulseMlp,
                boostParents,
                boostChildren);
        }

        /// <summary>
        /// Sets both channels for a semantic group and returns the number of matching muscles.
        /// Returns zero when no muscle profile supplies semantic group assignments.
        /// </summary>
        public int Boost(
            RagdollMuscleGroup group,
            float immunity,
            float impulseMlp)
        {
            return RequireMuscleController().Boost(
                group,
                immunity,
                impulseMlp);
        }

        /// <summary>Sets incoming-damage immunity for every muscle.</summary>
        public void BoostImmunity(float immunity)
        {
            RequireMuscleController().BoostImmunity(immunity);
        }

        /// <summary>Sets incoming-damage immunity for one muscle.</summary>
        public void BoostImmunity(
            RagdollBoneHandle bone,
            float immunity)
        {
            RequireMuscleController().BoostImmunity(bone, immunity);
        }

        /// <summary>Official immunity overload using the muscle registry index.</summary>
        public void BoostImmunity(int muscleIndex, float immunity)
        {
            BoostImmunity(ResolveMuscleIndex(muscleIndex), immunity);
        }

        /// <summary>
        /// Sets immunity for one muscle and its ancestor/descendant chains.
        /// </summary>
        public void BoostImmunity(
            RagdollBoneHandle bone,
            float immunity,
            float boostParents,
            float boostChildren)
        {
            RequireMuscleController().BoostImmunity(
                bone,
                immunity,
                boostParents,
                boostChildren);
        }

        /// <summary>Official recursive immunity overload using the muscle registry index.</summary>
        public void BoostImmunity(
            int muscleIndex,
            float immunity,
            float boostParents,
            float boostChildren)
        {
            BoostImmunity(
                ResolveMuscleIndex(muscleIndex),
                immunity,
                boostParents,
                boostChildren);
        }

        /// <summary>Sets immunity for a semantic group and returns its match count.</summary>
        public int BoostImmunity(
            RagdollMuscleGroup group,
            float immunity)
        {
            return RequireMuscleController().BoostImmunity(group, immunity);
        }

        /// <summary>Sets outgoing cross-puppet impact damage for every muscle.</summary>
        public void BoostImpulseMlp(float impulseMlp)
        {
            RequireMuscleController().BoostImpulseMlp(impulseMlp);
        }

        /// <summary>Sets outgoing cross-puppet impact damage for one muscle.</summary>
        public void BoostImpulseMlp(
            RagdollBoneHandle bone,
            float impulseMlp)
        {
            RequireMuscleController().BoostImpulseMlp(bone, impulseMlp);
        }

        /// <summary>Official impulse overload using the muscle registry index.</summary>
        public void BoostImpulseMlp(int muscleIndex, float impulseMlp)
        {
            BoostImpulseMlp(ResolveMuscleIndex(muscleIndex), impulseMlp);
        }

        /// <summary>
        /// Sets outgoing damage for one muscle and its ancestor/descendant chains.
        /// </summary>
        public void BoostImpulseMlp(
            RagdollBoneHandle bone,
            float impulseMlp,
            float boostParents,
            float boostChildren)
        {
            RequireMuscleController().BoostImpulseMlp(
                bone,
                impulseMlp,
                boostParents,
                boostChildren);
        }

        /// <summary>Official recursive impulse overload using the muscle registry index.</summary>
        public void BoostImpulseMlp(
            int muscleIndex,
            float impulseMlp,
            float boostParents,
            float boostChildren)
        {
            BoostImpulseMlp(
                ResolveMuscleIndex(muscleIndex),
                impulseMlp,
                boostParents,
                boostChildren);
        }

        /// <summary>
        /// Sets outgoing damage for a semantic group and returns its match count.
        /// </summary>
        public int BoostImpulseMlp(
            RagdollMuscleGroup group,
            float impulseMlp)
        {
            return RequireMuscleController().BoostImpulseMlp(
                group,
                impulseMlp);
        }

        public void SetNormalMode(
            RagdollPuppetNormalMode mode,
            bool immediate = false)
        {
            ValidateNormalMode(mode);
            if (mode == RagdollPuppetNormalMode.Kinematic && IsInitialized)
            {
                EnsureKinematicSimulationController();
            }

            RagdollPuppetNormalMode previous = normalMode;
            normalMode = mode;
            kinematicActivationQueue.Reset();

            if (previous == RagdollPuppetNormalMode.Kinematic
                && mode != RagdollPuppetNormalMode.Kinematic)
            {
                ReleaseKinematicSimulationMode(immediate);
            }

            if (!immediate || !IsInitialized) return;

            normalModeMappingWeight =
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    normalMode,
                    State,
                    unmappedContactActive);

            if (mode == RagdollPuppetNormalMode.Kinematic)
            {
                TryEnterKinematicSimulationMode(true);
            }
        }

        public bool LoseBalance()
        {
            return LoseBalance(
                RagdollBoneHandle.Invalid,
                State == RagdollPuppetState.GetUp
                    ? RagdollPuppetTransitionReason.GetUpInterrupted
                    : RagdollPuppetTransitionReason.Manual);
        }

        /// <summary>
        /// Starts GetUp after delay, hip-velocity and orientation checks pass.
        /// </summary>
        public bool TryBeginGetUp()
        {
            if (!CanBeginGetUp) return false;

            RagdollGroundingSnapshot grounding = Grounding;
            Vector3 groundNormal = grounding.IsGrounded
                ? grounding.GroundNormal
                : GetWorldUp();
            return BeginGetUp(
                ResolveGetUpOrientation(groundNormal),
                groundNormal);
        }

        /// <summary>
        /// Bypasses timing and ground checks while still selecting a deterministic pose.
        /// </summary>
        public bool BeginGetUpImmediately()
        {
            if (!IsInitialized || State != RagdollPuppetState.Unpinned)
            {
                return false;
            }

            Vector3 groundNormal = Grounding.IsGrounded
                ? Grounding.GroundNormal
                : GetWorldUp();
            return BeginGetUp(
                ResolveGetUpOrientation(groundNormal),
                groundNormal);
        }

        /// <summary>
        /// Bypasses readiness checks and uses an explicitly selected prone/supine pose.
        /// </summary>
        public bool BeginGetUpImmediately(RagdollGetUpOrientation orientation)
        {
            Vector3 groundNormal = Grounding.IsGrounded
                ? Grounding.GroundNormal
                : GetWorldUp();
            return BeginGetUp(orientation, groundNormal);
        }

        public bool InterruptGetUp()
        {
            if (State != RagdollPuppetState.GetUp) return false;

            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            return TransitionTo(
                RagdollPuppetState.Unpinned,
                RagdollPuppetTransitionReason.GetUpInterrupted);
        }

        protected override void OnBehaviourInitialize()
        {
            ValidateNormalMode(normalMode);
            NormalizeCollisionResponseConfiguration();
            simulationModeController =
                Context.Animator.GetComponent<RagdollSimulationModeController>();
            if (normalMode == RagdollPuppetNormalMode.Kinematic)
            {
                EnsureKinematicSimulationController();
            }

            colliderSurfaceController =
                new RagdollPuppetColliderSurfaceController(
                    Context.Bindings,
                    Context.Muscles);
            hasObservedSurfaceSimulationMode = false;
            stateMachine = new RagdollPuppetStateMachine();
            InitializeCenterOfMassSubBehaviour();
            collisionProcessor.Reset();
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            normalModeMappingWeight =
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    normalMode,
                    RagdollPuppetState.Puppet,
                    false);
            unmappedContactActive = false;
            kinematicActivationContactActive = false;
            kinematicModeManaged = false;
            rootPair = FindRootPair();
            getUpReferencePair = FindGetUpReferencePair();
            lifecycleSuspended = !Context.Animator.IsAlive;
        }

        /// <summary>
        /// Selects the documented Puppet/Unpinned/GetUp state directly. Unlike
        /// automatic GetUp, explicit assignment intentionally bypasses readiness gates.
        /// This is Hairibar's implementation of BehaviourPuppet.state [get,set].
        /// </summary>
        public bool SetState(RagdollPuppetState value)
        {
            if (!Enum.IsDefined(typeof(RagdollPuppetState), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (!IsInitialized || stateMachine == null)
                throw new InvalidOperationException(
                    "State assignment requires an initialized RagdollPuppetBehaviour.");
            if (value == State) return false;

            if (value != RagdollPuppetState.Puppet)
                EnsureActiveForNonPuppetState();

            if (!TransitionTo(
                value,
                RagdollPuppetTransitionReason.Manual,
                true))
            {
                return false;
            }

            if (value == RagdollPuppetState.Unpinned)
            {
                targetAlignmentPending = false;
                getUpBlendCompletedByTeleport = false;
                getUpOrientation = RagdollGetUpOrientation.Unknown;
                lastKnockOutBone = RagdollBoneHandle.Invalid;
            }
            else if (value == RagdollPuppetState.GetUp)
            {
                Vector3 groundNormal = Grounding.IsGrounded
                    ? Grounding.GroundNormal
                    : GetWorldUp();
                RagdollGetUpOrientation orientation =
                    ResolveGetUpOrientation(groundNormal);
                PrepareExplicitGetUp(orientation, groundNormal);
            }
            else
            {
                targetAlignmentPending = false;
                getUpOrientation = RagdollGetUpOrientation.Unknown;
            }
            return true;
        }

        /// <summary>Official BehaviourPuppet name for immediately losing balance.</summary>
        public void Unpin()
        {
            LoseBalance();
        }

        /// <summary>
        /// Returns whether the currently resolved recovery pose is prone. Ambiguous
        /// orientation uses the configured fallback, matching the recovery selector.
        /// </summary>
        public bool IsProne()
        {
            if (!IsInitialized) return false;
            Vector3 groundNormal = Grounding.IsGrounded
                ? Grounding.GroundNormal
                : GetWorldUp();
            return ResolveGetUpOrientation(groundNormal)
                == RagdollGetUpOrientation.Prone;
        }

        /// <summary>
        /// Repositions Target and Puppet, clears physical momentum and all temporary
        /// BehaviourPuppet state, then restores the live Puppet state. Restoration is
        /// best-effort; all failures are reported together after every cleanup ran.
        /// </summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html")]
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Respawn requires an initialized RagdollPuppetBehaviour.");
            }
            if (!IsFinite(position) || !IsFinite(rotation)
                || QuaternionLengthSquared(rotation) <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    "Respawn pose must contain finite values and a non-zero rotation.");
            }

            List<Exception> failures = new List<Exception>();
            RagdollPuppetState previousState = State;
            TryRespawnStep(failures, () => CancelPropActionsForRespawn());
            TryRespawnStep(failures, () => Context.Animator.ResurrectImmediately());
            TryRespawnStep(failures, () => Context.Animator.TeleportImmediate(
                position,
                Quaternion.Normalize(rotation),
                true));
            TryRespawnStep(failures, ClearPuppetMomentum);
            TryRespawnStep(failures, () => Context.Muscles.ResetAll());
            TryRespawnStep(failures, ResetRespawnState);
            TryRespawnStep(failures, () =>
            {
                if (simulationModeController && simulationModeController.IsInitialized)
                {
                    simulationModeController.ForceActiveForLifecycle();
                }
            });
            TryRespawnStep(failures, () =>
            {
                lifecycleSuspended = false;
                ApplyRecoveryConfiguration();
                SetColliderSurfaceState(false);
                Context.Animator.ReapplyInternalCollisionPolicy();
            });

            if (previousState != RagdollPuppetState.Puppet)
            {
                InvokeStateChangedSafely(
                    previousState,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetTransitionReason.Respawn);
            }
            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Respawn completed with one or more restoration failures.",
                    failures);
            }
        }

        /// <summary>Official BehaviourPuppet name for transactional respawn.</summary>
#pragma warning disable UNT0006
        public void Reset(Vector3 position, Quaternion rotation)
        {
            Respawn(position, rotation);
        }
#pragma warning restore UNT0006

        void ResetRespawnState()
        {
            stateMachine.Reset(RagdollPuppetState.Puppet);
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            centerOfMass?.Reset();
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            unmappedContactActive = false;
            kinematicActivationContactActive = false;
            normalModeMappingWeight = 1f;
            ResetCollisionStatistics();
        }

        void ClearPuppetMomentum()
        {
            for (int index = 0; index < Context.Bindings.BoneCount; index++)
            {
                Rigidbody body = Context.Bindings.GetBoneAt(index).Rigidbody;
                // Unity rejects velocity assignments on kinematic bodies. A later
                // Kinematic -> Active transition clears both velocities after the
                // simulation controller restores the body to dynamic operation.
                if (!body || body.isKinematic) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        static void TryRespawnStep(List<Exception> failures, Action action)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }

        /// <summary>
        /// Applies the authored Puppet or Unpinned collider/material policy immediately
        /// without changing the behaviour state. The activation baseline remains intact.
        /// </summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html")]
        public bool SetColliderSurfaceState(bool unpinned)
        {
            if (!IsInitialized || colliderSurfaceController == null) return false;
            if (!colliderSurfaceController.BaselineCaptured)
            {
                colliderSurfaceController.CaptureBaseline();
            }
            return colliderSurfaceController.Apply(
                unpinned ? RagdollPuppetState.Unpinned : RagdollPuppetState.Puppet,
                true);
        }

        /// <summary>Official BehaviourPuppet name for its collider surface policy.</summary>
        public void SetColliders(bool unpinned)
        {
            SetColliderSurfaceState(unpinned);
        }

        /// <summary>Clears accepted/rejected collision telemetry and step state.</summary>
        public void ResetCollisionStatistics()
        {
            acceptedCollisionCount = 0;
            rejectedCollisionCount = 0;
            lastAcceptedCollisionBone = RagdollBoneHandle.Invalid;
            lastAcceptedCollisionImpulse = 0f;
            lastAcceptedCollisionFixedTime = 0f;
            lastCollisionRejectionReason = RagdollPuppetCollisionRejectionReason.None;
            lastCollisionResponse = RagdollPuppetCollisionResponseSnapshot.Empty;
            collisionProcessor.Reset();
        }

        protected override void OnBehaviourShutdown()
        {
            if (centerOfMass != null) centerOfMass.Shutdown();
        }

        // Direct component/GameObject toggles do not pass through
        // RagdollBehaviourController.SetActiveInternal. Surface overrides are
        // temporary ownership and must therefore follow Unity's enable lifecycle.
        void OnDisable()
        {
            if (colliderSurfaceController != null)
            {
                colliderSurfaceController.Restore();
            }
            hasObservedSurfaceSimulationMode = false;
        }

        void OnEnable()
        {
            if (!IsInitialized
                || !IsActive
                || colliderSurfaceController == null)
            {
                return;
            }

            if (!colliderSurfaceController.BaselineCaptured)
            {
                colliderSurfaceController.CaptureBaseline();
            }
            ObserveSurfaceSimulationMode();
            ApplySurfaceConfiguration(true);
        }

        void InitializeCenterOfMassSubBehaviour()
        {
            if (centerOfMass == null)
            {
                centerOfMass = new RagdollCenterOfMassSubBehaviour();
            }
            centerOfMass.Configure(
                groundLayers,
                groundProbeStartOffset,
                groundProbeDistance,
                maximumGroundAngle);
            centerOfMass.Initialize(this);
        }

        void ReinitializeCenterOfMassSubBehaviour()
        {
            bool active = IsActive;
            if (centerOfMass != null) centerOfMass.Shutdown();
            InitializeCenterOfMassSubBehaviour();
            if (active) centerOfMass.SetActive(true);
        }

        void ReconfigureCenterOfMass()
        {
            if (!IsInitialized || centerOfMass == null) return;
            centerOfMass.Configure(
                groundLayers,
                groundProbeStartOffset,
                groundProbeDistance,
                maximumGroundAngle);
        }

        protected override void OnBehaviourHierarchyChanged(
            IReadOnlyList<RagdollMuscleChange> added,
            IReadOnlyList<RagdollMuscleChange> removed)
        {
            if (colliderSurfaceController != null)
            {
                colliderSurfaceController.Restore();
            }

            colliderSurfaceController =
                new RagdollPuppetColliderSurfaceController(
                    Context.Bindings,
                    Context.Muscles);
            ReinitializeCenterOfMassSubBehaviour();
            rootPair = FindRootPair();
            getUpReferencePair = FindGetUpReferencePair();
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            collisionProcessor.Reset();
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            hasObservedSurfaceSimulationMode = false;

            if (IsActive)
            {
                colliderSurfaceController.CaptureBaseline();
                ObserveSurfaceSimulationMode();
                ApplySurfaceConfiguration(true);
                Context.Animator.ReapplyInternalCollisionPolicy();
            }
        }

        protected override void OnBehaviourMuscleDisconnected(
            RagdollMuscleConnectionChange change)
        {
            ResetAfterMuscleConnectionChange();
        }

        protected override void OnBehaviourMuscleReconnected(
            RagdollMuscleConnectionChange change)
        {
            ResetAfterMuscleConnectionChange();
        }

        void ResetAfterMuscleConnectionChange()
        {
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            if (centerOfMass != null) centerOfMass.Reset();
            collisionProcessor.Reset();
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            unmappedContactActive = false;
            kinematicActivationContactActive = false;
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;

            if (!IsActive) return;
            ApplySurfaceConfiguration(true);
            Context.Animator.ReapplyInternalCollisionPolicy();
        }

        protected override void OnBehaviourActivated()
        {
            lifecycleSuspended = !Context.Animator.IsAlive
                || Context.Animator.IsKilling;
            stateMachine.Reset(
                lifecycleSuspended
                    ? RagdollPuppetState.Unpinned
                    : RagdollPuppetState.Puppet);
            ApplyPropDropPolicyForCurrentState();
            centerOfMass.SetActive(true);
            centerOfMass.Reset();
            balanceTrigger.Reset();
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            unmappedContactActive = false;
            kinematicActivationContactActive = false;
            kinematicModeManaged = false;
            normalModeMappingWeight =
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    normalMode,
                    RagdollPuppetState.Puppet,
                    false);
            lastKinematicActivationSource =
                RagdollPuppetKinematicActivationSource.None;
            lastKinematicActivationImpulse = 0f;
            lastKinematicActivationFixedTime = 0f;
            kinematicActivationCount = 0L;
            Context.Muscles.SetCombatBoostsEnabled(!lifecycleSuspended);
            ApplyRecoveryConfiguration();
            hasObservedSurfaceSimulationMode = false;
            colliderSurfaceController.CaptureBaseline();
            ObserveSurfaceSimulationMode();
            ApplySurfaceConfiguration(true);
            ResetCollisionProcessing(true);
        }

        protected override void OnBehaviourReactivated()
        {
            lifecycleSuspended = !Context.Animator.IsAlive
                || Context.Animator.IsKilling;
            // Disabling the Animator is also required by Unity's manual simulation
            // workflow. Reactivation must therefore preserve an in-progress
            // Unpinned/GetUp state instead of treating enable as a respawn. Lifecycle
            // suspension remains authoritative and may only reactivate as Unpinned.
            if (lifecycleSuspended)
            {
                stateMachine.Reset(RagdollPuppetState.Unpinned);
                getUpOrientation = RagdollGetUpOrientation.Unknown;
                targetAlignmentPending = false;
                getUpBlendCompletedByTeleport = false;
            }
            ApplyPropDropPolicyForCurrentState();
            centerOfMass.SetActive(true);
            centerOfMass.Reset();
            balanceTrigger.Reset();
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            preparedGroundNormal = GetWorldUp();
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            unmappedContactActive = false;
            kinematicActivationContactActive = false;
            kinematicModeManaged = false;
            normalModeMappingWeight =
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    normalMode,
                    State,
                    false);
            lastKinematicActivationSource =
                RagdollPuppetKinematicActivationSource.None;
            lastKinematicActivationImpulse = 0f;
            lastKinematicActivationFixedTime = 0f;
            kinematicActivationCount = 0L;

            if (!IsActive) return;

            Context.Muscles.SetCombatBoostsEnabled(!lifecycleSuspended);
            ApplyRecoveryConfiguration();
            hasObservedSurfaceSimulationMode = false;
            if (!colliderSurfaceController.BaselineCaptured)
            {
                colliderSurfaceController.CaptureBaseline();
            }
            ObserveSurfaceSimulationMode();
            ApplySurfaceConfiguration(true);
            ResetCollisionProcessing(true);
        }

        protected override void OnBehaviourKillStarted()
        {
            lifecycleSuspended = true;
            if (!IsActive) return;

            Context.Muscles.SetCombatBoostsEnabled(true);
            ReleaseKinematicSimulationMode(true);
            ResetCollisionProcessing(false);
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            ApplyRecoveryConfiguration();
            ApplySurfaceConfiguration(true);
            CancelPropActionsForRespawn();
        }

        protected override void OnBehaviourKillEnded()
        {
            lifecycleSuspended = true;
            if (!IsActive) return;

            Context.Muscles.SetCombatBoostsEnabled(false);
            if (State != RagdollPuppetState.Unpinned)
            {
                RagdollPuppetState previous = State;
                stateMachine.Reset(RagdollPuppetState.Unpinned);
                ApplyRecoveryConfiguration();
                ApplySurfaceConfiguration(true);
                RaiseStateChanged(
                    previous,
                    RagdollPuppetState.Unpinned,
                    RagdollPuppetTransitionReason.LifecycleDeath);
            }
            else
            {
                ApplySurfaceConfiguration(true);
            }
        }

        protected override void OnBehaviourResurrected()
        {
            lifecycleSuspended = false;
            if (!IsActive) return;

            stateMachine.Reset(RagdollPuppetState.Unpinned);
            ApplyPropDropPolicyForCurrentState();
            stateMachine.SetElapsedTime(float.PositiveInfinity);
            centerOfMass.Reset();
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            Context.Muscles.SetAllPositionSuppressions(1f);
            Context.Muscles.SetCombatBoostsEnabled(true);
            ApplyRecoveryConfiguration();
            ApplySurfaceConfiguration(true);
            ResetCollisionProcessing(false);
        }

        protected override void OnBehaviourTeleported(
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot,
            bool moveToTarget)
        {
            if (centerOfMass != null)
            {
                centerOfMass.Reset();
            }

            preparedGroundNormal =
                RagdollPuppetBehaviourMath.TransformDirectionForTeleport(
                    preparedGroundNormal,
                    deltaRotation,
                    GetWorldUp());

            if (State != RagdollPuppetState.GetUp || !moveToTarget)
            {
                return;
            }

            // The external teleport has already moved the Target with the Puppet. Do not
            // apply the pending one-shot root correction again and do not restart GetUp.
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = true;
        }

        protected override void OnBehaviourDeactivated()
        {
            Context.Muscles.SetCombatBoostsEnabled(false);
            if (colliderSurfaceController != null)
            {
                colliderSurfaceController.Restore();
                if (Context.Animator)
                {
                    Context.Animator.ReapplyInternalCollisionPolicy();
                }
            }
            hasObservedSurfaceSimulationMode = false;
            Context.Muscles.ClearPositionSuppressionRecoveryMultiplier();
            Context.Muscles.ClearMinimumPositionAuthorityMultiplier();
            ReleaseKinematicSimulationMode(true);

            if (stateMachine != null)
            {
                stateMachine.Reset(RagdollPuppetState.Puppet);
            }

            if (centerOfMass != null)
            {
                centerOfMass.SetActive(false);
                centerOfMass.Reset();
            }

            lastKnockOutBone = RagdollBoneHandle.Invalid;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            unmappedContactTracker.Reset();
            kinematicContactTracker.Reset();
            kinematicActivationQueue.Reset();
            unmappedContactActive = false;
            kinematicActivationContactActive = false;
            kinematicModeManaged = false;
            normalModeMappingWeight = 1f;
            ResetCollisionProcessing(false);
        }

        protected override void OnBehaviourCollision(
            RagdollCollisionEvent collisionEvent)
        {
            InvokeCollisionSafely(
                collisionObservedSubscribers.Snapshot,
                collisionEvent);
            if (centerOfMass != null)
            {
                centerOfMass.RegisterCollision(collisionEvent);
            }
            if (lifecycleSuspended) return;
            RagdollPuppetCollisionResponseMath.LayerResolution layerResolution =
                RagdollPuppetCollisionResponseMath.ResolveLayer(
                    collisionResistanceMultipliers,
                    collisionEvent.OtherLayer,
                    collisionThreshold);

            RagdollPuppetCollisionRejectionReason rejectionReason;
            if (!collisionProcessor.TryAccept(
                collisionEvent.FixedTime,
                collisionEvent.Phase,
                collisionEvent.OtherLayer,
                collisionEvent.Impulse.sqrMagnitude,
                collisionLayers.value,
                layerResolution.CollisionThreshold,
                maximumCollisionsPerFixedStep,
                out rejectionReason))
            {
                rejectedCollisionCount++;
                lastCollisionRejectionReason = rejectionReason;
                return;
            }

            unmappedContactTracker.Register(collisionEvent.FixedTime);
            QueueKinematicActivation(collisionEvent);
            acceptedCollisionCount++;
            lastCollisionRejectionReason =
                RagdollPuppetCollisionRejectionReason.None;
            lastAcceptedCollisionBone = collisionEvent.Bone;
            lastAcceptedCollisionImpulse = collisionEvent.ImpulseMagnitude;
            lastAcceptedCollisionFixedTime = collisionEvent.FixedTime;

            // Collision.impulse is also the support impulse used to resolve a
            // persistent contact. Reapplying it as damage on every Stay makes a
            // stationary character continuously lose pin authority under gravity.
            // Stay remains accepted for contact-driven modes and diagnostics; only
            // the beginning of a collision may apply a new unpin impulse.
            float positionSuppression =
                collisionEvent.Phase == RagdollCollisionPhase.Enter
                    ? ApplyCollisionUnpin(collisionEvent, layerResolution)
                    : 0f;
            InvokeCollisionSafely(
                collisionAcceptedSubscribers.Snapshot,
                collisionEvent);
            if (positionSuppression > 0f)
            {
                InvokeCollisionImpulseSafely(
                    collisionUnpinSubscribers.Snapshot,
                    collisionEvent,
                    positionSuppression);
            }
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            if (lifecycleSuspended)
            {
                if (Context.Animator.IsKilling)
                {
                    Context.Muscles.AdvanceBoostFalloff(
                        boostFalloff,
                        deltaTime);
                }
                ApplySurfaceConfiguration(false);
                return;
            }

            Context.Muscles.AdvanceBoostFalloff(boostFalloff, deltaTime);
            ApplyRecoveryConfiguration();
            ApplySurfaceConfiguration(false);

            unmappedContactActive = unmappedContactTracker.IsRecent(
                Time.fixedTime,
                Time.fixedDeltaTime);
            kinematicActivationContactActive =
                kinematicContactTracker.IsRecent(
                    Time.fixedTime,
                    Time.fixedDeltaTime);
            ProcessPendingKinematicActivation();

            float normalModeTarget =
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    normalMode,
                    State,
                    unmappedContactActive);
            normalModeMappingWeight =
                RagdollPuppetNormalModeMath.StepMappingWeight(
                    normalModeMappingWeight,
                    normalModeTarget,
                    mappingBlendSpeed,
                    deltaTime);

            centerOfMass.Configure(
                groundLayers,
                groundProbeStartOffset,
                groundProbeDistance,
                maximumGroundAngle);
            centerOfMass.FixedUpdate(deltaTime);

            // Alignment is applied in OnModifyTargetPose later in this same animator tick.
            // Do not complete a zero-duration GetUp before that one-shot correction occurs.
            if (State != RagdollPuppetState.GetUp || !targetAlignmentPending)
            {
                RagdollPuppetState previous = State;
                if (stateMachine.Advance(deltaTime, minimumGetUpDuration))
                {
                    getUpOrientation = RagdollGetUpOrientation.Unknown;
                    getUpBlendCompletedByTeleport = false;
                    ApplyRecoveryConfiguration();
                    ApplySurfaceConfiguration(true);
                    RaiseStateChanged(
                        previous,
                        RagdollPuppetState.Puppet,
                        RagdollPuppetTransitionReason.GetUpCompleted);
                }
            }

            if (canGetUp
                && State == RagdollPuppetState.Unpinned
                && TryBeginGetUp())
            {
                UpdateKinematicSimulationMode();
                ApplySurfaceConfiguration(false);
                return;
            }

            // canStagger (recovery step) and balancerSettings.TorqueMlp>0 (continuous
            // ankle correction) are independent PuppetMaster capabilities -- a
            // project may want the reactive balancer without opting into steps,
            // or vice versa. Only gate the shared classification on needing either.
            bool balanceMonitoringRequired = canStagger || balancerSettings.TorqueMlp > 0f;
            if (balanceMonitoringRequired
                && State == RagdollPuppetState.Puppet
                && !targetAlignmentPending
                && TryClassifyStaggerBalance(out RagdollBipedBalanceState staggerClassification))
            {
                ApplyReactiveBalancer(staggerClassification);
                if (canStagger) EvaluateStaggerTrigger(staggerClassification, deltaTime);
            }

            if (!loseBalanceOnTargetDrift
                || State == RagdollPuppetState.Unpinned
                || targetAlignmentPending)
            {
                UpdateKinematicSimulationMode();
                ApplySurfaceConfiguration(false);
                return;
            }

            RagdollBoneHandle knockOutBone;
            if (TryFindKnockOutBone(out knockOutBone))
            {
                LoseBalance(
                    knockOutBone,
                    State == RagdollPuppetState.GetUp
                        ? RagdollPuppetTransitionReason.GetUpInterrupted
                        : RagdollPuppetTransitionReason.TargetDrift);
            }

            UpdateKinematicSimulationMode();
            ApplySurfaceConfiguration(false);
        }

        protected override void OnModifyTargetPose(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs)
        {
            if (!targetAlignmentPending || State != RagdollPuppetState.GetUp)
            {
                return;
            }

            if (canMoveTarget)
            {
                ApplyTargetRootAlignment(pairs);
            }
            targetAlignmentPending = false;
        }

        protected override float OnGetLifecycleMuscleWeight(
            RagdollAnimator.AnimatedPair pair)
        {
            return ResolveStateRotationAuthority(pair);
        }

        protected override void OnModifyBoneProfile(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
            if (lifecycleSuspended) return;

            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    State,
                    GetUpProgress,
                    unpinnedMuscleWeightMultiplier);

            boneProfile.MultiplyPositionPinWeight(weights.PositionAuthority);
            boneProfile.rotationAlpha *= ResolveStateRotationAuthority(pair);
        }

        float ResolveStateRotationAuthority(
            RagdollAnimator.AnimatedPair pair)
        {
            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    State,
                    GetUpProgress,
                    unpinnedMuscleWeightMultiplier);

            float rotationAuthority = weights.RotationAuthority;
            if (State != RagdollPuppetState.Puppet)
            {
                return rotationAuthority;
            }

            float effectivePinAuthority =
                Context.Muscles.GetEffectivePositionAuthority(pair.Handle);
            return rotationAuthority
                * RagdollMuscleRecoveryMath.ResolveRelativeMuscleWeight(
                    effectivePinAuthority,
                    muscleWeightRelativeToPinWeight);
        }

        protected override void OnModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
            // BehaviourPuppet.canMoveTarget explicitly owns the Target root while
            // Unpinned and GetUp. Child mapping remains active so a network-owned
            // root can still display the physical pose without being displaced.
            if (!canMoveTarget
                && State != RagdollPuppetState.Puppet
                && pair.RagdollBone.IsRoot)
            {
                mappingWeights.positionWeight = 0f;
                mappingWeights.rotationWeight = 0f;
                return;
            }

            if (State == RagdollPuppetState.Puppet)
            {
                float normalMapping = Mathf.Clamp01(normalModeMappingWeight);
                mappingWeights.Multiply(normalMapping, normalMapping);
            }

            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    State,
                    GetUpProgress,
                    unpinnedMuscleWeightMultiplier);

            float blend = weights.MaximumMappingBlend;
            if (blend <= 0f) return;

            RagdollMuscleBehaviourSettings settings =
                Context.Muscles.GetBehaviourSettings(pair.Handle);
            float maximum = Mathf.Clamp01(settings.maximumMappingAuthority);

            float maximumPositionMapping =
                pair.MappingWeights.PositionWeight * maximum;
            float maximumRotationMapping =
                pair.MappingWeights.RotationWeight * maximum;

            mappingWeights.positionWeight = Mathf.Lerp(
                mappingWeights.PositionWeight,
                maximumPositionMapping,
                blend);
            mappingWeights.rotationWeight = Mathf.Lerp(
                mappingWeights.RotationWeight,
                maximumRotationMapping,
                blend);
        }

        void QueueKinematicActivation(
            RagdollCollisionEvent collisionEvent)
        {
            if (normalMode != RagdollPuppetNormalMode.Kinematic
                || State != RagdollPuppetState.Puppet
                || !KinematicSimulationAvailable
                || !simulationModeController.isActiveAndEnabled)
            {
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Disabled
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Disabled)
            {
                return;
            }

            Rigidbody otherRigidbody = collisionEvent.OtherRigidbody;
            RagdollPuppetKinematicActivationSource source =
                RagdollPuppetKinematicActivationPolicy.ResolveSource(
                    otherRigidbody != null,
                    otherRigidbody != null && otherRigidbody.isKinematic);

            if (!RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                normalMode,
                State,
                source,
                collisionEvent.ImpulseMagnitude,
                activateOnImpulse,
                activateOnStaticCollisions,
                activateOnDynamicCollisions))
            {
                return;
            }

            // Eligible Stay callbacks keep an already activated Puppet Active until
            // the contact stream ends. Activation itself is queued only while the
            // simulation is Kinematic or is transitioning toward Kinematic.
            kinematicContactTracker.Register(collisionEvent.FixedTime);
            kinematicActivationContactActive = true;

            if (simulationModeController.CurrentMode
                    != RagdollSimulationMode.Kinematic
                && simulationModeController.TargetMode
                    != RagdollSimulationMode.Kinematic)
            {
                return;
            }

            kinematicActivationQueue.Request(
                source,
                collisionEvent.ImpulseMagnitude,
                collisionEvent.FixedTime);
        }

        void ProcessPendingKinematicActivation()
        {
            RagdollPuppetKinematicActivationSource source;
            float impulse;
            float fixedTime;
            if (!kinematicActivationQueue.TryConsume(
                out source,
                out impulse,
                out fixedTime))
            {
                return;
            }

            if (normalMode != RagdollPuppetNormalMode.Kinematic
                || State != RagdollPuppetState.Puppet
                || !KinematicSimulationAvailable
                || !simulationModeController.isActiveAndEnabled)
            {
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Disabled
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Disabled)
            {
                return;
            }

            if (simulationModeController.CurrentMode
                    != RagdollSimulationMode.Kinematic
                && simulationModeController.TargetMode
                    != RagdollSimulationMode.Kinematic)
            {
                return;
            }

            if (!simulationModeController.SetModeImmediate(
                RagdollSimulationMode.Active))
            {
                return;
            }

            kinematicModeManaged = true;
            lastKinematicActivationSource = source;
            lastKinematicActivationImpulse = impulse;
            lastKinematicActivationFixedTime = fixedTime;
            kinematicActivationCount++;
            InvokeKinematicActivatedSafely(
                kinematicActivatedSubscribers.Snapshot,
                source,
                impulse);
        }

        void UpdateKinematicSimulationMode()
        {
            if (normalMode != RagdollPuppetNormalMode.Kinematic
                || !KinematicSimulationAvailable
                || !simulationModeController.isActiveAndEnabled)
            {
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Disabled
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Disabled)
            {
                return;
            }

            if (State != RagdollPuppetState.Puppet)
            {
                EnsureActiveForNonPuppetState();
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Kinematic
                && !simulationModeController.IsTransitioning)
            {
                return;
            }

            TryEnterKinematicSimulationMode(false);
        }

        bool TryEnterKinematicSimulationMode(bool immediate)
        {
            if (normalMode != RagdollPuppetNormalMode.Kinematic
                || State != RagdollPuppetState.Puppet)
            {
                return false;
            }

            EnsureKinematicSimulationController();
            if (!simulationModeController.IsInitialized
                || !simulationModeController.isActiveAndEnabled)
            {
                return false;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Disabled
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Disabled)
            {
                return false;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Kinematic
                && !simulationModeController.IsTransitioning)
            {
                return false;
            }

            if (!RagdollPuppetKinematicActivationPolicy
                .ShouldReturnToKinematic(
                    normalMode,
                    State,
                    simulationModeController.CurrentMode,
                    simulationModeController.IsTransitioning,
                    kinematicActivationContactActive,
                    kinematicActivationQueue.HasRequest,
                    AreTemporarySuppressionsRecovered()))
            {
                return false;
            }

            bool changed = immediate
                ? simulationModeController.SetModeImmediate(
                    RagdollSimulationMode.Kinematic)
                : simulationModeController.SetMode(
                    RagdollSimulationMode.Kinematic);
            if (changed)
            {
                kinematicModeManaged = true;
            }

            return changed;
        }

        void EnsureActiveForNonPuppetState()
        {
            if (normalMode != RagdollPuppetNormalMode.Kinematic
                || !KinematicSimulationAvailable
                || !simulationModeController.isActiveAndEnabled)
            {
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Disabled
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Disabled)
            {
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Kinematic
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Kinematic)
            {
                if (simulationModeController.SetModeImmediate(
                    RagdollSimulationMode.Active))
                {
                    kinematicModeManaged = true;
                }
            }
        }

        void ReleaseKinematicSimulationMode(bool immediate)
        {
            kinematicContactTracker.Reset();
            kinematicActivationContactActive = false;
            kinematicActivationQueue.Reset();
            if (!kinematicModeManaged)
            {
                return;
            }

            if (!simulationModeController
                || !simulationModeController.IsInitialized)
            {
                kinematicModeManaged = false;
                return;
            }

            if (simulationModeController.CurrentMode
                    == RagdollSimulationMode.Kinematic
                || simulationModeController.TargetMode
                    == RagdollSimulationMode.Kinematic)
            {
                if (immediate)
                {
                    simulationModeController.SetModeImmediate(
                        RagdollSimulationMode.Active);
                }
                else
                {
                    simulationModeController.SetMode(
                        RagdollSimulationMode.Active);
                }
            }

            kinematicModeManaged = false;
        }

        bool AreTemporarySuppressionsRecovered()
        {
            for (int index = 0; index < Context.Pairs.Count; index++)
            {
                MuscleRuntimeState state =
                    Context.Muscles.GetState(Context.Pairs[index].Handle);
                if (state.PositionSuppression > KinematicSuppressionEpsilon
                    || state.RotationSuppression > KinematicSuppressionEpsilon)
                {
                    return false;
                }
            }

            return true;
        }

        void EnsureKinematicSimulationController()
        {
            if (simulationModeController && simulationModeController.enabled)
            {
                return;
            }

            throw new InvalidOperationException(
                "RagdollPuppetBehaviour NormalMode.Kinematic requires an enabled "
                + "RagdollSimulationModeController beside RagdollAnimator before "
                + "RagdollAnimator.Start initializes its modifiers.");
        }

        static void ValidateNormalMode(RagdollPuppetNormalMode mode)
        {
            switch (mode)
            {
                case RagdollPuppetNormalMode.Active:
                case RagdollPuppetNormalMode.Unmapped:
                case RagdollPuppetNormalMode.Kinematic:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unsupported RagdollPuppetNormalMode value.");
            }
        }

        float ApplyCollisionUnpin(
            RagdollCollisionEvent collisionEvent,
            RagdollPuppetCollisionResponseMath.LayerResolution layerResolution)
        {
            RagdollAnimator.AnimatedPair pair =
                Context.GetPair(collisionEvent.Bone);
            float targetSpeed = pair.GetTargetSpeedAtPhysicsBoundary(
                Time.fixedDeltaTime);
            float globalResistance = collisionResistance.Evaluate(targetSpeed);
            float muscleResistance = Context.Muscles
                .GetBehaviourSettings(collisionEvent.Bone)
                .collisionResistance;
            float stateResistanceMultiplier =
                RagdollPuppetBehaviourMath.ResolveGetUpStateMultiplier(
                    State,
                    getUpCollisionResistanceMultiplier);
            float effectiveResistance =
                RagdollPuppetCollisionResponseMath.EvaluateEffectiveResistance(
                    globalResistance,
                    layerResolution.ResistanceMultiplier,
                    muscleResistance,
                    stateResistanceMultiplier);
            float sourceImpulseMultiplier =
                RagdollMuscleController.ResolveExternalImpulseMultiplier(
                    collisionEvent.OtherRigidbody,
                    Context.Muscles);
            float damageImpulse =
                RagdollMuscleBoostMath.ApplyImpulseMultiplier(
                    collisionEvent.ImpulseMagnitude,
                    sourceImpulseMultiplier);
            float unmitigatedPositionSuppression =
                RagdollPuppetCollisionResponseMath.EvaluatePositionSuppression(
                    damageImpulse,
                    globalResistance,
                    layerResolution.ResistanceMultiplier,
                    muscleResistance,
                    stateResistanceMultiplier);
            float receivingImmunity = Context.Muscles.CombatBoostsEnabled
                ? Context.Muscles.GetImmunity(collisionEvent.Bone)
                : 0f;
            float appliedPositionSuppression = 0f;

            if (unmitigatedPositionSuppression > 0f)
            {
                MuscleImpactSettings impact = new MuscleImpactSettings
                {
                    positionSuppression = unmitigatedPositionSuppression,
                    rotationSuppression = 0f,
                    maximumPropagationDistance =
                        Mathf.Max(0, Context.Bindings.BoneCount - 1),
                    propagationFalloff = 1f
                };
                appliedPositionSuppression =
                    Context.Muscles.ApplyResolvedImpact(
                        collisionEvent.Bone,
                        impact);
            }

            lastCollisionResponse =
                new RagdollPuppetCollisionResponseSnapshot(
                    true,
                    collisionEvent.Bone,
                    collisionEvent.FixedTime,
                    collisionEvent.ImpulseMagnitude,
                    damageImpulse,
                    sourceImpulseMultiplier,
                    receivingImmunity,
                    unmitigatedPositionSuppression,
                    targetSpeed,
                    globalResistance,
                    layerResolution.ResistanceMultiplier,
                    muscleResistance,
                    stateResistanceMultiplier,
                    effectiveResistance,
                    appliedPositionSuppression,
                    layerResolution.RuleIndex);

            return appliedPositionSuppression;
        }

        bool BeginGetUp(
            RagdollGetUpOrientation orientation,
            Vector3 groundNormal)
        {
            if (State != RagdollPuppetState.Unpinned
                || orientation == RagdollGetUpOrientation.Unknown)
            {
                return false;
            }

            if (!TransitionTo(
                RagdollPuppetState.GetUp,
                RagdollPuppetTransitionReason.GetUpStarted))
            {
                return false;
            }

            PrepareExplicitGetUp(orientation, groundNormal);
            return true;
        }

        void PrepareExplicitGetUp(
            RagdollGetUpOrientation orientation,
            Vector3 groundNormal)
        {
            getUpOrientation = orientation;
            preparedGroundNormal = groundNormal.sqrMagnitude > Mathf.Epsilon
                ? groundNormal.normalized
                : GetWorldUp();
            targetAlignmentPending = true;
            getUpBlendCompletedByTeleport = false;
            InvokeGetUpPoseSelectedSafely(
                getUpPoseSubscribers.Snapshot,
                orientation);
            InvokeGetUpEvent(orientation);
        }

        void InvokeGetUpEvent(RagdollGetUpOrientation orientation)
        {
            if (orientation == RagdollGetUpOrientation.Prone)
            {
                InvokePuppetEventSafely(onGetUpProne);
            }
            else if (orientation == RagdollGetUpOrientation.Supine)
            {
                InvokePuppetEventSafely(onGetUpSupine);
            }
        }

        RagdollGetUpOrientation ResolveGetUpOrientation(Vector3 groundNormal)
        {
            RagdollGetUpOrientation classified =
                quadrupedGetUp
                    ? RagdollGetUpAlignmentMath.ClassifyQuadruped(
                        getUpReferencePair.RagdollBone.Transform.rotation,
                        bodyFrontAxis,
                        bodyUpAxis,
                        groundNormal,
                        minimumOrientationDot)
                    : RagdollGetUpAlignmentMath.Classify(
                        getUpReferencePair.RagdollBone.Transform.rotation,
                        bodyFrontAxis,
                        groundNormal,
                        minimumOrientationDot);

            return classified != RagdollGetUpOrientation.Unknown
                ? classified
                : fallbackOrientation;
        }

        void ApplyTargetRootAlignment(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs)
        {
            Transform targetRoot = Context.Animator.transform;
            Vector3 previousRootPosition = targetRoot.position;
            Quaternion previousRootRotation = targetRoot.rotation;

            Vector3 desiredRootPosition;
            Quaternion desiredRootRotation;
            Vector3 offset = getUpOrientation == RagdollGetUpOrientation.Prone
                ? getUpOffsetProne
                : getUpOffsetSupine;

            RagdollGetUpAlignmentMath.CalculateTargetRootPose(
                previousRootPosition,
                previousRootRotation,
                getUpReferencePair.TargetBone.position,
                getUpReferencePair.RagdollBone.Transform.position,
                getUpReferencePair.RagdollBone.Transform.rotation,
                bodyUpAxis,
                getUpOrientation,
                preparedGroundNormal,
                targetRoot.forward,
                offset,
                out desiredRootPosition,
                out desiredRootRotation);

            Quaternion rotationDelta = desiredRootRotation
                * Quaternion.Inverse(previousRootRotation);
            targetRoot.SetPositionAndRotation(
                desiredRootPosition,
                desiredRootRotation);

            for (int index = 0; index < pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = pairs[index];
                RagdollAnimator.AnimatedPose pose = pair.currentPose;
                pose.worldPosition = RagdollGetUpAlignmentMath.ApplyPositionDelta(
                    pose.worldPosition,
                    previousRootPosition,
                    desiredRootPosition,
                    rotationDelta);
                pose.worldRotation = rotationDelta * pose.worldRotation;

                if (pair.RagdollBone.IsRoot)
                {
                    Transform ragdollParent = pair.RagdollBone.Transform.parent;
                    pose.localRotation = ragdollParent
                        ? Quaternion.Inverse(ragdollParent.rotation)
                            * pose.worldRotation
                        : pose.worldRotation;
                }

                pair.currentPose = pose;
            }
        }

        RagdollAnimator.AnimatedPair FindRootPair()
        {
            for (int index = 0; index < Context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = Context.Pairs[index];
                if (pair.RagdollBone.IsRoot)
                {
                    return pair;
                }
            }

            throw new InvalidOperationException(
                "RagdollPuppetBehaviour requires an animated pair for the ragdoll root bone.");
        }

        RagdollAnimator.AnimatedPair FindGetUpReferencePair()
        {
            for (int index = 0; index < Context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = Context.Pairs[index];
                RagdollMuscleGroup group;
                if (Context.Muscles.TryGetMuscleGroup(pair.Handle, out group)
                    && group == RagdollMuscleGroup.Hips)
                {
                    return pair;
                }
            }

            // A muscle profile is optional. Without an explicit Hips assignment,
            // the ragdoll root remains the deterministic pelvis-compatible fallback.
            return rootPair;
        }

        bool TryFindKnockOutBone(out RagdollBoneHandle knockOutBone)
        {
            float stateDistanceMultiplier =
                State == RagdollPuppetState.GetUp
                    ? Mathf.Max(0f, getUpKnockOutDistanceMultiplier)
                    : 1f;

            float statePositionAuthority =
                RagdollPuppetStateWeights.Evaluate(
                    State,
                    GetUpProgress,
                    unpinnedMuscleWeightMultiplier).PositionAuthority;

            for (int index = 0; index < Context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = Context.Pairs[index];

                RagdollMuscleGroup pairGroup;
                if (Context.Muscles.TryGetMuscleGroup(pair.Handle, out pairGroup)
                    && !RagdollPuppetBehaviourMath.ShouldCountTowardKnockout(
                        pairGroup, propDriftPolicy))
                {
                    continue;
                }

                MuscleRuntimeState muscleState =
                    Context.Muscles.GetState(pair.Handle);
                RagdollMuscleBehaviourSettings settings =
                    Context.Muscles.GetBehaviourSettings(pair.Handle);

                float targetDistance = Vector3.Distance(
                    pair.RagdollBone.Transform.position,
                    pair.currentPose.worldPosition);

                // ponytail: currentPose can go stale relative to the live Target
                // after ApplyTargetRootAlignment's rigid root-delta propagation
                // (the propagation math itself is correct; the snapshot it moves
                // is not refreshed if the Target is nudged again afterward, e.g.
                // by weapon/IK binding). A cached-only distance can then false-
                // positive TargetDrift on a bone that is actually fine. Require
                // the live Target to also exceed threshold before knocking out.
                float knockOutThreshold =
                    settings.knockOutDistance * stateDistanceMultiplier;
                if (pair.TargetBone
                    && targetDistance > knockOutThreshold)
                {
                    float liveTargetDistance = Vector3.Distance(
                        pair.RagdollBone.Transform.position,
                        pair.TargetBone.position);
                    if (liveTargetDistance <= knockOutThreshold)
                    {
                        UnityEngine.Debug.Log("TARGET_POSE_MISMATCH bone=" + pair.Name
                            + " cached=" + targetDistance.ToString("0.000")
                            + " live=" + liveTargetDistance.ToString("0.000")
                            + " threshold=" + knockOutThreshold.ToString("0.000")
                            + " decision=suppress_target_drift");
                        continue;
                    }
                }

                BoneProfile authoredProfile =
                    Context.Animator.GetBoneProfile(pair.Name);
                float configuredPinWeight =
                    RagdollPuppetBehaviourMath.ResolveConfiguredPinWeight(
                        authoredProfile.PositionPinWeight,
                        Context.Animator.MasterPinWeight,
                        muscleState.PositionAuthority);
                float effectivePinWeight =
                    RagdollPuppetBehaviourMath.ResolveEffectivePinWeight(
                        configuredPinWeight,
                        muscleState.PositionSuppression,
                        Context.Muscles.GetAppliedMinimumPositionAuthority(pair.Handle),
                        statePositionAuthority);

                if (!RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    targetDistance,
                    settings.knockOutDistance,
                    effectivePinWeight,
                    configuredPinWeight,
                    pinWeightThreshold,
                    stateDistanceMultiplier,
                    unpinnedMuscleKnockout))
                {
                    continue;
                }

                knockOutBone = pair.Handle;
                lastKnockOutDistance = targetDistance;
                lastKnockOutThreshold = settings.knockOutDistance
                    * stateDistanceMultiplier;
                lastKnockOutEffectivePinWeight = effectivePinWeight;
                return true;
            }

            knockOutBone = RagdollBoneHandle.Invalid;
            return false;
        }

        bool LoseBalance(
            RagdollBoneHandle source,
            RagdollPuppetTransitionReason reason)
        {
            if (State != RagdollPuppetState.Puppet
                && State != RagdollPuppetState.GetUp)
            {
                return false;
            }

            EnsureActiveForNonPuppetState();
            if (!TransitionTo(RagdollPuppetState.Unpinned, reason))
            {
                return false;
            }

            targetAlignmentPending = false;
            getUpBlendCompletedByTeleport = false;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            lastKnockOutBone = source;
            return true;
        }

        bool TransitionTo(
            RagdollPuppetState next,
            RagdollPuppetTransitionReason reason,
            bool direct = false)
        {
            if (stateMachine == null) return false;

            RagdollPuppetState previous = stateMachine.State;
            if (!(direct
                ? stateMachine.TrySetState(next)
                : stateMachine.TryTransition(next)))
            {
                return false;
            }

            if (next == RagdollPuppetState.Unpinned)
            {
                getUpBlendCompletedByTeleport = false;
                LimitVelocitiesForUnpinnedState();
            }
            else if (next == RagdollPuppetState.GetUp)
            {
                getUpBlendCompletedByTeleport = false;
                Context.Muscles.SetAllPositionSuppressions(1f);
            }
            else
            {
                getUpBlendCompletedByTeleport = false;
            }

            ApplyRecoveryConfiguration();
            ApplySurfaceConfiguration(true);
            RaiseStateChanged(previous, next, reason);
            return true;
        }

        void LimitVelocitiesForUnpinnedState()
        {
            if (maxRigidbodyVelocity == Mathf.Infinity) return;

            for (int index = 0; index < Context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = Context.Pairs[index];
                Rigidbody rigidbody = pair.RagdollBone.Rigidbody;
                if (rigidbody && !rigidbody.isKinematic)
                {
                    rigidbody.linearVelocity =
                        RagdollPuppetBehaviourMath.LimitVelocity(
                            rigidbody.linearVelocity,
                            maxRigidbodyVelocity);
                }

                pair.poseLinearVelocity =
                    RagdollPuppetBehaviourMath.LimitVelocity(
                        pair.poseLinearVelocity,
                        maxRigidbodyVelocity);
            }
        }

        void RaiseStateChanged(
            RagdollPuppetState previous,
            RagdollPuppetState current,
            RagdollPuppetTransitionReason reason)
        {
            ApplyPropDropPolicy(previous, current);
            InvokeStateChangedSafely(previous, current, reason);
            InvokeTransitionEvents(previous, current, reason);
        }

        void InvokeStateChangedSafely(
            RagdollPuppetState previous,
            RagdollPuppetState current,
            RagdollPuppetTransitionReason reason)
        {
            Delegate[] subscribers = stateChangedSubscribers.Snapshot;
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollPuppetState, RagdollPuppetState,
                        RagdollPuppetTransitionReason>)subscribers[index])(
                        previous,
                        current,
                        reason);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeCollisionSafely(
            Delegate[] subscribers,
            RagdollCollisionEvent collisionEvent)
        {
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollCollisionEvent>)subscribers[index])(
                        collisionEvent);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeCollisionImpulseSafely(
            Delegate[] subscribers,
            RagdollCollisionEvent collisionEvent,
            float suppression)
        {
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollCollisionEvent, float>)subscribers[index])(
                        collisionEvent,
                        suppression);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeGetUpPoseSelectedSafely(
            Delegate[] subscribers,
            RagdollGetUpOrientation orientation)
        {
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollGetUpOrientation>)subscribers[index])(
                        orientation);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeKinematicActivatedSafely(
            Delegate[] subscribers,
            RagdollPuppetKinematicActivationSource source,
            float impulse)
        {
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RagdollPuppetKinematicActivationSource, float>)
                        subscribers[index])(source, impulse);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeTransitionEvents(
            RagdollPuppetState previous,
            RagdollPuppetState current,
            RagdollPuppetTransitionReason reason)
        {
            if (current == RagdollPuppetState.Unpinned
                && previous != RagdollPuppetState.Unpinned
                && reason != RagdollPuppetTransitionReason.LifecycleDeath)
            {
                InvokePuppetEventSafely(onLoseBalance);
                if (previous == RagdollPuppetState.Puppet)
                {
                    InvokePuppetEventSafely(onLoseBalanceFromPuppet);
                }
                else if (previous == RagdollPuppetState.GetUp)
                {
                    InvokePuppetEventSafely(onLoseBalanceFromGetUp);
                }
            }
            else if (previous == RagdollPuppetState.GetUp
                && current == RagdollPuppetState.Puppet)
            {
                InvokePuppetEventSafely(onRegainBalance);
            }
        }

        void InvokePuppetEventSafely(RagdollPuppetEvent puppetEvent)
        {
            try { puppetEvent.Invoke(this); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// Hysteresis gate over the RequiresStep classification -- fires OnRequiresStep
        /// exactly once per sustained episode. Classification is computed by the caller
        /// (ClassifyStaggerBalance) so this method stays independently testable.
        /// </summary>
        bool EvaluateStaggerTrigger(RagdollBipedBalanceState classification, float deltaTime)
        {
            if (!canStagger)
            {
                return false;
            }

            bool fired = balanceTrigger.Evaluate(
                classification, deltaTime, minimumRequiresStepDuration);
            if (fired)
            {
                InvokePuppetEventSafely(onRequiresStep);
            }
            return fired;
        }

        bool TryClassifyStaggerBalance(out RagdollBipedBalanceState classification)
        {
            classification = RagdollBipedBalanceState.Stable;
            if (!Context.Bindings.TryGetBone(staggerLeftFootBone, out RagdollBone leftFoot)
                || !Context.Bindings.TryGetBone(staggerRightFootBone, out RagdollBone rightFoot)
                || leftFoot.Rigidbody == null || rightFoot.Rigidbody == null)
            {
                return false;
            }

            RagdollGroundingSnapshot snapshot = centerOfMass.Snapshot;
            float margin = RagdollBipedBalanceMath.SignedCaptureMargin(
                snapshot.CenterOfMass,
                snapshot.CenterOfMassVelocity,
                leftFoot.Rigidbody.position,
                rightFoot.Rigidbody.position,
                staggerPendulumLength,
                Physics.gravity.magnitude,
                staggerSupportRadius);
            classification = RagdollBipedBalanceMath.Classify(
                margin, staggerStableMargin, staggerRequiresStepMargin);
            return true;
        }

        /// <summary>
        /// SubBehaviourBalancer port: continuous corrective ankle torque while the
        /// capture point has drifted enough to need correction but not enough to
        /// require a step (RecoverableWithoutStep) -- a damping zone *before*
        /// RequiresStep would fire the stagger step. No-ops while torqueMlp==0
        /// (the official product's own default), so this is inert unless opted in.
        /// </summary>
        void ApplyReactiveBalancer(RagdollBipedBalanceState classification)
        {
            if (balancerSettings.TorqueMlp <= 0f
                || classification != RagdollBipedBalanceState.RecoverableWithoutStep)
            {
                return;
            }

            if (!Context.Bindings.TryGetBone(staggerLeftFootBone, out RagdollBone leftFoot)
                || !Context.Bindings.TryGetBone(staggerRightFootBone, out RagdollBone rightFoot)
                || !Context.Bindings.TryGetBone(balancerLeftCalfBone, out RagdollBone leftCalf)
                || !Context.Bindings.TryGetBone(balancerRightCalfBone, out RagdollBone rightCalf)
                || leftFoot.Rigidbody == null || rightFoot.Rigidbody == null
                || leftCalf.Rigidbody == null || rightCalf.Rigidbody == null)
            {
                return;
            }

            RagdollGroundingSnapshot snapshot = centerOfMass.Snapshot;
            Vector3 capturePoint = RagdollBipedBalanceMath.CapturePoint(
                snapshot.CenterOfMass,
                snapshot.CenterOfMassVelocity,
                staggerPendulumLength,
                Physics.gravity.magnitude);
            Vector3 supportCenter =
                (leftFoot.Rigidbody.position + rightFoot.Rigidbody.position) * 0.5f;
            Vector3 centerOfPressureTarget = RagdollBipedBalancerMath.ResolveCenterOfPressureTarget(
                supportCenter, balancerSettings.CopOffset);
            Vector3 torque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                capturePoint,
                snapshot.CenterOfMassVelocity,
                centerOfPressureTarget,
                centerOfMass.Up,
                balancerSettings);
            if (torque.sqrMagnitude <= Mathf.Epsilon) return;

            leftCalf.Rigidbody.AddTorque(torque, ForceMode.Force);
            rightCalf.Rigidbody.AddTorque(torque, ForceMode.Force);
        }

        sealed class CachedSubscribers<T> where T : Delegate
        {
            Delegate combined;
            Delegate[] snapshot = Array.Empty<Delegate>();

            internal Delegate[] Snapshot => snapshot;

            internal void Add(T value)
            {
                if (value == null) return;
                combined = Delegate.Combine(combined, value);
                snapshot = combined.GetInvocationList();
            }

            internal void Remove(T value)
            {
                if (value == null) return;
                combined = Delegate.Remove(combined, value);
                snapshot = combined == null
                    ? Array.Empty<Delegate>()
                    : combined.GetInvocationList();
            }
        }

        void ResetCollisionProcessing(bool resetTotals)
        {
            collisionProcessor.Reset();
            lastAcceptedCollisionBone = RagdollBoneHandle.Invalid;
            lastAcceptedCollisionImpulse = 0f;
            lastAcceptedCollisionFixedTime = 0f;
            lastCollisionRejectionReason =
                RagdollPuppetCollisionRejectionReason.None;
            lastCollisionResponse =
                RagdollPuppetCollisionResponseSnapshot.Empty;

            if (!resetTotals) return;
            acceptedCollisionCount = 0L;
            rejectedCollisionCount = 0L;
        }

        RagdollMuscleController RequireMuscleController()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollPuppetBehaviour has not been initialized by a "
                    + "RagdollBehaviourController.");
            }

            return Context.Muscles;
        }

        RagdollBoneHandle ResolveMuscleIndex(int muscleIndex)
        {
            RequireMuscleController();
            if (muscleIndex < 0 || muscleIndex >= Context.Bindings.BoneCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(muscleIndex),
                    muscleIndex,
                    "The muscle index is outside the active registry.");
            }
            return Context.Bindings.GetHandleAt(muscleIndex);
        }

        void ApplyRecoveryConfiguration()
        {
            if (!IsInitialized) return;

            bool gettingUp = State == RagdollPuppetState.GetUp;
            Context.Muscles.SetMinimumPositionAuthorityMultiplier(
                gettingUp ? 0f : 1f);

            float stateMultiplier =
                RagdollPuppetBehaviourMath.ResolveGetUpStateMultiplier(
                    State,
                    getUpRegainPinSpeedMultiplier);
            float effectiveMultiplier =
                RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                    1f,
                    regainPinSpeed,
                    stateMultiplier);
            Context.Muscles.SetPositionSuppressionRecoveryMultiplier(
                effectiveMultiplier);
        }

        void ApplySurfaceConfiguration(bool force)
        {
            if (colliderSurfaceController == null
                || !colliderSurfaceController.BaselineCaptured)
            {
                return;
            }

            if (simulationModeController
                && simulationModeController.IsInitialized)
            {
                RagdollSimulationMode current =
                    simulationModeController.CurrentMode;
                RagdollSimulationMode target =
                    simulationModeController.TargetMode;
                if (current == RagdollSimulationMode.Disabled
                    || target == RagdollSimulationMode.Disabled)
                {
                    return;
                }

                if (!hasObservedSurfaceSimulationMode
                    || current != lastObservedSurfaceSimulationMode)
                {
                    force = true;
                }

                lastObservedSurfaceSimulationMode = current;
                hasObservedSurfaceSimulationMode = true;
            }

            bool surfaceApplied = colliderSurfaceController.Apply(
                lifecycleSuspended
                    ? RagdollPuppetState.Unpinned
                    : State,
                force);
            if (surfaceApplied && Context.Animator)
            {
                Context.Animator.ReapplyInternalCollisionPolicy();
            }
        }

        void ObserveSurfaceSimulationMode()
        {
            if (!simulationModeController
                || !simulationModeController.IsInitialized)
            {
                hasObservedSurfaceSimulationMode = false;
                return;
            }

            lastObservedSurfaceSimulationMode =
                simulationModeController.CurrentMode;
            hasObservedSurfaceSimulationMode = true;
        }

        void NormalizeCollisionResponseConfiguration()
        {
            if (collisionResistance == null)
            {
                collisionResistance =
                    new RagdollPuppetCollisionResistance();
            }
            collisionResistance.Normalize();

            if (collisionResistanceMultipliers == null)
            {
                collisionResistanceMultipliers =
                    new RagdollPuppetCollisionLayerRule[0];
            }

            for (int index = 0;
                index < collisionResistanceMultipliers.Length;
                index++)
            {
                if (collisionResistanceMultipliers[index] != null)
                {
                    collisionResistanceMultipliers[index].Normalize();
                }
            }
        }

        Vector3 GetWorldUp()
        {
            if (centerOfMass != null) return centerOfMass.Up;

            Vector3 gravity = Physics.gravity;
            return gravity.sqrMagnitude > Mathf.Epsilon
                ? -gravity.normalized
                : Vector3.up;
        }

        static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }

        static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }

        static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)
                || float.IsNaN(value.z) || float.IsInfinity(value.z));
        }

        static Vector3 SanitizeAxis(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) && value.sqrMagnitude > 0.000001f
                ? value.normalized
                : fallback;
        }

        static bool IsFinite(Quaternion value)
        {
            return !(float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)
                || float.IsNaN(value.z) || float.IsInfinity(value.z)
                || float.IsNaN(value.w) || float.IsInfinity(value.w));
        }

        static float QuaternionLengthSquared(Quaternion value)
        {
            return value.x * value.x + value.y * value.y
                + value.z * value.z + value.w * value.w;
        }

        static float SanitizeMaximumVelocity(float value)
        {
            if (float.IsPositiveInfinity(value)) return Mathf.Infinity;
            return float.IsNaN(value) || float.IsNegativeInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }

        void OnValidate()
        {
            pinWeightThreshold = Mathf.Clamp01(pinWeightThreshold);
            unpinnedMuscleWeightMultiplier =
                Mathf.Clamp01(unpinnedMuscleWeightMultiplier);
            regainPinSpeed = float.IsNaN(regainPinSpeed)
                || float.IsInfinity(regainPinSpeed)
                ? 1f
                : Mathf.Clamp(regainPinSpeed, 0.001f, 10f);
            muscleWeightRelativeToPinWeight =
                Mathf.Clamp01(muscleWeightRelativeToPinWeight);
            boostFalloff = RagdollMuscleBoostMath.SanitizeFalloff(boostFalloff);
            maxRigidbodyVelocity =
                SanitizeMaximumVelocity(maxRigidbodyVelocity);
            mappingBlendSpeed = float.IsNaN(mappingBlendSpeed)
                || float.IsInfinity(mappingBlendSpeed)
                ? 0f
                : Mathf.Max(0f, mappingBlendSpeed);
            activateOnImpulse = float.IsNaN(activateOnImpulse)
                || float.IsInfinity(activateOnImpulse)
                ? 0f
                : Mathf.Max(0f, activateOnImpulse);
            collisionThreshold = float.IsNaN(collisionThreshold)
                || float.IsInfinity(collisionThreshold)
                ? 0f
                : Mathf.Max(0f, collisionThreshold);
            maximumCollisionsPerFixedStep = Mathf.Clamp(
                maximumCollisionsPerFixedStep,
                1,
                30);
            NormalizeCollisionResponseConfiguration();
            groundProbeStartOffset = Mathf.Max(0f, groundProbeStartOffset);
            groundProbeDistance = Mathf.Max(0.001f, groundProbeDistance);
            maximumGroundAngle = Mathf.Clamp(maximumGroundAngle, 0f, 89.9f);
            getUpDelay = SanitizeNonNegative(getUpDelay);
            blendToAnimationTime = SanitizeNonNegative(blendToAnimationTime);
            maximumGetUpVelocity =
                SanitizeNonNegative(maximumGetUpVelocity);
            minimumGetUpDuration =
                SanitizeNonNegative(minimumGetUpDuration);
            getUpCollisionResistanceMultiplier =
                SanitizeNonNegative(getUpCollisionResistanceMultiplier);
            getUpRegainPinSpeedMultiplier =
                SanitizeNonNegative(getUpRegainPinSpeedMultiplier);
            getUpKnockOutDistanceMultiplier =
                SanitizeNonNegative(getUpKnockOutDistanceMultiplier);
            minimumOrientationDot = Mathf.Clamp01(minimumOrientationDot);

            if (bodyFrontAxis.sqrMagnitude <= 0.000001f)
            {
                bodyFrontAxis = Vector3.forward;
            }

            if (bodyUpAxis.sqrMagnitude <= 0.000001f)
            {
                bodyUpAxis = Vector3.up;
            }
        }
    }
}
