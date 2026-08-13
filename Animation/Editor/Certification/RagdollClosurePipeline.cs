using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    /// <summary>
    /// Independent second pass for the coverage manifest. This type deliberately
    /// deserializes and audits the provisional JSON; it never calls
    /// RagdollCoverageManifest.Build, so producer and verifier cannot share a
    /// successful in-memory result.
    /// </summary>
    public static class RagdollClosurePipeline
    {
        public const int ValidationSchemaVersion = 1;
        public const int FinalSchemaVersion = 1;

        static readonly Regex CapabilityId = new Regex(
            "^[A-J][0-9]{2}$", RegexOptions.Compiled);
        static readonly Regex Sha256Pattern = new Regex(
            "^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

        [Serializable]
        public sealed class IndependentValidation
        {
            public int schemaVersion;
            public string validator;
            public string generatedUtc;
            public string provisionalManifestPath;
            public string provisionalManifestSha256;
            public string sourceRevision;
            public string sourceTreeSha256;
            public string certificationRunId;
            public int producerProcessId;
            public int validatorProcessId;
            public int total;
            public int verifiedBeforeJ06;
            public int openBeforeJ06;
            public int notApplicable;
            public bool succeeded;
            public string[] errors;
        }

        [Serializable]
        public sealed class FinalManifestEnvelope
        {
            public int schemaVersion;
            public string generatedUtc;
            public string provisionalManifestPath;
            public string provisionalManifestSha256;
            public string independentValidationPath;
            public string independentValidationSha256;
            public int finalizerProcessId;
            public RagdollCoverageManifest.Manifest manifest;
        }

        public static string WriteProvisional(
            RagdollCoverageManifest.Manifest manifest,
            string outputPath)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            string path = PrepareOutputPath(outputPath);
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
            return path;
        }

        public static IndependentValidation ValidateProvisional(
            string provisionalPath,
            string validationOutputPath)
        {
            string provisional = RequireExistingPath(
                provisionalPath, nameof(provisionalPath));
            var errors = new List<string>();
            RagdollCoverageManifest.Manifest manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<RagdollCoverageManifest.Manifest>(
                    File.ReadAllText(provisional));
            }
            catch (Exception exception)
            {
                errors.Add("ManifestJsonInvalid: " + exception.Message);
            }

            if (manifest == null) errors.Add("ManifestJsonDeserializedNull");
            else AuditManifest(manifest, errors);

            var result = new IndependentValidation
            {
                schemaVersion = ValidationSchemaVersion,
                validator = typeof(RagdollClosurePipeline).FullName,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                provisionalManifestPath = provisional,
                provisionalManifestSha256 = ComputeSha256(provisional),
                sourceRevision = manifest?.sourceRevision ?? string.Empty,
                sourceTreeSha256 = manifest?.sourceTreeSha256 ?? string.Empty,
                certificationRunId = manifest?.certificationRunId ?? string.Empty,
                producerProcessId = manifest?.producerProcessId ?? 0,
                validatorProcessId = System.Diagnostics.Process
                    .GetCurrentProcess().Id,
                total = manifest?.entries?.Length ?? 0,
                verifiedBeforeJ06 = manifest?.entries?.Count(
                    entry => entry != null && entry.status == "Verified") ?? 0,
                openBeforeJ06 = manifest?.entries?.Count(
                    entry => entry != null && entry.status == "Open") ?? 0,
                notApplicable = manifest?.entries?.Count(
                    entry => entry != null && entry.status == "N/A") ?? 0,
                succeeded = errors.Count == 0,
                errors = errors.ToArray()
            };

            string output = PrepareOutputPath(validationOutputPath);
            File.WriteAllText(output, JsonUtility.ToJson(result, true));
            return result;
        }

        public static FinalManifestEnvelope FinalizeManifest(
            string provisionalPath,
            string validationPath,
            string finalOutputPath)
        {
            string provisional = RequireExistingPath(
                provisionalPath, nameof(provisionalPath));
            string validationFile = RequireExistingPath(
                validationPath, nameof(validationPath));
            IndependentValidation validation =
                JsonUtility.FromJson<IndependentValidation>(
                    File.ReadAllText(validationFile));
            if (validation == null
                || validation.schemaVersion != ValidationSchemaVersion
                || !validation.succeeded
                || validation.errors == null
                || validation.errors.Length != 0)
            {
                throw new InvalidDataException(
                    "Independent validation did not succeed.");
            }

            string provisionalDigest = ComputeSha256(provisional);
            if (!string.Equals(
                validation.provisionalManifestSha256,
                provisionalDigest,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The provisional manifest digest does not match validation.json.");
            }
            if (!string.Equals(
                Path.GetFullPath(validation.provisionalManifestPath ?? string.Empty),
                provisional,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "validation.json belongs to another provisional manifest path.");
            }

            RagdollCoverageManifest.Manifest manifest =
                JsonUtility.FromJson<RagdollCoverageManifest.Manifest>(
                    File.ReadAllText(provisional));
            if (manifest == null) throw new InvalidDataException(
                "The provisional manifest could not be deserialized.");
            var finalAuditErrors = new List<string>();
            AuditManifest(manifest, finalAuditErrors);
            if (finalAuditErrors.Count != 0)
                throw new InvalidDataException(
                    "The provisional manifest no longer satisfies canonical "
                    + "closure validation: "
                    + string.Join(" | ", finalAuditErrors));
            if (!string.Equals(validation.sourceRevision, manifest.sourceRevision,
                    StringComparison.Ordinal)
                || !string.Equals(validation.sourceTreeSha256,
                    manifest.sourceTreeSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(validation.certificationRunId,
                    manifest.certificationRunId, StringComparison.Ordinal)
                || validation.producerProcessId != manifest.producerProcessId
                || validation.validatorProcessId <= 0
                || validation.validatorProcessId == manifest.producerProcessId
                || validation.validatorProcessId == System.Diagnostics.Process
                    .GetCurrentProcess().Id
                || manifest.producerProcessId == System.Diagnostics.Process
                    .GetCurrentProcess().Id
                || validation.total != RagdollCapabilityCatalog.ExpectedCount
                || validation.verifiedBeforeJ06 != RagdollCapabilityCatalog.ExpectedCount - 2
                || validation.openBeforeJ06 != 1
                || validation.notApplicable != 1)
            {
                throw new InvalidDataException(
                    "Independent validation provenance or pre-final counts changed.");
            }

            RagdollCoverageManifest.Entry j06 = manifest.entries.SingleOrDefault(
                entry => entry != null && entry.id == "J06");
            if (j06 == null || j06.status != "Open")
                throw new InvalidDataException(
                    "J06 must be the single open provisional capability.");

            string validationDigest = ComputeSha256(validationFile);
            var validationArtifact = new RagdollEvidenceArtifact
            {
                kind = RagdollEvidenceKind.IndependentValidation,
                path = validationFile,
                sha256 = validationDigest,
                platform = "Editor",
                scenario = "J06",
                generatedUtc = validation.generatedUtc,
                certificationRunId = manifest.certificationRunId,
                sourceRevision = manifest.sourceRevision,
                sourceTreeSha256 = manifest.sourceTreeSha256,
                capabilityIds = new[] { "J06" },
                validationStatus = "Valid",
                validationReason = string.Empty
            };
            j06.requiredEvidenceKinds = new[]
            {
                nameof(RagdollEvidenceKind.IndependentValidation)
            };
            j06.evidenceArtifacts = new[] { validationArtifact };
            j06.executionArtifact = validationFile;
            j06.executionArtifactSha256 = validationDigest;
            j06.status = "Verified";
            j06.reason = string.Empty;
            manifest.artifacts = (manifest.artifacts
                ?? Array.Empty<RagdollEvidenceArtifact>())
                .Concat(new[] { validationArtifact }).ToArray();
            manifest.verified = RagdollCapabilityCatalog.ExpectedCount - 1;
            manifest.open = 0;
            manifest.notApplicable = 1;
            manifest.total = RagdollCapabilityCatalog.ExpectedCount;
            manifest.generatedUtc = DateTime.UtcNow.ToString("O");

            var envelope = new FinalManifestEnvelope
            {
                schemaVersion = FinalSchemaVersion,
                generatedUtc = manifest.generatedUtc,
                provisionalManifestPath = provisional,
                provisionalManifestSha256 = provisionalDigest,
                independentValidationPath = validationFile,
                independentValidationSha256 = validationDigest,
                finalizerProcessId = System.Diagnostics.Process
                    .GetCurrentProcess().Id,
                manifest = manifest
            };
            string output = PrepareOutputPath(finalOutputPath);
            File.WriteAllText(output, JsonUtility.ToJson(envelope, true));
            return envelope;
        }

        static void AuditManifest(
            RagdollCoverageManifest.Manifest manifest,
            List<string> errors)
        {
            RagdollCoverageManifest.Entry[] entries = manifest.entries;
            if (entries == null || entries.Length != RagdollCapabilityCatalog.ExpectedCount)
            {
                errors.Add("EntryCountMustMatchCatalog");
                return;
            }

            if (!string.Equals(
                manifest.matrix,
                typeof(RagdollCapabilityCatalog).FullName,
                StringComparison.Ordinal))
                errors.Add("CatalogIdentityMismatch");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RagdollCoverageManifest.Entry entry in entries)
            {
                if (entry == null) { errors.Add("NullEntry"); continue; }
                if (!CapabilityId.IsMatch(entry.id ?? string.Empty))
                    errors.Add("InvalidCapabilityId:" + entry.id);
                else if (!ids.Add(entry.id))
                    errors.Add("DuplicateCapabilityId:" + entry.id);
                if (!HasOfficialSource(entry.source))
                    errors.Add("MissingOfficialSource:" + entry.id);
                if (string.IsNullOrWhiteSpace(entry.affectedApi))
                    errors.Add("MissingRequirementOrApi:" + entry.id);
                RagdollCapabilityContract contract;
                if (!RagdollCapabilityCatalog.TryGet(entry.id, out contract))
                {
                    errors.Add("UnknownCatalogCapability:" + entry.id);
                    continue;
                }
                AuditEntry(manifest, entry, contract, errors);
            }


            foreach (RagdollCapabilityContract contract in
                RagdollCapabilityCatalog.Contracts)
                if (!ids.Contains(contract.Id))
                    errors.Add("CatalogCapabilityMissing:" + contract.Id);

            int verified = entries.Count(value => value != null
                && value.status == "Verified");
            int open = entries.Count(value => value != null
                && value.status == "Open");
            int notApplicable = entries.Count(value => value != null
                && value.status == "N/A");
            if (manifest.total != RagdollCapabilityCatalog.ExpectedCount || manifest.verified != verified
                || manifest.open != open || manifest.notApplicable != notApplicable)
                errors.Add("DeclaredCountsDoNotMatchEntries");
            if (verified != RagdollCapabilityCatalog.ExpectedCount - 2
                || open != 1 || notApplicable != 1)
                errors.Add("ProvisionalCountsMustMatchCatalog");
            if (entries.Count(value => value != null && value.id == "J06"
                && value.status == "Open") != 1)
                errors.Add("J06MustBeTheSingleOpenEntry");
            if (entries.Count(value => value != null && value.id == "G05"
                && value.status == "N/A") != 1
                || entries.Any(value => value != null && value.id != "G05"
                    && value.status == "N/A"))
                errors.Add("G05MustBeTheOnlyNotApplicableEntry");

            if (string.IsNullOrWhiteSpace(manifest.sourceRevision))
                errors.Add("MissingSourceRevision");
            Guid runId;
            if (!Guid.TryParse(manifest.certificationRunId, out runId))
                errors.Add("CertificationRunIdInvalid");
            if (manifest.producerProcessId <= 0)
                errors.Add("ProducerProcessIdInvalid");
            else if (manifest.producerProcessId == System.Diagnostics.Process
                .GetCurrentProcess().Id)
                errors.Add("IndependentValidatorMustUseAnotherProcess");
            if (!IsSha256(manifest.sourceTreeSha256))
                errors.Add("InvalidSourceTreeSha256");
            else
            {
                string current = RagdollCoverageManifest
                    .ComputeCurrentSourceTreeSha256();
                if (!string.Equals(current, manifest.sourceTreeSha256,
                    StringComparison.OrdinalIgnoreCase))
                    errors.Add("SourceTreeSha256DoesNotMatchCurrentPackage");
                if (string.IsNullOrWhiteSpace(manifest.sourceRevision)
                    || !manifest.sourceRevision.EndsWith(
                        "tree-" + manifest.sourceTreeSha256,
                        StringComparison.OrdinalIgnoreCase))
                    errors.Add("SourceRevisionDoesNotBindSourceTree");
            }

            var artifactIdentities = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (RagdollEvidenceArtifact artifact in manifest.artifacts
                ?? Array.Empty<RagdollEvidenceArtifact>())
            {
                AuditArtifact(manifest, null, artifact, errors);
                if (artifact != null
                    && !string.IsNullOrWhiteSpace(artifact.path)
                    && !artifactIdentities.Add(
                    artifact.kind + "|" + Path.GetFullPath(
                        artifact.path)))
                    errors.Add("DuplicateManifestArtifact:" + artifact.kind);
            }
        }

        static void AuditEntry(
            RagdollCoverageManifest.Manifest manifest,
            RagdollCoverageManifest.Entry entry,
            RagdollCapabilityContract contract,
            List<string> errors)
        {
            AuditCanonicalContract(entry, contract, errors);
            if (entry.status != "Verified" && entry.status != "Open"
                && entry.status != "N/A")
                errors.Add("InvalidStatus:" + entry.id);
            string[] requirements = entry.requiredEvidenceKinds;
            if (requirements == null)
            {
                errors.Add("NullEvidenceRequirements:" + entry.id);
                requirements = Array.Empty<string>();
            }
            foreach (string requirement in requirements)
            {
                RagdollEvidenceKind ignored;
                if (!Enum.TryParse(requirement, out ignored))
                    errors.Add("UnknownEvidenceRequirement:" + entry.id + ":" + requirement);
            }

            if (contract.IsApplicable)
                AuditStableIdTest(manifest, entry, errors);

            if (entry.id == "J06" && entry.status == "Open")
            {
                if (!requirements.Contains(
                    nameof(RagdollEvidenceKind.IndependentValidation)))
                    errors.Add("J06MustRequireIndependentValidation");
                return;
            }
            if (entry.status != "Verified") return;
            if (requirements.Length == 0)
                errors.Add("VerifiedEntryHasNoEvidenceRequirement:" + entry.id);
            RagdollEvidenceArtifact[] artifacts = entry.evidenceArtifacts
                ?? Array.Empty<RagdollEvidenceArtifact>();
            foreach (string requirement in requirements)
            {
                int matches = artifacts.Count(artifact => artifact != null
                    && artifact.kind.ToString() == requirement);
                if (matches == 0)
                    errors.Add("RequiredEvidenceMissing:" + entry.id + ":" + requirement);
                else if (matches != 1)
                    errors.Add("RequiredEvidenceDuplicate:" + entry.id + ":" + requirement);
            }
            foreach (RagdollEvidenceArtifact artifact in artifacts)
            {
                AuditArtifact(manifest, entry.id, artifact, errors);
                if (artifact == null) continue;
                if (!requirements.Contains(artifact.kind.ToString()))
                    errors.Add("UnexpectedEvidenceKind:" + entry.id + ":"
                        + artifact.kind);
                if (!ManifestContainsArtifact(manifest, artifact))
                    errors.Add("EntryArtifactNotInManifest:" + entry.id + ":"
                        + artifact.kind);
                RagdollArtifactValidationResult validation =
                    ValidateArtifactContent(entry, contract, artifact);
                if (!validation.IsValid)
                    errors.Add("ArtifactContentInvalid:" + entry.id + ":"
                        + artifact.kind + ":" + validation.Reason);
            }

            if (string.IsNullOrWhiteSpace(entry.executionArtifact)
                || !File.Exists(entry.executionArtifact))
                errors.Add("ExecutionArtifactMissing:" + entry.id);
            else if (!IsSha256(entry.executionArtifactSha256)
                || !string.Equals(ComputeSha256(entry.executionArtifact),
                    entry.executionArtifactSha256,
                    StringComparison.OrdinalIgnoreCase))
                errors.Add("ExecutionArtifactHashMismatch:" + entry.id);
            else if (!artifacts.Any(artifact => artifact != null
                && !string.IsNullOrWhiteSpace(artifact.path)
                && string.Equals(Path.GetFullPath(artifact.path),
                    Path.GetFullPath(entry.executionArtifact),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(artifact.sha256,
                    entry.executionArtifactSha256,
                    StringComparison.OrdinalIgnoreCase)))
                errors.Add("ExecutionArtifactNotDeclared:" + entry.id);
        }

        static void AuditCanonicalContract(
            RagdollCoverageManifest.Entry entry,
            RagdollCapabilityContract contract,
            List<string> errors)
        {
            if (!string.Equals(entry.source, contract.OfficialSource,
                    StringComparison.Ordinal))
                errors.Add("CatalogSourceMismatch:" + entry.id);
            if (!string.Equals(entry.sourceLocator, contract.SourceLocator,
                    StringComparison.Ordinal))
                errors.Add("CatalogLocatorMismatch:" + entry.id);
            if (!string.Equals(entry.observableClaim, contract.ObservableClaim,
                    StringComparison.Ordinal))
                errors.Add("CatalogClaimMismatch:" + entry.id);
            if (!string.Equals(entry.affectedApis, contract.AffectedApis,
                    StringComparison.Ordinal))
                errors.Add("CatalogApisMismatch:" + entry.id);
            if (!string.Equals(entry.affectedApi, contract.AffectedApis,
                    StringComparison.Ordinal))
                errors.Add("CatalogAffectedApiMismatch:" + entry.id);
            string[] actual = entry.requiredEvidenceKinds
                ?? Array.Empty<string>();
            string[] expected = contract.RequiredEvidence
                .Select(kind => kind.ToString()).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
                errors.Add("CatalogEvidenceRequirementsMismatch:" + entry.id);

            if (!contract.IsApplicable)
            {
                if (entry.status != "N/A"
                    || !string.Equals(entry.reason, contract.ExclusionReason,
                        StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(entry.exactTest))
                    errors.Add("CatalogExclusionMismatch:" + entry.id);
                return;
            }
            string expectedStatus = entry.id == "J06" ? "Open" : "Verified";
            if (!string.Equals(entry.status, expectedStatus,
                    StringComparison.Ordinal))
                errors.Add("CatalogStatusMismatch:" + entry.id);
        }

        static void AuditStableIdTest(
            RagdollCoverageManifest.Manifest manifest,
            RagdollCoverageManifest.Entry entry,
            List<string> errors)
        {
            string exact = entry.exactTest ?? string.Empty;
            string method = exact.LastIndexOf('.') >= 0
                ? exact.Substring(exact.LastIndexOf('.') + 1)
                : exact;
            if (string.IsNullOrWhiteSpace(exact)
                || !method.StartsWith(entry.id + "_", StringComparison.Ordinal))
            {
                errors.Add("StableIdTestMissing:" + entry.id);
                return;
            }
            RagdollEvidenceKind kind;
            if (string.Equals(entry.testKind, "EditMode", StringComparison.Ordinal))
                kind = RagdollEvidenceKind.NUnitEditMode;
            else if (!string.IsNullOrEmpty(entry.testKind)
                && entry.testKind.StartsWith("PlayMode/", StringComparison.Ordinal))
                kind = RagdollEvidenceKind.NUnitPlayMode;
            else
            {
                errors.Add("StableIdTestKindInvalid:" + entry.id);
                return;
            }
            RagdollEvidenceArtifact[] candidates = (manifest.artifacts
                ?? Array.Empty<RagdollEvidenceArtifact>())
                .Where(artifact => artifact != null && artifact.kind == kind)
                .ToArray();
            if (!candidates.Any(artifact =>
                RagdollEvidenceArtifactValidators.Validate(
                    artifact,
                    new RagdollArtifactValidationContext
                    {
                        ExactTestName = exact
                    }).IsValid))
                errors.Add("StableIdTestNotExecuted:" + entry.id);
        }

        static RagdollArtifactValidationResult ValidateArtifactContent(
            RagdollCoverageManifest.Entry entry,
            RagdollCapabilityContract contract,
            RagdollEvidenceArtifact artifact)
        {
            string exact = null;
            if (artifact.kind == RagdollEvidenceKind.NUnitEditMode
                || artifact.kind == RagdollEvidenceKind.NUnitPlayMode)
            {
                if (!contract.ExactNUnitEvidenceTests.TryGetValue(
                    artifact.kind, out exact))
                {
                    bool primary = artifact.kind == RagdollEvidenceKind.NUnitEditMode
                        ? string.Equals(entry.testKind, "EditMode",
                            StringComparison.Ordinal)
                        : !string.IsNullOrEmpty(entry.testKind)
                            && entry.testKind.StartsWith("PlayMode/",
                                StringComparison.Ordinal);
                    if (primary) exact = entry.exactTest;
                }
            }
            return RagdollEvidenceArtifactValidators.Validate(
                artifact,
                new RagdollArtifactValidationContext
                {
                    ExactTestName = exact,
                    ExpectedCapabilityId = entry.id,
                    ExpectedCertificationRunId = artifact.certificationRunId,
                    ExpectedSourceRevision = artifact.sourceRevision,
                    ExpectedSourceTreeSha256 = artifact.sourceTreeSha256,
                    ExpectedPlatform = artifact.kind ==
                        RagdollEvidenceKind.WindowsPlayerScenario
                            ? "Windows64"
                            : artifact.kind ==
                                RagdollEvidenceKind.LinuxPlayerScenario
                                ? "Linux64"
                                : string.Empty
                });
        }

        static bool ManifestContainsArtifact(
            RagdollCoverageManifest.Manifest manifest,
            RagdollEvidenceArtifact expected)
        {
            if (expected == null || string.IsNullOrWhiteSpace(expected.path))
                return false;
            return (manifest.artifacts ?? Array.Empty<RagdollEvidenceArtifact>())
                .Any(artifact => artifact != null
                    && !string.IsNullOrWhiteSpace(artifact.path)
                    && artifact.kind == expected.kind
                    && string.Equals(Path.GetFullPath(artifact.path),
                        Path.GetFullPath(expected.path),
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(artifact.sha256, expected.sha256,
                        StringComparison.OrdinalIgnoreCase));
        }

        static void AuditArtifact(
            RagdollCoverageManifest.Manifest manifest,
            string entryId,
            RagdollEvidenceArtifact artifact,
            List<string> errors)
        {
            string label = entryId ?? "manifest";
            if (artifact == null) { errors.Add("NullArtifact:" + label); return; }
            if (!Enum.IsDefined(typeof(RagdollEvidenceKind), artifact.kind))
                errors.Add("UnknownArtifactKind:" + label);
            if (string.IsNullOrWhiteSpace(artifact.path)
                || !File.Exists(artifact.path))
                errors.Add("ArtifactMissing:" + label);
            else if (!IsSha256(artifact.sha256)
                || !string.Equals(ComputeSha256(artifact.path), artifact.sha256,
                    StringComparison.OrdinalIgnoreCase))
                errors.Add("ArtifactHashMismatch:" + label);
            if (!string.Equals(artifact.sourceRevision, manifest.sourceRevision,
                StringComparison.Ordinal)
                || !string.Equals(artifact.sourceTreeSha256,
                    manifest.sourceTreeSha256, StringComparison.OrdinalIgnoreCase))
                errors.Add("ArtifactProvenanceMismatch:" + label);
            if (!string.Equals(artifact.certificationRunId,
                manifest.certificationRunId, StringComparison.Ordinal))
                errors.Add("ArtifactRunIdMismatch:" + label);
            if (!string.Equals(artifact.validationStatus, "Valid",
                StringComparison.Ordinal))
                errors.Add("ArtifactNotValidated:" + label);
            if (entryId != null
                && artifact.kind != RagdollEvidenceKind.NUnitEditMode
                && artifact.kind != RagdollEvidenceKind.NUnitPlayMode
                && (artifact.capabilityIds == null
                    || !artifact.capabilityIds.Contains(entryId,
                        StringComparer.Ordinal)))
                errors.Add("ArtifactCapabilityMismatch:" + label);
            DateTime generated;
            if (!DateTime.TryParse(artifact.generatedUtc, out generated))
                errors.Add("ArtifactGeneratedUtcInvalid:" + label);
            else
            {
                DateTime latestSource;
                if (DateTime.TryParse(
                    RagdollCoverageManifest.CurrentSourceLatestWriteUtc(),
                    out latestSource)
                    && generated.ToUniversalTime() < latestSource.ToUniversalTime())
                    errors.Add("ArtifactStale:" + label);
                if (generated.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5d))
                    errors.Add("ArtifactGeneratedUtcFuture:" + label);
            }
        }

        static bool HasOfficialSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return false;
            string[] parts = source.Split(new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries);
            return parts.Length != 0 && parts.All(part =>
            {
                Uri uri;
                if (!Uri.TryCreate(part.Trim(), UriKind.Absolute, out uri))
                    return false;
                return string.Equals(uri.Host, "www.root-motion.com",
                           StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "root-motion.com",
                           StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "docs.unity3d.com",
                           StringComparison.OrdinalIgnoreCase);
            });
        }

        static string RequireExistingPath(string path, string parameter)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", parameter);
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
                throw new FileNotFoundException("The required artifact was not found.", full);
            return full;
        }

        static string PrepareOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("An output path is required.", nameof(path));
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            return full;
        }

        static bool IsSha256(string value)
        {
            return !string.IsNullOrEmpty(value) && Sha256Pattern.IsMatch(value);
        }

        internal static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
