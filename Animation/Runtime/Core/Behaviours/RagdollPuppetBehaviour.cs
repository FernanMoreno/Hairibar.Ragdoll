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
        RagdollAnimator.AnimatedPair rootPair;
        RagdollBoneHandle lastKnockOutBone = RagdollBoneHandle.Invalid;
        RagdollGetUpOrientation getUpOrientation = RagdollGetUpOrientation.Unknown;
        Vector3 preparedGroundNormal = Vector3.up;
        bool targetAlignmentPending;

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
            stateMachine = new RagdollPuppetStateMachine();
            groundProbe = new RagdollGroundProbe(Context);
            rootPair = FindRootPair();
        }

        protected override void OnBehaviourActivated()
        {
            stateMachine.Reset(RagdollPuppetState.Puppet);
            groundProbe.Reset();
            lastKnockOutBone = RagdollBoneHandle.Invalid;
            getUpOrientation = RagdollGetUpOrientation.Unknown;
            targetAlignmentPending = false;
        }

        protected override void OnBehaviourDeactivated()
        {
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
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
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
                return;
            }

            if (!loseBalanceOnTargetDrift
                || State == RagdollPuppetState.Unpinned
                || targetAlignmentPending)
            {
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

            boneProfile.positionAlpha *= weights.PositionAuthority;
            boneProfile.rotationAlpha *= weights.RotationAuthority;
        }

        protected override void OnModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
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
