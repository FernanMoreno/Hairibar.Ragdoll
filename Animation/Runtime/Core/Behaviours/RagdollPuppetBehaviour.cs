using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Puppet/Unpinned/GetUp behaviour with ground validation, center-of-mass sampling,
    /// prone/supine selection and one-shot Target root alignment before GetUp blending.
    /// </summary>
    [AddComponentMenu("Ragdoll/Behaviours/Ragdoll Puppet Behaviour")]
    [DisallowMultipleComponent]
    public sealed class RagdollPuppetBehaviour : RagdollBehaviourBase
    {
        [Header("Losing Balance")]
        [SerializeField] bool loseBalanceOnTargetDrift = true;
        [SerializeField, Range(0f, 1f)] float pinWeightThreshold = 1f;
        [SerializeField, Range(0f, 1f)] float unpinnedMuscleWeightMultiplier = 0.3f;

        [Header("Recovery")]
        [Tooltip("Global multiplier for temporary pin-authority recovery. It composes with the RagdollMuscleController base rate and each semantic group's Regain Position Authority Multiplier.")]
        [SerializeField, Range(0.001f, 10f)] float regainPinSpeed = 1f;
        [Tooltip("How strongly rotational muscle authority follows effective pin authority in the normal Puppet state. Zero preserves authored muscle strength; one makes muscle strength follow pin authority completely.")]
        [SerializeField, Range(0f, 1f)] float muscleWeightRelativeToPinWeight;

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
        [SerializeField, Min(0f)] float minimumGroundedTime = 0.25f;

        [Header("Getting Up")]
        [SerializeField] bool automaticGetUp = true;
        [SerializeField, Min(0f)] float getUpDelay = 1f;
        [SerializeField, Min(0f)] float blendToAnimationTime = 0.5f;
        [SerializeField, Min(0f)] float maximumGetUpVelocity = 0.5f;
        [SerializeField, Min(0f)] float getUpKnockOutDistanceMultiplier = 2f;

        [Header("Get Up Orientation")]
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

        RagdollPuppetStateMachine stateMachine;
        RagdollGroundProbe groundProbe;
        RagdollPuppetCollisionProcessor collisionProcessor;
        RagdollPuppetUnmappedContactTracker unmappedContactTracker;
        // Kinematic mode needs contact memory filtered by its own activation policy.
        RagdollPuppetUnmappedContactTracker kinematicContactTracker;
        RagdollPuppetKinematicActivationQueue kinematicActivationQueue;
        RagdollSimulationModeController simulationModeController;
        RagdollAnimator.AnimatedPair rootPair;
        RagdollBoneHandle lastKnockOutBone = RagdollBoneHandle.Invalid;
        RagdollGetUpOrientation getUpOrientation = RagdollGetUpOrientation.Unknown;
        Vector3 preparedGroundNormal = Vector3.up;
        bool targetAlignmentPending;
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

        public RagdollPuppetState State => stateMachine != null
            ? stateMachine.State
            : RagdollPuppetState.Puppet;

        public float StateElapsedTime => stateMachine != null
            ? stateMachine.StateElapsedTime
            : 0f;

        public float GetUpProgress => stateMachine != null
            ? stateMachine.GetUpProgress(blendToAnimationTime)
            : 1f;

        public RagdollBoneHandle LastKnockOutBone => lastKnockOutBone;
        public RagdollGetUpOrientation GetUpOrientation => getUpOrientation;
        public float RegainPinSpeed => regainPinSpeed;
        public float MuscleWeightRelativeToPinWeight =>
            muscleWeightRelativeToPinWeight;
        public float AppliedRegainPinSpeed => IsInitialized
            ? Context.Muscles.PositionSuppressionRecoveryMultiplier
            : 1f;
        public RagdollPuppetNormalMode NormalMode => normalMode;
        public float MappingBlendSpeed => mappingBlendSpeed;
        public float NormalModeMappingWeight => normalModeMappingWeight;
        public bool UnmappedContactActive => unmappedContactActive;
        public bool ActivateOnStaticCollisions => activateOnStaticCollisions;
        public bool ActivateOnDynamicCollisions => activateOnDynamicCollisions;
        public float ActivateOnImpulse => activateOnImpulse;
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
        public LayerMask CollisionLayers => collisionLayers;
        public float CollisionThreshold => collisionThreshold;
        public int MaximumCollisionsPerFixedStep => maximumCollisionsPerFixedStep;
        public RagdollPuppetCollisionResistance CollisionResistance =>
            collisionResistance;
        public IReadOnlyList<RagdollPuppetCollisionLayerRule>
            CollisionResistanceMultipliers => collisionResistanceMultipliers;
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
        public RagdollGroundingSnapshot Grounding => groundProbe != null
            ? groundProbe.Snapshot
            : RagdollGroundingSnapshot.Empty;

        public bool CanBeginGetUp
        {
            get
            {
                if (!IsInitialized || State != RagdollPuppetState.Unpinned)
                {
                    return false;
                }

                RagdollGroundingSnapshot grounding = Grounding;
                if (!grounding.IsGrounded
                    || grounding.StableTime < Mathf.Max(0f, minimumGroundedTime))
                {
                    return false;
                }

                if (!RagdollPuppetBehaviourMath.IsGetUpReady(
                    StateElapsedTime,
                    getUpDelay,
                    Context.Bindings.Root.Rigidbody.velocity.magnitude,
                    maximumGetUpVelocity))
                {
                    return false;
                }

                return ResolveGetUpOrientation(grounding.GroundNormal)
                    != RagdollGetUpOrientation.Unknown;
            }
        }

        public event Action<
            RagdollPuppetState,
            RagdollPuppetState,
            RagdollPuppetTransitionReason> StateChanged;
        public event Action<RagdollGetUpOrientation> GetUpPoseSelected;

        /// <summary>
        /// Raised immediately for each Enter/Stay collision accepted by the layer,
        /// squared-impulse threshold and per-step budget policy. The embedded Collision
        /// reference is callback-scoped and must not be retained.
        /// </summary>
        public event Action<RagdollCollisionEvent> CollisionAccepted;

        /// <summary>
        /// Raised after an accepted collision has applied position-authority suppression.
        /// The float is the resolved source suppression in the range 0..1.
        /// </summary>
        public event Action<RagdollCollisionEvent, float> CollisionUnpinApplied;

        /// <summary>
        /// Raised after a queued Kinematic contact has safely switched the global
        /// simulation controller back to Active.
        /// </summary>
        public event Action<RagdollPuppetKinematicActivationSource, float>
            KinematicActivated;

        /// <summary>
        /// Changes the balanced Puppet policy. Immediate changes skip the mapping blend and
        /// request the corresponding stable simulation mode immediately when possible.
        /// </summary>
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
        /// Starts GetUp after delay, hip-velocity, stable-ground and orientation checks pass.
        /// </summary>
        public bool TryBeginGetUp()
        {
            if (!CanBeginGetUp) return false;

            RagdollGroundingSnapshot grounding = Grounding;
            return BeginGetUp(
                ResolveGetUpOrientation(grounding.GroundNormal),
                grounding.GroundNormal);
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

            stateMachine = new RagdollPuppetStateMachine();
            groundProbe = new RagdollGroundProbe(Context);
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
        }

        protected override void OnBehaviourActivated()
        {
            stateMachine.Reset(RagdollPuppetState.Puppet);
            groundProbe.Reset();
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            targetAlignmentPending = false;
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
            ApplyRecoveryConfiguration();
            ResetCollisionProcessing(true);
        }

        protected override void OnBehaviourDeactivated()
        {
            Context.Muscles.ClearPositionSuppressionRecoveryMultiplier();
            ReleaseKinematicSimulationMode(true);

            if (stateMachine != null)
            {
                stateMachine.Reset(RagdollPuppetState.Puppet);
            }

            if (groundProbe != null)
            {
                groundProbe.Reset();
            }

            lastKnockOutBone = RagdollBoneHandle.Invalid;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            targetAlignmentPending = false;
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

            float positionSuppression = ApplyCollisionUnpin(
                collisionEvent,
                layerResolution);
            CollisionAccepted?.Invoke(collisionEvent);
            if (positionSuppression > 0f)
            {
                CollisionUnpinApplied?.Invoke(
                    collisionEvent,
                    positionSuppression);
            }
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            ApplyRecoveryConfiguration();

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

            groundProbe.Update(
                deltaTime,
                groundLayers,
                groundProbeStartOffset,
                groundProbeDistance,
                maximumGroundAngle);

            // Alignment is applied in OnModifyTargetPose later in this same animator tick.
            // Do not complete a zero-duration GetUp before that one-shot correction occurs.
            if (State != RagdollPuppetState.GetUp || !targetAlignmentPending)
            {
                RagdollPuppetState previous = State;
                if (stateMachine.Advance(deltaTime, blendToAnimationTime))
                {
                    getUpOrientation = RagdollGetUpOrientation.Unknown;
                    RaiseStateChanged(
                        previous,
                        RagdollPuppetState.Puppet,
                        RagdollPuppetTransitionReason.GetUpCompleted);
                }
            }

            if (automaticGetUp
                && State == RagdollPuppetState.Unpinned
                && TryBeginGetUp())
            {
                UpdateKinematicSimulationMode();
                return;
            }

            if (!loseBalanceOnTargetDrift
                || State == RagdollPuppetState.Unpinned
                || targetAlignmentPending)
            {
                UpdateKinematicSimulationMode();
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
        }

        protected override void OnModifyTargetPose(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs)
        {
            if (!targetAlignmentPending || State != RagdollPuppetState.GetUp)
            {
                return;
            }

            ApplyTargetRootAlignment(pairs);
            targetAlignmentPending = false;
        }

        protected override void OnModifyBoneProfile(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    State,
                    GetUpProgress,
                    unpinnedMuscleWeightMultiplier);

            float rotationAuthority = weights.RotationAuthority;
            if (State == RagdollPuppetState.Puppet)
            {
                float effectivePinAuthority =
                    Context.Muscles.GetEffectivePositionAuthority(pair.Handle);
                rotationAuthority *=
                    RagdollMuscleRecoveryMath.ResolveRelativeMuscleWeight(
                        effectivePinAuthority,
                        muscleWeightRelativeToPinWeight);
            }

            boneProfile.positionAlpha *= weights.PositionAuthority;
            boneProfile.rotationAlpha *= rotationAuthority;
        }

        protected override void OnModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
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
            KinematicActivated?.Invoke(source, impulse);
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
            float targetSpeed = pair.poseLinearVelocity.magnitude;
            float globalResistance = collisionResistance.Evaluate(targetSpeed);
            float muscleResistance = Context.Muscles
                .GetBehaviourSettings(collisionEvent.Bone)
                .collisionResistance;
            float effectiveResistance =
                RagdollPuppetCollisionResponseMath.EvaluateEffectiveResistance(
                    globalResistance,
                    layerResolution.ResistanceMultiplier,
                    muscleResistance);
            float positionSuppression =
                RagdollPuppetCollisionResponseMath.EvaluatePositionSuppression(
                    collisionEvent.ImpulseMagnitude,
                    globalResistance,
                    layerResolution.ResistanceMultiplier,
                    muscleResistance);

            lastCollisionResponse =
                new RagdollPuppetCollisionResponseSnapshot(
                    true,
                    collisionEvent.Bone,
                    collisionEvent.FixedTime,
                    collisionEvent.ImpulseMagnitude,
                    targetSpeed,
                    globalResistance,
                    layerResolution.ResistanceMultiplier,
                    muscleResistance,
                    effectiveResistance,
                    positionSuppression,
                    layerResolution.RuleIndex);

            if (positionSuppression <= 0f) return 0f;

            MuscleImpactSettings impact = new MuscleImpactSettings
            {
                positionSuppression = positionSuppression,
                rotationSuppression = 0f,
                maximumPropagationDistance =
                    Mathf.Max(0, Context.Bindings.BoneCount - 1),
                propagationFalloff = 1f
            };
            Context.Muscles.ApplyResolvedImpact(collisionEvent.Bone, impact);
            return positionSuppression;
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

            getUpOrientation = orientation;
            preparedGroundNormal = groundNormal.sqrMagnitude > Mathf.Epsilon
                ? groundNormal.normalized
                : GetWorldUp();
            targetAlignmentPending = true;
            GetUpPoseSelected?.Invoke(orientation);
            return true;
        }

        RagdollGetUpOrientation ResolveGetUpOrientation(Vector3 groundNormal)
        {
            RagdollGetUpOrientation classified =
                RagdollGetUpAlignmentMath.Classify(
                    rootPair.RagdollBone.Transform.rotation,
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
                rootPair.TargetBone.position,
                rootPair.RagdollBone.Transform.position,
                rootPair.RagdollBone.Transform.rotation,
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

        bool TryFindKnockOutBone(out RagdollBoneHandle knockOutBone)
        {
            float stateDistanceMultiplier =
                State == RagdollPuppetState.GetUp
                    ? Mathf.Max(0f, getUpKnockOutDistanceMultiplier)
                    : 1f;

            for (int index = 0; index < Context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = Context.Pairs[index];
                MuscleRuntimeState muscleState =
                    Context.Muscles.GetState(pair.Handle);
                RagdollMuscleBehaviourSettings settings =
                    Context.Muscles.GetBehaviourSettings(pair.Handle);

                float targetDistance = Vector3.Distance(
                    pair.RagdollBone.Transform.position,
                    pair.currentPose.worldPosition);

                if (!RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    targetDistance,
                    settings.knockOutDistance,
                    muscleState.EffectivePositionAuthority,
                    pinWeightThreshold,
                    stateDistanceMultiplier))
                {
                    continue;
                }

                knockOutBone = pair.Handle;
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
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            lastKnockOutBone = source;
            return true;
        }

        bool TransitionTo(
            RagdollPuppetState next,
            RagdollPuppetTransitionReason reason)
        {
            if (stateMachine == null) return false;

            RagdollPuppetState previous = stateMachine.State;
            if (!stateMachine.TryTransition(next))
            {
                return false;
            }

            RaiseStateChanged(previous, next, reason);
            return true;
        }

        void RaiseStateChanged(
            RagdollPuppetState previous,
            RagdollPuppetState current,
            RagdollPuppetTransitionReason reason)
        {
            StateChanged?.Invoke(previous, current, reason);
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

        void ApplyRecoveryConfiguration()
        {
            if (!IsInitialized) return;

            Context.Muscles.SetPositionSuppressionRecoveryMultiplier(
                regainPinSpeed);
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
            if (groundProbe != null) return groundProbe.Up;

            Vector3 gravity = Physics.gravity;
            return gravity.sqrMagnitude > Mathf.Epsilon
                ? -gravity.normalized
                : Vector3.up;
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
            minimumGroundedTime = Mathf.Max(0f, minimumGroundedTime);
            getUpDelay = Mathf.Max(0f, getUpDelay);
            blendToAnimationTime = Mathf.Max(0f, blendToAnimationTime);
            maximumGetUpVelocity = Mathf.Max(0f, maximumGetUpVelocity);
            getUpKnockOutDistanceMultiplier =
                Mathf.Max(0f, getUpKnockOutDistanceMultiplier);
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
