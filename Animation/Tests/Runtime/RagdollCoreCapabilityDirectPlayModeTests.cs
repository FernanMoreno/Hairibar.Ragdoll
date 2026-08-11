using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Direct executable evidence for PuppetMaster core contracts. Every test builds
    /// its own physical Target/Puppet pair through the production setup service and
    /// invokes production APIs only; no test fixture or [Test] method is reused.
    /// </summary>
    public sealed class RagdollCoreCapabilityDirectPlayModeTests
    {
        CoreRig rig;
        SimulationMode originalSimulationMode;

        [SetUp]
        public void SetUp()
        {
            originalSimulationMode = Physics.simulationMode;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics.simulationMode = originalSimulationMode;
            rig?.Dispose();
            rig = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator B08_KillBlendAndDeadDrive()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            yield return new WaitForFixedUpdate();
            JointDrive authored = rig.RootJoint.slerpDrive;
            Assert.That(authored.positionSpring, Is.GreaterThan(0f),
                "The powered authored drive must be materialized before Kill is sampled.");

            rig.Animator.Kill(new RagdollLifecycleSettings(0f, 0.2f, 9f));
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.ActiveState, Is.EqualTo(RagdollLifecycleState.Dead));
            Assert.That(rig.RootJoint.slerpDrive.positionSpring,
                Is.EqualTo(authored.positionSpring * 0.2f).Within(0.01f));
            Assert.That(rig.RootJoint.slerpDrive.positionDamper,
                Is.GreaterThanOrEqualTo(9f));

            rig.Animator.Resurrect();
            yield return null;
            rig.Result.PuppetBehaviour.State = RagdollPuppetState.Puppet;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.ActiveState, Is.EqualTo(RagdollLifecycleState.Alive));
            Assert.That(rig.RootJoint.slerpDrive.positionSpring,
                Is.EqualTo(authored.positionSpring).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator B09_TemporaryAndPermanentFreeze()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            rig.RootBody.linearVelocity = Vector3.right * 2f;
            var temporary = new RagdollLifecycleSettings(
                0f, 0f, 2f, 0.01f, false);
            rig.Animator.Freeze(temporary);
            yield return null;
            Assert.That(rig.Animator.ActiveState, Is.EqualTo(RagdollLifecycleState.Dead));
            Assert.That(rig.Animator.IsWaitingForFreeze, Is.True);

            rig.RootBody.linearVelocity = Vector3.zero;
            rig.ChildBody.linearVelocity = Vector3.zero;
            for (int step = 0;
                step < 10 && rig.Animator.ActiveState != RagdollLifecycleState.Frozen;
                step++) yield return null;
            Assert.That(rig.Animator.ActiveState, Is.EqualTo(RagdollLifecycleState.Frozen));

            rig.Animator.Resurrect();
            yield return null;
            Assert.That(rig.Animator.ActiveState, Is.EqualTo(RagdollLifecycleState.Alive));
            Assert.That(rig.Result.Puppet.gameObject.activeSelf, Is.True);

            RagdollAnimator permanentlyFrozenAnimator = rig.Animator;
            rig.RootBody.linearVelocity = Vector3.zero;
            rig.ChildBody.linearVelocity = Vector3.zero;
            rig.Animator.Freeze(new RagdollLifecycleSettings(
                0f, 0f, 2f, float.MaxValue, true));
            for (int frame = 0;
                frame < 10 && permanentlyFrozenAnimator
                    && permanentlyFrozenAnimator.ActiveState
                        != RagdollLifecycleState.Frozen;
                frame++) yield return null;
            Assert.That(permanentlyFrozenAnimator.ActiveState,
                Is.EqualTo(RagdollLifecycleState.Frozen));
            yield return null;
            yield return null;
            yield return null;
            Assert.That(!permanentlyFrozenAnimator, Is.True,
                "Permanent Freeze irreversibly removes the runtime subsystem.");
        }

        [UnityTest]
        public IEnumerator B10_LifecycleLimitsAndCollisionRollback()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            rig.Animator.AngularLimits = false;
            rig.Animator.InternalCollisions = false;
            ConfigurableJointMotion priorX = rig.ChildJoint.angularXMotion;
            ConfigurableJointMotion priorY = rig.ChildJoint.angularYMotion;
            ConfigurableJointMotion priorZ = rig.ChildJoint.angularZMotion;
            bool priorIgnore = Physics.GetIgnoreCollision(
                rig.RootCollider, rig.ChildCollider);

            rig.Animator.Kill(new RagdollLifecycleSettings(
                0f, 0f, 2f, float.MaxValue, false, true, true));
            yield return null;
            Assert.That(rig.ChildJoint.angularXMotion,
                Is.Not.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(Physics.GetIgnoreCollision(rig.RootCollider, rig.ChildCollider),
                Is.False);

            rig.Animator.Resurrect();
            yield return null;
            Assert.That(rig.ChildJoint.angularXMotion,
                Is.EqualTo(priorX));
            Assert.That(rig.ChildJoint.angularYMotion, Is.EqualTo(priorY));
            Assert.That(rig.ChildJoint.angularZMotion, Is.EqualTo(priorZ));
            Assert.That(Physics.GetIgnoreCollision(rig.RootCollider, rig.ChildCollider),
                Is.EqualTo(priorIgnore));
        }

        [UnityTest]
        public IEnumerator Legacy_SimulationModesRespectLifecycleOwnership()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            Assert.That(rig.Simulation.SetModeImmediate(
                RagdollSimulationMode.Kinematic), Is.True);
            Assert.That(rig.RootBody.isKinematic, Is.True);
            Assert.That(rig.Animator.IsActive, Is.False);

            Assert.That(rig.Simulation.SetModeImmediate(
                RagdollSimulationMode.Active), Is.True);
            Assert.That(rig.RootBody.isKinematic, Is.False);
            rig.Animator.Kill(new RagdollLifecycleSettings(0f));
            yield return null;
            Assert.That(rig.Simulation.SetModeImmediate(
                RagdollSimulationMode.Disabled), Is.False,
                "Lifecycle owns simulation while Dead.");
            Assert.That(rig.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Active));
        }

        [UnityTest]
        public IEnumerator B19_AngularLimitsAndManualOwnershipRestoreAuthoredJointMotions()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            // CoreRig authors three distinct motions before runtime initialization.
            // The default global angularLimits=false may already have released them
            // when this coroutine resumes, so the authored contract must not be
            // recaptured from that live override.
            ConfigurableJointMotion authoredX = ConfigurableJointMotion.Limited;
            ConfigurableJointMotion authoredY = ConfigurableJointMotion.Locked;
            ConfigurableJointMotion authoredZ = ConfigurableJointMotion.Limited;

            rig.Animator.AngularLimits = false;
            Assert.That(rig.ChildJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(rig.ChildJoint.angularYMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(rig.ChildJoint.angularZMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));

            rig.Animator.AngularLimits = true;
            Assert.That(rig.ChildJoint.angularXMotion, Is.EqualTo(authoredX));
            Assert.That(rig.ChildJoint.angularYMotion, Is.EqualTo(authoredY));
            Assert.That(rig.ChildJoint.angularZMotion, Is.EqualTo(authoredZ));

            rig.Animator.ManualAngularLimitControl = true;
            rig.Animator.SetAngularLimitsManual(false);
            Assert.That(rig.ChildJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            rig.Animator.AngularLimits = true;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.ChildJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Free),
                "Automatic writes must not steal explicit manual ownership.");

            rig.Animator.SetAngularLimitsManual(true);
            Assert.That(rig.ChildJoint.angularXMotion, Is.EqualTo(authoredX));
            Assert.That(rig.ChildJoint.angularYMotion, Is.EqualTo(authoredY));
            Assert.That(rig.ChildJoint.angularZMotion, Is.EqualTo(authoredZ));
            rig.Animator.ManualAngularLimitControl = false;
        }

        [UnityTest]
        public IEnumerator B20_InternalCollisionsRestoreAcrossLifecycle()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            rig.Animator.InternalCollisions = false;
            yield return new WaitForFixedUpdate();
            Assert.That(Physics.GetIgnoreCollision(rig.RootCollider, rig.ChildCollider),
                Is.True);

            rig.Animator.Kill(new RagdollLifecycleSettings(
                0f, 0f, 2f, float.MaxValue, false, true, true));
            yield return null;
            Assert.That(rig.Animator.InternalCollisionLifecycleOverrideActive, Is.True);
            Assert.That(Physics.GetIgnoreCollision(rig.RootCollider, rig.ChildCollider),
                Is.False);
            rig.Animator.InternalCollisions = true;
            rig.Animator.Resurrect();
            yield return null;
            Assert.That(rig.Animator.InternalCollisionLifecycleOverrideActive, Is.False);
            Assert.That(Physics.GetIgnoreCollision(rig.RootCollider, rig.ChildCollider),
                Is.False, "The runtime value selected during override is restored.");
        }

        [UnityTest]
        public IEnumerator B21_BranchAuthoritySurvivesCollectionMutation()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            rig.Animator.SetMuscleWeights(1, 0.2f, 0.4f, 0.6f, 1.4f);
            MuscleRuntimeState rootBefore = rig.Muscles.GetState(rig.RootHandle);
            MuscleRuntimeState child = rig.Muscles.GetState(rig.ChildHandle);
            Assert.That(child.RotationAuthority, Is.EqualTo(0.2f));
            Assert.That(child.PositionAuthority, Is.EqualTo(0.4f));
            Assert.That(rootBefore.RotationAuthority, Is.EqualTo(1f));

            rig.Animator.SetMuscleWeightsRecursive(
                0, 0.35f, 0.55f, 0.75f, 1.25f);
            Assert.That(rig.Muscles.GetState(rig.RootHandle).RotationAuthority,
                Is.EqualTo(0.35f));
            Assert.That(rig.Muscles.GetState(rig.ChildHandle).PositionAuthority,
                Is.EqualTo(0.55f));
        }

        [UnityTest]
        public IEnumerator B23_ManualAndLegacyUpdateLifecycle()
        {
            rig = new CoreRig(true);
            yield return rig.Initialize();
            Assert.That(rig.Animator.TargetAnimation, Is.SameAs(rig.Legacy));
            Physics.simulationMode = SimulationMode.Script;
            Assert.Throws<InvalidOperationException>(() =>
                rig.Animator.CompleteManualSimulation());

            rig.Animator.enabled = false;
            rig.Animator.PrepareManualSimulation(Time.fixedDeltaTime);
            Assert.That(rig.Animator.IsManualSimulationPrepared, Is.True);
            Assert.That(rig.Legacy.enabled, Is.True);
            Physics.Simulate(Time.fixedDeltaTime);
            rig.Animator.CompleteManualSimulation();
            Assert.That(rig.Animator.IsManualSimulationPrepared, Is.False);
            Assert.That(rig.Legacy.enabled, Is.True);
            Assert.That(rig.Animator.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator B24_AllCoreHooksAreOrderedAndIsolated()
        {
            rig = new CoreRig();
            RagdollSetupResult result = rig.Configure();
            var trace = new List<string>();
            int exceptions = 0;
            Application.LogCallback capture = (_, __, type) =>
            {
                if (type == LogType.Exception) exceptions++;
            };
            result.Animator.OnPostInitialized += () =>
                throw new InvalidOperationException("B24 post-init");
            result.Animator.OnPostInitialized += () => trace.Add("initialized");
            result.Animator.OnFixTransforms += () =>
                throw new InvalidOperationException("B24 fix");
            result.Animator.OnFixTransforms += () => trace.Add("fix");
            result.Animator.OnRead += () =>
                throw new InvalidOperationException("B24 read");
            result.Animator.OnRead += () => trace.Add("read");
            result.Animator.OnWrite += () =>
                throw new InvalidOperationException("B24 write");
            result.Animator.OnWrite += () => trace.Add("write");
            result.Animator.OnPostLateUpdate += () =>
                throw new InvalidOperationException("B24 post");
            result.Animator.OnPostLateUpdate += () => trace.Add("post");

            bool previousIgnore = LogAssert.ignoreFailingMessages;
            Application.logMessageReceived += capture;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null;
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
                Application.logMessageReceived -= capture;
            }
            Assert.That(trace, Does.Contain("initialized"));
            Assert.That(trace, Does.Contain("fix"));
            Assert.That(trace, Does.Contain("read"));
            Assert.That(trace, Does.Contain("write"));
            Assert.That(trace, Does.Contain("post"));
            Assert.That(trace.IndexOf("read"), Is.LessThan(trace.IndexOf("write")));
            Assert.That(trace.IndexOf("write"), Is.LessThan(trace.IndexOf("post")));
            Assert.That(exceptions, Is.GreaterThanOrEqualTo(5));
        }

        [UnityTest]
        public IEnumerator B26_CompleteCollectionCommitsAndRollsBackAtomically()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            int before = rig.Bindings.RegistryGeneration;
            RagdollBoneHandle staleChild = rig.ChildHandle;
            RagdollRuntimeMuscleRegistration root = rig.RootRegistration();
            RagdollRuntimeMuscleRegistration replacement =
                rig.CreateReplacementChild("Child");
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.TrySetMuscles(
                new[] { root, replacement }, out var committed), Is.True,
                committed.Error);
            Assert.That(rig.Bindings.RegistryGeneration, Is.GreaterThan(before));
            Assert.That(rig.Bindings.Topology.Contains(staleChild), Is.False);

            int committedGeneration = rig.Bindings.RegistryGeneration;
            Rigidbody connected = replacement.Joint.connectedBody;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.TrySetMuscles(
                new[] { root, root }, out var rejected), Is.False);
            Assert.That(rejected.Error, Is.Not.Empty);
            Assert.That(rig.Bindings.RegistryGeneration,
                Is.EqualTo(committedGeneration));
            Assert.That(replacement.Joint.connectedBody, Is.SameAs(connected));
        }

        [UnityTest]
        public IEnumerator B27_DisconnectReconnectPreservesMappingContract()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            rig.Animator.MapDisconnectedMuscles = false;
            rig.Animator.DisconnectMuscleRecursive(rig.ChildHandle);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.GetMuscleConnectionState(rig.ChildHandle),
                Is.EqualTo(RagdollMuscleConnectionState.Disconnected));
            Assert.That(rig.Animator.MapDisconnectedMuscles, Is.False);

            rig.Animator.ReconnectMuscleRecursive(rig.ChildHandle);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.GetMuscleConnectionState(rig.ChildHandle),
                Is.EqualTo(RagdollMuscleConnectionState.Connected));
            Assert.That(rig.ChildJoint.connectedBody, Is.SameAs(rig.RootBody));
        }

        [UnityTest]
        public IEnumerator B28_RealJointBreakIsIrreversibleAndEmitsOnce()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            int broken = 0;
            RagdollBoneHandle brokenHandle = rig.ChildHandle;
            rig.Animator.JointBroken += _ => broken++;
            rig.RootBody.isKinematic = false;
            rig.ChildBody.isKinematic = false;
            rig.ChildJoint.xMotion = ConfigurableJointMotion.Locked;
            rig.ChildJoint.yMotion = ConfigurableJointMotion.Locked;
            rig.ChildJoint.zMotion = ConfigurableJointMotion.Locked;
            rig.ChildJoint.breakForce = 0.01f;
            rig.ChildJoint.breakTorque = 0.01f;
            for (int step = 0; step < 120 && broken == 0; step++)
            {
                rig.ChildBody.AddForceAtPosition(
                    Vector3.right * 1000f,
                    rig.ChildBody.worldCenterOfMass + Vector3.up,
                    ForceMode.Impulse);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(broken, Is.EqualTo(1));
            Assert.That(rig.Bindings.Topology.Contains(brokenHandle), Is.False);
            yield return new WaitForFixedUpdate();
            Assert.That(broken, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator B29_FlatTreeConversionPreservesTopologyAndPose()
        {
            rig = new CoreRig();
            yield return rig.Initialize();
            Transform treeParent = rig.ChildBody.transform.parent;
            Rigidbody connected = rig.ChildJoint.connectedBody;
            Vector3 rootPose = rig.RootBody.position;
            Vector3 childPose = rig.ChildBody.position;

            rig.Animator.FlattenHierarchy();
            Assert.That(rig.Animator.HierarchyIsFlat(), Is.True);
            Assert.That(rig.ChildBody.transform.parent,
                Is.SameAs(rig.RootBody.transform.parent));
            Assert.That(rig.RootBody.position, Is.EqualTo(rootPose));
            Assert.That(rig.ChildBody.position, Is.EqualTo(childPose));
            Assert.That(rig.ChildJoint.connectedBody, Is.SameAs(connected));

            rig.Animator.TreeHierarchy();
            Assert.That(rig.Animator.HierarchyIsFlat(), Is.False);
            Assert.That(rig.ChildBody.transform.parent, Is.SameAs(treeParent));
            Assert.That(rig.ChildJoint.connectedBody, Is.SameAs(connected));
        }

        sealed class CoreRig : IDisposable
        {
            readonly bool useLegacy;
            readonly bool ignoredBefore;
            readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            RagdollDefinition definition;
            RagdollAnimationProfile profile;
            GameObject target;
            GameObject puppet;

            internal RagdollSetupResult Result { get; private set; }
            internal RagdollAnimator Animator => Result.Animator;
            internal RagdollMuscleController Muscles => Result.Muscles;
            internal RagdollSimulationModeController Simulation => Result.Simulation;
            internal RagdollDefinitionBindings Bindings { get; private set; }
            internal Rigidbody RootBody { get; private set; }
            internal Rigidbody ChildBody { get; private set; }
            internal ConfigurableJoint RootJoint { get; private set; }
            internal ConfigurableJoint ChildJoint { get; private set; }
            internal Collider RootCollider { get; private set; }
            internal Collider ChildCollider { get; private set; }
            internal RagdollBoneHandle RootHandle => Bindings.GetHandleAt(0);
            internal RagdollBoneHandle ChildHandle => Bindings.GetHandleAt(1);
            internal UnityEngine.Animation Legacy { get; private set; }

            internal CoreRig(bool useLegacy = false)
            {
                this.useLegacy = useLegacy;
                ignoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
                Build();
            }

            internal RagdollSetupResult Configure()
            {
                if (Result != null && Result.Animator) return Result;
                Result = RagdollRuntimeSetupService.ConfigureSeparated(
                    target.transform, Bindings, profile, 28, 29);
                Assert.That(Result.Succeeded, Is.True, Result.Error);
                return Result;
            }

            internal IEnumerator Initialize()
            {
                Configure();
                yield return null;
                Assert.That(Animator.Initiated, Is.True);
            }

            void Build()
            {
                BoneName rootName = new BoneName("Root");
                BoneName childName = new BoneName("Child");
                puppet = Own(new GameObject("B Direct Puppet"));
                puppet.SetActive(false);
                GameObject child = Own(new GameObject("Child"));
                child.transform.SetParent(puppet.transform, false);
                child.transform.localPosition = Vector3.up;
                RootBody = puppet.AddComponent<Rigidbody>();
                RootBody.useGravity = false;
                RootJoint = puppet.AddComponent<ConfigurableJoint>();
                RootCollider = puppet.AddComponent<BoxCollider>();
                ChildBody = child.AddComponent<Rigidbody>();
                ChildBody.useGravity = false;
                ChildJoint = child.AddComponent<ConfigurableJoint>();
                ChildJoint.connectedBody = RootBody;
                ChildJoint.angularXMotion = ConfigurableJointMotion.Limited;
                ChildJoint.angularYMotion = ConfigurableJointMotion.Locked;
                ChildJoint.angularZMotion = ConfigurableJointMotion.Limited;
                ChildCollider = child.AddComponent<BoxCollider>();

                definition = Own(ScriptableObject.CreateInstance<RagdollDefinition>());
                SetField(definition, "_isValid", true);
                SetField(definition, "_root", rootName);
                SetField(definition, "bones", new[] { rootName, childName });
                Bindings = puppet.AddComponent<RagdollDefinitionBindings>();
                SetField(Bindings, "_definition", definition);
                SetField(Bindings, "bindings", CreateBindings(
                    rootName, RootJoint, childName, ChildJoint));
                puppet.SetActive(true);
                Assert.That(Bindings.IsInitialized, Is.True);

                target = Own(new GameObject("B Direct Puppet"));
                target.SetActive(false);
                GameObject targetChild = Own(new GameObject("Child"));
                targetChild.transform.SetParent(target.transform, false);
                targetChild.transform.localPosition = Vector3.up;
                if (useLegacy)
                {
                    Legacy = target.AddComponent<UnityEngine.Animation>();
                    Legacy.animatePhysics = true;
                }
                target.SetActive(true);
                profile = Own(ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            }

            internal RagdollRuntimeMuscleRegistration RootRegistration()
            {
                return new RagdollRuntimeMuscleRegistration(
                    Bindings.Root.Name,
                    Bindings.Root.Joint,
                    Result.Target,
                    RagdollMuscleGroup.Hips,
                    null,
                    false,
                    false);
            }

            internal RagdollRuntimeMuscleRegistration CreateReplacementChild(
                string name)
            {
                GameObject replacement = Own(new GameObject(name + " Replacement"));
                replacement.transform.SetParent(RootBody.transform, false);
                replacement.transform.localPosition = Vector3.up;
                replacement.AddComponent<Rigidbody>().useGravity = false;
                ConfigurableJoint joint = replacement.AddComponent<ConfigurableJoint>();
                joint.connectedBody = RootBody;
                replacement.AddComponent<BoxCollider>();
                Transform targetBone = Result.Target.GetChild(0);
                return new RagdollRuntimeMuscleRegistration(
                    new BoneName(name),
                    joint,
                    targetBone,
                    RagdollMuscleGroup.Spine,
                    Result.Target,
                    true,
                    false);
            }

            public void Dispose()
            {
                Physics.IgnoreLayerCollision(28, 29, ignoredBefore);
                if (Result != null && Result.Root)
                    UnityEngine.Object.DestroyImmediate(Result.Root.gameObject);
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    if (owned[index]) UnityEngine.Object.DestroyImmediate(owned[index]);
                }
                owned.Clear();
            }

            T Own<T>(T value) where T : UnityEngine.Object
            {
                owned.Add(value);
                return value;
            }

            static object CreateBindings(
                BoneName root,
                ConfigurableJoint rootJoint,
                BoneName child,
                ConfigurableJoint childJoint)
            {
                Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                    "BoneJointBindingsDictionary", BindingFlags.NonPublic);
                object dictionary = Activator.CreateInstance(type, true);
                MethodInfo add = type.GetMethod("Add", BindingFlags.Instance
                    | BindingFlags.Public, null,
                    new[] { typeof(BoneName), typeof(ConfigurableJoint) }, null);
                add.Invoke(dictionary, new object[] { root, rootJoint });
                add.Invoke(dictionary, new object[] { child, childJoint });
                return dictionary;
            }

            static void SetField(object target, string name, object value)
            {
                FieldInfo field = target.GetType().GetField(
                    name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, name);
                field.SetValue(target, value);
            }
        }
    }
}
