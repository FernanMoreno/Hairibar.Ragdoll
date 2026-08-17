using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public interface IRagdollTuningArtifactTransport
    {
        bool TryRead(
            string directory,
            RagdollTuningRunBinding expected,
            out EvaluationReport report,
            out RagdollTuningArtifactManifest manifest,
            out string reason);
    }

    /// <summary>
    /// Publishes and verifies the small manifest that binds persisted RagdollLab
    /// JSON to one tuning run. The manifest is written last and readers fail
    /// closed on any missing, unsafe or changed payload.
    /// </summary>
    public sealed class RagdollTuningFileArtifactTransport : IRagdollTuningArtifactTransport
    {
        public bool TryWriteManifest(
            string directory,
            RagdollTuningRunBinding binding,
            EvaluationReport report,
            out RagdollTuningArtifactManifest manifest,
            out string reason)
        {
            manifest = null;
            reason = ValidateBinding(binding);
            if (reason != null) return false;
            reason = MetadataMismatch(report?.metadata, binding);
            if (reason != null) return false;
            if (string.IsNullOrWhiteSpace(directory)) return Fail("artifact_directory_missing", out reason);

            try
            {
                Directory.CreateDirectory(directory);
                string evaluationPath = Path.Combine(directory, RagdollTuningArtifactSchema.EvaluationFileName);
                string balancePath = Path.Combine(directory, RagdollTuningArtifactSchema.BalanceComparisonFileName);
                if (!File.Exists(evaluationPath)) return Fail("evaluation_artifact_missing", out reason);
                if (!File.Exists(balancePath)) return Fail("balance_comparison_artifact_missing", out reason);

                manifest = CreateManifest(binding, evaluationPath, balancePath);
                string manifestPath = Path.Combine(directory, RagdollTuningArtifactSchema.ManifestFileName);
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                manifest = null;
                return Fail("manifest_write_failed:" + exception.GetType().Name, out reason);
            }
        }

        public bool TryRead(
            string directory,
            RagdollTuningRunBinding expected,
            out EvaluationReport report,
            out RagdollTuningArtifactManifest manifest,
            out string reason)
        {
            report = null;
            manifest = null;
            reason = ValidateBinding(expected);
            if (reason != null) return false;
            if (string.IsNullOrWhiteSpace(directory)) return Fail("artifact_directory_missing", out reason);

            try
            {
                if (!Directory.Exists(directory)) return Fail("artifact_directory_missing", out reason);
                string manifestPath = Path.Combine(directory, RagdollTuningArtifactSchema.ManifestFileName);
                if (!File.Exists(manifestPath)) return Fail("artifact_manifest_missing", out reason);
                manifest = JsonUtility.FromJson<RagdollTuningArtifactManifest>(File.ReadAllText(manifestPath));
                if (manifest == null) return Fail("artifact_manifest_invalid", out reason);
                reason = ManifestMismatch(manifest, expected);
                if (reason != null) return false;
                if (!SafeFileName(manifest.evaluationFile)
                    || !string.Equals(manifest.evaluationFile, RagdollTuningArtifactSchema.EvaluationFileName, StringComparison.Ordinal))
                    return Fail("evaluation_file_unsafe", out reason);
                if (!SafeFileName(manifest.balanceComparisonFile)
                    || !string.Equals(manifest.balanceComparisonFile, RagdollTuningArtifactSchema.BalanceComparisonFileName, StringComparison.Ordinal))
                    return Fail("balance_comparison_file_unsafe", out reason);

                string evaluationPath = Path.Combine(directory, manifest.evaluationFile);
                string balancePath = Path.Combine(directory, manifest.balanceComparisonFile);
                if (!File.Exists(evaluationPath)) return Fail("evaluation_artifact_missing", out reason);
                if (!File.Exists(balancePath)) return Fail("balance_comparison_artifact_missing", out reason);
                if (!string.Equals(Sha256(evaluationPath), manifest.evaluationSha256, StringComparison.Ordinal))
                    return Fail("evaluation_hash_mismatch", out reason);
                if (!string.Equals(Sha256(balancePath), manifest.balanceComparisonSha256, StringComparison.Ordinal))
                    return Fail("balance_comparison_hash_mismatch", out reason);

                report = JsonUtility.FromJson<EvaluationReport>(File.ReadAllText(evaluationPath));
                reason = MetadataMismatch(report?.metadata, expected);
                if (reason != null)
                {
                    report = null;
                    return false;
                }
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                report = null;
                manifest = null;
                return Fail("artifact_read_failed:" + exception.GetType().Name, out reason);
            }
        }

        public static string RunDirectory(string artifactRoot, string runId)
        {
            if (string.IsNullOrWhiteSpace(artifactRoot) || !SafePathSegment(runId)) return null;
            return Path.Combine(artifactRoot, runId);
        }

        static RagdollTuningArtifactManifest CreateManifest(
            RagdollTuningRunBinding binding,
            string evaluationPath,
            string balancePath)
        {
            return new RagdollTuningArtifactManifest
            {
                sessionId = binding.sessionId,
                experimentId = binding.experimentId,
                runId = binding.runId,
                runRole = binding.runRole,
                configurationFingerprint = binding.configurationFingerprint,
                baselineConfigurationFingerprint = binding.baselineConfigurationFingerprint,
                treatmentParameter = binding.treatmentParameter,
                treatmentValueAvailable = binding.treatmentValueAvailable,
                treatmentValue = binding.treatmentValue,
                evaluationSha256 = Sha256(evaluationPath),
                balanceComparisonSha256 = Sha256(balancePath),
                publishedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        static string ValidateBinding(RagdollTuningRunBinding binding)
        {
            if (binding == null) return "tuning_binding_missing";
            if (string.IsNullOrWhiteSpace(binding.sessionId)) return "tuning_session_id_missing";
            if (string.IsNullOrWhiteSpace(binding.experimentId)) return "experiment_id_missing";
            if (string.IsNullOrWhiteSpace(binding.runId) || !SafePathSegment(binding.runId)) return "run_id_invalid";
            if (binding.runRole != "baseline" && binding.runRole != "candidate") return "run_role_invalid";
            if (string.IsNullOrWhiteSpace(binding.configurationFingerprint)) return "configuration_fingerprint_missing";
            if (string.IsNullOrWhiteSpace(binding.baselineConfigurationFingerprint)) return "baseline_configuration_fingerprint_missing";
            if (string.IsNullOrWhiteSpace(binding.treatmentParameter)) return "treatment_parameter_missing";
            if (!binding.treatmentValueAvailable || !Finite(binding.treatmentValue)) return "treatment_value_invalid";
            return null;
        }

        static string ManifestMismatch(RagdollTuningArtifactManifest manifest, RagdollTuningRunBinding binding)
        {
            if (!string.Equals(manifest.schemaVersion, RagdollTuningArtifactSchema.Version, StringComparison.Ordinal)) return "artifact_schema_mismatch";
            if (!string.Equals(manifest.sessionId, binding.sessionId, StringComparison.Ordinal)) return "tuning_session_id_mismatch";
            if (!string.Equals(manifest.experimentId, binding.experimentId, StringComparison.Ordinal)) return "experiment_id_mismatch";
            if (!string.Equals(manifest.runId, binding.runId, StringComparison.Ordinal)) return "run_id_mismatch";
            if (!string.Equals(manifest.runRole, binding.runRole, StringComparison.Ordinal)) return "run_role_mismatch";
            if (!string.Equals(manifest.configurationFingerprint, binding.configurationFingerprint, StringComparison.Ordinal)) return "configuration_fingerprint_mismatch";
            if (!string.Equals(manifest.baselineConfigurationFingerprint, binding.baselineConfigurationFingerprint, StringComparison.Ordinal)) return "baseline_configuration_fingerprint_mismatch";
            if (!string.Equals(manifest.treatmentParameter, binding.treatmentParameter, StringComparison.Ordinal)) return "treatment_parameter_mismatch";
            if (!manifest.treatmentValueAvailable || !Approximately(manifest.treatmentValue, binding.treatmentValue)) return "treatment_value_mismatch";
            return null;
        }

        static string MetadataMismatch(RagdollLabMetadata metadata, RagdollTuningRunBinding binding)
        {
            if (metadata == null) return "report_metadata_missing";
            if (!string.Equals(metadata.tuningSessionId, binding.sessionId, StringComparison.Ordinal)) return "tuning_session_id_mismatch";
            if (!string.Equals(metadata.experimentId, binding.experimentId, StringComparison.Ordinal)) return "experiment_id_mismatch";
            if (!string.Equals(metadata.runId, binding.runId, StringComparison.Ordinal)) return "run_id_mismatch";
            if (!string.Equals(metadata.runRole, binding.runRole, StringComparison.Ordinal)) return "run_role_mismatch";
            if (!string.Equals(metadata.configurationFingerprint, binding.configurationFingerprint, StringComparison.Ordinal)) return "configuration_fingerprint_mismatch";
            if (!string.Equals(metadata.baselineConfigurationFingerprint, binding.baselineConfigurationFingerprint, StringComparison.Ordinal)) return "baseline_configuration_fingerprint_mismatch";
            if (!string.Equals(metadata.treatmentParameter, binding.treatmentParameter, StringComparison.Ordinal)) return "treatment_parameter_mismatch";
            if (!metadata.treatmentValueAvailable || !Approximately(metadata.treatmentValue, binding.treatmentValue)) return "treatment_value_mismatch";
            return null;
        }

        static bool SafeFileName(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !Path.IsPathRooted(value)
                && string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
                && value != "." && value != "..";
        }

        static bool SafePathSegment(string value)
        {
            return SafeFileName(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static bool Approximately(float left, float right)
        {
            return Finite(left) && Finite(right) && Mathf.Abs(left - right) <= 0.000001f;
        }

        static bool Fail(string value, out string reason)
        {
            reason = value;
            return false;
        }
    }

    /// <summary>
    /// Persists the planner session separately from run artifacts. The state is
    /// written through a temporary file and replaced only after serialization
    /// succeeds, so a failed write cannot silently become a new baseline.
    /// </summary>
    public sealed class RagdollTuningSessionFileStore
    {
        public bool TryWrite(
            string path,
            RagdollTuningSession session,
            out string reason)
        {
            reason = Validate(session);
            if (reason != null) return false;
            if (string.IsNullOrWhiteSpace(path)) return Fail("session_state_path_missing", out reason);

            string temporaryPath = path + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory)) return Fail("session_state_directory_missing", out reason);
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(session, true), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                reason = "session_state_write_failed:" + exception.GetType().Name;
                return false;
            }
        }

        public bool TryRead(
            string path,
            string expectedSessionId,
            out RagdollTuningSession session,
            out string reason)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(path)) return Fail("session_state_path_missing", out reason);
            if (!File.Exists(path)) return Fail("session_state_missing", out reason);

            try
            {
                session = JsonUtility.FromJson<RagdollTuningSession>(File.ReadAllText(path));
                reason = Validate(session);
                if (reason != null)
                {
                    session = null;
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(expectedSessionId)
                    && !string.Equals(session.sessionId, expectedSessionId, StringComparison.Ordinal))
                {
                    session = null;
                    return Fail("session_id_mismatch", out reason);
                }
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                session = null;
                return Fail("session_state_read_failed:" + exception.GetType().Name, out reason);
            }
        }

        static string Validate(RagdollTuningSession session)
        {
            if (session == null) return "session_state_invalid";
            if (!string.Equals(session.schemaVersion, RagdollTuningArtifactSchema.SessionVersion, StringComparison.Ordinal))
                return "session_schema_mismatch";
            if (string.IsNullOrWhiteSpace(session.sessionId)) return "session_id_missing";
            if (string.IsNullOrWhiteSpace(session.scenarioProfile)) return "session_profile_missing";
            if (session.baseline == null || session.baseline.Count == 0) return "session_baseline_missing";
            if (session.experiments == null) return "session_experiments_missing";
            if (session.maxExperiments < 1 || session.maxExperiments > RagdollTuningPlanner.MaxExperiments)
                return "session_budget_invalid";
            return null;
        }

        static bool Fail(string value, out string reason)
        {
            reason = value;
            return false;
        }
    }
}
