using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollTuningArtifactTransportTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "RagdollLabArtifact-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ManifestRoundTripsBoundEvaluationAndHashesNormativeFiles()
        {
            RagdollTuningRunBinding binding = Binding();
            EvaluationReport source = Report(binding);
            WritePayloads(source);
            var transport = new RagdollTuningFileArtifactTransport();

            Assert.That(transport.TryWriteManifest(directory, binding, source, out RagdollTuningArtifactManifest written, out string writeReason), Is.True, writeReason);
            Assert.That(written.schemaVersion, Is.EqualTo(RagdollTuningArtifactSchema.Version));
            Assert.That(File.Exists(Path.Combine(directory, RagdollTuningArtifactSchema.ManifestFileName)), Is.True);

            Assert.That(transport.TryRead(directory, binding, out EvaluationReport read, out RagdollTuningArtifactManifest manifest, out string readReason), Is.True, readReason);
            Assert.That(read.metadata.runId, Is.EqualTo("candidate-run"));
            Assert.That(manifest.evaluationSha256, Is.EqualTo(written.evaluationSha256));
            Assert.That(manifest.balanceComparisonSha256, Is.EqualTo(written.balanceComparisonSha256));
        }

        [Test]
        public void ChangedEvaluationBytesFailClosedBeforeDeserialization()
        {
            RagdollTuningRunBinding binding = Binding();
            WritePayloads(Report(binding));
            var transport = new RagdollTuningFileArtifactTransport();
            Assert.That(transport.TryWriteManifest(directory, binding, Report(binding), out _, out string writeReason), Is.True, writeReason);

            File.AppendAllText(Path.Combine(directory, RagdollTuningArtifactSchema.EvaluationFileName), "\nchanged");

            Assert.That(transport.TryRead(directory, binding, out _, out _, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("evaluation_hash_mismatch"));
        }

        [Test]
        public void ManifestBindingMismatchAndPathTraversalFailClosed()
        {
            RagdollTuningRunBinding binding = Binding();
            WritePayloads(Report(binding));
            var transport = new RagdollTuningFileArtifactTransport();
            Assert.That(transport.TryWriteManifest(directory, binding, Report(binding), out _, out string writeReason), Is.True, writeReason);

            RagdollTuningRunBinding wrongRun = Binding();
            wrongRun.runId = "other-run";
            Assert.That(transport.TryRead(directory, wrongRun, out _, out _, out string mismatch), Is.False);
            Assert.That(mismatch, Is.EqualTo("run_id_mismatch"));

            RagdollTuningArtifactManifest manifest = JsonUtility.FromJson<RagdollTuningArtifactManifest>(
                File.ReadAllText(Path.Combine(directory, RagdollTuningArtifactSchema.ManifestFileName)));
            manifest.evaluationFile = "../evaluation.json";
            File.WriteAllText(Path.Combine(directory, RagdollTuningArtifactSchema.ManifestFileName), JsonUtility.ToJson(manifest));

            Assert.That(transport.TryRead(directory, binding, out _, out _, out string unsafePath), Is.False);
            Assert.That(unsafePath, Is.EqualTo("evaluation_file_unsafe"));
        }

        [Test]
        public void SessionStateRoundTripsAndReplacesAtomically()
        {
            RagdollTuningSession source = RagdollTuningPlanner.CreateSession(
                "session", "Stagger", new[] { new RagdollTuningParameterValue("pin", 0.7f) }, 4);
            source.lastDecision = "accepted";
            string path = Path.Combine(directory, RagdollTuningArtifactSchema.SessionFileName);
            var store = new RagdollTuningSessionFileStore();

            Assert.That(store.TryWrite(path, source, out string writeReason), Is.True, writeReason);

            source.lastDecision = "promoted";
            source.baseline[0].value = 0.8f;
            Assert.That(store.TryWrite(path, source, out writeReason), Is.True, writeReason);
            Assert.That(store.TryRead(path, "session", out RagdollTuningSession read, out string readReason), Is.True, readReason);
            Assert.That(read.lastDecision, Is.EqualTo("promoted"));
            Assert.That(read.baseline[0].value, Is.EqualTo(0.8f));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void SessionStateRejectsSchemaAndIdentityMismatch()
        {
            RagdollTuningSession source = RagdollTuningPlanner.CreateSession(
                "session", "Stagger", new[] { new RagdollTuningParameterValue("pin", 0.7f) }, 4);
            string path = Path.Combine(directory, RagdollTuningArtifactSchema.SessionFileName);
            File.WriteAllText(path, JsonUtility.ToJson(source));
            var store = new RagdollTuningSessionFileStore();

            Assert.That(store.TryRead(path, "other-session", out _, out string identityReason), Is.False);
            Assert.That(identityReason, Is.EqualTo("session_id_mismatch"));

            source.schemaVersion = "0.0.0";
            File.WriteAllText(path, JsonUtility.ToJson(source));
            Assert.That(store.TryRead(path, "session", out _, out string schemaReason), Is.False);
            Assert.That(schemaReason, Is.EqualTo("session_schema_mismatch"));
        }

        static RagdollTuningRunBinding Binding()
        {
            return new RagdollTuningRunBinding
            {
                sessionId = "session",
                experimentId = "experiment",
                runId = "candidate-run",
                runRole = "candidate",
                artifactDirectory = "unused",
                configurationFingerprint = "candidate-config",
                baselineConfigurationFingerprint = "baseline-config",
                treatmentParameter = "pin",
                treatmentValueAvailable = true,
                treatmentValue = 0.8f
            };
        }

        static EvaluationReport Report(RagdollTuningRunBinding binding)
        {
            return new EvaluationReport
            {
                metadata = new RagdollLabMetadata
                {
                    runId = binding.runId,
                    tuningSessionId = binding.sessionId,
                    experimentId = binding.experimentId,
                    runRole = binding.runRole,
                    configurationFingerprint = binding.configurationFingerprint,
                    baselineConfigurationFingerprint = binding.baselineConfigurationFingerprint,
                    treatmentParameter = binding.treatmentParameter,
                    treatmentValueAvailable = binding.treatmentValueAvailable,
                    treatmentValue = binding.treatmentValue
                },
                completed = true,
                finiteData = true
            };
        }

        void WritePayloads(EvaluationReport report)
        {
            File.WriteAllText(Path.Combine(directory, RagdollTuningArtifactSchema.EvaluationFileName), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(directory, RagdollTuningArtifactSchema.BalanceComparisonFileName), JsonUtility.ToJson(new BalanceComparisonReport(), true));
        }
    }
}
