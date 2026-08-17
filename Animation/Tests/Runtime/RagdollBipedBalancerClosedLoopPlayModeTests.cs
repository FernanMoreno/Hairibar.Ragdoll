using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Paired closed-loop evidence for the reactive balancer. The test deliberately
    /// reports the A/B metrics instead of certifying a universal improvement: the
    /// formal accept/reject policy belongs to RagdollLabComparison.
    /// </summary>
    public sealed class RagdollBipedBalancerClosedLoopPlayModeTests
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
        public IEnumerator ClosedLoopBalancer_ReportsPairedOffOnMetrics()
        {
            BalancerRun baseline = null;
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, freezeBodies: false);
            yield return RunPairedScenario(rig, enabled: false, completed: result => baseline = result);
            rig.Dispose();
            rig = null;

            BalancerRun candidate = null;
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, freezeBodies: false);
            yield return RunPairedScenario(rig, enabled: true, completed: result => candidate = result);

            Assert.That(baseline, Is.Not.Null);
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.impulse, Is.EqualTo(baseline.impulse).Within(0.0001f));
            Assert.That(candidate.maxTorque, Is.GreaterThan(0f),
                "The enabled run must expose an observable reactive torque output.");
            Assert.That(candidate.unpinnedObserved, Is.False);
            Assert.That(candidate.stepRequiredObserved, Is.False,
                "The paired benchmark uses a recoverable push and must not enter Stagger.");
            Assert.That(candidate.minimumMargin, Is.GreaterThanOrEqualTo(baseline.minimumMargin - 0.10f),
                "Balancer candidate must not create a materially worse margin deficit before tuning is accepted.");

            TestContext.WriteLine(
                $"Balancer A/B: impulse={baseline.impulse:F5}, " +
                $"offMinMargin={baseline.minimumMargin:F5}, onMinMargin={candidate.minimumMargin:F5}, " +
                $"offFinalMargin={baseline.finalMargin:F5}, onFinalMargin={candidate.finalMargin:F5}, " +
                $"offMaxComSpeed={baseline.maxComSpeed:F5}, onMaxComSpeed={candidate.maxComSpeed:F5}, " +
                $"onMaxTorque={candidate.maxTorque:F5}, " +
                $"onTorqueFrames={candidate.torqueFrames}");
        }

        [UnityTest]
        public IEnumerator ClosedLoopBalancer_MultiScenarioMatrixReportsFinitePairedMetrics()
        {
            // The matrix varies support width and lateral impact direction while
            // keeping the calibrated fixture lifecycle identical. This is a
            // bounded physical benchmark, not a universal tuning claim.
            float[] supportWidths = { 0.45f, 0.50f, 0.55f, 0.50f };
            float[] impulseSigns = { -1f, -1f, -1f, 1f };
            int pairedRuns = 0;
            bool anyTorqueObserved = false;

            for (int index = 0; index < supportWidths.Length; index++)
            {
                float supportWidth = supportWidths[index];
                float impulseSign = impulseSigns[index];
                string impactDirection = impulseSign < 0f ? "Left" : "Right";
                string scenarioName =
                    $"SupportWidth-{supportWidth:F2}-{impactDirection}";
                BalancerRun baseline = null;
                rig = new StaggerPhysicalRig(footOffsetX: supportWidth, freezeBodies: false);
                yield return RunPairedScenario(
                    rig,
                    enabled: false,
                    desiredMargin: 0.08f,
                    impulseSign: impulseSign,
                    scenarioName: scenarioName,
                    completed: result => baseline = result);
                rig.Dispose();
                rig = null;

                BalancerRun candidate = null;
                rig = new StaggerPhysicalRig(footOffsetX: supportWidth, freezeBodies: false);
                yield return RunPairedScenario(
                    rig,
                    enabled: true,
                    desiredMargin: 0.08f,
                    impulseSign: impulseSign,
                    scenarioName: scenarioName,
                    completed: result => candidate = result);

                Assert.That(baseline, Is.Not.Null);
                Assert.That(candidate, Is.Not.Null);
                Assert.That(candidate.scenarioName, Is.EqualTo(baseline.scenarioName));
                Assert.That(candidate.impulse, Is.EqualTo(baseline.impulse).Within(0.0001f));
                Assert.That(IsFinite(baseline.minimumMargin), Is.True);
                Assert.That(IsFinite(baseline.finalMargin), Is.True);
                Assert.That(IsFinite(baseline.maxComSpeed), Is.True);
                Assert.That(IsFinite(baseline.maxTorque), Is.True);
                Assert.That(IsFinite(candidate.minimumMargin), Is.True);
                Assert.That(IsFinite(candidate.finalMargin), Is.True);
                Assert.That(IsFinite(candidate.maxComSpeed), Is.True);
                Assert.That(IsFinite(candidate.maxTorque), Is.True);
                Assert.That(candidate.unpinnedObserved, Is.False);
                Assert.That(candidate.stepRequiredObserved, Is.False,
                    "The bounded Balancer matrix must not turn its recoverable impulse into Stagger.");
                anyTorqueObserved |= candidate.torqueFrames > 0;
                pairedRuns++;

                TestContext.WriteLine(
                    $"Balancer matrix {candidate.scenarioName}: " +
                    $"impulse={candidate.impulse:F5}, " +
                    $"offMinMargin={baseline.minimumMargin:F5}, " +
                    $"onMinMargin={candidate.minimumMargin:F5}, " +
                    $"offFinalMargin={baseline.finalMargin:F5}, " +
                    $"onFinalMargin={candidate.finalMargin:F5}, " +
                    $"onMaxTorque={candidate.maxTorque:F5}, " +
                    $"onTorqueFrames={candidate.torqueFrames}");
                rig.Dispose();
                rig = null;
            }

            Assert.That(pairedRuns, Is.EqualTo(4));
            Assert.That(anyTorqueObserved, Is.True,
                "At least one physical matrix case must observe balancer torque.");
        }

        [UnityTest]
        public IEnumerator StressMatrix_FixedTimestepAndMassRatio_ReportsFinitePairedRuns()
        {
            float originalFixedDeltaTime = Time.fixedDeltaTime;
            float[] fixedDeltaTimes = { 0.01f, 0.02f, 1f / 30f };
            StressMassCase[] massCases =
            {
                new StressMassCase("Nominal", 1f, 1f, 1f),
                new StressMassCase("RootHeavy", 1.15f, 0.85f, 1.15f),
                new StressMassCase("FeetHeavy", 0.90f, 1.10f, 0.90f)
            };
            int pairedRuns = 0;

            try
            {
                for (int timeIndex = 0; timeIndex < fixedDeltaTimes.Length; timeIndex++)
                {
                    float fixedDeltaTime = fixedDeltaTimes[timeIndex];
                    Assert.That(IsFinite(fixedDeltaTime) && fixedDeltaTime > 0f, Is.True);
                    Time.fixedDeltaTime = fixedDeltaTime;

                    for (int massIndex = 0; massIndex < massCases.Length; massIndex++)
                    {
                        StressMassCase massCase = massCases[massIndex];
                        string scenarioName =
                            $"Stress-dt{fixedDeltaTime:F5}-{massCase.name}";
                        BalancerRun baseline = null;
                        rig = new StaggerPhysicalRig(
                            footOffsetX: 0.5f,
                            freezeBodies: false,
                            rootMassScale: massCase.rootMassScale,
                            footMassScale: massCase.footMassScale,
                            inertiaScale: massCase.inertiaScale);
                        yield return RunPairedScenario(
                            rig,
                            enabled: false,
                            desiredMargin: 0.08f,
                            scenarioName: scenarioName,
                            completed: result => baseline = result);
                        rig.Dispose();
                        rig = null;

                        BalancerRun candidate = null;
                        rig = new StaggerPhysicalRig(
                            footOffsetX: 0.5f,
                            freezeBodies: false,
                            rootMassScale: massCase.rootMassScale,
                            footMassScale: massCase.footMassScale,
                            inertiaScale: massCase.inertiaScale);
                        yield return RunPairedScenario(
                            rig,
                            enabled: true,
                            desiredMargin: 0.08f,
                            scenarioName: scenarioName,
                            completed: result => candidate = result);

                        Assert.That(baseline, Is.Not.Null);
                        Assert.That(candidate, Is.Not.Null);
                        Assert.That(candidate.scenarioName, Is.EqualTo(baseline.scenarioName));
                        Assert.That(candidate.impulse, Is.EqualTo(baseline.impulse).Within(0.0001f));
                        Assert.That(IsFinite(baseline.minimumMargin), Is.True);
                        Assert.That(IsFinite(baseline.finalMargin), Is.True);
                        Assert.That(IsFinite(baseline.maxComSpeed), Is.True);
                        Assert.That(IsFinite(baseline.maxTorque), Is.True);
                        Assert.That(IsFinite(candidate.minimumMargin), Is.True);
                        Assert.That(IsFinite(candidate.finalMargin), Is.True);
                        Assert.That(IsFinite(candidate.maxComSpeed), Is.True);
                        Assert.That(IsFinite(candidate.maxTorque), Is.True);
                        Assert.That(candidate.unpinnedObserved, Is.False,
                            $"Stress cell {scenarioName} entered Unpinned.");
                        Assert.That(candidate.stepRequiredObserved, Is.False,
                            $"Stress cell {scenarioName} entered RequiresStep.");

                        TestContext.WriteLine(
                            $"Stress matrix {scenarioName}: " +
                            $"dt={fixedDeltaTime:F5}, " +
                            $"rootMass={massCase.rootMassScale:F3}, " +
                            $"footMass={massCase.footMassScale:F3}, " +
                            $"inertia={massCase.inertiaScale:F3}, " +
                            $"impulse={candidate.impulse:F5}, " +
                            $"offMinMargin={baseline.minimumMargin:F5}, " +
                            $"onMinMargin={candidate.minimumMargin:F5}, " +
                            $"offFinalMargin={baseline.finalMargin:F5}, " +
                            $"onFinalMargin={candidate.finalMargin:F5}, " +
                            $"onMaxComSpeed={candidate.maxComSpeed:F5}, " +
                            $"onMaxTorque={candidate.maxTorque:F5}, " +
                            $"onTorqueFrames={candidate.torqueFrames}");
                        pairedRuns++;
                        rig.Dispose();
                        rig = null;
                    }
                }
            }
            finally
            {
                if (rig != null)
                {
                    rig.Dispose();
                    rig = null;
                }

                Time.fixedDeltaTime = originalFixedDeltaTime;
            }

            Assert.That(pairedRuns, Is.EqualTo(fixedDeltaTimes.Length * massCases.Length));
        }

        [UnityTest]
        public IEnumerator PhysicalContextMatrix_ReportsPairedOffOnMetrics()
        {
            float originalFixedDeltaTime = Time.fixedDeltaTime;
            Vector3 originalGravity = Physics.gravity;
            float[] fixedDeltaTimes = { 0.02f, 1f / 30f };
            StressMassCase[] massCases =
            {
                new StressMassCase("Nominal", 1f, 1f, 1f),
                new StressMassCase("RootHeavy", 1.15f, 0.85f, 1.15f)
            };
            PhysicalScenario[] scenarios =
            {
                new PhysicalScenario("Slope-15-Left", Vector3.up,
                    SlopeNormal(15f), false, false, 1, -1f),
                new PhysicalScenario("Slope-30-Right", Vector3.up,
                    SlopeNormal(30f), false, false, 1, 1f),
                new PhysicalScenario("Gravity-Right-Left", Vector3.right,
                    Vector3.right, false, false, 1, -1f),
                new PhysicalScenario("Gravity-Diagonal-Right",
                    new Vector3(1f, 1f, 0f).normalized,
                    new Vector3(1f, 1f, 0f).normalized,
                    false, false, 1, 1f),
                new PhysicalScenario("MovingPlatform-Left", Vector3.up,
                    Vector3.up, true, false, 1, -1f),
                new PhysicalScenario("PartialContact-Right", Vector3.up,
                    Vector3.up, false, true, 1, 1f),
                new PhysicalScenario("ConsecutivePushes-Left", Vector3.up,
                    Vector3.up, false, false, 3, -1f)
            };
            int pairedCells = 0;

            try
            {
                for (int scenarioIndex = 0; scenarioIndex < scenarios.Length; scenarioIndex++)
                {
                    PhysicalScenario scenario = scenarios[scenarioIndex];
                    Physics.gravity = -scenario.gravityUp * 9.81f;

                    for (int timeIndex = 0; timeIndex < fixedDeltaTimes.Length; timeIndex++)
                    {
                        Time.fixedDeltaTime = fixedDeltaTimes[timeIndex];

                        for (int massIndex = 0; massIndex < massCases.Length; massIndex++)
                        {
                            StressMassCase massCase = massCases[massIndex];
                            string cellName =
                                $"{scenario.name}-dt{Time.fixedDeltaTime:F5}-{massCase.name}";
                            BalancerRun baseline = null;
                            yield return RunPhysicalBenchmarkSide(
                                scenario,
                                massCase,
                                enabled: false,
                                cellName: cellName,
                                completed: result => baseline = result);

                            BalancerRun candidate = null;
                            yield return RunPhysicalBenchmarkSide(
                                scenario,
                                massCase,
                                enabled: true,
                                cellName: cellName,
                                completed: result => candidate = result);

                            AssertPairedPhysicalMetrics(cellName, baseline, candidate);
                            TestContext.WriteLine(
                                $"Balancer physical matrix {cellName}: " +
                                $"offMinMargin={baseline.minimumMargin:F5}, " +
                                $"onMinMargin={candidate.minimumMargin:F5}, " +
                                $"offCom={baseline.maxComDisplacement:F5}, " +
                                $"onCom={candidate.maxComDisplacement:F5}, " +
                                $"offTorque={baseline.maxTorque:F5}, " +
                                $"onTorque={candidate.maxTorque:F5}, " +
                                $"offAngularVelocity={baseline.maxAngularVelocity:F5}, " +
                                $"onAngularVelocity={candidate.maxAngularVelocity:F5}, " +
                                $"offSlip={baseline.maxFootSlip:F5}, " +
                                $"onSlip={candidate.maxFootSlip:F5}, " +
                                $"offRecovery={baseline.recoveryTime:F5}, " +
                                $"onRecovery={candidate.recoveryTime:F5}, " +
                                $"offRequiresStep={baseline.requiresStepSamples}, " +
                                $"onRequiresStep={candidate.requiresStepSamples}");
                            pairedCells++;
                        }
                    }
                }
            }
            finally
            {
                if (rig != null)
                {
                    rig.Dispose();
                    rig = null;
                }

                Physics.gravity = originalGravity;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }

            Assert.That(pairedCells, Is.EqualTo(
                scenarios.Length * fixedDeltaTimes.Length * massCases.Length));
        }

        IEnumerator RunPhysicalBenchmarkSide(
            PhysicalScenario scenario,
            StressMassCase massCase,
            bool enabled,
            string cellName,
            Action<BalancerRun> completed)
        {
            GameObject partialPlatform = null;
            try
            {
                rig = new StaggerPhysicalRig(
                    footOffsetX: 0.5f,
                    freezeBodies: false,
                    rootMassScale: massCase.rootMassScale,
                    footMassScale: massCase.footMassScale,
                    inertiaScale: massCase.inertiaScale,
                    gravityUp: scenario.gravityUp,
                    groundNormal: scenario.groundNormal,
                    movingGround: scenario.movingPlatform,
                    allowFootRotation: true);

                if (scenario.partialContact)
                    partialPlatform = ConfigurePartialContact(rig);

                yield return RunPairedScenario(
                    rig,
                    enabled,
                    completed,
                    desiredMargin: 0.08f,
                    impulseSign: scenario.impulseSign,
                    scenarioName: cellName,
                    movingPlatform: scenario.movingPlatform,
                    totalPushes: scenario.totalPushes);
            }
            finally
            {
                if (partialPlatform)
                    UnityEngine.Object.DestroyImmediate(partialPlatform);
                if (rig != null)
                {
                    rig.Dispose();
                    rig = null;
                }
            }
        }

        static GameObject ConfigurePartialContact(StaggerPhysicalRig scenarioRig)
        {
            scenarioRig.GroundCollider.enabled = false;
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Balancer Partial Contact Ground";
            platform.transform.position = new Vector3(-0.25f, -1.15f, 0f);
            platform.transform.localScale = new Vector3(0.75f, 0.1f, 10f);
            platform.layer = 0;
            Collider collider = platform.GetComponent<Collider>();
            scenarioRig.LeftFootBody.GetComponent<GroundContactProbe>()
                .ExpectedGround = collider;
            scenarioRig.RightFootBody.GetComponent<GroundContactProbe>()
                .ExpectedGround = collider;
            return platform;
        }

        static void AssertPairedPhysicalMetrics(
            string cellName,
            BalancerRun baseline,
            BalancerRun candidate)
        {
            Assert.That(baseline, Is.Not.Null, cellName);
            Assert.That(candidate, Is.Not.Null, cellName);
            Assert.That(candidate.scenarioName, Is.EqualTo(baseline.scenarioName));
            Assert.That(candidate.impulse, Is.EqualTo(baseline.impulse).Within(0.0001f));
            Assert.That(IsFinite(baseline.minimumMargin), Is.True, cellName);
            Assert.That(IsFinite(candidate.minimumMargin), Is.True, cellName);
            Assert.That(IsFinite(baseline.finalMargin), Is.True, cellName);
            Assert.That(IsFinite(candidate.finalMargin), Is.True, cellName);
            Assert.That(IsFinite(baseline.maxComDisplacement), Is.True, cellName);
            Assert.That(IsFinite(candidate.maxComDisplacement), Is.True, cellName);
            Assert.That(IsFinite(baseline.maxTorque), Is.True, cellName);
            Assert.That(IsFinite(candidate.maxTorque), Is.True, cellName);
            Assert.That(IsFinite(baseline.maxAngularVelocity), Is.True, cellName);
            Assert.That(IsFinite(candidate.maxAngularVelocity), Is.True, cellName);
            Assert.That(IsFinite(baseline.maxFootSlip), Is.True, cellName);
            Assert.That(IsFinite(candidate.maxFootSlip), Is.True, cellName);
            Assert.That(IsFinite(baseline.recoveryTime), Is.True, cellName);
            Assert.That(IsFinite(candidate.recoveryTime), Is.True, cellName);
            Assert.That(baseline.requiresStepSamples, Is.GreaterThanOrEqualTo(0));
            Assert.That(candidate.requiresStepSamples, Is.GreaterThanOrEqualTo(0));
        }

        static IEnumerator RunPairedScenario(
            StaggerPhysicalRig scenarioRig,
            bool enabled,
            Action<BalancerRun> completed,
            float desiredMargin = 0.08f,
            float impulseSign = -1f,
            string scenarioName = "Balancer",
            bool movingPlatform = false,
            int totalPushes = 1)
        {
            scenarioRig.RootBody.constraints = RigidbodyConstraints.FreezeRotation;
            scenarioRig.Puppet.CanStagger = false;
            scenarioRig.Puppet.LoseBalanceOnTargetDrift = false;
            // This minimal three-bone fixture has feet but no authored calf
            // bones. Bind the Balancer's lower-leg channels to those physical
            // foot bodies for this fixture only; production rigs retain their
            // default calf_l/calf_r mapping.
            StaggerPhysicalRig.SetField(
                scenarioRig.Puppet, "balancerLeftCalfBone", new BoneName("foot_l"));
            StaggerPhysicalRig.SetField(
                scenarioRig.Puppet, "balancerRightCalfBone", new BoneName("foot_r"));
            RagdollBipedBalancerSettings settings = RagdollBipedBalancerSettings.Default;
            settings.TorqueMlp = enabled ? 1f : 0f;
            settings.MaxForceMlp = 0.05f;
            settings.MaxTorqueMag = 45f;
            scenarioRig.Puppet.BalancerSettings = settings;
            scenarioRig.Stagger.StableMargin = 0.05f;

            yield return new WaitForFixedUpdate();
            RagdollGroundingSnapshot before = scenarioRig.Puppet.CenterOfMass.Snapshot;
            // The physical fixture loses roughly 0.04 m of margin during the
            // first driven ticks. Aim inside the recoverable band rather than
            // at its lower boundary, otherwise the run becomes RequiresStep
            // before the balancer gets a chance to act.
            float impulse = CalibratedRecoverableImpulse(
                scenarioRig, before, desiredMargin, impulseSign);
            Vector3 impulseDirection = ResolveLateralDirection(
                scenarioRig, impulseSign);
            scenarioRig.RootBody.AddForce(
                impulseDirection * impulse, ForceMode.Impulse);
            var result = new BalancerRun
            {
                impulse = impulse,
                minimumMargin = float.PositiveInfinity,
                recoveryTime = -1f,
                scenarioName = scenarioName
            };
            Vector3 previousLeftFootPosition = scenarioRig.LeftFootBody.position;
            Vector3 previousRightFootPosition = scenarioRig.RightFootBody.position;
            bool previousLeftGrounded = false;
            bool previousRightGrounded = false;
            int stableSamples = 0;
            int additionalPushes = 0;

            for (int tick = 0; tick < 50; tick++)
            {
                if (movingPlatform)
                    scenarioRig.MoveGround(new Vector3(0.002f, 0f, 0f));
                if (additionalPushes < totalPushes - 1 && tick > 0 && tick % 12 == 0)
                {
                    float nextImpulseSign = additionalPushes % 2 == 0
                        ? -impulseSign
                        : impulseSign;
                    scenarioRig.RootBody.AddForce(
                        ResolveLateralDirection(scenarioRig, nextImpulseSign) * impulse,
                        ForceMode.Impulse);
                    additionalPushes++;
                }

                yield return new WaitForFixedUpdate();
                float margin = CaptureMargin(scenarioRig);
                RagdollGroundingSnapshot snapshot = scenarioRig.Puppet.CenterOfMass.Snapshot;
                result.minimumMargin = Mathf.Min(result.minimumMargin, margin);
                result.finalMargin = margin;
                result.maxComSpeed = Mathf.Max(result.maxComSpeed, snapshot.CenterOfMassVelocity.magnitude);
                result.maxComDisplacement = Mathf.Max(
                    result.maxComDisplacement,
                    Vector3.Distance(snapshot.CenterOfMass, before.CenterOfMass));
                result.maxAngularVelocity = Mathf.Max(
                    result.maxAngularVelocity,
                    scenarioRig.RootBody.angularVelocity.magnitude,
                    scenarioRig.LeftFootBody.angularVelocity.magnitude,
                    scenarioRig.RightFootBody.angularVelocity.magnitude);
                UpdateFootSlip(
                    result,
                    scenarioRig.LeftFootBody,
                    scenarioRig.LeftFootBody.GetComponent<GroundContactProbe>(),
                    ref previousLeftFootPosition,
                    ref previousLeftGrounded,
                    snapshot.EffectiveUp);
                UpdateFootSlip(
                    result,
                    scenarioRig.RightFootBody,
                    scenarioRig.RightFootBody.GetComponent<GroundContactProbe>(),
                    ref previousRightFootPosition,
                    ref previousRightGrounded,
                    snapshot.EffectiveUp);
                result.maxTorque = Mathf.Max(result.maxTorque,
                    scenarioRig.Puppet.LastReactiveBalancerTorque.magnitude);
                if (scenarioRig.Puppet.LastReactiveBalancerApplied) result.torqueFrames++;
                result.unpinnedObserved |= scenarioRig.Puppet.State == RagdollPuppetState.Unpinned;
                RagdollBipedBalanceState classification =
                    RagdollBipedBalanceMath.Classify(
                        margin,
                        scenarioRig.Stagger.StableMargin,
                        scenarioRig.Stagger.RequiresStepMargin);
                if (classification == RagdollBipedBalanceState.RequiresStep)
                {
                    result.requiresStepSamples++;
                    result.stepRequiredObserved = true;
                }

                bool recovered = snapshot.IsGrounded
                    && margin >= 0f
                    && classification != RagdollBipedBalanceState.RequiresStep
                    && scenarioRig.Puppet.State != RagdollPuppetState.Unpinned;
                stableSamples = recovered ? stableSamples + 1 : 0;
                if (result.recoveryTime < 0f && stableSamples >= 3)
                    result.recoveryTime = (tick + 1) * Time.fixedDeltaTime;
            }

            Assert.That(float.IsNaN(result.minimumMargin) || float.IsInfinity(result.minimumMargin), Is.False);
            Assert.That(float.IsNaN(result.finalMargin) || float.IsInfinity(result.finalMargin), Is.False);
            completed(result);
        }

        static float CaptureMargin(StaggerPhysicalRig scenarioRig)
        {
            RagdollGroundingSnapshot snapshot = scenarioRig.Puppet.CenterOfMass.Snapshot;
            Vector3 supportUp = snapshot.EffectiveUpAvailable
                ? snapshot.EffectiveUp
                : -Physics.gravity.normalized;
            Vector3 velocity = snapshot.HasRelativeMotion
                ? snapshot.RelativeCenterOfMassVelocity
                : snapshot.CenterOfMassVelocity;
            return RagdollBipedBalanceMath.SignedCaptureMargin(
                snapshot.CenterOfMass,
                velocity,
                scenarioRig.LeftFootBody.worldCenterOfMass,
                scenarioRig.RightFootBody.worldCenterOfMass,
                scenarioRig.Stagger.PendulumLength,
                Physics.gravity.magnitude,
                scenarioRig.Stagger.SupportRadius,
                supportUp);
        }

        static float CalibratedRecoverableImpulse(
            StaggerPhysicalRig scenarioRig,
            RagdollGroundingSnapshot snapshot,
            float desiredMargin,
            float impulseSign)
        {
            Vector3 lateral = ResolveLateralDirection(scenarioRig, 1f);
            float leftEdge = Mathf.Min(
                Vector3.Dot(scenarioRig.LeftFootBody.worldCenterOfMass, lateral),
                Vector3.Dot(scenarioRig.RightFootBody.worldCenterOfMass, lateral));
            float rightEdge = Mathf.Max(
                Vector3.Dot(scenarioRig.LeftFootBody.worldCenterOfMass, lateral),
                Vector3.Dot(scenarioRig.RightFootBody.worldCenterOfMass, lateral));
            float direction = impulseSign < 0f ? -1f : 1f;
            float edge = direction < 0f ? leftEdge : rightEdge;
            float targetCapturePoint = edge
                + direction * (scenarioRig.Stagger.SupportRadius - desiredMargin);
            float omega = Mathf.Sqrt(Mathf.Max(0.01f, Physics.gravity.magnitude)
                / Mathf.Max(0.05f, scenarioRig.Stagger.PendulumLength));
            float comCoordinate = Vector3.Dot(snapshot.CenterOfMass, lateral);
            float requiredComSpeed = Mathf.Abs(targetCapturePoint - comCoordinate) * omega;
            return requiredComSpeed * snapshot.TotalMass;
        }

        static Vector3 ResolveLateralDirection(
            StaggerPhysicalRig scenarioRig,
            float sign)
        {
            Vector3 supportUp = scenarioRig.SupportUp;
            Vector3 lateral = Vector3.ProjectOnPlane(
                scenarioRig.RootBody.transform.right, supportUp);
            if (lateral.sqrMagnitude <= Mathf.Epsilon)
                lateral = Vector3.ProjectOnPlane(Vector3.forward, supportUp);
            return lateral.normalized * (sign < 0f ? -1f : 1f);
        }

        static Vector3 SlopeNormal(float angle)
        {
            return Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up;
        }

        static void UpdateFootSlip(
            BalancerRun result,
            Rigidbody body,
            GroundContactProbe probe,
            ref Vector3 previousPosition,
            ref bool previousGrounded,
            Vector3 supportUp)
        {
            bool grounded = probe != null && probe.IsGrounded;
            if (grounded && previousGrounded)
            {
                Vector3 delta = body.position - previousPosition;
                float tangentialSpeed = Vector3.ProjectOnPlane(
                    delta, supportUp).magnitude / Mathf.Max(0.0001f, Time.fixedDeltaTime);
                result.maxFootSlip = Mathf.Max(result.maxFootSlip, tangentialSpeed);
            }

            previousPosition = body.position;
            previousGrounded = grounded;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        sealed class BalancerRun
        {
            public float impulse;
            public float minimumMargin;
            public float finalMargin;
            public float maxComSpeed;
            public float maxComDisplacement;
            public float maxTorque;
            public float maxAngularVelocity;
            public float maxFootSlip;
            public float recoveryTime;
            public int torqueFrames;
            public int requiresStepSamples;
            public bool unpinnedObserved;
            public bool stepRequiredObserved;
            public string scenarioName;
        }

        sealed class StressMassCase
        {
            public readonly string name;
            public readonly float rootMassScale;
            public readonly float footMassScale;
            public readonly float inertiaScale;

            public StressMassCase(
                string name,
                float rootMassScale,
                float footMassScale,
                float inertiaScale)
            {
                this.name = name;
                this.rootMassScale = rootMassScale;
                this.footMassScale = footMassScale;
                this.inertiaScale = inertiaScale;
            }
        }

        sealed class PhysicalScenario
        {
            public readonly string name;
            public readonly Vector3 gravityUp;
            public readonly Vector3 groundNormal;
            public readonly bool movingPlatform;
            public readonly bool partialContact;
            public readonly int totalPushes;
            public readonly float impulseSign;

            public PhysicalScenario(
                string name,
                Vector3 gravityUp,
                Vector3 groundNormal,
                bool movingPlatform,
                bool partialContact,
                int totalPushes,
                float impulseSign)
            {
                this.name = name;
                this.gravityUp = gravityUp;
                this.groundNormal = groundNormal;
                this.movingPlatform = movingPlatform;
                this.partialContact = partialContact;
                this.totalPushes = totalPushes;
                this.impulseSign = impulseSign;
            }
        }
    }
}
