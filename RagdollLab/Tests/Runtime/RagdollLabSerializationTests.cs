using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabSerializationTests
    {
        [Test]
        public void NewDiagnosticEvidenceRoundTripsActionableFields()
        {
            var source = new DiagnosticsReport
            {
                schemaVersion = RagdollLabSchema.Version,
                scenarioProfile = "Stagger",
                profileAvailable = true,
                diagnostics = new System.Collections.Generic.List<DiagnosticEvidence>
                {
                    new DiagnosticEvidence
                    {
                        type = "STEP_FAILED_TO_REPLANT",
                        availability = "available",
                        scenario = "Stagger",
                        subject = "episode-1",
                        firstFrame = 4,
                        peakFrame = 8,
                        firstSimulationTime = 0.08f,
                        peakSimulationTime = 0.16f,
                        recommendedChecks = new[] { "verify selected-foot ground support" },
                        falsifiers = new[] { "a later ground-backed replant is observed" }
                    }
                }
            };

            DiagnosticsReport roundTrip = JsonUtility.FromJson<DiagnosticsReport>(JsonUtility.ToJson(source));

            Assert.That(roundTrip.schemaVersion, Is.EqualTo(RagdollLabSchema.Version));
            Assert.That(roundTrip.scenarioProfile, Is.EqualTo("Stagger"));
            Assert.That(roundTrip.profileAvailable, Is.True);
            Assert.That(roundTrip.diagnostics, Has.Count.EqualTo(1));
            Assert.That(roundTrip.diagnostics[0].recommendedChecks[0], Does.Contain("ground support"));
            Assert.That(roundTrip.diagnostics[0].falsifiers[0], Does.Contain("replant"));
            Assert.That(roundTrip.diagnostics[0].firstSimulationTime, Is.EqualTo(0.08f).Within(0.0001f));
        }

        [Test]
        public void Version130DiagnosticsRemainReadableWithUnavailableNewFields()
        {
            DiagnosticsReport old = JsonUtility.FromJson<DiagnosticsReport>(
                "{\"schemaVersion\":\"1.3.0\",\"diagnostics\":[]}");

            Assert.That(old, Is.Not.Null);
            Assert.That(old.schemaVersion, Is.EqualTo("1.3.0"));
            Assert.That(old.diagnostics, Is.Empty);
            Assert.That(old.profileAvailable, Is.False);
        }

        [Test]
        public void NewBalanceComparisonRoundTripsProfileAndDecisionReasons()
        {
            var source = new BalanceComparisonReport
            {
                scenarioProfile = "Balancer",
                profileAvailable = true,
                decision = "reject",
                invalidReason = "safety_guard_failed",
                rejectionReasons = new System.Collections.Generic.List<string> { "candidate_foot_slip_increased" }
            };

            BalanceComparisonReport roundTrip = JsonUtility.FromJson<BalanceComparisonReport>(JsonUtility.ToJson(source));

            Assert.That(roundTrip.scenarioProfile, Is.EqualTo("Balancer"));
            Assert.That(roundTrip.profileAvailable, Is.True);
            Assert.That(roundTrip.decision, Is.EqualTo("reject"));
            Assert.That(roundTrip.rejectionReasons, Does.Contain("candidate_foot_slip_increased"));
        }

        [Test]
        public void AnimatedPairTrackingRoundTripsDerivativesAndMappingState()
        {
            var source = new PhysicsFrame
            {
                animatedPairSourceAvailable = true,
                animatedPairCount = 1,
                animatedPairs = new[]
                {
                    new TargetPoseTelemetry
                    {
                        pairId = "pair:A",
                        targetLinearJerk = new Vector3Data(Vector3.right * 3f),
                        physicsAngularJerk = new Vector3Data(Vector3.up * 4f),
                        authoredMappingAvailable = true,
                        authoredMappingPositionWeight = 0.8f,
                        effectiveMappingAvailable = true,
                        effectiveMappingPositionWeight = 0.4f
                    }
                }
            };

            PhysicsFrame roundTrip = JsonUtility.FromJson<PhysicsFrame>(JsonUtility.ToJson(source));

            Assert.That(roundTrip.animatedPairSourceAvailable, Is.True);
            Assert.That(roundTrip.animatedPairs, Has.Length.EqualTo(1));
            Assert.That(roundTrip.animatedPairs[0].pairId, Is.EqualTo("pair:A"));
            Assert.That(roundTrip.animatedPairs[0].targetLinearJerk.ToVector3(), Is.EqualTo(Vector3.right * 3f));
            Assert.That(roundTrip.animatedPairs[0].effectiveMappingPositionWeight, Is.EqualTo(0.4f).Within(0.0001f));
        }
    }
}
