using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabAnalyzerTests
    {
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

        static RagdollLabThresholds RagdollLabThresholds()
        {
            var thresholds = ScriptableObject.CreateInstance<RagdollLabThresholds>();
            thresholds.anchorErrorWarningMeters = 0.02f;
            return thresholds;
        }
    }
}
