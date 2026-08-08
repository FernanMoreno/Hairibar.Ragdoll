using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class HairibarCertificationContinuationTests
    {
        const string PendingOperationKey =
            "Hairibar.Ragdoll.Certification.PendingOperation";
        const string ContinuationScheduledKey =
            "Hairibar.Ragdoll.Certification.ContinuationScheduled";
        const string RunnerWaitStartedKey =
            "Hairibar.Ragdoll.Certification.RunnerWaitStarted";

        [TearDown]
        public void TearDown()
        {
            SessionState.EraseInt(PendingOperationKey);
            SessionState.EraseBool(ContinuationScheduledKey);
            SessionState.EraseFloat(RunnerWaitStartedKey);
        }

        [Test]
        public void RunnerCompilationTimeout_ClearsEveryPersistedContinuationKey()
        {
            SessionState.SetInt(PendingOperationKey, 1);
            SessionState.SetBool(ContinuationScheduledKey, true);
            SessionState.SetFloat(RunnerWaitStartedKey, 123f);

            Type certification = typeof(HairibarCertification);
            MethodInfo timeout = certification.GetMethod(
                "ThrowRunnerUnavailable",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(timeout, Is.Not.Null);

            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                () => timeout.Invoke(null, null));
            Assert.That(thrown.InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(SessionState.GetInt(PendingOperationKey, 0), Is.Zero);
            Assert.That(SessionState.GetBool(
                ContinuationScheduledKey, false), Is.False);
            Assert.That(SessionState.GetFloat(
                RunnerWaitStartedKey, 0f), Is.Zero);
        }

        [Test]
        public void SamplePayloadComparison_DetectsImportedMutationWithoutTouchingSource()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "HairibarCertification_" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "Source");
            string imported = Path.Combine(root, "Imported");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(imported);
            try
            {
                File.WriteAllText(Path.Combine(source, "Runner.cs"), "source");
                File.WriteAllText(Path.Combine(imported, "Runner.cs"), "source");
                File.WriteAllText(Path.Combine(source, "Runner.cs.meta"), "A");
                File.WriteAllText(Path.Combine(imported, "Runner.cs.meta"), "B");

                Assert.That(HairibarCertification.SamplePayloadTreesMatch(
                    source, imported), Is.True,
                    "Unity-generated meta differences do not change sample payload.");

                File.WriteAllText(Path.Combine(imported, "Runner.cs"), "modified");
                Assert.That(HairibarCertification.SamplePayloadTreesMatch(
                    source, imported), Is.False);
                Assert.That(File.ReadAllText(Path.Combine(source, "Runner.cs")),
                    Is.EqualTo("source"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase("Packages/com.hairibar.ragdoll/Animation/Foo.cs(1): warning", true)]
        [TestCase("Hairibar.Ragdoll runtime warning", true)]
        [TestCase("Packages/com.hairibar.engineextensions/Runtime/Foo.cs(1): warning Hairibar.EngineExtensions", false)]
        [TestCase("Packages/com.vendor.tool/Foo.cs(1): warning", false)]
        public void BuildDiagnosticOwnership_IsClassifiedByPackageIdentity(
            string message,
            bool expected)
        {
            Assert.That(
                HairibarCertification.IsHairibarBuildDiagnostic(message),
                Is.EqualTo(expected));
        }
    }
}
