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
        public void NUnit_RequiresExactCompletePassingParameterizedCases()
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
        public void Profiler_RequiresWarmupSamplesStatisticsAndZeroCriticalGc()
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
        public void BuildReport_RequiresAllDevelopmentTargetsAndRejectsOwnWarnings()
        {
            string path = Write("builds.json", BuildJson(false));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).IsValid, Is.True);

            path = Write("own-warning.json", BuildJson(true));
            Assert.That(Validate(RagdollEvidenceKind.BuildReport,
                path, null).IsValid, Is.False);
        }

        [Test]
        public void SceneArtifact_RequiresFourExecutedScenes()
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
        public void DocumentationAudit_RequiresOfficialSourceAndExecutableMapping()
        {
            string path = Write("docs.json",
                "{\"schemaVersion\":2,\"succeeded\":true,\"entries\":[{"
                + "\"id\":\"B01\",\"sourceUrl\":\"http://www.root-motion.com/puppetmasterdox/html/pages.html\","
                + "\"affectedApi\":\"RagdollAnimator\",\"exactTest\":\"Fixture.B01_Test\","
                + "\"artifactKinds\":[\"NUnitPlayMode\"],\"audited\":true}]}");
            Assert.That(Validate(RagdollEvidenceKind.DocumentationAudit, path,
                new RagdollArtifactValidationContext { ExpectedCapabilityId = "B01" })
                .IsValid, Is.True);

            string unofficial = File.ReadAllText(path).Replace(
                "www.root-motion.com", "example.com");
            path = Write("unofficial.json", unofficial);
            Assert.That(Validate(RagdollEvidenceKind.DocumentationAudit, path,
                new RagdollArtifactValidationContext { ExpectedCapabilityId = "B01" })
                .IsValid, Is.False);
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
                    + "\",\"succeeded\":true,\"samples\":600,"
                    + "\"gcAllocatedBytes\":" + (index == 0 ? allocation : 0)
                    + ",\"maxGcAllocatedBytesInFrame\":0}";
            return "{\"schemaVersion\":2,\"succeeded\":true,\"warmupFrames\":120,"
                + "\"measuredFrames\":600,\"cpuMilliseconds\":{\"median\":1,\"p95\":2},"
                + "\"memoryBytes\":{\"median\":100,\"p95\":150},\"criticalPaths\":["
                + string.Join(",", paths) + "]}";
        }

        static string BuildJson(bool ownWarning)
        {
            string[] targets = { "Windows64", "Linux64", "macOS", "WebGL" };
            string[] builds = new string[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                string diagnostics = index == 0 && ownWarning
                    ? "[{\"severity\":\"Warning\",\"own\":true,\"message\":\"Hairibar warning\"}]"
                    : "[{\"severity\":\"Warning\",\"own\":false,\"message\":\"external\"}]";
                builds[index] = "{\"target\":\"" + targets[index]
                    + "\",\"result\":\"Succeeded\",\"succeeded\":true,"
                    + "\"development\":true,\"allowDebugging\":true,\"outputExists\":true,"
                    + "\"diagnostics\":" + diagnostics + "}";
            }
            return "{\"schemaVersion\":2,\"builds\":["
                + string.Join(",", builds) + "]}";
        }
    }
}
