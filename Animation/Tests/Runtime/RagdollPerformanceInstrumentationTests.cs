using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Platform-local instrumentation. Values are deliberately not asserted against a
    /// universal budget; Unity documents that Editor and development-player allocation
    /// profiles differ. The test guarantees that the supported counters can measure a
    /// real initialized population at the required scales.
    /// </summary>
    public sealed class RagdollPerformanceInstrumentationTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly List<RagdollSetupResult> setups =
            new List<RagdollSetupResult>();
        readonly List<Transform> puppetChildren = new List<Transform>();
        readonly List<Transform> treeParents = new List<Transform>();
        RagdollDefinition definition;
        RagdollAnimationProfile profile;
        bool ignoredBefore;

        [SetUp]
        public void SetUp()
        {
            created.Clear();
            setups.Clear();
            puppetChildren.Clear();
            treeParents.Clear();
            ignoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            definition = CreateDefinition();
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(28, 29, ignoredBefore);
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index]) UnityEngine.Object.DestroyImmediate(created[index]);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
        }

        [UnityTest] public IEnumerator Profile_1_Puppet() => ProfilePopulation(1);
        [UnityTest] public IEnumerator Profile_10_Puppets() => ProfilePopulation(10);
        [UnityTest] public IEnumerator Profile_25_Puppets() => ProfilePopulation(25);
        [UnityTest] public IEnumerator Profile_50_Puppets() => ProfilePopulation(50);

        [UnityTest]
        public IEnumerator H07_NoConsumerCollisionDispatch_AllocatesZeroManagedBytes()
        {
            CreateRig(0);
            yield return null;
            RagdollCollisionHub hub = setups[0].Collisions;
            setups[0].Behaviours.enabled = false;
            yield return null;
            RagdollCollisionRelay[] relays = setups[0].Puppet
                .GetComponentsInChildren<RagdollCollisionRelay>(true);
            Assert.That(relays.Length, Is.EqualTo(2));
            for (int index = 0; index < relays.Length; index++)
            {
                Assert.That(relays[index].enabled, Is.False,
                    "Relays without consumers must not receive PhysX callbacks.");
            }

            int entered = 0;
            System.Action<RagdollCollisionEvent> listener = _ => entered++;
            hub.CollisionEntered += listener;
            for (int index = 0; index < relays.Length; index++)
                Assert.That(relays[index].enabled, Is.True);
            GameObject obstacle = new GameObject("H07 Physical Collision");
            created.Add(obstacle);
            obstacle.transform.position = setups[0].Puppet.position
                + Vector3.right * 1.2f;
            obstacle.AddComponent<BoxCollider>();
            Rigidbody rootBody = setups[0].Puppet.GetComponent<Rigidbody>();
            rootBody.linearVelocity = Vector3.right * 5f;
            for (int step = 0; step < 20 && entered == 0; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(entered, Is.GreaterThan(0),
                "An enabled relay must forward a real PhysX collision.");

            hub.CollisionEntered -= listener;
            for (int index = 0; index < relays.Length; index++)
                Assert.That(relays[index].enabled, Is.False);
            RagdollBoneHandle handle = setups[0].Puppet
                .GetComponent<RagdollDefinitionBindings>()
                .GetHandleAt(0);
            for (int index = 0; index < 128; index++)
            {
                hub.Dispatch(handle, RagdollCollisionPhase.Enter, null);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                hub.Dispatch(handle, RagdollCollisionPhase.Enter, null);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        IEnumerator ProfilePopulation(int count)
        {
            for (int index = 0; index < count; index++) CreateRig(index);
            for (int frame = 0; frame < 4; frame++) yield return null;

            yield return CaptureFrames(count, "ActiveTree");

            for (int index = 0; index < puppetChildren.Count; index++)
                puppetChildren[index].SetParent(
                    setups[index].Puppet.parent,
                    true);
            yield return null;
            yield return CaptureFrames(count, "ActiveFlat");
            for (int index = 0; index < puppetChildren.Count; index++)
                puppetChildren[index].SetParent(treeParents[index], true);

            SetMode(RagdollSimulationMode.Kinematic);
            yield return null;
            yield return CaptureFrames(count, "KinematicTree");

            SetMode(RagdollSimulationMode.Disabled);
            yield return null;
            yield return CaptureFrames(count, "DisabledTree");
        }

        void SetMode(RagdollSimulationMode mode)
        {
            for (int index = 0; index < setups.Count; index++)
            {
                Assert.That(setups[index].Simulation.SetModeImmediate(mode), Is.True);
            }
        }

        IEnumerator CaptureFrames(int count, string mode)
        {
            using (ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "CPU Main Thread Frame Time",
                8))
            using (ProfilerRecorder gcMemory = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Reserved Memory",
                8))
            using (ProfilerRecorder totalMemory = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "Total Reserved Memory",
                8))
            using (ProfilerRecorder gcAllocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                8))
            {
                for (int frame = 0; frame < 8; frame++) yield return null;
                Assert.That(mainThread.Valid, Is.True);
                Assert.That(gcMemory.Valid, Is.True);
                Assert.That(totalMemory.Valid, Is.True);
                Assert.That(gcAllocated.Valid, Is.True);
                Assert.That(mainThread.LastValue, Is.GreaterThanOrEqualTo(0));
                Assert.That(gcMemory.LastValue, Is.GreaterThanOrEqualTo(0));
                Assert.That(totalMemory.LastValue, Is.GreaterThanOrEqualTo(0));
                TestContext.Out.WriteLine(
                    "Hairibar local profile | platform={0} | puppets={1} | mode={2} | "
                    + "lastMainThreadMs={3:F4} | gcReservedBytes={4} | "
                    + "totalReservedBytes={5} | gcAllocatedInFrame={6}",
                    Application.platform,
                    count,
                    mode,
                    mainThread.LastValue / 1000000.0,
                    gcMemory.LastValue,
                    totalMemory.LastValue,
                    gcAllocated.LastValue);
            }
        }

        void CreateRig(int index)
        {
            GameObject container = new GameObject("ProfileRig_" + index);
            container.transform.position = new Vector3((index % 10) * 3f, 2f,
                (index / 10) * 3f);
            created.Add(container);

            GameObject puppet = new GameObject("Puppet");
            puppet.transform.SetParent(container.transform, false);
            puppet.SetActive(false);
            GameObject puppetChild = new GameObject("Child");
            puppetChild.transform.SetParent(puppet.transform, false);
            puppetChild.transform.localPosition = Vector3.up;
            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            ConfigurableJoint rootJoint = puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();
            puppetChild.AddComponent<Rigidbody>();
            ConfigurableJoint childJoint =
                puppetChild.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            puppetChild.AddComponent<BoxCollider>();
            RagdollDefinitionBindings bindings =
                puppet.AddComponent<RagdollDefinitionBindings>();
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            puppet.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            GameObject target = new GameObject("Puppet");
            target.transform.SetParent(container.transform, false);
            GameObject targetChild = new GameObject("Child");
            targetChild.transform.SetParent(target.transform, false);
            targetChild.transform.localPosition = Vector3.up;
            target.AddComponent<UnityEngine.Animation>().animatePhysics = true;

            RagdollSetupResult result = RagdollRuntimeSetupService.ConfigureSeparated(
                target.transform,
                bindings,
                profile,
                28,
                29);
            Assert.That(result.Succeeded, Is.True, result.Error);
            setups.Add(result);
            puppetChildren.Add(puppetChild.transform);
            treeParents.Add(puppet.transform);
        }

        static RagdollDefinition CreateDefinition()
        {
            BoneName root = new BoneName("Root");
            RagdollDefinition value =
                ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(value, "_isValid", true);
            SetField(value, "_root", root);
            SetField(value, "bones", new[] { root, new BoneName("Child") });
            return value;
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
            target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
