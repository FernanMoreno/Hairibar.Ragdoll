using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    public enum RagdollEvidenceKind
    {
        NUnitEditMode,
        NUnitPlayMode,
        WindowsPlayerScenario,
        LinuxPlayerScenario,
        BuildReport,
        ProfilerResult,
        SceneArtifact,
        DocumentationAudit,
        IndependentValidation
    }

    [Serializable]
    public sealed class RagdollEvidenceArtifact
    {
        public RagdollEvidenceKind kind;
        public string path;
        public string sha256;
        public string platform;
        public string scenario;
        public string generatedUtc;
        public string sourceRevision;
        public string sourceTreeSha256;
        public string[] capabilityIds;
        public string validationStatus;
        public string validationReason;
    }

    /// <summary>
    /// Strict, reproducible input for closure generation. The legacy string
    /// overload remains available for local diagnostics, but it deliberately
    /// does not provide source provenance.
    /// </summary>
    [Serializable]
    public sealed class RagdollCoverageRequest
    {
        public string[] nunitResultPaths;
        public RagdollEvidenceArtifact[] artifacts;
        public string sourceRevision;
        public string sourceTreeSha256;
        public string sourceLatestWriteUtc;
    }

    /// <summary>
    /// Builds auditable coverage data from the normative matrix, discovered NUnit
    /// tests and an optional NUnit result XML. Documentation status is deliberately
    /// not treated as executable evidence.
    /// </summary>
    public static class RagdollCoverageManifest
    {
        public const string TestResultsEnvironmentVariable =
            "HAIRIBAR_TEST_RESULTS";
        public const string OutputEnvironmentVariable =
            "HAIRIBAR_COVERAGE_MANIFEST";

        const string CatalogIdentity =
            "Hairibar.Ragdoll.Animation.Editor.RagdollCapabilityCatalog";
        static readonly Regex TestId = new Regex(
            @"^(?<id>[A-J]\d{2})_",
            RegexOptions.Compiled);

        [Serializable]
        public sealed class Manifest
        {
            public string generatedUtc;
            public string matrix;
            public string testResultsArtifact;
            public string sourceRevision;
            public string sourceTreeSha256;
            public RagdollEvidenceArtifact[] artifacts;
            public int total;
            public int verified;
            public int open;
            public int notApplicable;
            public Entry[] entries;
        }

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string source;
            public string sourceLocator;
            public string observableClaim;
            public string affectedApi;
            public string affectedApis;
            public string exactTest;
            public string testKind;
            public string executionArtifact;
            public string executionArtifactSha256;
            public string[] requiredEvidenceKinds;
            public RagdollEvidenceArtifact[] evidenceArtifacts;
            public string status;
            public string reason;
        }

        [MenuItem("Tools/Hairibar/Certification/Generate Coverage Manifest")]
        public static void GenerateFromEnvironment()
        {
            string results = Environment.GetEnvironmentVariable(
                TestResultsEnvironmentVariable);
            Manifest manifest = Build(results);
            string output = Environment.GetEnvironmentVariable(
                OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "../Library/HairibarCertification/coverage-manifest.json"));
            }

            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(output, JsonUtility.ToJson(manifest, true));
            UnityEngine.Debug.Log($"Hairibar coverage manifest written to '{output}'. " +
                $"Verified={manifest.verified}, Open={manifest.open}, " +
                $"N/A={manifest.notApplicable}.");
        }

        public static Manifest Build(string testResultsXmlPath = null)
        {
            return BuildCore(
                string.IsNullOrWhiteSpace(testResultsXmlPath)
                    ? Array.Empty<string>()
                    : new[] { testResultsXmlPath },
                Array.Empty<RagdollEvidenceArtifact>(),
                null,
                false);
        }

        public static Manifest Build(RagdollCoverageRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return BuildCore(
                request.nunitResultPaths ?? Array.Empty<string>(),
                request.artifacts ?? Array.Empty<RagdollEvidenceArtifact>(),
                request,
                true);
        }

        static Manifest BuildCore(
            string[] testResultsXmlPaths,
            RagdollEvidenceArtifact[] artifacts,
            RagdollCoverageRequest request,
            bool strict)
        {
            string packageRoot = ResolvePackageRoot();
            if (strict) ValidateStrictRequest(request, packageRoot);
            Dictionary<string, List<TestEvidence>> discovered = DiscoverTests();
            NUnitResultIndex results = LoadNUnitResults(
                testResultsXmlPaths, strict);
            List<Entry> entries = new List<Entry>(140);

            foreach (RagdollCapabilityContract contract in
                RagdollCapabilityCatalog.Contracts)
            {
                string id = contract.Id;
                string capability = contract.AffectedApis;
                if (id == "G05")
                {
                    entries.Add(new Entry
                    {
                        id = id,
                        source = contract.OfficialSource,
                        sourceLocator = contract.SourceLocator,
                        observableClaim = contract.ObservableClaim,
                        affectedApi = capability + " / IRagdollIKSolver",
                        affectedApis = contract.AffectedApis,
                        testKind = "N/A",
                        requiredEvidenceKinds = Array.Empty<string>(),
                        evidenceArtifacts = Array.Empty<RagdollEvidenceArtifact>(),
                        status = "N/A",
                        reason = contract.ExclusionReason
                    });
                    continue;
                }

                List<TestEvidence> tests;
                discovered.TryGetValue(id, out tests);
                if (strict && tests != null && tests.Count > 1)
                    throw new InvalidDataException(
                        "Multiple executable tests claim capability " + id + ": "
                        + string.Join(", ", tests.Select(test => test.FullName)));
                TestEvidence evidence = tests != null && tests.Count == 1
                    ? tests[0]
                    : null;
                string[] requiredKinds = RequiredEvidenceKinds(id, evidence);
                RagdollEvidenceArtifact[] matchingArtifacts = strict
                    ? FindEvidenceArtifacts(id, evidence, artifacts, requiredKinds)
                    : Array.Empty<RagdollEvidenceArtifact>();
                bool executed = evidence != null
                    && results.IsPassed(evidence.FullName)
                    && (!strict || HasAllRequiredEvidence(
                        id,
                        evidence,
                        requiredKinds,
                        matchingArtifacts,
                        results));
                bool artifactOnlyExecuted = strict
                    && AllowsArtifactOnlyEvidence(id)
                    && requiredKinds.Length != 0
                    && requiredKinds.All(required => matchingArtifacts.Any(
                        artifact => artifact.kind.ToString() == required
                            && ArtifactContentProvesCapability(artifact, id)));
                bool verified = executed || artifactOnlyExecuted;
                string reason;
                if (verified)
                {
                    reason = string.Empty;
                }
                else if (tests == null || tests.Count == 0)
                {
                    reason = "No stable ID-prefixed executable test was discovered.";
                }
                else if (tests.Count > 1)
                {
                    reason = "Multiple tests claim this ID; evidence is ambiguous.";
                }
                else if (!strict && testResultsXmlPaths.Length == 0)
                {
                    reason = "Test exists but no NUnit execution artifact was supplied.";
                }
                else if (!verified)
                {
                    reason = EvidenceFailureReason(requiredKinds, artifacts)
                        ?? results.ReasonFor(evidence?.FullName)
                        ?? EvidenceContentFailureReason(
                            id, evidence, requiredKinds, matchingArtifacts)
                        ?? "Required current, hashed evidence was not supplied.";
                }
                else reason = string.Empty;

                entries.Add(new Entry
                {
                    id = id,
                    source = contract.OfficialSource,
                    sourceLocator = contract.SourceLocator,
                    observableClaim = contract.ObservableClaim,
                    affectedApi = capability,
                    affectedApis = contract.AffectedApis,
                    exactTest = evidence?.FullName ?? string.Empty,
                    testKind = evidence?.Kind ?? string.Empty,
                    executionArtifact = verified && matchingArtifacts.Length != 0
                        ? matchingArtifacts[0].path
                        : executed && testResultsXmlPaths.Length != 0
                            ? Path.GetFullPath(testResultsXmlPaths[0])
                        : string.Empty,
                    executionArtifactSha256 = verified && matchingArtifacts.Length != 0
                        ? matchingArtifacts[0].sha256
                        : executed && testResultsXmlPaths.Length != 0
                            ? ComputeSha256(testResultsXmlPaths[0])
                        : string.Empty,
                    requiredEvidenceKinds = requiredKinds,
                    evidenceArtifacts = matchingArtifacts,
                    status = verified ? "Verified" : "Open",
                    reason = reason
                });
            }

            ValidateRows(entries);
            return new Manifest
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                matrix = CatalogIdentity,
                testResultsArtifact = testResultsXmlPaths.Length == 1
                    && HasUsableArtifact(testResultsXmlPaths[0])
                    ? Path.GetFullPath(testResultsXmlPaths[0])
                    : string.Empty,
                sourceRevision = request?.sourceRevision ?? string.Empty,
                sourceTreeSha256 = request?.sourceTreeSha256 ?? string.Empty,
                artifacts = artifacts,
                total = entries.Count,
                verified = entries.Count(entry => entry.status == "Verified"),
                open = entries.Count(entry => entry.status == "Open"),
                notApplicable = entries.Count(entry => entry.status == "N/A"),
                entries = entries.ToArray()
            };
        }

        static Dictionary<string, List<TestEvidence>> DiscoverTests()
        {
            var result = new Dictionary<string, List<TestEvidence>>(
                StringComparer.Ordinal);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }

                foreach (Type type in types)
                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    Match idMatch = TestId.Match(method.Name);
                    if (!idMatch.Success || !IsExecutableTest(method)) continue;

                    string id = idMatch.Groups["id"].Value;
                    List<TestEvidence> list;
                    if (!result.TryGetValue(id, out list))
                    {
                        list = new List<TestEvidence>();
                        result.Add(id, list);
                    }

                    list.Add(new TestEvidence(
                        type.FullName + "." + method.Name,
                        ResolveTestKind(assembly, method),
                        CountExplicitTestCases(method)));
                }
            }

            return result;
        }

        static bool IsExecutableTest(MethodInfo method)
        {
            return method.GetCustomAttributes(false).Any(attribute =>
            {
                string name = attribute.GetType().FullName;
                return name == "NUnit.Framework.TestAttribute"
                    || name == "UnityEngine.TestTools.UnityTestAttribute"
                    || name == "NUnit.Framework.TestCaseAttribute";
            });
        }

        static bool IsUnityTest(MethodInfo method)
        {
            return method.GetCustomAttributes(false).Any(attribute =>
                attribute.GetType().FullName ==
                "UnityEngine.TestTools.UnityTestAttribute");
        }

        static int CountExplicitTestCases(MethodInfo method)
        {
            return method.GetCustomAttributes(false).Count(attribute =>
                attribute.GetType().FullName ==
                "NUnit.Framework.TestCaseAttribute");
        }

        static string ResolveTestKind(Assembly assembly, MethodInfo method)
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            if (assemblyName.IndexOf(
                "Editor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "EditMode";
            }
            return IsUnityTest(method) ? "PlayMode/UnityTest" : "PlayMode/NUnit";
        }

        static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        static void ValidateStrictRequest(
            RagdollCoverageRequest request,
            string packageRoot)
        {
            if (string.IsNullOrWhiteSpace(request.sourceRevision))
                throw new ArgumentException(
                    "Strict coverage requires a source revision.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.sourceTreeSha256))
                throw new ArgumentException(
                    "Strict coverage requires a source tree SHA-256.", nameof(request));
            if (!IsSha256(request.sourceTreeSha256))
                throw new ArgumentException(
                    "sourceTreeSha256 must contain exactly 64 hexadecimal characters.",
                    nameof(request));

            string currentSourceHash = ComputeSourceTreeSha256(packageRoot);
            if (!string.Equals(
                request.sourceTreeSha256,
                currentSourceHash,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The requested source tree hash does not match the current package. " +
                    "Expected " + currentSourceHash + ".");

            DateTime sourceLatestWriteUtc;
            if (!DateTime.TryParse(
                request.sourceLatestWriteUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out sourceLatestWriteUtc))
            {
                throw new ArgumentException(
                    "Strict coverage requires sourceLatestWriteUtc in round-trip format.",
                    nameof(request));
            }
            sourceLatestWriteUtc = GetLatestSourceWriteUtc(packageRoot);

            RagdollEvidenceArtifact[] artifacts = request.artifacts
                ?? Array.Empty<RagdollEvidenceArtifact>();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < artifacts.Length; index++)
            {
                RagdollEvidenceArtifact artifact = artifacts[index]
                    ?? throw new ArgumentException(
                        "Evidence artifacts cannot contain null entries.",
                        nameof(request));
                if (!Enum.IsDefined(typeof(RagdollEvidenceKind), artifact.kind))
                    throw new InvalidDataException(
                        "Evidence artifact kind is not defined: " + (int)artifact.kind);
                artifact.validationStatus = "Invalid";
                artifact.validationReason = string.Empty;
                if (string.IsNullOrWhiteSpace(artifact.path)
                    || !File.Exists(artifact.path))
                {
                    artifact.validationReason = "ArtifactMissing";
                    continue;
                }

                artifact.path = Path.GetFullPath(artifact.path);
                string identity = artifact.kind + "|" + artifact.path + "|"
                    + (artifact.platform ?? string.Empty) + "|"
                    + (artifact.scenario ?? string.Empty);
                if (!identities.Add(identity))
                    throw new InvalidOperationException(
                        "Duplicate evidence artifact: " + identity);

                string actualHash = ComputeSha256(artifact.path);
                if (!IsSha256(artifact.sha256)
                    || !string.Equals(
                    actualHash, artifact.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    artifact.validationReason = "ArtifactHashMismatch";
                    continue;
                }
                if (!string.Equals(
                    artifact.sourceRevision,
                    request.sourceRevision,
                    StringComparison.Ordinal)
                    || !string.Equals(
                        artifact.sourceTreeSha256,
                        request.sourceTreeSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    artifact.validationReason = "ArtifactSourceMismatch";
                    continue;
                }

                DateTime generatedUtc;
                if (!DateTime.TryParse(
                    artifact.generatedUtc,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out generatedUtc))
                {
                    artifact.validationReason = "ArtifactGeneratedUtcInvalid";
                    continue;
                }
                if (generatedUtc.ToUniversalTime() < sourceLatestWriteUtc)
                {
                    artifact.validationReason = "ArtifactStale";
                    continue;
                }
                if (generatedUtc.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5d))
                {
                    artifact.validationReason = "ArtifactGeneratedUtcFuture";
                    continue;
                }
                artifact.validationStatus = "Valid";
            }

            string[] resultPaths = request.nunitResultPaths ?? Array.Empty<string>();
            for (int index = 0; index < resultPaths.Length; index++)
            {
                string fullPath = Path.GetFullPath(resultPaths[index]);
                if (!artifacts.Any(artifact =>
                    string.Equals(artifact.path, fullPath, StringComparison.OrdinalIgnoreCase)
                    && (artifact.kind == RagdollEvidenceKind.NUnitEditMode
                        || artifact.kind == RagdollEvidenceKind.NUnitPlayMode)))
                {
                    // This is an evidence deficiency, not a malformed catalog. The
                    // affected rows remain Open because no valid NUnit descriptor
                    // will be selected by FindEvidenceArtifacts.
                }
            }
        }

        static bool IsSha256(string value)
        {
            return !string.IsNullOrEmpty(value)
                && Regex.IsMatch(value, "^[0-9a-fA-F]{64}$");
        }

        static string[] RequiredEvidenceKinds(string id, TestEvidence evidence)
        {
            return RagdollCapabilityCatalog.Get(id).RequiredEvidence
                .Select(kind => kind.ToString()).ToArray();
        }

        static bool AllowsArtifactOnlyEvidence(string id)
        {
            return id[0] == 'J';
        }

        static RagdollEvidenceArtifact[] FindEvidenceArtifacts(
            string id,
            TestEvidence evidence,
            RagdollEvidenceArtifact[] artifacts,
            string[] requiredKinds)
        {
            return artifacts.Where(artifact =>
            {
                if (!string.Equals(
                    artifact.validationStatus, "Valid", StringComparison.Ordinal))
                    return false;
                bool kindMatches = requiredKinds.Contains(artifact.kind.ToString());
                if (!kindMatches) return false;
                if (artifact.capabilityIds != null
                    && artifact.capabilityIds.Contains(id, StringComparer.Ordinal))
                    return true;
                if (evidence == null) return false;
                return (artifact.kind == RagdollEvidenceKind.NUnitEditMode
                        || artifact.kind == RagdollEvidenceKind.NUnitPlayMode)
                    && artifact.path != null;
            }).ToArray();
        }

        static bool ArtifactContentProvesCapability(
            RagdollEvidenceArtifact artifact,
            string id)
        {
            if (artifact == null
                || !string.Equals(
                    artifact.validationStatus, "Valid", StringComparison.Ordinal)
                || !File.Exists(artifact.path))
                return false;

            return RagdollEvidenceArtifactValidators.Validate(
                artifact,
                new RagdollArtifactValidationContext
                {
                    ExpectedCapabilityId = id,
                    ExpectedPlatform = artifact.kind ==
                        RagdollEvidenceKind.LinuxPlayerScenario
                            ? "Linux64"
                            : artifact.kind ==
                                RagdollEvidenceKind.WindowsPlayerScenario
                                ? "Windows64"
                                : string.Empty
                }).IsValid;
        }

        static string ComputeSourceTreeSha256(string packageRoot)
        {
            string root = Path.GetFullPath(packageRoot);
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(
                    Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(path => Path.GetRelativePath(root, path)
                    .Replace('\\', '/'), StringComparer.Ordinal)
                .ToArray();
            using (SHA256 sha = SHA256.Create())
            using (var aggregate = new MemoryStream())
            {
                for (int index = 0; index < files.Length; index++)
                {
                    string relative = Path.GetRelativePath(root, files[index])
                        .Replace('\\', '/');
                    byte[] line = System.Text.Encoding.UTF8.GetBytes(
                        relative + "\0" + ComputeSha256(files[index]) + "\n");
                    aggregate.Write(line, 0, line.Length);
                }
                aggregate.Position = 0;
                return BitConverter.ToString(sha.ComputeHash(aggregate))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        internal static string ComputeCurrentSourceTreeSha256()
        {
            return ComputeSourceTreeSha256(ResolvePackageRoot());
        }

        internal static string CurrentSourceLatestWriteUtc()
        {
            return GetLatestSourceWriteUtc(ResolvePackageRoot()).ToString("O");
        }

        static DateTime GetLatestSourceWriteUtc(string packageRoot)
        {
            DateTime latest = DateTime.MinValue;
            foreach (string path in Directory.GetFiles(
                packageRoot, "*", SearchOption.AllDirectories))
            {
                if (path.IndexOf(
                    Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                DateTime write = File.GetLastWriteTimeUtc(path);
                if (write > latest) latest = write;
            }
            return latest;
        }

        static bool HasNUnitArtifactFor(
            RagdollEvidenceKind requiredKind,
            string exactTestName,
            TestEvidence evidence,
            RagdollEvidenceArtifact[] artifacts,
            NUnitResultIndex results)
        {
            RagdollEvidenceKind primaryKind = evidence.Kind == "EditMode"
                ? RagdollEvidenceKind.NUnitEditMode
                : RagdollEvidenceKind.NUnitPlayMode;
            string requiredTest = string.IsNullOrWhiteSpace(exactTestName)
                ? requiredKind == primaryKind ? evidence.FullName : null
                : exactTestName;
            if (string.IsNullOrWhiteSpace(requiredTest)) return false;

            int expectedCases = string.Equals(
                requiredTest, evidence.FullName, StringComparison.Ordinal)
                    ? evidence.ExpectedParameterizedCases
                    : 0;
            return artifacts.Any(artifact => artifact.kind == requiredKind
                && results.ContainsInArtifact(requiredTest, artifact.path)
                && RagdollEvidenceArtifactValidators.Validate(
                    artifact,
                    new RagdollArtifactValidationContext
                    {
                        ExactTestName = requiredTest,
                        ExpectedParameterizedCases = expectedCases
                    }).IsValid);
        }

        static bool HasAllRequiredEvidence(
            string id,
            TestEvidence evidence,
            string[] requiredKinds,
            RagdollEvidenceArtifact[] artifacts,
            NUnitResultIndex results)
        {
            for (int index = 0; index < requiredKinds.Length; index++)
            {
                RagdollEvidenceKind kind;
                if (!Enum.TryParse(requiredKinds[index], out kind)) return false;
                // AND semantics require a distinct descriptor of every declared
                // kind. Never let the aggregate NUnit result index substitute a
                // passing case from a different artifact kind.
                if (!artifacts.Any(artifact => artifact.kind == kind))
                    return false;
                if (kind == RagdollEvidenceKind.NUnitEditMode
                    || kind == RagdollEvidenceKind.NUnitPlayMode)
                {
                    RagdollCapabilityContract contract =
                        RagdollCapabilityCatalog.Get(id);
                    contract.ExactNUnitEvidenceTests.TryGetValue(
                        kind, out string exactTestName);
                    if (!HasNUnitArtifactFor(
                        kind, exactTestName, evidence, artifacts, results))
                        return false;
                    continue;
                }
                if (!artifacts.Any(artifact => artifact.kind == kind
                    && ArtifactContentProvesCapability(artifact, id)))
                    return false;
            }
            return requiredKinds.Length != 0;
        }

        static string EvidenceFailureReason(
            string[] requiredKinds,
            RagdollEvidenceArtifact[] artifacts)
        {
            RagdollEvidenceArtifact invalid = artifacts.FirstOrDefault(artifact =>
                artifact != null
                && requiredKinds.Contains(artifact.kind.ToString())
                && !string.Equals(
                    artifact.validationStatus, "Valid", StringComparison.Ordinal));
            if (invalid == null) return null;
            switch (invalid.validationReason)
            {
                case "ArtifactMissing":
                    return "The required evidence artifact does not exist.";
                case "ArtifactHashMismatch":
                    return "The required evidence artifact SHA-256 does not match.";
                case "ArtifactSourceMismatch":
                    return "The evidence source provenance does not match.";
                case "ArtifactGeneratedUtcInvalid":
                    return "The evidence generatedUtc is invalid.";
                case "ArtifactStale":
                    return "The evidence artifact is stale and older than the source.";
                case "ArtifactGeneratedUtcFuture":
                    return "The evidence generatedUtc is implausibly in the future.";
                default:
                    return "The required evidence artifact is invalid.";
            }
        }

        static string EvidenceContentFailureReason(
            string id,
            TestEvidence evidence,
            string[] requiredKinds,
            RagdollEvidenceArtifact[] artifacts)
        {
            for (int index = 0; index < requiredKinds.Length; index++)
            {
                RagdollEvidenceKind kind;
                if (!Enum.TryParse(requiredKinds[index], out kind))
                    return "Unknown evidence requirement: " + requiredKinds[index] + ".";
                RagdollEvidenceArtifact[] candidates = artifacts
                    .Where(artifact => artifact.kind == kind).ToArray();
                if (candidates.Length == 0)
                    return "Required evidence kind is missing: " + kind + ".";
                for (int candidateIndex = 0;
                    candidateIndex < candidates.Length; candidateIndex++)
                {
                    RagdollArtifactValidationResult validation =
                        RagdollEvidenceArtifactValidators.Validate(
                            candidates[candidateIndex],
                            new RagdollArtifactValidationContext
                            {
                                ExactTestName = kind == RagdollEvidenceKind.NUnitEditMode
                                    || kind == RagdollEvidenceKind.NUnitPlayMode
                                        ? evidence?.FullName
                                        : null,
                                ExpectedParameterizedCases =
                                    evidence?.ExpectedParameterizedCases ?? 0,
                                ExpectedCapabilityId = id
                            });
                    if (validation.IsValid) goto NextRequirement;
                    if (candidateIndex == candidates.Length - 1)
                        return "Evidence content is invalid for " + id + ": "
                            + validation.Reason + ".";
                }
            NextRequirement:;
            }
            return null;
        }

        static NUnitResultIndex LoadNUnitResults(
            IEnumerable<string> paths,
            bool rejectDuplicates)
        {
            var index = new NUnitResultIndex();
            var exactCases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths ?? Array.Empty<string>())
            {
                if (!HasUsableArtifact(path)) continue;
                XDocument document = XDocument.Load(path);
                foreach (XElement testCase in document.Descendants("test-case"))
                {
                    string fullName = (string)testCase.Attribute("fullname")
                        ?? (string)testCase.Attribute("name");
                    if (string.IsNullOrWhiteSpace(fullName)) continue;
                    string result = (string)testCase.Attribute("result") ?? "Unknown";
                    string previousResult;
                    if (rejectDuplicates
                        && exactCases.TryGetValue(fullName, out previousResult))
                    {
                        if (!string.Equals(
                            previousResult, result, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException(
                                "Contradictory NUnit test case evidence: " + fullName);
                        continue;
                    }
                    exactCases[fullName] = result;
                    string methodName = NormalizeNUnitMethodName(fullName);
                    index.Add(methodName, result, Path.GetFullPath(path));
                }
            }
            return index;
        }

        static string NormalizeNUnitMethodName(string fullName)
        {
            int arguments = fullName.IndexOf('(');
            return arguments >= 0 ? fullName.Substring(0, arguments) : fullName;
        }

        static HashSet<string> LoadPassedTests(string path)
        {
            var passed = new HashSet<string>(StringComparer.Ordinal);
            if (!HasUsableArtifact(path)) return passed;

            XDocument document = XDocument.Load(path);
            foreach (XElement testCase in document.Descendants("test-case"))
            {
                string result = (string)testCase.Attribute("result");
                if (!string.Equals(result, "Passed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fullName = (string)testCase.Attribute("fullname")
                    ?? (string)testCase.Attribute("name");
                if (!string.IsNullOrWhiteSpace(fullName)) passed.Add(fullName);
            }

            return passed;
        }

        static bool HasUsableArtifact(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        static void ValidateRows(List<Entry> entries)
        {
            if (entries.Count != 140)
            {
                throw new InvalidOperationException(
                    $"Coverage matrix must contain exactly 140 rows; found {entries.Count}.");
            }

            string[] duplicateIds = entries.GroupBy(entry => entry.id)
                .Where(group => group.Count() != 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateIds.Length != 0)
            {
                throw new InvalidOperationException(
                    "Coverage IDs must be unique: " + string.Join(", ", duplicateIds));
            }

            Entry[] na = entries.Where(entry => entry.status == "N/A").ToArray();
            if (na.Length != 1 || na[0].id != "G05")
            {
                throw new InvalidOperationException(
                    "G05 must be the only deliberately excluded capability.");
            }
        }

        static string ResolvePackageRoot()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(RagdollAnimator).Assembly);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                throw new InvalidOperationException(
                    "Hairibar package root could not be resolved.");
            }

            return package.resolvedPath;
        }

        sealed class TestEvidence
        {
            public TestEvidence(
                string fullName,
                string kind,
                int expectedParameterizedCases)
            {
                FullName = fullName;
                Kind = kind;
                ExpectedParameterizedCases = expectedParameterizedCases;
            }

            public string FullName { get; }
            public string Kind { get; }
            public int ExpectedParameterizedCases { get; }
        }

        sealed class NUnitResultIndex
        {
            readonly Dictionary<string, ResultAggregate> results =
                new Dictionary<string, ResultAggregate>(StringComparer.Ordinal);

            public void Add(string fullName, string result, string artifactPath)
            {
                ResultAggregate aggregate;
                if (!results.TryGetValue(fullName, out aggregate))
                {
                    aggregate = new ResultAggregate();
                    results.Add(fullName, aggregate);
                }
                aggregate.Count++;
                aggregate.ArtifactPaths.Add(artifactPath);
                if (!string.Equals(
                    result, "Passed", StringComparison.OrdinalIgnoreCase))
                {
                    aggregate.AllPassed = false;
                    aggregate.NonPassingResult = result;
                }
            }

            public bool IsPassed(string fullName)
            {
                ResultAggregate aggregate;
                return !string.IsNullOrWhiteSpace(fullName)
                    && results.TryGetValue(fullName, out aggregate)
                    && aggregate.Count != 0
                    && aggregate.AllPassed;
            }

            public string ReasonFor(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return null;
                ResultAggregate aggregate;
                if (!results.TryGetValue(fullName, out aggregate))
                    return "The exact test is absent from the supplied NUnit artifacts.";
                if (!aggregate.AllPassed)
                    return "The exact test contains a non-passing case: "
                        + aggregate.NonPassingResult + ".";
                return null;
            }

            public bool ContainsInArtifact(string fullName, string artifactPath)
            {
                ResultAggregate aggregate;
                return !string.IsNullOrWhiteSpace(fullName)
                    && !string.IsNullOrWhiteSpace(artifactPath)
                    && results.TryGetValue(fullName, out aggregate)
                    && aggregate.ArtifactPaths.Contains(Path.GetFullPath(artifactPath));
            }

            sealed class ResultAggregate
            {
                public int Count;
                public bool AllPassed = true;
                public string NonPassingResult;
                public readonly HashSet<string> ArtifactPaths =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

    }
}
