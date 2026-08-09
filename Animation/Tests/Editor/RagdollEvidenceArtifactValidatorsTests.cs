using System;
using System.IO;
using NUnit.Framework;

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
                "{\"schemaVersion\":2,\"succeeded\":true,\"platform\":\"Windows64\","
                + "\"capabilityIds\":[\"B01\"],\"scenarios\":[]}");
            Assert.That(Validate(RagdollEvidenceKind.WindowsPlayerScenario,
                path, new RagdollArtifactValidationContext()).IsValid, Is.False);
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
        public void J01_BuildReportRequiresAllDevelopmentTargetsAndRejectsOwnedWarnings()
        {
            string path = Write("builds.json", BuildJson(false));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).IsValid, Is.True);

            path = Write("own-warning.json", BuildJson(true));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).IsValid, Is.False);
        }

        [Test]
        public void J04_SceneArtifactRequiresFourExecutedRegressionScenes()
        {
            string path = Write("scenes.json", "{\"schemaVersion\":2,\"succeeded\":true,\"scenes\":"
                + ScenarioArray(true) + "}");
            Assert.That(Validate(RagdollEvidenceKind.SceneArtifact,
                path, null).IsValid, Is.True);

            path = Write("missing.json", "{\"schemaVersion\":2,\"succeeded\":true,\"scenes\":[]}");
            Assert.That(Validate(RagdollEvidenceKind.SceneArtifact,
                path, null).IsValid, Is.False);
        }

        [Test]
        public void J07_DocumentationAuditRequiresOfficialSourceAndExecutableMapping()
        {
            string package = Path.Combine(directory, "Package");
            string documentationDirectory = Path.Combine(
                package, "Documentation~", "Certification");
            string sourceDirectory = Path.Combine(package, "Animation", "Tests");
            Directory.CreateDirectory(documentationDirectory);
            Directory.CreateDirectory(sourceDirectory);
            string document = Path.Combine(documentationDirectory, "migration.md");
            File.WriteAllText(document,
                "## Test\nMasterMappingWeight http://www.root-motion.com/puppetmasterdox/html/pages.html");
            string sourcePath = Path.Combine(sourceDirectory, "Evidence.cs");
            File.WriteAllText(sourcePath,
                "class RagdollAnimator { float MasterMappingWeight; } // B01_Test "
                + "J07_DocumentationAuditRequiresOfficialSourceAndExecutableMapping");
            string escapedDocument = document.Replace("\\", "\\\\");
            string escapedSource = sourcePath.Replace("\\", "\\\\");
            string documentHash = RagdollClosurePipeline.ComputeSha256(document);
            string path = Write("docs.json",
                "{\"schemaVersion\":2,\"succeeded\":true,\"documentPath\":\""
                + escapedDocument + "\",\"documentSha256\":\"" + documentHash
                + "\",\"entries\":[{"
                + "\"id\":\"B01\",\"sourceUrl\":\"http://www.root-motion.com/puppetmasterdox/html/pages.html\","
                + "\"affectedApi\":\"MasterMappingWeight\",\"exactTest\":\"Fixture.B01_Test\","
                + "\"artifactKinds\":[\"NUnitPlayMode\"],\"audited\":true,"
                + "\"members\":[{\"symbol\":\"MasterMappingWeight\","
                + "\"declaringType\":\"Hairibar.Ragdoll.Animation.RagdollAnimator\","
                + "\"memberName\":\"MasterMappingWeight\",\"memberKind\":\"Property\","
                + "\"documentationSection\":\"Test\",\"sourcePath\":\""
                + escapedSource + "\",\"verified\":true}]}]}");
            Assert.That(Validate(RagdollEvidenceKind.DocumentationAudit, path,
                new RagdollArtifactValidationContext { ExpectedCapabilityId = "B01" })
                .IsValid, Is.True);

            string unofficial = File.ReadAllText(path).Replace(
                "www.root-motion.com", "example.com");
            path = Write("unofficial.json", unofficial);
            Assert.That(Validate(RagdollEvidenceKind.DocumentationAudit, path,
                new RagdollArtifactValidationContext { ExpectedCapabilityId = "B01" })
                .IsValid, Is.False);

            File.AppendAllText(document, " changed-after-audit");
            RagdollArtifactValidationResult stale = Validate(
                RagdollEvidenceKind.DocumentationAudit,
                Write("stale.json", File.ReadAllText(Write("original.json",
                    File.ReadAllText(path).Replace("example.com",
                        "www.root-motion.com")))),
                new RagdollArtifactValidationContext
                {
                    ExpectedCapabilityId = "B01"
                });
            Assert.That(stale.IsValid, Is.False);
            Assert.That(stale.Reason, Is.EqualTo("DocumentationHashMismatch"));
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
            return "{\"schemaVersion\":2,\"succeeded\":true,\"platform\":\""
                + platform + "\",\"scenarios\":" + ScenarioArray(assertions) + "}";
        }

        static string ScenarioArray(bool assertions)
        {
            string assertion = assertions
                ? "[{\"name\":\"completed\",\"succeeded\":true}]"
                : "[]";
            string[] names =
            {
                "CoreLifecycle", "HumanoidBakerFall",
                "HierarchyProps", "CollisionsPerformance"
            };
            string[] values = new string[names.Length];
            for (int index = 0; index < names.Length; index++)
                values[index] = "{\"name\":\"" + names[index]
                    + "\",\"succeeded\":true,\"frames\":600,\"assertions\":"
                    + assertion + "}";
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
                    + "\"memoryP95Bytes\":4,\"maximumGcAllocatedInFrame\":0}");
            }
            string standardAssertion =
                "[{\"name\":\"completed\",\"succeeded\":true}]";
            string performanceAssertions =
                "[{\"name\":\"FlattenHierarchy completed\",\"succeeded\":true},"
                + "{\"name\":\"TreeHierarchy completed\",\"succeeded\":true}]";
            return "{\"schemaVersion\":2,\"succeeded\":true,"
                + "\"platform\":\"Windows64\",\"scenarios\":["
                + "{\"name\":\"CoreLifecycle\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":" + standardAssertion + "},"
                + "{\"name\":\"HumanoidBakerFall\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":" + standardAssertion + "},"
                + "{\"name\":\"HierarchyProps\",\"succeeded\":true,"
                + "\"frames\":1,\"assertions\":" + standardAssertion + "},"
                + "{\"name\":\"CollisionsPerformance\",\"succeeded\":true,"
                + "\"frames\":600,\"assertions\":" + performanceAssertions
                + ",\"performance\":[" + string.Join(",", cells) + "]}]}";
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
                paths[index] = "{\"name\":\"" + names[index]
                    + "\",\"measurementScope\":\"exact-" + names[index] + "\","
                    + "\"succeeded\":true,\"samples\":600,"
                    + "\"gcAllocatedBytes\":" + (index == 0 ? allocation : 0)
                    + ",\"maxGcAllocatedBytesInFrame\":0}";
            return "{\"schemaVersion\":2,\"succeeded\":true,\"warmupFrames\":120,"
                + "\"measuredFrames\":600,\"cpuMilliseconds\":{\"median\":1,\"p95\":2},"
                + "\"memoryBytes\":{\"median\":100,\"p95\":150},\"criticalPaths\":["
                + string.Join(",", paths) + "]}";
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
                builds[index] = "{\"target\":\"" + targets[index]
                    + "\",\"result\":\"Succeeded\",\"succeeded\":true,"
                    + "\"development\":true,\"allowDebugging\":true,\"outputExists\":true,"
                    + "\"output\":\"" + escapedOutput + "\","
                    + "\"diagnostics\":" + diagnostics + "}";
            }
            return "{\"schemaVersion\":2,\"builds\":["
                + string.Join(",", builds) + "]}";
        }
    }
}
