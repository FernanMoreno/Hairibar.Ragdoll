using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Core Puppet/Unpinned/GetUp behaviour. It releases world-space pinning when balance
    /// is lost, keeps reduced rotational muscle strength while unpinned, and progressively
    /// restores pinning during GetUp. Grounding, prone/supine classification and Target
    /// root alignment are intentionally delegated to the next behaviour module.
    /// </summary>
    [AddComponentMenu("Ragdoll/Behaviours/Ragdoll Puppet Behaviour")]
    [DisallowMultipleComponent]
    public sealed class RagdollPuppetBehaviour : RagdollBehaviourBase
    {
        [Header("Losing Balance")]
        [SerializeField] bool loseBalanceOnTargetDrift = true;
        [SerializeField, Range(0f, 1f)] float pinWeightThreshold = 1f;
        [SerializeField, Range(0f, 1f)] float unpinnedMuscleWeightMultiplier = 0.3f;

        [Header("Getting Up")]
        [SerializeField, Min(0f)] float getUpDelay = 1f;
        [SerializeField, Min(0f)] float blendToAnimationTime = 0.5f;
        [SerializeField, Min(0f)] float maximumGetUpVelocity = 0.5f;
        [SerializeField, Min(0f)] float getUpKnockOutDistanceMultiplier = 2f;

        RagdollPuppetStateMachine stateMachine;
        RagdollBoneHandle lastKnockOutBone = RagdollBoneHandle.Invalid;

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

        /// <summary>
        /// True after the configured delay and once the root Rigidbody has slowed down.
        /// Ground validation must be performed by gameplay or the future ground module.
        /// </summary>
        public bool CanBeginGetUp
        {
            get
            {
                if (!IsInitialized || State != RagdollPuppetState.Unpinned)
                {
                    return false;
                }

                return RagdollPuppetBehaviourMath.IsGetUpReady(
                    StateElapsedTime,
                    getUpDelay,
                    Context.Bindings.Root.Rigidbody.velocity.magnitude,
                    maximumGetUpVelocity);
            }
        }

        public event Action<
            RagdollPuppetState,
            RagdollPuppetState,
            RagdollPuppetTransitionReason> StateChanged;

        /// <summary>Moves Puppet or GetUp to Unpinned.</summary>
        public bool LoseBalance()
        {
            return LoseBalance(RagdollBoneHandle.Invalid,
                State == RagdollPuppetState.GetUp
                    ? RagdollPuppetTransitionReason.GetUpInterrupted
                    : RagdollPuppetTransitionReason.Manual);
        }

        /// <summary>
        /// Starts GetUp only after delay and root-velocity readiness requirements pass.
        /// Grounding is intentionally not guessed by this patch.
        /// </summary>
        public bool TryBeginGetUp()
        {
            return CanBeginGetUp
                && TransitionTo(
                    RagdollPuppetState.GetUp,
                    RagdollPuppetTransitionReason.GetUpStarted);
        }

        /// <summary>
        /// Starts GetUp without readiness checks. Intended for a gameplay controller that
        /// has already validated ground contact and pose externally.
        /// </summary>
        public bool BeginGetUpImmediately()
        {
            return TransitionTo(
                RagdollPuppetState.GetUp,
                RagdollPuppetTransitionReason.GetUpStarted);
        }

        /// <summary>Interrupts GetUp and returns to Unpinned.</summary>
        public bool InterruptGetUp()
        {
            return State == RagdollPuppetState.GetUp
                && TransitionTo(
                    RagdollPuppetState.Unpinned,
                    RagdollPuppetTransitionReason.GetUpInterrupted);
        }

        protected override void OnBehaviourInitialize()
        {
            stateMachine = new RagdollPuppetStateMachine();
        }

        protected override void OnBehaviourActivated()
        {
            stateMachine.Reset(RagdollPuppetState.Puppet);
            lastKnockOutBone = RagdollBoneHandle.Invalid;
        }

        protected override void OnBehaviourDeactivated()
        {
            if (stateMachine != null)
            {
                stateMachine.Reset(RagdollPuppetState.Puppet);
            }

            lastKnockOutBone = RagdollBoneHandle.Invalid;
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            RagdollPuppetState previous = State;
            if (stateMachine.Advance(deltaTime, blendToAnimationTime))
            {
                RaiseStateChanged(
                    previous,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetTransitionReason.GetUpCompleted);
            }

            if (!loseBalanceOnTargetDrift
                || State == RagdollPuppetState.Unpinned)
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

        void OnValidate()
        {
            pinWeightThreshold = Mathf.Clamp01(pinWeightThreshold);
            unpinnedMuscleWeightMultiplier =
                Mathf.Clamp01(unpinnedMuscleWeightMultiplier);
            getUpDelay = Mathf.Max(0f, getUpDelay);
            blendToAnimationTime = Mathf.Max(0f, blendToAnimationTime);
            maximumGetUpVelocity = Mathf.Max(0f, maximumGetUpVelocity);
            getUpKnockOutDistanceMultiplier =
                Mathf.Max(0f, getUpKnockOutDistanceMultiplier);
        }
    }
}
