using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollManualSimulationPlayModeTests
    {
        [UnityTest]
        public IEnumerator ManualStep_EnforcesOrderAndRestoresLegacyAnimation()
        {
            GameObject puppetRoot = null;
            GameObject targetRoot = null;
            RagdollDefinition definition = null;
            RagdollAnimationProfile profile = null;
            SimulationMode originalMode = Physics.simulationMode;
            try
            {
                BoneName rootName = new BoneName("Root");
                BoneName childName = new BoneName("Child");
                puppetRoot = new GameObject("Puppet");
                puppetRoot.SetActive(false);
                GameObject puppetChild = new GameObject("Child");
                puppetChild.transform.SetParent(puppetRoot.transform, false);
                puppetRoot.name = "Root";

                Rigidbody rootBody = puppetRoot.AddComponent<Rigidbody>();
                ConfigurableJoint rootJoint =
                    puppetRoot.AddComponent<ConfigurableJoint>();
                puppetRoot.AddComponent<BoxCollider>();
                Rigidbody childBody = puppetChild.AddComponent<Rigidbody>();
                ConfigurableJoint childJoint =
                    puppetChild.AddComponent<ConfigurableJoint>();
                childJoint.connectedBody = rootBody;
                puppetChild.AddComponent<BoxCollider>();

                definition = ScriptableObject.CreateInstance<RagdollDefinition>();
                SetField(definition, "_isValid", true);
                SetField(definition, "_root", rootName);
                SetField(definition, "bones", new[] { rootName, childName });
                RagdollDefinitionBindings bindings =
                    puppetRoot.AddComponent<RagdollDefinitionBindings>();
                SetField(bindings, "_definition", definition);
                SetField(bindings, "bindings", CreateBindings(
                    rootName,
                    rootJoint,
                    childName,
                    childJoint));
                puppetRoot.SetActive(true);
                Assert.That(bindings.IsInitialized, Is.True);

                targetRoot = new GameObject("Root");
                targetRoot.SetActive(false);
                GameObject targetChild = new GameObject("Child");
                targetChild.transform.SetParent(targetRoot.transform, false);
                targetChild.transform.localPosition = Vector3.up;
                UnityEngine.Animation legacy =
                    targetRoot.AddComponent<UnityEngine.Animation>();
                legacy.animatePhysics = true;

                RagdollTargetBindings targets =
                    targetRoot.AddComponent<RagdollTargetBindings>();
                targets.SetRagdollBindings(bindings);
                string error;
                Assert.That(targets.TryAutoBindByName(out error), Is.True, error);
                Assert.That(targets.TryCaptureOffsets(out error), Is.True, error);

                profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
                RagdollAnimator animator = targetRoot.AddComponent<RagdollAnimator>();
                animator.ConfigureBeforeInitialization(bindings, targets, profile);
                targetRoot.SetActive(true);
                yield return null;

                Assert.That(animator.TargetAnimation, Is.SameAs(legacy));
#if UNITY_6000_0_OR_NEWER
                Assert.That(animator.EffectiveUpdateMode,
                    Is.EqualTo(AnimatorUpdateMode.Fixed));
#endif
                Physics.simulationMode = SimulationMode.Script;
                Assert.Throws<InvalidOperationException>(() =>
                    animator.CompleteManualSimulation());
                Assert.Throws<InvalidOperationException>(() =>
                    animator.PrepareManualSimulation(0.02f));

                animator.enabled = false;
                animator.OnPreSimulate(0.02f);
                Assert.That(animator.IsManualSimulationPrepared, Is.True);
                Assert.That(legacy.enabled, Is.True,
                    "Manual Puppet simulation must not disable Legacy Animation.");
                Assert.Throws<InvalidOperationException>(() =>
                    animator.PrepareManualSimulation(0.02f));
                Assert.That(animator.IsManualSimulationPrepared, Is.False,
                    "Duplicate Prepare must abort the abandoned step.");
                Assert.That(legacy.enabled, Is.True);

                animator.OnPreSimulate(0.02f);
                Physics.Simulate(0.02f);
                animator.OnPostSimulate();
                Assert.That(animator.IsManualSimulationPrepared, Is.False);
                Assert.That(legacy.enabled, Is.True);
                Assert.That(animator.enabled, Is.False);
                Assert.Throws<InvalidOperationException>(() =>
                    animator.CompleteManualSimulation());
                Assert.That(childBody, Is.Not.Null);

                animator.OnPreSimulate(0.02f);
                animator.enabled = true;
                Assert.Throws<InvalidOperationException>(() =>
                    animator.OnPostSimulate());
                Assert.That(animator.IsManualSimulationPrepared, Is.False,
                    "Failed completion must release manual-step ownership.");
                Assert.That(legacy.enabled, Is.True);
            }
            finally
            {
                Physics.simulationMode = originalMode;
                if (targetRoot) UnityEngine.Object.DestroyImmediate(targetRoot);
                if (puppetRoot) UnityEngine.Object.DestroyImmediate(puppetRoot);
                if (profile) UnityEngine.Object.DestroyImmediate(profile);
                if (definition) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [UnityTest]
        public IEnumerator FixedAnimator_IsEvaluatedExclusivelyByRagdollAnimator()
        {
            GameObject puppetRoot = null;
            GameObject targetRoot = null;
            RagdollDefinition definition = null;
            RagdollAnimationProfile profile = null;
            try
            {
                BoneName rootName = new BoneName("Root");
                BoneName childName = new BoneName("Child");
                puppetRoot = new GameObject("Puppet");
                puppetRoot.SetActive(false);
                GameObject puppetChild = new GameObject("Child");
                puppetChild.transform.SetParent(puppetRoot.transform, false);
                Rigidbody rootBody = puppetRoot.AddComponent<Rigidbody>();
                ConfigurableJoint rootJoint =
                    puppetRoot.AddComponent<ConfigurableJoint>();
                puppetRoot.AddComponent<BoxCollider>();
                puppetChild.AddComponent<Rigidbody>();
                ConfigurableJoint childJoint =
                    puppetChild.AddComponent<ConfigurableJoint>();
                childJoint.connectedBody = rootBody;
                puppetChild.AddComponent<BoxCollider>();
                puppetRoot.name = "Root";

                definition = ScriptableObject.CreateInstance<RagdollDefinition>();
                SetField(definition, "_isValid", true);
                SetField(definition, "_root", rootName);
                SetField(definition, "bones", new[] { rootName, childName });
                RagdollDefinitionBindings bindings =
                    puppetRoot.AddComponent<RagdollDefinitionBindings>();
                SetField(bindings, "_definition", definition);
                SetField(bindings, "bindings", CreateBindings(
                    rootName, rootJoint, childName, childJoint));
                puppetRoot.SetActive(true);

                targetRoot = new GameObject("Root");
                targetRoot.SetActive(false);
                GameObject targetChild = new GameObject("Child");
                targetChild.transform.SetParent(targetRoot.transform, false);
                targetChild.transform.localPosition = Vector3.up;
                Animator targetAnimator = targetRoot.AddComponent<Animator>();
#if UNITY_6000_0_OR_NEWER
                targetAnimator.updateMode = AnimatorUpdateMode.Fixed;
#else
                targetAnimator.updateMode = AnimatorUpdateMode.AnimatePhysics;
#endif
                RagdollTargetBindings targets =
                    targetRoot.AddComponent<RagdollTargetBindings>();
                targets.SetRagdollBindings(bindings);
                string error;
                Assert.That(targets.TryAutoBindByName(out error), Is.True, error);
                Assert.That(targets.TryCaptureOffsets(out error), Is.True, error);

                profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
                RagdollAnimator animator = targetRoot.AddComponent<RagdollAnimator>();
                animator.ConfigureBeforeInitialization(bindings, targets, profile);
                targetRoot.SetActive(true);
                yield return null;

                Assert.That(animator.ControlsAnimator, Is.True);
                Assert.That(targetAnimator.enabled, Is.False,
                    "A fixed-update Animator must not also run in Unity's automatic loop.");

                animator.enabled = false;
                Assert.That(animator.ControlsAnimator, Is.False);
                Assert.That(targetAnimator.enabled, Is.True,
                    "Disabling RagdollAnimator must release Animator ownership.");

                animator.enabled = true;
                yield return null;
                Assert.That(animator.ControlsAnimator, Is.True);
                Assert.That(targetAnimator.enabled, Is.False);

                targetAnimator.updateMode = AnimatorUpdateMode.Normal;
                yield return null;
                Assert.That(animator.ControlsAnimator, Is.False);
                Assert.That(targetAnimator.enabled, Is.True);

                GameObject alternateRoot = new GameObject("Alternate Animator");
                alternateRoot.transform.SetParent(targetRoot.transform, false);
                Animator alternateAnimator = alternateRoot.AddComponent<Animator>();
#if UNITY_6000_0_OR_NEWER
                alternateAnimator.updateMode = AnimatorUpdateMode.Fixed;
#else
                alternateAnimator.updateMode = AnimatorUpdateMode.AnimatePhysics;
#endif
                animator.TargetAnimator = alternateAnimator;
                Assert.That(animator.TargetAnimator, Is.SameAs(alternateAnimator));
                Assert.That(animator.ControlsAnimator, Is.True);
                Assert.That(alternateAnimator.enabled, Is.False);
                Assert.That(targetAnimator.enabled, Is.True,
                    "Replacing the Animator must release the previous component.");

                animator.TargetAnimator = null;
                Assert.That(animator.TargetAnimator, Is.Null);
                Assert.That(animator.ControlsAnimator, Is.False);
                Assert.That(alternateAnimator.enabled, Is.True,
                    "Clearing the Animator must release its exact lifecycle state.");
            }
            finally
            {
                if (targetRoot) UnityEngine.Object.DestroyImmediate(targetRoot);
                if (puppetRoot) UnityEngine.Object.DestroyImmediate(puppetRoot);
                if (profile) UnityEngine.Object.DestroyImmediate(profile);
                if (definition) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        static object CreateBindings(
            BoneName root,
            ConfigurableJoint rootJoint,
            BoneName child,
            ConfigurableJoint childJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary",
                BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { child, childJoint });
            return dictionary;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
