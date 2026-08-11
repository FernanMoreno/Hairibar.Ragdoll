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
        List<string> kinematicVelocityWarnings;
        bool ignoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            baselineMaterial = new PhysicsMaterial("setup baseline");
            temporaryMaterial = new PhysicsMaterial("setup temporary");
            dynamicObjects = new List<GameObject>();
            kinematicVelocityWarnings = new List<string>();
            Application.logMessageReceived += CaptureKinematicVelocityWarning;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CaptureKinematicVelocityWarning;
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
            Assert.That(kinematicVelocityWarnings, Is.Empty,
                string.Join("\n", kinematicVelocityWarnings));
        }

        void CaptureKinematicVelocityWarning(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (type == LogType.Warning
                && condition.IndexOf("velocity of a kinematic body",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kinematicVelocityWarnings.Add(condition + "\n" + stackTrace);
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
            result.PuppetBehaviour.SetColliders(true);
            Assert.That(result.PuppetBehaviour.SurfaceState,
                Is.EqualTo(RagdollPuppetColliderSurfaceState.Unpinned));
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
            result.PuppetBehaviour.Unpin();
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

        [UnityTest]
        public IEnumerator ConfigureSeparated_BindingFailureRollsBackAllCreatedState()
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
            yield return null;
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
            yield return null;
            Assert.That(result.Target.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(result.Target.GetComponent<Collider>(), Is.Null);
            Assert.That(result.Target.GetComponent<ConfigurableJoint>(), Is.Null);
            Assert.That(result.Target.GetComponent<RagdollDefinitionBindings>(), Is.Null);
            Assert.That(foreign, Is.Not.Null);
            Assert.That(result.Animator, Is.Not.Null);
            Assert.That(result.Puppet.GetComponent<RagdollDefinitionBindings>(),
                Is.Not.Null);

            Assert.That(result.Muscles.IsInitialized, Is.True);
            Assert.That(result.Behaviours.IsInitialized, Is.True);

            UnityEngine.Object.DestroyImmediate(result.Root.gameObject);
            puppetRoot = null;
        }

        [UnityTest]
        public IEnumerator CoreHooksPreserveOrderAndIsolateEverySubscriber()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            targetRoot.SetActive(false);
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            targetRoot.SetActive(true);
            yield return null;
            Assert.That(result.Animator.Initiated, Is.True,
                "Core hooks require the initialized runtime pipeline.");

            List<string> order = new List<string>();
            result.Animator.OnFixTransforms += () =>
                throw new InvalidOperationException("expected fix hook failure");
            result.Animator.OnFixTransforms += () => order.Add("fix");
            result.Animator.OnRead += () =>
                throw new InvalidOperationException("expected read hook failure");
            result.Animator.OnRead += () => order.Add("read");
            result.Animator.OnWrite += () =>
                throw new InvalidOperationException("expected write hook failure");
            result.Animator.OnWrite += () => order.Add("write");
            result.Animator.OnPostLateUpdate += () =>
                throw new InvalidOperationException("expected post hook failure");
            result.Animator.OnPostLateUpdate += () => order.Add("post");
            List<string> exceptions = new List<string>();
            Application.LogCallback capture = (message, stackTrace, type) =>
            {
                if (type == LogType.Exception) exceptions.Add(message);
            };
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            Application.logMessageReceived += capture;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                InvokePrivate(result.Animator,
                    "FixTargetTransformsAtUpdateBoundary");
                InvokePrivate(result.Animator, "InvokeReadHooks");
                InvokePrivate(result.Animator, "InvokeWriteHooks");
                InvokePrivate(result.Animator, "InvokePostLateUpdateHook");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
                Application.logMessageReceived -= capture;
            }

            CollectionAssert.AreEqual(
                new[] { "fix", "read", "write", "post" }, order);
            Assert.That(exceptions, Has.Count.EqualTo(4));
            StringAssert.Contains("expected fix hook failure", exceptions[0]);
            StringAssert.Contains("expected read hook failure", exceptions[1]);
            StringAssert.Contains("expected write hook failure", exceptions[2]);
            StringAssert.Contains("expected post hook failure", exceptions[3]);
        }

        [UnityTest]
        public IEnumerator CollectionValidationFailuresLeaveRegistryAndPhysicsUntouched()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            // This scenario certifies transactional hierarchy preservation. The
            // documented BehaviourPuppet policy drops held props when entering
            // Unpinned, so disable that independent policy before deliberately
            // severing a branch; prop-drop behaviour is covered by its own tests.
            result.PuppetBehaviour.DropProps = false;
            yield return null;
            yield return new WaitForFixedUpdate();

            int generation = puppet.RegistryGeneration;
            RagdollBone root = puppet.Root;
            RagdollBone child = puppet.GetBoneAt(1);
            Rigidbody connectedBody = child.Joint.connectedBody;
            Transform childParent = child.Transform.parent;
            RagdollRuntimeMuscleRegistration rootRegistration =
                new RagdollRuntimeMuscleRegistration(
                    root.Name,
                    root.Joint,
                    targetRoot.transform,
                    RagdollMuscleGroup.Hips);
            RagdollHierarchyTransactionResult setResult;
            Assert.That(result.Animator.TrySetMuscles(
                new[] { rootRegistration, rootRegistration },
                out setResult), Is.False);
            Assert.That(puppet.RegistryGeneration, Is.EqualTo(generation));
            Assert.That(child.Joint.connectedBody, Is.SameAs(connectedBody));
            Assert.That(child.Transform.parent, Is.SameAs(childParent));

            RagdollHierarchyTransactionResult replaceResult;
            Assert.That(result.Animator.TryReplaceMuscles(
                new[]
                {
                    new RagdollMuscleReplacement(
                        puppet.GetHandleAt(1),
                        new RagdollRuntimeMuscleRegistration(
                            child.Name,
                            root.Joint,
                            targetRoot.transform.GetChild(0),
                            RagdollMuscleGroup.Spine))
                },
                out replaceResult), Is.False);
            Assert.That(puppet.RegistryGeneration, Is.EqualTo(generation));
            Assert.That(puppet.GetBoneAt(1).Joint, Is.SameAs(child.Joint));
            Assert.That(child.Joint.connectedBody, Is.SameAs(connectedBody));
        }

        [UnityTest]
        public IEnumerator RuntimeHierarchyLayoutAndPoseUtilitiesMatchPublicContract()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            yield return null;

            RagdollBone rootBone = puppet.Root;
            RagdollBone childBone = puppet.GetBoneAt(1);
            Transform treeParent = childBone.Transform.parent;
            Rigidbody connectedBody = childBone.Joint.connectedBody;
            Vector3 worldPosition = childBone.Transform.position;

            Assert.That(result.Animator.HierarchyIsFlat(), Is.False);
            GameObject wrongContainer = new GameObject("Wrong Puppet Container");
            dynamicObjects.Add(wrongContainer);
            rootBone.Transform.SetParent(wrongContainer.transform, true);
            Assert.That(result.Animator.HierarchyIsFlat(), Is.False,
                "The root muscle participates in flat-hierarchy validation.");
            result.Animator.FlattenHierarchy();
            Assert.That(result.Animator.HierarchyIsFlat(), Is.True);
            Assert.That(rootBone.Transform.parent, Is.Null);
            Assert.That(childBone.Transform.parent,
                Is.SameAs(rootBone.Transform.parent));
            Assert.That(childBone.Transform.position, Is.EqualTo(worldPosition));
            Assert.That(childBone.Joint.connectedBody, Is.SameAs(connectedBody));

            result.Animator.TreeHierarchy();
            Assert.That(result.Animator.HierarchyIsFlat(), Is.False);
            Assert.That(childBone.Transform.parent, Is.SameAs(treeParent));
            Assert.That(childBone.Joint.connectedBody, Is.SameAs(connectedBody));

            RagdollAnimator.AnimatedPair childPair = null;
            for (int index = 0; index < result.Behaviours.Context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair candidate =
                    result.Behaviours.Context.Pairs[index];
                if (candidate.RagdollBone == childBone)
                {
                    childPair = candidate;
                    break;
                }
            }
            Assert.That(childPair, Is.Not.Null);
            Vector3 expectedPosition = childPair.currentPose.worldPosition;
            Quaternion expectedRotation = childPair.currentPose.worldRotation;
            childBone.Rigidbody.position += Vector3.right * 3f;
            Quaternion beforeRotation = childBone.Rigidbody.rotation;
            result.Animator.FixMusclePositions();
            Assert.That(Vector3.Distance(
                childBone.Rigidbody.position,
                expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(childBone.Rigidbody.rotation, Is.EqualTo(beforeRotation));

            childBone.Rigidbody.rotation = Quaternion.Euler(20f, 30f, 40f);
            result.Animator.FixMusclePositionsAndRotations();
            Assert.That(Quaternion.Angle(
                childBone.Rigidbody.rotation,
                expectedRotation),
                Is.LessThan(0.01f));

            GameObject grandchild = new GameObject("Grandchild");
            grandchild.transform.SetParent(childBone.Transform, false);
            Rigidbody grandchildBody = grandchild.AddComponent<Rigidbody>();
            ConfigurableJoint grandchildJoint =
                grandchild.AddComponent<ConfigurableJoint>();
            grandchildJoint.connectedBody = childBone.Rigidbody;
            grandchild.AddComponent<BoxCollider>();
            Transform grandchildTarget = new GameObject("Grandchild").transform;
            grandchildTarget.SetParent(childPair.TargetBone, false);
            grandchildTarget.localPosition = Vector3.up;
            yield return new WaitForFixedUpdate();
            Assert.That(Time.inFixedTimeStep, Is.True,
                "The hierarchy mutation fixture must resume inside FixedUpdate.");
            result.Animator.AddMuscle(new RagdollRuntimeMuscleRegistration(
                new BoneName("Grandchild"),
                grandchildJoint,
                grandchildTarget,
                RagdollMuscleGroup.Spine,
                childPair.TargetBone,
                true,
                false));

            GameObject replacement = new GameObject("Child Replacement");
            replacement.transform.SetParent(rootBone.Transform, false);
            Rigidbody replacementBody = replacement.AddComponent<Rigidbody>();
            ConfigurableJoint replacementJoint =
                replacement.AddComponent<ConfigurableJoint>();
            replacementJoint.connectedBody = rootBone.Rigidbody;
            replacement.AddComponent<BoxCollider>();
            RagdollBoneHandle staleChildHandle;
            Assert.That(puppet.TryGetBoneHandle(
                childBone.Name, out staleChildHandle), Is.True);
            RagdollBoneHandle replacementHandle;
            string replacementError;
            yield return new WaitForFixedUpdate();
            Assert.That(Time.inFixedTimeStep, Is.True,
                "The hierarchy replacement fixture must resume inside FixedUpdate.");
            Assert.That(result.Animator.TryReplaceMuscle(
                staleChildHandle,
                new RagdollRuntimeMuscleRegistration(
                    childBone.Name,
                    replacementJoint,
                    childPair.TargetBone,
                    RagdollMuscleGroup.Spine,
                    result.Target,
                    true,
                    false),
                out replacementHandle,
                out replacementError), Is.True, replacementError);
            Assert.That(replacementHandle.IsValid, Is.True);
            Assert.That(puppet.Topology.Contains(staleChildHandle), Is.False);
            Assert.That(grandchildJoint.connectedBody, Is.SameAs(replacementBody),
                "Replacing a branch root must reconnect its retained direct child.");
        }

        [UnityTest]
        public IEnumerator OfficialStateWeightsAndModeFacadesAffectLiveRuntime()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            yield return null;

            int stateNotifications = 0;
            result.PuppetBehaviour.StateChanged += (_, __, ___) =>
                throw new InvalidOperationException(
                    "expected state subscriber failure");
            result.PuppetBehaviour.StateChanged += (_, __, ___) =>
                stateNotifications++;
            LogAssert.Expect(LogType.Exception,
                new Regex("expected state subscriber failure"));
            result.PuppetBehaviour.State = RagdollPuppetState.Unpinned;
            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.Unpinned));
            LogAssert.Expect(LogType.Exception,
                new Regex("expected state subscriber failure"));
            result.PuppetBehaviour.State = RagdollPuppetState.GetUp;
            Assert.That(result.PuppetBehaviour.State,
                Is.EqualTo(RagdollPuppetState.GetUp));
            LogAssert.Expect(LogType.Exception,
                new Regex("expected state subscriber failure"));
            result.PuppetBehaviour.State = RagdollPuppetState.Puppet;
            Assert.That(stateNotifications, Is.EqualTo(3));

            result.Animator.SetMuscleWeights(
                1, 0.25f, 0.5f, 0.75f, 1.5f);
            MuscleRuntimeState childState = result.Muscles.GetState(
                puppet.GetHandleAt(1));
            Assert.That(childState.RotationAuthority, Is.EqualTo(0.25f));
            Assert.That(childState.PositionAuthority, Is.EqualTo(0.5f));
            Assert.That(childState.PositionMappingAuthority, Is.EqualTo(0.75f));
            Assert.That(childState.RotationDampingMultiplier, Is.EqualTo(1.5f));

            result.Animator.SetMuscleWeightsRecursive(
                0, 0.4f, 0.6f, 0.8f, 1.2f);
            for (int index = 0; index < puppet.BoneCount; index++)
            {
                MuscleRuntimeState state = result.Muscles.GetState(
                    puppet.GetHandleAt(index));
                Assert.That(state.RotationAuthority, Is.EqualTo(0.4f));
                Assert.That(state.PositionAuthority, Is.EqualTo(0.6f));
                Assert.That(state.PositionMappingAuthority, Is.EqualTo(0.8f));
                Assert.That(state.RotationDampingMultiplier, Is.EqualTo(1.2f));
            }

            Assert.That(result.Animator.Initiated, Is.True);
            result.Simulation.SetModeImmediate(RagdollSimulationMode.Kinematic);
            Assert.That(result.Animator.Mode,
                Is.EqualTo(RagdollSimulationMode.Kinematic));
            Assert.That(result.Animator.IsActive, Is.False);
            result.Simulation.SetModeImmediate(RagdollSimulationMode.Active);
            Assert.That(result.Animator.IsActive, Is.True);
            // MasterMuscleDamper affects the ConfigurableJoint drive of powered
            // muscles. Kinematic muscles are pose-driven and do not consume a drive.
            puppet.Root.PowerSetting = PowerSetting.Powered;
            result.Animator.MasterMuscleDamper = 7f;
            result.Animator.MasterMuscleDamperMultiplier = 1.5f;
            Assert.That(result.Animator.MasterMuscleDamper, Is.EqualTo(7f));
            Assert.That(result.Animator.MasterMuscleDamperMultiplier,
                Is.EqualTo(1.5f));
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.Root.Joint.slerpDrive.positionDamper,
                Is.GreaterThanOrEqualTo(7f),
                "MasterMuscleDamper is an absolute JointDrive.positionDamper channel.");
        }

        [UnityTest]
        public IEnumerator FallBehaviourUsesExplicitTargetAnimator()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);

            GameObject externalTarget = new GameObject("External Animator");
            dynamicObjects.Add(externalTarget);
            Animator explicitAnimator = externalTarget.AddComponent<Animator>();
            result.Animator.TargetAnimator = explicitAnimator;
            RagdollFallBehaviour fall =
                result.PuppetBehaviour.gameObject.AddComponent<RagdollFallBehaviour>();
            fall.StateName = string.Empty;
            fall.enabled = false;
            targetRoot.SetActive(true);
            yield return null;

            Assert.That(fall.IsInitialized, Is.True,
                "BehaviourFall must exist under Character Behaviours before the "
                + "controller initializes.");
            Assert.That(fall.Activate(), Is.True);
            Assert.That(fall.IsActive, Is.True);
            FieldInfo field = typeof(RagdollFallBehaviour).GetField(
                "targetAnimator",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field.GetValue(fall), Is.SameAs(explicitAnimator));
        }

        [UnityTest]
        public IEnumerator ReplaceRootMusclePreservesTargetAndDisconnectedChildren()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            yield return null;

            RagdollBone oldRoot = puppet.Root;
            RagdollBone child = puppet.GetBoneAt(1);
            RagdollBoneHandle staleRoot = puppet.GetHandleAt(0);
            RagdollBoneHandle childHandle = puppet.GetHandleAt(1);
            Transform rootTarget = result.Behaviours.Context.Pairs[0].TargetBone;
            GameObject replacement = new GameObject("Root Replacement");
            dynamicObjects.Add(replacement);
            Rigidbody replacementBody = replacement.AddComponent<Rigidbody>();
            ConfigurableJoint replacementJoint =
                replacement.AddComponent<ConfigurableJoint>();
            replacement.AddComponent<BoxCollider>();

            result.Animator.DisconnectMuscleRecursive(
                childHandle,
                RagdollMuscleDisconnectMode.Sever);
            yield return new WaitForFixedUpdate();
            Assert.That(result.Animator.GetMuscleConnectionState(childHandle),
                Is.EqualTo(RagdollMuscleConnectionState.Disconnected));
            Rigidbody disconnectedConnection = child.Joint.connectedBody;

            yield return new WaitForFixedUpdate();
            RagdollBoneHandle replacementHandle;
            string error;
            Assert.That(result.Animator.TryReplaceMuscle(
                staleRoot,
                new RagdollRuntimeMuscleRegistration(
                    oldRoot.Name,
                    replacementJoint,
                    rootTarget,
                    RagdollMuscleGroup.Hips,
                    null,
                    false,
                    true),
                out replacementHandle,
                out error), Is.True, error);

            Assert.That(puppet.Topology.Contains(staleRoot), Is.False);
            Assert.That(puppet.Root.Joint, Is.SameAs(replacementJoint));
            Assert.That(child.Joint.connectedBody,
                Is.SameAs(disconnectedConnection),
                "A severed child must preserve its physical connection snapshot.");
            RagdollBoneHandle rebuiltChild;
            Assert.That(puppet.TryGetBoneHandle(child.Name, out rebuiltChild), Is.True);
            Assert.That(result.Animator.GetMuscleConnectionState(rebuiltChild),
                Is.EqualTo(RagdollMuscleConnectionState.Disconnected));
            Assert.That(replacementHandle.IsValid, Is.True);
        }

        [UnityTest]
        public IEnumerator ReplaceRootMuscle_PreservesHeldPropAdditionalPin_AndRollsBackCommitFailure()
        {
            RagdollDefinitionBindings puppet = CreatePuppet();
            CreateTarget("Child");
            ThrowOnceBoneProfileModifier rollbackProbe =
                targetRoot.AddComponent<ThrowOnceBoneProfileModifier>();
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    targetRoot.transform, puppet, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            yield return null;

            Rigidbody rootBody = puppet.Root.Rigidbody;
            GameObject slotObject = new GameObject("Held Prop Slot");
            dynamicObjects.Add(slotObject);
            Rigidbody slotBody = slotObject.AddComponent<Rigidbody>();
            ConfigurableJoint slotJoint =
                slotObject.AddComponent<ConfigurableJoint>();
            slotJoint.connectedBody = rootBody;
            slotObject.AddComponent<BoxCollider>();
            Transform targetSlot = new GameObject("Held Prop Target").transform;
            targetSlot.SetParent(targetRoot.transform, false);
            targetSlot.localPosition = Vector3.right;

            RagdollPropMuscle propMuscle =
                result.Animator.gameObject.AddComponent<RagdollPropMuscle>();
            string error;
            Assert.That(propMuscle.TryConfigureBeforeInitialization(
                result.Animator,
                slotJoint,
                targetSlot,
                targetRoot.transform,
                new BoneName("HeldProp"),
                false,
                true,
                out error), Is.True, error);
            propMuscle.Initialize();
            for (int frame = 0;
                frame < 30 && propMuscle.State != RagdollPropMuscleState.Empty;
                frame++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(propMuscle.State,
                Is.EqualTo(RagdollPropMuscleState.Empty), propMuscle.LastError);

            GameObject propObject = new GameObject("Held Prop");
            dynamicObjects.Add(propObject);
            Rigidbody standaloneBody = propObject.AddComponent<Rigidbody>();
            propObject.AddComponent<BoxCollider>();
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(propObject.transform, false);
            RagdollProp prop = propObject.AddComponent<RagdollProp>();
            Assert.That(prop.TryConfigureStandalone(
                visual, standaloneBody, out error), Is.True, error);
            prop.AddAdditionalPin();
            prop.AdditionalPin.Weight = 0.75f;
            prop.AdditionalPin.Mass = 2f;
            Assert.That(propMuscle.TrySetCurrentProp(prop, out error),
                Is.True, error);
            for (int frame = 0;
                frame < 30 && propMuscle.State != RagdollPropMuscleState.Holding;
                frame++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(propMuscle.State,
                Is.EqualTo(RagdollPropMuscleState.Holding), propMuscle.LastError);
            Assert.That(prop.CurrentRigidbody, Is.SameAs(slotBody));

            RagdollBoneHandle childHandle = puppet.GetHandleAt(1);
            RagdollBone child = puppet.GetBone(childHandle);
            result.Animator.DisconnectMuscleRecursive(
                childHandle, RagdollMuscleDisconnectMode.Sever);
            yield return new WaitForFixedUpdate();
            Assert.That(result.Animator.GetMuscleConnectionState(childHandle),
                Is.EqualTo(RagdollMuscleConnectionState.Disconnected));
            Rigidbody disconnectedConnection = child.Joint.connectedBody;

            for (int frame = 0;
                frame < 30
                    && result.Animator.PendingMuscleConnectionOperationCount != 0;
                frame++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(result.Animator.PendingMuscleConnectionOperationCount,
                Is.Zero,
                "Root replacement requires the preceding disconnect transaction "
                + "to have reached a stable physics boundary.");
            Assert.That(propMuscle.State,
                Is.EqualTo(RagdollPropMuscleState.Holding),
                "Disconnecting an unrelated branch must preserve a held prop when "
                + "BehaviourPuppet's independent DropProps policy is disabled.");

            RagdollBone oldRoot = puppet.Root;
            RagdollBoneHandle staleRoot = puppet.GetHandleAt(0);
            Transform rootTarget = result.Behaviours.Context.Pairs[0].TargetBone;
            GameObject replacementObject = new GameObject("Root Replacement A");
            dynamicObjects.Add(replacementObject);
            Rigidbody replacementBody =
                replacementObject.AddComponent<Rigidbody>();
            ConfigurableJoint replacementJoint =
                replacementObject.AddComponent<ConfigurableJoint>();
            replacementObject.AddComponent<BoxCollider>();

            RagdollBoneHandle replacementHandle;
            replacementHandle = RagdollBoneHandle.Invalid;
            bool replacementCommitted = result.Animator.TryReplaceMuscle(
                staleRoot,
                new RagdollRuntimeMuscleRegistration(
                    oldRoot.Name,
                    replacementJoint,
                    rootTarget,
                    RagdollMuscleGroup.Hips,
                    null,
                    false,
                    true),
                out replacementHandle,
                out error);
            Assert.That(replacementCommitted, Is.True, error);
            Assert.That(propMuscle.State,
                Is.EqualTo(RagdollPropMuscleState.Holding));
            Assert.That(prop.CurrentRigidbody, Is.SameAs(slotBody));
            Assert.That(prop.AdditionalPin.Enabled, Is.True);
            Assert.That(prop.AdditionalPin.Weight, Is.EqualTo(0.75f));
            RagdollBoneHandle rebuiltChild;
            Assert.That(puppet.TryGetBoneHandle(child.Name, out rebuiltChild), Is.True);
            Assert.That(result.Animator.GetMuscleConnectionState(rebuiltChild),
                Is.EqualTo(RagdollMuscleConnectionState.Disconnected));
            Assert.That(child.Joint.connectedBody,
                Is.SameAs(disconnectedConnection));

            GameObject rejectedObject = new GameObject("Root Replacement B");
            dynamicObjects.Add(rejectedObject);
            rejectedObject.AddComponent<Rigidbody>();
            ConfigurableJoint rejectedJoint =
                rejectedObject.AddComponent<ConfigurableJoint>();
            rejectedObject.AddComponent<BoxCollider>();
            rollbackProbe.ThrowOnNextInitialize = true;
            int generationBeforeRollback = puppet.RegistryGeneration;

            RagdollBoneHandle ignored;
            ignored = RagdollBoneHandle.Invalid;
            bool rollbackCommitted = result.Animator.TryReplaceMuscle(
                replacementHandle,
                new RagdollRuntimeMuscleRegistration(
                    oldRoot.Name,
                    rejectedJoint,
                    rootTarget,
                    RagdollMuscleGroup.Hips,
                    null,
                    false,
                    true),
                out ignored,
                out error);
            Assert.That(rollbackCommitted, Is.False);
            Assert.That(error, Does.Contain("rolled back"));
            Assert.That(puppet.Root.Joint, Is.SameAs(replacementJoint));
            Assert.That(puppet.RegistryGeneration,
                Is.EqualTo(generationBeforeRollback));
            Assert.That(propMuscle.State,
                Is.EqualTo(RagdollPropMuscleState.Holding));
            Assert.That(prop.CurrentRigidbody, Is.SameAs(slotBody));
            Assert.That(prop.AdditionalPin.Enabled, Is.True);
            Assert.That(prop.AdditionalPin.Weight, Is.EqualTo(0.75f));
            Assert.That(child.Joint.connectedBody,
                Is.SameAs(disconnectedConnection));
            Assert.That(replacementBody, Is.Not.Null);
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

        static void InvokePrivate(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, null);
        }

        static RagdollPuppetEvent CreateEvent(
            UnityEngine.Events.UnityAction action)
        {
            RagdollPuppetEvent value = new RagdollPuppetEvent();
            value.UnityEvent.AddListener(action);
            return value;
        }

        sealed class ThrowOnceBoneProfileModifier : MonoBehaviour,
            IBoneProfileModifier
        {
            public bool ThrowOnNextInitialize { get; set; }

            public void Initialize(
                IEnumerable<RagdollAnimator.AnimatedPair> pairs)
            {
                if (!ThrowOnNextInitialize) return;
                ThrowOnNextInitialize = false;
                throw new InvalidOperationException(
                    "Synthetic hierarchy rebuild failure.");
            }

            public void Modify(
                ref BoneProfile boneProfile,
                RagdollAnimator.AnimatedPair pair,
                float deltaTime)
            {
            }
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
