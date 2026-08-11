using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollEvidenceArtifactValidatorsTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),
                "HairibarArtifactValidators-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void J02_NUnitEditModeArtifactRequiresCompletePassingRunAndExactCases()
        {
            string path = Write("nunit.xml",
                "<test-run result='Passed' failed='0' skipped='0' inconclusive='0' warnings='0'>"
                + "<test-suite result='Passed'><test-case fullname='Fixture.B02_Case(1)' result='Passed'/>"
                + "<test-case fullname='Fixture.B02_Case(2)' result='Passed'/></test-suite></test-run>");
            RagdollArtifactValidationResult result = Validate(
                RagdollEvidenceKind.NUnitEditMode, path,
                new RagdollArtifactValidationContext
                {
                    ExactTestName = "Fixture.B02_Case",
                    ExpectedParameterizedCases = 2
                });
            Assert.That(result.IsValid, Is.True, result.Reason);
        }

        [TestCase("Skipped")]
        [TestCase("Inconclusive")]
        [TestCase("Failed")]
        public void NUnit_RejectsAnyNonPassingCase(string status)
        {
            string path = Write("nunit.xml",
                "<test-run result='Passed'><test-case fullname='Fixture.B02_Case' result='Passed'/>"
                + "<test-case fullname='Elsewhere' result='" + status + "'/></test-run>");
            Assert.That(Validate(RagdollEvidenceKind.NUnitPlayMode, path,
                Context("Fixture.B02_Case")).IsValid, Is.False);
        }

        [Test]
        public void Player_RequiresPlatformExecutedScenariosAndAssertions()
        {
            string path = Write("player.json", PlayerJson("Windows64", true));
            Assert.That(Validate(RagdollEvidenceKind.WindowsPlayerScenario,
                path, new RagdollArtifactValidationContext()).IsValid, Is.True);

            path = Write("linux.json", PlayerJson("Windows64", true));
            RagdollArtifactValidationResult wrongPlatform = Validate(
                RagdollEvidenceKind.LinuxPlayerScenario, path,
                new RagdollArtifactValidationContext());
            Assert.That(wrongPlatform.IsValid, Is.False);
            Assert.That(wrongPlatform.Reason, Is.EqualTo("PlayerPlatformMismatch"));
        }

        [Test]
        public void Player_RejectsCapabilityLabelsWithoutExecutedAssertions()
        {
            string path = Write("player.json",
                "{\"schemaVersion\":3,\"succeeded\":true,\"platform\":\"Windows64\","
                + "\"capabilityIds\":[\"B01\"],\"scenarios\":[]}");
            Assert.That(Validate(RagdollEvidenceKind.WindowsPlayerScenario,
                path, new RagdollArtifactValidationContext()).IsValid, Is.False);
        }

        [Test]
        public void Player_RejectsGenericAssertionsWithoutScenarioSemantics()
        {
            string[] names =
            {
                "CoreLifecycle", "HumanoidBakerFall",
                "HierarchyProps", "CollisionsPerformance"
            };
            string scenarios = "[" + string.Join(",",
                Array.ConvertAll(names, name => "{\"name\":\"" + name
                    + "\",\"succeeded\":true,\"frames\":600,"
                    + "\"assertions\":[{\"name\":\"completed\","
                    + "\"succeeded\":true}]}")) + "]";
            string path = Write("generic-player.json",
                "{\"schemaVersion\":3,\"succeeded\":true,"
                + "\"platform\":\"Windows64\",\"scenarios\":"
                + scenarios + "}");
            RagdollArtifactValidationResult result = Validate(
                RagdollEvidenceKind.WindowsPlayerScenario, path, null);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason,
                Does.StartWith("PlayerScenarioSemanticAssertionMissing:"));
        }

        [Test]
        public void Player_RejectsSemanticIdWithContradictoryMetric()
        {
            string forged = PlayerJson("Windows64", true).Replace(
                "\"actual\":1", "\"actual\":0");
            string path = Write("forged-semantic-player.json", forged);

            RagdollArtifactValidationResult result = Validate(
                RagdollEvidenceKind.WindowsPlayerScenario, path, null);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason,
                Does.StartWith("PlayerScenarioSemanticMetricInvalid:"));
        }

        [Test]
        public void Player_PerformanceGateRequiresCompleteMeasuredMatrix()
        {
            string path = Write("performance-player.json",
                PerformancePlayerJson(true));
            Assert.That(Validate(RagdollEvidenceKind.WindowsPlayerScenario,
                path, new RagdollArtifactValidationContext
                {
                    ExpectedCapabilityId = "H08"
                }).IsValid, Is.True);

            path = Write("incomplete-performance-player.json",
                PerformancePlayerJson(false));
            Assert.That(Validate(RagdollEvidenceKind.WindowsPlayerScenario,
                path, new RagdollArtifactValidationContext
                {
                    ExpectedCapabilityId = "H08"
                }).IsValid, Is.False);
        }

        [Test]
        public void J05_ProfilerArtifactRequiresWarmupStatisticsAndZeroCriticalGc()
        {
            string path = Write("profiler.json", ProfilerJson(0));
            Assert.That(Validate(RagdollEvidenceKind.ProfilerResult,
                path, null).IsValid, Is.True);

            path = Write("allocated.json", ProfilerJson(16));
            RagdollArtifactValidationResult allocated = Validate(
                RagdollEvidenceKind.ProfilerResult, path, null);
            Assert.That(allocated.IsValid, Is.False);
            Assert.That(allocated.Reason,
                Does.StartWith("ProfilerCriticalPathAllocated:"));
        }

        [Test]
        public void Profiler_RejectsInventedScopeAndMissingDistributionSamples()
        {
            string path = Write("wrong-scope.json", ProfilerJson(0).Replace(
                "RagdollAnimator.DoAnimationMatching", "invented-scope"));
            Assert.That(Validate(RagdollEvidenceKind.ProfilerResult,
                path, null).Reason,
                Is.EqualTo("ProfilerCriticalPathScopeMismatch:matching"));

            path = Write("missing-samples.json", ProfilerJson(0).Replace(
                "\"sampleCount\":600", "\"sampleCount\":0"));
            Assert.That(Validate(RagdollEvidenceKind.ProfilerResult,
                path, null).Reason, Is.EqualTo("ProfilerCpuSampleCountInvalid"));

            path = Write("forged-aggregate.json", ProfilerJson(0).Replace(
                "\"median\":1", "\"median\":1.25"));
            Assert.That(Validate(RagdollEvidenceKind.ProfilerResult,
                path, null).Reason, Is.EqualTo("ProfilerCpuAggregateMismatch"));

            path = Write("missing-raw.json", ProfilerJson(0).Replace(
                "\"rawSamples\":[", "\"ignoredRawSamples\":["));
            Assert.That(Validate(RagdollEvidenceKind.ProfilerResult,
                path, null).Reason, Is.EqualTo("ProfilerCpuRawSamplesInvalid"));
        }

        [Test]
        public void J01_BuildReportRequiresAllDevelopmentTargetsAndRejectsOwnedWarnings()
        {
            string path = Write("builds.json", BuildJson(false));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).IsValid, Is.True);

            path = Write("own-warning.json", BuildJson(true));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).IsValid, Is.False);

            path = Write("missing-traversal.json", BuildJson(false).Replace(
                "\"stepsScanned\":1", "\"stepsScanned\":0"));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).Reason,
                Does.StartWith("BuildReportTraversalMissing:"));

            path = Write("forged-traversal.json", BuildJson(false).Replace(
                "\"message\":\"external\"",
                "\"message\":\"changed external\""));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).Reason,
                Does.StartWith("BuildReportTraversalDigestMismatch:"));
        }

        [Test]
        public void J04_SceneArtifactRequiresFourExecutedRegressionScenes()
        {
            string path = Write("scenes.json", "{\"schemaVersion\":3,\"succeeded\":true,\"scenes\":"
                + ScenarioArray(true) + "}");
            Assert.That(Validate(RagdollEvidenceKind.SceneArtifact,
                path, null).IsValid, Is.True);

            path = Write("missing.json", "{\"schemaVersion\":3,\"succeeded\":true,\"scenes\":[]}");
            Assert.That(Validate(RagdollEvidenceKind.SceneArtifact,
                path, null).IsValid, Is.False);
        }

        [Test]
        public void J07_DocumentationAuditRequiresOfficialSourceAndExecutableMapping()
        {
            string path = GenerateDocumentationAudit();
            Assert.That(Validate(RagdollEvidenceKind.DocumentationAudit, path,
                new RagdollArtifactValidationContext { ExpectedCapabilityId = "J07" })
                .IsValid, Is.True);

            TestDocumentationEvidence evidence = ReadDocumentation(path);
            TestDocumentationMapping[] complete = evidence.entries[0].members;

            string documentHash = evidence.documentSha256;
            evidence.documentSha256 = new string('0', 64);
            AssertInvalid(evidence, "stale-document.json",
                "DocumentationHashMismatch");
            evidence.documentSha256 = documentHash;

            string entrySource = evidence.entries[0].sourceUrl;
            evidence.entries[0].sourceUrl = "https://example.com/not-official";
            AssertInvalid(evidence, "unofficial-source.json",
                "DocumentationSourceNotOfficial");
            evidence.entries[0].sourceUrl = entrySource;

            evidence.entries[0].members = complete.Take(complete.Length - 1).ToArray();
            AssertInvalid(evidence, "missing-row.json",
                "DocumentationInventoryCountMismatch");

            evidence.entries[0].members = complete;
            string originalId = complete[0].memberId;
            complete[0].memberId = complete[1].memberId;
            AssertInvalid(evidence, "duplicate-row.json",
                "DocumentationInventoryIdentityMissingOrDuplicate");
            complete[0].memberId = originalId;

            TestDocumentationMapping migration = complete.First(mapping =>
                mapping.inventoryKind == "SerializationMigration");
            string oldName = migration.oldSerializedName;
            migration.oldSerializedName = oldName + "-wrong";
            AssertInvalid(evidence, "wrong-old-name.json",
                "DocumentationMemberMappingMismatch:");
            migration.oldSerializedName = oldName;

            string sourceHash = complete[0].sourceSha256;
            complete[0].sourceSha256 = new string('0', 64);
            AssertInvalid(evidence, "stale-source.json",
                "DocumentationMemberSourceHashMismatch:");
            complete[0].sourceSha256 = sourceHash;

            string affectedApi = evidence.entries[0].affectedApi;
            evidence.entries[0].affectedApi = affectedApi.Substring(
                affectedApi.IndexOf(',') + 1).Trim();
            AssertInvalid(evidence, "missing-api.json",
                "DocumentationApiInventoryMismatch");
        }

        [Test]
        public void DocumentationInventoryExhaustsRuntimeCompatibilityAndMigrations()
        {
            string root = PackageRoot();
            RagdollDocumentationContractItem[] inventory;
            string error;
            Assert.That(RagdollDocumentationContractInventory.TryBuild(
                root, out inventory, out error), Is.True, error);

            string[] runtimeSources = Directory.GetFiles(
                    root, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string normalized = path.Replace('\\', '/');
                    return normalized.Contains("/Runtime/")
                        && !normalized.Contains("/Tests/")
                        && !normalized.Contains("/Samples~/");
                })
                .ToArray();
            int migrationDeclarations = runtimeSources.Sum(path =>
                CountOccurrences(File.ReadAllText(path), "[FormerlySerializedAs("));
            int compatibilityDeclarations = runtimeSources
                .Where(path => !path.EndsWith(
                    "RagdollCompatibilityApiAttribute.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Sum(path => CountOccurrences(File.ReadAllText(path),
                    "[RagdollCompatibilityApi("));

            Assert.That(inventory.Count(item =>
                item.InventoryKind == "SerializationMigration"),
                Is.EqualTo(migrationDeclarations));
            Assert.That(inventory.Count(item =>
                item.InventoryKind == "CompatibilityApi"),
                Is.EqualTo(compatibilityDeclarations));
            Assert.That(inventory.Select(item => item.MemberId).Distinct().Count(),
                Is.EqualTo(inventory.Length));
        }

        string GenerateDocumentationAudit()
        {
            MethodInfo method = typeof(HairibarCertification).GetMethod(
                "WriteDocumentationAudit",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            string path = Path.Combine(directory, "documentation-audit.json");
            string previous = Environment.GetEnvironmentVariable(
                "HAIRIBAR_DOCUMENTATION_AUDIT");
            try
            {
                Environment.SetEnvironmentVariable(
                    "HAIRIBAR_DOCUMENTATION_AUDIT", path);
                method.Invoke(null, new object[] { directory });
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "HAIRIBAR_DOCUMENTATION_AUDIT", previous);
            }
            Assert.That(File.Exists(path), Is.True);
            return path;
        }

        TestDocumentationEvidence ReadDocumentation(string path)
        {
            TestDocumentationEvidence evidence =
                JsonUtility.FromJson<TestDocumentationEvidence>(
                    File.ReadAllText(path));
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.entries, Has.Length.EqualTo(1));
            return evidence;
        }

        void AssertInvalid(
            TestDocumentationEvidence evidence,
            string name,
            string reasonPrefix)
        {
            string path = Write(name, JsonUtility.ToJson(evidence, true));
            RagdollArtifactValidationResult validation = Validate(
                RagdollEvidenceKind.DocumentationAudit, path,
                new RagdollArtifactValidationContext
                {
                    ExpectedCapabilityId = "J07"
                });
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Reason, Does.StartWith(reasonPrefix));
        }

        static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset,
                StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        static string PackageRoot()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(RagdollAnimator).Assembly);
            Assert.That(package, Is.Not.Null);
            return Path.GetFullPath(package.resolvedPath);
        }

        RagdollArtifactValidationResult Validate(
            RagdollEvidenceKind kind,
            string path,
            RagdollArtifactValidationContext context)
        {
            return RagdollEvidenceArtifactValidators.Validate(
                new RagdollEvidenceArtifact { kind = kind, path = path }, context);
        }

        static RagdollArtifactValidationContext Context(string test)
        {
            return new RagdollArtifactValidationContext { ExactTestName = test };
        }

        string Write(string name, string contents)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, contents);
            return path;
        }

        static string PlayerJson(string platform, bool assertions)
        {
            return "{\"schemaVersion\":3,\"succeeded\":true,\"platform\":\""
                + platform + "\",\"scenarios\":" + ScenarioArray(assertions) + "}";
        }

        static string ScenarioArray(bool assertions)
        {
            string[] names =
            {
                "CoreLifecycle", "HumanoidBakerFall",
                "HierarchyProps", "CollisionsPerformance"
            };
            string[] values = new string[names.Length];
            for (int index = 0; index < names.Length; index++)
                values[index] = "{\"name\":\"" + names[index]
                    + "\",\"succeeded\":true,\"frames\":600,\"assertions\":"
                    + (assertions ? SemanticAssertions(names[index]) : "[]") + "}";
            return "[" + string.Join(",", values) + "]";
        }

        static string PerformancePlayerJson(bool complete)
        {
            int[] populations = { 1, 10, 25, 50 };
            string[] modes =
            {
                "ActiveTree", "ActiveFlat", "Kinematic", "Disabled"
            };
            var cells = new System.Collections.Generic.List<string>();
            foreach (int population in populations)
            foreach (string mode in modes)
            {
                if (!complete && population == 50 && mode == "Disabled")
                    continue;
                cells.Add("{\"puppets\":" + population + ",\"mode\":\""
                    + mode + "\",\"cpuMedianNanoseconds\":1,"
                    + "\"cpuP95Nanoseconds\":2,\"memoryMedianBytes\":3,"
                    + "\"memoryP95Bytes\":4,\"maximumGcAllocatedInFrame\":0,"
                    + "\"measuredFrames\":600}");
            }
            string performanceAssertions =
                SemanticAssertions("CollisionsPerformance");
            return "{\"schemaVersion\":3,\"succeeded\":true,"
                + "\"platform\":\"Windows64\",\"scenarios\":["
                + "{\"name\":\"CoreLifecycle\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":" + SemanticAssertions("CoreLifecycle") + "},"
                + "{\"name\":\"HumanoidBakerFall\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":" + SemanticAssertions("HumanoidBakerFall") + "},"
                + "{\"name\":\"HierarchyProps\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":" + SemanticAssertions("HierarchyProps") + "},"
                + "{\"name\":\"CollisionsPerformance\",\"succeeded\":true,"
                + "\"frames\":600,\"assertions\":" + performanceAssertions
                + ",\"performance\":[" + string.Join(",", cells) + "]}]}";
        }

        static string SemanticAssertions(string scenario)
        {
            string[] names;
            switch (scenario)
            {
                case "CoreLifecycle": names = new[]
                {
                    "core.physx-fall-distance", "core.saturated-contact-count",
                    "core.respawn-position-error", "core.manual-simulation-completed",
                    "core.joint-break-irreversible"
                }; break;
                case "HumanoidBakerFall": names = new[]
                {
                    "humanoid.valid-avatar-count", "humanoid.fall-initialized",
                    "humanoid.ik-owned-solver-count",
                    "humanoid.animation-event-count", "humanoid.root-motion-distance",
                    "humanoid.baker-clip-length"
                }; break;
                case "HierarchyProps": names = new[]
                {
                    "props.pickup-held", "props.collection-held",
                    "props.additional-pin-preserved", "props.rollback-exact",
                    "props.drop-empty"
                }; break;
                default: names = new[]
                {
                    "performance.flatten-hierarchy",
                    "performance.tree-hierarchy"
                }; break;
            }
            return "[" + string.Join(",", Array.ConvertAll(names, name =>
                "{\"id\":\"" + name + "\",\"name\":\"" + name
                + "\",\"succeeded\":true,\"comparison\":\"Equal\","
                + "\"actual\":1,\"expected\":1,\"tolerance\":0}")) + "]";
        }

        static string ProfilerJson(long allocation)
        {
            string[] names =
            {
                "matching", "mapping", "collision-relay", "com",
                "additional-pin", "baker-realtime"
            };
            string[] paths = new string[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                string[] raw = Enumerable.Repeat("0", 600).ToArray();
                if (index == 0 && allocation != 0) raw[0] = allocation.ToString();
                paths[index] = "{\"name\":\"" + names[index]
                    + "\",\"measurementScope\":\"" + CriticalScope(names[index]) + "\","
                    + "\"succeeded\":true,\"samples\":600,"
                    + "\"gcAllocatedBytes\":" + (index == 0 ? allocation : 0)
                    + ",\"maxGcAllocatedBytesInFrame\":"
                    + (index == 0 ? allocation : 0)
                    + ",\"rawAllocationSamples\":[" + string.Join(",", raw) + "]}";
            }
            string[] cpu = Enumerable.Repeat("1", 300)
                .Concat(Enumerable.Repeat("2", 300)).ToArray();
            string[] memory = Enumerable.Repeat("100", 300)
                .Concat(Enumerable.Repeat("150", 300)).ToArray();
            return "{\"schemaVersion\":3,\"succeeded\":true,\"warmupFrames\":120,"
                + "\"measuredFrames\":600,\"cpuMilliseconds\":{\"median\":1,\"p95\":2,\"sampleCount\":600,\"rawSamples\":[" + string.Join(",", cpu) + "]},"
                + "\"memoryBytes\":{\"median\":100,\"p95\":150,\"sampleCount\":600,\"rawSamples\":[" + string.Join(",", memory) + "]},\"criticalPaths\":["
                + string.Join(",", paths) + "]}";
        }

        static string CriticalScope(string name)
        {
            switch (name)
            {
                case "matching": return "RagdollAnimator.DoAnimationMatching";
                case "mapping": return "RagdollAnimator.MapRagdollToTarget";
                case "collision-relay": return "RagdollCollisionHub.Dispatch";
                case "com": return "RagdollCenterOfMassSubBehaviour.FixedUpdate";
                case "additional-pin":
                    return "RagdollPropMuscle.ApplyAdditionalPinAfterAnimationMatching";
                default: return "RagdollBaker.AdvanceRealtimeSampling";
            }
        }

        string BuildJson(bool ownWarning)
        {
            string[] targets = { "Windows64", "Linux64", "macOS", "WebGL" };
            string[] builds = new string[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                string output = Path.Combine(directory, "build-" + targets[index]);
                if (targets[index] == "WebGL" || targets[index] == "macOS")
                    Directory.CreateDirectory(output);
                else
                    File.WriteAllText(output, "player");
                string escapedOutput = output.Replace("\\", "\\\\");
                string diagnostics = index == 0 && ownWarning
                    ? "[{\"severity\":\"Warning\",\"own\":true,\"message\":\"Hairibar warning\"}]"
                    : "[{\"severity\":\"Warning\",\"own\":false,\"message\":\"external\"}]";
                string rawMessage = index == 0 && ownWarning
                    ? "Hairibar warning" : "external";
                string traversal = "0|Build|1\n0|Warning|" + rawMessage + "\n";
                string digest = Sha256Text(traversal);
                builds[index] = "{\"target\":\"" + targets[index]
                    + "\",\"result\":\"Succeeded\",\"succeeded\":true,"
                    + "\"development\":true,\"allowDebugging\":true,\"outputExists\":true,"
                    + "\"output\":\"" + escapedOutput + "\","
                    + "\"stepsScanned\":1,\"messagesScanned\":1,"
                    + "\"reportTraversalSha256\":\"" + digest + "\","
                    + "\"reportSteps\":[{\"name\":\"Build\",\"messages\":[{"
                    + "\"severity\":\"Warning\",\"message\":\"" + rawMessage
                    + "\"}]}],\"diagnostics\":" + diagnostics + "}";
            }
            return "{\"schemaVersion\":3,\"builds\":["
                + string.Join(",", builds) + "]}";
        }

        static string Sha256Text(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2")));
        }

        [Serializable]
        sealed class TestDocumentationEvidence
        {
            public int schemaVersion;
            public bool succeeded;
            public string documentPath;
            public string documentSha256;
            public TestDocumentationEntry[] entries;
        }

        [Serializable]
        sealed class TestDocumentationEntry
        {
            public string id;
            public string sourceUrl;
            public string affectedApi;
            public string exactTest;
            public string[] artifactKinds;
            public bool audited;
            public int compatibilityApiCount;
            public int serializationMigrationCount;
            public string inventorySha256;
            public TestDocumentationMapping[] members;
        }

        [Serializable]
        sealed class TestDocumentationMapping
        {
            public string inventoryKind;
            public string memberId;
            public string symbol;
            public string declaringType;
            public string memberName;
            public string memberKind;
            public string documentationSection;
            public string officialSourceUrl;
            public string oldSerializedName;
            public string sourcePath;
            public string sourceSha256;
            public bool verified;
        }
    }
}
