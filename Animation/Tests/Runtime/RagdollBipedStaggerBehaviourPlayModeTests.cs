using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// End-to-end physical coverage for the biped stagger actuator: a real
    /// RagdollAnimator, muscle registry, behaviour controller and dynamic
    /// Rigidbodies for a 3-bone (root + two feet) fixture. Feet and root are
    /// frozen so the capture-point classification stays constant across the
    /// whole step cycle, making the actuator's success/failure branch
    /// deterministic without depending on real step physics.
    /// </summary>
    public sealed class RagdollBipedStaggerBehaviourPlayModeTests
    {
        StaggerPhysicalRig rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (rig != null) rig.Dispose();
            rig = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BalancedCaptureMargin_CompletesStepCycleAndReactivatesPuppetAsPuppet()
        {
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f);
            yield return null;
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.25f;
            // Root sits directly between the feet: capture point margin is
            // comfortably inside the support segment -> Stable every frame.

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);

            yield return RunUntilIdleOrTimeout(rig);

            Assert.That(rig.Controller.ActiveBehaviour, Is.InstanceOf<RagdollPuppetBehaviour>());
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
        }

        [UnityTest]
        public IEnumerator UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet()
        {
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f);
            yield return null;
            // Move both feet far to one side while root (and therefore the
            // mass-weighted center of mass) stays near the origin: the
            // foot-to-foot support segment ends up far from the capture
            // point -> Unrecoverable every frame, so the very first step
            // must fail immediately regardless of remaining budget
            // (RagdollBipedStaggerMath.ResolveOutcome).
            rig.LeftFootBody.transform.position += Vector3.right * 5f;
            rig.RightFootBody.transform.position += Vector3.right * 5f;
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.1f;
            rig.Stagger.MaxSteps = 1;

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);

            yield return RunUntilIdleOrTimeout(rig);

            Assert.That(rig.Controller.ActiveBehaviour, Is.InstanceOf<RagdollPuppetBehaviour>());
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));
        }

        static IEnumerator RunUntilIdleOrTimeout(StaggerPhysicalRig rig)
        {
            WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();
            for (int frame = 0; frame < 60; frame++)
            {
                yield return fixedUpdate;
                if (rig.Controller.ActiveBehaviour is RagdollPuppetBehaviour) yield break;
            }
            Assert.Fail("Stagger actuator never returned control to RagdollPuppetBehaviour.");
        }
    }

    internal sealed class StaggerPhysicalRig : IDisposable
    {
        readonly GameObject puppetRoot;
        readonly RagdollDefinition definition;
        readonly RagdollAnimationProfile profile;
        readonly bool ignoredBefore;

        internal RagdollSetupResult Result { get; }
        internal RagdollPuppetBehaviour Puppet => Result.PuppetBehaviour;
        internal RagdollBehaviourController Controller => Result.Behaviours;
        internal RagdollBipedStaggerBehaviour Stagger { get; }
        internal Rigidbody RootBody { get; }
        internal Rigidbody LeftFootBody { get; }
        internal Rigidbody RightFootBody { get; }

        internal StaggerPhysicalRig(float footOffsetX)
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
            BoneName rootName = new BoneName("Root");
            BoneName leftFootName = new BoneName("foot_l");
            BoneName rightFootName = new BoneName("foot_r");

            puppetRoot = new GameObject("Stagger Puppet");
            puppetRoot.SetActive(false);
            GameObject leftFoot = new GameObject("foot_l");
            leftFoot.transform.SetParent(puppetRoot.transform, false);
            leftFoot.transform.localPosition = new Vector3(-footOffsetX, -1f, 0f);
            GameObject rightFoot = new GameObject("foot_r");
            rightFoot.transform.SetParent(puppetRoot.transform, false);
            rightFoot.transform.localPosition = new Vector3(footOffsetX, -1f, 0f);

            RootBody = puppetRoot.AddComponent<Rigidbody>();
            RootBody.useGravity = false;
            RootBody.constraints = RigidbodyConstraints.FreezeAll;
            RootBody.mass = 2f;
            ConfigurableJoint rootJoint = puppetRoot.AddComponent<ConfigurableJoint>();
            BoxCollider rootCollider = puppetRoot.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one * 0.75f;

            ConfigurableJoint leftJoint = ConfigureFrozenFoot(leftFoot, RootBody);
            LeftFootBody = leftFoot.GetComponent<Rigidbody>();
            ConfigurableJoint rightJoint = ConfigureFrozenFoot(rightFoot, RootBody);
            RightFootBody = rightFoot.GetComponent<Rigidbody>();

            definition = ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones",
                new[] { rootName, leftFootName, rightFootName });
            RagdollDefinitionBindings bindings =
                puppetRoot.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint,
                leftFootName, leftJoint,
                rightFootName, rightJoint));
            puppetRoot.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            GameObject target = new GameObject("Stagger Puppet");
            GameObject leftTarget = new GameObject("foot_l");
            leftTarget.transform.SetParent(target.transform, false);
            leftTarget.transform.localPosition = new Vector3(-footOffsetX, -1f, 0f);
            GameObject rightTarget = new GameObject("foot_r");
            rightTarget.transform.SetParent(target.transform, false);
            rightTarget.transform.localPosition = new Vector3(footOffsetX, -1f, 0f);

            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            Result = RagdollRuntimeSetupService.ConfigureSeparated(
                target.transform, bindings, profile, 28, 29);
            Assert.That(Result.Succeeded, Is.True, Result.Error);
            Result.PuppetBehaviour.CanStagger = true;
            Result.PuppetBehaviour.LoseBalanceOnTargetDrift = false;

            Stagger = Result.PuppetBehaviour.gameObject
                .AddComponent<RagdollBipedStaggerBehaviour>();
        }

        static ConfigurableJoint ConfigureFrozenFoot(GameObject foot, Rigidbody root)
        {
            Rigidbody body = foot.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.mass = 0.5f;
            ConfigurableJoint joint = foot.AddComponent<ConfigurableJoint>();
            joint.connectedBody = root;
            BoxCollider collider = foot.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.25f;
            return joint;
        }

        public void Dispose()
        {
            Physics.IgnoreLayerCollision(28, 29, ignoredBefore);
            if (Result != null && Result.Target)
                UnityEngine.Object.DestroyImmediate(Result.Target.gameObject);
            if (puppetRoot) UnityEngine.Object.DestroyImmediate(puppetRoot);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
        }

        static object CreateBindings(
            BoneName root, ConfigurableJoint rootJoint,
            BoneName leftFoot, ConfigurableJoint leftJoint,
            BoneName rightFoot, ConfigurableJoint rightJoint)
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
            add.Invoke(dictionary, new object[] { leftFoot, leftJoint });
            add.Invoke(dictionary, new object[] { rightFoot, rightJoint });
            return dictionary;
        }

        static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(owner, value);
        }
    }
}
