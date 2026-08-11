using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    /// <summary>
    /// Expected observable evidence for one artifact. Capability identifiers are
    /// labels only; validation is based on executed tests, scenarios and metrics.
    /// </summary>
    public sealed class RagdollArtifactValidationContext
    {
        public string ExactTestName { get; set; }
        public int ExpectedParameterizedCases { get; set; }
        public string ExpectedPlatform { get; set; }
        public string[] ExpectedScenarios { get; set; } = Array.Empty<string>();
        public string ExpectedCapabilityId { get; set; }
        public string ExpectedCertificationRunId { get; set; }
        public string ExpectedSourceRevision { get; set; }
        public string ExpectedSourceTreeSha256 { get; set; }
    }

    public sealed class RagdollArtifactValidationResult
    {
        RagdollArtifactValidationResult(bool valid, string reason)
        {
            IsValid = valid;
            Reason = reason ?? string.Empty;
        }

        public bool IsValid { get; }
        public string Reason { get; }

        public static RagdollArtifactValidationResult Valid()
        {
            return new RagdollArtifactValidationResult(true, string.Empty);
        }

        public static RagdollArtifactValidationResult Invalid(string reason)
        {
            return new RagdollArtifactValidationResult(false, reason);
        }
    }

    /// <summary>
    /// Content validators for closure artifacts. The JSON contracts deliberately
    /// contain observable results instead of accepting capabilityIds as proof.
    /// </summary>
    public static class RagdollEvidenceArtifactValidators
    {
        static readonly string[] RequiredPlayerScenarios =
        {
            "CoreLifecycle", "HumanoidBakerFall",
            "HierarchyProps", "CollisionsPerformance"
        };

        static readonly Dictionary<string, string[]> RequiredScenarioAssertions =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "CoreLifecycle", new[]
                    {
                        "core.physx-fall-distance",
                        "core.saturated-contact-count",
                        "core.respawn-position-error",
                        "core.manual-simulation-completed",
                        "core.joint-break-irreversible"
                    }
                },
                { "HumanoidBakerFall", new[]
                    {
                        "humanoid.valid-avatar-count",
                        "humanoid.fall-initialized",
                        "humanoid.ik-owned-solver-count",
                        "humanoid.animation-event-count",
                        "humanoid.root-motion-distance",
                        "humanoid.baker-clip-length"
                    }
                },
                { "HierarchyProps", new[]
                    {
                        "props.pickup-held",
                        "props.collection-held",
                        "props.additional-pin-preserved",
                        "props.rollback-exact",
                        "props.drop-empty"
                    }
                },
                { "CollisionsPerformance", new[]
                    {
                        "performance.flatten-hierarchy",
                        "performance.tree-hierarchy"
                    }
                }
            };

        static readonly string[] RequiredCriticalPaths =
        {
            "matching", "mapping", "collision-relay", "com",
            "additional-pin", "baker-realtime"
        };
        static readonly Dictionary<string, string> RequiredCriticalPathScopes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "matching", "RagdollAnimator.DoAnimationMatching" },
                { "mapping", "RagdollAnimator.MapRagdollToTarget" },
                { "collision-relay", "RagdollCollisionHub.Dispatch" },
                { "com", "RagdollCenterOfMassSubBehaviour.FixedUpdate" },
                { "additional-pin", "RagdollPropMuscle.ApplyAdditionalPinAfterAnimationMatching" },
                { "baker-realtime", "RagdollBaker.AdvanceRealtimeSampling" }
            };

        static readonly string[] RequiredBuildTargets =
        {
            "Windows64", "Linux64", "macOS", "WebGL"
        };

        public static RagdollArtifactValidationResult Validate(
            RagdollEvidenceArtifact artifact,
            RagdollArtifactValidationContext context = null)
        {
            if (artifact == null)
                return Invalid("ArtifactNull");
            if (string.IsNullOrWhiteSpace(artifact.path)
                || !File.Exists(artifact.path))
                return Invalid("ArtifactMissing");

            context = context ?? new RagdollArtifactValidationContext();
            try
            {
                if (artifact.kind != RagdollEvidenceKind.NUnitEditMode
                    && artifact.kind != RagdollEvidenceKind.NUnitPlayMode)
                {
                    RagdollArtifactValidationResult provenance =
                        ValidateEmbeddedProvenance(artifact.path, context);
                    if (!provenance.IsValid) return provenance;
                }
                switch (artifact.kind)
                {
                    case RagdollEvidenceKind.NUnitEditMode:
                    case RagdollEvidenceKind.NUnitPlayMode:
                        return ValidateNUnit(artifact.path, context);
                    case RagdollEvidenceKind.WindowsPlayerScenario:
                        return ValidatePlayer(
                            artifact.path, "Windows64", context);
                    case RagdollEvidenceKind.LinuxPlayerScenario:
                        return ValidatePlayer(
                            artifact.path, "Linux64", context);
                    case RagdollEvidenceKind.ProfilerResult:
                        return ValidateProfiler(artifact.path);
                    case RagdollEvidenceKind.BuildReport:
                        return ValidateBuildReport(artifact.path);
                    case RagdollEvidenceKind.SceneArtifact:
                        return ValidateScenes(artifact.path, context);
                    case RagdollEvidenceKind.DocumentationAudit:
                        return ValidateDocumentationAudit(artifact.path, context);
                    default:
                        return Invalid("UnsupportedArtifactKind:" + artifact.kind);
                }
            }
            catch (Exception exception)
            {
                return Invalid("MalformedArtifact:" + exception.GetType().Name);
            }
        }

        public static RagdollArtifactValidationResult ValidateNUnit(
            string path,
            RagdollArtifactValidationContext context)
        {
            XDocument document = XDocument.Load(path);
            XElement run = document.Root;
            if (run == null || run.Name.LocalName != "test-run")
                return Invalid("NUnitRootMissing");
            if (!IsPassed(run)) return Invalid("NUnitRunNotPassed");

            XElement[] cases = run.Descendants("test-case").ToArray();
            if (cases.Length == 0) return Invalid("NUnitContainsNoCases");
            XElement incomplete = cases.FirstOrDefault(test => !IsPassed(test));
            if (incomplete != null)
                return Invalid("NUnitNonPassingCase:" + CaseName(incomplete));

            foreach (string zeroCounter in new[]
                     { "failed", "skipped", "inconclusive", "warnings" })
            {
                int value;
                string raw = (string)run.Attribute(zeroCounter);
                if (!string.IsNullOrEmpty(raw)
                    && (!int.TryParse(raw, out value) || value != 0))
                    return Invalid("NUnitCounterNotZero:" + zeroCounter);
            }

            string exact = context?.ExactTestName;
            if (string.IsNullOrWhiteSpace(exact))
                return RagdollArtifactValidationResult.Valid();
            XElement[] matching = cases.Where(test =>
            {
                string fullName = (string)test.Attribute("fullname") ?? string.Empty;
                return string.Equals(fullName, exact, StringComparison.Ordinal)
                    || fullName.StartsWith(exact + "(", StringComparison.Ordinal)
                    || fullName.StartsWith(exact + "[", StringComparison.Ordinal);
            }).ToArray();
            if (matching.Length == 0) return Invalid("NUnitExactTestMissing");
            if (context.ExpectedParameterizedCases > 0
                && matching.Length != context.ExpectedParameterizedCases)
                return Invalid("NUnitParameterizedCaseCountMismatch");
            if (matching.Any(test => !IsPassed(test)))
                return Invalid("NUnitExactTestNotPassed");
            return RagdollArtifactValidationResult.Valid();
        }

        public static RagdollArtifactValidationResult ValidatePlayer(
            string path,
            string defaultPlatform,
            RagdollArtifactValidationContext context)
        {
            PlayerEvidence evidence = ReadJson<PlayerEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 3)
                return Invalid("PlayerSchemaInvalid");
            if (!evidence.succeeded) return Invalid("PlayerRunFailed");
            string expectedPlatform = string.IsNullOrWhiteSpace(context?.ExpectedPlatform)
                ? defaultPlatform
                : context.ExpectedPlatform;
            if (!string.Equals(
                evidence.platform, expectedPlatform, StringComparison.OrdinalIgnoreCase))
                return Invalid("PlayerPlatformMismatch");
            string[] expected = HasItems(context?.ExpectedScenarios)
                ? context.ExpectedScenarios
                : RequiredPlayerScenarios;
            RagdollArtifactValidationResult scenarios =
                ValidateExecutedScenarios(evidence.scenarios, expected, "Player");
            if (!scenarios.IsValid) return scenarios;
            if (string.Equals(context?.ExpectedCapabilityId, "H05",
                    StringComparison.Ordinal)
                || string.Equals(context?.ExpectedCapabilityId, "H08",
                    StringComparison.Ordinal))
                return ValidatePerformanceMatrix(evidence.scenarios);
            return RagdollArtifactValidationResult.Valid();
        }

        static RagdollArtifactValidationResult ValidatePerformanceMatrix(
            ExecutedScenario[] scenarios)
        {
            ExecutedScenario[] performanceScenarios = scenarios.Where(value =>
                value != null && string.Equals(value.name,
                    "CollisionsPerformance", StringComparison.Ordinal)).ToArray();
            if (performanceScenarios.Length != 1)
                return Invalid("PlayerPerformanceScenarioMissingOrDuplicate");
            ExecutedScenario scenario = performanceScenarios[0];
            if (scenario.performance == null)
                return Invalid("PlayerPerformanceMatrixMissing");
            int[] populations = { 1, 10, 25, 50 };
            string[] modes =
            {
                "ActiveTree", "ActiveFlat", "Kinematic", "Disabled"
            };
            foreach (int population in populations)
            foreach (string mode in modes)
            {
                PerformanceEvidence[] matches = scenario.performance.Where(value =>
                    value != null && value.puppets == population
                    && string.Equals(value.mode, mode,
                        StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1)
                    return Invalid("PlayerPerformanceCellMissingOrDuplicate:"
                        + population + ":" + mode);
                PerformanceEvidence cell = matches[0];
                if (cell.cpuMedianNanoseconds < 0L
                    || cell.cpuP95Nanoseconds < cell.cpuMedianNanoseconds
                    || cell.memoryMedianBytes < 0L
                    || cell.memoryP95Bytes < cell.memoryMedianBytes)
                    return Invalid("PlayerPerformanceDistributionInvalid:"
                        + population + ":" + mode);
                if (cell.measuredFrames < 600)
                    return Invalid("PlayerPerformanceSampleCountInvalid:"
                        + population + ":" + mode);
                if (cell.maximumGcAllocatedInFrame < 0L)
                    return Invalid("PlayerPerformanceAmbientGcInvalid:"
                        + population + ":" + mode);
            }
            bool flatAsserted = scenario.assertions.Any(assertion =>
                assertion != null && assertion.succeeded
                && string.Equals(assertion.id,
                    "performance.flatten-hierarchy", StringComparison.Ordinal)
                && MetricProvesAssertion(assertion));
            bool treeAsserted = scenario.assertions.Any(assertion =>
                assertion != null && assertion.succeeded
                && string.Equals(assertion.id,
                    "performance.tree-hierarchy", StringComparison.Ordinal)
                && MetricProvesAssertion(assertion));
            return flatAsserted && treeAsserted
                ? RagdollArtifactValidationResult.Valid()
                : Invalid("PlayerHierarchyModesNotAsserted");
        }

        public static RagdollArtifactValidationResult ValidateProfiler(string path)
        {
            ProfilerEvidence evidence = ReadJson<ProfilerEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 3)
                return Invalid("ProfilerSchemaInvalid");
            if (!evidence.succeeded) return Invalid("ProfilerRunFailed");
            if (evidence.warmupFrames != 120)
                return Invalid("ProfilerWarmupFrameCountInvalid");
            if (evidence.measuredFrames != 600)
                return Invalid("ProfilerMeasurementFrameCountInvalid");
            RagdollArtifactValidationResult cpu = ValidateDistribution(
                evidence.cpuMilliseconds, "Cpu", evidence.measuredFrames);
            if (!cpu.IsValid) return cpu;
            RagdollArtifactValidationResult memory = ValidateDistribution(
                evidence.memoryBytes, "Memory", evidence.measuredFrames);
            if (!memory.IsValid) return memory;
            if (evidence.criticalPaths == null)
                return Invalid("ProfilerCriticalPathsMissing");
            var measurementScopes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string required in RequiredCriticalPaths)
            {
                CriticalPathEvidence[] matching = evidence.criticalPaths.Where(pathResult =>
                    pathResult != null && string.Equals(
                        Normalize(pathResult.name), required,
                        StringComparison.Ordinal)).ToArray();
                if (matching.Length != 1)
                    return Invalid("ProfilerCriticalPathMissingOrDuplicate:" + required);
                CriticalPathEvidence result = matching[0];
                if (!result.succeeded || result.samples != evidence.measuredFrames)
                    return Invalid("ProfilerCriticalPathIncomplete:" + required);
                if (string.IsNullOrWhiteSpace(result.measurementScope))
                    return Invalid("ProfilerCriticalPathScopeMissing:" + required);
                if (!measurementScopes.Add(result.measurementScope))
                    return Invalid("ProfilerCriticalPathScopeReused:"
                        + result.measurementScope);
                if (!string.Equals(result.measurementScope,
                    RequiredCriticalPathScopes[required],
                    StringComparison.Ordinal))
                    return Invalid("ProfilerCriticalPathScopeMismatch:" + required);
                if (result.gcAllocatedBytes != 0
                    || result.maxGcAllocatedBytesInFrame != 0)
                    return Invalid("ProfilerCriticalPathAllocated:" + required);
                if (result.rawAllocationSamples == null
                    || result.rawAllocationSamples.Length != evidence.measuredFrames)
                    return Invalid("ProfilerCriticalPathRawSamplesMissing:" + required);
                long total = 0L;
                long maximum = 0L;
                for (int index = 0; index < result.rawAllocationSamples.Length; index++)
                {
                    long sample = result.rawAllocationSamples[index];
                    if (sample < 0L)
                        return Invalid("ProfilerCriticalPathRawSampleInvalid:" + required);
                    total += sample;
                    if (sample > maximum) maximum = sample;
                }
                if (total != result.gcAllocatedBytes
                    || maximum != result.maxGcAllocatedBytesInFrame)
                    return Invalid("ProfilerCriticalPathAggregateMismatch:" + required);
            }
            return RagdollArtifactValidationResult.Valid();
        }

        public static RagdollArtifactValidationResult ValidateBuildReport(string path)
        {
            BuildEvidence evidence = ReadJson<BuildEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 3)
                return Invalid("BuildSchemaInvalid");
            if (evidence.builds == null)
                return Invalid("BuildEntriesMissing");
            foreach (string target in RequiredBuildTargets)
            {
                BuildEntry[] matching = evidence.builds.Where(build => build != null
                    && string.Equals(build.target, target,
                        StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matching.Length != 1)
                    return Invalid("BuildTargetMissingOrDuplicate:" + target);
                BuildEntry build = matching[0];
                if (!build.succeeded
                    || !string.Equals(build.result, "Succeeded",
                        StringComparison.OrdinalIgnoreCase))
                    return Invalid("BuildFailed:" + target);
                if (!build.development || !build.allowDebugging)
                    return Invalid("BuildOptionsInvalid:" + target);
                if (!build.outputExists)
                    return Invalid("BuildOutputMissing:" + target);
                if (string.IsNullOrWhiteSpace(build.output)
                    || (!File.Exists(build.output)
                        && !Directory.Exists(build.output)))
                    return Invalid("BuildOutputNotFound:" + target);
                if (build.stepsScanned <= 0 || build.reportSteps == null
                    || build.reportSteps.Length != build.stepsScanned)
                    return Invalid("BuildReportTraversalMissing:" + target);
                var traversal = new StringBuilder();
                int messagesScanned = 0;
                for (int stepIndex = 0; stepIndex < build.reportSteps.Length;
                    stepIndex++)
                {
                    BuildTraversalStep step = build.reportSteps[stepIndex];
                    if (step == null || step.messages == null)
                        return Invalid("BuildReportTraversalInvalid:" + target);
                    traversal.Append(stepIndex).Append('|')
                        .Append(step.name ?? string.Empty).Append('|')
                        .Append(step.messages.Length).Append('\n');
                    for (int messageIndex = 0;
                        messageIndex < step.messages.Length; messageIndex++)
                    {
                        BuildTraversalMessage message = step.messages[messageIndex];
                        if (message == null)
                            return Invalid("BuildReportTraversalInvalid:" + target);
                        messagesScanned++;
                        traversal.Append(messageIndex).Append('|')
                            .Append(message.severity ?? string.Empty).Append('|')
                            .Append(message.message ?? string.Empty).Append('\n');
                    }
                }
                if (messagesScanned != build.messagesScanned
                    || !string.Equals(Sha256(traversal.ToString()),
                        build.reportTraversalSha256,
                        StringComparison.OrdinalIgnoreCase))
                    return Invalid("BuildReportTraversalDigestMismatch:" + target);
                if (build.diagnostics == null)
                    return Invalid("BuildDiagnosticsMissing:" + target);
                BuildTraversalMessage[] rawDiagnostics = build.reportSteps
                    .SelectMany(step => step.messages)
                    .Where(message => IsBuildProblemSeverity(message.severity))
                    .ToArray();
                if (rawDiagnostics.Length != build.diagnostics.Length)
                    return Invalid("BuildDiagnosticsTraversalMismatch:" + target);
                for (int diagnosticIndex = 0;
                    diagnosticIndex < rawDiagnostics.Length; diagnosticIndex++)
                {
                    BuildTraversalMessage raw = rawDiagnostics[diagnosticIndex];
                    bool expectedOwn = HairibarCertification
                        .IsHairibarBuildDiagnostic(raw.message);
                    BuildDiagnostic diagnostic = build.diagnostics[diagnosticIndex];
                    if (diagnostic == null
                        || !string.Equals(diagnostic.severity, raw.severity,
                            StringComparison.Ordinal)
                        || !string.Equals(diagnostic.message, raw.message,
                            StringComparison.Ordinal)
                        || diagnostic.own != expectedOwn)
                        return Invalid("BuildDiagnosticClassificationMismatch:"
                            + target);
                }
                BuildDiagnostic ownProblem = build.diagnostics.FirstOrDefault(message =>
                    message != null && message.own
                    && (string.Equals(message.severity, "Error",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(message.severity, "Warning",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(message.severity, "Exception",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(message.severity, "Assert",
                            StringComparison.OrdinalIgnoreCase)));
                if (ownProblem != null)
                    return Invalid("BuildOwnDiagnostic:" + target);
            }
            return RagdollArtifactValidationResult.Valid();
        }

        static bool IsBuildProblemSeverity(string severity)
        {
            return string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "Exception", StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "Assert", StringComparison.OrdinalIgnoreCase);
        }

        static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty))
                    .Select(item => item.ToString("x2")));
        }

        public static RagdollArtifactValidationResult ValidateScenes(
            string path,
            RagdollArtifactValidationContext context)
        {
            SceneEvidence evidence = ReadJson<SceneEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 3)
                return Invalid("SceneSchemaInvalid");
            if (!evidence.succeeded) return Invalid("SceneRunFailed");
            string[] expected = HasItems(context?.ExpectedScenarios)
                ? context.ExpectedScenarios
                : RequiredPlayerScenarios;
            return ValidateExecutedScenarios(evidence.scenes, expected, "Scene");
        }

        public static RagdollArtifactValidationResult ValidateDocumentationAudit(
            string path,
            RagdollArtifactValidationContext context)
        {
            DocumentationEvidence evidence = ReadJson<DocumentationEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 3)
                return Invalid("DocumentationSchemaInvalid");
            if (!evidence.succeeded || evidence.entries == null)
                return Invalid("DocumentationAuditFailed"
                    + (string.IsNullOrWhiteSpace(evidence.failureReason)
                        ? string.Empty : ":" + evidence.failureReason));
            DocumentationEntry[] candidates = evidence.entries.Where(entry =>
                entry != null && (string.IsNullOrWhiteSpace(context?.ExpectedCapabilityId)
                    || string.Equals(entry.id, context.ExpectedCapabilityId,
                        StringComparison.Ordinal))).ToArray();
            if (candidates.Length != 1)
                return Invalid("DocumentationCapabilityMissingOrDuplicate");
            DocumentationEntry candidate = candidates[0];
            if (!candidate.audited || string.IsNullOrWhiteSpace(candidate.affectedApi)
                || string.IsNullOrWhiteSpace(candidate.exactTest)
                || !HasItems(candidate.artifactKinds))
                return Invalid("DocumentationContractIncomplete");
            if (!IsOfficialDocumentation(candidate.sourceUrl))
                return Invalid("DocumentationSourceNotOfficial");
            if (string.IsNullOrWhiteSpace(evidence.documentPath)
                || !File.Exists(evidence.documentPath))
                return Invalid("DocumentationFileMissing");
            if (string.IsNullOrWhiteSpace(evidence.documentSha256)
                || !string.Equals(
                    RagdollClosurePipeline.ComputeSha256(evidence.documentPath),
                    evidence.documentSha256,
                    StringComparison.OrdinalIgnoreCase))
                return Invalid("DocumentationHashMismatch");
            string documentation = File.ReadAllText(evidence.documentPath);
            string packageRoot = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(evidence.documentPath), "..", ".."));
            string animationRoot = Path.Combine(packageRoot, "Animation");
            if (!Directory.Exists(animationRoot))
                return Invalid("DocumentationPackageSourceMissing");
            string[] sourceFiles = Directory.GetFiles(
                animationRoot, "*.cs", SearchOption.AllDirectories);
            RagdollDocumentationContractItem[] expected;
            string inventoryError;
            if (!RagdollDocumentationContractInventory.TryBuild(
                    packageRoot, out expected, out inventoryError))
                return Invalid("DocumentationInventoryUnavailable:" + inventoryError);
            DocumentationMemberMapping[] actual = candidate.members
                ?? Array.Empty<DocumentationMemberMapping>();
            if (actual.Length != expected.Length)
                return Invalid("DocumentationInventoryCountMismatch");
            if (actual.GroupBy(mapping => mapping?.memberId ?? string.Empty,
                    StringComparer.Ordinal).Any(group => string.IsNullOrEmpty(group.Key)
                        || group.Count() != 1))
                return Invalid("DocumentationInventoryIdentityMissingOrDuplicate");
            int expectedApiCount = expected.Count(item =>
                item.InventoryKind == "CompatibilityApi");
            int expectedMigrationCount = expected.Count(item =>
                item.InventoryKind == "SerializationMigration");
            if (candidate.compatibilityApiCount != expectedApiCount
                || candidate.serializationMigrationCount != expectedMigrationCount)
                return Invalid("DocumentationInventoryCategoryCountMismatch");
            string expectedInventoryHash = RagdollDocumentationContractInventory
                .ComputeInventorySha256(expected);
            if (string.IsNullOrWhiteSpace(candidate.inventorySha256)
                || !string.Equals(candidate.inventorySha256,
                    expectedInventoryHash, StringComparison.OrdinalIgnoreCase))
                return Invalid("DocumentationInventoryHashMismatch");
            string[] expectedSymbols = expected
                .Where(item => item.InventoryKind == "CompatibilityApi")
                .Select(item => item.Symbol).Distinct(StringComparer.Ordinal)
                .OrderBy(symbol => symbol, StringComparer.Ordinal).ToArray();
            string[] declaredSymbols = candidate.affectedApi.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(symbol => symbol.Trim())
                .OrderBy(symbol => symbol, StringComparer.Ordinal).ToArray();
            if (!expectedSymbols.SequenceEqual(declaredSymbols,
                    StringComparer.Ordinal))
                return Invalid("DocumentationApiInventoryMismatch");
            foreach (RagdollDocumentationContractItem expectedItem in expected)
            {
                DocumentationMemberMapping[] memberMappings =
                    actual.Where(mapping => mapping != null
                        && string.Equals(mapping.memberId, expectedItem.MemberId,
                            StringComparison.Ordinal)).ToArray();
                if (memberMappings.Length != 1)
                    return Invalid(
                        "DocumentationMemberMappingMissingOrDuplicate:"
                        + expectedItem.MemberId);
                DocumentationMemberMapping mapping = memberMappings[0];
                if (!mapping.verified
                    || !string.Equals(mapping.inventoryKind,
                        expectedItem.InventoryKind, StringComparison.Ordinal)
                    || !string.Equals(mapping.symbol,
                        expectedItem.Symbol, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(mapping.declaringType)
                    || string.IsNullOrWhiteSpace(mapping.memberName)
                    || string.IsNullOrWhiteSpace(mapping.memberKind)
                    || string.IsNullOrWhiteSpace(mapping.documentationSection)
                    || string.IsNullOrWhiteSpace(mapping.officialSourceUrl)
                    || string.IsNullOrWhiteSpace(mapping.sourcePath)
                    || !File.Exists(mapping.sourcePath))
                    return Invalid("DocumentationMemberMappingInvalid:"
                        + expectedItem.MemberId);
                if (!string.Equals(mapping.declaringType,
                        expectedItem.DeclaringType, StringComparison.Ordinal)
                    || !string.Equals(mapping.memberName,
                        expectedItem.MemberName, StringComparison.Ordinal)
                    || !string.Equals(mapping.memberKind,
                        expectedItem.MemberKind, StringComparison.Ordinal)
                    || !string.Equals(mapping.documentationSection,
                        expectedItem.DocumentationSection, StringComparison.Ordinal)
                    || !string.Equals(mapping.officialSourceUrl,
                        expectedItem.OfficialSourceUrl, StringComparison.Ordinal)
                    || !string.Equals(mapping.oldSerializedName,
                        expectedItem.OldSerializedName, StringComparison.Ordinal)
                    || !string.Equals(Path.GetFullPath(mapping.sourcePath),
                        Path.GetFullPath(expectedItem.SourcePath),
                        StringComparison.OrdinalIgnoreCase))
                    return Invalid("DocumentationMemberMappingMismatch:"
                        + expectedItem.MemberId);
                if (!IsOfficialDocumentation(mapping.officialSourceUrl))
                    return Invalid("DocumentationMemberSourceNotOfficial:"
                        + expectedItem.MemberId);
                if (string.IsNullOrWhiteSpace(mapping.sourceSha256)
                    || !string.Equals(mapping.sourceSha256,
                        expectedItem.SourceSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(mapping.sourceSha256,
                        RagdollClosurePipeline.ComputeSha256(mapping.sourcePath),
                        StringComparison.OrdinalIgnoreCase))
                    return Invalid("DocumentationMemberSourceHashMismatch:"
                        + expectedItem.MemberId);
                if (documentation.IndexOf(mapping.symbol,
                        StringComparison.Ordinal) < 0)
                    return Invalid("DocumentationSymbolMissing:"
                        + expectedItem.MemberId);
                if (!string.IsNullOrEmpty(mapping.oldSerializedName)
                    && documentation.IndexOf(mapping.oldSerializedName,
                        StringComparison.Ordinal) < 0)
                    return Invalid("DocumentationMigrationNameMissing:"
                        + expectedItem.MemberId);
                if (documentation.IndexOf(
                        "## " + mapping.documentationSection,
                        StringComparison.Ordinal) < 0)
                    return Invalid("DocumentationSectionMissing:"
                        + expectedItem.MemberId);
            }
            string exactMethod = candidate.exactTest.Substring(
                candidate.exactTest.LastIndexOf('.') + 1);
            if (!sourceFiles.Any(file => File.ReadAllText(file).IndexOf(
                    exactMethod, StringComparison.Ordinal) >= 0))
                return Invalid("DocumentationExactTestHasNoSource");
            return RagdollArtifactValidationResult.Valid();
        }

        static RagdollArtifactValidationResult ValidateExecutedScenarios(
            ExecutedScenario[] scenarios,
            string[] expected,
            string prefix)
        {
            if (scenarios == null) return Invalid(prefix + "ScenariosMissing");
            foreach (string name in expected)
            {
                ExecutedScenario[] matching = scenarios.Where(scenario =>
                    scenario != null && string.Equals(
                        scenario.name, name, StringComparison.Ordinal)).ToArray();
                if (matching.Length != 1)
                    return Invalid(prefix + "ScenarioMissingOrDuplicate:" + name);
                ExecutedScenario scenario = matching[0];
                if (!scenario.succeeded || scenario.frames <= 0
                    || scenario.assertions == null || scenario.assertions.Length == 0)
                    return Invalid(prefix + "ScenarioNotExecuted:" + name);
                ScenarioAssertion failed = scenario.assertions.FirstOrDefault(
                    assertion => assertion == null || !assertion.succeeded
                        || string.IsNullOrWhiteSpace(assertion.name));
                if (failed != null)
                    return Invalid(prefix + "ScenarioAssertionFailed:" + name);
                string[] semanticAssertions;
                if (!RequiredScenarioAssertions.TryGetValue(
                        name, out semanticAssertions))
                    return Invalid(prefix + "ScenarioContractMissing:" + name);
                foreach (string requiredAssertion in semanticAssertions)
                {
                    ScenarioAssertion[] assertions = scenario.assertions.Where(
                        assertion => assertion != null
                            && string.Equals(assertion.id, requiredAssertion,
                                StringComparison.Ordinal)).ToArray();
                    if (assertions.Length != 1)
                        return Invalid(prefix + "ScenarioSemanticAssertionMissing:"
                            + name + ":" + requiredAssertion);
                    if (!MetricProvesAssertion(assertions[0]))
                        return Invalid(prefix + "ScenarioSemanticMetricInvalid:"
                            + name + ":" + requiredAssertion);
                }
            }
            return RagdollArtifactValidationResult.Valid();
        }

        static bool MetricProvesAssertion(ScenarioAssertion assertion)
        {
            if (!Finite(assertion.actual) || !Finite(assertion.expected)
                || !FiniteNonNegative(assertion.tolerance)) return false;
            switch (assertion.comparison)
            {
                case "Equal":
                    return Math.Abs(assertion.actual - assertion.expected)
                        <= assertion.tolerance;
                case "Approximately":
                    return Math.Abs(assertion.actual - assertion.expected)
                        <= assertion.tolerance;
                case "GreaterOrEqual":
                    return assertion.actual + assertion.tolerance
                        >= assertion.expected;
                case "GreaterThan":
                    return assertion.actual > assertion.expected;
                default:
                    return false;
            }
        }

        static RagdollArtifactValidationResult ValidateDistribution(
            Distribution distribution,
            string name,
            int expectedSamples)
        {
            if (distribution == null || !FiniteNonNegative(distribution.median)
                || !FiniteNonNegative(distribution.p95)
                || distribution.p95 < distribution.median)
                return Invalid("Profiler" + name + "DistributionInvalid");
            if (distribution.sampleCount != expectedSamples)
                return Invalid("Profiler" + name + "SampleCountInvalid");
            if (distribution.rawSamples == null
                || distribution.rawSamples.Length != expectedSamples
                || distribution.rawSamples.Any(sample => !FiniteNonNegative(sample)))
                return Invalid("Profiler" + name + "RawSamplesInvalid");
            double[] ordered = (double[])distribution.rawSamples.Clone();
            Array.Sort(ordered);
            double median = Percentile(ordered, 0.5d);
            double p95 = Percentile(ordered, 0.95d);
            if (Math.Abs(median - distribution.median) > 0.0000001d
                || Math.Abs(p95 - distribution.p95) > 0.0000001d)
                return Invalid("Profiler" + name + "AggregateMismatch");
            return RagdollArtifactValidationResult.Valid();
        }

        static double Percentile(double[] ordered, double percentile)
        {
            int index = Math.Max(0, Math.Min(ordered.Length - 1,
                (int)Math.Ceiling(ordered.Length * percentile) - 1));
            return ordered[index];
        }

        static RagdollArtifactValidationResult ValidateEmbeddedProvenance(
            string path,
            RagdollArtifactValidationContext context)
        {
            bool required = !string.IsNullOrWhiteSpace(
                    context.ExpectedCertificationRunId)
                || !string.IsNullOrWhiteSpace(context.ExpectedSourceRevision)
                || !string.IsNullOrWhiteSpace(
                    context.ExpectedSourceTreeSha256);
            if (!required) return RagdollArtifactValidationResult.Valid();
            ArtifactProvenance provenance = ReadJson<ArtifactProvenance>(path);
            if (provenance == null)
                return Invalid("EmbeddedProvenanceMissing");
            if (!string.Equals(provenance.certificationRunId,
                    context.ExpectedCertificationRunId,
                    StringComparison.Ordinal)
                || !string.Equals(provenance.sourceRevision,
                    context.ExpectedSourceRevision,
                    StringComparison.Ordinal)
                || !string.Equals(provenance.sourceTreeSha256,
                    context.ExpectedSourceTreeSha256,
                    StringComparison.OrdinalIgnoreCase))
                return Invalid("EmbeddedProvenanceMismatch");
            return RagdollArtifactValidationResult.Valid();
        }

        static bool IsOfficialDocumentation(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            return string.Equals(uri.Host, "www.root-motion.com",
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "root-motion.com",
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "docs.unity3d.com",
                       StringComparison.OrdinalIgnoreCase);
        }

        static bool IsPassed(XElement element)
        {
            return string.Equals((string)element.Attribute("result"), "Passed",
                StringComparison.OrdinalIgnoreCase);
        }

        static string CaseName(XElement element)
        {
            return (string)element.Attribute("fullname")
                ?? (string)element.Attribute("name") ?? "unknown";
        }

        static T ReadJson<T>(string path) where T : class
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<T>(json);
        }

        static bool HasItems<T>(T[] values)
        {
            return values != null && values.Length != 0;
        }

        static bool FiniteNonNegative(double value)
        {
            return Finite(value) && value >= 0d;
        }

        static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant()
                .Replace('_', '-').Replace(' ', '-');
        }

        static RagdollArtifactValidationResult Invalid(string reason)
        {
            return RagdollArtifactValidationResult.Invalid(reason);
        }

        [Serializable]
        sealed class PlayerEvidence
        {
            public int schemaVersion = 0;
            public bool succeeded = false;
            public string platform = string.Empty;
            public ExecutedScenario[] scenarios = Array.Empty<ExecutedScenario>();
        }

        [Serializable]
        sealed class ArtifactProvenance
        {
            public string certificationRunId = string.Empty;
            public string sourceRevision = string.Empty;
            public string sourceTreeSha256 = string.Empty;
        }

        [Serializable]
        sealed class SceneEvidence
        {
            public int schemaVersion = 0;
            public bool succeeded = false;
            public ExecutedScenario[] scenes = Array.Empty<ExecutedScenario>();
        }

        [Serializable]
        sealed class ExecutedScenario
        {
            public string name = string.Empty;
            public bool succeeded = false;
            public int frames = 0;
            public ScenarioAssertion[] assertions = Array.Empty<ScenarioAssertion>();
            public PerformanceEvidence[] performance =
                Array.Empty<PerformanceEvidence>();
        }

        [Serializable]
        sealed class PerformanceEvidence
        {
            public int puppets = 0;
            public string mode = string.Empty;
            public long cpuMedianNanoseconds = 0L;
            public long cpuP95Nanoseconds = 0L;
            public long memoryMedianBytes = 0L;
            public long memoryP95Bytes = 0L;
            public long maximumGcAllocatedInFrame = 0L;
            public int measuredFrames = 0;
        }

        [Serializable]
        sealed class ScenarioAssertion
        {
            public string id = string.Empty;
            public string name = string.Empty;
            public bool succeeded = false;
            public string comparison = string.Empty;
            public double actual = double.NaN;
            public double expected = double.NaN;
            public double tolerance = double.NaN;
        }

        [Serializable]
        sealed class ProfilerEvidence
        {
            public int schemaVersion = 0;
            public bool succeeded = false;
            public int warmupFrames = 0;
            public int measuredFrames = 0;
            public Distribution cpuMilliseconds = null;
            public Distribution memoryBytes = null;
            public CriticalPathEvidence[] criticalPaths =
                Array.Empty<CriticalPathEvidence>();
        }

        [Serializable]
        sealed class Distribution
        {
            public double median = 0d;
            public double p95 = 0d;
            public int sampleCount = 0;
            public double[] rawSamples = Array.Empty<double>();
        }

        [Serializable]
        sealed class CriticalPathEvidence
        {
            public string name = string.Empty;
            public string measurementScope = string.Empty;
            public bool succeeded = false;
            public int samples = 0;
            public long gcAllocatedBytes = 0L;
            public long maxGcAllocatedBytesInFrame = 0L;
            public long[] rawAllocationSamples = Array.Empty<long>();
        }

        [Serializable]
        sealed class BuildEvidence
        {
            public int schemaVersion = 0;
            public BuildEntry[] builds = Array.Empty<BuildEntry>();
        }

        [Serializable]
        sealed class BuildEntry
        {
            public string target = string.Empty;
            public string result = string.Empty;
            public bool succeeded = false;
            public bool development = false;
            public bool allowDebugging = false;
            public bool outputExists = false;
            public string output = string.Empty;
            public BuildDiagnostic[] diagnostics = Array.Empty<BuildDiagnostic>();
            public int stepsScanned = 0;
            public int messagesScanned = 0;
            public string reportTraversalSha256 = string.Empty;
            public BuildTraversalStep[] reportSteps = Array.Empty<BuildTraversalStep>();
        }

        [Serializable]
        sealed class BuildTraversalStep
        {
            public string name = string.Empty;
            public BuildTraversalMessage[] messages =
                Array.Empty<BuildTraversalMessage>();
        }

        [Serializable]
        sealed class BuildTraversalMessage
        {
            public string severity = string.Empty;
            public string message = string.Empty;
        }

        [Serializable]
        sealed class BuildDiagnostic
        {
            public string severity = string.Empty;
            public bool own = false;
            public string message = string.Empty;
        }

        [Serializable]
        sealed class DocumentationEvidence
        {
            public int schemaVersion = 0;
            public bool succeeded = false;
            public string failureReason = string.Empty;
            public string documentPath = string.Empty;
            public string documentSha256 = string.Empty;
            public DocumentationEntry[] entries =
                Array.Empty<DocumentationEntry>();
        }

        [Serializable]
        sealed class DocumentationEntry
        {
            public string id = string.Empty;
            public string sourceUrl = string.Empty;
            public string affectedApi = string.Empty;
            public string exactTest = string.Empty;
            public string[] artifactKinds = Array.Empty<string>();
            public bool audited = false;
            public int compatibilityApiCount = 0;
            public int serializationMigrationCount = 0;
            public string inventorySha256 = string.Empty;
            public DocumentationMemberMapping[] members =
                Array.Empty<DocumentationMemberMapping>();
        }

        [Serializable]
        sealed class DocumentationMemberMapping
        {
            public string inventoryKind = string.Empty;
            public string memberId = string.Empty;
            public string symbol = string.Empty;
            public string declaringType = string.Empty;
            public string memberName = string.Empty;
            public string memberKind = string.Empty;
            public string documentationSection = string.Empty;
            public string officialSourceUrl = string.Empty;
            public string oldSerializedName = string.Empty;
            public string sourcePath = string.Empty;
            public string sourceSha256 = string.Empty;
            public bool verified = false;
        }
    }
}
