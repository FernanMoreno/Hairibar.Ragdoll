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
        public void LocomotionWithoutTrackingOrTaskCompletionIsInvalidBeforeBalanceScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "Locomotion");
            EvaluationReport candidate = CreateReport("candidate", "Locomotion");

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("required_signals_missing"));
            Assert.That(comparison.rejectionReasons, Has.Some.Contains("tracking.poseError"));
            Assert.That(comparison.rejectionReasons, Has.Some.Contains("locomotion.taskCompletion"));
        }

        [Test]
        public void GetUpWithoutExplicitCompletionCannotBeAccepted()
        {
            EvaluationReport baseline = CreateReport("baseline", "GetUp");
            EvaluationReport candidate = CreateReport("candidate", "GetUp");

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.rejectionReasons, Has.Some.Contains("recovery.completion"));
        }

        [Test]
        public void StaggerWithoutReplantCannotBeCountedAsRecovery()
        {
            EvaluationReport baseline = CreateReport("baseline", "Stagger");
            EvaluationReport candidate = CreateReport("candidate", "Stagger");

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.rejectionReasons, Has.Some.Contains("stagger.replant"));
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
        public void NonFiniteRequiredSignalIsInvalidBeforeSafetyScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            candidate.scenarioReport.footSlipSpeed.mean = float.NaN;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("required_signals_missing"));
            Assert.That(comparison.rejectionReasons, Has.Some.Contains(RagdollLabScenarioSignalIds.FootSlip));
        }

        [Test]
        public void NonFiniteCapturePointEvidenceIsInvalidBeforeSafetyScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "RecoverablePush");
            EvaluationReport candidate = CreateReport("candidate", "RecoverablePush");
            candidate.scenarioReport.capturePointNonFiniteSampleCount = 1;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("required_signals_missing"));
            Assert.That(comparison.rejectionReasons, Has.Some.Contains(RagdollLabScenarioSignalIds.CapturePoint));
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

        [Test]
        public void MatchingTuningProvenanceIsCopiedIntoBalanceComparison()
        {
            EvaluationReport baseline = CreateReport("baseline", "Balancer");
            EvaluationReport candidate = CreateReport("candidate", "Balancer");
            ApplyProvenance(baseline, "b-config", "baseline", 0.7f);
            ApplyProvenance(candidate, "c-config", "candidate", 0.8f);
            candidate.metadata.baselineConfigurationFingerprint = "b-config";
            baseline.scenarioReport.minimumSignedSupportMargin = -0.2f;
            baseline.scenarioReport.centerOfMassSpeed.mean = 0.4f;
            baseline.scenarioReport.recoveryTimeSeconds = 1f;
            candidate.scenarioReport.minimumSignedSupportMargin = -0.1f;
            candidate.scenarioReport.centerOfMassSpeed.mean = 0.3f;
            candidate.scenarioReport.recoveryTimeSeconds = 0.8f;

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("accept"));
            Assert.That(comparison.provenanceAvailable, Is.True);
            Assert.That(comparison.experimentId, Is.EqualTo("experiment"));
            Assert.That(comparison.baselineConfigurationFingerprint, Is.EqualTo("b-config"));
            Assert.That(comparison.candidateConfigurationFingerprint, Is.EqualTo("c-config"));
            Assert.That(comparison.treatmentParameter, Is.EqualTo("pin"));
            Assert.That(comparison.treatmentValue, Is.EqualTo(0.8f));
        }

        [Test]
        public void MismatchedTuningProvenanceIsInvalidBeforeScoring()
        {
            EvaluationReport baseline = CreateReport("baseline", "Balancer");
            EvaluationReport candidate = CreateReport("candidate", "Balancer");
            ApplyProvenance(baseline, "b-config", "baseline", 0.7f);
            ApplyProvenance(candidate, "c-config", "candidate", 0.8f);
            candidate.metadata.baselineConfigurationFingerprint = "wrong-baseline";

            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate);

            Assert.That(comparison.decision, Is.EqualTo("invalid"));
            Assert.That(comparison.invalidReason, Is.EqualTo("provenance_mismatch"));
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
                    frameCount = 1,
                    durationSeconds = 0.02f,
                    balanceTelemetryAvailable = true,
                    capturePointSampleCount = 1,
                    signedSupportMarginAvailable = true,
                    centerOfMassSpeed = Metric("CenterOfMassSpeed", "m/s", 0.4f),
                    penetration = Metric("PenetrationDepth", "m", 0f),
                    footSlipSpeed = Metric("FootSlipSpeed", "m/s", 0.01f),
                    kineticEnergy = Metric("KineticEnergy", "J", 10f),
                    staggerEpisodes = new StaggerEpisodeReport[0]
                }
            };
        }

        static void ApplyProvenance(EvaluationReport report, string configurationFingerprint, string runRole, float treatmentValue)
        {
            report.metadata.tuningSessionId = "session";
            report.metadata.experimentId = "experiment";
            report.metadata.runRole = runRole;
            report.metadata.configurationFingerprint = configurationFingerprint;
            report.metadata.baselineConfigurationFingerprint = configurationFingerprint;
            report.metadata.treatmentParameter = "pin";
            report.metadata.treatmentValueAvailable = true;
            report.metadata.treatmentValue = treatmentValue;
        }

        static MetricSummary Metric(string name, string unit, float value)
        {
            return new MetricSummary { name = name, unit = unit, count = 1, current = value, mean = value, rms = value, p95 = value, max = value };
        }

    }
}
