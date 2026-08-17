using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    [Serializable]
    public sealed class RagdollTuningParameterValue
    {
        public string name;
        public float value;

        public RagdollTuningParameterValue() { }

        public RagdollTuningParameterValue(string name, float value)
        {
            this.name = name;
            this.value = value;
        }
    }

    [Serializable]
    public sealed class RagdollTuningDecision
    {
        public string decision = "invalid";
        public string stage = "structural";
        public string reason = "unavailable";
        public string experimentId;
        public string scenarioProfile;
        public bool valid;
        public bool promotionEligible;
        public bool rollbackRequired;
        public bool candidateActive;
    }

    [Serializable]
    public sealed class RagdollTuningExperiment
    {
        public string experimentId;
        public string parameterName;
        public float baselineValue;
        public float candidateValue;
        public string baselineRunId;
        public string candidateRunId;
        public string scenarioProfile;
        public string state = "active";
        public string decision = "pending";
        public string decisionStage = "unavailable";
        public string decisionReason = "unavailable";
        public bool promotionEligible;
        public bool rollbackRequired;
        public bool candidateActive = true;
        public BalanceComparisonReport comparison;
    }

    [Serializable]
    public sealed class RagdollTuningSession
    {
        public string sessionId;
        public string scenarioProfile;
        public string baselineFingerprint;
        public int maxExperiments;
        public int startedExperiments;
        public bool candidateActive;
        public string activeExperimentId;
        public string lastDecision = "unavailable";
        public string lastReason = "unavailable";
        public List<RagdollTuningParameterValue> baseline = new();
        public List<RagdollTuningExperiment> experiments = new();
    }

    /// <summary>
    /// Pure, bounded protocol for proposing and judging balance tuning values.
    /// It owns only serializable evidence; it never applies values to Unity.
    /// </summary>
    public static class RagdollTuningPlanner
    {
        public const int MaxExperiments = 64;
        public const int MaxBaselineParameters = 64;

        public static RagdollTuningSession CreateSession(
            string sessionId,
            string scenarioProfile,
            IList<RagdollTuningParameterValue> baseline,
            int maxExperiments)
        {
            RequireText(sessionId, "sessionId");
            RequireText(scenarioProfile, "scenarioProfile");
            if (baseline == null || baseline.Count == 0 || baseline.Count > MaxBaselineParameters)
                throw new ArgumentException("baseline must contain between one and 64 parameters", "baseline");
            if (maxExperiments < 1 || maxExperiments > MaxExperiments)
                throw new ArgumentOutOfRangeException("maxExperiments", "experiment budget must be between one and 64");

            var copied = new List<RagdollTuningParameterValue>(baseline.Count);
            for (int i = 0; i < baseline.Count; i++)
            {
                RagdollTuningParameterValue value = baseline[i];
                if (value == null || string.IsNullOrWhiteSpace(value.name))
                    throw new ArgumentException("baseline parameter names are required", "baseline");
                if (!IsFinite(value.value))
                    throw new ArgumentException("baseline parameter values must be finite", "baseline");
                if (Find(copied, value.name) != null)
                    throw new ArgumentException("baseline parameter names must be unique: " + value.name, "baseline");
                copied.Add(new RagdollTuningParameterValue(value.name, value.value));
            }

            copied.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return new RagdollTuningSession
            {
                sessionId = sessionId,
                scenarioProfile = scenarioProfile,
                baseline = copied,
                baselineFingerprint = Fingerprint(copied),
                maxExperiments = maxExperiments,
                lastReason = "baseline_captured"
            };
        }

        public static RagdollTuningDecision BeginSingleVariableExperiment(
            RagdollTuningSession session,
            string experimentId,
            string parameterName,
            float candidateValue,
            string baselineRunId,
            string candidateRunId)
        {
            if (session == null)
                return Invalid(null, "session_missing");
            if (string.IsNullOrWhiteSpace(parameterName))
                return Invalid(session, "parameter_missing", experimentId);

            var candidate = CopyParameters(session.baseline);
            RagdollTuningParameterValue target = Find(candidate, parameterName);
            if (target == null)
                return Invalid(session, "parameter_missing", experimentId);
            target.value = candidateValue;
            return BeginSingleVariableExperiment(session, experimentId, candidate, baselineRunId, candidateRunId);
        }

        public static RagdollTuningDecision BeginSingleVariableExperiment(
            RagdollTuningSession session,
            string experimentId,
            IList<RagdollTuningParameterValue> candidate,
            string baselineRunId,
            string candidateRunId)
        {
            if (session == null)
                return Invalid(null, "session_missing", experimentId);
            if (string.IsNullOrWhiteSpace(experimentId))
                return Invalid(session, "experiment_id_missing", experimentId);
            if (string.IsNullOrWhiteSpace(baselineRunId) || string.IsNullOrWhiteSpace(candidateRunId))
                return Invalid(session, "paired_run_id_missing", experimentId);
            if (session.candidateActive)
                return Invalid(session, "candidate_active", experimentId);
            if (session.startedExperiments >= session.maxExperiments)
                return Invalid(session, "experiment_budget_exhausted", experimentId);
            if (candidate == null || candidate.Count != session.baseline.Count)
                return Invalid(session, "candidate_parameter_set_mismatch", experimentId);

            var candidateByName = new List<RagdollTuningParameterValue>(candidate.Count);
            for (int i = 0; i < candidate.Count; i++)
            {
                RagdollTuningParameterValue value = candidate[i];
                if (value == null || string.IsNullOrWhiteSpace(value.name))
                    return Invalid(session, "candidate_parameter_missing", experimentId);
                if (!IsFinite(value.value))
                    return Invalid(session, "candidate_value_non_finite", experimentId);
                if (Find(candidateByName, value.name) != null)
                    return Invalid(session, "candidate_parameter_duplicate", experimentId);
                candidateByName.Add(value);
            }

            RagdollTuningParameterValue changed = null;
            int changedCount = 0;
            for (int i = 0; i < session.baseline.Count; i++)
            {
                RagdollTuningParameterValue baselineValue = session.baseline[i];
                RagdollTuningParameterValue candidateValue = Find(candidateByName, baselineValue.name);
                if (candidateValue == null)
                    return Invalid(session, "candidate_parameter_missing", experimentId);
                if (!IsFinite(baselineValue.value))
                    return Invalid(session, "baseline_value_non_finite", experimentId);
                if (!Mathf.Approximately(baselineValue.value, candidateValue.value))
                {
                    changed = candidateValue;
                    changedCount++;
                }
            }
            if (changedCount != 1)
                return Invalid(session, changedCount == 0 ? "candidate_value_unchanged" : "multiple_parameters_changed", experimentId);

            var experiment = new RagdollTuningExperiment
            {
                experimentId = experimentId,
                parameterName = changed.name,
                baselineValue = Find(session.baseline, changed.name).value,
                candidateValue = changed.value,
                baselineRunId = baselineRunId,
                candidateRunId = candidateRunId,
                scenarioProfile = session.scenarioProfile
            };
            if (session.experiments == null)
                session.experiments = new List<RagdollTuningExperiment>();
            session.experiments.Add(experiment);
            session.startedExperiments++;
            session.candidateActive = true;
            session.activeExperimentId = experiment.experimentId;
            session.lastDecision = "started";
            session.lastReason = "one_variable_candidate_recorded";
            return Decision(experiment, "started", "structural", session.lastReason, true, false, true);
        }

        public static RagdollTuningDecision Evaluate(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            BalanceComparisonReport comparison)
        {
            if (session == null || experiment == null)
                return Invalid(session, "experiment_missing", experiment?.experimentId);
            if (!Contains(session, experiment))
                return Invalid(session, "experiment_not_in_session", experiment.experimentId);
            if (!string.Equals(experiment.state, "active", StringComparison.Ordinal))
                return ExistingDecision(session, experiment);

            experiment.comparison = comparison;
            if (!StructuralEvidenceIsValid(session, experiment, comparison))
                return FinalizeRejected(session, experiment, "invalid", "structural", StructuralReason(session, experiment, comparison));

            if (!comparison.safetyGuardsPassed || comparison.safetyGuards == null || comparison.safetyGuards.Count > 0)
                return FinalizeRejected(session, experiment, "rejected", "safety", "safety_guards_failed");

            if (string.Equals(comparison.decision, "accept", StringComparison.Ordinal))
            {
                experiment.state = "accepted";
                experiment.decision = "accepted";
                experiment.decisionStage = "stability";
                experiment.decisionReason = "explicit_stability_improvement";
                experiment.promotionEligible = true;
                experiment.rollbackRequired = false;
                experiment.candidateActive = true;
                session.lastDecision = experiment.decision;
                session.lastReason = experiment.decisionReason;
                return Decision(experiment, experiment.decision, experiment.decisionStage, experiment.decisionReason, true, false, true);
            }

            if (string.Equals(comparison.decision, "neutral", StringComparison.Ordinal))
                return FinalizeRejected(session, experiment, "neutral", "stability", "no_explicit_stability_improvement");
            return FinalizeRejected(session, experiment, "rejected", "stability", "stability_regression");
        }

        public static RagdollTuningDecision Evaluate(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            EvaluationReport baseline,
            EvaluationReport candidate,
            RagdollLabThresholds thresholds = null)
        {
            BalanceComparisonReport comparison = RagdollLabComparison.BuildBalanceComparison(baseline, candidate, thresholds);
            return Evaluate(session, experiment, comparison);
        }

        public static RagdollTuningDecision Rollback(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            string reason)
        {
            if (session == null || experiment == null)
                return Invalid(session, "experiment_missing", experiment?.experimentId);
            if (!Contains(session, experiment))
                return Invalid(session, "experiment_not_in_session", experiment.experimentId);
            if (string.Equals(experiment.state, "rolled_back", StringComparison.Ordinal))
                return ExistingDecision(session, experiment);

            string rollbackReason = string.IsNullOrWhiteSpace(reason) ? "operator_requested_rollback" : reason;
            experiment.state = "rolled_back";
            experiment.rollbackRequired = true;
            experiment.promotionEligible = false;
            experiment.candidateActive = false;
            experiment.decisionStage = "rollback";
            experiment.decisionReason = rollbackReason;
            session.candidateActive = false;
            session.activeExperimentId = null;
            session.lastDecision = experiment.decision;
            session.lastReason = rollbackReason;
            return Decision(experiment, experiment.decision, "rollback", rollbackReason, true, true, false);
        }

        static bool StructuralEvidenceIsValid(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            BalanceComparisonReport comparison)
        {
            return comparison != null
                && comparison.profileAvailable
                && comparison.setupMatched
                && string.Equals(session.scenarioProfile, experiment.scenarioProfile, StringComparison.Ordinal)
                && string.Equals(session.scenarioProfile, comparison.scenarioProfile, StringComparison.Ordinal)
                && (string.Equals(comparison.decision, "accept", StringComparison.Ordinal)
                    || string.Equals(comparison.decision, "reject", StringComparison.Ordinal)
                    || string.Equals(comparison.decision, "neutral", StringComparison.Ordinal))
                && AreFinite(comparison.stabilityMetrics)
                && AreFinite(comparison.safetyMetrics);
        }

        static string StructuralReason(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            BalanceComparisonReport comparison)
        {
            if (comparison == null) return "comparison_missing";
            if (!comparison.profileAvailable) return "comparison_profile_unavailable";
            if (!comparison.setupMatched) return "paired_setup_mismatch";
            if (!string.Equals(session.scenarioProfile, comparison.scenarioProfile, StringComparison.Ordinal)) return "scenario_profile_mismatch";
            if (!AreFinite(comparison.stabilityMetrics) || !AreFinite(comparison.safetyMetrics)) return "comparison_non_finite";
            return "comparison_decision_invalid";
        }

        static RagdollTuningDecision FinalizeRejected(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            string decision,
            string stage,
            string reason)
        {
            experiment.state = "rolled_back";
            experiment.decision = decision;
            experiment.decisionStage = stage;
            experiment.decisionReason = reason;
            experiment.promotionEligible = false;
            experiment.rollbackRequired = true;
            experiment.candidateActive = false;
            session.candidateActive = false;
            session.activeExperimentId = null;
            session.lastDecision = decision;
            session.lastReason = reason;
            return Decision(experiment, decision, stage, reason, false, true, false);
        }

        static RagdollTuningDecision ExistingDecision(RagdollTuningSession session, RagdollTuningExperiment experiment)
        {
            return Decision(experiment, experiment.decision, experiment.decisionStage, experiment.decisionReason,
                experiment.decision != "invalid", experiment.rollbackRequired, session != null && session.candidateActive);
        }

        static RagdollTuningDecision Invalid(RagdollTuningSession session, string reason, string experimentId = null)
        {
            if (session != null)
            {
                session.lastDecision = "invalid";
                session.lastReason = reason;
            }
            return new RagdollTuningDecision
            {
                decision = "invalid",
                stage = "structural",
                reason = reason,
                experimentId = experimentId,
                scenarioProfile = session?.scenarioProfile,
                valid = false,
                promotionEligible = false,
                rollbackRequired = true,
                candidateActive = session != null && session.candidateActive
            };
        }

        static RagdollTuningDecision Decision(
            RagdollTuningExperiment experiment,
            string decision,
            string stage,
            string reason,
            bool valid,
            bool rollbackRequired,
            bool candidateActive)
        {
            return new RagdollTuningDecision
            {
                decision = decision,
                stage = stage,
                reason = reason,
                experimentId = experiment?.experimentId,
                scenarioProfile = experiment?.scenarioProfile,
                valid = valid,
                promotionEligible = experiment != null && experiment.promotionEligible,
                rollbackRequired = rollbackRequired,
                candidateActive = candidateActive
            };
        }

        static bool Contains(RagdollTuningSession session, RagdollTuningExperiment experiment)
        {
            return session.experiments != null && session.experiments.Contains(experiment);
        }

        static List<RagdollTuningParameterValue> CopyParameters(IList<RagdollTuningParameterValue> source)
        {
            var result = new List<RagdollTuningParameterValue>(source.Count);
            for (int i = 0; i < source.Count; i++)
                result.Add(source[i] == null ? null : new RagdollTuningParameterValue(source[i].name, source[i].value));
            return result;
        }

        static RagdollTuningParameterValue Find(IList<RagdollTuningParameterValue> values, string name)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Count; i++)
                if (values[i] != null && string.Equals(values[i].name, name, StringComparison.Ordinal)) return values[i];
            return null;
        }

        static bool AreFinite(List<ComparisonMetric> metrics)
        {
            if (metrics == null) return false;
            for (int i = 0; i < metrics.Count; i++)
            {
                ComparisonMetric metric = metrics[i];
                if (metric == null || !IsFinite(metric.current) || !IsFinite(metric.baseline)
                    || !IsFinite(metric.delta) || !IsFinite(metric.relativeDelta)) return false;
            }
            return true;
        }

        static string Fingerprint(IList<RagdollTuningParameterValue> values)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append('|');
                builder.Append(values[i].name).Append('=').Append(values[i].value.ToString("R", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(parameterName + " is required", parameterName);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
