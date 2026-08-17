using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Hairibar biped stagger behaviour, following the documented semantic split
    /// between BehaviourPuppet (fall/unpin/get-up) and a separate biped stagger
    /// concept for in-place balance recovery. The recovered Doxygen corpus does
    /// not establish a detail-level RootMotion BehaviourBipedStagger contract.
    /// It classifies whether the capture point (center of mass projected along its
    /// own velocity, inverted-pendulum approximation) has left the current
    /// contact-backed support base, using its own RagdollCenterOfMassSubBehaviour instance --
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
        [Tooltip("Radius around the contact-backed support region still considered supported.")]
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
        [Tooltip("Normalized progress at which the selected step Animator may hand off Swing to Replant when contact has not arrived yet.")]
        [SerializeField, Range(0f, 1f)] float swingAnimatorReplantProgress = 0.75f;
        [Tooltip("Continuous Stable/RecoverableWithoutStep window required during Settling before normal completion.")]
        [SerializeField, Min(0f)] float minimumSettlingStableDuration = 0.04f;
        [Tooltip("Optional suffix for an explicit right-foot branch, appended to the directional state name when that state exists (for example StepLeftRightFoot). If absent, StepSwingFoot parameter branching remains the fallback.")]
        [SerializeField] string rightFootStateSuffix = "RightFoot";
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
        RagdollGroundingSnapshot activationSnapshot =
            RagdollGroundingSnapshot.Empty;
        RagdollGroundingSnapshot lastClassificationSnapshot =
            RagdollGroundingSnapshot.Empty;
        bool hasActivationSnapshot;
        bool hasClassificationSnapshot;
        bool pendingFirstClassification;
        bool stepBalanceLatched;
        int stepAnimatorLayer = -1;
        int stepAnimatorStateHash;

        public RagdollBipedBalanceState CurrentState { get; private set; } =
            RagdollBipedBalanceState.Stable;
        public float LastSignedSupportMargin { get; private set; }
        public Vector3 LastCapturePoint { get; private set; }
        public string CurrentPhase => stepMachine.State.ToString();
        public int StepCount => stepMachine.StepCount;
        public bool SwingFootAvailable => stepMachine.StepCount > 0;
        public string SwingFootName => SwingFootAvailable ? swingFoot.ToString() : "Unavailable";
        internal RagdollGroundingSnapshot LastClassificationSnapshot =>
            lastClassificationSnapshot;
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
            centerOfMass.ConfigureSupportFeet(leftFootBone, rightFootBone);
            if (!puppet) puppet = GetComponent<RagdollPuppetBehaviour>();
        }

        protected override void OnBehaviourCollision(
            RagdollCollisionEvent collisionEvent)
        {
            centerOfMass.RegisterCollision(collisionEvent);
        }

        protected override void OnBehaviourActivated()
        {
            centerOfMass.SetActive(true);
            centerOfMass.Reset();
            CurrentState = RagdollBipedBalanceState.Stable;
            LastSignedSupportMargin = 0f;
            LastCapturePoint = Vector3.zero;
            stepMachine.Reset();
            hasActivationSnapshot = puppet != null
                && puppet.TryConsumeStaggerSnapshot(out activationSnapshot);
            hasClassificationSnapshot = false;
            lastClassificationSnapshot = RagdollGroundingSnapshot.Empty;
            stepBalanceLatched = false;
            stepAnimatorLayer = -1;
            stepAnimatorStateHash = 0;
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
            hasActivationSnapshot = false;
            hasClassificationSnapshot = false;
            stepAnimatorLayer = -1;
            stepAnimatorStateHash = 0;
        }

        protected override void OnBehaviourShutdown()
        {
            centerOfMass.Shutdown();
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            centerOfMass.FixedUpdate(deltaTime);

            // Behaviour activation can happen before the first collision callback
            // for the newly-active stream. Do not interpret that one empty sample
            // as a physical loss of support; the next fixed tick is the bounded
            // opportunity for Enter/Stay contacts to populate the snapshot. A
            // genuinely contact-free rig still reaches Unrecoverable on the next
            // tick, so this is initialization ordering protection, not support
            // synthesis or a stale-foot fallback.
            if (pendingFirstClassification
                && centerOfMass.Snapshot.TotalMass > Mathf.Epsilon
                && centerOfMass.Snapshot.ContactBackedSupportPointCount == 0)
            {
                return;
            }
            if (!UpdateBalanceClassification()) return;

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
                deltaTime,
                liftOffDuration,
                swingDuration,
                replantDuration,
                settlingDuration,
                BuildPhaseSignals(),
                swingAnimatorReplantProgress,
                minimumSettlingStableDuration);
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

        bool UpdateBalanceClassification()
        {
            RagdollGroundingSnapshot grounding;
            if (hasActivationSnapshot)
            {
                grounding = activationSnapshot;
                hasActivationSnapshot = false;
            }
            else
            {
                grounding = centerOfMass.Snapshot;
            }
            if (grounding.TotalMass <= Mathf.Epsilon) return false;

            lastClassificationSnapshot = grounding;
            hasClassificationSnapshot = true;
            Vector3 supportUp = grounding.EffectiveUp;
            Vector3 centerOfMassVelocity = grounding.HasRelativeMotion
                ? grounding.RelativeCenterOfMassVelocity
                : grounding.CenterOfMassVelocity;
            Vector3 capturePoint = RagdollBipedBalanceMath.CapturePoint(
                grounding.CenterOfMass,
                centerOfMassVelocity,
                pendulumLength,
                Physics.gravity.magnitude,
                supportUp);
            LastCapturePoint = capturePoint;
            float margin = RagdollBipedBalanceMath.SignedSupportMargin(
                point: capturePoint,
                hasLeftFootSupport: grounding.HasLeftFootSupport,
                leftFoot: grounding.LeftFootSupportPoint,
                hasRightFootSupport: grounding.HasRightFootSupport,
                rightFoot: grounding.RightFootSupportPoint,
                supportRadius: supportRadius,
                supportUp: supportUp);
            LastSignedSupportMargin = margin;

            RagdollBipedBalanceState nextState = RagdollBipedBalanceMath.Classify(
                margin,
                grounding.ContactBackedSupportPointCount,
                stableMargin,
                requiresStepMargin);
            // Once a step has started, do not let a transient zero-support
            // interval abort the episode as Unrecoverable. Continue observing
            // valid Stable/RecoverableWithoutStep samples so Settling can use
            // an actual stable window rather than a timer-only transition.
            if (stepMachine.State != RagdollBipedStaggerState.Idle
                && stepBalanceLatched
                && nextState == RagdollBipedBalanceState.Unrecoverable)
            {
                return true;
            }
            if (stepMachine.State != RagdollBipedStaggerState.Idle)
            {
                stepBalanceLatched = true;
            }
            if (nextState == CurrentState) return true;

            RagdollBipedBalanceState previous = CurrentState;
            CurrentState = nextState;
            BalanceStateChanged?.Invoke(previous, nextState);
            return true;
        }

        void BeginStep()
        {
            if (!stepMachine.TryBeginStep(maxSteps)) return;
            stepBalanceLatched = false;

            if (!Context.Bindings.TryGetBone(leftFootBone, out RagdollBone leftFoot)
                || !Context.Bindings.TryGetBone(rightFootBone, out RagdollBone rightFoot))
            {
                stepMachine.RegisterStepFailed();
                return;
            }

            RagdollGroundingSnapshot grounding = hasClassificationSnapshot
                ? lastClassificationSnapshot
                : centerOfMass.Snapshot;
            if (grounding.TotalMass <= Mathf.Epsilon)
            {
                stepMachine.RegisterStepFailed();
                return;
            }
            Vector3 supportUp = grounding.EffectiveUp;
            Vector3 centerOfMassVelocity = grounding.HasRelativeMotion
                ? grounding.RelativeCenterOfMassVelocity
                : grounding.CenterOfMassVelocity;
            Vector3 capturePoint = RagdollBipedBalanceMath.CapturePoint(
                grounding.CenterOfMass,
                centerOfMassVelocity,
                pendulumLength,
                Physics.gravity.magnitude,
                supportUp);
            swingFoot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint,
                grounding.HasLeftFootSupport,
                grounding.LeftFootSupportPoint,
                grounding.HasRightFootSupport,
                grounding.RightFootSupportPoint,
                supportUp);
            swingFootBone = swingFoot == RagdollBipedStepFoot.Left
                ? leftFootBone
                : rightFootBone;

            Vector3 stanceFootPosition;
            if (swingFoot == RagdollBipedStepFoot.Left)
            {
                stanceFootPosition = grounding.HasRightFootSupport
                    ? grounding.RightFootSupportPoint
                    : grounding.LeftFootSupportPoint;
            }
            else
            {
                stanceFootPosition = grounding.HasLeftFootSupport
                    ? grounding.LeftFootSupportPoint
                    : grounding.RightFootSupportPoint;
            }
            Vector3 offset = capturePoint - stanceFootPosition;
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset, transform.forward, supportUp);

            if (!TryCrossFadeStep(direction))
            {
                // Nothing will animate the step -- fail the cycle now rather
                // than let the state machine run its timers on an Animator
                // that never actually moved.
                stepMachine.RegisterStepFailed();
            }
        }

        RagdollBipedStaggerPhaseSignals BuildPhaseSignals()
        {
            RagdollGroundingSnapshot grounding = hasClassificationSnapshot
                ? lastClassificationSnapshot
                : centerOfMass.Snapshot;
            bool selectedFootGrounded = swingFoot == RagdollBipedStepFoot.Left
                ? grounding.HasLeftFootSupport
                : grounding.HasRightFootSupport;

            bool animatorStateAvailable = false;
            float animatorNormalizedTime = 0f;
            Animator animator = Context != null && Context.Animator
                ? Context.Animator.TargetAnimator
                : null;
            if (animator && stepAnimatorLayer >= 0
                && stepAnimatorLayer < animator.layerCount
                && stepAnimatorStateHash != 0)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(
                    stepAnimatorLayer);
                animatorStateAvailable = stateInfo.fullPathHash == stepAnimatorStateHash;
                if (animatorStateAvailable)
                    animatorNormalizedTime = stateInfo.normalizedTime;
            }

            bool balanceRecovered = CurrentState == RagdollBipedBalanceState.Stable
                || CurrentState == RagdollBipedBalanceState.RecoverableWithoutStep;
            return new RagdollBipedStaggerPhaseSignals(
                selectedFootGrounded,
                animatorStateAvailable,
                animatorNormalizedTime,
                balanceRecovered);
        }

        bool TryCrossFadeStep(RagdollBipedStepDirection direction)
        {
            Animator animator = Context != null && Context.Animator
                ? Context.Animator.TargetAnimator
                : null;
            if (!animator) return false;

            string stateName = ResolveStepStateName(direction);
            if (string.IsNullOrEmpty(stateName)) return false;
            int stateHash = Animator.StringToHash(stateName);
            int layer = ResolveStepStateLayer(animator, stateHash);
            if (layer < 0) return false;

            if (!string.IsNullOrEmpty(swingFootParameterName))
            {
                if (!HasIntegerParameter(animator, swingFootParameterName)) return false;
                animator.SetInteger(
                    swingFootParameterName, swingFoot == RagdollBipedStepFoot.Left ? 0 : 1);
            }

            // A parameter-driven transition can be evaluated from the previous
            // Animator state during the same fixed tick as CrossFade. When an
            // explicit right-foot branch exists, target it directly so the
            // physical selection and the clip branch cannot diverge. Controllers
            // without this naming convention retain the StepSwingFoot fallback.
            if (swingFoot == RagdollBipedStepFoot.Right
                && !string.IsNullOrEmpty(rightFootStateSuffix))
            {
                string explicitRightState = stateName + rightFootStateSuffix;
                int explicitRightHash = Animator.StringToHash(explicitRightState);
                int explicitRightLayer = ResolveStepStateLayer(
                    animator, explicitRightHash);
                if (explicitRightLayer < 0)
                {
                    for (int candidateLayer = 0;
                        candidateLayer < animator.layerCount;
                        candidateLayer++)
                    {
                        int fullPathHash = Animator.StringToHash(
                            animator.GetLayerName(candidateLayer) + "." +
                            explicitRightState);
                        if (!animator.HasState(candidateLayer, fullPathHash))
                            continue;

                        explicitRightHash = fullPathHash;
                        explicitRightLayer = candidateLayer;
                        break;
                    }
                }
                if (explicitRightLayer >= 0)
                {
                    stateName = explicitRightState;
                    stateHash = explicitRightHash;
                    layer = explicitRightLayer;
                }
            }
            CrossFadeStepState(animator, stateName, transitionDuration, layer);
            stepAnimatorLayer = layer;
            stepAnimatorStateHash = Animator.StringToHash(
                animator.GetLayerName(layer) + "." + stateName);
            return true;
        }

        internal static void CrossFadeStepState(
            Animator animator, string stateName, float duration, int layer)
        {
            string fullPath = animator.GetLayerName(layer) + "." + stateName;
            int fullPathHash = Animator.StringToHash(fullPath);
            if (animator.HasState(layer, fullPathHash))
            {
                animator.CrossFadeInFixedTime(fullPath, duration, layer);
                return;
            }

            // Some imported controllers expose only the short state name.
            // Preserve that documented overload as a compatibility fallback,
            // but prefer the layer-qualified path whenever HasState confirms it.
            animator.CrossFadeInFixedTime(stateName, duration, layer);
        }

        internal static void CrossFadeStepState(
            Animator animator, int stateHash, float duration, int layer)
        {
            animator.CrossFadeInFixedTime(stateHash, duration, layer);
        }

        internal static bool HasIntegerParameter(Animator animator, string parameterName)
        {
            if (!animator || string.IsNullOrEmpty(parameterName)) return false;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].name == parameterName)
                    return parameters[index].type == AnimatorControllerParameterType.Int;
            }
            return false;
        }

        // Animator.CrossFadeInFixedTime accepts -1 as its default layer, but
        // Animator.HasState only queries one concrete layer. Resolve that
        // default to the layer that actually owns the requested state so the
        // validation and transition always use the same layer.
        internal int ResolveStepStateLayer(Animator animator, int stateHash)
        {
            if (animatorLayer >= 0)
            {
                return animatorLayer < animator.layerCount
                    && animator.HasState(animatorLayer, stateHash)
                    ? animatorLayer
                    : -1;
            }

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.HasState(layer, stateHash)) return layer;
            }
            return -1;
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
