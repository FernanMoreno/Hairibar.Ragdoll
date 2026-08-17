using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// End-to-end physical coverage for the biped stagger actuator: a real
    /// RagdollAnimator, muscle registry, behaviour controller and dynamic
    /// Rigidbodies for a 3-bone (root + two feet) fixture. Feet and root are
    /// frozen so the capture-point classification stays constant across the
    /// whole step cycle, making the actuator's success/failure branch
    /// deterministic without depending on real step physics.
    /// </summary>
    public sealed class RagdollBipedStaggerBehaviourPlayModeTests
    {
        StaggerPhysicalRig rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (rig != null) rig.Dispose();
            rig = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PackageFixture_ProvidesRequiredAnimatorContract()
        {
            RuntimeAnimatorController controller =
                StaggerTestAnimatorFixture.LoadController();
            Assert.That(controller, Is.Not.Null);
            Assert.That(StaggerTestAnimatorFixture.IsValid(controller), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BalancedCaptureMargin_CompletesStepCycleAndReactivatesPuppetAsPuppet()
        {
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f);
            yield return null;
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.25f;
            // Root sits directly between the feet: capture point margin is
            // comfortably inside the support segment -> Stable every frame.

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);

            yield return RunUntilIdleOrTimeout(rig);

            Assert.That(rig.Controller.ActiveBehaviour, Is.InstanceOf<RagdollPuppetBehaviour>());
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
        }

        [UnityTest]
        public IEnumerator UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet()
        {
            // Create feet offset before their joints are created. Teleporting
            // constrained bodies after setup is reverted by PhysX and does
            // not construct an Unrecoverable capture-margin scenario.
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, footCenterX: 5f);
            yield return null;
            // Root remains near origin while authored foot support segment is
            // far right: first classification must be Unrecoverable.
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.1f;
            rig.Stagger.MaxSteps = 1;

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);

            yield return RunUntilIdleOrTimeout(rig);

            Assert.That(rig.Controller.ActiveBehaviour, Is.InstanceOf<RagdollPuppetBehaviour>());
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));
        }

        [UnityTest]
        public IEnumerator StepActuator_CrossFadesStepStateAndRunsStepPhases()
        {
            RuntimeAnimatorController controller = StaggerTestAnimatorFixture.LoadController();
            Assert.That(controller, Is.Not.Null,
                "The package-owned Stagger test fixture could not provide StepRecovery.");
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f,
                footCenterX: 0.5f, stepController: controller);
            yield return new WaitForFixedUpdate();
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.25f;
            StaggerPhysicalRig.SetField(rig.Stagger, "liftOffDuration", 0.04f);
            StaggerPhysicalRig.SetField(rig.Stagger, "swingDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "replantDuration", 0.04f);
            StaggerPhysicalRig.SetField(rig.Stagger, "settlingDuration", 0.04f);
            StaggerPhysicalRig.SetField(rig.Stagger, "transitionDuration", 0f);

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);
            // This frozen actuator fixture validates the directional state
            // handoff only. It intentionally does not certify physical recovery.
            SetProperty(rig.Stagger, "CurrentState", RagdollBipedBalanceState.RequiresStep);
            StaggerPhysicalRig.SetField(
                rig.Stagger, "lastClassificationSnapshot", rig.Puppet.LastStaggerSnapshot);
            StaggerPhysicalRig.SetField(rig.Stagger, "hasClassificationSnapshot", true);
            InvokePrivate(rig.Stagger, "BeginStep");
            Assert.That(rig.Stagger.CurrentState,
                Is.EqualTo(RagdollBipedBalanceState.RequiresStep));
            // This test deliberately isolates the actuator call. Evaluate the
            // queued crossfade directly instead of allowing the production
            // pending-first-classification lifecycle to recover the fixture
            // before the directional handoff is observed.
            rig.TargetAnimator.Update(0f);

            Assert.That(rig.TargetAnimator.GetInteger("StepSwingFoot"), Is.EqualTo(1),
                "The fixture's manually invoked actuator selects its current swing foot.");
            int actuatorStateHash = rig.TargetAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            string actuatorState = DescribeAnimatorState(actuatorStateHash);
            Assert.That(actuatorState,
                Does.EndWith("RightFoot"),
                $"When the explicit right-foot branch exists, the actuator must enter that branch " +
                $"regardless of directional state name (actual={actuatorState}, hash={actuatorStateHash}).");

            // Sample animator directly before the ragdoll mapping pass can
            // reconcile the Target pose. This is actuator evidence only.
            rig.TargetAnimator.Update(0.15f);
            Assert.That(rig.LeftTarget.localPosition.x,
                Is.EqualTo(0f).Within(0.001f),
                "StepSwingFoot=Right must leave LeftTarget as stance foot.");
            Assert.That(rig.RightTarget.localPosition.x,
                Is.LessThan(1f),
                "StepSwingFoot=Right must move RightTarget, regardless of direction state name.");
        }

        [UnityTest]
        public IEnumerator StepActuator_LeftSelectionMovesOnlyLeftTarget()
        {
            RuntimeAnimatorController controller = StaggerTestAnimatorFixture.LoadController();
            Assert.That(controller, Is.Not.Null);
            // Move the support segment left of the COM so the runtime selector
            // deterministically chooses the left swing foot.
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f,
                footCenterX: -0.5f, stepController: controller);
            yield return new WaitForFixedUpdate();
            StaggerPhysicalRig.SetField(rig.Stagger, "transitionDuration", 0f);

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);
            SetProperty(rig.Stagger, "CurrentState", RagdollBipedBalanceState.RequiresStep);
            StaggerPhysicalRig.SetField(
                rig.Stagger, "lastClassificationSnapshot", rig.Puppet.LastStaggerSnapshot);
            StaggerPhysicalRig.SetField(rig.Stagger, "hasClassificationSnapshot", true);
            InvokePrivate(rig.Stagger, "BeginStep");
            float leftTargetStart = rig.LeftTarget.localPosition.x;
            float rightTargetStart = rig.RightTarget.localPosition.x;
            yield return null;

            Assert.That(rig.TargetAnimator.GetInteger("StepSwingFoot"), Is.EqualTo(0),
                "The runtime-selected left foot must drive the left Animator branch.");
            rig.TargetAnimator.Update(0.15f);
            Assert.That(Mathf.Abs(rig.LeftTarget.localPosition.x - leftTargetStart),
                Is.GreaterThan(0.001f),
                "StepSwingFoot=Left must move LeftTarget.");
            Assert.That(rig.RightTarget.localPosition.x,
                Is.EqualTo(rightTargetStart).Within(0.001f),
                "StepSwingFoot=Left must leave RightTarget as stance foot.");
        }

        [UnityTest]
        public IEnumerator PhysicalStep_SelectedFootMovesMoreThanStanceFoot()
        {
            RuntimeAnimatorController controller = StaggerTestAnimatorFixture.LoadController();
            Assert.That(controller, Is.Not.Null);
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, stepController: controller,
                freezeBodies: false);
            yield return new WaitForFixedUpdate();
            StaggerPhysicalRig.SetField(rig.Stagger, "transitionDuration", 0f);
            StaggerPhysicalRig.SetField(rig.Stagger, "liftOffDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "swingDuration", 0.18f);
            StaggerPhysicalRig.SetField(rig.Stagger, "replantDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "settlingDuration", 0.12f);
            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);
            SetProperty(rig.Stagger, "CurrentState", RagdollBipedBalanceState.RequiresStep);
            StaggerPhysicalRig.SetField(
                rig.Stagger, "lastClassificationSnapshot", rig.Puppet.LastStaggerSnapshot);
            StaggerPhysicalRig.SetField(rig.Stagger, "hasClassificationSnapshot", true);
            InvokePrivate(rig.Stagger, "BeginStep");
            bool selectedLeft = rig.TargetAnimator.GetInteger("StepSwingFoot") == 0;
            Rigidbody selectedBody = selectedLeft ? rig.LeftFootBody : rig.RightFootBody;
            Rigidbody stanceBody = selectedLeft ? rig.RightFootBody : rig.LeftFootBody;
            Vector3 selectedStart = selectedBody.position;
            Vector3 stanceStart = stanceBody.position;

            float selectedPeakHeight = selectedStart.y;
            for (int frame = 0; frame < 18; frame++)
            {
                yield return new WaitForFixedUpdate();
                selectedPeakHeight = Mathf.Max(selectedPeakHeight,
                    selectedBody.position.y);
            }

            float selectedTravel = Vector3.Distance(
                selectedBody.position, selectedStart);
            float stanceTravel = Vector3.Distance(
                stanceBody.position, stanceStart);
            Assert.That(selectedTravel, Is.GreaterThan(0.02f),
                "Selected physical foot must move during the step clip.");
            Assert.That(stanceTravel, Is.LessThan(selectedTravel),
                "Stance foot must move less than selected swing foot.");
            Assert.That(selectedPeakHeight, Is.GreaterThan(selectedStart.y + 0.005f),
                "Selected physical foot must lift off the ground during Swing.");
        }

        [UnityTest]
        public IEnumerator PhysicalPush_RequiresStepEventActivatesStaggerWithoutManualBeginStep()
        {
            RuntimeAnimatorController controller = StaggerTestAnimatorFixture.LoadController();
            Assert.That(controller, Is.Not.Null);
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, stepController: controller,
                freezeBodies: false);
            rig.RootBody.constraints = RigidbodyConstraints.FreezeRotation;
            rig.Puppet.CanStagger = true;
            // Require two fixed samples before the behaviour switch so the
            // benchmark proves a sustained RequiresStep episode, not a single
            // classification that immediately activates Stagger.
            rig.Puppet.MinimumRequiresStepDuration = Time.fixedDeltaTime * 2f;
            rig.Puppet.OnRequiresStep = new RagdollPuppetEvent
            {
                SwitchToBehaviour = typeof(RagdollBipedStaggerBehaviour).FullName
            };

            yield return new WaitForFixedUpdate();
            RagdollGroundingSnapshot prePushSnapshot = rig.Puppet.CenterOfMass.Snapshot;
            float marginBeforePush = CaptureMargin(rig);
            float measuredComSpeed = prePushSnapshot.CenterOfMassVelocity.magnitude;
            // ForceMode.Impulse applies an instantaneous impulse to the root;
            // the COM snapshot is mass-weighted over root plus both feet.  The
            // fixture therefore needs a calculated impulse, not root-only
            // arithmetic such as 4.4 N*s / 2 kg.  Aim for a modest negative
            // margin so the result remains RequiresStep rather than becoming
            // Unrecoverable.
            float calibratedImpulse = CalibratedRequiresStepImpulse(
                rig, desiredMargin: -0.15f);
            rig.RootBody.AddForce(Vector3.left * calibratedImpulse, ForceMode.Impulse);

            List<StaggerFixedTick> trace = new List<StaggerFixedTick>();
            float minimumMarginAfterPush = float.PositiveInfinity;
            bool sawRequiresStep = false;
            bool staggerActivated = false;
            List<string> activationTrace = new List<string>();
            Vector3 leftStart = rig.LeftFootBody.position;
            Vector3 rightStart = rig.RightFootBody.position;
            for (int frame = 0; frame < 12; frame++)
            {
                yield return new WaitForFixedUpdate();
                float margin = CaptureMargin(rig);
                minimumMarginAfterPush = Mathf.Min(minimumMarginAfterPush, margin);
                bool puppetActive = rig.Controller.ActiveBehaviour
                    is RagdollPuppetBehaviour;
                sawRequiresStep |= puppetActive
                    && rig.Puppet.LastStaggerClassification ==
                        RagdollBipedBalanceState.RequiresStep;
                if (rig.Controller.ActiveBehaviour is RagdollBipedStaggerBehaviour)
                {
                    staggerActivated = true;
                    sawRequiresStep |= rig.Stagger.CurrentState ==
                        RagdollBipedBalanceState.RequiresStep;
                }
                activationTrace.Add(
                    $"tick={frame}, active={rig.Controller.ActiveBehaviour?.GetType().Name}, " +
                    $"puppetClassification={rig.Puppet.LastStaggerClassification}, " +
                    $"requiresStepElapsed={rig.Puppet.StaggerRequiresStepElapsed:F4}, " +
                    $"triggerFired={rig.Puppet.StaggerTriggerFired}, " +
                    $"staggerState={rig.Stagger.CurrentState}, margin={margin:F4}");
                int selectedFoot = rig.TargetAnimator.GetInteger("StepSwingFoot");
                string stepPhase = ReadStepPhase(rig.Stagger);
                bool leftGrounded = IsFootGrounded(rig, rig.LeftFootBody);
                bool rightGrounded = IsFootGrounded(rig, rig.RightFootBody);
                float leftTravel = Vector3.Distance(rig.LeftFootBody.position, leftStart);
                float rightTravel = Vector3.Distance(rig.RightFootBody.position, rightStart);
                trace.Add(new StaggerFixedTick(
                    frame,
                    rig.Controller.ActiveBehaviour?.GetType().Name,
                    rig.Puppet.State,
                    rig.Stagger.CurrentState,
                    rig.Stagger.LastSignedSupportMargin,
                    margin,
                    selectedFoot,
                    stepPhase,
                    rig.LeftFootBody.position,
                    rig.RightFootBody.position,
                    leftGrounded,
                    rightGrounded,
                    stepPhase == "Replant" && (selectedFoot == 0
                        ? leftGrounded
                        : rightGrounded),
                    selectedFoot == 0
                        ? Mathf.Abs(rig.LeftFootBody.position.x - leftStart.x)
                        : Mathf.Abs(rig.RightFootBody.position.x - rightStart.x),
                    leftTravel,
                    rightTravel));

                // Keep the externally-applied push active until the event
                // routes into Stagger. This is a fixed-tick sustained input,
                // not a second classification shortcut: the Puppet trigger
                // still has to observe RequiresStep for its configured gate.
                if (!staggerActivated)
                {
                    rig.RootBody.AddForce(
                        Vector3.left * calibratedImpulse,
                        ForceMode.Impulse);
                }
            }

            Assert.That(float.IsNaN(marginBeforePush) || float.IsInfinity(marginBeforePush), Is.False,
                "The pre-push COM snapshot must be usable for calibration.");
            Assert.That(prePushSnapshot.TotalMass, Is.EqualTo(3f).Within(0.01f),
                "The fixture calibration must include root plus both 0.5 kg feet.");
            Assert.That(float.IsNaN(measuredComSpeed) || float.IsInfinity(measuredComSpeed), Is.False,
                "The measured pre-push COM velocity must be finite.");
            Assert.That(float.IsNaN(minimumMarginAfterPush)
                || float.IsInfinity(minimumMarginAfterPush), Is.False,
                "The post-push COM trace must contain a usable margin.");
            Assert.That(minimumMarginAfterPush, Is.LessThan(0f),
                "The calibrated impulse must actually leave the support region.");
            Assert.That(sawRequiresStep, Is.True,
                "The outgoing Puppet classification must include RequiresStep before activation. " +
                string.Join(" | ", activationTrace));
            Assert.That(staggerActivated, Is.True,
                "Physical push did not route RequiresStep through OnRequiresStep to Stagger. " +
                string.Join(" | ", activationTrace));

            TestContext.WriteLine(
                $"Impact trace: marginBefore={marginBeforePush:F4}, " +
                $"minimumAfter={minimumMarginAfterPush:F4}, " +
                $"prePushComSpeed={measuredComSpeed:F4}, ticks={trace.Count}");
        }

        [UnityTest]
        public IEnumerator E02_PhysicalPush_StaggerRecoveryBenchmarkProvesCompleteEpisode()
        {
            RuntimeAnimatorController controller = StaggerTestAnimatorFixture.LoadController();
            Assert.That(controller, Is.Not.Null);
            rig = new StaggerPhysicalRig(
                footOffsetX: 0.5f,
                stepController: controller,
                freezeBodies: false);
            rig.RootBody.constraints = RigidbodyConstraints.FreezeRotation;
            rig.Puppet.CanStagger = true;
            rig.Puppet.MinimumRequiresStepDuration = Time.fixedDeltaTime * 2f;
            rig.Puppet.OnRequiresStep = new RagdollPuppetEvent
            {
                SwitchToBehaviour = typeof(RagdollBipedStaggerBehaviour).FullName
            };
            rig.Stagger.StableMargin = 0.05f;
            // The benchmark keeps the declared RequiresStep band wide enough
            // to observe the complete physical episode; Unrecoverable remains
            // a fail-fast result in the runtime.
            rig.Stagger.RequiresStepMargin = 0.5f;
            rig.Stagger.MaxSteps = 1;
            StaggerPhysicalRig.SetField(rig.Stagger, "liftOffDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "swingDuration", 0.18f);
            StaggerPhysicalRig.SetField(rig.Stagger, "replantDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "settlingDuration", 0.12f);
            StaggerPhysicalRig.SetField(rig.Stagger, "transitionDuration", 0f);
            StaggerPhysicalRig.SetField(rig.Stagger, "swingPositionPinWeight", 0.8f);

            yield return new WaitForFixedUpdate();
            LogFixturePose("pre-push", rig);
            float marginBeforePush = CaptureMargin(rig);
            float calibratedImpulse = CalibratedRequiresStepImpulse(
                rig, desiredMargin: -0.15f);
            rig.RootBody.AddForce(Vector3.left * calibratedImpulse, ForceMode.Impulse);

            bool activated = false;
            bool sawRequiresStep = false;
            bool sustainedRequiresStep = false;
            bool selectedFootKnown = false;
            bool selectedFootMoved = false;
            bool liftOffObserved = false;
            bool swingObserved = false;
            bool replantObserved = false;
            bool replantContactObserved = false;
            bool returnedToPuppet = false;
            bool sawUnpinned = false;
            float minimumMargin = float.PositiveInfinity;
            float maximumMargin = float.NegativeInfinity;
            float minimumReplantHeight = float.PositiveInfinity;
            float maximumSelectedHeight = float.NegativeInfinity;
            Vector3 selectedStart = Vector3.zero;
            Vector3 stanceStart = Vector3.zero;
            int selectedFoot = -1;
            List<StaggerRecoveryFixedTick> recoveryTrace =
                new List<StaggerRecoveryFixedTick>();

            for (int tick = 0; tick < 90; tick++)
            {
                yield return new WaitForFixedUpdate();
                sawUnpinned |= rig.Puppet.State == RagdollPuppetState.Unpinned;
                bool staggerActive = rig.Controller.ActiveBehaviour
                    is RagdollBipedStaggerBehaviour;
                RagdollGroundingSnapshot tickSnapshot = staggerActive
                    && rig.Stagger.LastClassificationSnapshot.TotalMass > Mathf.Epsilon
                    ? rig.Stagger.LastClassificationSnapshot
                    : rig.Puppet.LastStaggerSnapshot.TotalMass > Mathf.Epsilon
                        ? rig.Puppet.LastStaggerSnapshot
                        : rig.Puppet.CenterOfMass.Snapshot;
                float tickMargin = staggerActive
                    && rig.Stagger.LastClassificationSnapshot.TotalMass > Mathf.Epsilon
                    ? rig.Stagger.LastSignedSupportMargin
                    : CaptureMargin(rig);
                Vector3 tickCapturePoint = RagdollBipedBalanceMath.CapturePoint(
                    tickSnapshot.CenterOfMass,
                    tickSnapshot.CenterOfMassVelocity,
                    rig.Stagger.PendulumLength,
                    Physics.gravity.magnitude);
                AnimatorStateInfo tickAnimatorState =
                    rig.TargetAnimator.GetCurrentAnimatorStateInfo(0);
                int tickSelectedFoot = rig.TargetAnimator.GetInteger("StepSwingFoot");
                string tickPhase = ReadStepPhase(rig.Stagger);
                recoveryTrace.Add(new StaggerRecoveryFixedTick(
                    tick,
                    Time.fixedTime,
                    rig.Controller.ActiveBehaviour?.GetType().Name,
                    rig.Puppet.State,
                    rig.Stagger.CurrentState,
                    tickMargin,
                    tickCapturePoint,
                    tickSelectedFoot,
                    tickPhase,
                    DescribeAnimatorState(tickAnimatorState.fullPathHash),
                    tickAnimatorState.fullPathHash,
                    rig.LeftTarget.position,
                    rig.RightTarget.position,
                    rig.LeftFootBody.position,
                    rig.RightFootBody.position,
                    rig.RootBody.position,
                    rig.RootBody.linearVelocity,
                    IsFootGrounded(rig, rig.LeftFootBody),
                    IsFootGrounded(rig, rig.RightFootBody)));
                sawRequiresStep |= rig.Puppet.LastStaggerClassification ==
                    RagdollBipedBalanceState.RequiresStep;
                sustainedRequiresStep |= rig.Puppet.StaggerRequiresStepElapsed >=
                    rig.Puppet.MinimumRequiresStepDuration - 0.00001f;
                if (rig.Controller.ActiveBehaviour is RagdollBipedStaggerBehaviour)
                {
                    activated = true;
                    float margin = tickMargin;
                    minimumMargin = Mathf.Min(minimumMargin, margin);
                    maximumMargin = Mathf.Max(maximumMargin, margin);
                    if (rig.Stagger.CurrentState == RagdollBipedBalanceState.RequiresStep)
                    {
                        sawRequiresStep = true;
                    }

                    int currentFoot = tickSelectedFoot;
                    // The first Stagger tick can still expose the Animator's
                    // neutral parameter while the pending first classification
                    // is being consumed. Record the physical selection only
                    // once the step machine has entered an actual phase.
                    if ((currentFoot == 0 || currentFoot == 1)
                        && tickPhase != "Idle")
                    {
                        if (!selectedFootKnown)
                        {
                            selectedFoot = currentFoot;
                            selectedStart = currentFoot == 0
                                ? rig.LeftFootBody.position
                                : rig.RightFootBody.position;
                            stanceStart = currentFoot == 0
                                ? rig.RightFootBody.position
                                : rig.LeftFootBody.position;
                            selectedFootKnown = true;
                        }
                        selectedFootMoved |= Vector3.Distance(
                            currentFoot == 0 ? rig.LeftFootBody.position : rig.RightFootBody.position,
                            selectedStart) > 0.02f;
                    }

                    string phase = tickPhase;
                    liftOffObserved |= phase == "LiftOff";
                    swingObserved |= phase == "Swing";
                    replantObserved |= phase == "Replant";
                    if ((phase == "Replant"
                            || (phase == "Settling" && replantObserved))
                        && selectedFootKnown)
                    {
                        Rigidbody replantBody = selectedFoot == 0
                            ? rig.LeftFootBody
                            : rig.RightFootBody;
                        minimumReplantHeight = Mathf.Min(
                            minimumReplantHeight, replantBody.position.y);
                        replantContactObserved |= IsFootGrounded(rig, replantBody);
                    }
                    if (selectedFootKnown)
                    {
                        maximumSelectedHeight = Mathf.Max(
                            maximumSelectedHeight,
                            (selectedFoot == 0
                                ? rig.LeftFootBody
                                : rig.RightFootBody).position.y);
                    }
                }
                else if (activated)
                {
                    returnedToPuppet = rig.Controller.ActiveBehaviour
                        is RagdollPuppetBehaviour;
                    if (returnedToPuppet)
                    {
                        // OnBehaviourActivated resets Puppet's COM snapshot. Give
                        // its next fixed update a chance to sample the post-step
                        // pose before measuring the final capture margin.
                        yield return new WaitForFixedUpdate();
                        break;
                    }
                }

                if (!activated)
                {
                    rig.RootBody.AddForce(
                        Vector3.left * calibratedImpulse,
                        ForceMode.Impulse);
                }
            }

            float finalMargin = CaptureMargin(rig);
            Rigidbody selectedBody = selectedFoot == 0
                ? rig.LeftFootBody
                : selectedFoot == 1
                    ? rig.RightFootBody
                    : null;
            Transform selectedTarget = selectedFoot == 0
                ? rig.LeftTarget
                : selectedFoot == 1
                    ? rig.RightTarget
                    : null;
            float selectedTravel = selectedBody != null
                ? Vector3.Distance(selectedBody.position, selectedStart)
                : float.PositiveInfinity;
            // The selected foot is expected to travel with the authored stride.
            // Foot-slip is the residual physical-to-target error after Replant,
            // not the intended displacement from the pre-step pose.
            float selectedLandingError = selectedBody != null && selectedTarget != null
                ? Mathf.Abs(selectedBody.position.x - selectedTarget.position.x)
                : float.PositiveInfinity;
            float stanceTravel = selectedFoot == 0
                ? Vector3.Distance(rig.RightFootBody.position, stanceStart)
                : selectedFoot == 1
                    ? Vector3.Distance(rig.LeftFootBody.position, stanceStart)
                    : float.PositiveInfinity;
            RagdollGroundingSnapshot finalSnapshot = rig.Puppet.CenterOfMass.Snapshot;
            Vector3 finalCapturePoint = RagdollBipedBalanceMath.CapturePoint(
                finalSnapshot.CenterOfMass,
                finalSnapshot.CenterOfMassVelocity,
                rig.Stagger.PendulumLength,
                Physics.gravity.magnitude);
            int finalAnimatorStateHash = rig.TargetAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;

            TestContext.WriteLine(
                $"Recovery trace: marginBefore={marginBeforePush:F4}, " +
                $"minimum={minimumMargin:F4}, maximum={maximumMargin:F4}, " +
                $"final={finalMargin:F4}, selectedFoot={selectedFoot}, " +
                $"selectedStart={selectedStart}, left={rig.LeftFootBody.position}, " +
                $"right={rig.RightFootBody.position}, leftTarget={rig.LeftTarget.position}, " +
                $"rightTarget={rig.RightTarget.position}, selectedTravel={selectedTravel:F4}, " +
                $"selectedLandingError={selectedLandingError:F4}, " +
                $"stanceTravel={stanceTravel:F4}, capturePoint={finalCapturePoint}, " +
                $"animatorStateHash={finalAnimatorStateHash}, returned={returnedToPuppet}");
            foreach (StaggerRecoveryFixedTick sample in recoveryTrace)
                TestContext.WriteLine(sample.Format());

            Assert.That(activated, Is.True);
            Assert.That(sawRequiresStep, Is.True,
                "The outgoing Puppet classification must observe RequiresStep " +
                "before OnRequiresStep switches behaviours.");
            Assert.That(sustainedRequiresStep, Is.True,
                "RequiresStep must persist across fixed ticks before the step.");
            Assert.That(selectedFootKnown, Is.True);
            Assert.That(selectedFootMoved, Is.True);
            Assert.That(liftOffObserved, Is.True);
            Assert.That(swingObserved, Is.True);
            Assert.That(replantContactObserved, Is.True,
                $"The selected foot must make observable contact during Replant " +
                $"or at the immediate Replant-to-Settling handoff " +
                $"(minReplantY={minimumReplantHeight:F4}, maxSelectedY={maximumSelectedHeight:F4}).");
            // The authored baseline is centered between the feet. With a
            // support radius of 0.15 it is already the fixture's maximum
            // possible margin, so final > marginBefore would be impossible.
            // Recovery is therefore measured against the post-push minimum,
            // while the final pose must return to baseline within measurement
            // tolerance rather than degrade below it.
            Assert.That(finalMargin, Is.GreaterThan(minimumMargin + 0.05f),
                $"Capture margin must improve from the post-push minimum " +
                $"(before={marginBeforePush:F4}, final={finalMargin:F4}, " +
                $"minimum={minimumMargin:F4}, maximum={maximumMargin:F4}, " +
                $"selectedFoot={selectedFoot}, left={rig.LeftFootBody.position}, " +
                $"right={rig.RightFootBody.position}, leftTarget={rig.LeftTarget.position}, " +
                $"rightTarget={rig.RightTarget.position}, capturePoint={finalCapturePoint}, " +
                $"animatorStateHash={finalAnimatorStateHash}).");
            Assert.That(finalMargin, Is.GreaterThanOrEqualTo(marginBeforePush - 0.001f),
                $"Capture margin must not finish below its stable baseline " +
                $"(before={marginBeforePush:F4}, final={finalMargin:F4}).");
            Assert.That(selectedTravel, Is.GreaterThan(0.02f),
                "The physically selected foot must execute the authored stride.");
            Assert.That(selectedLandingError, Is.LessThan(0.15f),
                "Selected foot horizontal landing error must remain bounded after contact.");
            Assert.That(stanceTravel, Is.LessThan(selectedTravel + 0.01f),
                "The stance foot must travel less than the selected swing foot, within fixture drift tolerance.");
            Assert.That(returnedToPuppet, Is.True);
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
            Assert.That(sawUnpinned, Is.False);
            Assert.That(minimumMargin, Is.LessThan(0f));
            Assert.That(maximumMargin, Is.GreaterThan(minimumMargin));
            AssertE02TelemetryTrace(recoveryTrace, selectedFoot);
        }

        [UnityTest]
        public IEnumerator UnpinThenBehaviourSwitch_ResetsPuppetStateAtLifecycleBoundary()
        {
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f);
            yield return null;

            bool changed = false;
            rig.Controller.ActiveBehaviourChanged += (previous, current) => changed = true;
            rig.Puppet.Unpin();
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);
            Assert.That(changed, Is.True,
                "The controller must report the explicit post-Unpin behaviour switch.");
            Assert.That(rig.Controller.ActiveBehaviour,
                Is.InstanceOf<RagdollBipedStaggerBehaviour>());
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Puppet),
                "Puppet.OnBehaviourDeactivated resets Puppet state; this is the boundary " +
                "that must be distinguished from an unexplained production switch.");
        }

        static float CaptureMargin(StaggerPhysicalRig rig)
        {
            RagdollGroundingSnapshot snapshot = rig.Puppet.CenterOfMass.Snapshot;
            return RagdollBipedBalanceMath.SignedCaptureMargin(
                snapshot.CenterOfMass,
                snapshot.CenterOfMassVelocity,
                rig.LeftFootBody.worldCenterOfMass,
                rig.RightFootBody.worldCenterOfMass,
                rig.Stagger.PendulumLength,
                Physics.gravity.magnitude,
                rig.Stagger.SupportRadius);
        }

        static void AssertE02TelemetryTrace(
            IReadOnlyList<StaggerRecoveryFixedTick> trace,
            int expectedSelectedFoot)
        {
            Assert.That(trace, Is.Not.Null.And.Not.Empty,
                "E02 must retain at least one fixed-tick telemetry sample.");

            int previousTick = -1;
            float previousSimulationTime = float.NegativeInfinity;
            int phaseOrder = -1;
            int observedSelectedFoot = -1;
            bool sawPuppetBeforeStagger = false;
            bool sawStagger = false;
            bool sawPuppetAfterStagger = false;
            bool sawLiftOff = false;
            bool sawSwing = false;
            bool sawReplant = false;
            bool sawReplantContact = false;
            bool sawSettling = false;

            for (int index = 0; index < trace.Count; index++)
            {
                StaggerRecoveryFixedTick sample = trace[index];
                Assert.That(sample.tick, Is.GreaterThan(previousTick),
                    $"Telemetry tick provenance must be strictly ordered at sample {index}.");
                Assert.That(sample.simulationTime,
                    Is.GreaterThan(previousSimulationTime),
                    $"Telemetry simulation-time provenance must be strictly ordered at tick {sample.tick}.");
                Assert.That(IsFinite(sample.signedMargin), Is.True,
                    $"Signed capture margin must be finite at tick {sample.tick}.");
                Assert.That(IsFinite(sample.capturePoint), Is.True,
                    $"Capture point must be finite at tick {sample.tick}.");

                previousTick = sample.tick;
                previousSimulationTime = sample.simulationTime;

                if (sample.activeBehaviour == nameof(RagdollPuppetBehaviour)
                    && !sawStagger)
                {
                    sawPuppetBeforeStagger = true;
                }

                if (sample.activeBehaviour == nameof(RagdollBipedStaggerBehaviour))
                {
                    sawStagger = true;
                    string phase = sample.stepPhase;
                    if (phase == "Idle" || phase == "Unknown") continue;

                    Assert.That(sample.selectedFoot == 0 || sample.selectedFoot == 1, Is.True,
                        $"A non-idle Stagger phase must expose a selected foot at tick {sample.tick}.");
                    if (observedSelectedFoot < 0)
                        observedSelectedFoot = sample.selectedFoot;
                    Assert.That(sample.selectedFoot, Is.EqualTo(observedSelectedFoot),
                        $"Swing foot identity must remain stable throughout the episode at tick {sample.tick}.");

                    int currentPhaseOrder = PhaseOrder(phase);
                    Assert.That(currentPhaseOrder, Is.GreaterThanOrEqualTo(phaseOrder),
                        $"Stagger phases must be monotonic at tick {sample.tick}: {phase}.");
                    phaseOrder = Mathf.Max(phaseOrder, currentPhaseOrder);

                    bool selectedFootGrounded = sample.selectedFoot == 0
                        ? sample.leftFootGrounded
                        : sample.rightFootGrounded;
                    if (phase == "LiftOff") sawLiftOff = true;
                    if (phase == "Swing") sawSwing = true;
                    if (phase == "Replant")
                    {
                        sawReplant = true;
                        sawReplantContact |= selectedFootGrounded;
                    }
                    if (phase == "Settling")
                    {
                        sawSettling = true;
                        sawReplantContact |= sawReplant && selectedFootGrounded;
                    }
                }
                else if (sawStagger && sample.activeBehaviour == nameof(RagdollPuppetBehaviour))
                {
                    sawPuppetAfterStagger = true;
                }
            }

            Assert.That(sawPuppetBeforeStagger, Is.True,
                "Telemetry must contain the Puppet sample before Stagger activation.");
            Assert.That(sawStagger, Is.True,
                "Telemetry must contain the active Stagger interval.");
            Assert.That(sawPuppetAfterStagger, Is.True,
                "Telemetry must contain the Puppet sample after Stagger recovery.");
            Assert.That(observedSelectedFoot, Is.EqualTo(expectedSelectedFoot),
                "Telemetry selected foot must match the benchmark's physical selection.");
            Assert.That(sawLiftOff, Is.True);
            Assert.That(sawSwing, Is.True);
            Assert.That(sawReplant, Is.True);
            Assert.That(sawSettling, Is.True);
            Assert.That(sawReplantContact, Is.True,
                "Replant telemetry must be backed by selected-foot ground contact at its tick.");
            Assert.That(phaseOrder, Is.EqualTo(3),
                "The complete E02 episode must reach Settling after Replant.");
        }

        static int PhaseOrder(string phase)
        {
            switch (phase)
            {
                case "LiftOff": return 0;
                case "Swing": return 1;
                case "Replant": return 2;
                case "Settling": return 3;
                default: return -1;
            }
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static float CalibratedRequiresStepImpulse(
            StaggerPhysicalRig rig,
            float desiredMargin)
        {
            RagdollGroundingSnapshot snapshot = rig.Puppet.CenterOfMass.Snapshot;
            Vector3 leftFoot = Vector3.ProjectOnPlane(
                rig.LeftFootBody.worldCenterOfMass, Vector3.up);
            Vector3 rightFoot = Vector3.ProjectOnPlane(
                rig.RightFootBody.worldCenterOfMass, Vector3.up);
            Vector3 centerOfMass = Vector3.ProjectOnPlane(
                snapshot.CenterOfMass, Vector3.up);

            // For a point beyond the left edge, SignedSupportMargin is
            // supportRadius - (leftEdge - capturePoint). Solve that equation
            // for the requested margin, then convert the required capture-point
            // displacement into the mass-weighted COM impulse.
            float leftEdge = Mathf.Min(leftFoot.x, rightFoot.x);
            float targetCapturePointX = leftEdge
                - rig.Stagger.SupportRadius
                + desiredMargin;
            float omega = Mathf.Sqrt(
                Mathf.Max(0.01f, Physics.gravity.magnitude)
                / Mathf.Max(0.05f, rig.Stagger.PendulumLength));
            float requiredComSpeed = Mathf.Abs(targetCapturePointX - centerOfMass.x)
                * omega;
            return requiredComSpeed * snapshot.TotalMass;
        }

        static void LogFixturePose(string label, StaggerPhysicalRig rig)
        {
            TestContext.WriteLine(
                $"{label}: root={rig.RootBody.position}, " +
                $"leftBody={rig.LeftFootBody.position}, " +
                $"rightBody={rig.RightFootBody.position}, " +
                $"leftTransform={rig.LeftFootBody.transform.position}, " +
                $"rightTransform={rig.RightFootBody.transform.position}, " +
                $"leftLocal={rig.LeftFootBody.transform.localPosition}, " +
                $"rightLocal={rig.RightFootBody.transform.localPosition}, " +
                $"leftKinematic={rig.LeftFootBody.isKinematic}, " +
                $"rightKinematic={rig.RightFootBody.isKinematic}, " +
                $"leftConstraints={rig.LeftFootBody.constraints}, " +
                $"rightConstraints={rig.RightFootBody.constraints}, " +
                $"leftTarget={rig.LeftTarget.position}, " +
                $"rightTarget={rig.RightTarget.position}, " +
                $"leftJointAnchor={rig.LeftFootJoint.anchor}, " +
                $"leftConnectedAnchor={rig.LeftFootJoint.connectedAnchor}, " +
                $"rightJointAnchor={rig.RightFootJoint.anchor}, " +
                $"rightConnectedAnchor={rig.RightFootJoint.connectedAnchor}, " +
                $"leftXMotion={rig.LeftFootJoint.xMotion}, " +
                $"rightXMotion={rig.RightFootJoint.xMotion}");

            IReadOnlyList<RagdollTargetBinding> bindings =
                rig.Result.Animator.TargetBindings.Bindings;
            for (int index = 0; index < bindings.Count; index++)
            {
                RagdollTargetBinding binding = bindings[index];
                RagdollBone bone;
                string physical = rig.Result.Animator.Bindings.TryGetBone(
                    binding.Bone,
                    out bone)
                    ? bone.Transform.position.ToString()
                    : "<missing>";
                TestContext.WriteLine(
                    $"{label} binding[{index}] bone={binding.Bone}, " +
                    $"target={binding.Target.position}, " +
                    $"offset={binding.TargetPositionOffset}, " +
                    $"physical={physical}");
            }

            FieldInfo pairsField = typeof(RagdollAnimator).GetField(
                "animatedPairs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Array pairs = pairsField?.GetValue(rig.Result.Animator) as Array;
            if (pairs == null) return;
            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                TestContext.WriteLine(
                    $"{label} pair={pair.Name}, target={pair.TargetBone.position}, " +
                    $"sampledTarget={pair.SampledTargetPose.worldPosition}, " +
                    $"currentPose={pair.currentPose.worldPosition}, " +
                    $"physical={pair.RagdollBone.Rigidbody.position}");
            }
        }

        static RagdollBipedBalanceState Classify(
            float margin, RagdollBipedStaggerBehaviour stagger)
        {
            return RagdollBipedBalanceMath.Classify(
                margin, stagger.StableMargin, stagger.RequiresStepMargin);
        }

        static string ReadStepPhase(RagdollBipedStaggerBehaviour stagger)
        {
            FieldInfo machineField = typeof(RagdollBipedStaggerBehaviour).GetField(
                "stepMachine", BindingFlags.Instance | BindingFlags.NonPublic);
            object machine = machineField?.GetValue(stagger);
            PropertyInfo state = machine?.GetType().GetProperty(
                "State", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return state?.GetValue(machine, null)?.ToString() ?? "Unknown";
        }

        static string DescribeAnimatorState(int fullPathHash)
        {
            string[] stateNames =
            {
                "StepForward", "StepForwardRightFoot",
                "StepBackward", "StepBackwardRightFoot",
                "StepLeft", "StepLeftRightFoot",
                "StepRight", "StepRightRightFoot"
            };
            foreach (string stateName in stateNames)
            {
                if (fullPathHash == Animator.StringToHash("Base Layer." + stateName))
                    return stateName;
            }
            return "Unknown";
        }

        static bool IsFootGrounded(StaggerPhysicalRig rig, Rigidbody foot)
        {
            GroundContactProbe probe = foot.GetComponent<GroundContactProbe>();
            return probe != null && probe.IsGrounded;
        }

        readonly struct StaggerFixedTick
        {
            internal readonly int Tick;
            internal readonly string ActiveBehaviour;
            internal readonly RagdollPuppetState PuppetState;
            internal readonly RagdollBipedBalanceState BalanceState;
            internal readonly float LastSignedSupportMargin;
            internal readonly float SignedMargin;
            internal readonly int SelectedFoot;
            internal readonly string StepPhase;
            internal readonly Vector3 LeftFootPosition;
            internal readonly Vector3 RightFootPosition;
            internal readonly bool LeftFootGrounded;
            internal readonly bool RightFootGrounded;
            internal readonly bool ReplantContact;
            internal readonly float SelectedFootSlip;
            internal readonly float LeftFootTravel;
            internal readonly float RightFootTravel;

            internal StaggerFixedTick(
                int tick,
                string activeBehaviour,
                RagdollPuppetState puppetState,
                RagdollBipedBalanceState balanceState,
                float lastSignedSupportMargin,
                float signedMargin,
                int selectedFoot,
                string stepPhase,
                Vector3 leftFootPosition,
                Vector3 rightFootPosition,
                bool leftFootGrounded,
                bool rightFootGrounded,
                bool replantContact,
                float selectedFootSlip,
                float leftFootTravel,
                float rightFootTravel)
            {
                Tick = tick;
                ActiveBehaviour = activeBehaviour;
                PuppetState = puppetState;
                BalanceState = balanceState;
                LastSignedSupportMargin = lastSignedSupportMargin;
                SignedMargin = signedMargin;
                SelectedFoot = selectedFoot;
                StepPhase = stepPhase;
                LeftFootPosition = leftFootPosition;
                RightFootPosition = rightFootPosition;
                LeftFootGrounded = leftFootGrounded;
                RightFootGrounded = rightFootGrounded;
                ReplantContact = replantContact;
                SelectedFootSlip = selectedFootSlip;
                LeftFootTravel = leftFootTravel;
                RightFootTravel = rightFootTravel;
            }
        }

        readonly struct StaggerRecoveryFixedTick
        {
            internal readonly int tick;
            internal readonly float simulationTime;
            internal readonly string activeBehaviour;
            internal readonly RagdollPuppetState puppetState;
            internal readonly RagdollBipedBalanceState balanceState;
            internal readonly float signedMargin;
            internal readonly Vector3 capturePoint;
            internal readonly int selectedFoot;
            internal readonly string stepPhase;
            internal readonly string animatorState;
            internal readonly int animatorStateHash;
            internal readonly Vector3 leftTargetPosition;
            internal readonly Vector3 rightTargetPosition;
            internal readonly Vector3 leftFootPosition;
            internal readonly Vector3 rightFootPosition;
            internal readonly Vector3 rootPosition;
            internal readonly Vector3 rootVelocity;
            internal readonly bool leftFootGrounded;
            internal readonly bool rightFootGrounded;

            internal StaggerRecoveryFixedTick(
                int tick,
                float simulationTime,
                string activeBehaviour,
                RagdollPuppetState puppetState,
                RagdollBipedBalanceState balanceState,
                float signedMargin,
                Vector3 capturePoint,
                int selectedFoot,
                string stepPhase,
                string animatorState,
                int animatorStateHash,
                Vector3 leftTargetPosition,
                Vector3 rightTargetPosition,
                Vector3 leftFootPosition,
                Vector3 rightFootPosition,
                Vector3 rootPosition,
                Vector3 rootVelocity,
                bool leftFootGrounded,
                bool rightFootGrounded)
            {
                this.tick = tick;
                this.simulationTime = simulationTime;
                this.activeBehaviour = activeBehaviour;
                this.puppetState = puppetState;
                this.balanceState = balanceState;
                this.signedMargin = signedMargin;
                this.capturePoint = capturePoint;
                this.selectedFoot = selectedFoot;
                this.stepPhase = stepPhase;
                this.animatorState = animatorState;
                this.animatorStateHash = animatorStateHash;
                this.leftTargetPosition = leftTargetPosition;
                this.rightTargetPosition = rightTargetPosition;
                this.leftFootPosition = leftFootPosition;
                this.rightFootPosition = rightFootPosition;
                this.rootPosition = rootPosition;
                this.rootVelocity = rootVelocity;
                this.leftFootGrounded = leftFootGrounded;
                this.rightFootGrounded = rightFootGrounded;
            }

            internal string Format()
            {
                return $"Recovery tick={tick}, active={activeBehaviour}, " +
                    $"time={simulationTime:F4}, " +
                    $"puppet={puppetState}, balance={balanceState}, " +
                    $"margin={signedMargin:F4}, capturePoint={capturePoint}, " +
                    $"selectedFoot={selectedFoot}, phase={stepPhase}, " +
                    $"animator={animatorState}, animatorHash={animatorStateHash}, " +
                    $"leftTarget={leftTargetPosition}, rightTarget={rightTargetPosition}, " +
                    $"leftBody={leftFootPosition}, rightBody={rightFootPosition}, " +
                    $"root={rootPosition}, rootVelocity={rootVelocity}, " +
                    $"leftGrounded={leftFootGrounded}, rightGrounded={rightFootGrounded}";
            }
        }

        static IEnumerator RunUntilIdleOrTimeout(StaggerPhysicalRig rig)
        {
            WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();
            for (int frame = 0; frame < 60; frame++)
            {
                yield return fixedUpdate;
                if (rig.Controller.ActiveBehaviour is RagdollPuppetBehaviour) yield break;
            }
            Assert.Fail("Stagger actuator never returned control to RagdollPuppetBehaviour.");
        }

        static void SetProperty(object owner, string name, object value)
        {
            PropertyInfo property = owner.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(owner, value);
        }

        static void InvokePrivate(object owner, string name)
        {
            MethodInfo method = owner.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(owner, null);
        }
    }

    /// <summary>
    /// Owns the deterministic Animator asset required by the package Stagger
    /// tests. The package must not depend on a consumer/CODE RED asset merely
    /// to execute its own PlayMode evidence.
    /// </summary>
    internal static class StaggerTestAnimatorFixture
    {
        const string ResourceName = "HairibarStaggerTests/StepRecovery";
        const string GeneratedRoot =
            "Assets/__HairibarRagdollTestFixtures/Stagger";
        const string ResourceFolder =
            GeneratedRoot + "/Resources/HairibarStaggerTests";
        const string ControllerPath = ResourceFolder + "/StepRecovery.controller";

        static readonly string[] DirectionalStates =
        {
            "StepForward", "StepBackward", "StepLeft", "StepRight",
            "StepForwardRightFoot", "StepBackwardRightFoot",
            "StepLeftRightFoot", "StepRightRightFoot"
        };

        internal static RuntimeAnimatorController LoadController()
        {
            RuntimeAnimatorController existing =
                Resources.Load<RuntimeAnimatorController>(ResourceName);
            if (IsValid(existing)) return existing;

#if UNITY_EDITOR
            return CreateEditorFixture();
#else
            return null;
#endif
        }

        internal static bool IsValid(RuntimeAnimatorController controller)
        {
            if (!controller) return false;

            PropertyInfo parametersProperty = controller.GetType().GetProperty(
                "parameters", BindingFlags.Instance | BindingFlags.Public);
            Array parameters = parametersProperty?.GetValue(controller, null) as Array;
            bool hasSwingFootParameter = false;
            if (parameters != null)
            {
                foreach (object parameter in parameters)
                {
                    PropertyInfo name = parameter.GetType().GetProperty("name");
                    PropertyInfo type = parameter.GetType().GetProperty("type");
                    if ((string)name?.GetValue(parameter, null) != "StepSwingFoot")
                        continue;

                    hasSwingFootParameter = type?.GetValue(parameter, null)
                        is AnimatorControllerParameterType.Int;
                    break;
                }
            }

            PropertyInfo layersProperty = controller.GetType().GetProperty(
                "layers", BindingFlags.Instance | BindingFlags.Public);
            Array layers = layersProperty?.GetValue(controller, null) as Array;
            if (!hasSwingFootParameter || layers == null || layers.Length == 0)
                return false;

            object layer = layers.GetValue(0);
            PropertyInfo stateMachineProperty = layer.GetType().GetProperty(
                "stateMachine", BindingFlags.Instance | BindingFlags.Public);
            object stateMachine = stateMachineProperty?.GetValue(layer, null);
            PropertyInfo statesProperty = stateMachine?.GetType().GetProperty(
                "states", BindingFlags.Instance | BindingFlags.Public);
            Array states = statesProperty?.GetValue(stateMachine, null) as Array;
            if (states == null) return false;

            HashSet<string> found = new HashSet<string>();
            foreach (object childState in states)
            {
                PropertyInfo stateProperty = childState.GetType().GetProperty("state");
                object state = stateProperty?.GetValue(childState, null);
                if (state == null) continue;

                string name = state.GetType().GetProperty("name")?.GetValue(state, null)
                    as string;
                object motion = state.GetType().GetProperty("motion")?.GetValue(state, null);
                if (!string.IsNullOrEmpty(name) && motion != null)
                    found.Add(name);
            }

            for (int index = 0; index < DirectionalStates.Length; index++)
            {
                if (!found.Contains(DirectionalStates[index])) return false;
            }

            return true;
        }

#if UNITY_EDITOR
        static RuntimeAnimatorController CreateEditorFixture()
        {
            Type assetDatabase = FindEditorType("UnityEditor.AssetDatabase");
            Type animatorController = FindEditorType(
                "UnityEditor.Animations.AnimatorController");
            if (assetDatabase == null || animatorController == null)
                return null;

            DeleteAsset(assetDatabase, GeneratedRoot);
            EnsureFolder(assetDatabase, "Assets", "__HairibarRagdollTestFixtures");
            EnsureFolder(assetDatabase,
                "Assets/__HairibarRagdollTestFixtures", "Stagger");
            EnsureFolder(assetDatabase, GeneratedRoot, "Resources");
            EnsureFolder(assetDatabase, GeneratedRoot + "/Resources", "HairibarStaggerTests");

            MethodInfo createController = animatorController.GetMethod(
                "CreateAnimatorControllerAtPath",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            object controller = createController?.Invoke(
                null, new object[] { ControllerPath });
            if (controller == null) return null;

            MethodInfo addParameter = animatorController.GetMethod(
                "AddParameter",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(AnimatorControllerParameterType) },
                null);
            addParameter?.Invoke(controller, new object[]
            {
                "StepSwingFoot", AnimatorControllerParameterType.Int
            });

            Array layers = (Array)animatorController.GetProperty("layers")
                ?.GetValue(controller, null);
            if (layers == null || layers.Length == 0) return null;
            object layer = layers.GetValue(0);
            object stateMachine = layer.GetType().GetProperty("stateMachine")
                ?.GetValue(layer, null);
            if (stateMachine == null) return null;

            Type stateMachineType = stateMachine.GetType();
            MethodInfo addState = stateMachineType.GetMethod(
                "AddState",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(Vector3) },
                null);
            if (addState == null) return null;

            AnimationClip idle = NewClip("Idle");
            CreateAsset(assetDatabase, idle, ResourceFolder + "/Idle.anim");
            object idleState = addState.Invoke(
                stateMachine, new object[] { "Idle", Vector3.zero });
            idleState.GetType().GetProperty("motion")?.SetValue(idleState, idle, null);
            stateMachineType.GetProperty("defaultState")?.SetValue(
                stateMachine, idleState, null);

            for (int index = 0; index < DirectionalStates.Length; index++)
            {
                string stateName = DirectionalStates[index];
                bool rightFoot = stateName.EndsWith("RightFoot", StringComparison.Ordinal);
                AnimationClip clip = NewStepClip(stateName, rightFoot);
                CreateAsset(assetDatabase, clip,
                    ResourceFolder + "/" + stateName + ".anim");
                object state = addState.Invoke(
                    stateMachine, new object[] { stateName, Vector3.zero });
                state.GetType().GetProperty("motion")?.SetValue(state, clip, null);
            }

            assetDatabase.GetMethod("SaveAssets", BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, null);
            assetDatabase.GetMethod("Refresh", BindingFlags.Static | BindingFlags.Public,
                null, Type.EmptyTypes, null)?.Invoke(null, null);

            RuntimeAnimatorController generated = LoadAsset<RuntimeAnimatorController>(
                assetDatabase, ControllerPath);
            return IsValid(generated) ? generated : null;
        }

        static AnimationClip NewClip(string name)
        {
            return new AnimationClip
            {
                name = name,
                legacy = false,
                frameRate = 60f,
                wrapMode = WrapMode.ClampForever
            };
        }

        static AnimationClip NewStepClip(string name, bool rightFoot)
        {
            AnimationClip clip = NewClip(name + "Clip");
            string footPath = rightFoot ? "foot_r" : "foot_l";
            float baselineX = rightFoot ? 0.5f : -0.5f;
            float landingX = rightFoot ? 0.2f : -0.2f;
            clip.SetCurve(footPath, typeof(Transform), "localPosition.x",
                Curve(baselineX, landingX, baselineX));
            clip.SetCurve(footPath, typeof(Transform), "localPosition.y",
                Curve(-1f, -0.8f, -1f));
            return clip;
        }

        static AnimationCurve Curve(float first, float middle, float last)
        {
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, first),
                new Keyframe(0.17f, middle),
                new Keyframe(0.34f, last));
            curve.preWrapMode = WrapMode.ClampForever;
            curve.postWrapMode = WrapMode.ClampForever;
            return curve;
        }

        static Type FindEditorType(string fullName)
        {
            Type type = Type.GetType(fullName + ", UnityEditor.CoreModule")
                ?? Type.GetType(fullName + ", UnityEditor");
            if (type != null) return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }

        static void EnsureFolder(Type assetDatabase, string parent, string name)
        {
            bool valid = (bool)assetDatabase.GetMethod("IsValidFolder",
                BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(string) }, null)
                .Invoke(null, new object[] { parent + "/" + name });
            if (valid) return;

            assetDatabase.GetMethod("CreateFolder", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(string), typeof(string) }, null)
                .Invoke(null, new object[] { parent, name });
        }

        static void DeleteAsset(Type assetDatabase, string path)
        {
            assetDatabase.GetMethod("DeleteAsset", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(string) }, null)?.Invoke(null, new object[] { path });
        }

        static void CreateAsset(Type assetDatabase, UnityEngine.Object asset, string path)
        {
            assetDatabase.GetMethod("CreateAsset", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(UnityEngine.Object), typeof(string) }, null)
                .Invoke(null, new object[] { asset, path });
        }

        static T LoadAsset<T>(Type assetDatabase, string path) where T : UnityEngine.Object
        {
            MethodInfo load = null;
            foreach (MethodInfo method in assetDatabase.GetMethods(
                BindingFlags.Static | BindingFlags.Public))
            {
                if (method.Name != "LoadAssetAtPath"
                    || !method.IsGenericMethodDefinition
                    || method.GetParameters().Length != 1)
                    continue;

                load = method.MakeGenericMethod(typeof(T));
                break;
            }

            return load?.Invoke(null, new object[] { path }) as T;
        }
