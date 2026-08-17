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
    public sealed class RagdollTuningParameterDescriptor
    {
        public string name;
        public string owner;
        public string units;
        public float minimum;
        public float maximum = 1f;
        public float safeDelta = 1f;
        public float step = 0.01f;
        public string scale = "linear";
        public string[] scenarios = Array.Empty<string>();
        public bool runtimeWritable = true;
        public bool requiresRestart;

        public bool AllowsScenario(string scenarioProfile)
        {
            if (scenarios == null || scenarios.Length == 0) return true;
            for (int i = 0; i < scenarios.Length; i++)
                if (string.Equals(scenarios[i], scenarioProfile, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class RagdollTuningParameterRegistry
    {
        public List<RagdollTuningParameterDescriptor> parameters = new();

        public RagdollTuningParameterRegistry() { }

        public RagdollTuningParameterRegistry(IList<RagdollTuningParameterDescriptor> descriptors)
        {
            if (descriptors != null)
                for (int i = 0; i < descriptors.Count; i++) parameters.Add(descriptors[i]);
        }

        public RagdollTuningParameterDescriptor Find(string name)
        {
            if (parameters == null) return null;
            for (int i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && string.Equals(parameters[i].name, name, StringComparison.Ordinal)) return parameters[i];
            return null;
        }

        public string ValidateBaseline(string scenarioProfile, string name, float value)
        {
            RagdollTuningParameterDescriptor descriptor = Find(name);
            if (descriptor == null) return "parameter_not_registered";
            if (!IsFinite(value)) return "baseline_value_non_finite";
            if (!DescriptorIsValid(descriptor)) return "parameter_descriptor_invalid";
            if (!descriptor.AllowsScenario(scenarioProfile)) return "parameter_scenario_not_allowed";
            if (value < descriptor.minimum || value > descriptor.maximum) return "baseline_value_out_of_range";
            if (!OnStep(descriptor, value)) return "baseline_value_off_step";
            return null;
        }

        public string ValidateCandidate(string scenarioProfile, string name, float baselineValue, float candidateValue)
        {
            RagdollTuningParameterDescriptor descriptor = Find(name);
            if (descriptor == null) return "parameter_not_registered";
            if (!descriptor.runtimeWritable) return "parameter_not_runtime_writable";
            if (descriptor.requiresRestart) return "parameter_requires_restart";
            if (!IsFinite(baselineValue) || !IsFinite(candidateValue)) return "candidate_value_non_finite";
            if (!DescriptorIsValid(descriptor)) return "parameter_descriptor_invalid";
            if (!descriptor.AllowsScenario(scenarioProfile)) return "parameter_scenario_not_allowed";
            if (candidateValue < descriptor.minimum || candidateValue > descriptor.maximum) return "candidate_value_out_of_range";
            if (!OnStep(descriptor, candidateValue)) return "candidate_value_off_step";
            if (Mathf.Abs(candidateValue - baselineValue) > descriptor.safeDelta + 0.000001f)
                return "candidate_delta_exceeds_safe_limit";
            return null;
        }

        static bool DescriptorIsValid(RagdollTuningParameterDescriptor descriptor)
        {
            return !string.IsNullOrWhiteSpace(descriptor.name)
                && IsFinite(descriptor.minimum) && IsFinite(descriptor.maximum)
                && IsFinite(descriptor.safeDelta) && IsFinite(descriptor.step)
                && descriptor.minimum <= descriptor.maximum
                && descriptor.safeDelta > 0f && descriptor.step > 0f;
        }

        static bool OnStep(RagdollTuningParameterDescriptor descriptor, float value)
        {
            float index = (value - descriptor.minimum) / descriptor.step;
            return Mathf.Abs(index - Mathf.Round(index)) <= 0.0001f;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IRagdollTuningParameterStore
    {
        bool TryRead(string name, out float value);
        bool TryWrite(string name, float value);
    }

    public interface IRagdollTuningScenarioRunner
    {
        EvaluationReport Run(RagdollTuningRunBinding binding);
    }

    [Serializable]
    public sealed class RagdollTuningExecutionResult
    {
        public bool valid;
        public bool persistedPair;
        public bool restored;
        public bool promoted;
        public string reason = "unavailable";
        public RagdollTuningDecision decision;
        public RagdollTuningDecision promotionDecision;
        public EvaluationReport baselineReport;
        public EvaluationReport candidateReport;
        public RagdollTuningArtifactManifest baselineArtifact;
        public RagdollTuningArtifactManifest candidateArtifact;
        public BalanceComparisonReport comparison;
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
        public string tuningSessionId;
        public string experimentId;
        public string parameterName;
        public float baselineValue;
        public float candidateValue;
        public string baselineRunId;
        public string candidateRunId;
        public string baselineConfigurationFingerprint;
        public string candidateConfigurationFingerprint;
        public string scenarioProfile;
        public bool promoted;
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
        public string schemaVersion = RagdollTuningArtifactSchema.SessionVersion;
        public string sessionId;
        public string scenarioProfile;
        public string artifactRoot;
        public string baselineFingerprint;
        public string baselineRunId;
        public int maxExperiments;
        public int startedExperiments;
        public bool candidateActive;
        public string activeExperimentId;
        public string lastDecision = "unavailable";
        public string lastReason = "unavailable";
        public List<RagdollTuningParameterValue> baseline = new();
        public List<RagdollTuningExperiment> experiments = new();
        public RagdollTuningParameterRegistry parameterRegistry;
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
            int maxExperiments,
            RagdollTuningParameterRegistry parameterRegistry = null,
            string artifactRoot = null)
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
                if (parameterRegistry != null)
                {
                    string registryReason = parameterRegistry.ValidateBaseline(scenarioProfile, value.name, value.value);
                    if (registryReason != null)
                        throw new ArgumentException(registryReason + ": " + value.name, "baseline");
                }
                copied.Add(new RagdollTuningParameterValue(value.name, value.value));
            }

            copied.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return new RagdollTuningSession
            {
                sessionId = sessionId,
                scenarioProfile = scenarioProfile,
                artifactRoot = artifactRoot,
                baseline = copied,
                baselineFingerprint = Fingerprint(copied),
                maxExperiments = maxExperiments,
                parameterRegistry = parameterRegistry,
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

            if (session.parameterRegistry != null)
            {
                string registryReason = session.parameterRegistry.ValidateCandidate(
                    session.scenarioProfile, changed.name, Find(session.baseline, changed.name).value, changed.value);
                if (registryReason != null) return Invalid(session, registryReason, experimentId);
            }

            candidateByName.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            var experiment = new RagdollTuningExperiment
            {
                tuningSessionId = session.sessionId,
                experimentId = experimentId,
                parameterName = changed.name,
                baselineValue = Find(session.baseline, changed.name).value,
                candidateValue = changed.value,
                baselineRunId = baselineRunId,
                candidateRunId = candidateRunId,
                baselineConfigurationFingerprint = session.baselineFingerprint,
                candidateConfigurationFingerprint = Fingerprint(candidateByName),
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

        public static RagdollTuningDecision PromoteAcceptedCandidate(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment)
        {
            if (session == null || experiment == null)
                return Invalid(session, "experiment_missing", experiment?.experimentId);
            if (!Contains(session, experiment))
                return Invalid(session, "experiment_not_in_session", experiment.experimentId);
            if (string.Equals(experiment.state, "promoted", StringComparison.Ordinal))
                return ExistingDecision(session, experiment);
            if (!string.Equals(experiment.state, "accepted", StringComparison.Ordinal)
                || !experiment.promotionEligible
                || !session.candidateActive
                || !string.Equals(session.activeExperimentId, experiment.experimentId, StringComparison.Ordinal))
                return Invalid(session, "candidate_not_promotion_eligible", experiment.experimentId);

            RagdollTuningParameterValue baseline = Find(session.baseline, experiment.parameterName);
            if (baseline == null)
                return Invalid(session, "baseline_parameter_missing", experiment.experimentId);
            baseline.value = experiment.candidateValue;
            session.baseline.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            session.baselineFingerprint = Fingerprint(session.baseline);
            session.baselineRunId = experiment.candidateRunId;
            experiment.state = "promoted";
            experiment.decision = "promoted";
            experiment.decisionStage = "promotion";
            experiment.decisionReason = "accepted_candidate_promoted";
            experiment.promotionEligible = false;
            experiment.rollbackRequired = false;
            experiment.candidateActive = false;
            experiment.promoted = true;
            session.candidateActive = false;
            session.activeExperimentId = null;
            session.lastDecision = experiment.decision;
            session.lastReason = experiment.decisionReason;
            return Decision(experiment, experiment.decision, experiment.decisionStage, experiment.decisionReason, true, false, false);
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
            if (string.Equals(experiment.state, "promoted", StringComparison.Ordinal))
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
                && comparison.provenanceAvailable
                && string.Equals(comparison.tuningSessionId, experiment.tuningSessionId, StringComparison.Ordinal)
                && string.Equals(comparison.experimentId, experiment.experimentId, StringComparison.Ordinal)
                && string.Equals(comparison.baselineRunId, experiment.baselineRunId, StringComparison.Ordinal)
                && string.Equals(comparison.candidateRunId, experiment.candidateRunId, StringComparison.Ordinal)
                && string.Equals(comparison.baselineConfigurationFingerprint, experiment.baselineConfigurationFingerprint, StringComparison.Ordinal)
                && string.Equals(comparison.candidateConfigurationFingerprint, experiment.candidateConfigurationFingerprint, StringComparison.Ordinal)
                && string.Equals(comparison.treatmentParameter, experiment.parameterName, StringComparison.Ordinal)
                && comparison.treatmentValueAvailable
                && Approximately(comparison.treatmentValue, experiment.candidateValue)
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
            if (!comparison.provenanceAvailable) return "comparison_provenance_missing";
            if (!string.Equals(comparison.tuningSessionId, experiment.tuningSessionId, StringComparison.Ordinal)) return "tuning_session_id_mismatch";
            if (!string.Equals(comparison.experimentId, experiment.experimentId, StringComparison.Ordinal)) return "experiment_id_mismatch";
            if (!string.Equals(comparison.baselineRunId, experiment.baselineRunId, StringComparison.Ordinal)) return "baseline_run_id_mismatch";
            if (!string.Equals(comparison.candidateRunId, experiment.candidateRunId, StringComparison.Ordinal)) return "candidate_run_id_mismatch";
            if (!string.Equals(comparison.baselineConfigurationFingerprint, experiment.baselineConfigurationFingerprint, StringComparison.Ordinal)) return "baseline_configuration_fingerprint_mismatch";
            if (!string.Equals(comparison.candidateConfigurationFingerprint, experiment.candidateConfigurationFingerprint, StringComparison.Ordinal)) return "candidate_configuration_fingerprint_mismatch";
            if (!string.Equals(comparison.treatmentParameter, experiment.parameterName, StringComparison.Ordinal)) return "treatment_parameter_mismatch";
            if (!comparison.treatmentValueAvailable || !Approximately(comparison.treatmentValue, experiment.candidateValue)) return "treatment_value_mismatch";
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

        static bool Approximately(float left, float right)
        {
            return IsFinite(left) && IsFinite(right) && Mathf.Abs(left - right) <= 0.000001f;
        }

        public static string ConfigurationFingerprint(IList<RagdollTuningParameterValue> values)
        {
            if (values == null) return string.Empty;
            var sorted = CopyParameters(values);
            sorted.Sort((left, right) => string.CompareOrdinal(left?.name, right?.name));
            var builder = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) builder.Append('|');
                builder.Append(sorted[i]?.name).Append('=').Append(sorted[i]?.value.ToString("R", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        static string Fingerprint(IList<RagdollTuningParameterValue> values)
        {
            return ConfigurationFingerprint(values);
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

    /// <summary>
    /// Applies one registered candidate through injected adapters, then restores
    /// or explicitly promotes it. It owns orchestration, not project settings.
    /// </summary>
    public static class RagdollTuningExecutor
    {
        /// <summary>
        /// Evaluates an already completed baseline/candidate pair through the
        /// same artifact transport and planner used by live execution. This is
        /// the asynchronous Unity hand-off: it does not apply parameters,
        /// start PlayMode or promote a candidate.
        /// </summary>
        public static RagdollTuningExecutionResult EvaluatePersistedPair(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            IRagdollTuningArtifactTransport artifactTransport,
            RagdollLabThresholds thresholds = null)
        {
            var result = new RagdollTuningExecutionResult { persistedPair = true };
            if (session == null || experiment == null)
                return Failure(result, "experiment_missing");
            if (session.experiments == null || !session.experiments.Contains(experiment))
                return Failure(result, "experiment_not_in_session");
            if (!string.Equals(experiment.state, "active", StringComparison.Ordinal))
                return Failure(result, "experiment_not_active");
            if (artifactTransport == null)
                return FailPersistedPair(result, session, experiment, "artifact_transport_missing");

            RagdollTuningRunBinding baselineBinding = Binding(session, experiment, "baseline");
            if (!TryReadArtifact(artifactTransport, baselineBinding, null,
                out result.baselineReport, out result.baselineArtifact, out string reason))
                return FailPersistedPair(result, session, experiment, reason);

            RagdollTuningRunBinding candidateBinding = Binding(session, experiment, "candidate");
            if (!TryReadArtifact(artifactTransport, candidateBinding, null,
                out result.candidateReport, out result.candidateArtifact, out reason))
                return FailPersistedPair(result, session, experiment, reason);

            result.decision = RagdollTuningPlanner.Evaluate(
                session, experiment, result.baselineReport, result.candidateReport, thresholds);
            result.comparison = experiment.comparison;
            result.valid = result.decision != null && result.decision.valid;
            result.reason = result.decision?.reason ?? "decision_missing";
            return result;
        }

        public static RagdollTuningExecutionResult Execute(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            IRagdollTuningParameterStore parameterStore,
            IRagdollTuningScenarioRunner scenarioRunner,
            bool promoteAcceptedCandidate = true,
            RagdollLabThresholds thresholds = null,
            IRagdollTuningArtifactTransport artifactTransport = null)
        {
            var result = new RagdollTuningExecutionResult();
            if (session == null || experiment == null)
                return Failure(result, "experiment_missing");
            if (session.experiments == null || !session.experiments.Contains(experiment))
                return Failure(result, "experiment_not_in_session");
            if (!string.Equals(experiment.state, "active", StringComparison.Ordinal))
                return Failure(result, "experiment_not_active");
            if (parameterStore == null) return FailAndRollback(result, session, experiment, "parameter_store_missing");
            if (scenarioRunner == null) return FailAndRollback(result, session, experiment, "scenario_runner_missing");
            if (session.parameterRegistry == null) return FailAndRollback(result, session, experiment, "parameter_registry_missing");

            string registryReason = session.parameterRegistry.ValidateCandidate(
                session.scenarioProfile, experiment.parameterName, experiment.baselineValue, experiment.candidateValue);
            if (registryReason != null) return FailAndRollback(result, session, experiment, registryReason);

            string baselineStateReason = VerifyBaselineState(session, parameterStore);
            if (baselineStateReason != null) return FailAndRollback(result, session, experiment, baselineStateReason);

            bool writeAttempted = false;
            try
            {
                RagdollTuningRunBinding baselineBinding = Binding(session, experiment, "baseline");
                result.baselineReport = scenarioRunner.Run(baselineBinding);
                string reportReason = MetadataMismatch(result.baselineReport?.metadata, baselineBinding);
                if (reportReason != null) return FailAndRollback(result, session, experiment, reportReason);
                if (!TryReadArtifact(artifactTransport, baselineBinding, result.baselineReport,
                    out result.baselineReport, out result.baselineArtifact, out reportReason))
                    return FailAndRollback(result, session, experiment, reportReason);

                writeAttempted = true;
                if (!parameterStore.TryWrite(experiment.parameterName, experiment.candidateValue))
                    return FailAndRestore(result, session, experiment, parameterStore, writeAttempted, "candidate_apply_failed");
                if (!parameterStore.TryRead(experiment.parameterName, out float appliedValue)
                    || !Approximately(appliedValue, experiment.candidateValue))
                    return FailAndRestore(result, session, experiment, parameterStore, writeAttempted, "candidate_readback_mismatch");

                RagdollTuningRunBinding candidateBinding = Binding(session, experiment, "candidate");
                result.candidateReport = scenarioRunner.Run(candidateBinding);
                reportReason = MetadataMismatch(result.candidateReport?.metadata, candidateBinding);
                if (reportReason != null)
                    return FailAndRestore(result, session, experiment, parameterStore, writeAttempted, reportReason);
                if (!TryReadArtifact(artifactTransport, candidateBinding, result.candidateReport,
                    out result.candidateReport, out result.candidateArtifact, out reportReason))
                    return FailAndRestore(result, session, experiment, parameterStore, writeAttempted, reportReason);

                result.decision = RagdollTuningPlanner.Evaluate(
                    session, experiment, result.baselineReport, result.candidateReport, thresholds);
                result.comparison = experiment.comparison;
                if (string.Equals(result.decision.decision, "accepted", StringComparison.Ordinal)
                    && promoteAcceptedCandidate)
                {
                    result.promotionDecision = RagdollTuningPlanner.PromoteAcceptedCandidate(session, experiment);
                    if (!string.Equals(result.promotionDecision.decision, "promoted", StringComparison.Ordinal))
                        return FailAndRestore(result, session, experiment, parameterStore, writeAttempted, "promotion_failed");
                    result.valid = true;
                    result.promoted = true;
                    result.reason = "accepted_candidate_promoted";
                    return result;
                }

                bool restored = Restore(parameterStore, experiment.parameterName, experiment.baselineValue);
                result.restored = restored;
                result.valid = result.decision != null && !string.Equals(result.decision.decision, "invalid", StringComparison.Ordinal);
                result.reason = result.decision?.reason ?? "decision_missing";
                if (!restored)
                {
                    result.valid = false;
                    result.reason = "restore_failed";
                }
                return result;
            }
            catch (Exception exception)
            {
                return FailAndRestore(result, session, experiment, parameterStore, writeAttempted,
                    "scenario_execution_failed:" + exception.GetType().Name);
            }
        }

        static RagdollTuningExecutionResult FailAndRollback(
            RagdollTuningExecutionResult result,
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            string reason)
        {
            result.decision = RagdollTuningPlanner.Rollback(session, experiment, reason);
            result.valid = false;
            result.reason = reason;
            return result;
        }

        static RagdollTuningExecutionResult FailAndRestore(
            RagdollTuningExecutionResult result,
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            IRagdollTuningParameterStore parameterStore,
            bool restoreRequired,
            string reason)
        {
            result.decision = RagdollTuningPlanner.Rollback(session, experiment, reason);
            result.valid = false;
            result.reason = reason;
            if (restoreRequired)
            {
                result.restored = Restore(parameterStore, experiment.parameterName, experiment.baselineValue);
                if (!result.restored) result.reason = "restore_failed";
            }
            return result;
        }

        static RagdollTuningExecutionResult Failure(RagdollTuningExecutionResult result, string reason)
        {
            result.valid = false;
            result.reason = reason;
            return result;
        }

        static RagdollTuningExecutionResult FailPersistedPair(
            RagdollTuningExecutionResult result,
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            string reason)
        {
            result.decision = RagdollTuningPlanner.Rollback(session, experiment,
                string.IsNullOrWhiteSpace(reason) ? "persisted_pair_invalid" : reason);
            result.valid = false;
            result.reason = reason;
            return result;
        }

        static string VerifyBaselineState(RagdollTuningSession session, IRagdollTuningParameterStore parameterStore)
        {
            if (session.baseline == null) return "baseline_missing";
            for (int i = 0; i < session.baseline.Count; i++)
            {
                RagdollTuningParameterValue expected = session.baseline[i];
                if (expected == null || !parameterStore.TryRead(expected.name, out float actual))
                    return "baseline_parameter_unavailable";
                if (!Approximately(actual, expected.value)) return "baseline_configuration_mismatch";
            }
            return null;
        }

        static RagdollTuningRunBinding Binding(
            RagdollTuningSession session,
            RagdollTuningExperiment experiment,
            string role)
        {
            bool candidate = string.Equals(role, "candidate", StringComparison.Ordinal);
            return new RagdollTuningRunBinding
            {
                sessionId = session.sessionId,
                experimentId = experiment.experimentId,
                runId = candidate ? experiment.candidateRunId : experiment.baselineRunId,
                runRole = role,
                configurationFingerprint = candidate
                    ? experiment.candidateConfigurationFingerprint
                    : experiment.baselineConfigurationFingerprint,
                artifactDirectory = RagdollTuningFileArtifactTransport.RunDirectory(
                    session.artifactRoot, candidate ? experiment.candidateRunId : experiment.baselineRunId),
                baselineConfigurationFingerprint = experiment.baselineConfigurationFingerprint,
                treatmentParameter = experiment.parameterName,
                treatmentValueAvailable = true,
                treatmentValue = candidate ? experiment.candidateValue : experiment.baselineValue
            };
        }

        static string MetadataMismatch(RagdollLabMetadata metadata, RagdollTuningRunBinding binding)
        {
            if (metadata == null) return "report_metadata_missing";
            if (!string.Equals(metadata.tuningSessionId, binding.sessionId, StringComparison.Ordinal)) return "tuning_session_id_mismatch";
            if (!string.Equals(metadata.experimentId, binding.experimentId, StringComparison.Ordinal)) return "experiment_id_mismatch";
            if (!string.Equals(metadata.runId, binding.runId, StringComparison.Ordinal)) return "run_id_mismatch";
            if (!string.Equals(metadata.runRole, binding.runRole, StringComparison.Ordinal)) return "run_role_mismatch";
            if (!string.Equals(metadata.configurationFingerprint, binding.configurationFingerprint, StringComparison.Ordinal))
                return binding.runRole == "candidate" ? "candidate_configuration_fingerprint_mismatch" : "baseline_configuration_fingerprint_mismatch";
            if (!string.Equals(metadata.baselineConfigurationFingerprint, binding.baselineConfigurationFingerprint, StringComparison.Ordinal))
                return "baseline_configuration_fingerprint_mismatch";
            if (!string.Equals(metadata.treatmentParameter, binding.treatmentParameter, StringComparison.Ordinal)) return "treatment_parameter_mismatch";
            if (!metadata.treatmentValueAvailable || !Approximately(metadata.treatmentValue, binding.treatmentValue)) return "treatment_value_mismatch";
            return null;
        }

        static bool Restore(IRagdollTuningParameterStore parameterStore, string name, float value)
        {
            if (parameterStore == null || !parameterStore.TryWrite(name, value)) return false;
            return parameterStore.TryRead(name, out float restored) && Approximately(restored, value);
        }

        static bool Approximately(float left, float right)
        {
            return !float.IsNaN(left) && !float.IsInfinity(left)
                && !float.IsNaN(right) && !float.IsInfinity(right)
                && Mathf.Abs(left - right) <= 0.000001f;
        }

        static bool TryReadArtifact(
            IRagdollTuningArtifactTransport transport,
            RagdollTuningRunBinding binding,
            EvaluationReport runnerReport,
            out EvaluationReport report,
            out RagdollTuningArtifactManifest manifest,
            out string reason)
        {
            report = runnerReport;
            manifest = null;
            reason = null;
            if (transport == null) return true;
            if (string.IsNullOrWhiteSpace(binding?.artifactDirectory))
            {
                reason = "artifact_directory_missing";
                return false;
            }
            if (!transport.TryRead(binding.artifactDirectory, binding, out report, out manifest, out reason))
                return false;
            if (report == null)
            {
                reason = "artifact_report_missing";
                return false;
            }
            string metadataReason = MetadataMismatch(report.metadata, binding);
            if (metadataReason != null)
            {
                reason = metadataReason;
                report = null;
                manifest = null;
                return false;
            }
            return true;
        }
    }
}
