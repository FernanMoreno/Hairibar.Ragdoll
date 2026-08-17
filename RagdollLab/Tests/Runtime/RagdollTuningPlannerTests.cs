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
            BalanceComparisonReport comparison = Comparison("accept", safetyPassed: false);
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
                session, session.experiments[0], Comparison("accept", safetyPassed: true));

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
            RagdollTuningDecision first = RagdollTuningPlanner.Evaluate(session, experiment, Comparison("neutral", true));
            RagdollTuningDecision second = RagdollTuningPlanner.Evaluate(session, experiment, Comparison("accept", true));

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
            RagdollTuningPlanner.Evaluate(session, experiment, Comparison("accept", true));

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

        static RagdollTuningSession CreateSession(int budget)
        {
            return RagdollTuningPlanner.CreateSession("session", "Balancer", new List<RagdollTuningParameterValue>
            {
                new("pin", 0.7f),
                new("muscle", 0.4f)
            }, budget);
        }

        static void Start(RagdollTuningSession session, string id)
        {
            RagdollTuningDecision result = RagdollTuningPlanner.BeginSingleVariableExperiment(
                session, id, "pin", 0.8f, id + "-baseline", id + "-candidate");
            Assert.That(result.decision, Is.EqualTo("started"));
        }

        static BalanceComparisonReport Comparison(string decision, bool safetyPassed)
        {
            var report = new BalanceComparisonReport
            {
                decision = decision,
                scenarioProfile = "Balancer",
                profileAvailable = true,
                setupMatched = true,
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
