using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollClosurePipelineEditorTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),
                "hairibar-closure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void J06_IndependentValidationAuditsProvisionalWithoutRebuildingIt()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            string provisional = PathIn("provisional.json");
            string validationPath = PathIn("validation.json");
            RagdollClosurePipeline.WriteProvisional(manifest, provisional);

            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    provisional, validationPath);

            Assert.That(validation.succeeded, Is.True,
                string.Join("\n", validation.errors ?? Array.Empty<string>()));
            Assert.That(validation.total,
                Is.EqualTo(RagdollCapabilityCatalog.ExpectedCount));
            Assert.That(validation.verifiedBeforeJ06,
                Is.EqualTo(RagdollCapabilityCatalog.ExpectedCount - 2));
            Assert.That(validation.openBeforeJ06, Is.EqualTo(1));
            Assert.That(validation.notApplicable, Is.EqualTo(1));
            Assert.That(validation.provisionalManifestSha256,
                Is.EqualTo(RagdollClosurePipeline.ComputeSha256(provisional)));
            Assert.That(File.Exists(validationPath), Is.True);
        }

        [Test]
        public void IndependentValidation_RejectsDuplicateCapabilityId()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            manifest.entries[1].id = manifest.entries[0].id;
            string provisional = Write(manifest);

            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    provisional, PathIn("validation.json"));

            Assert.That(validation.succeeded, Is.False);
            Assert.That(validation.errors,
                Has.Some.StartsWith("DuplicateCapabilityId:"));
        }

        [Test]
        public void IndependentValidation_RejectsArtifactHashAndProvenanceMismatch()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            RagdollCoverageManifest.Entry entry = manifest.entries.First(
                value => value.status == "Verified");
            entry.evidenceArtifacts[0].sha256 = new string('0', 64);
            entry.evidenceArtifacts[0].sourceRevision = "another-revision";
            string provisional = Write(manifest);

            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    provisional, PathIn("validation.json"));

            Assert.That(validation.succeeded, Is.False);
            Assert.That(validation.errors,
                Has.Some.StartsWith("ArtifactHashMismatch:"));
            Assert.That(validation.errors,
                Has.Some.StartsWith("ArtifactProvenanceMismatch:"));
        }

        [Test]
        public void IndependentValidation_RejectsPrematureClosureCounts()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            RagdollCoverageManifest.Entry entry = manifest.entries.First(
                value => value.id != "G05" && value.id != "J06");
            entry.status = "Open";
            manifest.verified--;
            manifest.open++;
            string provisional = Write(manifest);

            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    provisional, PathIn("validation.json"));

            Assert.That(validation.succeeded, Is.False);
            Assert.That(validation.errors,
                Does.Contain("ProvisionalCountsMustBe138Verified1Open1NA"));
        }

        [Test]
        public void IndependentValidation_RejectsCanonicalContractMutation()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            RagdollCoverageManifest.Entry entry = manifest.entries.Single(
                value => value.id == "A01");
            entry.observableClaim = "A substituted claim.";
            entry.affectedApis = "Substituted.Api";
            entry.requiredEvidenceKinds = new[]
            {
                nameof(RagdollEvidenceKind.DocumentationAudit)
            };

            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    Write(manifest), PathIn("validation.json"));

            Assert.That(validation.succeeded, Is.False);
            Assert.That(validation.errors,
                Does.Contain("CatalogClaimMismatch:A01"));
            Assert.That(validation.errors,
                Does.Contain("CatalogApisMismatch:A01"));
            Assert.That(validation.errors,
                Does.Contain("CatalogEvidenceRequirementsMismatch:A01"));
        }

        [Test]
        public void IndependentValidation_RejectsMinimalSelfReportedJson()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            RagdollEvidenceArtifact profiler = manifest.artifacts.Single(
                value => value.kind == RagdollEvidenceKind.ProfilerResult);
            File.WriteAllText(profiler.path,
                "{\"schemaVersion\":2,\"succeeded\":true}");
            profiler.sha256 = RagdollClosurePipeline.ComputeSha256(profiler.path);
            foreach (RagdollCoverageManifest.Entry entry in manifest.entries)
                if (string.Equals(entry.executionArtifact, profiler.path,
                    StringComparison.OrdinalIgnoreCase))
                    entry.executionArtifactSha256 = profiler.sha256;

            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    Write(manifest), PathIn("validation.json"));

            Assert.That(validation.succeeded, Is.False);
            Assert.That(validation.errors, Has.Some.StartsWith(
                "ArtifactContentInvalid:H05:ProfilerResult:"));
        }

        [Test]
        public void Finalizer_ClosesOnlyJ06AndBindsBothDigests()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            string provisional = Write(manifest);
            string validationPath = PathIn("validation.json");
            RagdollClosurePipeline.IndependentValidation validation =
                RagdollClosurePipeline.ValidateProvisional(
                    provisional, validationPath);
            Assert.That(validation.succeeded, Is.True);
            validation.validatorProcessId = System.Diagnostics.Process
                .GetCurrentProcess().Id + 2;
            File.WriteAllText(validationPath,
                JsonUtility.ToJson(validation, true));

            RagdollClosurePipeline.FinalManifestEnvelope result =
                RagdollClosurePipeline.FinalizeManifest(
                    provisional, validationPath, PathIn("final.json"));

            Assert.That(result.schemaVersion,
                Is.EqualTo(RagdollClosurePipeline.FinalSchemaVersion));
            Assert.That(result.provisionalManifestSha256,
                Is.EqualTo(RagdollClosurePipeline.ComputeSha256(provisional)));
            Assert.That(result.independentValidationSha256,
                Is.EqualTo(RagdollClosurePipeline.ComputeSha256(validationPath)));
            Assert.That(result.manifest.total,
                Is.EqualTo(RagdollCapabilityCatalog.ExpectedCount));
            Assert.That(result.manifest.verified,
                Is.EqualTo(RagdollCapabilityCatalog.ExpectedCount - 1));
            Assert.That(result.manifest.open, Is.Zero);
            Assert.That(result.manifest.notApplicable, Is.EqualTo(1));

            RagdollCoverageManifest.Entry j06 = result.manifest.entries.Single(
                value => value.id == "J06");
            Assert.That(j06.status, Is.EqualTo("Verified"));
            Assert.That(j06.evidenceArtifacts, Has.Length.EqualTo(1));
            Assert.That(j06.evidenceArtifacts[0].kind,
                Is.EqualTo(RagdollEvidenceKind.IndependentValidation));
            Assert.That(j06.executionArtifactSha256,
                Is.EqualTo(result.independentValidationSha256));
        }

        [Test]
        public void Finalizer_RejectsValidationProducedByCurrentProcess()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            string provisional = Write(manifest);
            string validationPath = PathIn("validation.json");
            Assert.That(RagdollClosurePipeline.ValidateProvisional(
                provisional, validationPath).succeeded, Is.True);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => RagdollClosurePipeline.FinalizeManifest(
                    provisional, validationPath, PathIn("final.json")));

            Assert.That(exception.Message,
                Does.Contain("provenance").IgnoreCase);
            Assert.That(File.Exists(PathIn("final.json")), Is.False);
        }

        [Test]
        public void Finalizer_RejectsProvisionalModifiedAfterValidation()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            string provisional = Write(manifest);
            string validationPath = PathIn("validation.json");
            Assert.That(RagdollClosurePipeline.ValidateProvisional(
                provisional, validationPath).succeeded, Is.True);
            File.AppendAllText(provisional, " ");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => RagdollClosurePipeline.FinalizeManifest(
                    provisional, validationPath, PathIn("final.json")));

            Assert.That(exception.Message, Does.Contain("digest").IgnoreCase);
            Assert.That(File.Exists(PathIn("final.json")), Is.False);
        }

        [Test]
        public void Finalizer_RejectsFailedValidation()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            manifest.entries[0].source = "https://example.invalid/not-official";
            string provisional = Write(manifest);
            string validationPath = PathIn("validation.json");
            Assert.That(RagdollClosurePipeline.ValidateProvisional(
                provisional, validationPath).succeeded, Is.False);

            Assert.Throws<InvalidDataException>(() =>
                RagdollClosurePipeline.FinalizeManifest(
                    provisional, validationPath, PathIn("final.json")));
            Assert.That(File.Exists(PathIn("final.json")), Is.False);
        }

        string Write(RagdollCoverageManifest.Manifest manifest)
        {
            string path = PathIn("provisional.json");
            return RagdollClosurePipeline.WriteProvisional(manifest, path);
        }

        string PathIn(string name)
        {
            return Path.Combine(directory, name);
        }

        RagdollCoverageManifest.Manifest CreateClosableManifest()
        {
            RagdollCoverageManifest.Manifest manifest =
                RagdollCoverageManifest.Build();
            string sourceHash = RagdollCoverageManifest
                .ComputeCurrentSourceTreeSha256();
            string revision = RagdollClosureCoordinator
                .ResolveSourceRevision(sourceHash);
            string runId = Guid.NewGuid().ToString("D");
            List<RagdollEvidenceArtifact> artifacts =
                CreateCanonicalArtifacts(manifest, revision, sourceHash, runId);

            foreach (RagdollCoverageManifest.Entry entry in manifest.entries)
            {
                if (entry.id == "G05") continue;
                if (entry.id == "J06")
                {
                    entry.status = "Open";
                    entry.reason = "Independent validation has not run.";
                    entry.requiredEvidenceKinds = new[]
                    {
                        nameof(RagdollEvidenceKind.IndependentValidation)
                    };
                    entry.evidenceArtifacts = Array.Empty<RagdollEvidenceArtifact>();
                    entry.executionArtifact = string.Empty;
                    entry.executionArtifactSha256 = string.Empty;
                    continue;
                }

                entry.status = "Verified";
                entry.reason = string.Empty;
                RagdollCapabilityContract contract =
                    RagdollCapabilityCatalog.Get(entry.id);
                entry.requiredEvidenceKinds = contract.RequiredEvidence
                    .Select(kind => kind.ToString()).ToArray();
                entry.evidenceArtifacts = contract.RequiredEvidence
                    .Select(kind => artifacts.Single(artifact =>
                        artifact.kind == kind)).ToArray();
                entry.executionArtifact = entry.evidenceArtifacts[0].path;
                entry.executionArtifactSha256 =
                    entry.evidenceArtifacts[0].sha256;
            }

            manifest.generatedUtc = DateTime.UtcNow.ToString("O");
            manifest.sourceRevision = revision;
            manifest.sourceTreeSha256 = sourceHash;
            manifest.certificationRunId = runId;
            manifest.producerProcessId = System.Diagnostics.Process
                .GetCurrentProcess().Id + 1;
            manifest.artifacts = artifacts.ToArray();
            manifest.total = RagdollCapabilityCatalog.ExpectedCount;
            manifest.verified = RagdollCapabilityCatalog.ExpectedCount - 2;
            manifest.open = 1;
            manifest.notApplicable = 1;
            return manifest;
        }

        List<RagdollEvidenceArtifact> CreateCanonicalArtifacts(
            RagdollCoverageManifest.Manifest manifest,
            string revision,
            string sourceHash,
            string runId)
        {
            var result = new List<RagdollEvidenceArtifact>();
            string editPath = PathIn("editmode.xml");
            string playPath = PathIn("playmode.xml");
            WriteNUnit(editPath, manifest.entries.Where(entry =>
                entry.id != "G05" && entry.testKind == "EditMode")
                .Select(entry => entry.exactTest));
            IEnumerable<string> playTests = manifest.entries.Where(entry =>
                entry.id != "G05" && entry.testKind != null
                && entry.testKind.StartsWith("PlayMode/", StringComparison.Ordinal))
                .Select(entry => entry.exactTest)
                .Concat(RagdollCapabilityCatalog.Contracts.SelectMany(contract =>
                    contract.ExactNUnitEvidenceTests
                        .Where(pair => pair.Key == RagdollEvidenceKind.NUnitPlayMode)
                        .Select(pair => pair.Value)));
            WriteNUnit(playPath, playTests);
            result.Add(Artifact(RagdollEvidenceKind.NUnitEditMode,
                editPath, revision, sourceHash, runId, Array.Empty<string>()));
            result.Add(Artifact(RagdollEvidenceKind.NUnitPlayMode,
                playPath, revision, sourceHash, runId, Array.Empty<string>()));

            string playerPath = PathIn("windows-player-result.json");
            File.WriteAllText(playerPath,
                PlayerJson(runId, revision, sourceHash));
            result.Add(Artifact(RagdollEvidenceKind.WindowsPlayerScenario,
                playerPath, revision, sourceHash, runId,
                IdsRequiring(RagdollEvidenceKind.WindowsPlayerScenario)));

            string scenePath = PathIn("scene-results.json");
            File.WriteAllText(scenePath,
                ProvenancePrefix(runId, revision, sourceHash)
                + ",\"succeeded\":true,\"scenes\":"
                + ScenarioArrayJson() + "}");
            result.Add(Artifact(RagdollEvidenceKind.SceneArtifact,
                scenePath, revision, sourceHash, runId,
                IdsRequiring(RagdollEvidenceKind.SceneArtifact)));

            string profilerPath = PathIn("profiler-results.json");
            File.WriteAllText(profilerPath,
                ProfilerJson(runId, revision, sourceHash));
            result.Add(Artifact(RagdollEvidenceKind.ProfilerResult,
                profilerPath, revision, sourceHash, runId,
                IdsRequiring(RagdollEvidenceKind.ProfilerResult)));

            string buildPath = PathIn("build-manifest.json");
            File.WriteAllText(buildPath,
                BuildJson(runId, revision, sourceHash));
            result.Add(Artifact(RagdollEvidenceKind.BuildReport,
                buildPath, revision, sourceHash, runId,
                IdsRequiring(RagdollEvidenceKind.BuildReport)));

            string documentationPath = PathIn("documentation-audit.json");
            string previous = Environment.GetEnvironmentVariable(
                "HAIRIBAR_DOCUMENTATION_AUDIT");
            string previousOutput = Environment.GetEnvironmentVariable(
                RagdollClosureCoordinator.OutputEnvironmentVariable);
            string previousRunId = Environment.GetEnvironmentVariable(
                RagdollClosureCoordinator.RunIdEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    "HAIRIBAR_DOCUMENTATION_AUDIT", documentationPath);
                Environment.SetEnvironmentVariable(
                    RagdollClosureCoordinator.OutputEnvironmentVariable,
                    directory);
                Environment.SetEnvironmentVariable(
                    RagdollClosureCoordinator.RunIdEnvironmentVariable,
                    runId);
                MethodInfo write = typeof(HairibarCertification).GetMethod(
                    "WriteDocumentationAudit",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(write, Is.Not.Null);
                write.Invoke(null, new object[] { directory });
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "HAIRIBAR_DOCUMENTATION_AUDIT", previous);
                Environment.SetEnvironmentVariable(
                    RagdollClosureCoordinator.OutputEnvironmentVariable,
                    previousOutput);
                Environment.SetEnvironmentVariable(
                    RagdollClosureCoordinator.RunIdEnvironmentVariable,
                    previousRunId);
            }
            result.Add(Artifact(RagdollEvidenceKind.DocumentationAudit,
                documentationPath, revision, sourceHash, runId,
                IdsRequiring(RagdollEvidenceKind.DocumentationAudit)));
            return result;
        }

        static string[] IdsRequiring(RagdollEvidenceKind kind)
        {
            return RagdollCapabilityCatalog.Contracts
                .Where(contract => contract.IsApplicable
                    && contract.RequiredEvidence.Contains(kind))
                .Select(contract => contract.Id).ToArray();
        }

        static RagdollEvidenceArtifact Artifact(
            RagdollEvidenceKind kind,
            string path,
            string revision,
            string sourceHash,
            string runId,
            string[] ids)
        {
            return new RagdollEvidenceArtifact
            {
                kind = kind,
                path = path,
                sha256 = RagdollClosurePipeline.ComputeSha256(path),
                platform = kind == RagdollEvidenceKind.WindowsPlayerScenario
                    ? "Windows64" : "Editor",
                scenario = "CanonicalClosureFixture",
                generatedUtc = DateTime.UtcNow.ToString("O"),
                certificationRunId = runId,
                sourceRevision = revision,
                sourceTreeSha256 = sourceHash,
                capabilityIds = ids,
                validationStatus = "Valid",
                validationReason = string.Empty
            };
        }

        static void WriteNUnit(string path, IEnumerable<string> tests)
        {
            string[] names = tests.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).ToArray();
            var xml = new StringBuilder(
                "<test-run result='Passed' failed='0' skipped='0' "
                + "inconclusive='0' warnings='0'><test-suite result='Passed'>");
            foreach (string name in names)
                xml.Append("<test-case fullname='")
                    .Append(SecurityElement.Escape(name))
                    .Append("' result='Passed'/>");
            xml.Append("</test-suite></test-run>");
            File.WriteAllText(path, xml.ToString());
        }

        string BuildJson(string runId, string revision, string sourceHash)
        {
            string[] targets = { "Windows64", "Linux64", "macOS", "WebGL" };
            var entries = new List<string>();
            foreach (string target in targets)
            {
                string output = PathIn("build-" + target);
                Directory.CreateDirectory(output);
                const string traversal = "0|Build|0\n";
                entries.Add("{\"target\":\"" + target
                    + "\",\"result\":\"Succeeded\",\"succeeded\":true,"
                    + "\"development\":true,\"allowDebugging\":true,"
                    + "\"outputExists\":true,\"output\":\""
                    + EscapeJson(output) + "\",\"stepsScanned\":1,"
                    + "\"messagesScanned\":0,\"reportTraversalSha256\":\""
                    + Sha256Text(traversal) + "\",\"reportSteps\":[{"
                    + "\"name\":\"Build\",\"messages\":[]}],\"diagnostics\":[]}");
            }
            return ProvenancePrefix(runId, revision, sourceHash)
                + ",\"builds\":["
                + string.Join(",", entries) + "]}";
        }

        static string PlayerJson(
            string runId, string revision, string sourceHash)
        {
            return ProvenancePrefix(runId, revision, sourceHash)
                + ",\"succeeded\":true,"
                + "\"platform\":\"Windows64\",\"scenarios\":"
                + ScenarioArrayJson() + "}";
        }

        static string ScenarioArrayJson()
        {
            var performance = new List<string>();
            foreach (int population in new[] { 1, 10, 25, 50 })
            foreach (string mode in new[]
                     { "ActiveTree", "ActiveFlat", "Kinematic", "Disabled" })
                performance.Add("{\"puppets\":" + population
                    + ",\"mode\":\"" + mode
                    + "\",\"cpuMedianNanoseconds\":1,"
                    + "\"cpuP95Nanoseconds\":2,\"memoryMedianBytes\":1,"
                    + "\"memoryP95Bytes\":2,"
                    + "\"maximumGcAllocatedInFrame\":0,"
                    + "\"measuredFrames\":600}");
            Func<string, string> assertion = name =>
                "{\"id\":\"" + name + "\",\"name\":\"" + name
                + "\",\"succeeded\":true,\"comparison\":\"Equal\","
                + "\"actual\":1,\"expected\":1,\"tolerance\":0}";
            string coreAssertions = string.Join(",", new[]
            {
                "core.physx-fall-distance",
                "core.saturated-contact-count",
                "core.respawn-position-error",
                "core.manual-simulation-completed",
                "core.joint-break-irreversible"
            }.Select(assertion));
            string humanoidAssertions = string.Join(",", new[]
            {
                "humanoid.valid-avatar-count",
                "humanoid.fall-initialized",
                "humanoid.ik-owned-solver-count",
                "humanoid.animation-event-count",
                "humanoid.root-motion-distance",
                "humanoid.baker-clip-length"
            }.Select(assertion));
            string propAssertions = string.Join(",", new[]
            {
                "props.pickup-held",
                "props.collection-held",
                "props.additional-pin-preserved",
                "props.rollback-exact",
                "props.drop-empty"
            }.Select(assertion));
            string performanceAssertions = assertion(
                "performance.flatten-hierarchy") + "," + assertion(
                "performance.tree-hierarchy");
            return "[{\"name\":\"CoreLifecycle\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":[" + coreAssertions + "]},"
                + "{\"name\":\"HumanoidBakerFall\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":[" + humanoidAssertions + "]},"
                + "{\"name\":\"HierarchyProps\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":[" + propAssertions + "]},"
                + "{\"name\":\"CollisionsPerformance\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":[" + performanceAssertions
                + "],\"performance\":[" + string.Join(",", performance) + "]}]";
        }

        static string ProfilerJson(
            string runId, string revision, string sourceHash)
        {
            string[] names =
            {
                "matching", "mapping", "collision-relay", "com",
                "additional-pin", "baker-realtime"
            };
            string[] scopes =
            {
                "RagdollAnimator.DoAnimationMatching",
                "RagdollAnimator.MapRagdollToTarget",
                "RagdollCollisionHub.Dispatch",
                "RagdollCenterOfMassSubBehaviour.FixedUpdate",
                "RagdollPropMuscle.ApplyAdditionalPinAfterAnimationMatching",
                "RagdollBaker.AdvanceRealtimeSampling"
            };
            var paths = new string[names.Length];
            string rawZero = string.Join(",", Enumerable.Repeat("0", 600));
            for (int index = 0; index < names.Length; index++)
                paths[index] = "{\"name\":\"" + names[index]
                    + "\",\"measurementScope\":\"" + scopes[index]
                    + "\",\"succeeded\":true,\"samples\":600,"
                    + "\"gcAllocatedBytes\":0,"
                    + "\"maxGcAllocatedBytesInFrame\":0,"
                    + "\"rawAllocationSamples\":[" + rawZero + "]}";
            string cpu = string.Join(",", Enumerable.Repeat("1", 300)
                .Concat(Enumerable.Repeat("2", 300)));
            string memory = string.Join(",", Enumerable.Repeat("1", 300)
                .Concat(Enumerable.Repeat("2", 300)));
            return ProvenancePrefix(runId, revision, sourceHash)
                + ",\"succeeded\":true,"
                + "\"warmupFrames\":120,\"measuredFrames\":600,"
                + "\"cpuMilliseconds\":{\"median\":1,\"p95\":2,\"sampleCount\":600,\"rawSamples\":[" + cpu + "]},"
                + "\"memoryBytes\":{\"median\":1,\"p95\":2,\"sampleCount\":600,\"rawSamples\":[" + memory + "]},"
                + "\"criticalPaths\":[" + string.Join(",", paths) + "]}";
        }

        static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string Sha256Text(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2")));
        }

        static string ProvenancePrefix(
            string runId, string revision, string sourceHash)
        {
            return "{\"schemaVersion\":3,\"certificationRunId\":\""
                + EscapeJson(runId) + "\",\"sourceRevision\":\""
                + EscapeJson(revision) + "\",\"sourceTreeSha256\":\""
                + EscapeJson(sourceHash) + "\"";
        }
    }
}
