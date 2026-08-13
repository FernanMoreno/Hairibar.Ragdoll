using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Biped stagger behaviour, following PuppetMaster's documented split between
    /// BehaviourPuppet (fall/unpin/get-up) and a separate biped stagger behaviour
    /// for in-place balance recovery (RootMotion's BehaviourBipedStagger). It
    /// classifies whether the capture point (center of mass projected along its
    /// own velocity, inverted-pendulum approximation) has left the foot-to-foot
    /// support base, using its own RagdollCenterOfMassSubBehaviour instance --
    /// the same reusable module RagdollPuppetBehaviour already uses for
    /// grounding/COM.
    /// </summary>
    [AddComponentMenu("Ragdoll/Behaviours/Ragdoll Biped Stagger Behaviour")]
    public sealed class RagdollBipedStaggerBehaviour : RagdollBehaviourBase
    {
        [Header("Center of Mass")]
        [SerializeField] LayerMask groundLayers = -1;
        [SerializeField, Min(0f)] float probeStartOffset = 0.1f;
        [SerializeField, Min(0f)] float probeDistance = 1f;
        [SerializeField, Range(0f, 89.9f)] float maximumGroundAngle = 60f;

        [Header("Feet")]
        [SerializeField] BoneName leftFootBone = "foot_l";
        [SerializeField] BoneName rightFootBone = "foot_r";

        [Header("Capture Point")]
        [Tooltip("Effective inverted-pendulum height used by the capture-point projection.")]
        [SerializeField, Min(0.01f)] float pendulumLength = 0.9f;
        [Tooltip("Radius around the foot-to-foot segment still considered supported.")]
        [SerializeField, Min(0f)] float supportRadius = 0.15f;
        [Tooltip("Signed margin above which balance is Stable (comfortably inside support).")]
        [SerializeField, Min(0f)] float stableMargin = 0.05f;
        [Tooltip("How far outside support the capture point may go before Unrecoverable.")]
        [SerializeField, Min(0f)] float requiresStepMargin = 0.25f;

        [Header("Step Cycle")]
        [Tooltip("Maximum recovery steps attempted before giving up (LoseBalance).")]
        [SerializeField, Min(1)] int maxSteps = 2;
        [SerializeField, Min(0f)] float liftOffDuration = 0.08f;
        [SerializeField, Min(0f)] float swingDuration = 0.18f;
        [SerializeField, Min(0f)] float replantDuration = 0.08f;
        [SerializeField, Min(0f)] float settlingDuration = 0.12f;
        [Tooltip("Position pin weight multiplier applied to the swinging foot during Swing/Replant, letting physics drive the step.")]
        [SerializeField, Range(0f, 1f)] float swingPositionPinWeight = 0.2f;

        [Header("Animation")]
        [SerializeField] string forwardStateName = "StepForward";
        [SerializeField] string backwardStateName = "StepBackward";
        [SerializeField] string leftStateName = "StepLeft";
        [SerializeField] string rightStateName = "StepRight";
        [SerializeField, Min(0f)] float transitionDuration = 0.1f;
        [SerializeField] int animatorLayer = -1;
        [Tooltip("Animator int parameter set to 0 (left) or 1 (right) right before the crossfade, so the controller can mirror/branch the clip to the physically-selected swing foot. Leave empty to skip -- the 4 clips above must then already commit to one leading foot.")]
        [SerializeField] string swingFootParameterName = "StepSwingFoot";

        [Header("Recovery")]
        [Tooltip("Sibling RagdollPuppetBehaviour reactivated on both success and failure. Auto-resolved from this GameObject when left empty.")]
        [SerializeField] RagdollPuppetBehaviour puppet;

        [SerializeField] RagdollCenterOfMassSubBehaviour centerOfMass =
            new RagdollCenterOfMassSubBehaviour();
        readonly RagdollBipedStaggerStateMachine stepMachine =
            new RagdollBipedStaggerStateMachine();
        RagdollBipedStepFoot swingFoot;
        BoneName swingFootBone;
        bool pendingFirstClassification;

        public RagdollBipedBalanceState CurrentState { get; private set; } =
            RagdollBipedBalanceState.Stable;
        public float LastSignedSupportMargin { get; private set; }
        public event Action<RagdollBipedBalanceState, RagdollBipedBalanceState> BalanceStateChanged;

        public float PendulumLength
        {
            get => pendulumLength;
            set => pendulumLength = Mathf.Max(0.01f, value);
        }
        public float SupportRadius
        {
            get => supportRadius;
            set => supportRadius = Mathf.Max(0f, value);
        }
        public float StableMargin
        {
            get => stableMargin;
            set => stableMargin = Mathf.Max(0f, value);
        }
        public float RequiresStepMargin
        {
            get => requiresStepMargin;
            set => requiresStepMargin = Mathf.Max(0f, value);
        }
        public int MaxSteps
        {
            get => maxSteps;
            set => maxSteps = Mathf.Max(1, value);
        }

        protected override void OnBehaviourInitialize()
        {
            centerOfMass.Configure(
                groundLayers, probeStartOffset, probeDistance, maximumGroundAngle);
            centerOfMass.Initialize(this);
            if (!puppet) puppet = GetComponent<RagdollPuppetBehaviour>();
        }

        protected override void OnBehaviourActivated()
        {
            centerOfMass.SetActive(true);
            centerOfMass.Reset();
            CurrentState = RagdollBipedBalanceState.Stable;
            LastSignedSupportMargin = 0f;
            stepMachine.Reset();
            // Do not classify/BeginStep here: centerOfMass has not run a single
            // FixedUpdate yet, so its Snapshot is still Empty (zero COM/velocity).
            // Deciding a step from that would pick a foot/direction blind. Defer
            // to the first real OnBehaviourFixedUpdate, which runs the probe
            // before classifying.
            pendingFirstClassification = true;
        }

        protected override void OnBehaviourDeactivated()
        {
            centerOfMass.SetActive(false);
        }

        protected override void OnBehaviourShutdown()
        {
            centerOfMass.Shutdown();
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            centerOfMass.FixedUpdate(deltaTime);
            UpdateBalanceClassification();

            // Unrecoverable aborts the moment it is observed, mid-cycle or not --
            // finishing a visual LiftOff/Swing/Replant/Settling the physics has
            // already given up on would be lying to the player.
            if (CurrentState == RagdollBipedBalanceState.Unrecoverable)
            {
                Recover(succeeded: false);
                return;
            }

            if (pendingFirstClassification)
            {
                pendingFirstClassification = false;
                if (CurrentState == RagdollBipedBalanceState.Stable
                    || CurrentState == RagdollBipedBalanceState.RecoverableWithoutStep)
                {
                    // Already balanced by the time the real capture point could
                    // be read -- no step needed after all.
                    Recover(succeeded: true);
                    return;
                }
                BeginStep();
                return;
            }

            if (stepMachine.State == RagdollBipedStaggerState.Failed)
            {
                Recover(succeeded: false);
                return;
            }

            bool completedPhase = stepMachine.Advance(
                deltaTime, liftOffDuration, swingDuration, replantDuration, settlingDuration);
            if (!completedPhase || stepMachine.State != RagdollBipedStaggerState.Idle) return;

            RagdollBipedStaggerOutcome outcome = RagdollBipedStaggerMath.ResolveOutcome(
                CurrentState, stepMachine.StepCount, maxSteps);
            switch (outcome)
            {
                case RagdollBipedStaggerOutcome.Succeeded:
                    Recover(succeeded: true);
                    break;
                case RagdollBipedStaggerOutcome.Continue:
                    BeginStep();
                    break;
                default:
                    Recover(succeeded: false);
                    break;
            }
        }

        protected override void OnModifyBoneProfile(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
            bool swinging = stepMachine.State == RagdollBipedStaggerState.Swing
                || stepMachine.State == RagdollBipedStaggerState.Replant;
            if (swinging && pair.Name.Equals(swingFootBone))
            {
                boneProfile.MultiplyPositionPinWeight(swingPositionPinWeight);
            }
        }

        void UpdateBalanceClassification()
        {
            if (!Context.Bindings.TryGetBone(leftFootBone, out RagdollBone leftFoot)
                || !Context.Bindings.TryGetBone(rightFootBone, out RagdollBone rightFoot)
                || leftFoot.Rigidbody == null || rightFoot.Rigidbody == null)
            {
                return;
            }

            RagdollGroundingSnapshot grounding = centerOfMass.Snapshot;
            float margin = RagdollBipedBalanceMath.SignedCaptureMargin(
                grounding.CenterOfMass,
                grounding.CenterOfMassVelocity,
                leftFoot.Rigidbody.position,
                rightFoot.Rigidbody.position,
                pendulumLength,
                Physics.gravity.magnitude,
                supportRadius);
            LastSignedSupportMargin = margin;

            RagdollBipedBalanceState nextState = RagdollBipedBalanceMath.Classify(
                margin, stableMargin, requiresStepMargin);
            if (nextState == CurrentState) return;

            RagdollBipedBalanceState previous = CurrentState;
            CurrentState = nextState;
            BalanceStateChanged?.Invoke(previous, nextState);
        }

        void BeginStep()
        {
            if (!stepMachine.TryBeginStep(maxSteps)) return;

            if (!Context.Bindings.TryGetBone(leftFootBone, out RagdollBone leftFoot)
                || !Context.Bindings.TryGetBone(rightFootBone, out RagdollBone rightFoot)
                || leftFoot.Rigidbody == null || rightFoot.Rigidbody == null)
            {
                stepMachine.RegisterStepFailed();
                return;
            }

            RagdollGroundingSnapshot grounding = centerOfMass.Snapshot;
            Vector3 capturePoint = RagdollBipedBalanceMath.CapturePoint(
                grounding.CenterOfMass,
                grounding.CenterOfMassVelocity,
                pendulumLength,
                Physics.gravity.magnitude);
            swingFoot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint, leftFoot.Rigidbody.position, rightFoot.Rigidbody.position);
            swingFootBone = swingFoot == RagdollBipedStepFoot.Left
                ? leftFootBone
                : rightFootBone;

            Vector3 stanceFootPosition = swingFoot == RagdollBipedStepFoot.Left
                ? rightFoot.Rigidbody.position
                : leftFoot.Rigidbody.position;
            Vector3 offset = capturePoint - stanceFootPosition;
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset, transform.forward, transform.up);

            if (!TryCrossFadeStep(direction))
            {
                // Nothing will animate the step -- fail the cycle now rather
                // than let the state machine run its timers on an Animator
                // that never actually moved.
                stepMachine.RegisterStepFailed();
            }
        }

        bool TryCrossFadeStep(RagdollBipedStepDirection direction)
        {
            Animator animator = Context != null && Context.Animator
                ? Context.Animator.TargetAnimator
                : null;
            if (!animator) return false;

            string stateName = ResolveStepStateName(direction);
            if (string.IsNullOrEmpty(stateName)) return false;
            if (!animator.HasState(Mathf.Max(0, animatorLayer), Animator.StringToHash(stateName)))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(swingFootParameterName))
            {
                animator.SetInteger(
                    swingFootParameterName, swingFoot == RagdollBipedStepFoot.Left ? 0 : 1);
            }
            animator.CrossFadeInFixedTime(
                stateName, transitionDuration, animatorLayer);
            return true;
        }

        string ResolveStepStateName(RagdollBipedStepDirection direction)
        {
            switch (direction)
            {
                case RagdollBipedStepDirection.Forward: return forwardStateName;
                case RagdollBipedStepDirection.Backward: return backwardStateName;
                case RagdollBipedStepDirection.Left: return leftStateName;
                default: return rightStateName;
            }
        }

        void Recover(bool succeeded)
        {
            stepMachine.Reset();
            if (!puppet || Controller == null) return;

            Controller.Activate<RagdollPuppetBehaviour>();
            if (!succeeded)
            {
                puppet.Unpin();
            }
        }
    }
}
