using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        static readonly string[] RequiredCriticalPaths =
        {
            "matching", "mapping", "collision-relay", "com",
            "additional-pin", "baker-realtime"
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
            if (evidence == null || evidence.schemaVersion != 2)
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
                if (cell.maximumGcAllocatedInFrame < 0L)
                    return Invalid("PlayerPerformanceAmbientGcInvalid:"
                        + population + ":" + mode);
            }
            bool flatAsserted = scenario.assertions.Any(assertion =>
                assertion != null && assertion.succeeded
                && assertion.name != null
                && assertion.name.IndexOf("FlattenHierarchy",
                    StringComparison.Ordinal) >= 0);
            bool treeAsserted = scenario.assertions.Any(assertion =>
                assertion != null && assertion.succeeded
                && assertion.name != null
                && assertion.name.IndexOf("TreeHierarchy",
                    StringComparison.Ordinal) >= 0);
            return flatAsserted && treeAsserted
                ? RagdollArtifactValidationResult.Valid()
                : Invalid("PlayerHierarchyModesNotAsserted");
        }

        public static RagdollArtifactValidationResult ValidateProfiler(string path)
        {
            ProfilerEvidence evidence = ReadJson<ProfilerEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 2)
                return Invalid("ProfilerSchemaInvalid");
            if (!evidence.succeeded) return Invalid("ProfilerRunFailed");
            if (evidence.warmupFrames < 120)
                return Invalid("ProfilerWarmupTooShort");
            if (evidence.measuredFrames < 600)
                return Invalid("ProfilerMeasurementTooShort");
            RagdollArtifactValidationResult cpu = ValidateDistribution(
                evidence.cpuMilliseconds, "Cpu");
            if (!cpu.IsValid) return cpu;
            RagdollArtifactValidationResult memory = ValidateDistribution(
                evidence.memoryBytes, "Memory");
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
                if (!result.succeeded || result.samples < 600)
                    return Invalid("ProfilerCriticalPathIncomplete:" + required);
                if (string.IsNullOrWhiteSpace(result.measurementScope))
                    return Invalid("ProfilerCriticalPathScopeMissing:" + required);
                if (!measurementScopes.Add(result.measurementScope))
                    return Invalid("ProfilerCriticalPathScopeReused:"
                        + result.measurementScope);
                if (result.gcAllocatedBytes != 0
                    || result.maxGcAllocatedBytesInFrame != 0)
                    return Invalid("ProfilerCriticalPathAllocated:" + required);
            }
            return RagdollArtifactValidationResult.Valid();
        }

        public static RagdollArtifactValidationResult ValidateBuildReport(string path)
        {
            BuildEvidence evidence = ReadJson<BuildEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 2)
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
                if (build.diagnostics == null) continue;
                BuildDiagnostic ownProblem = build.diagnostics.FirstOrDefault(message =>
                    message != null && message.own
                    && (string.Equals(message.severity, "Error",
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(message.severity, "Warning",
                            StringComparison.OrdinalIgnoreCase)));
                if (ownProblem != null)
                    return Invalid("BuildOwnDiagnostic:" + target);
            }
            return RagdollArtifactValidationResult.Valid();
        }

        public static RagdollArtifactValidationResult ValidateScenes(
            string path,
            RagdollArtifactValidationContext context)
        {
            SceneEvidence evidence = ReadJson<SceneEvidence>(path);
            if (evidence == null || evidence.schemaVersion != 2)
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
            if (evidence == null || evidence.schemaVersion != 2)
                return Invalid("DocumentationSchemaInvalid");
            if (!evidence.succeeded || evidence.entries == null)
                return Invalid("DocumentationAuditFailed");
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
            string[] symbols = candidate.affectedApi.Split(new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries);
            if (symbols.Length == 0)
                return Invalid("DocumentationApiMappingMissing");
            foreach (string raw in symbols)
            {
                string symbol = raw.Trim();
                if (documentation.IndexOf(symbol, StringComparison.Ordinal) < 0)
                    return Invalid("DocumentationSymbolMissing:" + symbol);
                DocumentationMemberMapping[] memberMappings =
                    candidate.members == null
                        ? Array.Empty<DocumentationMemberMapping>()
                        : candidate.members.Where(mapping => mapping != null
                            && string.Equals(mapping.symbol, symbol,
                                StringComparison.Ordinal)).ToArray();
                if (memberMappings.Length != 1)
                    return Invalid(
                        "DocumentationMemberMappingMissingOrDuplicate:" + symbol);
                DocumentationMemberMapping mapping = memberMappings[0];
                if (!mapping.verified
                    || string.IsNullOrWhiteSpace(mapping.declaringType)
                    || string.IsNullOrWhiteSpace(mapping.memberName)
                    || string.IsNullOrWhiteSpace(mapping.memberKind)
                    || string.IsNullOrWhiteSpace(mapping.documentationSection)
                    || string.IsNullOrWhiteSpace(mapping.sourcePath)
                    || !File.Exists(mapping.sourcePath))
                    return Invalid("DocumentationMemberMappingInvalid:" + symbol);
                Type mappedType = ResolveLoadedType(mapping.declaringType);
                if (mappedType == null)
                    return Invalid("DocumentationMappedTypeMissing:" + symbol);
                const System.Reflection.BindingFlags PublicFlags =
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public;
                if (string.Equals(mapping.memberKind, "Type",
                        StringComparison.Ordinal))
                {
                    if (!string.Equals(mappedType.Name, mapping.memberName,
                            StringComparison.Ordinal))
                        return Invalid("DocumentationMappedTypeMismatch:" + symbol);
                }
                else if (string.Equals(mapping.memberKind, "Attribute",
                    StringComparison.Ordinal))
                {
                    System.Reflection.FieldInfo field = mappedType.GetField(
                        mapping.memberName,
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic);
                    if (field == null || !field.GetCustomAttributes(true).Any(
                        attribute => attribute != null
                            && string.Equals(attribute.GetType().Name,
                                symbol + "Attribute", StringComparison.Ordinal)))
                        return Invalid("DocumentationMappedAttributeMissing:" + symbol);
                }
                else if (mappedType.GetMember(
                    mapping.memberName, PublicFlags).Length == 0)
                    return Invalid("DocumentationMappedMemberInfoMissing:" + symbol);
                string memberSource = File.ReadAllText(mapping.sourcePath);
                if (memberSource.IndexOf(mapping.memberName,
                        StringComparison.Ordinal) < 0
                    && memberSource.IndexOf(symbol,
                        StringComparison.Ordinal) < 0)
                    return Invalid("DocumentationMappedMemberMissing:" + symbol);
                if (documentation.IndexOf(
                        "## " + mapping.documentationSection,
                        StringComparison.Ordinal) < 0)
                    return Invalid("DocumentationSectionMissing:" + symbol);
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
            }
            return RagdollArtifactValidationResult.Valid();
        }

        static RagdollArtifactValidationResult ValidateDistribution(
            Distribution distribution,
            string name)
        {
            if (distribution == null || !FiniteNonNegative(distribution.median)
                || !FiniteNonNegative(distribution.p95)
                || distribution.p95 < distribution.median)
                return Invalid("Profiler" + name + "DistributionInvalid");
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

        static Type ResolveLoadedType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            System.Reflection.Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
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
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
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
        }

        [Serializable]
        sealed class ScenarioAssertion
        {
            public string name = string.Empty;
            public bool succeeded = false;
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
            public DocumentationMemberMapping[] members =
                Array.Empty<DocumentationMemberMapping>();
        }

        [Serializable]
        sealed class DocumentationMemberMapping
        {
            public string symbol = string.Empty;
            public string declaringType = string.Empty;
            public string memberName = string.Empty;
            public string memberKind = string.Empty;
            public string documentationSection = string.Empty;
            public string sourcePath = string.Empty;
            public bool verified = false;
        }
    }
}
