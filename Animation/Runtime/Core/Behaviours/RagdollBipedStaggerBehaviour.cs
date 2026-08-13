using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Skeleton biped balance monitor, following PuppetMaster's documented split
    /// between BehaviourPuppet (fall/unpin/get-up) and a separate biped stagger
    /// behaviour for in-place balance recovery. It classifies whether the
    /// capture point (center of mass projected along its own velocity, inverted-
    /// pendulum approximation) has left the foot-to-foot support base, using its
    /// own RagdollCenterOfMassSubBehaviour instance -- the same reusable module
    /// RagdollPuppetBehaviour already uses for grounding/COM.
    ///
    /// This behaviour does not move anything: no foot targeting, no IK, no
    /// Animator calls, and nothing here switches the controller between this and
    /// RagdollPuppetBehaviour. A future stagger implementation would drive a
    /// procedural or clip-based recovery step while RequiresStep holds, call
    /// Deactivate() (returning to Puppet) once Stable, and defer to Puppet's own
    /// Unpinned/GetUp when Unrecoverable persists. Wiring that trigger, and the
    /// actual foot movement, is out of scope for this change.
    /// </summary>
    [AddComponentMenu("Ragdoll/Behaviours/Ragdoll Biped Balance Behaviour")]
    public sealed class RagdollBipedBalanceBehaviour : RagdollBehaviourBase
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

        [SerializeField] RagdollCenterOfMassSubBehaviour centerOfMass =
            new RagdollCenterOfMassSubBehaviour();

        public RagdollBipedBalanceState CurrentState { get; private set; } =
            RagdollBipedBalanceState.Stable;
        public float LastSignedSupportMargin { get; private set; }
        public event Action<RagdollBipedBalanceState, RagdollBipedBalanceState> BalanceStateChanged;

        protected override void OnBehaviourInitialize()
        {
            centerOfMass.Configure(
                groundLayers, probeStartOffset, probeDistance, maximumGroundAngle);
            centerOfMass.Initialize(this);
        }

        protected override void OnBehaviourActivated()
        {
            centerOfMass.SetActive(true);
            centerOfMass.Reset();
            CurrentState = RagdollBipedBalanceState.Stable;
            LastSignedSupportMargin = 0f;
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
    }
}
