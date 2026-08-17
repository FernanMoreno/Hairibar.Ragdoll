using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabAnalyzerTests
    {
        [Test]
        public void AnimatedPairTrackingAggregatesTargetAndPhysicsDerivativesByStableIdentity()
        {
            var frames = new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, animatedPairSourceAvailable = true, animatedPairCount = 1,
                    animatedPairs = new[]
                    {
                        new TargetPoseTelemetry { pairId = "pair:A", id = "pair:A", bone = "Chest",
                            targetTransformId = "target:A", physicsBodyId = "body:A", sourceAvailable = true,
                            targetAvailable = true, physicsAvailable = true, targetPhysicsDistance = 0.1f,
                            targetPhysicsAngularError = 2f, targetKinematicsAvailable = true,
                            targetVelocityAvailable = true, targetAccelerationAvailable = true,
                            targetJerkAvailable = true, physicsVelocityAvailable = true,
                            physicsAccelerationAvailable = true, physicsJerkAvailable = true,
                            targetLinearVelocity = new Vector3Data(Vector3.right),
                            targetLinearAcceleration = new Vector3Data(Vector3.right * 2f),
                            targetLinearJerk = new Vector3Data(Vector3.right * 3f),
                            physicsKinematicsAvailable = true,
                            physicsLinearAcceleration = new Vector3Data(Vector3.right * 4f),
                            physicsLinearJerk = new Vector3Data(Vector3.right * 5f),
                            authoredMappingAvailable = true, authoredMappingPositionWeight = 0.8f,
                            effectiveMappingAvailable = true, effectiveMappingPositionWeight = 0.4f }
                    } },
                new() { fixedDeltaTime = 0.02f, animatedPairSourceAvailable = true, animatedPairCount = 1,
                    animatedPairs = new[]
                    {
                        new TargetPoseTelemetry { pairId = "pair:A", id = "pair:A", bone = "Chest",
                            targetTransformId = "target:A", physicsBodyId = "body:A", sourceAvailable = true,
                            targetAvailable = true, physicsAvailable = true, targetPhysicsDistance = 0.2f,
                            targetPhysicsAngularError = 4f, targetKinematicsAvailable = true,
                            targetVelocityAvailable = true, targetAccelerationAvailable = true,
                            targetJerkAvailable = true, physicsVelocityAvailable = true,
                            physicsAccelerationAvailable = true, physicsJerkAvailable = true,
                            targetLinearVelocity = new Vector3Data(Vector3.right * 2f),
                            targetLinearAcceleration = new Vector3Data(Vector3.right * 4f),
                            targetLinearJerk = new Vector3Data(Vector3.right * 6f),
                            physicsKinematicsAvailable = true,
                            physicsLinearAcceleration = new Vector3Data(Vector3.right * 8f),
                            physicsLinearJerk = new Vector3Data(Vector3.right * 10f),
                            authoredMappingAvailable = true, authoredMappingPositionWeight = 0.8f,
                            effectiveMappingAvailable = true, effectiveMappingPositionWeight = 0.4f }
                    } }
            };

            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f);

            Assert.That(report.animatedPairSourceAvailable, Is.True);
            Assert.That(report.animatedPairCount, Is.EqualTo(1));
            Assert.That(report.animatedPairSampleCount, Is.EqualTo(2));
            Assert.That(report.pairTracking, Has.Length.EqualTo(1));
            Assert.That(report.pairTracking[0].id, Is.EqualTo("pair:A"));
            Assert.That(report.pairTracking[0].targetLinearAcceleration.mean, Is.EqualTo(3f).Within(0.001f));
            Assert.That(report.pairTracking[0].physicsLinearJerk.max, Is.EqualTo(10f).Within(0.001f));
            Assert.That(report.pairTracking[0].effectiveMappingPositionWeight, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void MappingDiagnosticRequiresExplicitRecorderWarning()
        {
            PhysicsFrame frame = new()
            {
                fixedDeltaTime = 0.02f,
                animatedPairSourceAvailable = true,
                animatedPairs = new[]
                {
                    new TargetPoseTelemetry { pairId = "pair:A", sourceAvailable = true, targetPhysicsAngularError = 90f }
                }
            };

            DiagnosticsReport noWarning = RagdollLabAnalyzer.Diagnose(
                RagdollLabAnalyzer.Analyze(new List<PhysicsFrame> { frame }, 1.8f, 70f, 9.81f));
            Assert.That(noWarning.diagnostics.Exists(d => d.type == "MAPPING_INTEGRITY"), Is.False);

            frame.mappingIntegrityWarnings = new[] { "duplicate_pair_id:pair:A" };
            DiagnosticsReport warning = RagdollLabAnalyzer.Diagnose(
                RagdollLabAnalyzer.Analyze(new List<PhysicsFrame> { frame }, 1.8f, 70f, 9.81f));
            Assert.That(warning.diagnostics.Exists(d => d.type == "MAPPING_INTEGRITY"), Is.True);
        }

        [Test]
        public void PairReportsRemainSeparateWhenFramesContainMultiplePairs()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new PhysicsFrame
                {
                    fixedDeltaTime = 0.02f,
                    animatedPairSourceAvailable = true,
                    animatedPairCount = 2,
                    animatedPairs = new[]
                    {
                        new TargetPoseTelemetry { pairId = "pair:A", targetAvailable = true, physicsAvailable = true, targetPhysicsDistance = 0.1f },
                        new TargetPoseTelemetry { pairId = "pair:B", targetAvailable = true, physicsAvailable = true, targetPhysicsDistance = 0.9f }
                    }
                }
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.pairTracking, Has.Length.EqualTo(2));
            Assert.That(report.pairTracking[0].targetPhysicsDistance.mean, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(report.pairTracking[1].targetPhysicsDistance.mean, Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void PerJointAngularTrackingErrorIsNotSharedAcrossJoints()
        {
            JointTelemetry jointA = new() { id = "ConfigurableJoint:A", name = "A", bodyId = "Rigidbody:A" };
            JointTelemetry jointB = new() { id = "ConfigurableJoint:B", name = "B", bodyId = "Rigidbody:B" };

            var frames = new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f,
                    joints = new[] { jointA, jointB },
                    targetPoses = new[] { new TargetPoseTelemetry { physicsBodyId = "Rigidbody:A", targetPhysicsAngularError = 10f } } },
                new() { fixedDeltaTime = 0.02f,
                    joints = new[] { jointA, jointB },
                    targetPoses = new[] { new TargetPoseTelemetry { physicsBodyId = "Rigidbody:A", targetPhysicsAngularError = 20f } } },
            };

            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, characterHeight: 1.8f, totalMass: 70f, gravity: 9.81f);

            Assert.That(report.joints[0].angularTrackingError.count, Is.EqualTo(2),
                "Joint A has a matching target-pose sample in both frames.");
            Assert.That(report.joints[0].angularTrackingError.mean, Is.EqualTo(15f).Within(0.001f));
            Assert.That(report.joints[1].angularTrackingError.count, Is.EqualTo(0),
                "Joint B has no matching target-pose sample; before the physicsBodyId fix this " +
                "incorrectly inherited joint A's samples (count=2, mean=15) because the accumulation " +
                "loop never filtered by body.");
        }

        [Test]
        public void JointReportsFollowStableIdWhenArraysReorderOrGoSparse()
        {
            JointTelemetry a0 = new() { id = "joint:A", name = "A", bodyId = "body:A", anchorError = 1f };
            JointTelemetry b0 = new() { id = "joint:B", name = "B", bodyId = "body:B", anchorError = 10f };
            JointTelemetry b1 = new() { id = "joint:B", name = "B", bodyId = "body:B", anchorError = 20f };
            JointTelemetry a1 = new() { id = "joint:A", name = "A", bodyId = "body:A", anchorError = 2f };
            JointTelemetry c0 = new() { id = "joint:C", name = "C", bodyId = "body:C", anchorError = 30f };

            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, joints = new[] { a0, b0 } },
                new() { fixedDeltaTime = 0.02f, joints = new[] { b1, a1 } },
                new() { fixedDeltaTime = 0.02f, joints = new[] { c0 } },
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.joints, Has.Length.EqualTo(3));
            JointReport a = report.joints[0];
            JointReport b = report.joints[1];
            JointReport c = report.joints[2];
            Assert.That(a.id, Is.EqualTo("joint:A"));
            Assert.That(a.anchorError.count, Is.EqualTo(2));
            Assert.That(a.anchorError.mean, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(b.id, Is.EqualTo("joint:B"));
            Assert.That(b.anchorError.count, Is.EqualTo(2));
            Assert.That(b.anchorError.mean, Is.EqualTo(15f).Within(0.001f));
            Assert.That(c.id, Is.EqualTo("joint:C"));
            Assert.That(c.anchorError.count, Is.EqualTo(1));
        }

        [Test]
        public void SparseJointAnchorEventUsesOriginalFrameIndices()
        {
            var frames = new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, joints = new[]
                    { new JointTelemetry { id = "joint:A", name = "A", bodyId = "body:A", anchorError = 0.001f } } },
                new() { fixedDeltaTime = 0.02f, joints = new[]
                    { new JointTelemetry { id = "joint:B", name = "B", bodyId = "body:B", anchorError = 0.5f } } },
                new() { fixedDeltaTime = 0.02f, joints = new[]
                    { new JointTelemetry { id = "joint:A", name = "A", bodyId = "body:A", anchorError = 0.05f } },
                    events = new[] { new EventMarker { name = "impact", frameIndex = 2, simulationTime = 0.04f } } },
            };

            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f);

            Assert.That(report.joints[0].id, Is.EqualTo("joint:A"));
            Assert.That(report.joints[0].anchorErrorEvents, Has.Length.EqualTo(1));
            Assert.That(report.joints[0].anchorErrorEvents[0].eventFrameIndex, Is.EqualTo(2));
            Assert.That(report.joints[0].anchorErrorEvents[0].peak, Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void ShortContactCountUsesContactDurationNotRunTimePosition()
        {
            RagdollLabThresholds thresholds = RagdollLabThresholds();
            thresholds.shortContactDurationSeconds = 0.1f;
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, simulationTime = 5f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactStart = true, hasContactStartTime = true, contactStartTime = 5f } } },
                new() { fixedDeltaTime = 0.02f, simulationTime = 5.02f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactEnd = true, hasContactEndTime = true, contactEndTime = 5.02f } } },
            }, 1.8f, 70f, 9.81f, thresholds);

            Assert.That(report.shortContactCount, Is.EqualTo(1));
        }

        [Test]
        public void LongContactAtRunStartIsNotShort()
        {
            RagdollLabThresholds thresholds = RagdollLabThresholds();
            thresholds.shortContactDurationSeconds = 0.1f;
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, simulationTime = 0f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactStart = true } } },
                new() { fixedDeltaTime = 0.02f, simulationTime = 0.2f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactEnd = true } } },
            }, 1.8f, 70f, 9.81f, thresholds);

            Assert.That(report.shortContactCount, Is.EqualTo(0));
        }

        [Test]
        public void ContactEndWithoutStartIsIgnored()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, simulationTime = 5.02f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactEnd = true, hasContactEndTime = true, contactEndTime = 5.02f } } },
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.shortContactCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateContactStartPreservesFirstStartTime()
        {
            RagdollLabThresholds thresholds = RagdollLabThresholds();
            thresholds.shortContactDurationSeconds = 0.1f;
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, simulationTime = 5f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactStart = true, hasContactStartTime = true, contactStartTime = 5f } } },
                new() { fixedDeltaTime = 0.02f, simulationTime = 5.02f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactStart = true, hasContactStartTime = true, contactStartTime = 5.02f } } },
                new() { fixedDeltaTime = 0.02f, simulationTime = 5.04f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactEnd = true, hasContactEndTime = true, contactEndTime = 5.04f } } },
            }, 1.8f, 70f, 9.81f, thresholds);

            Assert.That(report.shortContactCount, Is.EqualTo(1));
        }

        [Test]
        public void StillActiveContactIsNotCountedAsShort()
        {
            RagdollLabThresholds thresholds = RagdollLabThresholds();
            thresholds.shortContactDurationSeconds = 0.1f;
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, simulationTime = 5f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactStart = true, hasContactStartTime = true, contactStartTime = 5f } } },
                new() { fixedDeltaTime = 0.02f, simulationTime = 5.02f, contacts = new[]
                    { new ContactTelemetry { key = "foot|ground", contactStay = true } } },
            }, 1.8f, 70f, 9.81f, thresholds);

            Assert.That(report.shortContactCount, Is.EqualTo(0));
        }

        [Test]
        public void AnalyzerUsesStoredFallSemanticsAndRecoveryFrames()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, character = new CharacterTelemetry
                    { supportContactCount = 2, supportPointCount = 2, likelyFallen = false } },
                new() { fixedDeltaTime = 0.02f, character = new CharacterTelemetry
                    { supportContactCount = 1, supportPointCount = 1, likelyFallen = true } },
                new() { fixedDeltaTime = 0.02f, character = new CharacterTelemetry
                    { supportContactCount = 0, supportPointCount = 0, likelyFallen = true } },
                new() { fixedDeltaTime = 0.02f, character = new CharacterTelemetry
                    { supportContactCount = 2, supportPointCount = 2, likelyFallen = false } },
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.fallenFrameCount, Is.EqualTo(2));
            Assert.That(report.recoveryTimeSeconds, Is.EqualTo(0.04f).Within(0.0001f));
        }

        [Test]
        public void AnchorErrorEventsAreEmptyWhenNoEventMarkersArePresent()
        {
            JointTelemetry joint = new() { id = "ConfigurableJoint:A", name = "A", bodyId = "Rigidbody:A" };
            var frames = new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, joints = new[] { joint } },
                new() { fixedDeltaTime = 0.02f, joints = new[] { joint } },
            };

            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f);

            Assert.That(report.joints[0].anchorErrorEvents, Is.Not.Null.And.Empty);
        }

        [Test]
        public void AnchorErrorEventsCaptureBaselinePeakAndSettling()
        {
            JointTelemetry joint = new() { id = "ConfigurableJoint:A", name = "A", bodyId = "Rigidbody:A" };
            float[] anchorErrors = { 0.001f, 0.001f, 0.001f, 0.05f, 0.03f, 0.001f };
            var frames = new List<PhysicsFrame>();
            for (int i = 0; i < anchorErrors.Length; i++)
            {
                JointTelemetry sample = new() { id = joint.id, name = joint.name, bodyId = joint.bodyId, anchorError = anchorErrors[i] };
                frames.Add(new PhysicsFrame
                {
                    fixedDeltaTime = 0.02f,
                    joints = new[] { sample },
                    events = i == 3 ? new[] { new EventMarker { name = "eventApplied", frameIndex = i, simulationTime = i * 0.02f } } : null,
                });
            }

            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f, RagdollLabThresholds());

            Assert.That(report.joints[0].anchorErrorEvents, Has.Length.EqualTo(1));
            AnchorDriftEventReport evt = report.joints[0].anchorErrorEvents[0];
            Assert.That(evt.eventName, Is.EqualTo("eventApplied"));
            Assert.That(evt.eventFrameIndex, Is.EqualTo(3));
            Assert.That(evt.baseline, Is.EqualTo(0.001f).Within(0.0001f),
                "Baseline must average the samples before the event (frames 0-2), not include the spike.");
            Assert.That(evt.peak, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(evt.peakOffsetSeconds, Is.EqualTo(0f).Within(0.001f),
                "The peak in this synthetic series is the event frame itself.");
            Assert.That(evt.aucError, Is.GreaterThan(0f));
        }

        [Test]
        public void Diagnose_TransientSpikeThatSettlesQuickly_IsNotFlaggedAsPersistentDrift()
        {
            // 10 frames, spike at 3-4 (20% of samples -> p95 still crosses the
            // warning threshold), settled back below tolerance from frame 5.
            float[] anchorErrors = BuildSeries(frameCount: 10, spikeFrame: 3, spikeValue: 0.05f,
                baseline: 0.001f, settleAtFrame: 5);
            ScenarioReport report = AnalyzeAnchorSeries(anchorErrors, spikeFrame: 3);
            RagdollLabThresholds thresholds = RagdollLabThresholds();

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report, thresholds);

            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "TransientAnchorExcursion"),
                Is.True);
            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "PersistentAnchorDrift"),
                Is.False);
        }

        [Test]
        public void Diagnose_SpikeThatNeverSettles_IsFlaggedAsPersistentDrift()
        {
            // 40 frames (0.8s @ 0.02 dt): spike at frame 3, stays above tolerance
            // for the rest of the run -- settlingTimeSeconds must exceed the
            // 0.5s persistence threshold, which needs real elapsed time, not
            // just a high proportion of samples.
            float[] anchorErrors = BuildSeries(frameCount: 40, spikeFrame: 3, spikeValue: 0.05f,
                baseline: 0.001f, settleAtFrame: -1);
            ScenarioReport report = AnalyzeAnchorSeries(anchorErrors, spikeFrame: 3);
            RagdollLabThresholds thresholds = RagdollLabThresholds();

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report, thresholds);

            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "PersistentAnchorDrift"),
                Is.True);
            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "TransientAnchorExcursion"),
                Is.False);
        }

        [Test]
        public void Diagnose_PersistentEventBelowGlobalP95_IsStillFlagged()
        {
            // Thirty 20 ms samples above threshold are persistent evidence,
            // but only 3% of this long capture: global p95 stays at baseline.
            float[] anchorErrors = BuildSeries(frameCount: 1000, spikeFrame: 3, spikeValue: 0.05f,
                baseline: 0.001f, settleAtFrame: 33);
            ScenarioReport report = AnalyzeAnchorSeries(anchorErrors, spikeFrame: 3);
            RagdollLabThresholds thresholds = RagdollLabThresholds();

            Assert.That(report.joints[0].anchorError.p95,
                Is.LessThanOrEqualTo(thresholds.anchorErrorWarningMeters));

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report, thresholds);

            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "PersistentAnchorDrift"),
                Is.True, "Event evidence must not be hidden by a low global p95.");
        }

        [Test]
        public void Diagnose_EventWithoutAnchorExcursion_ProducesNoAnchorDiagnostic()
        {
            float[] anchorErrors = BuildSeries(frameCount: 40, spikeFrame: 3, spikeValue: 0.001f,
                baseline: 0.001f, settleAtFrame: -1);
            ScenarioReport report = AnalyzeAnchorSeries(anchorErrors, spikeFrame: 3);

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(
                report, RagdollLabThresholds());

            Assert.That(diagnostics.diagnostics.Exists(d =>
                    d.type == "AnchorDrift"
                    || d.type == "TransientAnchorExcursion"
                    || d.type == "PersistentAnchorDrift"), Is.False);
        }

        [Test]
        public void BalanceTelemetryPreservesStatesAndBuildsStaggerEpisode()
        {
            var frames = new List<PhysicsFrame>
            {
                BalanceFrame(0, "Puppet", "Stable", 0.10f),
                BalanceFrame(1, "Puppet", "RequiresStep", -0.05f),
                BalanceFrame(2, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.20f,
                    new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "run-stagger-001", phase = "LiftOff", swingFoot = "Right", swingFootAvailable = true, stepCount = 1, selectedFootGroundSupport = false }),
                BalanceFrame(3, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.10f,
                    new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "run-stagger-001", phase = "Replant", swingFoot = "Right", swingFootAvailable = true, stepCount = 1, selectedFootGroundSupport = true, liftOffObserved = false, replantObserved = true }),
                BalanceFrame(4, "Puppet", "Stable", 0.12f,
                    new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "run-stagger-001", phase = "Settling", swingFoot = "Right", swingFootAvailable = true, stepCount = 1, selectedFootGroundSupport = true })
            };
            for (int i = 0; i < frames.Count; i++)
                frames[i].character = new CharacterTelemetry { puppetState = frames[i].balance.activeBehaviour.Contains("Puppet") ? "Puppet" : "Puppet" };

            frames[2].stagger.liftOffObserved = true;
            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f);

            Assert.That(report.balanceTelemetryAvailable, Is.True);
            Assert.That(report.balanceSampleCount, Is.EqualTo(5));
            Assert.That(report.requiresStepFrameCount, Is.EqualTo(3));
            Assert.That(report.minimumSignedSupportMargin, Is.EqualTo(-0.20f).Within(0.0001f));
            Assert.That(report.staggerEpisodes, Has.Length.EqualTo(1));
            Assert.That(report.staggerEpisodes[0].swingFoot, Is.EqualTo("Right"));
            Assert.That(report.staggerEpisodes[0].liftOffFrame, Is.EqualTo(2));
            Assert.That(report.staggerEpisodes[0].replantFrame, Is.EqualTo(3));
            Assert.That(report.staggerEpisodes[0].terminalOutcome, Is.EqualTo("Recovered"));
        }

        [Test]
        public void UnavailableBalanceSourceDoesNotBecomeStable()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, balance = new BalanceFrameTelemetry { sourceAvailable = false, state = "Unavailable" } }
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.balanceTelemetryAvailable, Is.False);
            Assert.That(report.balanceSampleCount, Is.EqualTo(0));
            Assert.That(report.staggerEpisodes, Is.Empty);
        }

        [Test]
        public void BalanceTelemetryCountsEveryRuntimeClassification()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                BalanceFrame(0, "Puppet", "Stable", 0.10f),
                BalanceFrame(1, "Puppet", "RecoverableWithoutStep", 0.01f),
                BalanceFrame(2, "Puppet", "RequiresStep", -0.01f),
                BalanceFrame(3, "Puppet", "Unrecoverable", -0.40f)
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.balanceSampleCount, Is.EqualTo(4));
            Assert.That(report.requiresStepFrameCount, Is.EqualTo(1));
            Assert.That(report.unrecoverableFrameCount, Is.EqualTo(1));
            Assert.That(report.minimumSignedSupportMargin, Is.EqualTo(-0.40f).Within(0.0001f));
            Assert.That(report.finalSignedSupportMargin, Is.EqualTo(-0.40f).Within(0.0001f));
        }

        [Test]
        public void BalanceTransitionProvenanceRemainsAttachedToThePhysicsFrame()
        {
            PhysicsFrame frame = BalanceFrame(7, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.1f);
            frame.simulationTime = 0.14f;
            frame.balance.transitionObserved = true;
            frame.balance.transitionFrom = "Puppet:RequiresStep";
            frame.balance.transitionTo = "RagdollBipedStaggerBehaviour:RequiresStep";

            Assert.That(frame.frameIndex, Is.EqualTo(7));
            Assert.That(frame.simulationTime, Is.EqualTo(0.14f).Within(0.0001f));
            Assert.That(frame.balance.transitionObserved, Is.True);
            Assert.That(frame.balance.transitionFrom, Is.EqualTo("Puppet:RequiresStep"));
            Assert.That(frame.balance.transitionTo, Is.EqualTo("RagdollBipedStaggerBehaviour:RequiresStep"));
        }

        [Test]
        public void UnpinnedStaggerEpisodeIsNotReportedAsRecovered()
        {
            PhysicsFrame start = BalanceFrame(0, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.1f,
                new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "episode-unpinned", phase = "Swing", swingFoot = "Left", swingFootAvailable = true, stepCount = 1 });
            start.character = new CharacterTelemetry { puppetState = "Unpinned" };
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame> { start }, 1.8f, 70f, 9.81f);

            Assert.That(report.staggerEpisodes, Has.Length.EqualTo(1));
            Assert.That(report.staggerEpisodes[0].terminalOutcome, Is.EqualTo("Unpinned"));
            Assert.That(report.unpinnedStaggerEpisodeCount, Is.EqualTo(1));
        }

        [Test]
        public void NonGroundSelectedFootContactDoesNotCountAsReplant()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                BalanceFrame(0, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.1f,
                    new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "episode-wall", phase = "Swing", swingFoot = "Left", swingFootAvailable = true, stepCount = 1 }),
                BalanceFrame(1, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.05f,
                    new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "episode-wall", phase = "Replant", swingFoot = "Left", swingFootAvailable = true, stepCount = 1, selectedFootGroundSupport = false, replantObserved = false })
            }, 1.8f, 70f, 9.81f);

            Assert.That(report.staggerEpisodes[0].replantFrame, Is.EqualTo(-1));
            Assert.That(report.staggerEpisodes[0].terminalOutcome, Is.EqualTo("Incomplete"));
            Assert.That(RagdollLabAnalyzer.Diagnose(report).diagnostics.Exists(d => d.type == "STEP_FAILED_TO_REPLANT"), Is.True);
        }

        [Test]
        public void StaggerEpisodeTimesOutWhenCaptureExceedsConfiguredBound()
        {
            RagdollLabThresholds thresholds = RagdollLabThresholds();
            thresholds.staggerEpisodeTimeoutSeconds = 0.5f;
            var frames = new List<PhysicsFrame>();
            for (int i = 0; i < 3; i++)
                frames.Add(BalanceFrame(i, "RagdollBipedStaggerBehaviour", "RequiresStep", -0.1f,
                    new StaggerFrameTelemetry { sourceAvailable = true, episodeId = "episode-timeout", phase = "Swing", swingFoot = "Right", swingFootAvailable = true, stepCount = 1 }));
            frames[2].simulationTime = 0.6f;

            ScenarioReport report = RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f, thresholds);

            Assert.That(report.staggerEpisodes[0].terminalOutcome, Is.EqualTo("TimedOut"));
            Assert.That(report.staggerEpisodes[0].invalidReason, Is.EqualTo("stagger_episode_timeout"));
        }

        [Test]
        public void ScenarioAwareDiagnosticsFailClosedAndExposeSupportEvidence()
        {
            ScenarioReport report = RagdollLabAnalyzer.Analyze(new List<PhysicsFrame>
            {
                new() { fixedDeltaTime = 0.02f, simulationTime = 0f,
                    character = new CharacterTelemetry { supportReferenceAvailable = true, supportContactCount = 2, supportPointCount = 2 },
                    balance = new BalanceFrameTelemetry { sourceAvailable = true, state = "Stable", hasSignedSupportMargin = true, signedSupportMargin = 0.10f } },
                new() { fixedDeltaTime = 0.02f, simulationTime = 0.02f,
                    character = new CharacterTelemetry { supportReferenceAvailable = true, supportContactCount = 0, supportPointCount = 0 },
                    balance = new BalanceFrameTelemetry { sourceAvailable = true, state = "RequiresStep", hasSignedSupportMargin = true, signedSupportMargin = -0.20f } }
            }, 1.8f, 70f, 9.81f);
            report.name = "Push";

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report);

            Assert.That(diagnostics.scenarioProfile, Is.EqualTo("Push"));
            Assert.That(diagnostics.profileAvailable, Is.True);
            Assert.That(report.supportSampleCount, Is.EqualTo(2));
            Assert.That(report.supportLossFrameCount, Is.EqualTo(1));
            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "COM_INSTABILITY"), Is.True);
            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "CONTACT_SUPPORT_LOST"), Is.True);
            Assert.That(diagnostics.diagnostics.TrueForAll(d => d.recommendedChecks != null && d.recommendedChecks.Length > 0), Is.True);
            Assert.That(diagnostics.diagnostics.TrueForAll(d => d.falsifiers != null && d.falsifiers.Length > 0), Is.True);
        }

        [Test]
        public void UnknownScenarioDoesNotSilentlyUseIdleExpectations()
        {
            ScenarioReport report = new() { name = "Unspecified", centerOfMassSpeed = new MetricSummary { mean = 100f } };

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report);

            Assert.That(diagnostics.profileAvailable, Is.False);
            Assert.That(diagnostics.scenarioProfile, Is.EqualTo(RagdollLabScenarioProfiles.UnavailableId));
            Assert.That(diagnostics.unavailableReasons, Does.Contain("scenario_profile_unavailable:Unspecified"));
        }

        [Test]
        public void RecoveryTimingAndEarlyStepDiagnosticsUseScenarioThresholds()
        {
            ScenarioReport report = new()
            {
                name = "Stagger",
                balanceTelemetryAvailable = true,
                signedSupportMarginAvailable = true,
                balanceSampleCount = 4,
                recoveryTimeSeconds = 2f,
                recoveryOvershootMeters = 0.2f,
                firstRequiresStepSimulationTime = 0.02f,
                minimumSignedSupportMargin = 0.01f,
                finalSignedSupportMargin = 0.1f
            };

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report);

            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "RECOVERY_TOO_SLOW"), Is.True);
            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "RECOVERY_OVERSHOOT"), Is.True);
            Assert.That(diagnostics.diagnostics.Exists(d => d.type == "STEP_REQUIRED_TOO_EARLY"), Is.True);
        }

        [Test]
        public void EveryEmittedDiagnosticContainsActionableEvidenceContract()
        {
            ScenarioReport report = new()
            {
                name = "Push",
                shortContactCount = 1,
                balanceTelemetryAvailable = true,
                signedSupportMarginAvailable = true,
                minimumSignedSupportMargin = -0.2f,
                frameCount = 4,
                durationSeconds = 0.06f
            };

            DiagnosticsReport diagnostics = RagdollLabAnalyzer.Diagnose(report);

            Assert.That(diagnostics.diagnostics, Is.Not.Empty);
            foreach (DiagnosticEvidence evidence in diagnostics.diagnostics)
            {
                Assert.That(evidence.type, Is.Not.Empty);
                Assert.That(evidence.scenario, Is.EqualTo("Push"));
                Assert.That(evidence.severity, Is.Not.Empty);
                Assert.That(evidence.confidence, Is.Not.Empty);
                Assert.That(evidence.observation, Is.Not.Empty);
                Assert.That(evidence.hypothesis, Is.Not.Empty);
                Assert.That(evidence.metrics, Is.Not.Null.And.Not.Empty);
                Assert.That(evidence.firstFrame, Is.GreaterThanOrEqualTo(0));
                Assert.That(evidence.peakFrame, Is.GreaterThanOrEqualTo(evidence.firstFrame));
                Assert.That(evidence.firstSimulationTime, Is.GreaterThanOrEqualTo(0f));
                Assert.That(evidence.peakSimulationTime, Is.GreaterThanOrEqualTo(evidence.firstSimulationTime));
                Assert.That(evidence.recommendedChecks, Is.Not.Null.And.Not.Empty);
                Assert.That(evidence.falsifiers, Is.Not.Null.And.Not.Empty);
            }
        }

        static PhysicsFrame BalanceFrame(int index, string behaviour, string state, float margin, StaggerFrameTelemetry stagger = null)
        {
            return new PhysicsFrame
            {
                frameIndex = index,
                fixedDeltaTime = 0.02f,
                simulationTime = index * 0.02f,
                balance = new BalanceFrameTelemetry
                {
                    sourceAvailable = true,
                    activeBehaviour = behaviour,
                    state = state,
                    hasCapturePoint = true,
                    capturePoint = new Vector3Data(Vector3.zero),
                    hasSignedSupportMargin = true,
                    signedSupportMargin = margin,
                    supportReferenceAvailable = true,
                    supportUp = new Vector3Data(Vector3.up)
                },
                stagger = stagger
            };
        }

        static float[] BuildSeries(int frameCount, int spikeFrame, float spikeValue, float baseline, int settleAtFrame)
        {
            var series = new float[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                if (i < spikeFrame) series[i] = baseline;
                else if (settleAtFrame >= 0 && i >= settleAtFrame) series[i] = baseline;
                else series[i] = spikeValue;
            }
            return series;
        }

        static ScenarioReport AnalyzeAnchorSeries(float[] anchorErrors, int spikeFrame)
        {
            JointTelemetry joint = new() { id = "ConfigurableJoint:A", name = "A", bodyId = "Rigidbody:A" };
            var frames = new List<PhysicsFrame>();
            for (int i = 0; i < anchorErrors.Length; i++)
            {
                JointTelemetry sample = new() { id = joint.id, name = joint.name, bodyId = joint.bodyId, anchorError = anchorErrors[i] };
                frames.Add(new PhysicsFrame
                {
                    fixedDeltaTime = 0.02f,
                    joints = new[] { sample },
                    events = i == spikeFrame ? new[] { new EventMarker { name = "eventApplied", frameIndex = i, simulationTime = i * 0.02f } } : null,
                });
            }
            return RagdollLabAnalyzer.Analyze(frames, 1.8f, 70f, 9.81f, RagdollLabThresholds());
        }

        static RagdollLabThresholds RagdollLabThresholds()
        {
            var thresholds = ScriptableObject.CreateInstance<RagdollLabThresholds>();
            thresholds.anchorErrorWarningMeters = 0.02f;
            return thresholds;
        }
    }
}
