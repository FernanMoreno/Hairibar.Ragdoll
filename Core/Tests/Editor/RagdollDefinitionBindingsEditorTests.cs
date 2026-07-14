using System;
using System.Reflection;
using NUnit.Framework;

namespace Hairibar.Ragdoll.Tests
{
    public class RagdollDefinitionBindingsEditorTests
    {
        RagdollBindingsTestRig rig;

        [SetUp]
        public void SetUp()
        {
            rig = new RagdollBindingsTestRig();
        }

        [TearDown]
        public void TearDown()
        {
            rig.Dispose();
        }

        [Test]
        public void RegistryOrderTopologyAndAllRegisteredLookups_Agree()
        {
            Assert.That(rig.Bindings.BoneCount, Is.EqualTo(2));
            Assert.That(rig.Bindings.GetBoneAt(0).Name, Is.EqualTo(rig.RootName));
            Assert.That(rig.Bindings.GetBoneAt(1).Name, Is.EqualTo(rig.ChildName));
            Assert.That(rig.Bindings.IndexedBones, Is.Not.InstanceOf<RagdollBone[]>());

            RagdollBoneHandle expected = rig.Bindings.GetHandleAt(1);
            RagdollBoneHandle actual;
            Assert.That(rig.Bindings.TryGetBoneHandle(rig.ChildName, out actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(rig.Bindings.TryGetBoneHandle(rig.ChildBody, out actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(rig.Bindings.TryGetBoneHandle(rig.ChildJoint, out actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(rig.Bindings.TryGetBoneHandle(rig.ChildCollider, out actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(rig.Bindings.Topology.GetDepth(expected), Is.EqualTo(1));
        }

        [Test]
        public void ColliderLookup_IsStrictUnlessAttachedRigidbodyFallbackIsExplicit()
        {
            UnityEngine.Collider unregisteredCollider = rig.AddUnregisteredChildCollider();
            RagdollBone bone;
            RagdollBoneHandle handle;

            Assert.That(rig.Bindings.TryGetBone(unregisteredCollider, out bone), Is.False);
            Assert.That(rig.Bindings.TryGetBoneHandle(unregisteredCollider, out handle), Is.False);
            Assert.That(rig.Bindings.TryGetBoneFromAttachedRigidbody(unregisteredCollider, out bone), Is.True);
            Assert.That(bone.Name, Is.EqualTo(rig.ChildName));
            Assert.That(rig.Bindings.TryGetBoneHandleFromAttachedRigidbody(unregisteredCollider, out handle), Is.True);
            Assert.That(rig.Bindings.GetBone(handle).Name, Is.EqualTo(rig.ChildName));
        }

        [Test]
        public void Rebuild_InvalidatesHandlesAndTopologyFromPreviousGeneration()
        {
            RagdollBoneHandle staleHandle = rig.Bindings.GetHandleAt(1);
            RagdollBoneTopology staleTopology = rig.Bindings.Topology;

            rig.ReverseDefinitionOrderAndRebuild();

            RagdollBone ignored;
            Assert.That(rig.Bindings.TryGetBone(staleHandle, out ignored), Is.False);
            Assert.That(staleTopology.Contains(staleHandle), Is.True);
            Assert.That(rig.Bindings.Topology.Contains(staleHandle), Is.False);
            Assert.That(rig.Bindings.GetBoneAt(0).Name, Is.EqualTo(rig.ChildName));
            Assert.That(rig.Bindings.GetHandleAt(0), Is.Not.EqualTo(staleHandle));
        }

        [Test]
        public void HandleFromAnotherRegistry_IsRejected()
        {
            using (RagdollBindingsTestRig otherRig = new RagdollBindingsTestRig())
            {
                RagdollBone ignored;
                Assert.That(
                    rig.Bindings.TryGetBone(otherRig.Bindings.GetHandleAt(0), out ignored),
                    Is.False);
            }
        }

        [Test]
        public void IndexedAccess_RejectsOutOfRangeIndices()
        {
            Assert.That(() => rig.Bindings.GetBoneAt(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => rig.Bindings.GetBoneAt(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => rig.Bindings.GetHandleAt(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => rig.Bindings.GetHandleAt(2), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void HotPathLookups_AllocateZeroBytesAfterPrewarmWhenRuntimeSupportsMeasurement()
        {
            MethodInfo measurementMethod = typeof(GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                BindingFlags.Public | BindingFlags.Static);
            if (measurementMethod == null)
            {
                Assert.Ignore("This Unity runtime does not expose per-thread allocation measurement.");
            }

            Func<long> measure = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), measurementMethod);
            RagdollBoneHandle handle;
            RagdollBone bone;

            for (int i = 0; i < 100; i++)
            {
                rig.Bindings.TryGetBoneHandle(rig.ChildBody, out handle);
                rig.Bindings.TryGetBone(handle, out bone);
            }

            long before = measure();
            for (int i = 0; i < 10000; i++)
            {
                rig.Bindings.TryGetBoneHandle(rig.ChildBody, out handle);
                rig.Bindings.TryGetBone(handle, out bone);
            }
            long allocatedBytes = measure() - before;

            Assert.That(allocatedBytes, Is.EqualTo(0));
        }
    }
}
