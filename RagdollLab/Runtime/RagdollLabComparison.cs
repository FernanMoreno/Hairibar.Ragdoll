using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollLabComparison
    {
        public static ComparisonReport Build(EvaluationReport current, EvaluationReport baseline)
        {
            ScenarioProfile profile = RagdollLabScenarioProfiles.Resolve(current?.metadata?.scenario ?? current?.scenarioReport?.name);
            var result = new ComparisonReport { currentRunId = current?.metadata?.runId, baselineRunId = baseline?.metadata?.runId, baselineFound = baseline != null,
                tuningSessionId = current?.metadata?.tuningSessionId, experimentId = current?.metadata?.experimentId,
                configurationFingerprint = current?.metadata?.configurationFingerprint,
                baselineConfigurationFingerprint = current?.metadata?.baselineConfigurationFingerprint,
                treatmentParameter = current?.metadata?.treatmentParameter,
                treatmentValueAvailable = current?.metadata?.treatmentValueAvailable ?? false,
                treatmentValue = current?.metadata?.treatmentValue ?? 0f,
                scenarioProfile = profile.id, profileAvailable = profile.available };
            if (!profile.available)
            {
                result.decision = "invalid";
                result.invalidReason = "scenario_profile_unavailable";
                result.rejectionReasons.Add(result.invalidReason);
                return result;
            }
            if (current?.scenarioReport == null || baseline?.scenarioReport == null)
            {
                result.decision = "invalid";
                result.invalidReason = "current_or_baseline_report_missing";
                result.rejectionReasons.Add(result.invalidReason);
                return result;
            }
            List<string> legacyMissingSignals = RagdollLabScenarioSignalCatalog.MissingRequiredSignals(profile, current.scenarioReport, "current");
            legacyMissingSignals.AddRange(RagdollLabScenarioSignalCatalog.MissingRequiredSignals(profile, baseline.scenarioReport, "baseline"));
            if (legacyMissingSignals.Count > 0)
            {
                result.decision = "invalid";
                result.invalidReason = "required_signals_missing";
                result.rejectionReasons.Add(result.invalidReason);
                result.rejectionReasons.AddRange(legacyMissingSignals);
                return result;
            }
            Add(result, "KineticEnergy.mean", "J", current.scenarioReport.kineticEnergy.mean, baseline.scenarioReport.kineticEnergy.mean, false);
            Add(result, "CenterOfMassSpeed.mean", "m/s", current.scenarioReport.centerOfMassSpeed.mean, baseline.scenarioReport.centerOfMassSpeed.mean, true);
            if (current.scenarioReport.contactImpulse != null && baseline.scenarioReport.contactImpulse != null
                && RagdollLabMath.IsFinite(current.scenarioReport.contactImpulse.p95)
                && RagdollLabMath.IsFinite(baseline.scenarioReport.contactImpulse.p95))
                Add(result, "ContactImpulse.p95", "N*s", current.scenarioReport.contactImpulse.p95, baseline.scenarioReport.contactImpulse.p95, true);
            Add(result, "PenetrationDepth.max", "m", current.scenarioReport.penetration.max, baseline.scenarioReport.penetration.max, true);
            Add(result, "FootSlipSpeed.mean", "m/s", current.scenarioReport.footSlipSpeed.mean, baseline.scenarioReport.footSlipSpeed.mean, true);
            for (int i = 0; i < result.metrics.Count; i++)
            {
                ComparisonMetric metric = result.metrics[i];
                metric.expectation = profile.ExpectationFor(metric.name);
                metric.regression = metric.expectation == "lower" ? metric.delta > 0f : metric.expectation == "higher" && metric.delta < 0f;
            }
            if (current.scenarioReport.penetration.max > 0f)
                AddGlobalGuard(result, "penetration present");
            if (current.scenarioReport.footSlipSpeed.mean > 0.15f)
                AddGlobalGuard(result, "foot slip above default warning");
            bool hasRegression = result.metrics.Exists(metric => metric.regression);
            result.decision = result.regressionGuards.Count > 0 || hasRegression ? "reject" : "accept";
            return result;
        }

        static void AddGlobalGuard(ComparisonReport result, string reason)
        {
            result.regressionGuards.Add(reason);
            result.rejectionReasons.Add(reason);
        }

        /// <summary>
        /// Compares two runs of the same scenario with safety taking precedence
        /// over stability. This is intentionally separate from the historical
        /// global comparison above: COM speed or energy do not have one universal
        /// direction across Idle, Locomotion, GetUp, and a recoverable push.
        /// </summary>
        public static BalanceComparisonReport BuildBalanceComparison(
            EvaluationReport baseline,
            EvaluationReport candidate,
            RagdollLabThresholds thresholds = null)
        {
            thresholds ??= ScriptableObject.CreateInstance<RagdollLabThresholds>();
            var result = new BalanceComparisonReport
            {
                baselineRunId = baseline?.metadata?.runId,
                candidateRunId = candidate?.metadata?.runId
            };
            if (baseline == null || candidate == null)
                return Invalid(result, "baseline_or_candidate_missing");
            if (baseline.metadata == null || candidate.metadata == null)
                return Invalid(result, "metadata_missing");
            if (!PopulateProvenance(result, baseline.metadata, candidate.metadata))
                return Invalid(result, "provenance_mismatch");
            ScenarioProfile baselineProfile = RagdollLabScenarioProfiles.Resolve(baseline.metadata.scenario);
            ScenarioProfile candidateProfile = RagdollLabScenarioProfiles.Resolve(candidate.metadata.scenario);
            if (!baselineProfile.available || !candidateProfile.available)
                return Invalid(result, "scenario_profile_unavailable");
            if (!string.Equals(baselineProfile.id, candidateProfile.id, System.StringComparison.Ordinal))
                return Invalid(result, "scenario_profile_mismatch");
            result.scenarioProfile = candidateProfile.id;
            result.profileAvailable = true;
            if (!SetupsMatch(baseline.metadata, candidate.metadata))
                return Invalid(result, "paired_setup_mismatch");
            if (!baseline.completed || !candidate.completed)
                return Invalid(result, "run_incomplete");
            if (!baseline.finiteData || !candidate.finiteData)
                return Invalid(result, "non_finite_run");
            if (baseline.scenarioReport == null || candidate.scenarioReport == null)
                return Invalid(result, "scenario_report_missing");
            List<string> missingSignals = RagdollLabScenarioSignalCatalog.MissingRequiredSignals(
                candidateProfile, baseline.scenarioReport, "baseline");
            missingSignals.AddRange(RagdollLabScenarioSignalCatalog.MissingRequiredSignals(
                candidateProfile, candidate.scenarioReport, "candidate"));
            if (missingSignals.Count > 0)
                return Invalid(result, "required_signals_missing", missingSignals);
            if (!Approximately(baseline.scenarioReport.durationSeconds, candidate.scenarioReport.durationSeconds, 0.0001f))
                return Invalid(result, "duration_mismatch");

            result.setupMatched = true;
            ScenarioReport before = baseline.scenarioReport;
            ScenarioReport after = candidate.scenarioReport;
            if (RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.SignedSupportMargin, before)
                && RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.SignedSupportMargin, after))
                Add(result.stabilityMetrics, "SignedSupportMargin.minimum", "m",
                    after.minimumSignedSupportMargin, before.minimumSignedSupportMargin, false);
            if (RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.CenterOfMassSpeed, before)
                && RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.CenterOfMassSpeed, after))
                Add(result.stabilityMetrics, "CenterOfMassSpeed.mean", "m/s",
                    after.centerOfMassSpeed.mean, before.centerOfMassSpeed.mean, true);
            if (RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.RecoveryTime, before)
                && RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.RecoveryTime, after))
                Add(result.stabilityMetrics, "RecoveryTime.seconds", "s",
                    after.recoveryTimeSeconds, before.recoveryTimeSeconds, true);
            if (RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.FallenFrames, before)
                && RagdollLabScenarioSignalCatalog.IsAvailable(RagdollLabScenarioSignalIds.FallenFrames, after))
                Add(result.stabilityMetrics, "FallenFrameCount.count", "frames",
                    after.fallenFrameCount, before.fallenFrameCount, true);
            ApplyScenarioExpectations(result.stabilityMetrics, candidateProfile, thresholds);

            Add(result.safetyMetrics, "PenetrationDepth.max", "m",
                after.penetration.max, before.penetration.max, true,
                thresholds.comparisonSafetyToleranceRatio, thresholds.comparisonSafetyToleranceAbsolute);
            Add(result.safetyMetrics, "FootSlipSpeed.mean", "m/s",
                after.footSlipSpeed.mean, before.footSlipSpeed.mean, true,
                thresholds.comparisonSafetyToleranceRatio, thresholds.comparisonSafetyToleranceAbsolute);
            Add(result.safetyMetrics, "KineticEnergy.max", "J",
                after.kineticEnergy.max, before.kineticEnergy.max, true,
                thresholds.comparisonSafetyToleranceRatio, thresholds.comparisonSafetyToleranceAbsolute);
            Add(result.safetyMetrics, "BalancerTorque.max", "N*m",
                after.balancerTorque?.max ?? 0f, before.balancerTorque?.max ?? 0f, true,
                thresholds.comparisonSafetyToleranceRatio, thresholds.comparisonSafetyToleranceAbsolute);
            Add(result.safetyMetrics, "UnrecoverableFrameCount", "frames",
                after.unrecoverableFrameCount, before.unrecoverableFrameCount, true, 0f, 0f);
            Add(result.safetyMetrics, "UnpinnedStaggerEpisodeCount", "episodes",
                after.unpinnedStaggerEpisodeCount, before.unpinnedStaggerEpisodeCount, true, 0f, 0f);
            Add(result.safetyMetrics, "StaggerEpisodeCount", "episodes",
                after.staggerEpisodes?.Length ?? 0, before.staggerEpisodes?.Length ?? 0, true, 0f, 0f);

            AddSafetyGuards(result, baseline, candidate, thresholds);
            result.safetyGuardsPassed = result.safetyGuards.Count == 0;
            if (!result.safetyGuardsPassed)
            {
                result.decision = "reject";
                result.invalidReason = "safety_guard_failed";
                return result;
            }

            int improvements = 0, regressions = 0;
            for (int i = 0; i < result.stabilityMetrics.Count; i++)
            {
                ComparisonMetric metric = result.stabilityMetrics[i];
                if (string.Equals(metric.expectation, "neutral", System.StringComparison.Ordinal)) continue;
                float tolerance = Mathf.Max(
                    thresholds.comparisonImprovementToleranceRatio * Mathf.Abs(metric.baseline),
                    thresholds.comparisonSafetyToleranceAbsolute);
                if (Mathf.Abs(metric.delta) <= tolerance) continue;
                if (metric.regression) regressions++; else improvements++;
            }
            result.decision = improvements > regressions
                ? "accept"
                : regressions > 0 ? "reject" : "neutral";
            if (string.Equals(candidateProfile.id, "Balancer", System.StringComparison.Ordinal)
                && improvements == 0 && regressions == 0)
                result.rejectionReasons.Add("balancer_no_stability_improvement");
            return result;
        }

        static void ApplyScenarioExpectations(List<ComparisonMetric> metrics, ScenarioProfile profile, RagdollLabThresholds thresholds)
        {
            if (metrics == null || profile == null) return;
            for (int i = 0; i < metrics.Count; i++)
            {
                ComparisonMetric metric = metrics[i];
                metric.expectation = profile.ExpectationFor(metric.name);
                float tolerance = Mathf.Max(
                    thresholds.comparisonImprovementToleranceRatio * Mathf.Abs(metric.baseline),
                    thresholds.comparisonSafetyToleranceAbsolute);
                metric.regression = metric.expectation == "lower"
                    ? metric.delta > tolerance
                    : metric.expectation == "higher" && metric.delta < -tolerance;
            }
        }

        static BalanceComparisonReport Invalid(BalanceComparisonReport result, string reason, List<string> details = null)
        {
            result.decision = "invalid";
            result.invalidReason = reason;
            result.setupMatched = false;
            result.safetyGuardsPassed = false;
            if (result.rejectionReasons == null) result.rejectionReasons = new List<string>();
            result.rejectionReasons.Add(reason);
            if (details != null) result.rejectionReasons.AddRange(details);
            return result;
        }

        static bool SetupsMatch(RagdollLabMetadata baseline, RagdollLabMetadata candidate)
        {
            string mismatch = SetupMismatchReason(baseline, candidate);
            if (mismatch == null) return true;
            Debug.LogWarning("[RagdollLab] paired setup mismatch: " + mismatch);
            return false;
        }

        static string SetupMismatchReason(RagdollLabMetadata baseline, RagdollLabMetadata candidate)
        {
            if (!string.Equals(baseline.captureRoot, candidate.captureRoot, System.StringComparison.Ordinal)) return "captureRoot";
            if (baseline.seed != candidate.seed) return "seed";
            if (!OptionalTextEqual(baseline.pushDescriptor, candidate.pushDescriptor)) return "pushDescriptor";
            if (!OptionalTextEqual(baseline.initialConditionFingerprint, candidate.initialConditionFingerprint)) return "initialConditionFingerprint";
            if (!Approximately(baseline.fixedDeltaTime, candidate.fixedDeltaTime, 0.000001f)) return "fixedDeltaTime";
            if (!Approximately(baseline.gravityMagnitude, candidate.gravityMagnitude, 0.0001f)) return "gravityMagnitude";
            if (!Approximately(baseline.characterHeight, candidate.characterHeight, 0.0001f)) return "characterHeight";
            if (!Approximately(baseline.totalMass, candidate.totalMass, 0.0001f)) return "totalMass";
            return null;
        }

        static bool OptionalTextEqual(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, System.StringComparison.Ordinal);
        }

        static bool PopulateProvenance(
            BalanceComparisonReport result,
            RagdollLabMetadata baseline,
            RagdollLabMetadata candidate)
        {
            bool baselineHasProvenance = HasProvenance(baseline);
            bool candidateHasProvenance = HasProvenance(candidate);
            if (!baselineHasProvenance && !candidateHasProvenance)
                return true;
            if (!baselineHasProvenance || !candidateHasProvenance)
                return false;
            if (string.IsNullOrWhiteSpace(baseline.tuningSessionId)
                || !string.Equals(baseline.tuningSessionId, candidate.tuningSessionId, System.StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(baseline.experimentId)
                || !string.Equals(baseline.experimentId, candidate.experimentId, System.StringComparison.Ordinal)) return false;
            if (!string.Equals(baseline.runRole, "baseline", System.StringComparison.Ordinal)
                || !string.Equals(candidate.runRole, "candidate", System.StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(baseline.runId) || string.IsNullOrWhiteSpace(candidate.runId)) return false;
            if (string.IsNullOrWhiteSpace(baseline.configurationFingerprint)
                || string.IsNullOrWhiteSpace(candidate.configurationFingerprint)) return false;
            if (!string.Equals(baseline.baselineConfigurationFingerprint, baseline.configurationFingerprint, System.StringComparison.Ordinal)) return false;
            if (!string.Equals(candidate.baselineConfigurationFingerprint, baseline.configurationFingerprint, System.StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(baseline.treatmentParameter)
                || !string.Equals(baseline.treatmentParameter, candidate.treatmentParameter, System.StringComparison.Ordinal)) return false;
            if (!baseline.treatmentValueAvailable || !candidate.treatmentValueAvailable
                || !RagdollLabMath.IsFinite(baseline.treatmentValue) || !RagdollLabMath.IsFinite(candidate.treatmentValue)) return false;

            result.tuningSessionId = candidate.tuningSessionId;
            result.experimentId = candidate.experimentId;
            result.baselineConfigurationFingerprint = baseline.configurationFingerprint;
            result.candidateConfigurationFingerprint = candidate.configurationFingerprint;
            result.treatmentParameter = candidate.treatmentParameter;
            result.treatmentValueAvailable = true;
            result.treatmentValue = candidate.treatmentValue;
            result.provenanceAvailable = true;
            return true;
        }

        static bool HasProvenance(RagdollLabMetadata metadata)
        {
            return metadata != null
                && (!string.IsNullOrWhiteSpace(metadata.tuningSessionId)
                    || !string.IsNullOrWhiteSpace(metadata.experimentId)
                    || !string.IsNullOrWhiteSpace(metadata.configurationFingerprint)
                    || !string.IsNullOrWhiteSpace(metadata.treatmentParameter)
                    || !string.Equals(metadata.runRole, "none", System.StringComparison.Ordinal));
        }

        static bool Approximately(float left, float right, float tolerance)
        {
            return RagdollLabMath.IsFinite(left) && RagdollLabMath.IsFinite(right)
                && Mathf.Abs(left - right) <= tolerance;
        }

        static void AddSafetyGuards(
            BalanceComparisonReport result,
            EvaluationReport baseline,
            EvaluationReport candidate,
            RagdollLabThresholds thresholds)
        {
            ScenarioReport before = baseline.scenarioReport;
            ScenarioReport after = candidate.scenarioReport;
            if (after.fallenFrameCount > before.fallenFrameCount)
                AddSafetyGuard(result, "candidate_fallen_frames_increased");
            if (after.unrecoverableFrameCount > before.unrecoverableFrameCount)
                AddSafetyGuard(result, "candidate_unrecoverable_frames_increased");
            if (after.unpinnedStaggerEpisodeCount > before.unpinnedStaggerEpisodeCount)
                AddSafetyGuard(result, "candidate_unpinned_episodes_increased");
            if (after.penetration.max > SafetyLimit(before.penetration.max, thresholds))
                AddSafetyGuard(result, "candidate_penetration_increased");
            if (after.footSlipSpeed.mean > SafetyLimit(before.footSlipSpeed.mean, thresholds))
                AddSafetyGuard(result, "candidate_foot_slip_increased");
            if (after.kineticEnergy.max > SafetyLimit(before.kineticEnergy.max, thresholds))
                AddSafetyGuard(result, "candidate_energy_increased");
            if (after.balancerTorque != null && after.balancerTorque.max > thresholds.balancerTorqueWarning)
                AddSafetyGuard(result, "candidate_balancer_torque_exceeded");
            if (after.staggerEpisodes != null && after.staggerEpisodes.Length > 0
                && string.Equals(after.name, "RecoverablePush", System.StringComparison.OrdinalIgnoreCase))
                AddSafetyGuard(result, "candidate_required_stagger_step");
        }

        static void AddSafetyGuard(BalanceComparisonReport result, string reason)
        {
            result.safetyGuards.Add(reason);
            if (result.rejectionReasons == null) result.rejectionReasons = new List<string>();
            result.rejectionReasons.Add(reason);
        }

        static float SafetyLimit(float baseline, RagdollLabThresholds thresholds)
        {
            return baseline + Mathf.Max(
                Mathf.Abs(baseline) * thresholds.comparisonSafetyToleranceRatio,
                thresholds.comparisonSafetyToleranceAbsolute);
        }

        static void Add(
            List<ComparisonMetric> metrics,
            string name,
            string unit,
            float current,
            float baseline,
            bool lowerIsBetter,
            float toleranceRatio = 0f,
            float toleranceAbsolute = 0f)
        {
            float delta = current - baseline;
            float relative = Mathf.Abs(baseline) > 0.000001f ? delta / Mathf.Abs(baseline) : 0f;
            float tolerance = Mathf.Max(Mathf.Abs(baseline) * toleranceRatio, toleranceAbsolute);
            metrics.Add(new ComparisonMetric
            {
                name = name, unit = unit, current = current, baseline = baseline,
                expectation = lowerIsBetter ? "lower" : "higher",
                delta = delta, relativeDelta = relative,
                regression = lowerIsBetter ? delta > tolerance : delta < -tolerance
            });
        }

        static void Add(ComparisonReport result, string name, string unit, float current, float baseline, bool lowerIsBetter)
        {
            float delta = current - baseline;
            float relative = Mathf.Abs(baseline) > 0.000001f ? delta / Mathf.Abs(baseline) : 0f;
            result.metrics.Add(new ComparisonMetric { name = name, unit = unit, current = current, baseline = baseline, delta = delta, relativeDelta = relative,
                regression = lowerIsBetter ? delta > 0f : delta < 0f });
        }

        public static EvaluationReport Read(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<EvaluationReport>(File.ReadAllText(path)); } catch { return null; }
        }

        public static void CopyBaseline(string source, string destination)
        {
            if (!File.Exists(source)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }
    }
}
