using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollTuningPlannerTests
    {
        [Test]
        public void SessionCopiesFiniteUniqueBaselineAndRejectsInvalidBudget()
        {
            var source = new List<RagdollTuningParameterValue>
            {
                new("pin", 0.7f),
                new("muscle", 0.4f)
            };
            RagdollTuningSession session = RagdollTuningPlanner.CreateSession("session", "Balancer", source, 2);
            source[0].value = 99f;

            Assert.That(session.baseline[0].value, Is.EqualTo(0.4f));
            Assert.That(session.baselineFingerprint, Is.EqualTo("muscle=0.4|pin=0.7"));
            Assert.That(() => RagdollTuningPlanner.CreateSession("session", "Balancer", source, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => RagdollTuningPlanner.CreateSession("session", "Balancer", source, 65),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => RagdollTuningPlanner.CreateSession("session", "Balancer",
                new List<RagdollTuningParameterValue> { new("pin", float.NaN) }, 1),
                Throws.ArgumentException);
            Assert.That(() => RagdollTuningPlanner.CreateSession("session", "Balancer",
                new List<RagdollTuningParameterValue> { new("pin", 1f), new("pin", 2f) }, 1),
                Throws.ArgumentException);
        }

        [Test]
        public void SingleVariableExperimentRecordsPairAndRejectsInvalidCandidates()
        {
            RagdollTuningSession session = CreateSession(2);
            RagdollTuningDecision started = RagdollTuningPlanner.BeginSingleVariableExperiment(
                session, "exp-1", "pin", 0.8f, "baseline-1", "candidate-1");

            Assert.That(started.decision, Is.EqualTo("started"));
            Assert.That(session.startedExperiments, Is.EqualTo(1));
            Assert.That(session.experiments[0].parameterName, Is.EqualTo("pin"));
            Assert.That(session.experiments[0].baselineRunId, Is.EqualTo("baseline-1"));
            Assert.That(session.experiments[0].candidateRunId, Is.EqualTo("candidate-1"));
            Assert.That(session.experiments[0].candidateValue, Is.EqualTo(0.8f));

            RagdollTuningSession multi = CreateSession(2);
            RagdollTuningDecision multiple = RagdollTuningPlanner.BeginSingleVariableExperiment(
                multi, "exp-multi", new List<RagdollTuningParameterValue>
                {
                    new("pin", 0.8f), new("muscle", 0.9f)
                }, "baseline", "candidate");
            Assert.That(multiple.decision, Is.EqualTo("invalid"));
            Assert.That(multiple.reason, Is.EqualTo("multiple_parameters_changed"));
            Assert.That(multi.startedExperiments, Is.Zero);

            RagdollTuningSession nonFinite = CreateSession(2);
            RagdollTuningDecision invalidValue = RagdollTuningPlanner.BeginSingleVariableExperiment(
                nonFinite, "exp-nan", "pin", float.PositiveInfinity, "baseline", "candidate");
            Assert.That(invalidValue.reason, Is.EqualTo("candidate_value_non_finite"));

            RagdollTuningSession unchanged = CreateSession(2);
            RagdollTuningDecision equal = RagdollTuningPlanner.BeginSingleVariableExperiment(
                unchanged, "exp-equal", "pin", 0.7f, "baseline", "candidate");
            Assert.That(equal.reason, Is.EqualTo("candidate_value_unchanged"));

            RagdollTuningSession missing = CreateSession(2);
            RagdollTuningDecision noParameter = RagdollTuningPlanner.BeginSingleVariableExperiment(
                missing, "exp-missing", "does-not-exist", 0.8f, "baseline", "candidate");
            Assert.That(noParameter.reason, Is.EqualTo("parameter_missing"));
        }

        [Test]
        public void BudgetIsEnforcedAfterRollback()
        {
            RagdollTuningSession session = CreateSession(1);
            RagdollTuningPlanner.BeginSingleVariableExperiment(session, "exp-1", "pin", 0.8f, "b1", "c1");
            RagdollTuningPlanner.Rollback(session, session.experiments[0], "test");

            RagdollTuningDecision exhausted = RagdollTuningPlanner.BeginSingleVariableExperiment(
                session, "exp-2", "pin", 0.9f, "b2", "c2");
            Assert.That(exhausted.reason, Is.EqualTo("experiment_budget_exhausted"));
            Assert.That(session.experiments.Count, Is.EqualTo(1));
        }

        [Test]
        public void StructuralInvalidityRollsBackBeforeSafetyOrStability()
        {
            RagdollTuningSession session = CreateSession(2);
            Start(session, "invalid");

            RagdollTuningDecision result = RagdollTuningPlanner.Evaluate(session, session.experiments[0], null);

            Assert.That(result.decision, Is.EqualTo("invalid"));
            Assert.That(result.stage, Is.EqualTo("structural"));
            Assert.That(result.reason, Is.EqualTo("comparison_missing"));
            Assert.That(result.rollbackRequired, Is.True);
            Assert.That(session.candidateActive, Is.False);
        }

        [Test]
        public void SafetyRejectsBeforeStabilityAndCannotPromote()
        {
            RagdollTuningSession session = CreateSession(2);
            Start(session, "safety");
            BalanceComparisonReport comparison = Comparison(session.experiments[0], "accept", safetyPassed: false);
            comparison.safetyGuards.Add("candidate_foot_slip_increased");

            RagdollTuningDecision result = RagdollTuningPlanner.Evaluate(session, session.experiments[0], comparison);

            Assert.That(result.decision, Is.EqualTo("rejected"));
            Assert.That(result.stage, Is.EqualTo("safety"));
            Assert.That(result.promotionEligible, Is.False);
            Assert.That(session.experiments[0].state, Is.EqualTo("rolled_back"));
        }

        [Test]
        public void AcceptedCandidateIsTheOnlyPromotionEligibleOutcome()
        {
            RagdollTuningSession session = CreateSession(2);
            Start(session, "accepted");
            RagdollTuningDecision result = RagdollTuningPlanner.Evaluate(
                session, session.experiments[0], Comparison(session.experiments[0], "accept", safetyPassed: true));

            Assert.That(result.decision, Is.EqualTo("accepted"));
            Assert.That(result.stage, Is.EqualTo("stability"));
            Assert.That(result.promotionEligible, Is.True);
            Assert.That(session.candidateActive, Is.True);
            Assert.That(session.experiments[0].rollbackRequired, Is.False);
        }

        [Test]
        public void NeutralOutcomeRollsBackAndRepeatedEvaluationIsStable()
        {
            RagdollTuningSession session = CreateSession(2);
            Start(session, "neutral");
            RagdollTuningExperiment experiment = session.experiments[0];
            RagdollTuningDecision first = RagdollTuningPlanner.Evaluate(session, experiment, Comparison(experiment, "neutral", true));
            RagdollTuningDecision second = RagdollTuningPlanner.Evaluate(session, experiment, Comparison(experiment, "accept", true));

            Assert.That(first.decision, Is.EqualTo("neutral"));
            Assert.That(first.stage, Is.EqualTo("stability"));
            Assert.That(first.rollbackRequired, Is.True);
            Assert.That(second.decision, Is.EqualTo(first.decision));
            Assert.That(second.reason, Is.EqualTo(first.reason));
            Assert.That(session.candidateActive, Is.False);
        }

        [Test]
        public void RollbackIsIdempotentAndClearsAcceptedCandidate()
        {
            RagdollTuningSession session = CreateSession(2);
            Start(session, "rollback");
            RagdollTuningExperiment experiment = session.experiments[0];
            RagdollTuningPlanner.Evaluate(session, experiment, Comparison(experiment, "accept", true));

            RagdollTuningDecision first = RagdollTuningPlanner.Rollback(session, experiment, "operator_cancelled");
            RagdollTuningDecision second = RagdollTuningPlanner.Rollback(session, experiment, "different_reason");

            Assert.That(first.decision, Is.EqualTo("accepted"));
            Assert.That(first.reason, Is.EqualTo("operator_cancelled"));
            Assert.That(second.decision, Is.EqualTo(first.decision));
            Assert.That(second.reason, Is.EqualTo(first.reason));
            Assert.That(experiment.state, Is.EqualTo("rolled_back"));
            Assert.That(experiment.promotionEligible, Is.False);
            Assert.That(session.candidateActive, Is.False);
        }

        [Test]
        public void ProvenanceMismatchRollsBackBeforeSafetyScoring()
        {
            RagdollTuningSession session = CreateSession(2);
            Start(session, "provenance");
            RagdollTuningExperiment experiment = session.experiments[0];
            BalanceComparisonReport comparison = Comparison(experiment, "accept", true);
            comparison.candidateRunId = "wrong-run";

            RagdollTuningDecision result = RagdollTuningPlanner.Evaluate(session, experiment, comparison);

            Assert.That(result.decision, Is.EqualTo("invalid"));
            Assert.That(result.stage, Is.EqualTo("structural"));
            Assert.That(result.reason, Is.EqualTo("candidate_run_id_mismatch"));
            Assert.That(session.candidateActive, Is.False);
        }

        [Test]
        public void AcceptedCandidateCanBePromotedAndNextExperimentStarts()
        {
            RagdollTuningSession session = CreateSession(3);
            Start(session, "promote-1");
            RagdollTuningExperiment first = session.experiments[0];
            RagdollTuningPlanner.Evaluate(session, first, Comparison(first, "accept", true));

            RagdollTuningDecision promoted = RagdollTuningPlanner.PromoteAcceptedCandidate(session, first);

            Assert.That(promoted.decision, Is.EqualTo("promoted"));
            Assert.That(session.baselineFingerprint, Is.EqualTo("muscle=0.4|pin=0.8"));
            Assert.That(session.baseline.Find(value => value.name == "pin").value, Is.EqualTo(0.8f));
            Assert.That(session.candidateActive, Is.False);
            Assert.That(first.state, Is.EqualTo("promoted"));
            Assert.That(RagdollTuningPlanner.PromoteAcceptedCandidate(session, first).decision, Is.EqualTo("promoted"));

            RagdollTuningDecision second = RagdollTuningPlanner.BeginSingleVariableExperiment(
                session, "promote-2", "pin", 0.9f, "baseline-2", "candidate-2");

            Assert.That(second.decision, Is.EqualTo("started"));
            Assert.That(session.experiments, Has.Count.EqualTo(2));
            Assert.That(session.experiments[1].baselineValue, Is.EqualTo(0.8f));
        }

        [Test]
        public void RegistryRejectsUnsafeAndIneligibleCandidatesBeforeUnity()
        {
            RagdollTuningParameterRegistry registry = Registry();
            RagdollTuningSession session = CreateSession(3, registry);

            RagdollTuningDecision tooFar = RagdollTuningPlanner.BeginSingleVariableExperiment(
                session, "too-far", "pin", 1.0f, "b", "c");
            Assert.That(tooFar.reason, Is.EqualTo("candidate_delta_exceeds_safe_limit"));

            RagdollTuningSession nonWritable = RagdollTuningPlanner.CreateSession("session", "Balancer",
                new List<RagdollTuningParameterValue> { new("locked", 0.5f) }, 1,
                new RagdollTuningParameterRegistry(new List<RagdollTuningParameterDescriptor>
                {
                    Descriptor("locked", 0f, 1f, 1f, 0.1f, runtimeWritable: false)
                }));
            RagdollTuningDecision locked = RagdollTuningPlanner.BeginSingleVariableExperiment(
                nonWritable, "locked", "locked", 0.6f, "b", "c");
            Assert.That(locked.reason, Is.EqualTo("parameter_not_runtime_writable"));

            RagdollTuningSession restartOnly = RagdollTuningPlanner.CreateSession("session", "Balancer",
                new List<RagdollTuningParameterValue> { new("restart", 0.5f) }, 1,
                new RagdollTuningParameterRegistry(new List<RagdollTuningParameterDescriptor>
                {
                    new RagdollTuningParameterDescriptor
                    {
                        name = "restart", minimum = 0f, maximum = 1f, safeDelta = 1f,
                        step = 0.1f, requiresRestart = true, scenarios = new[] { "Balancer" }
                    }
                }));
            RagdollTuningDecision restart = RagdollTuningPlanner.BeginSingleVariableExperiment(
                restartOnly, "restart", "restart", 0.6f, "b", "c");
            Assert.That(restart.reason, Is.EqualTo("parameter_requires_restart"));

            Assert.That(() => RagdollTuningPlanner.CreateSession("session", "GetUp",
                new List<RagdollTuningParameterValue> { new("pin", 0.7f) }, 1, registry),
                Throws.ArgumentException.With.Message.Contains("parameter_scenario_not_allowed"));
        }

        [Test]
        public void ExecutorRestoresRejectedCandidateAndPromotesAcceptedCandidate()
        {
            RagdollTuningSession acceptedSession = CreateSession(2, Registry());
            Start(acceptedSession, "execute-accepted");
            var acceptedStore = new FakeStore(0.7f, 0.4f);
            var acceptedRunner = new FakeRunner();

            RagdollTuningExecutionResult accepted = RagdollTuningExecutor.Execute(
                acceptedSession, acceptedSession.experiments[0], acceptedStore, acceptedRunner, promoteAcceptedCandidate: true);

            Assert.That(accepted.valid, Is.True);
            Assert.That(accepted.promoted, Is.True);
            Assert.That(accepted.restored, Is.False);
            Assert.That(acceptedStore.Values["pin"], Is.EqualTo(0.8f));
            Assert.That(acceptedSession.baselineFingerprint, Is.EqualTo("muscle=0.4|pin=0.8"));
            Assert.That(acceptedRunner.Bindings, Has.Count.EqualTo(2));
            Assert.That(acceptedRunner.Bindings[0].runRole, Is.EqualTo("baseline"));
            Assert.That(acceptedRunner.Bindings[1].runRole, Is.EqualTo("candidate"));

            RagdollTuningSession rejectedSession = CreateSession(2, Registry());
            Start(rejectedSession, "execute-rejected");
            var rejectedStore = new FakeStore(0.7f, 0.4f);
            var rejectedRunner = new FakeRunner { RejectCandidate = true };

            RagdollTuningExecutionResult rejected = RagdollTuningExecutor.Execute(
                rejectedSession, rejectedSession.experiments[0], rejectedStore, rejectedRunner, promoteAcceptedCandidate: true);

            Assert.That(rejected.valid, Is.True);
            Assert.That(rejected.restored, Is.True);
            Assert.That(rejected.promoted, Is.False);
            Assert.That(rejectedStore.Values["pin"], Is.EqualTo(0.7f));
            Assert.That(rejectedSession.experiments[0].state, Is.EqualTo("rolled_back"));
        }

        [Test]
        public void ExecutorRejectsRunnerProvenanceAndRestoresCandidate()
        {
            RagdollTuningSession session = CreateSession(2, Registry());
            Start(session, "execute-provenance");
            var store = new FakeStore(0.7f, 0.4f);
            var runner = new FakeRunner { CorruptCandidateFingerprint = true };

            RagdollTuningExecutionResult result = RagdollTuningExecutor.Execute(
                session, session.experiments[0], store, runner, promoteAcceptedCandidate: true);

            Assert.That(result.valid, Is.False);
            Assert.That(result.reason, Is.EqualTo("candidate_configuration_fingerprint_mismatch"));
            Assert.That(result.restored, Is.True);
            Assert.That(store.Values["pin"], Is.EqualTo(0.7f));
        }

        [Test]
        public void ExecutorUsesPersistedTransportReportAndExposesVerifiedManifests()
        {
            RagdollTuningSession session = CreateSession(2, Registry(), "artifacts");
            Start(session, "execute-artifact");
            var store = new FakeStore(0.7f, 0.4f);
            var runner = new FakeRunner();
            var transport = new FakeTransport();

            RagdollTuningExecutionResult result = RagdollTuningExecutor.Execute(
                session, session.experiments[0], store, runner, promoteAcceptedCandidate: false,
                artifactTransport: transport);

            Assert.That(result.valid, Is.True);
            Assert.That(result.restored, Is.True);
            Assert.That(result.baselineArtifact, Is.Not.Null);
            Assert.That(result.candidateArtifact, Is.Not.Null);
            Assert.That(transport.Bindings, Has.Count.EqualTo(2));
            Assert.That(transport.Bindings[0].artifactDirectory, Does.EndWith("execute-artifact-baseline"));
            Assert.That(store.Values["pin"], Is.EqualTo(0.7f));
        }

        [Test]
        public void ExecutorEvaluatesPersistedPairThroughPlannerWithoutApplyingOrPromoting()
        {
            RagdollTuningSession session = CreateSession(1, Registry(), "/artifacts");
            Start(session, "persisted-experiment");
            RagdollTuningExperiment experiment = session.experiments[0];
            var transport = new FakeTransport();

            RagdollTuningExecutionResult result = RagdollTuningExecutor.EvaluatePersistedPair(
                session, experiment, transport);

            Assert.That(result.persistedPair, Is.True);
            Assert.That(result.valid, Is.True);
            Assert.That(result.decision.decision, Is.EqualTo("accepted"));
            Assert.That(result.baselineReport, Is.Not.Null);
            Assert.That(result.candidateReport, Is.Not.Null);
            Assert.That(result.baselineArtifact, Is.Not.Null);
            Assert.That(result.candidateArtifact, Is.Not.Null);
            Assert.That(result.restored, Is.False);
            Assert.That(result.promoted, Is.False);
            Assert.That(session.candidateActive, Is.True);
            Assert.That(transport.Bindings, Has.Count.EqualTo(2));
        }

        static RagdollTuningSession CreateSession(int budget, RagdollTuningParameterRegistry registry = null, string artifactRoot = null)
        {
            return RagdollTuningPlanner.CreateSession("session", "Balancer", new List<RagdollTuningParameterValue>
            {
                new("pin", 0.7f),
                new("muscle", 0.4f)
            }, budget, registry, artifactRoot);
        }

        static void Start(RagdollTuningSession session, string id)
        {
            RagdollTuningDecision result = RagdollTuningPlanner.BeginSingleVariableExperiment(
                session, id, "pin", 0.8f, id + "-baseline", id + "-candidate");
            Assert.That(result.decision, Is.EqualTo("started"));
        }

        static BalanceComparisonReport Comparison(RagdollTuningExperiment experiment, string decision, bool safetyPassed)
        {
            var report = new BalanceComparisonReport
            {
                decision = decision,
                tuningSessionId = experiment.tuningSessionId,
                experimentId = experiment.experimentId,
                provenanceAvailable = true,
                scenarioProfile = "Balancer",
                profileAvailable = true,
                setupMatched = true,
                baselineRunId = experiment.baselineRunId,
                candidateRunId = experiment.candidateRunId,
                baselineConfigurationFingerprint = experiment.baselineConfigurationFingerprint,
                candidateConfigurationFingerprint = experiment.candidateConfigurationFingerprint,
                treatmentParameter = experiment.parameterName,
                treatmentValueAvailable = true,
                treatmentValue = experiment.candidateValue,
                safetyGuardsPassed = safetyPassed,
                stabilityMetrics = new List<ComparisonMetric>
                {
                    Metric("SignedSupportMargin.minimum", -0.1f, -0.2f),
                    Metric("RecoveryTime.seconds", 0.8f, 1f)
                },
                safetyMetrics = new List<ComparisonMetric>
                {
                    Metric("FootSlipSpeed.mean", 0.01f, 0.01f)
                },
                safetyGuards = new List<string>(),
                rejectionReasons = new List<string>()
            };
            return report;
        }

        static RagdollTuningParameterRegistry Registry()
        {
            return new RagdollTuningParameterRegistry(new List<RagdollTuningParameterDescriptor>
            {
                Descriptor("pin", 0f, 1f, 0.2f, 0.1f),
                Descriptor("muscle", 0f, 1f, 0.5f, 0.1f)
            });
        }

        static RagdollTuningParameterDescriptor Descriptor(string name, float minimum, float maximum,
            float safeDelta, float step, bool runtimeWritable = true)
        {
            return new RagdollTuningParameterDescriptor
            {
                name = name,
                minimum = minimum,
                maximum = maximum,
                safeDelta = safeDelta,
                step = step,
                runtimeWritable = runtimeWritable,
                scenarios = new[] { "Balancer" }
            };
        }

        sealed class FakeStore : IRagdollTuningParameterStore
        {
            public readonly Dictionary<string, float> Values = new();
            public FakeStore(float pin, float muscle)
            {
                Values["pin"] = pin;
                Values["muscle"] = muscle;
            }

            public bool TryRead(string name, out float value) => Values.TryGetValue(name, out value);
            public bool TryWrite(string name, float value)
            {
                if (!Values.ContainsKey(name)) return false;
                Values[name] = value;
                return true;
            }
        }

        sealed class FakeRunner : IRagdollTuningScenarioRunner
        {
            public readonly List<RagdollTuningRunBinding> Bindings = new();
            public bool RejectCandidate;
            public bool CorruptCandidateFingerprint;

            public EvaluationReport Run(RagdollTuningRunBinding binding)
            {
                Bindings.Add(binding);
                string fingerprint = CorruptCandidateFingerprint && binding.runRole == "candidate"
                    ? "wrong-fingerprint" : binding.configurationFingerprint;
                bool candidate = binding.runRole == "candidate";
                return new EvaluationReport
                {
                    metadata = new RagdollLabMetadata
                    {
                        runId = binding.runId,
                        scenario = "Balancer",
                        scenarioProfile = "Balancer",
                        tuningSessionId = binding.sessionId,
                        experimentId = binding.experimentId,
                        runRole = binding.runRole,
                        configurationFingerprint = fingerprint,
                        baselineConfigurationFingerprint = binding.baselineConfigurationFingerprint,
                        treatmentParameter = binding.treatmentParameter,
                        treatmentValueAvailable = binding.treatmentValueAvailable,
                        treatmentValue = binding.treatmentValue,
                        fixedDeltaTime = 0.02f,
                        gravityMagnitude = 9.81f,
                        characterHeight = 1.8f,
                        totalMass = 70f,
                        captureRoot = "rig",
                        initialConditionFingerprint = "same-pose"
                    },
                    completed = true,
                    finiteData = true,
                    scenarioReport = Scenario(candidate && RejectCandidate ? -0.2f : candidate ? -0.1f : -0.2f,
                        candidate && RejectCandidate ? 0.5f : 0.01f)
                };
            }

            static ScenarioReport Scenario(float margin, float footSlip)
            {
                return new ScenarioReport
                {
                    name = "Balancer",
                    frameCount = 1,
                    durationSeconds = 1f,
                    balanceTelemetryAvailable = true,
                    signedSupportMarginAvailable = true,
                    minimumSignedSupportMargin = margin,
                    centerOfMassSpeed = Metric("CenterOfMassSpeed", "m/s", margin < -0.15f ? 0.4f : 0.3f),
                    recoveryTimeSeconds = margin < -0.15f ? 1f : 0.8f,
                    kineticEnergy = Metric("KineticEnergy", "J", 10f),
                    penetration = Metric("PenetrationDepth", "m", 0f),
                    footSlipSpeed = Metric("FootSlipSpeed", "m/s", footSlip),
                    staggerEpisodes = Array.Empty<StaggerEpisodeReport>()
                };
            }

            static MetricSummary Metric(string name, string unit, float value)
            {
                return new MetricSummary { name = name, unit = unit, count = 1, current = value,
                    mean = value, rms = value, p95 = value, max = value };
            }
        }

        sealed class FakeTransport : IRagdollTuningArtifactTransport
        {
            public readonly List<RagdollTuningRunBinding> Bindings = new();

            public bool TryRead(string directory, RagdollTuningRunBinding expected,
                out EvaluationReport report, out RagdollTuningArtifactManifest manifest, out string reason)
            {
                Bindings.Add(expected);
                report = new FakeRunner().Run(expected);
                manifest = new RagdollTuningArtifactManifest
                {
                    sessionId = expected.sessionId,
                    experimentId = expected.experimentId,
                    runId = expected.runId,
                    runRole = expected.runRole,
                    configurationFingerprint = expected.configurationFingerprint,
                    baselineConfigurationFingerprint = expected.baselineConfigurationFingerprint,
                    treatmentParameter = expected.treatmentParameter,
                    treatmentValueAvailable = expected.treatmentValueAvailable,
                    treatmentValue = expected.treatmentValue
                };
                reason = null;
                return true;
            }
        }

        static ComparisonMetric Metric(string name, float current, float baseline)
        {
            return new ComparisonMetric
            {
                name = name,
                current = current,
                baseline = baseline,
                delta = current - baseline,
                relativeDelta = baseline == 0f ? 0f : (current - baseline) / Math.Abs(baseline)
            };
        }
    }
}
