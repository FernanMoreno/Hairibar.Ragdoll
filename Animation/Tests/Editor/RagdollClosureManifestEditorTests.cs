using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;

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
            foreach (string name in names)
            {
                string[] scenes = AssetDatabase.FindAssets(name + " t:Scene");
                Assert.That(scenes, Has.Length.EqualTo(1), name);
            }
            Assert.That(
                AssetDatabase.FindAssets("RegressionScenarioRunner t:MonoScript"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void H05_PerformanceSceneCoversFlatTreeAndFourPopulationSizes()
        {
            string source = ReadPackageFile(
                "Samples~/Demos/Regression/RegressionScenarioRunner.cs");
            StringAssert.Contains("CollisionsPerformance", source);
            StringAssert.Contains("50", source);
            StringAssert.Contains("ProfilerRecorder", source);
        }

        [Test]
        public void H08_CertificationBuildsAllSupportedDevelopmentTargets()
        {
            string source = ReadPackageFile(
                "Animation/Editor/Certification/HairibarCertification.cs");
            StringAssert.Contains("StandaloneWindows64", source);
            StringAssert.Contains("StandaloneLinux64", source);
            StringAssert.Contains("StandaloneOSX", source);
            StringAssert.Contains("BuildTarget.WebGL", source);
            StringAssert.Contains("BuildOptions.Development", source);
            StringAssert.Contains("BuildOptions.AllowDebugging", source);
            StringAssert.Contains("ExecuteWindowsPlayer", source);
        }

        [Test]
        public void J05_GcCertificationSeparatesWarmupAndMeasuredFrames()
        {
            string source = ReadPackageFile(
                "Samples~/Demos/Regression/RegressionScenarioRunner.cs");
            StringAssert.Contains("WarmupFrames = 120", source);
            StringAssert.Contains("MeasurementFrames = 600", source);
            StringAssert.Contains("GC Allocated In Frame", source);
            StringAssert.Contains("maximumGcAllocatedInFrame", source);
        }

        [Test]
        public void J06_HistoricalMatrixHasOnlyVerifiedOrJustifiedNaRows()
        {
            string matrix = ReadPackageFile(
                "Documentation~/Certification/PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md");
            Assert.That(
                Regex.Matches(matrix, @"(?m)^\| [A-J]\d{2} \|").Count,
                Is.EqualTo(140));
            Assert.That(matrix, Does.Not.Match(@"(?m)^\| [A-J]\d{2} \| [PO] \|"));
            Assert.That(matrix, Does.Match(
                @"(?m)^\| G05 \| N/A \|.*IRagdollIKSolver"));
        }

        [Test]
        public void J07_MigrationGuideDocumentsEveryNewPublicEntryPoint()
        {
            string guide = ReadPackageFile(
                "Documentation~/Certification/MIGRATION-PUPPETMASTER-CLOSURE.md");
            string[] contracts =
            {
                "MasterPinWeight", "MasterMuscleWeight", "MasterMuscleDamper",
                "PrepareManualSimulation", "CompleteManualSimulation", "Respawn",
                "TrySetMuscles", "TryReplaceMuscles", "StartAction(float)",
                "CurrentRigidbody", "IRagdollIKSolver"
            };
            foreach (string contract in contracts)
            {
                StringAssert.Contains(contract, guide, contract);
            }
            StringAssert.Contains("diseño propio Hairibar", guide);
        }

        static string ReadPackageFile(string relativePath)
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(RagdollAnimator).Assembly);
            Assert.That(package, Is.Not.Null);
            string path = Path.Combine(
                package.resolvedPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(path), Is.True, path);
            return File.ReadAllText(path);
        }
    }
}
