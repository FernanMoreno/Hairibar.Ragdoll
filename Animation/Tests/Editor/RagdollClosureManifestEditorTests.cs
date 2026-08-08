using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollClosureManifestEditorTests
    {
        [Test]
        public void A05_GuidedSetupIsOneUndoableTransaction()
        {
            var tests = new RagdollDualRigSetupWindowTests();
            tests.SetUp();
            try { tests.CompleteSetup_UndoRedoRestoresWholeTransaction(); }
            finally { tests.TearDown(); }
        }

        [Test]
        public void A06_ColliderSceneEditingPreservesSettingsAndUndo()
        {
            var tests = new RagdollAuthoredRigEditorTests();
            tests.SetUp();
            try { tests.BoxCapsuleSphereBox_PreservesCommonSettingsAndUndo(); }
            finally { tests.TearDown(); }
        }

        [Test]
        public void A08_InvalidAxisOrBindingInputRollsBackBeforeMutation()
        {
            var colliderTests = new RagdollAuthoredRigEditorTests();
            colliderTests.SetUp();
            try { colliderTests.UnsupportedType_IsRejectedBeforeMutation(); }
            finally { colliderTests.TearDown(); }

            var setupTests = new RagdollDualRigSetupWindowTests();
            setupTests.SetUp();
            try { setupTests.CompleteSetup_InvalidBindingLeavesNoPartialState(); }
            finally { setupTests.TearDown(); }
        }

        [Test]
        public void B30_AllRegressionScenesHaveDeterministicRunner()
        {
            string[] names =
            {
                "CoreLifecycle", "HumanoidBakerFall",
                "HierarchyProps", "CollisionsPerformance"
            };
            const string certificationRoot =
                "Assets/__HairibarCertification/Regression";
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            bool hasRestorableActiveScene = previousSetup.Any(
                setup => setup.isLoaded && setup.isActive);
            try
            {
                foreach (string name in names)
                {
                    string[] scenes = AssetDatabase.FindAssets(
                        name + " t:Scene", new[] { certificationRoot });
                    Assert.That(scenes, Has.Length.EqualTo(1), name);

                    string path = AssetDatabase.GUIDToAssetPath(scenes[0]);
                    Scene scene = EditorSceneManager.OpenScene(
                        path, OpenSceneMode.Additive);
                    int runnerCount = 0;
                    int missingScriptCount = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        missingScriptCount += GameObjectUtility
                            .GetMonoBehavioursWithMissingScriptCount(root);
                        MonoBehaviour[] behaviours = root
                            .GetComponentsInChildren<MonoBehaviour>(true);
                        for (int index = 0; index < behaviours.Length; index++)
                        {
                            MonoBehaviour behaviour = behaviours[index];
                            if (behaviour && behaviour.GetType().FullName ==
                                "Hairibar.Ragdoll.Demo.RegressionScenarioRunner")
                            {
                                runnerCount++;
                            }
                        }
                    }
                    Assert.That(missingScriptCount, Is.Zero,
                        name + " contains a missing MonoBehaviour script.");
                    Assert.That(runnerCount, Is.EqualTo(1),
                        name + " must contain exactly one deterministic runner.");
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                if (hasRestorableActiveScene)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else if (SceneManager.sceneCount == 0)
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
            Assert.That(
                AssetDatabase.FindAssets("RegressionScenarioRunner t:MonoScript"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void CoverageManifest_DoesNotTrustHistoricalMarkdownStatuses()
        {
            RagdollCoverageManifest.Manifest manifest =
                RagdollCoverageManifest.Build();

            Assert.That(manifest.total, Is.EqualTo(140));
            Assert.That(manifest.notApplicable, Is.EqualTo(1));
            Assert.That(manifest.entries,
                Has.Exactly(1).Matches<RagdollCoverageManifest.Entry>(entry =>
                    entry.id == "G05" && entry.status == "N/A"));
            Assert.That(manifest.verified, Is.Zero,
                "A discovered test is not verified without an execution artifact.");
            Assert.That(manifest.open, Is.EqualTo(139));
        }

        [Test]
        public void CoverageManifest_VerifiesOnlyExactPassedTestFromHashedArtifact()
        {
            string exact = RagdollCoverageManifest.Build().entries
                .Single(value => value.id == "B08").exactTest;
            Assert.That(exact, Is.Not.Empty,
                "The exact executable B08 test must be discoverable before its artifact can verify it.");
            string path = Path.Combine(
                Path.GetTempPath(),
                "hairibar-coverage-" + System.Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(path,
                    "<test-run><test-suite><test-case fullname=\""
                    + exact + "\" result=\"Passed\" /></test-suite></test-run>");

                RagdollCoverageManifest.Manifest manifest =
                    RagdollCoverageManifest.Build(path);
                RagdollCoverageManifest.Entry entry = manifest.entries
                    .Single(value => value.id == "B08");

                Assert.That(manifest.verified, Is.EqualTo(1));
                Assert.That(manifest.open, Is.EqualTo(138));
                Assert.That(entry.status, Is.EqualTo("Verified"));
                Assert.That(entry.exactTest, Is.EqualTo(exact));
                Assert.That(entry.affectedApi,
                    Is.EqualTo(RagdollCapabilityCatalog.Get("B08").AffectedApis));
                Assert.That(entry.executionArtifact,
                    Is.EqualTo(Path.GetFullPath(path)));
                Assert.That(entry.executionArtifactSha256,
                    Has.Length.EqualTo(64));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestAcceptsCurrentMatchingArtifact()
        {
            using (StrictEvidenceFixture fixture = StrictEvidenceFixture.Create("Passed"))
            {
                RagdollCoverageManifest.Manifest manifest =
                    RagdollCoverageManifest.Build(fixture.Request);
                RagdollCoverageManifest.Entry entry = Entry(manifest, "B08");

                Assert.That(entry.status, Is.EqualTo("Verified"));
                Assert.That(entry.executionArtifact,
                    Is.EqualTo(Path.GetFullPath(fixture.XmlPath)));
                Assert.That(entry.executionArtifactSha256,
                    Is.EqualTo(fixture.Artifact.sha256));
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestRejectsMissingArtifact()
        {
            using (StrictEvidenceFixture fixture = StrictEvidenceFixture.Create("Passed"))
            {
                File.Delete(fixture.XmlPath);

                RagdollCoverageManifest.Entry entry = Entry(
                    RagdollCoverageManifest.Build(fixture.Request), "B08");

                Assert.That(entry.status, Is.EqualTo("Open"));
                Assert.That(entry.reason, Does.Contain("exist").IgnoreCase);
                Assert.That(entry.executionArtifact, Is.Empty);
                Assert.That(entry.executionArtifactSha256, Is.Empty);
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestRejectsArtifactOlderThanSource()
        {
            using (StrictEvidenceFixture fixture = StrictEvidenceFixture.Create("Passed"))
            {
                fixture.Artifact.generatedUtc = DateTime.UtcNow
                    .AddYears(-10).ToString("O");

                RagdollCoverageManifest.Entry entry = Entry(
                    RagdollCoverageManifest.Build(fixture.Request), "B08");

                Assert.That(entry.status, Is.EqualTo("Open"));
                Assert.That(entry.reason, Does.Contain("stale").IgnoreCase
                    .Or.Contain("older").IgnoreCase);
                Assert.That(entry.executionArtifact, Is.Empty);
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestRejectsArtifactHashMismatch()
        {
            using (StrictEvidenceFixture fixture = StrictEvidenceFixture.Create("Passed"))
            {
                fixture.Artifact.sha256 = new string('0', 64);

                RagdollCoverageManifest.Entry entry = Entry(
                    RagdollCoverageManifest.Build(fixture.Request), "B08");

                Assert.That(entry.status, Is.EqualTo("Open"));
                Assert.That(entry.reason, Does.Contain("hash").IgnoreCase
                    .Or.Contain("SHA-256").IgnoreCase);
                Assert.That(entry.executionArtifact, Is.Empty);
                Assert.That(entry.executionArtifactSha256, Is.Empty);
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestRejectsMismatchedSourceProvenance()
        {
            using (StrictEvidenceFixture fixture = StrictEvidenceFixture.Create("Passed"))
            {
                fixture.Artifact.sourceTreeSha256 = new string('f', 64);

                RagdollCoverageManifest.Entry entry = Entry(
                    RagdollCoverageManifest.Build(fixture.Request), "B08");

                Assert.That(entry.status, Is.EqualTo("Open"));
                Assert.That(entry.reason, Does.Contain("source").IgnoreCase
                    .Or.Contain("provenance").IgnoreCase);
            }
        }

        [TestCase("Skipped")]
        [TestCase("Inconclusive")]
        public void CoverageManifest_StrictRequestRejectsNonPassedNUnitResult(
            string result)
        {
            using (StrictEvidenceFixture fixture = StrictEvidenceFixture.Create(result))
            {
                RagdollCoverageManifest.Entry entry = Entry(
                    RagdollCoverageManifest.Build(fixture.Request), "B08");

                Assert.That(entry.status, Is.EqualTo("Open"));
                Assert.That(entry.reason, Does.Contain(result).IgnoreCase
                    .Or.Contain("not passed").IgnoreCase);
                Assert.That(entry.executionArtifact, Is.Empty);
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestDeduplicatesIdenticalNUnitCase()
        {
            using (StrictEvidenceFixture first = StrictEvidenceFixture.Create("Passed"))
            using (StrictEvidenceFixture second = StrictEvidenceFixture.Create(
                "Passed", first.ExactTest))
            {
                var request = new RagdollCoverageRequest
                {
                    nunitResultPaths = new[] { first.XmlPath, second.XmlPath },
                    artifacts = new[] { first.Artifact, second.Artifact },
                    sourceRevision = first.Request.sourceRevision,
                    sourceTreeSha256 = first.Request.sourceTreeSha256,
                    sourceLatestWriteUtc = first.Request.sourceLatestWriteUtc
                };

                RagdollCoverageManifest.Entry entry = Entry(
                    RagdollCoverageManifest.Build(request), "B08");

                Assert.That(entry.status, Is.EqualTo("Verified"));
            }
        }

        [Test]
        public void CoverageManifest_StrictRequestRejectsContradictoryNUnitCase()
        {
            using (StrictEvidenceFixture passed = StrictEvidenceFixture.Create("Passed"))
            using (StrictEvidenceFixture failed = StrictEvidenceFixture.Create(
                "Failed", passed.ExactTest))
            {
                var request = new RagdollCoverageRequest
                {
                    nunitResultPaths = new[] { passed.XmlPath, failed.XmlPath },
                    artifacts = new[] { passed.Artifact, failed.Artifact },
                    sourceRevision = passed.Request.sourceRevision,
                    sourceTreeSha256 = passed.Request.sourceTreeSha256,
                    sourceLatestWriteUtc = passed.Request.sourceLatestWriteUtc
                };

                Exception exception = Assert.Throws<InvalidDataException>(
                    () => RagdollCoverageManifest.Build(request));
                Assert.That(exception.Message,
                    Does.Contain("conflict").IgnoreCase
                        .Or.Contain("contradict").IgnoreCase);
            }
        }

        [Test]
        public void CoverageManifest_LegacySingleXmlOverloadRemainsCompatible()
        {
            string exact = FindExactTest("B08");
            string path = WriteNUnitArtifact(exact, "Passed");
            try
            {
                RagdollCoverageManifest.Manifest manifest =
                    RagdollCoverageManifest.Build(path);

                Assert.That(Entry(manifest, "B08").status,
                    Is.EqualTo("Verified"));
                Assert.That(manifest.testResultsArtifact,
                    Is.EqualTo(Path.GetFullPath(path)));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        static RagdollCoverageManifest.Entry Entry(
            RagdollCoverageManifest.Manifest manifest,
            string id)
        {
            return manifest.entries.Single(value => value.id == id);
        }

        static string FindExactTest(string id)
        {
            string exact = RagdollCoverageManifest.Build().entries
                .Single(value => value.id == id).exactTest;
            Assert.That(exact, Is.Not.Empty,
                "The direct executable test must be discoverable.");
            return exact;
        }

        static string WriteNUnitArtifact(string exactTest, string result)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "hairibar-coverage-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path,
                "<test-run result=\"" + (result == "Passed" ? "Passed" : "Failed")
                + "\" total=\"1\" passed=\"" + (result == "Passed" ? "1" : "0")
                + "\" failed=\"" + (result == "Failed" ? "1" : "0")
                + "\" skipped=\"" + (result == "Skipped" ? "1" : "0")
                + "\" inconclusive=\"" + (result == "Inconclusive" ? "1" : "0")
                + "\" warnings=\"0\"><test-suite><test-case fullname=\""
                + exactTest + "\" result=\"" + result
                + "\" /></test-suite></test-run>");
            return path;
        }

        static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        sealed class StrictEvidenceFixture : IDisposable
        {
            StrictEvidenceFixture()
            {
            }

            public string XmlPath { get; private set; }
            public string ExactTest { get; private set; }
            public DateTime GeneratedUtc { get; private set; }
            public RagdollEvidenceArtifact Artifact {
                get;
                private set;
            }
            public RagdollCoverageRequest Request {
                get;
                private set;
            }

            public static StrictEvidenceFixture Create(
                string result,
                string exactTest = null)
            {
                var fixture = new StrictEvidenceFixture();
                fixture.ExactTest = exactTest ?? FindExactTest("B08");
                fixture.XmlPath = WriteNUnitArtifact(fixture.ExactTest, result);
                fixture.GeneratedUtc = DateTime.UtcNow.AddMinutes(1);
                const string revision = "0123456789abcdef";
                string treeHash = RagdollCoverageManifest
                    .ComputeCurrentSourceTreeSha256();
                fixture.Artifact = new RagdollEvidenceArtifact
                {
                    kind = RagdollEvidenceKind.NUnitPlayMode,
                    path = fixture.XmlPath,
                    sha256 = Sha256(fixture.XmlPath),
                    platform = "Editor",
                    scenario = "B08",
                    generatedUtc = fixture.GeneratedUtc.ToString("O"),
                    sourceRevision = revision,
                    sourceTreeSha256 = treeHash,
                    capabilityIds = new[] { "B08" }
                };
                fixture.Request = new RagdollCoverageRequest
                {
                    nunitResultPaths = new[] { fixture.XmlPath },
                    artifacts = new[] { fixture.Artifact },
                    sourceRevision = revision,
                    sourceTreeSha256 = treeHash,
                    sourceLatestWriteUtc = RagdollCoverageManifest
                        .CurrentSourceLatestWriteUtc()
                };
                return fixture;
            }

            public void Dispose()
            {
                if (File.Exists(XmlPath)) File.Delete(XmlPath);
            }
        }
    }
}
