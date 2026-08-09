using System;
using System.IO;
using System.Linq;
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
            Assert.That(validation.total, Is.EqualTo(140));
            Assert.That(validation.verifiedBeforeJ06, Is.EqualTo(138));
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
        public void Finalizer_ClosesOnlyJ06AndBindsBothDigests()
        {
            RagdollCoverageManifest.Manifest manifest = CreateClosableManifest();
            string provisional = Write(manifest);
            string validationPath = PathIn("validation.json");
            Assert.That(RagdollClosurePipeline.ValidateProvisional(
                provisional, validationPath).succeeded, Is.True);

            RagdollClosurePipeline.FinalManifestEnvelope result =
                RagdollClosurePipeline.FinalizeManifest(
                    provisional, validationPath, PathIn("final.json"));

            Assert.That(result.schemaVersion,
                Is.EqualTo(RagdollClosurePipeline.FinalSchemaVersion));
            Assert.That(result.provisionalManifestSha256,
                Is.EqualTo(RagdollClosurePipeline.ComputeSha256(provisional)));
            Assert.That(result.independentValidationSha256,
                Is.EqualTo(RagdollClosurePipeline.ComputeSha256(validationPath)));
            Assert.That(result.manifest.total, Is.EqualTo(140));
            Assert.That(result.manifest.verified, Is.EqualTo(139));
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
            string artifactPath = PathIn("evidence.json");
            File.WriteAllText(artifactPath,
                "{\"schemaVersion\":2,\"succeeded\":true}");
            string revision = "test-revision";
            string sourceHash = RagdollCoverageManifest
                .ComputeCurrentSourceTreeSha256();
            string artifactHash = RagdollClosurePipeline
                .ComputeSha256(artifactPath);
            string[] verifiedIds = manifest.entries
                .Where(entry => entry.id != "G05" && entry.id != "J06")
                .Select(entry => entry.id).ToArray();
            var artifact = new RagdollEvidenceArtifact
            {
                kind = RagdollEvidenceKind.DocumentationAudit,
                path = artifactPath,
                sha256 = artifactHash,
                platform = "Editor",
                scenario = "ClosureFixture",
                generatedUtc = DateTime.UtcNow.ToString("O"),
                sourceRevision = revision,
                sourceTreeSha256 = sourceHash,
                capabilityIds = verifiedIds,
                validationStatus = "Valid",
                validationReason = string.Empty
            };

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
                entry.requiredEvidenceKinds = new[]
                {
                    nameof(RagdollEvidenceKind.DocumentationAudit)
                };
                entry.evidenceArtifacts = new[] { artifact };
                entry.executionArtifact = artifactPath;
                entry.executionArtifactSha256 = artifactHash;
            }

            manifest.generatedUtc = DateTime.UtcNow.ToString("O");
            manifest.sourceRevision = revision;
            manifest.sourceTreeSha256 = sourceHash;
            manifest.artifacts = new[] { artifact };
            manifest.total = 140;
            manifest.verified = 138;
            manifest.open = 1;
            manifest.notApplicable = 1;
            return manifest;
        }
    }
}