#endif
    }

    internal sealed class StaggerPhysicalRig : IDisposable
    {
        readonly GameObject puppetRoot;
        readonly RagdollDefinition definition;
        readonly RagdollAnimationProfile profile;
        readonly bool ignoredBefore;
        GameObject ground;

        internal RagdollSetupResult Result { get; }
        internal RagdollPuppetBehaviour Puppet => Result.PuppetBehaviour;
        internal RagdollBehaviourController Controller => Result.Behaviours;
        internal RagdollBipedStaggerBehaviour Stagger { get; }
        internal Rigidbody RootBody { get; }
        internal Rigidbody LeftFootBody { get; }
        internal Rigidbody RightFootBody { get; }
        internal ConfigurableJoint LeftFootJoint { get; }
        internal ConfigurableJoint RightFootJoint { get; }
        internal Rigidbody GroundBody { get; private set; }
        internal Vector3 SupportUp { get; }
        internal Vector3 GroundNormal { get; }
        internal Collider GroundCollider => ground
            ? ground.GetComponent<Collider>()
            : null;
        internal Animator TargetAnimator { get; }
        internal Transform LeftTarget { get; }
        internal Transform RightTarget { get; }

        internal StaggerPhysicalRig(float footOffsetX, float footCenterX = 0f,
            RuntimeAnimatorController stepController = null, bool freezeBodies = true,
            float rootMassScale = 1f, float footMassScale = 1f,
            float inertiaScale = 1f, Vector3 gravityUp = default(Vector3),
            Vector3 groundNormal = default(Vector3), bool movingGround = false,
            bool allowFootRotation = false)
        {
            Assert.That(rootMassScale, Is.GreaterThan(0f));
            Assert.That(footMassScale, Is.GreaterThan(0f));
            Assert.That(inertiaScale, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(rootMassScale) || float.IsInfinity(rootMassScale), Is.False);
            Assert.That(float.IsNaN(footMassScale) || float.IsInfinity(footMassScale), Is.False);
            Assert.That(float.IsNaN(inertiaScale) || float.IsInfinity(inertiaScale), Is.False);
            SupportUp = ResolveDirection(gravityUp, Vector3.up);
            GroundNormal = ResolveDirection(groundNormal, SupportUp);
            Quaternion supportRotation = Quaternion.FromToRotation(
                Vector3.up, SupportUp);
            ignoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
            BoneName rootName = new BoneName("Root");
            BoneName leftFootName = new BoneName("foot_l");
            BoneName rightFootName = new BoneName("foot_r");

            puppetRoot = new GameObject("Stagger Puppet");
            puppetRoot.SetActive(false);
            puppetRoot.transform.rotation = supportRotation;
            GameObject leftFoot = new GameObject("foot_l");
            leftFoot.transform.SetParent(puppetRoot.transform, false);
            leftFoot.transform.localPosition = new Vector3(footCenterX - footOffsetX, -1f, 0f);
            GameObject rightFoot = new GameObject("foot_r");
            rightFoot.transform.SetParent(puppetRoot.transform, false);
            rightFoot.transform.localPosition = new Vector3(footCenterX + footOffsetX, -1f, 0f);

            RootBody = puppetRoot.AddComponent<Rigidbody>();
            RootBody.useGravity = false;
            RootBody.constraints = freezeBodies
                ? RigidbodyConstraints.FreezeAll
                : RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            ConfigurableJoint rootJoint = puppetRoot.AddComponent<ConfigurableJoint>();
            BoxCollider rootCollider = puppetRoot.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one * 0.75f;
            ApplyMassAndInertia(RootBody, 2f * rootMassScale, inertiaScale);

            ConfigurableJoint leftJoint = ConfigureFoot(
                leftFoot, RootBody, freezeBodies, footMassScale, inertiaScale,
                allowFootRotation);
            LeftFootJoint = leftJoint;
            LeftFootBody = leftFoot.GetComponent<Rigidbody>();
            ConfigurableJoint rightJoint = ConfigureFoot(
                rightFoot, RootBody, freezeBodies, footMassScale, inertiaScale,
                allowFootRotation);
            RightFootJoint = rightJoint;
            RightFootBody = rightFoot.GetComponent<Rigidbody>();

            definition = ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones",
                new[] { rootName, leftFootName, rightFootName });
            RagdollDefinitionBindings bindings =
                puppetRoot.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint,
                leftFootName, leftJoint,
                rightFootName, rightJoint));
            puppetRoot.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            GameObject target = new GameObject("Stagger Puppet");
            target.transform.rotation = supportRotation;
            TargetAnimator = target.AddComponent<Animator>();
            TargetAnimator.runtimeAnimatorController = stepController;
            GameObject leftTarget = new GameObject("foot_l");
            leftTarget.transform.SetParent(target.transform, false);
            leftTarget.transform.localPosition = new Vector3(footCenterX - footOffsetX, -1f, 0f);
            LeftTarget = leftTarget.transform;
            GameObject rightTarget = new GameObject("foot_r");
            rightTarget.transform.SetParent(target.transform, false);
            rightTarget.transform.localPosition = new Vector3(footCenterX + footOffsetX, -1f, 0f);
            RightTarget = rightTarget.transform;

            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            Result = RagdollRuntimeSetupService.ConfigureSeparated(
                target.transform, bindings, profile, 28, 29);
            Assert.That(Result.Succeeded, Is.True, Result.Error);
            Result.PuppetBehaviour.CanStagger = true;
            Result.PuppetBehaviour.LoseBalanceOnTargetDrift = false;

            if (!freezeBodies)
            {
                RootBody.isKinematic = false;
                LeftFootBody.isKinematic = false;
                RightFootBody.isKinematic = false;
                LeftFootBody.useGravity = true;
                RightFootBody.useGravity = true;
                ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Stagger Test Ground";
                ground.transform.position = -SupportUp * 1.15f;
                ground.transform.rotation = Quaternion.FromToRotation(
                    Vector3.up, GroundNormal);
                ground.transform.localScale = new Vector3(10f, 0.1f, 10f);
                ground.layer = 0;
                Collider groundCollider = ground.GetComponent<Collider>();
                leftFoot.GetComponent<GroundContactProbe>().ExpectedGround = groundCollider;
                rightFoot.GetComponent<GroundContactProbe>().ExpectedGround = groundCollider;
                if (movingGround)
                {
                    GroundBody = ground.AddComponent<Rigidbody>();
                    GroundBody.isKinematic = true;
                    GroundBody.useGravity = false;
                    GroundBody.constraints = RigidbodyConstraints.FreezeAll;
                }
            }

            Stagger = Result.PuppetBehaviour.gameObject
                .AddComponent<RagdollBipedStaggerBehaviour>();
        }

        static ConfigurableJoint ConfigureFoot(
            GameObject foot,
            Rigidbody root,
            bool freeze,
            float massScale,
            float inertiaScale,
            bool allowRotation)
        {
            Rigidbody body = foot.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = freeze
                ? RigidbodyConstraints.FreezeAll
                : allowRotation
                    ? RigidbodyConstraints.None
                    : RigidbodyConstraints.FreezeRotation;
            foot.AddComponent<GroundContactProbe>();
            ConfigurableJoint joint = foot.AddComponent<ConfigurableJoint>();
            joint.connectedBody = root;
            // Unity defines connectedAnchor relative to the connected
            // Rigidbody and autoConfigureConnectedAnchor otherwise computes
            // it for us. Preserve the authored foot support positions in this
            // dynamic evidence fixture so the physical support segment remains
            // the same one represented by the target Animator.
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = root.transform.InverseTransformPoint(
                foot.transform.position);
            if (!freeze)
            {
                joint.xMotion = ConfigurableJointMotion.Free;
                joint.yMotion = ConfigurableJointMotion.Free;
                joint.zMotion = ConfigurableJointMotion.Free;
                if (allowRotation)
                {
                    joint.angularXMotion = ConfigurableJointMotion.Free;
                    joint.angularYMotion = ConfigurableJointMotion.Free;
                    joint.angularZMotion = ConfigurableJointMotion.Free;
                }
            }
            BoxCollider collider = foot.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.25f;
            ApplyMassAndInertia(body, 0.5f * massScale, inertiaScale);
            return joint;
        }

        static void ApplyMassAndInertia(
            Rigidbody body,
            float mass,
            float inertiaScale)
        {
            body.mass = mass;
            body.inertiaTensor = body.inertiaTensor * inertiaScale;
        }

        internal void MoveGround(Vector3 delta)
        {
            if (!GroundBody)
                throw new InvalidOperationException(
                    "This StaggerPhysicalRig was not created with a moving ground.");
            GroundBody.MovePosition(GroundBody.position + delta);
        }

        public void Dispose()
        {
            Physics.IgnoreLayerCollision(28, 29, ignoredBefore);
            if (Result != null && Result.Target)
                UnityEngine.Object.DestroyImmediate(Result.Target.gameObject);
            if (ground) UnityEngine.Object.DestroyImmediate(ground);
            GroundBody = null;
            if (puppetRoot) UnityEngine.Object.DestroyImmediate(puppetRoot);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
        }

        static Vector3 ResolveDirection(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) && value.sqrMagnitude > Mathf.Epsilon
                ? value.normalized
                : fallback.normalized;
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        static object CreateBindings(
            BoneName root, ConfigurableJoint rootJoint,
            BoneName leftFoot, ConfigurableJoint leftJoint,
            BoneName rightFoot, ConfigurableJoint rightJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary",
                BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { leftFoot, leftJoint });
            add.Invoke(dictionary, new object[] { rightFoot, rightJoint });
            return dictionary;
        }

        internal static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(owner, value);
        }
    }

    /// <summary>
    /// Records an actual Unity collision with the fixture ground. This is
    /// intentionally different from Physics.ComputePenetration: Unity's
    /// documented API returns true only while colliders overlap, whereas a
    /// contact can be valid at the solver's touching boundary without a
    /// positive penetration depth.
    /// </summary>
    internal sealed class GroundContactProbe : MonoBehaviour
    {
        internal Collider ExpectedGround { get; set; }
        internal bool IsGrounded { get; private set; }

        void OnCollisionEnter(Collision collision)
        {
            UpdateContact(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            UpdateContact(collision);
        }

        void OnCollisionExit(Collision collision)
        {
            if (collision.collider == ExpectedGround)
                IsGrounded = false;
        }

        void UpdateContact(Collision collision)
        {
            if (ExpectedGround != null
                && collision.collider == ExpectedGround
                && collision.contactCount > 0)
            {
                IsGrounded = true;
            }
        }
    }
}
