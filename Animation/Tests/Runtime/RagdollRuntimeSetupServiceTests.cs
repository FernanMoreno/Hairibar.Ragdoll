using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollRuntimeSetupServiceTests
    {
        GameObject puppetRoot;
        GameObject targetRoot;
        RagdollDefinition definition;
        RagdollAnimationProfile profile;
        PhysicsMaterial baselineMaterial;
        PhysicsMaterial temporaryMaterial;
        List<GameObject> dynamicObjects;
        bool ignoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            baselineMaterial = new PhysicsMaterial("setup baseline");
            temporaryMaterial = new PhysicsMaterial("setup temporary");
            dynamicObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            if (targetRoot) UnityEngine.Object.DestroyImmediate(targetRoot);
            if (puppetRoot) UnityEngine.Object.DestroyImmediate(puppetRoot);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
            if (baselineMaterial) UnityEngine.Object.DestroyImmediate(baselineMaterial);
            if (temporaryMaterial) UnityEngine.Object.DestroyImmediate(temporaryMaterial);
            for (int index = 0; index < dynamicObjects.Count; index++)
            {
                if (dynamicObjects[index])
                {
                    UnityEngine.Object.DestroyImmediate(dynamicObjects[index]);
                }
            }
        }

        [UnityTest]
        public IEnumerator ConvertDirectly_CreatesCompleteRuntimeAndLayerContract()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            targetRoot.AddComponent<UnityEngine.Animation>().animatePhysics = true;
            Collider rootCollider = puppetRoot.GetComponent<Collider>();
            Collider childCollider = puppetRoot.transform.GetChild(0)
                .GetComponent<Collider>();
            rootCollider.sharedMaterial = baselineMaterial;
            childCollider.sharedMaterial = baselineMaterial;
            childCollider.enabled = false;

            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                targetRoot.transform,
                puppet,
                profile,
                30,
                31);

            int postInitialized = 0;
            result.Animator.OnPostInitialized += () =>
                throw new InvalidOperationException("expected post-init subscriber failure");
            result.Animator.OnPostInitialized += () => postInitialized++;
            LogAssert.Expect(
                LogType.Exception,
                new Regex("expected post-init subscriber failure"));

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Animator, Is.Not.Null);
            Assert.That(result.Muscles, Is.Not.Null);
            Assert.That(result.Behaviours, Is.Not.Null);
            Assert.That(result.Simulation, Is.Not.Null);
            Assert.That(result.Collisions, Is.Not.Null);
            Assert.That(result.PuppetBehaviour, Is.Not.Null);
            Assert.That(result.PuppetBehaviour.transform.parent,
                Is.EqualTo(targetRoot.transform));
            Assert.That(result.PuppetBehaviour.name, Is.EqualTo("Character Behaviours"));
            Assert.That(targetRoot.layer, Is.EqualTo(30));
            Assert.That(targetRoot.transform.GetChild(0).gameObject.layer, Is.EqualTo(30));
            Assert.That(puppetRoot.layer, Is.EqualTo(31));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.True);

            yield return null;
            Assert.That(result.Muscles.IsInitialized, Is.True);
            Assert.That(result.Behaviours.IsInitialized, Is.True);
            Assert.That(result.Simulation.IsInitialized, Is.True);
            Assert.That(result.PuppetBehaviour.IsInitialized, Is.True);
            Assert.That(postInitialized, Is.EqualTo(1));

            List<string> hookTrace = new List<string>();
            Action failingRead = null;
            Action passingRead = null;
            Action failingWrite = null;
            Action passingWrite = null;
            Action failingFix = null;
            Action passingFix = null;
            Action failingPostLate = null;
            Action passingPostLate = null;
            failingRead = () =>
            {
                result.Animator.OnRead -= failingRead;
                throw new InvalidOperationException("expected OnRead failure");
            };
            passingRead = () =>
            {
                result.Animator.OnRead -= passingRead;
                hookTrace.Add("read");
            };
            failingWrite = () =>
            {
                result.Animator.OnWrite -= failingWrite;
                throw new InvalidOperationException("expected OnWrite failure");
            };
            passingWrite = () =>
            {
                result.Animator.OnWrite -= passingWrite;
                hookTrace.Add("write");
            };
            failingFix = () =>
            {
                result.Animator.OnFixTransforms -= failingFix;
                throw new InvalidOperationException("expected OnFixTransforms failure");
            };
            passingFix = () =>
            {
                result.Animator.OnFixTransforms -= passingFix;
                hookTrace.Add("fix");
            };
            failingPostLate = () =>
            {
                result.Animator.OnPostLateUpdate -= failingPostLate;
                throw new InvalidOperationException("expected OnPostLateUpdate failure");
            };
            passingPostLate = () =>
            {
                result.Animator.OnPostLateUpdate -= passingPostLate;
                hookTrace.Add("post");
            };
            result.Animator.OnRead += failingRead;
            result.Animator.OnRead += passingRead;
            result.Animator.OnWrite += failingWrite;
            result.Animator.OnWrite += passingWrite;
            result.Animator.OnFixTransforms += failingFix;
            result.Animator.OnFixTransforms += passingFix;
            result.Animator.OnPostLateUpdate += failingPostLate;
            result.Animator.OnPostLateUpdate += passingPostLate;
            LogAssert.ignoreFailingMessages = true;
            yield return new WaitForFixedUpdate();
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            CollectionAssert.AreEquivalent(
                new[] { "read", "write", "fix", "post" },
                hookTrace);
            Assert.That(result.PuppetBehaviour.SurfaceBaselineCaptured, Is.True);
            Assert.That(result.PuppetBehaviour.SetColliderSurfaceState(true), Is.True);
            Assert.That(childCollider.enabled, Is.False);

            rootCollider.sharedMaterial = temporaryMaterial;
            childCollider.enabled = true;
            Assert.That(result.Behaviours.DeactivateActiveBehaviour(), Is.True);
            Assert.That(rootCollider.sharedMaterial, Is.SameAs(baselineMaterial));
            Assert.That(childCollider.sharedMaterial, Is.SameAs(baselineMaterial));
            Assert.That(childCollider.enabled, Is.False);
            Assert.That(result.Behaviours.Activate(result.PuppetBehaviour), Is.True);

            Rigidbody rootBody = puppetRoot.GetComponent<Rigidbody>();
            Rigidbody childBody = puppetRoot.transform.GetChild(0)
                .GetComponent<Rigidbody>();
            rootBody.transform.position += Vector3.right * 2f;
            rootBody.linearVelocity = new Vector3(3f, 2f, 1f);
            childBody.angularVelocity = new Vector3(1f, 2f, 3f);
            Assert.That(result.PuppetBehaviour.LoseBalance(), Is.True);
            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.Unpinned));
            result.PuppetBehaviour.CanMoveTarget = false;
            Assert.That(result.PuppetBehaviour.BeginGetUpImmediately(
                RagdollGetUpOrientation.Prone), Is.True);
            Vector3 externallyOwnedPosition = targetRoot.transform.position;
            result.PuppetBehaviour.ModifyTargetPoseInternal(
                result.Behaviours.Context.Pairs);
            Assert.That(targetRoot.transform.position,
                Is.EqualTo(externallyOwnedPosition));

            Assert.That(result.PuppetBehaviour.InterruptGetUp(), Is.True);
            result.PuppetBehaviour.CanMoveTarget = true;
            Assert.That(result.PuppetBehaviour.BeginGetUpImmediately(
                RagdollGetUpOrientation.Prone), Is.True);
            result.PuppetBehaviour.ModifyTargetPoseInternal(
                result.Behaviours.Context.Pairs);
            Assert.That(Vector3.Distance(
                targetRoot.transform.position,
                externallyOwnedPosition), Is.GreaterThan(0.1f));
            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.GetUp));

            Vector3 respawnPosition = new Vector3(4f, 5f, 6f);
            Quaternion respawnRotation = Quaternion.Euler(0f, 70f, 0f);
            result.PuppetBehaviour.Respawn(respawnPosition, respawnRotation);

            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.Puppet));
            Assert.That(Vector3.Distance(
                targetRoot.transform.position,
                respawnPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(
                targetRoot.transform.rotation,
                respawnRotation), Is.LessThan(0.001f));
            Assert.That(rootBody.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(rootBody.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(childBody.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(childBody.angularVelocity, Is.EqualTo(Vector3.zero));

            int proneEvents = 0;
            int supineEvents = 0;
            result.PuppetBehaviour.QuadrupedGetUp = true;
            result.PuppetBehaviour.OnGetUpProne = CreateEvent(() => proneEvents++);
            result.PuppetBehaviour.OnGetUpSupine = CreateEvent(() => supineEvents++);

            rootBody.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            Physics.SyncTransforms();
            Assert.That(result.PuppetBehaviour.LoseBalance(), Is.True);
            Assert.That(result.PuppetBehaviour.BeginGetUpImmediately(), Is.True);
            Assert.That(result.PuppetBehaviour.GetUpOrientation,
                Is.EqualTo(RagdollGetUpOrientation.Prone));
            Assert.That(proneEvents, Is.EqualTo(1));
            Assert.That(supineEvents, Is.Zero);

            Assert.That(result.PuppetBehaviour.InterruptGetUp(), Is.True);
            rootBody.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Physics.SyncTransforms();
            Assert.That(result.PuppetBehaviour.BeginGetUpImmediately(), Is.True);
            Assert.That(result.PuppetBehaviour.GetUpOrientation,
                Is.EqualTo(RagdollGetUpOrientation.Supine));
            Assert.That(proneEvents, Is.EqualTo(1));
            Assert.That(supineEvents, Is.EqualTo(1));

            RagdollLifecycleSettings immediateLifecycle =
                new RagdollLifecycleSettings(
                    0f,
                    0f,
                    2f,
                    float.MaxValue,
                    false);
            result.PuppetBehaviour.Respawn(respawnPosition, respawnRotation);
            result.Animator.Kill(new RagdollLifecycleSettings(10f));
            yield return null;
            Assert.That(result.Animator.IsKilling, Is.True);
            result.PuppetBehaviour.Respawn(respawnPosition, respawnRotation);
            Assert.That(result.Animator.ActiveState,
                Is.EqualTo(RagdollLifecycleState.Alive));
            Assert.That(result.Animator.IsKilling, Is.False);

            result.Animator.Kill(immediateLifecycle);
            yield return null;
            Assert.That(result.Animator.ActiveState,
                Is.EqualTo(RagdollLifecycleState.Dead));
            Assert.That(result.Animator.TargetAnimation.enabled, Is.False);

            result.PuppetBehaviour.Respawn(respawnPosition, respawnRotation);
            Assert.That(result.Animator.ActiveState,
                Is.EqualTo(RagdollLifecycleState.Alive));
            Assert.That(result.Animator.TargetAnimation.enabled, Is.True);
            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.Puppet));

            result.Animator.Freeze(immediateLifecycle);
            yield return null;
            yield return null;
            Assert.That(result.Animator.ActiveState,
                Is.EqualTo(RagdollLifecycleState.Frozen));
            Assert.That(result.Animator.TargetAnimation.enabled, Is.False);

            result.PuppetBehaviour.Respawn(respawnPosition, respawnRotation);
            Assert.That(result.Animator.ActiveState,
                Is.EqualTo(RagdollLifecycleState.Alive));
            Assert.That(result.Animator.TargetAnimation.enabled, Is.True);
            Assert.That(result.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Active));
            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.Puppet));

            List<string> hierarchyEvents = new List<string>();
            int hierarchyChanged = 0;
            result.Animator.MuscleAdded += change =>
                hierarchyEvents.Add("+" + change.Bone);
            result.Animator.MuscleRemoved += change =>
                hierarchyEvents.Add("-" + change.Bone);
            result.Animator.HierarchyChanged += () => hierarchyChanged++;

            yield return new WaitForFixedUpdate();
            RagdollRuntimeMuscleRegistration addedRegistration =
                CreateDynamicRegistration("Extra", rootBody, targetRoot.transform);
            RagdollBoneHandle addedHandle =
                result.Animator.AddMuscle(addedRegistration);
            Assert.That(puppet.BoneCount, Is.EqualTo(3));
            Assert.That(puppet.Topology.Contains(addedHandle), Is.True);
            Assert.That(result.Behaviours.Context.Pairs.Count, Is.EqualTo(3));

            yield return new WaitForFixedUpdate();
            RagdollRuntimeMuscleRegistration replacementRegistration =
                CreateDynamicRegistration("Extra", rootBody, targetRoot.transform);
            RagdollBoneHandle replacementHandle = result.Animator.ReplaceMuscle(
                addedHandle,
                replacementRegistration);
            Assert.That(puppet.Topology.Contains(addedHandle), Is.False);
            Assert.That(puppet.Topology.Contains(replacementHandle), Is.True);
            Assert.That(puppet.BoneCount, Is.EqualTo(3));
            Assert.That(result.Behaviours.Context.Pairs.Count, Is.EqualTo(3));

            yield return new WaitForFixedUpdate();
            RagdollMuscleChange[] removed = result.Animator.RemoveMuscleRecursive(
                replacementRegistration.Joint);
            Assert.That(removed.Length, Is.EqualTo(1));
            Assert.That(puppet.Topology.Contains(replacementHandle), Is.False);
            Assert.That(puppet.BoneCount, Is.EqualTo(2));
            Assert.That(result.Behaviours.Context.Pairs.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[] { "+Extra", "-Extra", "+Extra", "-Extra" },
                hierarchyEvents);
            Assert.That(hierarchyChanged, Is.EqualTo(3));

            yield return new WaitForFixedUpdate();
            hierarchyEvents.Clear();
            hierarchyChanged = 0;
            RagdollBoneHandle oldChildHandle;
            Assert.That(puppet.TryGetBoneHandle(
                new BoneName("Child"), out oldChildHandle), Is.True);
            ConfigurableJoint rootJoint = puppetRoot.GetComponent<ConfigurableJoint>();
            RagdollRuntimeMuscleRegistration rootRegistration =
                new RagdollRuntimeMuscleRegistration(
                    new BoneName("Root"),
                    rootJoint,
                    targetRoot.transform,
                    RagdollMuscleGroup.Hips,
                    null,
                    false,
                    false);
            RagdollRuntimeMuscleRegistration collectionChild =
                CreateDynamicRegistration(
                    "Child",
                    rootBody,
                    targetRoot.transform);
            RagdollHierarchyTransactionResult collectionResult;
            Assert.That(result.Animator.TrySetMuscles(
                new[] { rootRegistration, collectionChild },
                out collectionResult), Is.True, collectionResult.Error);
            Assert.That(collectionResult.Succeeded, Is.True);
            Assert.That(collectionResult.Added.Count, Is.EqualTo(1));
            Assert.That(collectionResult.Removed.Count, Is.EqualTo(1));
            Assert.That(collectionResult.RegistryGeneration,
                Is.EqualTo(puppet.RegistryGeneration));
            Assert.That(puppet.Topology.Contains(oldChildHandle), Is.False);
            Assert.That(hierarchyChanged, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { "-Child", "+Child" }, hierarchyEvents);

            RagdollBoneHandle collectionChildHandle;
            Assert.That(puppet.TryGetBoneHandle(
                new BoneName("Child"), out collectionChildHandle), Is.True);
            int generationBeforeFailure = puppet.RegistryGeneration;
            RagdollHierarchyTransactionResult invalidResult;
            Assert.That(result.Animator.TrySetMuscles(
                new[] { rootRegistration, rootRegistration },
                out invalidResult), Is.False);
            Assert.That(invalidResult.Succeeded, Is.False);
            Assert.That(invalidResult.Error, Does.Contain("Duplicate"));
            Assert.That(puppet.RegistryGeneration,
                Is.EqualTo(generationBeforeFailure));
            Assert.That(puppet.Topology.Contains(collectionChildHandle), Is.True);

            yield return new WaitForFixedUpdate();
            RagdollRuntimeMuscleRegistration secondChild =
                CreateDynamicRegistration(
                    "Child",
                    rootBody,
                    targetRoot.transform);
            RagdollHierarchyTransactionResult replacementResult;
            Assert.That(result.Animator.TryReplaceMuscles(
                new[]
                {
                    new RagdollMuscleReplacement(
                        collectionChildHandle,
                        secondChild)
                },
                out replacementResult), Is.True, replacementResult.Error);
            Assert.That(puppet.Topology.Contains(collectionChildHandle), Is.False);
            Assert.That(replacementResult.Succeeded, Is.True);
        }

        [Test]
        public void ConfigureSeparated_BindingFailureRollsBackAllCreatedState()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("WrongName");
            targetRoot.layer = 4;
            targetRoot.transform.GetChild(0).gameObject.layer = 5;
            puppetRoot.layer = 6;
            Physics.IgnoreLayerCollision(30, 31, false);

            RagdollSetupResult result = RagdollRuntimeSetupService.ConfigureSeparated(
                targetRoot.transform,
                puppet,
                profile,
                30,
                31);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.Not.Empty);
            Assert.That(targetRoot.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(targetRoot.GetComponent<RagdollTargetBindings>(), Is.Null);
            Assert.That(targetRoot.transform.Find("Character Behaviours"), Is.Null);
            Assert.That(targetRoot.layer, Is.EqualTo(4));
            Assert.That(targetRoot.transform.GetChild(0).gameObject.layer, Is.EqualTo(5));
            Assert.That(puppetRoot.layer, Is.EqualTo(6));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.False);
        }

        [UnityTest]
        public IEnumerator DuplicateAndConvert_CreatesRootAndStripsOnlyTargetPhysics()
        {
            RagdollDefinitionBindings original = CreatePuppet();
            ForeignSetupMarker foreign =
                puppetRoot.AddComponent<ForeignSetupMarker>();

            RagdollSetupResult result =
                RagdollRuntimeSetupService.DuplicateAndConvertOriginalToTarget(
                    original,
                    profile,
                    30,
                    31);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Root, Is.Not.Null);
            Assert.That(result.Target, Is.SameAs(puppetRoot.transform));
            Assert.That(result.Puppet, Is.Not.Null.And.Not.SameAs(result.Target));
            Assert.That(result.Root, Is.SameAs(result.Target.parent));
            Assert.That(result.Puppet.parent, Is.SameAs(result.Root));
            Assert.That(result.Target.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(result.Target.GetComponent<Collider>(), Is.Null);
            Assert.That(result.Target.GetComponent<ConfigurableJoint>(), Is.Null);
            Assert.That(result.Target.GetComponent<RagdollDefinitionBindings>(), Is.Null);
            Assert.That(foreign, Is.Not.Null);
            Assert.That(result.Animator, Is.Not.Null);
            Assert.That(result.Puppet.GetComponent<RagdollDefinitionBindings>(),
                Is.Not.Null);

            yield return null;
            Assert.That(result.Muscles.IsInitialized, Is.True);
            Assert.That(result.Behaviours.IsInitialized, Is.True);

            UnityEngine.Object.DestroyImmediate(result.Root.gameObject);
            puppetRoot = null;
        }

        [Test]
        public void DuplicateAndConvert_FailureRestoresOriginalHierarchyAndPhysics()
        {
            RagdollDefinitionBindings original = CreatePuppet();
            Rigidbody originalBody = puppetRoot.GetComponent<Rigidbody>();
            int originalLayer = puppetRoot.layer;

            RagdollSetupResult result =
                RagdollRuntimeSetupService.DuplicateAndConvertOriginalToTarget(
                    original,
                    profile,
                    30,
                    30);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.Not.Empty);
            Assert.That(puppetRoot.transform.parent, Is.Null);
            Assert.That(puppetRoot.name, Is.EqualTo("Puppet"));
            Assert.That(puppetRoot.activeSelf, Is.True);
            Assert.That(puppetRoot.layer, Is.EqualTo(originalLayer));
            Assert.That(puppetRoot.GetComponent<Rigidbody>(), Is.SameAs(originalBody));
            Assert.That(puppetRoot.GetComponent<RagdollDefinitionBindings>(),
                Is.SameAs(original));
            Assert.That(puppetRoot.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(GameObject.Find("Puppet Ragdoll"), Is.Null);
        }

        RagdollDefinitionBindings CreatePuppet()
        {
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");
            puppetRoot = new GameObject("Puppet");
            puppetRoot.SetActive(false);
            GameObject child = new GameObject("Child");
            child.transform.SetParent(puppetRoot.transform, false);

            Rigidbody rootBody = puppetRoot.AddComponent<Rigidbody>();
            ConfigurableJoint rootJoint = puppetRoot.AddComponent<ConfigurableJoint>();
            puppetRoot.AddComponent<BoxCollider>();
            child.AddComponent<Rigidbody>();
            ConfigurableJoint childJoint = child.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            child.AddComponent<BoxCollider>();

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
            Assert.That(bindings.IsInitialized, Is.True);
            return bindings;
        }

        void CreateTarget(string childName)
        {
            targetRoot = new GameObject("Puppet");
            GameObject child = new GameObject(childName);
            child.transform.SetParent(targetRoot.transform, false);
            child.transform.localPosition = Vector3.up;
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

        static RagdollPuppetEvent CreateEvent(
            UnityEngine.Events.UnityAction action)
        {
            RagdollPuppetEvent value = new RagdollPuppetEvent();
            value.UnityEvent.AddListener(action);
            return value;
        }

        RagdollRuntimeMuscleRegistration CreateDynamicRegistration(
            string name,
            Rigidbody connectedBody,
            Transform targetParent)
        {
            GameObject physical = new GameObject(name + " Puppet");
            physical.transform.position = connectedBody.position + Vector3.up;
            physical.AddComponent<Rigidbody>();
            ConfigurableJoint joint = physical.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            physical.AddComponent<BoxCollider>();

            GameObject animated = new GameObject(name);
            animated.transform.SetParent(targetParent, false);
            animated.transform.localPosition = Vector3.up * 2f;
            dynamicObjects.Add(physical);
            dynamicObjects.Add(animated);
            return new RagdollRuntimeMuscleRegistration(
                new BoneName(name),
                joint,
                animated.transform,
                RagdollMuscleGroup.Prop,
                targetParent);
        }
    }

    public sealed class ForeignSetupMarker : MonoBehaviour
    {
    }
}
