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
            // Create feet offset before their joints are created. Teleporting
            // constrained bodies after setup is reverted by PhysX and does
            // not construct an Unrecoverable capture-margin scenario.
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, footCenterX: 5f);
            yield return null;
            // Root remains near origin while authored foot support segment is
            // far right: first classification must be Unrecoverable.
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.1f;
            rig.Stagger.MaxSteps = 1;

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);

            yield return RunUntilIdleOrTimeout(rig);

            Assert.That(rig.Controller.ActiveBehaviour, Is.InstanceOf<RagdollPuppetBehaviour>());
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));
        }

        [UnityTest]
        public IEnumerator StepActuator_CrossFadesStepStateAndRunsStepPhases()
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(
                "HairibarStaggerTests/StepRecovery");
            Assert.That(controller, Is.Not.Null,
                "Run Code Red/Ragdoll/Create Stagger Test Assets before PlayMode tests.");
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f,
                stepController: controller);
            yield return null;
            rig.Stagger.StableMargin = 0.05f;
            rig.Stagger.RequiresStepMargin = 0.25f;
            StaggerPhysicalRig.SetField(rig.Stagger, "liftOffDuration", 0.04f);
            StaggerPhysicalRig.SetField(rig.Stagger, "swingDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "replantDuration", 0.04f);
            StaggerPhysicalRig.SetField(rig.Stagger, "settlingDuration", 0.04f);
            StaggerPhysicalRig.SetField(rig.Stagger, "transitionDuration", 0f);

            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);
            // This lifecycle fixture intentionally freezes physics. Invoke the
            // RequiresStep actuator with its real initialized context; physical
            // push/lift/replant evidence requires a separate non-frozen rig.
            SetProperty(rig.Stagger, "CurrentState", RagdollBipedBalanceState.RequiresStep);
            InvokePrivate(rig.Stagger, "BeginStep");
            Assert.That(rig.Stagger.CurrentState,
                Is.EqualTo(RagdollBipedBalanceState.RequiresStep));
            yield return null;

            Assert.That(rig.TargetAnimator.GetInteger("StepSwingFoot"), Is.EqualTo(1),
                "Capture point lies left of both feet, so right trailing foot must swing.");
            Assert.That(rig.TargetAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.StepLeft")));

            // Sample animator directly before the ragdoll mapping pass can
            // reconcile the Target pose. This is actuator evidence only.
            rig.TargetAnimator.Update(0.15f);
            Assert.That(rig.LeftTarget.localPosition.x,
                Is.LessThan(-0.5f), "StepLeft test clip must move its authored target foot.");
            Assert.That(rig.RightTarget.localPosition.x,
                Is.EqualTo(0.5f).Within(0.001f),
                "StepLeft test clip must not move its other target foot.");
        }

        [UnityTest]
        public IEnumerator PhysicalStep_SelectedFootMovesWhileStanceFootStaysGrounded()
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(
                "HairibarStaggerTests/StepRecovery");
            Assert.That(controller, Is.Not.Null);
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, stepController: controller,
                freezeBodies: false);
            yield return new WaitForFixedUpdate();
            StaggerPhysicalRig.SetField(rig.Stagger, "transitionDuration", 0f);
            StaggerPhysicalRig.SetField(rig.Stagger, "liftOffDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "swingDuration", 0.18f);
            StaggerPhysicalRig.SetField(rig.Stagger, "replantDuration", 0.08f);
            StaggerPhysicalRig.SetField(rig.Stagger, "settlingDuration", 0.12f);
            Assert.That(rig.Controller.Activate<RagdollBipedStaggerBehaviour>(), Is.True);
            SetProperty(rig.Stagger, "CurrentState", RagdollBipedBalanceState.RequiresStep);
            Vector3 selectedStart = rig.LeftFootBody.position;
            Vector3 stanceStart = rig.RightFootBody.position;
            InvokePrivate(rig.Stagger, "BeginStep");

            float selectedPeakHeight = selectedStart.y;
            for (int frame = 0; frame < 18; frame++)
            {
                yield return new WaitForFixedUpdate();
                selectedPeakHeight = Mathf.Max(selectedPeakHeight,
                    rig.LeftFootBody.position.y);
            }

            float selectedTravel = Vector3.Distance(
                rig.LeftFootBody.position, selectedStart);
            float stanceTravel = Vector3.Distance(
                rig.RightFootBody.position, stanceStart);
            Assert.That(selectedTravel, Is.GreaterThan(0.02f),
                "Selected physical foot must move during the step clip.");
            Assert.That(stanceTravel, Is.LessThan(selectedTravel),
                "Stance foot must move less than selected swing foot.");
            Assert.That(selectedPeakHeight, Is.GreaterThan(selectedStart.y + 0.005f),
                "Selected physical foot must lift off the ground during Swing.");
            Assert.That(rig.LeftFootBody.position.y,
                Is.EqualTo(selectedStart.y).Within(0.08f),
                "Selected physical foot must replant by the end of the cycle.");
        }

        [UnityTest]
        public IEnumerator PhysicalPush_RequiresStepEventActivatesStaggerWithoutManualBeginStep()
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(
                "HairibarStaggerTests/StepRecovery");
            Assert.That(controller, Is.Not.Null);
            rig = new StaggerPhysicalRig(footOffsetX: 0.5f, stepController: controller,
                freezeBodies: false);
            rig.RootBody.constraints = RigidbodyConstraints.FreezeRotation;
            rig.Puppet.CanStagger = true;
            rig.Puppet.MinimumRequiresStepDuration = 0f;
            rig.Puppet.OnRequiresStep = new RagdollPuppetEvent
            {
                SwitchToBehaviour = typeof(RagdollBipedStaggerBehaviour).FullName
            };

            yield return new WaitForFixedUpdate();
            // 4.4 N*s / 2 kg = 2.2 m/s: capture point leaves left edge of
            // support by a small amount, producing RequiresStep, not knockout.
            rig.RootBody.AddForce(Vector3.left * 4.4f, ForceMode.Impulse);

            for (int frame = 0; frame < 12; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (rig.Controller.ActiveBehaviour is RagdollBipedStaggerBehaviour)
                    yield break;
            }

            Assert.Fail("Physical push did not route RequiresStep through OnRequiresStep to Stagger.");
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

        static void SetProperty(object owner, string name, object value)
        {
            PropertyInfo property = owner.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(owner, value);
        }

        static void InvokePrivate(object owner, string name)
        {
            MethodInfo method = owner.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(owner, null);
        }
    }

    internal sealed class StaggerPhysicalRig : IDisposable
    {
        readonly GameObject puppetRoot;
        readonly RagdollDefinition definition;
        readonly RagdollAnimationProfile profile;
        readonly bool ignoredBefore;
        GameObject ground;

        internal RagdollSetupResult Result { get; }
        internal RagdollPuppetBehaviour Puppet => Result.PuppetBehaviour;
        internal RagdollBehaviourController Controller => Result.Behaviours;
        internal RagdollBipedStaggerBehaviour Stagger { get; }
        internal Rigidbody RootBody { get; }
        internal Rigidbody LeftFootBody { get; }
        internal Rigidbody RightFootBody { get; }
        internal Animator TargetAnimator { get; }
        internal Transform LeftTarget { get; }
        internal Transform RightTarget { get; }

        internal StaggerPhysicalRig(float footOffsetX, float footCenterX = 0f,
            RuntimeAnimatorController stepController = null, bool freezeBodies = true)
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
            BoneName rootName = new BoneName("Root");
            BoneName leftFootName = new BoneName("foot_l");
            BoneName rightFootName = new BoneName("foot_r");

            puppetRoot = new GameObject("Stagger Puppet");
            puppetRoot.SetActive(false);
            GameObject leftFoot = new GameObject("foot_l");
            leftFoot.transform.SetParent(puppetRoot.transform, false);
            leftFoot.transform.localPosition = new Vector3(footCenterX - footOffsetX, -1f, 0f);
            GameObject rightFoot = new GameObject("foot_r");
            rightFoot.transform.SetParent(puppetRoot.transform, false);
            rightFoot.transform.localPosition = new Vector3(footCenterX + footOffsetX, -1f, 0f);

            RootBody = puppetRoot.AddComponent<Rigidbody>();
            RootBody.useGravity = false;
            RootBody.constraints = freezeBodies
                ? RigidbodyConstraints.FreezeAll
                : RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            RootBody.mass = 2f;
            ConfigurableJoint rootJoint = puppetRoot.AddComponent<ConfigurableJoint>();
            BoxCollider rootCollider = puppetRoot.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one * 0.75f;

            ConfigurableJoint leftJoint = ConfigureFoot(leftFoot, RootBody, freezeBodies);
            LeftFootBody = leftFoot.GetComponent<Rigidbody>();
            ConfigurableJoint rightJoint = ConfigureFoot(rightFoot, RootBody, freezeBodies);
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
            TargetAnimator = target.AddComponent<Animator>();
            TargetAnimator.runtimeAnimatorController = stepController;
            GameObject leftTarget = new GameObject("foot_l");
            leftTarget.transform.SetParent(target.transform, false);
            leftTarget.transform.localPosition = new Vector3(footCenterX - footOffsetX, -1f, 0f);
            LeftTarget = leftTarget.transform;
            GameObject rightTarget = new GameObject("foot_r");
            rightTarget.transform.SetParent(target.transform, false);
            rightTarget.transform.localPosition = new Vector3(footCenterX + footOffsetX, -1f, 0f);
            RightTarget = rightTarget.transform;

            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            Result = RagdollRuntimeSetupService.ConfigureSeparated(
                target.transform, bindings, profile, 28, 29);
            Assert.That(Result.Succeeded, Is.True, Result.Error);
            Result.PuppetBehaviour.CanStagger = true;
            Result.PuppetBehaviour.LoseBalanceOnTargetDrift = false;

            if (!freezeBodies)
            {
                RootBody.isKinematic = false;
                LeftFootBody.isKinematic = false;
                RightFootBody.isKinematic = false;
                LeftFootBody.useGravity = true;
                RightFootBody.useGravity = true;
                ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Stagger Test Ground";
                ground.transform.position = new Vector3(0f, -1.15f, 0f);
                ground.transform.localScale = new Vector3(10f, 0.1f, 10f);
                ground.layer = 0;
            }

            Stagger = Result.PuppetBehaviour.gameObject
                .AddComponent<RagdollBipedStaggerBehaviour>();
        }

        static ConfigurableJoint ConfigureFoot(GameObject foot, Rigidbody root, bool freeze)
        {
            Rigidbody body = foot.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = freeze ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.FreezeRotation;
            body.mass = 0.5f;
            ConfigurableJoint joint = foot.AddComponent<ConfigurableJoint>();
            joint.connectedBody = root;
            if (!freeze)
            {
                joint.xMotion = ConfigurableJointMotion.Free;
                joint.yMotion = ConfigurableJointMotion.Free;
                joint.zMotion = ConfigurableJointMotion.Free;
            }
            BoxCollider collider = foot.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.25f;
            return joint;
        }

        public void Dispose()
        {
            Physics.IgnoreLayerCollision(28, 29, ignoredBefore);
            if (Result != null && Result.Target)
                UnityEngine.Object.DestroyImmediate(Result.Target.gameObject);
            if (ground) UnityEngine.Object.DestroyImmediate(ground);
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

        internal static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(owner, value);
        }
    }
}
