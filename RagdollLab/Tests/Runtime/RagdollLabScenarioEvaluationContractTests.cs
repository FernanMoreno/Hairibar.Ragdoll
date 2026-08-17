using System;
using NUnit.Framework;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabScenarioEvaluationContractTests
    {
        [Test]
        public void CatalogExposesSevenStableContracts()
        {
            string[] ids = { "PhysicalIntegrity", "Tracking", "GetUp", "Balance", "Stagger", "Props", "Locomotion" };

            for (int i = 0; i < ids.Length; i++)
            {
                ScenarioEvaluationContract contract = RagdollLabScenarioEvaluationCatalog.Resolve(ids[i]);
                Assert.That(contract, Is.Not.Null, ids[i]);
                Assert.That(contract.id, Is.EqualTo(ids[i]));
                Assert.That(contract.version, Is.Not.Empty);
            }
        }

        [Test]
        public void UnknownScenarioFailsClosedBeforeScoring()
        {
            ScenarioEvaluationContract contract = RagdollLabScenarioEvaluationCatalog.Resolve("not-a-scenario");

            Assert.That(contract, Is.Not.Null);
            Assert.That(contract.available, Is.False);
            Assert.That(contract.unavailableReason, Is.EqualTo("scenario_contract_unavailable"));
        }

        [Test]
        public void LocomotionWithoutTaskCompletionIsNotBalanceScored()
        {
            ScenarioComparisonReport comparison = RagdollLabComparison.BuildScenarioComparison(
                CreateReport("baseline", "Locomotion"), CreateReport("candidate", "Locomotion"));

            Assert.That(comparison.comparisonKind, Is.EqualTo("scenario"));
            Assert.That(comparison.contractId, Is.EqualTo("Locomotion"));
            Assert.That(comparison.scenarioEvaluation, Is.Not.Null);
            Assert.That(comparison.scenarioEvaluation.decision, Is.EqualTo("unavailable"));
            Assert.That(comparison.scenarioEvaluation.balanceFallbackUsed, Is.False);
            Assert.That(comparison.scenarioEvaluation.requiredSignalStatuses,
                Has.Some.Matches<RequiredSignalStatus>(status => status.signalId == RagdollLabScenarioSignalIds.LocomotionTaskCompletion));
        }

        [Test]
        public void PhysicalIntegrityUsesTaskMetricsWhenEvidenceExists()
        {
            EvaluationReport baseline = CreateReport("baseline", "PhysicalIntegrity");
            EvaluationReport candidate = CreateReport("candidate", "PhysicalIntegrity");
            candidate.scenarioReport.penetration.max = 0f;
            candidate.scenarioReport.footSlipSpeed.mean = 0.005f;
            candidate.scenarioReport.kineticEnergy.mean = 5f;
            candidate.scenarioReport.kineticEnergy.max = 5f;

            ScenarioComparisonReport comparison = RagdollLabComparison.BuildScenarioComparison(baseline, candidate);

            Assert.That(comparison.contractId, Is.EqualTo("PhysicalIntegrity"));
            Assert.That(comparison.scenarioEvaluation.taskMetrics, Is.Not.Empty);
            Assert.That(comparison.scenarioEvaluation.balanceFallbackUsed, Is.False);
            Assert.That(comparison.scenarioEvaluation.decision, Is.EqualTo("accepted"));
        }

        [Test]
        public void TrackingUsesPairMetricsAndNotBalanceMetrics()
        {
            EvaluationReport baseline = CreateReport("baseline", "Tracking");
            EvaluationReport candidate = CreateReport("candidate", "Tracking");
            baseline.scenarioReport.pairTracking = new[] { Pair(0.20f, 0.30f) };
            candidate.scenarioReport.pairTracking = new[] { Pair(0.05f, 0.08f) };

            ScenarioComparisonReport comparison = RagdollLabComparison.BuildScenarioComparison(baseline, candidate);

            Assert.That(comparison.contractId, Is.EqualTo("Tracking"));
            Assert.That(comparison.scenarioEvaluation.taskMetrics,
                Has.Some.Matches<ScenarioMetric>(metric => metric.name == "TrackingPoseError.mean"));
            Assert.That(comparison.scenarioEvaluation.balanceFallbackUsed, Is.False);
        }

        [Test]
        public void SafetyFailureBlocksTaskImprovement()
        {
            EvaluationReport baseline = CreateReport("baseline", "PhysicalIntegrity");
            EvaluationReport candidate = CreateReport("candidate", "PhysicalIntegrity");
            candidate.scenarioReport.footSlipSpeed.mean = 0.50f;
            candidate.scenarioReport.kineticEnergy.mean = 5f;
            candidate.scenarioReport.kineticEnergy.max = 5f;

            ScenarioComparisonReport comparison = RagdollLabComparison.BuildScenarioComparison(baseline, candidate);

            Assert.That(comparison.scenarioEvaluation.taskDecision, Is.EqualTo("accepted"));
            Assert.That(comparison.scenarioEvaluation.safetyDecision, Is.EqualTo("rejected"));
            Assert.That(comparison.scenarioEvaluation.decision, Is.EqualTo("rejected"));
            Assert.That(comparison.safetyGuardsPassed, Is.False);
        }

        [Test]
        public void UnsupportedProducerRemainsUnavailable()
        {
            string[] scenarios = { "GetUp", "Locomotion", "Props" };
            for (int i = 0; i < scenarios.Length; i++)
            {
                ScenarioComparisonReport comparison = RagdollLabComparison.BuildScenarioComparison(
                    CreateReport("baseline", scenarios[i]), CreateReport("candidate", scenarios[i]));

                Assert.That(comparison.scenarioEvaluation.decision, Is.EqualTo("unavailable"), scenarios[i]);
                Assert.That(comparison.scenarioEvaluation.rejectionReasons,
                    Has.Some.Contains("required_signal_missing"), scenarios[i]);
            }
        }

        [Test]
        public void EquivalentReportsProduceDeterministicDecisionFields()
        {
            EvaluationReport baseline = CreateReport("baseline", "PhysicalIntegrity");
            EvaluationReport candidate = CreateReport("candidate", "PhysicalIntegrity");

            ScenarioComparisonReport first = RagdollLabComparison.BuildScenarioComparison(baseline, candidate);
            ScenarioComparisonReport second = RagdollLabComparison.BuildScenarioComparison(baseline, candidate);

            Assert.That(second.decision, Is.EqualTo(first.decision));
            Assert.That(second.contractId, Is.EqualTo(first.contractId));
            Assert.That(second.contractVersion, Is.EqualTo(first.contractVersion));
            Assert.That(second.scenarioEvaluation.rejectionReasons,
                Is.EqualTo(first.scenarioEvaluation.rejectionReasons));
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
                    frameCount = 10,
                    durationSeconds = 0.2f,
                    capturePointSampleCount = 1,
                    signedSupportMarginAvailable = true,
                    minimumSignedSupportMargin = -0.1f,
                    centerOfMassSpeed = Metric("CenterOfMassSpeed", "m/s", 0.1f),
                    penetration = Metric("PenetrationDepth", "m", 0f),
                    footSlipSpeed = Metric("FootSlipSpeed", "m/s", 0.01f),
                    kineticEnergy = Metric("KineticEnergy", "J", 10f),
                    staggerEpisodes = Array.Empty<StaggerEpisodeReport>()
                }
            };
        }

        static PairTrackingReport Pair(float pose, float velocity)
        {
            return new PairTrackingReport
            {
                id = "pair:root",
                targetAvailable = true,
                physicsAvailable = true,
                sampleCount = 10,
                targetPhysicsDistance = Metric("Pose", "m", pose),
                targetPhysicsAngularError = Metric("Angle", "deg", pose),
                targetPhysicsVelocityError = Metric("Velocity", "m/s", velocity)
            };
        }

        static MetricSummary Metric(string name, string unit, float value)
        {
            return new MetricSummary
            {
                name = name,
                unit = unit,
                mean = value,
                max = value,
                p95 = value,
                count = 1
            };
        }
    }
}
