using NUnit.Framework;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabComparisonTests
    {
        [Test]
        public void MatchingRecoverablePushAcceptsSafeStabilityImprovement()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            baseline.scenarioReport.minimumSignedSupportMargin = -0.20f;
            candidate.scenarioReport.minimumSignedSupportMargin = -0.10f;
            baseline.scenarioReport.centerOfMassSpeed.mean = 0.40f;
            candidate.scenarioReport.centerOfMassSpeed.mean = 0.30f;
            baseline.scenarioReport.recoveryTimeSeconds = 1f;
            candidate.scenarioReport.recoveryTimeSeconds = 0.8f;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.setupMatched, Is.True);
            Assert.That(comparison.safetyGuardsPassed, Is.True);
            Assert.That(comparison.decision, Is.EqualTo("accept"));
        }

        [Test]
        public void MismatchedInitialConditionsAreInvalidBeforeScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            candidate.metadata.initialConditionFingerprint = "different-pose";

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("paired_setup_mismatch"));
        }

        [Test]
        public void SafetyRegressionRejectsCandidateEvenWhenMarginImproves()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            baseline.scenarioReport.minimumSignedSupportMargin = -0.20f;
            candidate.scenarioReport.minimumSignedSupportMargin = -0.05f;
            baseline.scenarioReport.footSlipSpeed.mean = 0.01f;
            candidate.scenarioReport.footSlipSpeed.mean = 0.50f;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("reject"));
            Assert.That(comparison.safetyGuardsPassed, Is.False);
            Assert.That(comparison.safetyGuards, Does.Contain("candidate_foot_slip_increased"));
        }

        [Test]
        public void LocomotionDoesNotTreatComSpeedAsUniversalStabilityObjective()
        {
            EvaluationReport baseline = CreateReport("baseline", "Locomotion");
            EvaluationReport candidate = CreateReport("candidate", "Locomotion");
            candidate.scenarioReport.centerOfMassSpeed.mean = 0.1f;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            ComparisonMetric comMetric = Find(comparison, "CenterOfMassSpeed.mean");
            Assert.That(comMetric.expectation, Is.EqualTo("neutral"));
            Assert.That(comMetric.regression, Is.False);
        }

        [Test]
        public void NonFiniteCandidateIsInvalidBeforeSafetyScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            candidate.finiteData = false;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("non_finite_run"));
        }

        [Test]
        public void FallAndUnpinnedRegressionHasSafetyPrecedence()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            candidate.scenarioReport.fallenFrameCount = 1;
            candidate.scenarioReport.unpinnedStaggerEpisodeCount = 1;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("reject"));
            Assert.That(comparison.safetyGuards, Does.Contain("candidate_fallen_frames_increased"));
            Assert.That(comparison.safetyGuards, Does.Contain("candidate_unpinned_episodes_increased"));
        }

        [Test]
        public void TorqueAndEnergyGuardsRejectUnsafeCandidate()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            candidate.scenarioReport.balancerTorque = Metric("BalancerTorque", "N*m", 100f);
            candidate.scenarioReport.kineticEnergy.max = 100f;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("reject"));
            Assert.That(comparison.safetyGuards, Does.Contain("candidate_balancer_torque_exceeded"));
            Assert.That(comparison.safetyGuards, Does.Contain("candidate_energy_increased"));
        }

        [Test]
        public void BalancerPairWithNoStabilityImprovementIsNeutralAndActionable()
        {
            EvaluationReport baseline = CreateReport("baseline", "BalancerOn");
            EvaluationReport candidate = CreateReport("candidate", "BalancerOn");

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.scenarioProfile, Is.EqualTo("Balancer"));
            Assert.That(comparison.profileAvailable, Is.True);
            Assert.That(comparison.safetyGuardsPassed, Is.True);
            Assert.That(comparison.decision, Is.EqualTo("neutral"));
            Assert.That(comparison.rejectionReasons, Does.Contain("balancer_no_stability_improvement"));
        }

        [Test]
        public void UnknownScenarioIsInvalidBeforeComparisonScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "UnknownScenario");
            EvaluationReport candidate = CreateReport("candidate", "UnknownScenario");

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("scenario_profile_unavailable"));
            Assert.That(comparison.profileAvailable, Is.False);
        }

        static EvaluationReport CreateReport(string runId, string scenario)
        {
            return new EvaluationReport
            {
                metadata = new RagdollLabMetadata
                {
                    runId = runId,
                    scenario = scenario,
                    fixedDeltaTime = 0.02f,
                    gravityMagnitude = 9.81f,
                    characterHeight = 1.8f,
                    totalMass = 70f,
                    captureRoot = "rig",
                    initialConditionFingerprint = "same-pose"
                },
                completed = true,
                finiteData = true,
                scenarioReport = new ScenarioReport
                {
                    name = scenario,
                    balanceTelemetryAvailable = true,
                    signedSupportMarginAvailable = true,
                    centerOfMassSpeed = Metric("CenterOfMassSpeed", "m/s", 0.4f),
                    penetration = Metric("PenetrationDepth", "m", 0f),
                    footSlipSpeed = Metric("FootSlipSpeed", "m/s", 0.01f),
                    kineticEnergy = Metric("KineticEnergy", "J", 10f),
                    staggerEpisodes = new StaggerEpisodeReport[0]
                }
            };
        }

        static MetricSummary Metric(string name, string unit, float value)
        {
            return new MetricSummary { name = name, unit = unit, count = 1, current = value, mean = value, rms = value, p95 = value, max = value };
        }

        static ComparisonMetric Find(BalanceComparisonReport report, string name)
        {
            for (int i = 0; i < report.stabilityMetrics.Count; i++)
                if (report.stabilityMetrics[i].name == name) return report.stabilityMetrics[i];
            Assert.Fail("Missing comparison metric: " + name);
            return null;
        }
    }
}
