using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Hairibar.Ragdoll;
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
            using (GuidedAuthoringClosureFixture fixture =
                GuidedAuthoringClosureFixture.Create())
            {
                Selection.activeGameObject = fixture.Root;
                RagdollAuthoredRig rig;
                string error;
                Assert.That(RagdollAuthoringWizard.TryCreateFromSelection(
                    Selection.activeGameObject,
                    RagdollAuthoringWizard.ReferenceMode.ExplicitGeneric,
                    null,
                    fixture.References,
                    RagdollAuthoringOptions.Default,
                    out rig,
                    out error), Is.True, error);
                Assert.That(rig, Is.Not.Null);
                Assert.That(rig.Rigidbodies, Has.Length.EqualTo(16));
                Assert.That(rig.Colliders, Has.Length.EqualTo(16));
                Assert.That(rig.Joints, Has.Length.EqualTo(16));
                Assert.That(rig.Rigidbodies.All(value => value), Is.True);
                Assert.That(rig.Colliders.All(value => value), Is.True);
                Assert.That(rig.Joints.All(value => value), Is.True);

                Undo.PerformUndo();
                Assert.That(fixture.Root.GetComponentInChildren<RagdollAuthoredRig>(true),
                    Is.Null);
                Assert.That(fixture.Root.GetComponentsInChildren<Rigidbody>(true),
                    Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<Collider>(true),
                    Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<ConfigurableJoint>(true),
                    Is.Empty);

                Undo.PerformRedo();
                rig = fixture.Root.GetComponentInChildren<RagdollAuthoredRig>(true);
                Assert.That(rig, Is.Not.Null);
                Assert.That(rig.Rigidbodies, Has.Length.EqualTo(16));
                Assert.That(rig.Colliders, Has.Length.EqualTo(16));
                Assert.That(rig.Joints, Has.Length.EqualTo(16));
            }
        }

        [Test]
        public void A06_ColliderSceneEditingPreservesSettingsAndUndo()
        {
            using (SymmetricAuthoredRigClosureFixture fixture =
                SymmetricAuthoredRigClosureFixture.Create())
            {
                SymmetricAuthoredRigClosureFixture.State before =
                    fixture.Capture();
                string error;
                Assert.That(fixture.Inspector.TryApplySymmetricEdit(
                    new Vector3(0.1f, 0.2f, 0.3f),
                    new Vector3(0.4f, 0.8f, 1.2f),
                    Vector3.forward,
                    Vector3.up,
                    -25f, 40f, 55f, 65f,
                    out error), Is.True, error);
                SymmetricAuthoredRigClosureFixture.State changed =
                    fixture.Capture();
                fixture.AssertSymmetricChanged(changed);

                Undo.PerformUndo();
                fixture.AssertState(before);
                Undo.PerformRedo();
                fixture.AssertState(changed);
            }
        }

        [Test]
        public void A08_InvalidAxisOrBindingInputRollsBackBeforeMutation()
        {
            using (AuthoredRigClosureFixture fixture =
                AuthoredRigClosureFixture.Create())
            {
                ConfigurableJoint joint = fixture.Rig.Joints[0];
                Vector3 originalAxis = joint.axis;
                Vector3 originalSecondary = joint.secondaryAxis;
                string error;
                Assert.That(fixture.Inspector.TrySetSelectedJointAxes(
                    Vector3.zero, Vector3.up, out error), Is.False);
                Assert.That(error, Does.Contain("non-zero"));
                Assert.That(joint.axis, Is.EqualTo(originalAxis));
                Assert.That(joint.secondaryAxis, Is.EqualTo(originalSecondary));
                Assert.That(fixture.Inspector.TrySetSelectedJointAxes(
                    Vector3.right, Vector3.right * 2f, out error), Is.False);
                Assert.That(error, Does.Contain("parallel"));
                Assert.That(joint.axis, Is.EqualTo(originalAxis));
                Assert.That(joint.secondaryAxis, Is.EqualTo(originalSecondary));
                GameObject external = new GameObject("External connected body");
                try
                {
                    Rigidbody externalBody = external.AddComponent<Rigidbody>();
                    Rigidbody originalConnection = joint.connectedBody;
                    Assert.That(fixture.Inspector.TrySetSelectedConnectedBody(
                        externalBody, out error), Is.False);
                    Assert.That(error, Does.Contain("authored ragdoll"));
                    Assert.That(joint.connectedBody, Is.SameAs(originalConnection));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(external);
                }
                Collider originalCollider = fixture.Rig.Colliders[0];
                Assert.Throws<ArgumentException>(() =>
                    fixture.Inspector.ConvertSelectedCollider(
                        typeof(MeshCollider)));
                Assert.That(fixture.Rig.Colliders[0], Is.SameAs(originalCollider));
            }
        }

        [Test]
        public void Legacy_AllRegressionScenesHaveDeterministicRunner()
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

        [Test]
        public void CoverageManifest_CrossModeRequirementNeedsDeclaredCompanionInItsOwnArtifact()
        {
            Assert.That(
                RagdollCapabilityCatalog.Get("I07").RequiredEvidence,
                Is.EquivalentTo(new[]
                {
                    RagdollEvidenceKind.NUnitEditMode,
                    RagdollEvidenceKind.NUnitPlayMode,
                    RagdollEvidenceKind.ProfilerResult
                }),
                "I07 must retain its cross-mode and scoped-profiler gates.");
            string editTest = FindExactTest("I07");
            const string playTest =
                "Hairibar.Ragdoll.Animation.Tests." +
                "RagdollBakerRealtimeFramePlayModeEvidence." +
                "RealtimeSamplesAtMostOncePerRenderedFrame";
            string editPath = WriteNUnitArtifact(editTest, "Passed");
            string playPath = WriteNUnitArtifact(playTest, "Passed");
            string profilerPath = Path.Combine(
                Path.GetTempPath(),
                "hairibar-profiler-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(profilerPath, ValidProfilerJson());
            try
            {
                string treeHash = RagdollCoverageManifest
                    .ComputeCurrentSourceTreeSha256();
                const string revision = "0123456789abcdef";
                string generatedUtc = DateTime.UtcNow.AddMinutes(1).ToString("O");
                RagdollEvidenceArtifact edit = Artifact(
                    RagdollEvidenceKind.NUnitEditMode,
                    editPath, revision, treeHash, generatedUtc, "I07");
                RagdollEvidenceArtifact play = Artifact(
                    RagdollEvidenceKind.NUnitPlayMode,
                    playPath, revision, treeHash, generatedUtc, "I07");
                RagdollEvidenceArtifact profiler = Artifact(
                    RagdollEvidenceKind.ProfilerResult,
                    profilerPath, revision, treeHash, generatedUtc, "I07");

                var editOnly = new RagdollCoverageRequest
                {
                    nunitResultPaths = new[] { editPath },
                    artifacts = new[] { edit, profiler },
                    sourceRevision = revision,
                    sourceTreeSha256 = treeHash,
                    sourceLatestWriteUtc = RagdollCoverageManifest
                        .CurrentSourceLatestWriteUtc()
                };
                RagdollCoverageManifest.Entry incomplete = Entry(
                    RagdollCoverageManifest.Build(editOnly), "I07");
                Assert.That(incomplete.status, Is.EqualTo("Open"),
                    "EditMode evidence must never satisfy PlayMode.");
                Assert.That(incomplete.reason, Is.Not.Empty);
                Assert.That(incomplete.evidenceArtifacts.Any(value =>
                    value.kind == RagdollEvidenceKind.NUnitPlayMode), Is.False);

                var complete = new RagdollCoverageRequest
                {
                    nunitResultPaths = new[] { editPath, playPath },
                    artifacts = new[] { edit, play, profiler },
                    sourceRevision = revision,
                    sourceTreeSha256 = treeHash,
                    sourceLatestWriteUtc = editOnly.sourceLatestWriteUtc
                };
                RagdollCoverageManifest.Entry verified = Entry(
                    RagdollCoverageManifest.Build(complete), "I07");
                Assert.That(verified.status, Is.EqualTo("Verified"),
                    verified.reason + " Evidence: " + string.Join(", ",
                        verified.evidenceArtifacts.Select(value =>
                            value.kind + "=" + value.validationStatus + "/"
                            + value.validationReason)));
                Assert.That(verified.evidenceArtifacts.Select(value => value.kind),
                    Is.EquivalentTo(new[]
                    {
                        RagdollEvidenceKind.NUnitEditMode,
                        RagdollEvidenceKind.NUnitPlayMode,
                        RagdollEvidenceKind.ProfilerResult
                    }));
            }
            finally
            {
                File.Delete(editPath);
                File.Delete(playPath);
                File.Delete(profilerPath);
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

        static RagdollEvidenceArtifact Artifact(
            RagdollEvidenceKind kind,
            string path,
            string revision,
            string treeHash,
            string generatedUtc,
            params string[] capabilityIds)
        {
            return new RagdollEvidenceArtifact
            {
                kind = kind,
                path = path,
                sha256 = Sha256(path),
                platform = kind == RagdollEvidenceKind.NUnitEditMode
                    || kind == RagdollEvidenceKind.NUnitPlayMode
                        ? "Editor" : "Player",
                scenario = "I07",
                generatedUtc = generatedUtc,
                sourceRevision = revision,
                sourceTreeSha256 = treeHash,
                capabilityIds = capabilityIds
            };
        }

        static string ValidProfilerJson()
        {
            string[] names =
            {
                "matching", "mapping", "collision-relay", "com",
                "additional-pin", "baker-realtime"
            };
            return "{\"schemaVersion\":2,\"succeeded\":true," +
                "\"warmupFrames\":120,\"measuredFrames\":600," +
                "\"cpuMilliseconds\":{\"median\":1,\"p95\":2}," +
                "\"memoryBytes\":{\"median\":100,\"p95\":150}," +
                "\"criticalPaths\":[" + string.Join(",", names.Select(name =>
                    "{\"name\":\"" + name +
                    "\",\"succeeded\":true,\"samples\":600," +
                    "\"measurementScope\":\"scope-" + name + "\"," +
                    "\"gcAllocatedBytes\":0," +
                    "\"maxGcAllocatedBytesInFrame\":0}")) + "]}";
        }

        sealed class DualRigClosureFixture : IDisposable
        {
            readonly bool ignoredBefore;
            readonly RagdollDefinition definition;
            readonly RagdollAnimationProfile profile;
            readonly RagdollDefinitionBindings bindings;
            GameObject setupRoot;

            DualRigClosureFixture(string targetChildName)
            {
                ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
                Physics.IgnoreLayerCollision(30, 31, false);
                profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();

                BoneName rootName = new BoneName("Root");
                BoneName childName = new BoneName("Child");
                Puppet = new GameObject("Puppet");
                Puppet.SetActive(false);
                GameObject puppetChild = new GameObject("Child");
                puppetChild.transform.SetParent(Puppet.transform, false);
                Rigidbody rootBody = Puppet.AddComponent<Rigidbody>();
                ConfigurableJoint rootJoint =
                    Puppet.AddComponent<ConfigurableJoint>();
                Puppet.AddComponent<BoxCollider>();
                puppetChild.AddComponent<Rigidbody>();
                ConfigurableJoint childJoint =
                    puppetChild.AddComponent<ConfigurableJoint>();
                childJoint.connectedBody = rootBody;
                puppetChild.AddComponent<BoxCollider>();

                definition = ScriptableObject.CreateInstance<RagdollDefinition>();
                SetPrivateField(definition, "_isValid", true);
                SetPrivateField(definition, "_root", rootName);
                SetPrivateField(definition, "bones", new[] { rootName, childName });
                bindings = Puppet.AddComponent<RagdollDefinitionBindings>();
                SetPrivateField(bindings, "_definition", definition);
                SetPrivateField(bindings, "bindings", CreateBindingDictionary(
                    rootName, rootJoint, childName, childJoint));
                Puppet.SetActive(true);
                Assert.That(bindings.IsInitialized, Is.True);

                Target = new GameObject("Puppet");
                GameObject targetChild = new GameObject(targetChildName);
                targetChild.transform.SetParent(Target.transform, false);
                targetChild.transform.localPosition = Vector3.up;
                Target.layer = 4;
                Puppet.layer = 5;
            }

            public GameObject Target { get; }
            public GameObject Puppet { get; }

            public static DualRigClosureFixture Create(string targetChildName)
            {
                return new DualRigClosureFixture(targetChildName);
            }

            public RagdollSetupResult Apply()
            {
                return RagdollDualRigSetupWindow.ApplyCompleteSetup(
                    Target.transform, bindings, profile, 30, 31);
            }

            public void CaptureSetupRoot(RagdollSetupResult result)
            {
                setupRoot = result.Root ? result.Root.gameObject : null;
            }

            public void Dispose()
            {
                Undo.ClearAll();
                Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
                if (setupRoot) UnityEngine.Object.DestroyImmediate(setupRoot);
                if (Target) UnityEngine.Object.DestroyImmediate(Target);
                if (Puppet) UnityEngine.Object.DestroyImmediate(Puppet);
                if (definition) UnityEngine.Object.DestroyImmediate(definition);
                if (profile) UnityEngine.Object.DestroyImmediate(profile);
            }

            static object CreateBindingDictionary(
                BoneName root,
                ConfigurableJoint rootJoint,
                BoneName child,
                ConfigurableJoint childJoint)
            {
                Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                    "BoneJointBindingsDictionary", BindingFlags.NonPublic);
                Assert.That(type, Is.Not.Null);
                object dictionary = Activator.CreateInstance(type, true);
                MethodInfo add = type.GetMethod(
                    "Add",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                    null);
                Assert.That(add, Is.Not.Null);
                add.Invoke(dictionary, new object[] { root, rootJoint });
                add.Invoke(dictionary, new object[] { child, childJoint });
                return dictionary;
            }

            static void SetPrivateField(object owner, string name, object value)
            {
                FieldInfo field = owner.GetType().GetField(
                    name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name);
                field.SetValue(owner, value);
            }
        }

        sealed class AuthoredRigClosureFixture : IDisposable
        {
            readonly GameObject owner;
            readonly PhysicsMaterial material;
            readonly float expectedContactOffset;

            AuthoredRigClosureFixture()
            {
                owner = new GameObject("Closure authored rig");
                Rigidbody body = owner.AddComponent<Rigidbody>();
                BoxCollider collider = owner.AddComponent<BoxCollider>();
                collider.center = new Vector3(1f, 2f, 3f);
                collider.size = new Vector3(2f, 6f, 4f);
                collider.isTrigger = true;
                collider.enabled = false;
                collider.contactOffset = 0.02f;
                expectedContactOffset = collider.contactOffset;
                collider.providesContacts = true;
                collider.layerOverridePriority = 7;
                collider.includeLayers = 1 << 8;
                collider.excludeLayers = 1 << 9;
                material = new PhysicsMaterial("Closure conversion material");
                collider.sharedMaterial = material;
                ConfigurableJoint joint = owner.AddComponent<ConfigurableJoint>();
                Rig = owner.AddComponent<RagdollAuthoredRig>();
                Rig.SetOwnedComponents(
                    new[] { body },
                    new Collider[] { collider },
                    new[] { joint });
                Inspector = (RagdollAuthoredRigEditor)UnityEditor.Editor.CreateEditor(
                    Rig, typeof(RagdollAuthoredRigEditor));
            }

            public RagdollAuthoredRig Rig { get; }
            public RagdollAuthoredRigEditor Inspector { get; }

            public static AuthoredRigClosureFixture Create()
            {
                return new AuthoredRigClosureFixture();
            }

            public void AssertCommonSettings(Collider value)
            {
                Assert.That(value, Is.Not.Null);
                Assert.That(value.enabled, Is.False);
                Assert.That(value.isTrigger, Is.True);
                Assert.That(value.sharedMaterial, Is.SameAs(material));
                Assert.That(value.contactOffset,
                    Is.EqualTo(expectedContactOffset).Within(0.0001f));
                Assert.That(value.providesContacts, Is.True);
                Assert.That(value.layerOverridePriority, Is.EqualTo(7));
                Assert.That(value.includeLayers.value, Is.EqualTo(1 << 8));
                Assert.That(value.excludeLayers.value, Is.EqualTo(1 << 9));
            }

            public void Dispose()
            {
                Undo.ClearAll();
                if (Inspector) UnityEngine.Object.DestroyImmediate(Inspector);
                if (owner) UnityEngine.Object.DestroyImmediate(owner);
                if (material) UnityEngine.Object.DestroyImmediate(material);
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
