using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hairibar.Ragdoll.Animation;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollTelemetryRecorderTests
    {
        [Test]
        public void RecorderWithoutAnimationSourcesCapturesExplicitUnavailableBalance()
        {
            GameObject root = new GameObject("TelemetryRoot");
            try
            {
                RagdollTelemetryRecorder recorder = root.AddComponent<RagdollTelemetryRecorder>();
                recorder.CaptureOnStart = false;
                recorder.ConfigureTracking(root.transform);
                recorder.Begin();
                recorder.ManualCaptureStep(0f);

                Assert.That(recorder.Frames, Has.Count.EqualTo(1));
                PhysicsFrame frame = recorder.Frames[0];
                Assert.That(frame.balance, Is.Not.Null);
                Assert.That(frame.balance.sourceAvailable, Is.False);
                Assert.That(frame.balance.state, Is.EqualTo("Unavailable"));
                Assert.That(frame.balance.hasCapturePoint, Is.False);
                Assert.That(frame.balance.effectiveUpAvailable, Is.False);
                Assert.That(frame.balance.relativeSupportMotionAvailable, Is.False);
                Assert.That(frame.balance.supportColliderId, Is.Zero);
                Assert.That(frame.balance.supportRigidbodyId, Is.Zero);
                Assert.That(frame.stagger, Is.Not.Null);
                Assert.That(frame.stagger.sourceAvailable, Is.False);
                Assert.That(frame.animatedPairCaptureAttempted, Is.True);
                Assert.That(frame.animatedPairSourceAvailable, Is.False);
                Assert.That(frame.animatedPairCount, Is.EqualTo(0));
                Assert.That(frame.animatedPairs, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RecorderArtifactsExposeScenarioAndDecisionContract()
        {
            GameObject root = new GameObject("TelemetryArtifactRoot");
            string outputDirectory = "Temp/RagdollLabFeature005-" + Guid.NewGuid().ToString("N");
            RagdollTelemetryRecorder recorder = null;
            try
            {
                recorder = root.AddComponent<RagdollTelemetryRecorder>();
                recorder.CaptureOnStart = false;
                recorder.ScenarioName = "Push";
                recorder.OutputDirectory = outputDirectory;
                recorder.ConfigureTracking(root.transform);
                recorder.Begin();
                recorder.ManualCaptureStep(0f);
                recorder.End();

                string path = recorder.OutputPath;
                string evaluation = File.ReadAllText(Path.Combine(path, "evaluation.json"));
                string comparison = File.ReadAllText(Path.Combine(path, "comparison.json"));
                string balanceComparison = File.ReadAllText(Path.Combine(path, "balance-comparison.json"));
                string diagnostics = File.ReadAllText(Path.Combine(path, "diagnostics.json"));
                string summary = File.ReadAllText(Path.Combine(path, "summary.md"));

                Assert.That(evaluation, Does.Contain("\"scenarioProfile\": \"Push\""));
                Assert.That(comparison, Does.Contain("\"decision\""));
                Assert.That(balanceComparison, Does.Contain("\"invalidReason\""));
                Assert.That(diagnostics, Does.Contain("\"profileAvailable\""));
                Assert.That(summary, Does.Contain("Scenario profile: `Push`"));
                Assert.That(summary, Does.Contain("Animated pairs:"));
                Assert.That(summary, Does.Contain("Decision:"));
            }
            finally
            {
                if (recorder != null && recorder.IsCapturing) recorder.End();
                string path = recorder?.OutputPath;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) Directory.Delete(path, true);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator RecorderCapturesEveryInitializedAnimatedPairIncludingNonHumanoidPairs()
        {
            RecorderRig rig = RecorderRig.Create();
            RagdollTelemetryRecorder recorder = null;
            string outputDirectory = "Temp/RagdollLabFeature007-Capture-" +
                Guid.NewGuid().ToString("N");
            try
            {
                yield return rig.WaitUntilReady();
                recorder = CreateRecorder(rig, outputDirectory);
                recorder.ManualCaptureStep(0.02f);
                recorder.ManualCaptureStep(0.04f);

                Assert.That(recorder.Frames, Has.Count.EqualTo(2));
                PhysicsFrame first = recorder.Frames[0];
                PhysicsFrame second = recorder.Frames[1];
                Assert.That(first.animatedPairSourceAvailable, Is.True);
                Assert.That(first.animatedPairCount, Is.EqualTo(3));
                Assert.That(second.animatedPairCount, Is.EqualTo(3));
                Assert.That(FindPair(first, "Tail"), Is.Not.Null,
                    "A non-humanoid pair must not be omitted by a legacy bone list.");

                HashSet<string> firstIds = PairIds(first);
                HashSet<string> secondIds = PairIds(second);
                Assert.That(firstIds.Count, Is.EqualTo(3));
                CollectionAssert.AreEquivalent(firstIds, secondIds);
                foreach (string pairId in firstIds)
                {
                    TargetPoseTelemetry firstPair = FindPair(first, pairId);
                    TargetPoseTelemetry secondPair = FindPair(second, pairId);
                    Assert.That(firstPair.targetTransformId, Is.Not.EqualTo("missing"));
                    Assert.That(firstPair.physicsBodyId, Is.Not.EqualTo("missing"));
                    Assert.That(secondPair.targetTransformId,
                        Is.EqualTo(firstPair.targetTransformId));
                    Assert.That(secondPair.physicsBodyId,
                        Is.EqualTo(firstPair.physicsBodyId));
                }
            }
            finally
            {
                DisposeRecorder(recorder);
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator RecorderMarksUninitializedAndMissingPairSidesWithoutFabricatingAssociations()
        {
            GameObject uninitializedRoot = new GameObject(
                "Feature007 Uninitialized Recorder Root");
            RagdollTelemetryRecorder uninitializedRecorder = null;
            try
            {
                uninitializedRoot.SetActive(false);
                RagdollAnimator animator =
                    uninitializedRoot.AddComponent<RagdollAnimator>();
                animator.enabled = false;
                uninitializedRecorder =
                    uninitializedRoot.AddComponent<RagdollTelemetryRecorder>();
                uninitializedRecorder.CaptureOnStart = false;
                uninitializedRecorder.ConfigureTracking(uninitializedRoot.transform);
                uninitializedRecorder.Begin();
                uninitializedRecorder.ManualCaptureStep(0f);

                PhysicsFrame unavailable = uninitializedRecorder.Frames[0];
                Assert.That(unavailable.animatedPairCaptureAttempted, Is.True);
                Assert.That(unavailable.animatedPairSourceAvailable, Is.False);
                Assert.That(unavailable.animatedPairCount, Is.Zero);
                Assert.That(unavailable.animatedPairs, Is.Empty);
            }
            finally
            {
                DisposeRecorder(uninitializedRecorder);
                UnityEngine.Object.DestroyImmediate(uninitializedRoot);
            }

            RecorderRig rig = RecorderRig.Create();
            RagdollTelemetryRecorder recorder = null;
            string outputDirectory = "Temp/RagdollLabFeature007-Missing-" +
                Guid.NewGuid().ToString("N");
            try
            {
                yield return rig.WaitUntilReady();
                recorder = CreateRecorder(rig, outputDirectory);
                recorder.ManualCaptureStep(0.02f);
                RagdollAnimator.AnimatedPair tail = FindAnimatorPair(
                    rig.Animator, "Tail");
                UnityEngine.Object.DestroyImmediate(tail.TargetBone.gameObject);
                UnityEngine.Object.DestroyImmediate(
                    tail.RagdollBone.Rigidbody.gameObject);
                recorder.ManualCaptureStep(0.04f);

                PhysicsFrame missing = recorder.Frames[1];
                TargetPoseTelemetry missingPair = FindPair(missing, "Tail");
                Assert.That(missingPair, Is.Not.Null);
                Assert.That(missingPair.targetAvailable, Is.False);
                Assert.That(missingPair.physicsAvailable, Is.False);
                Assert.That(missing.mappingIntegrityWarnings,
                    Does.Contain("animated_pair_identity_set_changed"));
                Assert.That(HasWarning(missing.mappingIntegrityWarnings,
                    "missing_target:"), Is.True);
                Assert.That(HasWarning(missing.mappingIntegrityWarnings,
                    "missing_physics_body:"), Is.True);
            }
            finally
            {
                DisposeRecorder(recorder);
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator RecorderCapturesPairedPhysicsDerivativesAndResetBoundaries()
        {
            RecorderRig rig = RecorderRig.Create();
            RagdollTelemetryRecorder recorder = null;
            string outputDirectory = "Temp/RagdollLabFeature007-Derivatives-" +
                Guid.NewGuid().ToString("N");
            try
            {
                yield return rig.WaitUntilReady();
                recorder = CreateRecorder(rig, outputDirectory);
                RagdollAnimator.AnimatedPair tail = FindAnimatorPair(
                    rig.Animator, "Tail");
                Rigidbody body = tail.RagdollBone.Rigidbody;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                recorder.ManualCaptureStep(1f);

                body.linearVelocity = Vector3.right;
                body.angularVelocity = Vector3.up;
                recorder.ManualCaptureStep(1.1f);
                body.linearVelocity = Vector3.right * 3f;
                body.angularVelocity = Vector3.up * 3f;
                recorder.ManualCaptureStep(1.2f);

                TargetPoseTelemetry first = FindPair(recorder.Frames[0], "Tail");
                TargetPoseTelemetry acceleration =
                    FindPair(recorder.Frames[1], "Tail");
                TargetPoseTelemetry jerk = FindPair(recorder.Frames[2], "Tail");
                Assert.That(first.physicsKinematicsReset, Is.True);
                Assert.That(first.physicsAccelerationAvailable, Is.False);
                Assert.That(acceleration.physicsKinematicsAvailable, Is.True);
                Assert.That(acceleration.physicsAccelerationAvailable, Is.True);
                Assert.That(acceleration.physicsJerkAvailable, Is.False);
                Assert.That(acceleration.sampleDeltaTime, Is.EqualTo(0.1f)
                    .Within(0.0001f));
                Assert.That(acceleration.physicsLinearAcceleration.x,
                    Is.EqualTo(10f).Within(0.001f));
                Assert.That(jerk.physicsJerkAvailable, Is.True);
                Assert.That(jerk.physicsLinearJerk.x,
                    Is.EqualTo(100f).Within(0.01f));

                recorder.ManualCaptureStep(1.2f);
                TargetPoseTelemetry repeated = FindPair(recorder.Frames[3], "Tail");
                Assert.That(repeated.physicsKinematicsReset, Is.True);
                Assert.That(repeated.physicsAccelerationAvailable, Is.False);
                Assert.That(repeated.physicsJerkAvailable, Is.False);
            }
            finally
            {
                DisposeRecorder(recorder);
                rig.Dispose();
            }
        }

        static RagdollTelemetryRecorder CreateRecorder(
            RecorderRig rig,
            string outputDirectory)
        {
            RagdollTelemetryRecorder recorder =
                rig.Root.AddComponent<RagdollTelemetryRecorder>();
            recorder.CaptureOnStart = false;
            recorder.OutputDirectory = outputDirectory;
            recorder.ConfigureTracking(rig.Root.transform);
            recorder.Begin();
            return recorder;
        }

        static void DisposeRecorder(RagdollTelemetryRecorder recorder)
        {
            if (!recorder) return;
            string outputPath = recorder.OutputPath;
            if (recorder.IsCapturing) recorder.End();
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }

        static HashSet<string> PairIds(PhysicsFrame frame)
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (TargetPoseTelemetry pair in frame.animatedPairs)
                ids.Add(pair.pairId);
            return ids;
        }

        static TargetPoseTelemetry FindPair(PhysicsFrame frame, string value)
        {
            foreach (TargetPoseTelemetry pair in frame.animatedPairs)
                if (pair.pairId == value || pair.bone == value) return pair;
            return null;
        }

        static bool HasWarning(IEnumerable<string> warnings, string prefix)
        {
            foreach (string warning in warnings)
                if (warning.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        static RagdollAnimator.AnimatedPair FindAnimatorPair(
            RagdollAnimator animator,
            string boneName)
        {
            foreach (RagdollAnimator.AnimatedPair pair in animator.AnimatedPairs)
                if (pair.Name.ToString() == boneName) return pair;
            Assert.Fail("No animated pair exists for " + boneName + ".");
            return null;
        }

        sealed class RecorderRig
        {
            public GameObject Root;
            public GameObject Puppet;
            public GameObject Target;
            public RagdollDefinition Definition;
            public RagdollDefinitionBindings Bindings;
            public RagdollAnimationProfile Profile;
            public RagdollAnimator Animator;
            bool ignoredBefore;

            public static RecorderRig Create()
            {
                var rig = new RecorderRig();
                rig.ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
                rig.Root = new GameObject("Feature007 Recorder Rig");
                rig.Puppet = new GameObject("Root");
                rig.Puppet.transform.SetParent(rig.Root.transform, false);
                rig.Puppet.SetActive(false);
                rig.Target = new GameObject("Root");
                rig.Target.transform.SetParent(rig.Root.transform, false);
                GameObject targetChild = new GameObject("Child");
                targetChild.transform.SetParent(rig.Target.transform, false);
                targetChild.transform.localPosition = Vector3.up;
                GameObject targetTail = new GameObject("Tail");
                targetTail.transform.SetParent(targetChild.transform, false);
                targetTail.transform.localPosition = Vector3.up;
                rig.BuildPuppet();
                rig.Target.AddComponent<UnityEngine.Animation>().animatePhysics = true;
                rig.Profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
                RagdollSetupResult result =
                    RagdollRuntimeSetupService.ConfigureSeparated(
                        rig.Target.transform,
                        rig.Bindings,
                        rig.Profile,
                        30,
                        31);
                Assert.That(result.Succeeded, Is.True, result.Error);
                rig.Animator = result.Animator;
                return rig;
            }

            public IEnumerator WaitUntilReady()
            {
                yield return null;
                Assert.That(Animator.Initiated, Is.True);
                Assert.That(Animator.AnimatedPairs.Count, Is.EqualTo(3));
            }

            void BuildPuppet()
            {
                BoneName rootName = new BoneName("Root");
                BoneName childName = new BoneName("Child");
                BoneName tailName = new BoneName("Tail");
                GameObject child = CreateBone(Puppet, "Child", Vector3.up);
                GameObject tail = CreateBone(child, "Tail", Vector3.up);
                Rigidbody rootBody = Puppet.AddComponent<Rigidbody>();
                ConfigurableJoint rootJoint = Puppet.AddComponent<ConfigurableJoint>();
                Puppet.AddComponent<BoxCollider>();
                Rigidbody childBody = child.GetComponent<Rigidbody>();
                ConfigurableJoint childJoint = child.GetComponent<ConfigurableJoint>();
                childJoint.connectedBody = rootBody;
                Rigidbody tailBody = tail.GetComponent<Rigidbody>();
                ConfigurableJoint tailJoint = tail.GetComponent<ConfigurableJoint>();
                tailJoint.connectedBody = childBody;

                Definition = ScriptableObject.CreateInstance<RagdollDefinition>();
                SetField(Definition, "_isValid", true);
                SetField(Definition, "_root", rootName);
                SetField(Definition, "bones", new[] { rootName, childName, tailName });
                Bindings = Puppet.AddComponent<RagdollDefinitionBindings>();
                SetField(Bindings, "_definition", Definition);
                SetField(Bindings, "bindings", CreateBindings(
                    rootName, rootJoint, childName, childJoint, tailName, tailJoint));
                Puppet.SetActive(true);
                Assert.That(Bindings.IsInitialized, Is.True);
            }

            static GameObject CreateBone(
                GameObject parent,
                string name,
                Vector3 localPosition)
            {
                GameObject bone = new GameObject(name);
                bone.transform.SetParent(parent.transform, false);
                bone.transform.localPosition = localPosition;
                bone.AddComponent<Rigidbody>();
                bone.AddComponent<ConfigurableJoint>();
                bone.AddComponent<BoxCollider>();
                return bone;
            }

            public void Dispose()
            {
                Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
                if (Root) UnityEngine.Object.DestroyImmediate(Root);
                if (Profile) UnityEngine.Object.DestroyImmediate(Profile);
                if (Definition) UnityEngine.Object.DestroyImmediate(Definition);
            }

            static object CreateBindings(
                BoneName root,
                ConfigurableJoint rootJoint,
                BoneName child,
                ConfigurableJoint childJoint,
                BoneName tail,
                ConfigurableJoint tailJoint)
            {
                Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                    "BoneJointBindingsDictionary", BindingFlags.NonPublic);
                object dictionary = Activator.CreateInstance(type, true);
                MethodInfo add = type.GetMethod(
                    "Add",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                    null);
                add.Invoke(dictionary, new object[] { root, rootJoint });
                add.Invoke(dictionary, new object[] { child, childJoint });
                add.Invoke(dictionary, new object[] { tail, tailJoint });
                return dictionary;
            }

            static void SetField(object owner, string name, object value)
            {
                FieldInfo field = owner.GetType().GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name);
                field.SetValue(owner, value);
            }
        }
    }
}
