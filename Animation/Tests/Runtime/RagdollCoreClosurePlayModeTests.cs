using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Direct, ID-stable integration evidence for the PuppetMaster core contract.
    /// Every case creates a real two-body PhysX Puppet and an independent Target;
    /// the assertions are deliberately made against runtime state, not another test.
    /// </summary>
    public sealed class RagdollCoreClosurePlayModeTests
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        bool ignoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index]) UnityEngine.Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator B01_DualRigMapsPhysicalPoseToIndependentTarget()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;

            Assert.That(rig.Result.Animator.Initiated, Is.True);
            Assert.That(rig.Puppet.transform, Is.Not.SameAs(rig.Target.transform));
            Assert.That(rig.Result.Behaviours.Context.Pairs.Count, Is.EqualTo(2));

            Vector3 simulated = new Vector3(0.35f, 1.2f, -0.2f);
            rig.RootBody.position = simulated;
            RagdollAnimator.AnimatedPair rootPair = FindPair(rig, 0);
            rootPair.GetMappedTargetWorldPose(
                out Vector3 expectedPosition,
                out Quaternion expectedRotation);
            InvokePrivate(rig.Result.Animator, "MapRagdollToTarget");
            Assert.That(Vector3.Distance(
                rig.Target.transform.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(
                rig.Target.transform.rotation, expectedRotation),
                Is.LessThan(0.01f));
            Assert.That(rig.Puppet.transform, Is.Not.SameAs(rig.Target.transform));
        }

        [UnityTest]
        public IEnumerator B02_PartialRagdollLeavesUnboundTargetBonesUntouched()
        {
            RuntimeRig rig = CreateRuntime(true);
            yield return null;

            Transform decoration = rig.Target.transform.Find("VisualOnly");
            Vector3 authored = new Vector3(4f, 5f, 6f);
            decoration.localPosition = authored;
            rig.ChildBody.position += Vector3.right;
            RagdollAnimator.AnimatedPair childPair = FindPair(rig, 1);
            childPair.GetMappedTargetWorldPose(
                out Vector3 expectedPosition,
                out Quaternion ignoredRotation);
            InvokePrivate(rig.Result.Animator, "MapRagdollToTarget");

            Assert.That(rig.Result.Behaviours.Context.Pairs.Count, Is.EqualTo(2));
            Assert.That(decoration.localPosition, Is.EqualTo(authored));
            Assert.That(Vector3.Distance(rig.TargetChild.position, expectedPosition),
                Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator B03_ReadMatchingPhysicsMappingPipelineHasObservableOrder()
        {
            RuntimeRig rig = CreateRuntime(false, true);
            PipelineProbe probe = rig.Target.GetComponent<PipelineProbe>();
            rig.Result.Animator.OnRead += () => probe.Trace.Add("read");
            rig.Result.Animator.OnWrite += () => probe.Trace.Add("write");
            yield return null;

            probe.Trace.Clear();
            SimulationMode previousSimulationMode = Physics.simulationMode;
            rig.Result.Animator.enabled = false;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                rig.Result.Animator.PrepareManualSimulation(Time.fixedDeltaTime);
                Physics.Simulate(Time.fixedDeltaTime);
                rig.Result.Animator.CompleteManualSimulation();
            }
            finally
            {
                Physics.simulationMode = previousSimulationMode;
                rig.Result.Animator.enabled = true;
            }

            int read = probe.Trace.IndexOf("read");
            int matching = probe.Trace.IndexOf("matching");
            int write = probe.Trace.IndexOf("write");
            Assert.That(read, Is.GreaterThanOrEqualTo(0));
            Assert.That(matching, Is.GreaterThan(read));
            Assert.That(write, Is.GreaterThan(matching));
            Assert.That(rig.RootJoint.slerpDrive.maximumForce, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator C04_SerializedPuppetEventsHaveDeterministicPhases()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            RagdollPuppetBehaviour puppet = rig.Result.PuppetBehaviour;
            List<string> phases = new List<string>();
            puppet.OnLoseBalance = CreatePuppetEvent(
                () => phases.Add("lose"));
            puppet.OnLoseBalanceFromPuppet = CreatePuppetEvent(
                () => phases.Add("lose-puppet"));
            puppet.OnRegainBalance = CreatePuppetEvent(
                () => phases.Add("regain"));
            puppet.OnGetUpProne = CreatePuppetEvent(
                () => phases.Add("prone"));
            puppet.OnGetUpSupine = CreatePuppetEvent(
                () => phases.Add("supine"));

            InvokePrivate(
                puppet,
                "InvokeTransitionEvents",
                RagdollPuppetState.Puppet,
                RagdollPuppetState.Unpinned,
                RagdollPuppetTransitionReason.TargetDrift);
            InvokePrivate(
                puppet,
                "InvokeTransitionEvents",
                RagdollPuppetState.GetUp,
                RagdollPuppetState.Puppet,
                RagdollPuppetTransitionReason.GetUpCompleted);
            InvokePrivate(
                puppet,
                "InvokeTransitionEvents",
                RagdollPuppetState.Puppet,
                RagdollPuppetState.Unpinned,
                RagdollPuppetTransitionReason.LifecycleDeath);
            InvokePrivate(
                puppet,
                "InvokeGetUpEvent",
                RagdollGetUpOrientation.Prone);
            InvokePrivate(
                puppet,
                "InvokeGetUpEvent",
                RagdollGetUpOrientation.Supine);

            CollectionAssert.AreEqual(
                new[] { "lose", "lose-puppet", "regain", "prone", "supine" },
                phases);
        }

        [UnityTest]
        public IEnumerator C07_ReactivationTeleportAndSubscriberExceptions()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            RagdollBehaviourController controller = rig.Result.Behaviours;
            GameObject failingObject = new GameObject("Failing Behaviour Hook");
            failingObject.transform.SetParent(controller.BehaviourRoot, false);
            BehaviourHookProbe failing =
                failingObject.AddComponent<BehaviourHookProbe>();
            failing.Throw = true;
            GameObject passingObject = new GameObject("Passing Behaviour Hook");
            passingObject.transform.SetParent(controller.BehaviourRoot, false);
            BehaviourHookProbe passing =
                passingObject.AddComponent<BehaviourHookProbe>();
            failing.InitializeInternal(controller.Context);
            passing.InitializeInternal(controller.Context);

            List<RagdollBehaviourBase> registered =
                new List<RagdollBehaviourBase>(controller.Behaviours);
            registered.Add(failing);
            registered.Add(passing);
            SetField(
                controller,
                "collection",
                new RagdollBehaviourCollection(registered.ToArray()));

            LogAssert.Expect(
                LogType.Exception,
                new Regex("expected reactivation hook failure"));
            controller.ReactivateAfterAnimator();
            Assert.That(failing.ReactivateCount, Is.EqualTo(1));
            Assert.That(passing.ReactivateCount, Is.EqualTo(1));

            Quaternion rotation = Quaternion.Euler(0f, 35f, 0f);
            Vector3 translation = new Vector3(2f, 0.5f, -1f);
            Vector3 pivot = new Vector3(0.25f, 0f, 0.75f);
            LogAssert.Expect(
                LogType.Exception,
                new Regex("expected teleport hook failure"));
            controller.NotifyTeleported(
                rotation,
                translation,
                pivot,
                true);
            Assert.That(failing.TeleportCount, Is.EqualTo(1));
            Assert.That(passing.TeleportCount, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(passing.LastRotation, rotation),
                Is.LessThan(0.001f));
            Assert.That(passing.LastTranslation, Is.EqualTo(translation));
            Assert.That(passing.LastPivot, Is.EqualTo(pivot));
            Assert.That(passing.LastMoveToTarget, Is.True);
        }

        [Test]
        public void D18_MasterPinAndMuscleAreIndependent()
        {
            BoneProfile pinOnly = CreateAuthorityProfile();
            RagdollMasterAuthority.Apply(
                ref pinOnly,
                1f,
                0f,
                1f,
                1f);
            Assert.That(pinOnly.PositionPinWeight, Is.EqualTo(1f));
            Assert.That(pinOnly.rotationAlpha, Is.Zero);
            Assert.That(pinOnly.positionAlpha, Is.EqualTo(4f));

            BoneProfile muscleOnly = CreateAuthorityProfile();
            RagdollMasterAuthority.Apply(
                ref muscleOnly,
                0f,
                1f,
                1f,
                1f);
            Assert.That(muscleOnly.PositionPinWeight, Is.Zero);
            Assert.That(muscleOnly.rotationAlpha, Is.EqualTo(8f));
            Assert.That(muscleOnly.positionAlpha, Is.EqualTo(4f));
        }

        [Test]
        public void D26_UnpinnedVelocityLimitHandlesExtremeValues()
        {
            Vector3 original = new Vector3(3f, 4f, 0f);
            Vector3 clamped =
                RagdollPuppetBehaviourMath.LimitVelocity(original, 2f);
            Assert.That(clamped.magnitude, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(Vector3.Dot(clamped.normalized, original.normalized),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                RagdollPuppetBehaviourMath.LimitVelocity(
                    original,
                    float.PositiveInfinity),
                Is.EqualTo(original));
            Assert.That(
                RagdollPuppetBehaviourMath.LimitVelocity(original, 0f),
                Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void D28_AuthoredZeroPinKnockoutIsConfigurable()
        {
            bool disabled = RagdollPuppetBehaviourMath.ShouldLoseBalance(
                2f,
                1f,
                0f,
                0f,
                1f,
                1f,
                false);
            bool enabled = RagdollPuppetBehaviourMath.ShouldLoseBalance(
                2f,
                1f,
                0f,
                0f,
                1f,
                1f,
                true);
            Assert.That(disabled, Is.False);
            Assert.That(enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator G02_DeterministicExternalSolverRunsAfterMapping()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            GameObject schedulerObject = new GameObject("G02 IK Scheduler");
            schedulerObject.SetActive(false);
            ObservingIKSolver solver =
                schedulerObject.AddComponent<ObservingIKSolver>();
            solver.ObservedTarget = rig.TargetChild;
            RagdollIKScheduler scheduler =
                schedulerObject.AddComponent<RagdollIKScheduler>();
            scheduler.Configure(
                rig.Result.Animator,
                RagdollIKSolvePhase.AfterPhysics,
                new MonoBehaviour[] { solver });
            RagdollBoneHandle child = rig.Bindings.GetHandleAt(1);
            rig.Result.Animator.MasterMappingWeight = 1f;
            rig.Result.Animator.SetBoneMappingWeights(
                child,
                new RagdollMappingWeights(0f, 1f));
            rig.Result.Animator.FixTargetTransforms = false;
            InvokePrivate(rig.Result.Animator, "ReadAnimatedPose", false, false);
            Quaternion before = rig.TargetChild.rotation;
            schedulerObject.SetActive(true);

            rig.ChildBody.rotation = Quaternion.Euler(20f, 65f, -15f);
            RagdollAnimator.AnimatedPair childPair = FindPair(rig, 1);
            childPair.GetMappedTargetWorldPose(
                out _,
                out Quaternion expectedRotation);
            InvokePrivate(rig.Result.Animator, "MapRagdollToTarget");
            Quaternion mapped = rig.TargetChild.rotation;
            Assert.That(Quaternion.Angle(mapped, before), Is.GreaterThan(10f));
            Assert.That(Quaternion.Angle(mapped, expectedRotation),
                Is.LessThan(0.001f));

            Assert.That(solver.SolveCount, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(solver.ObservedRotation, mapped),
                Is.LessThan(0.001f));
            Assert.That(solver.AutomaticUpdates, Is.False);
            schedulerObject.SetActive(false);
            Assert.That(solver.AutomaticUpdates, Is.True);
            UnityEngine.Object.DestroyImmediate(schedulerObject);
        }

        [UnityTest]
        public IEnumerator G04_SolverAndPublicHooksPreserveOrderAndIsolation()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            List<string> trace = new List<string>();
            rig.Result.Animator.OnRead += () =>
                throw new InvalidOperationException("expected read hook failure");
            rig.Result.Animator.OnRead += () => trace.Add("read-after");
            rig.Result.Animator.OnWrite += () =>
                throw new InvalidOperationException("expected write hook failure");
            rig.Result.Animator.OnWrite += () => trace.Add("write-after");

            GameObject schedulerObject = new GameObject("G04 IK Scheduler");
            schedulerObject.SetActive(false);
            OrderedIKSolver first = schedulerObject.AddComponent<OrderedIKSolver>();
            first.Label = "solver-first";
            first.Trace = trace;
            ThrowingOrderedIKSolver failing =
                schedulerObject.AddComponent<ThrowingOrderedIKSolver>();
            failing.Trace = trace;
            OrderedIKSolver last = schedulerObject.AddComponent<OrderedIKSolver>();
            last.Label = "solver-last";
            last.Trace = trace;
            RagdollIKScheduler scheduler =
                schedulerObject.AddComponent<RagdollIKScheduler>();
            scheduler.Configure(
                rig.Result.Animator,
                RagdollIKSolvePhase.BeforePhysics,
                new MonoBehaviour[] { first, failing, last });
            schedulerObject.SetActive(true);

            LogAssert.Expect(LogType.Exception,
                new Regex("expected read hook failure"));
            LogAssert.Expect(LogType.Exception,
                new Regex("expected deterministic IK failure"));
            InvokePrivate(rig.Result.Animator, "InvokeReadHooks");
            LogAssert.Expect(LogType.Exception,
                new Regex("expected write hook failure"));
            InvokePrivate(rig.Result.Animator, "InvokeWriteHooks");

            CollectionAssert.AreEqual(
                new[]
                {
                    "read-after",
                    "solver-first",
                    "solver-failing",
                    "solver-last",
                    "write-after"
                },
                trace);
            Assert.That(first.AutomaticUpdates, Is.False);
            Assert.That(failing.AutomaticUpdates, Is.False);
            Assert.That(last.AutomaticUpdates, Is.False);
            schedulerObject.SetActive(false);
            Assert.That(first.AutomaticUpdates, Is.True);
            Assert.That(failing.AutomaticUpdates, Is.True);
            Assert.That(last.AutomaticUpdates, Is.True);
            UnityEngine.Object.DestroyImmediate(schedulerObject);
        }

        [UnityTest]
        public IEnumerator B04_ActiveKinematicDisabledApplyDistinctPhysicalModes()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;

            Assert.That(rig.Result.Simulation.SetModeImmediate(RagdollSimulationMode.Kinematic), Is.True);
            Assert.That(rig.RootBody.isKinematic, Is.True);
            Assert.That(rig.Puppet.activeSelf, Is.True);
            Assert.That(rig.Result.Animator.IsActive, Is.False);

            Assert.That(rig.Result.Simulation.SetModeImmediate(RagdollSimulationMode.Disabled), Is.True);
            Assert.That(rig.Puppet.activeSelf, Is.False);
            Assert.That(rig.Result.Animator.Mode, Is.EqualTo(RagdollSimulationMode.Disabled));

            Assert.That(rig.Result.Simulation.SetModeImmediate(RagdollSimulationMode.Active), Is.True);
            Assert.That(rig.Puppet.activeSelf, Is.True);
            Assert.That(rig.RootBody.isKinematic, Is.False);
            Assert.That(rig.Result.Animator.IsActive, Is.True);
        }

        [UnityTest]
        public IEnumerator B05_ModeBlendRetainsActiveOwnershipUntilSafeCommit()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            Vector3 before = rig.RootBody.position;

            Assert.That(rig.Result.Simulation.SetMode(
                RagdollSimulationMode.Kinematic, 0.12f), Is.True);
            Assert.That(rig.Result.Animator.IsSwitchingMode, Is.True);
            Assert.That(rig.Result.Animator.IsActive, Is.True,
                "The official isActive contract includes blending in or out of Active.");
            Assert.That(rig.RootBody.isKinematic, Is.False,
                "Kinematic ownership is committed only after the Active fade-out.");
            Assert.That(Vector3.Distance(before, rig.RootBody.position), Is.LessThan(0.05f));

            yield return new WaitForSeconds(0.16f);
            Assert.That(rig.Result.Animator.IsSwitchingMode, Is.False);
            Assert.That(rig.Result.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Kinematic));
            Assert.That(rig.RootBody.isKinematic, Is.True);
        }

        [UnityTest]
        public IEnumerator B06_SharedQualityBudgetFallsBackAndRestoresAfterLifecycleOverride()
        {
            GameObject budgetObject = Own(new GameObject("Quality Budget"));
            RagdollPhysicsQualityBudget budget =
                budgetObject.AddComponent<RagdollPhysicsQualityBudget>();
            budget.MaximumActiveRagdolls = 0;
            RagdollPhysicsQualityProfile quality =
                Own(ScriptableObject.CreateInstance<RagdollPhysicsQualityProfile>());

            RuntimeRig rig = CreateRuntime();
            RagdollPhysicsQualityController controller =
                rig.Target.AddComponent<RagdollPhysicsQualityController>();
            SetField(controller, "profile", quality);
            SetField(controller, "budget", budget);
            yield return null;
            budget.EvaluateNow();
            yield return new WaitForSeconds(0.3f);

            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(controller.BudgetApproved, Is.False);
            Assert.That(rig.Result.Simulation.TargetMode,
                Is.Not.EqualTo(RagdollSimulationMode.Active));

            rig.Result.Animator.Kill(ImmediateLifecycleSettings());
            yield return null;
            Assert.That(rig.Result.Simulation.TargetMode,
                Is.EqualTo(RagdollSimulationMode.Active));
            rig.Result.Animator.Resurrect();
            yield return null;
            controller.RefreshNow();
            yield return new WaitForSeconds(0.3f);
            Assert.That(rig.Result.Simulation.TargetMode,
                Is.Not.EqualTo(RagdollSimulationMode.Active));
        }

        [UnityTest]
        public IEnumerator B07_AliveDeadFrozenLifecycleRestoresPhysicalSimulation()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            RagdollLifecycleSettings settings = ImmediateLifecycleSettings();

            rig.Result.Animator.Kill(settings);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Result.Animator.IsDead, Is.True);
            Assert.That(rig.RootJoint.slerpDrive.positionSpring, Is.EqualTo(0f));

            rig.RootBody.Sleep();
            rig.ChildBody.Sleep();
            rig.Result.Animator.Freeze(settings);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Result.Animator.IsFrozen, Is.True);
            Assert.That(rig.Result.Simulation.IsLifecycleFreezeSuspended, Is.True);

            rig.Result.Animator.Resurrect();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Result.Animator.IsAlive, Is.True);
            Assert.That(rig.Result.Simulation.IsLifecycleFreezeSuspended, Is.False);
            Assert.That(rig.Puppet.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator B11_FixTargetTransformsRestoresBoundDefaultsOnly()
        {
            RuntimeRig rig = CreateRuntime(true);
            yield return null;
            Transform visualOnly = rig.Target.transform.Find("VisualOnly");
            Vector3 visualPose = new Vector3(9f, 8f, 7f);
            visualOnly.localPosition = visualPose;
            rig.TargetChild.localPosition = new Vector3(3f, 4f, 5f);

            rig.Result.Animator.FixTargetTransforms = true;
            InvokePrivate(rig.Result.Animator, "FixTargetTransformsAtUpdateBoundary");

            Assert.That(rig.TargetChild.localPosition,
                Is.EqualTo(Vector3.up));
            Assert.That(visualOnly.localPosition, Is.EqualTo(visualPose));
        }

        [UnityTest]
        public IEnumerator B12_MappingPinMuscleAndDamperRemainIndependent()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;

            rig.Result.Animator.MasterMappingWeight = 0.25f;
            rig.Result.Animator.MasterPinWeight = 0.5f;
            rig.Result.Animator.MasterMuscleWeight = 0f;
            rig.Result.Animator.MasterMuscleDamper = 7f;
            rig.Result.Animator.MasterMuscleDamperMultiplier = 1.5f;
            yield return new WaitForFixedUpdate();

            Assert.That(rig.Result.Animator.MasterMappingWeight, Is.EqualTo(0.25f));
            Assert.That(rig.Result.Animator.MasterPinWeight, Is.EqualTo(0.5f));
            Assert.That(rig.Result.Animator.MasterMuscleWeight, Is.Zero);
            Assert.That(rig.RootJoint.slerpDrive.positionSpring, Is.Zero,
                "Zero muscle authority must not disable the independent pin channel.");
            Assert.That(rig.RootJoint.slerpDrive.positionDamper, Is.GreaterThanOrEqualTo(7f));

            rig.Result.Animator.MasterPinWeight = 0f;
            rig.Result.Animator.MasterMuscleWeight = 1f;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.RootJoint.slerpDrive.positionSpring, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator B13_MuscleSpringAndAbsoluteDamperReachJointDrive()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            rig.Result.Animator.MasterMuscleWeight = 1f;
            rig.Result.Animator.MasterMuscleDamper = 0f;
            yield return new WaitForFixedUpdate();
            JointDrive baseline = rig.RootJoint.slerpDrive;

            rig.Result.Animator.MasterMuscleDamper = 11f;
            yield return new WaitForFixedUpdate();
            JointDrive changed = rig.RootJoint.slerpDrive;
            Assert.That(changed.positionSpring, Is.EqualTo(baseline.positionSpring).Within(0.001f));
            Assert.That(changed.positionDamper - baseline.positionDamper,
                Is.EqualTo(11f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator B14_PinPowShapesOnlyIntermediateRuntimePinAuthority()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            rig.Result.Animator.MasterPinWeight = 0.5f;
            rig.Result.Animator.PinPow = 4f;
            float curved = RagdollPinMath.ResolveCurvedPinWeight(
                rig.Result.Animator.MasterPinWeight, rig.Result.Animator.PinPow);

            Assert.That(curved, Is.EqualTo(0.0625f).Within(0.00001f));
            Assert.That(RagdollPinMath.ResolveCurvedPinWeight(0f, 8f), Is.Zero);
            Assert.That(RagdollPinMath.ResolveCurvedPinWeight(1f, 8f),
                Is.EqualTo(1f));
            Assert.That(rig.Result.Animator.PinSettings.PinPow, Is.EqualTo(4f));
            Assert.That(rig.RootBody, Is.Not.Null,
                "The curve is configured on an initialized physical Puppet.");
        }

        [UnityTest]
        public IEnumerator B15_PinDistanceFalloffReducesPhysicalCorrectionWithDistance()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            rig.Result.Animator.PinDistanceFalloff = 5f;
            Vector3 correction = new Vector3(10f, 0f, 0f);
            Vector3 near = RagdollPinMath.ResolvePositionAcceleration(
                correction, Vector3.right * 0.1f, 1f, 4f,
                rig.Result.Animator.PinDistanceFalloff);
            Vector3 far = RagdollPinMath.ResolvePositionAcceleration(
                correction, Vector3.right * 2f, 1f, 4f,
                rig.Result.Animator.PinDistanceFalloff);

            Assert.That(far.magnitude, Is.LessThan(near.magnitude));
            Assert.That(rig.Result.Animator.PinSettings.PinDistanceFalloff,
                Is.EqualTo(5f));
            Assert.That(rig.Result.Behaviours.Context.Pairs.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator B16_AngularPinningAddsTorqueWithoutChangingMuscleDrive()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            rig.Result.Animator.MasterPinWeight = 1f;
            rig.Result.Animator.MasterMuscleWeight = 0f;
            rig.Result.Animator.AngularPinning = true;
            RagdollAnimator.AnimatedPair rootPair =
                rig.Result.Behaviours.Context.Pairs[0];
            rootPair.currentPose.worldRotation = Quaternion.Euler(0f, 40f, 0f);
            rig.RootBody.angularVelocity = Vector3.zero;

            InvokePrivate(rig.Result.Animator, "DoAnimationMatching", Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();

            Assert.That(rig.RootJoint.slerpDrive.positionSpring, Is.Zero);
            Assert.That(rig.RootBody.angularVelocity.sqrMagnitude, Is.GreaterThan(0.001f));
        }

        [UnityTest]
        public IEnumerator B17_RuntimeAnchorsUpdateAtPhysicsBoundaryAndRestoreAuthoredSnapshot()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            Vector3 authored = rig.ChildJoint.connectedAnchor;
            rig.Result.Animator.UpdateJointAnchors = true;
            rig.Result.Animator.SupportTranslationAnimation = true;
            rig.Result.Animator.FixTargetTransforms = false;
            rig.TargetChild.localPosition += Vector3.right * 0.4f;
            SimulationMode previousSimulationMode = Physics.simulationMode;
            rig.Result.Animator.enabled = false;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                rig.Result.Animator.PrepareManualSimulation(Time.fixedDeltaTime);
                Assert.That(rig.Result.Animator.LastJointAnchorUpdateCount,
                    Is.GreaterThan(0));
                Assert.That(Vector3.Distance(
                    rig.ChildJoint.connectedAnchor, authored),
                    Is.GreaterThan(0.1f));
                Physics.Simulate(Time.fixedDeltaTime);
                rig.Result.Animator.CompleteManualSimulation();
            }
            finally
            {
                Physics.simulationMode = previousSimulationMode;
                rig.Result.Animator.enabled = true;
            }

            rig.Result.Animator.UpdateJointAnchors = false;
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(rig.ChildJoint.connectedAnchor, authored),
                Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator B18_TranslationAnimationMapsPositionWithoutOverwritingRotationChannel()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            RagdollBoneHandle child = rig.Bindings.GetHandleAt(1);
            rig.Result.Animator.SupportTranslationAnimation = true;
            rig.Result.Animator.SetBoneMappingWeights(
                child, new RagdollMappingWeights(1f, 0f));
            Quaternion animatedRotation = Quaternion.Euler(12f, 23f, 34f);
            rig.Result.Animator.FixTargetTransforms = false;
            rig.TargetChild.rotation = animatedRotation;
            InvokePrivate(rig.Result.Animator, "ReadAnimatedPose", false, false);
            rig.ChildBody.position += Vector3.right * 0.6f;
            RagdollAnimator.AnimatedPair childPair = FindPair(rig, 1);
            childPair.GetMappedTargetWorldPose(
                out Vector3 expectedPosition,
                out Quaternion ignoredRotation);

            InvokePrivate(rig.Result.Animator, "MapRagdollToTarget");
            Assert.That(Vector3.Distance(rig.TargetChild.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(rig.TargetChild.rotation, animatedRotation),
                Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator B25_TeleportPreservesMomentumWhileRespawnClearsIt()
        {
            RuntimeRig rig = CreateRuntime();
            yield return null;
            rig.RootBody.linearVelocity = new Vector3(1f, 2f, 3f);
            rig.ChildBody.angularVelocity = new Vector3(2f, 1f, 0.5f);
            Vector3 velocity = rig.RootBody.linearVelocity;

            rig.Result.Animator.TeleportImmediate(
                new Vector3(4f, 2f, -3f), Quaternion.Euler(0f, 70f, 0f), false);
            Assert.That(Vector3.Distance(rig.RootBody.linearVelocity, velocity),
                Is.LessThan(0.0001f));
            Assert.That(rig.Result.Animator.HasPendingTeleport, Is.False);

            rig.Result.PuppetBehaviour.Respawn(
                new Vector3(-2f, 1f, 5f), Quaternion.Euler(0f, -30f, 0f));
            Assert.That(rig.Result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.Puppet));
            Assert.That(rig.RootBody.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(rig.ChildBody.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(Vector3.Distance(rig.Target.transform.position,
                new Vector3(-2f, 1f, 5f)), Is.LessThan(0.001f));
        }

        RuntimeRig CreateRuntime(
            bool addUnboundTarget = false,
            bool addPipelineProbe = false)
        {
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");
            GameObject puppet = Own(new GameObject("Physical Puppet"));
            puppet.SetActive(false);
            GameObject physicalChild = new GameObject("Child");
            physicalChild.transform.SetParent(puppet.transform, false);
            physicalChild.transform.localPosition = Vector3.up;

            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            rootBody.useGravity = false;
            ConfigurableJoint rootJoint = puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();
            Rigidbody childBody = physicalChild.AddComponent<Rigidbody>();
            childBody.useGravity = false;
            ConfigurableJoint childJoint = physicalChild.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            childJoint.autoConfigureConnectedAnchor = false;
            childJoint.anchor = Vector3.zero;
            childJoint.connectedAnchor = Vector3.up;
            physicalChild.AddComponent<BoxCollider>();

            RagdollDefinition definition =
                Own(ScriptableObject.CreateInstance<RagdollDefinition>());
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            RagdollDefinitionBindings bindings =
                puppet.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            ConfigurePhysicalSettings(
                puppet,
                definition,
                rootName,
                childName);
            puppet.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            // Legacy fallback is intentionally exercised here; PuppetMaster-style
            // dual rigs may be separate objects, but matching Transform names remain
            // the fallback binding contract when no explicit TargetBindings asset exists.
            GameObject target = Own(new GameObject("Physical Puppet"));
            GameObject targetChild = new GameObject("Child");
            targetChild.transform.SetParent(target.transform, false);
            targetChild.transform.localPosition = Vector3.up;
            if (addUnboundTarget)
            {
                GameObject visualOnly = new GameObject("VisualOnly");
                visualOnly.transform.SetParent(target.transform, false);
            }
            if (addPipelineProbe)
            {
                Animator animator = target.AddComponent<Animator>();
#if UNITY_6000_0_OR_NEWER
                animator.updateMode = AnimatorUpdateMode.Fixed;
#else
                animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
#endif
                target.AddComponent<PipelineProbe>();
            }

            RagdollAnimationProfile profile =
                Own(ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    target.transform, bindings, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            // The profile explicitly authors both muscles as Powered. Reasserting the
            // physical state here also protects the fixture from Unity applying a stale
            // kinematic value while the inactive hierarchy is being enabled.
            bindings.Root.PowerSetting = PowerSetting.Powered;
            bindings.GetBoneAt(1).PowerSetting = PowerSetting.Powered;
            rootBody.isKinematic = false;
            childBody.isKinematic = false;
            return new RuntimeRig
            {
                Puppet = puppet,
                Target = target,
                TargetChild = targetChild.transform,
                Bindings = bindings,
                RootBody = rootBody,
                ChildBody = childBody,
                RootJoint = rootJoint,
                ChildJoint = childJoint,
                Result = result
            };
        }

        T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        void ConfigurePhysicalSettings(
            GameObject puppet,
            RagdollDefinition definition,
            BoneName root,
            BoneName child)
        {
            RagdollPowerProfile power =
                Own(ScriptableObject.CreateInstance<RagdollPowerProfile>());
            SetField(power, "definition", definition);
            SetField(power, "_isValid", true);
            SetField(power, "settings", CreateProfileDictionary(
                typeof(RagdollPowerProfile),
                "PowerSettingsDictionary",
                typeof(PowerSetting),
                root,
                PowerSetting.Powered,
                child,
                PowerSetting.Powered));

            RagdollWeightDistribution weights =
                Own(ScriptableObject.CreateInstance<RagdollWeightDistribution>());
            SetField(weights, "definition", definition);
            SetField(weights, "_isValid", true);
            SetField(weights, "factors", CreateProfileDictionary(
                typeof(RagdollWeightDistribution),
                "WeightDistributionDictionary",
                typeof(float),
                root,
                0.5f,
                child,
                0.5f));

            RagdollSettings settings = puppet.AddComponent<RagdollSettings>();
            settings.useGravity = false;
            SetField(settings, "_powerProfile", power);
            SetField(settings, "_weightDistribution", weights);
        }

        static object CreateProfileDictionary(
            Type ownerType,
            string nestedTypeName,
            Type valueType,
            BoneName firstKey,
            object firstValue,
            BoneName secondKey,
            object secondValue)
        {
            Type type = ownerType.GetNestedType(
                nestedTypeName, BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), valueType },
                null);
            Assert.That(add, Is.Not.Null, nestedTypeName + ".Add");
            add.Invoke(dictionary, new[] { (object)firstKey, firstValue });
            add.Invoke(dictionary, new[] { (object)secondKey, secondValue });
            return dictionary;
        }

        static RagdollLifecycleSettings ImmediateLifecycleSettings()
        {
            return new RagdollLifecycleSettings(
                0f,
                0f,
                0f,
                0.02f,
                false,
                true,
                true);
        }

        static BoneProfile CreateAuthorityProfile()
        {
            return new BoneProfile
            {
                positionAlpha = 4f,
                positionDampingRatio = 2f,
                rotationAlpha = 8f,
                rotationDampingRatio = 3f
            };
        }

        static RagdollPuppetEvent CreatePuppetEvent(
            UnityEngine.Events.UnityAction action)
        {
            RagdollPuppetEvent result = new RagdollPuppetEvent();
            result.UnityEvent.AddListener(action);
            return result;
        }

        static object CreateBindings(
            BoneName root,
            ConfigurableJoint rootJoint,
            BoneName child,
            ConfigurableJoint childJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary", BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod("Add", BindingFlags.Instance |
                BindingFlags.Public, null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) }, null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { child, childJoint });
            return dictionary;
        }

        static void SetField(object target, string name, object value)
        {
            Type current = target.GetType();
            FieldInfo field = null;
            while (current != null && field == null)
            {
                field = current.GetField(
                    name,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly);
                current = current.BaseType;
            }
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        static RagdollAnimator.AnimatedPair FindPair(RuntimeRig rig, int handleIndex)
        {
            for (int index = 0;
                index < rig.Result.Behaviours.Context.Pairs.Count;
                index++)
            {
                RagdollAnimator.AnimatedPair pair =
                    rig.Result.Behaviours.Context.Pairs[index];
                if (pair.Handle.Index == handleIndex) return pair;
            }
            Assert.Fail("No animated pair exists for handle index " + handleIndex + ".");
            return null;
        }

        static void InvokePrivate(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, args);
        }

        sealed class RuntimeRig
        {
            internal GameObject Puppet;
            internal GameObject Target;
            internal Transform TargetChild;
            internal RagdollDefinitionBindings Bindings;
            internal Rigidbody RootBody;
            internal Rigidbody ChildBody;
            internal ConfigurableJoint RootJoint;
            internal ConfigurableJoint ChildJoint;
            internal RagdollSetupResult Result;
        }

        sealed class PipelineProbe : MonoBehaviour, IBoneProfileModifier
        {
            internal readonly List<string> Trace = new List<string>();

            public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
            {
            }

            public void Modify(
                ref BoneProfile boneProfile,
                RagdollAnimator.AnimatedPair pair,
                float deltaTime)
            {
                if (!Trace.Contains("matching")) Trace.Add("matching");
            }
        }

        sealed class BehaviourHookProbe : RagdollBehaviourBase
        {
            internal bool Throw;
            internal int ReactivateCount;
            internal int TeleportCount;
            internal Quaternion LastRotation;
            internal Vector3 LastTranslation;
            internal Vector3 LastPivot;
            internal bool LastMoveToTarget;

            protected override void OnBehaviourReactivated()
            {
                ReactivateCount++;
                if (Throw)
                {
                    throw new InvalidOperationException(
                        "expected reactivation hook failure");
                }
            }

            protected override void OnBehaviourTeleported(
                Quaternion deltaRotation,
                Vector3 deltaPosition,
                Vector3 pivot,
                bool moveToTarget)
            {
                TeleportCount++;
                LastRotation = deltaRotation;
                LastTranslation = deltaPosition;
                LastPivot = pivot;
                LastMoveToTarget = moveToTarget;
                if (Throw)
                {
                    throw new InvalidOperationException(
                        "expected teleport hook failure");
                }
            }
        }

        sealed class ObservingIKSolver : MonoBehaviour, IRagdollIKSolver
        {
            internal Transform ObservedTarget;
            internal int SolveCount;
            internal Quaternion ObservedRotation;
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;

            public void Solve()
            {
                SolveCount++;
                ObservedRotation = ObservedTarget.rotation;
            }
        }

        sealed class OrderedIKSolver : MonoBehaviour, IRagdollIKSolver
        {
            internal string Label;
            internal List<string> Trace;
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;
            public void Solve() => Trace.Add(Label);
        }

        sealed class ThrowingOrderedIKSolver : MonoBehaviour, IRagdollIKSolver
        {
            internal List<string> Trace;
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;

            public void Solve()
            {
                Trace.Add("solver-failing");
                throw new InvalidOperationException(
                    "expected deterministic IK failure");
            }
        }
    }
}
