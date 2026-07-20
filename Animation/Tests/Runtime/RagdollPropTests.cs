using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropTests
    {
        [Test]
        public void PickupDrop_RestoresHierarchyScaleAndExactRigidbodySnapshot()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                int originalRootSibling = rig.PropA.transform.GetSiblingIndex();
                int originalMeshSibling = rig.MeshA.GetSiblingIndex();
                Vector3 releasePosition = new Vector3(4f, 2f, -3f);
                Quaternion releaseRotation = Quaternion.Euler(15f, 35f, 5f);
                Vector3 releaseVelocity = new Vector3(2f, 3f, 4f);
                Vector3 releaseAngularVelocity = new Vector3(-1f, 0.5f, 2f);

                string error;
                Assert.That(
                    rig.PropA.TryPreparePickup(
                        rig.Muscle,
                        rig.PhysicalSlot.transform,
                        rig.TargetSlot.transform,
                        out error),
                    Is.True,
                    error);
                rig.CompletePendingBodyDestruction(rig.PropA);
                Assert.That(
                    rig.PropA.transform.parent,
                    Is.EqualTo(rig.PhysicalSlot.transform));
                Assert.That(
                    rig.MeshA.parent,
                    Is.EqualTo(rig.TargetSlot.transform));
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Null);

                bool pending;
                Assert.That(
                    rig.PropA.TryCompleteDrop(
                        rig.Muscle,
                        new RagdollPropReleaseState(
                            releasePosition,
                            releaseRotation,
                            releaseVelocity,
                            releaseAngularVelocity,
                            false),
                        out pending,
                        out error),
                    Is.True,
                    error);
                Assert.That(pending, Is.False);

                Assert.That(
                    rig.PropA.transform.parent,
                    Is.EqualTo(rig.StandaloneParent.transform));
                Assert.That(rig.PropA.transform.position, Is.EqualTo(releasePosition));
                Assert.That(
                    Quaternion.Angle(rig.PropA.transform.rotation, releaseRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    rig.PropA.transform.localScale,
                    Is.EqualTo(rig.RootLocalScale));
                Assert.That(
                    rig.PropA.transform.GetSiblingIndex(),
                    Is.EqualTo(originalRootSibling));
                Assert.That(rig.MeshA.parent, Is.EqualTo(rig.PropA.transform));
                Assert.That(
                    rig.MeshA.GetSiblingIndex(),
                    Is.EqualTo(originalMeshSibling));
                Assert.That(
                    rig.MeshA.localPosition,
                    Is.EqualTo(rig.MeshLocalPosition));
                Assert.That(
                    Quaternion.Angle(
                        rig.MeshA.localRotation,
                        rig.MeshLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    rig.MeshA.localScale,
                    Is.EqualTo(rig.MeshLocalScale));

                Rigidbody restored = rig.PropA.GetComponent<Rigidbody>();
                AssertExactBody(rig, restored);
                Assert.That(restored.velocity, Is.EqualTo(releaseVelocity));
                Assert.That(
                    restored.angularVelocity,
                    Is.EqualTo(releaseAngularVelocity));
            }
        }

        [Test]
        public void RepeatedPickup_DoesNotAccumulateInertiaOrLoseConfiguration()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                for (int iteration = 0; iteration < 5; iteration++)
                {
                    string error;
                    Assert.That(
                        rig.PropA.TryPreparePickup(
                            rig.Muscle,
                            rig.PhysicalSlot.transform,
                            rig.TargetSlot.transform,
                            out error),
                        Is.True,
                        error);
                    rig.CompletePendingBodyDestruction(rig.PropA);

                    bool pending;
                    Assert.That(
                        rig.PropA.TryCompleteDrop(
                            rig.Muscle,
                            new RagdollPropReleaseState(
                                new Vector3(iteration, 0f, 0f),
                                Quaternion.identity,
                                Vector3.zero,
                                Vector3.zero,
                                false),
                            out pending,
                            out error),
                        Is.True,
                        error);
                    Assert.That(pending, Is.False);
                    AssertExactBody(rig, rig.PropA.GetComponent<Rigidbody>());
                }
            }
        }

        [Test]
        public void CancelPickup_RestoresOriginalLocalPoseAndKinematics()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Vector3 originalLocalPosition = rig.PropA.transform.localPosition;
                Quaternion originalLocalRotation = rig.PropA.transform.localRotation;
                Vector3 originalVelocity =
                    rig.PropA.GetComponent<Rigidbody>().velocity;
                Vector3 originalAngularVelocity =
                    rig.PropA.GetComponent<Rigidbody>().angularVelocity;

                string error;
                Assert.That(
                    rig.PropA.TryPreparePickup(
                        rig.Muscle,
                        rig.PhysicalSlot.transform,
                        rig.TargetSlot.transform,
                        out error),
                    Is.True,
                    error);
                rig.CompletePendingBodyDestruction(rig.PropA);

                bool pending;
                Assert.That(
                    rig.PropA.TryCancelPreparedPickup(
                        rig.Muscle,
                        out pending,
                        out error),
                    Is.True,
                    error);
                Assert.That(pending, Is.False);
                Assert.That(
                    rig.PropA.transform.localPosition,
                    Is.EqualTo(originalLocalPosition));
                Assert.That(
                    Quaternion.Angle(
                        rig.PropA.transform.localRotation,
                        originalLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    rig.PropA.transform.localScale,
                    Is.EqualTo(rig.RootLocalScale));

                Rigidbody restored = rig.PropA.GetComponent<Rigidbody>();
                AssertExactBody(rig, restored);
                Assert.That(restored.velocity, Is.EqualTo(originalVelocity));
                Assert.That(
                    restored.angularVelocity,
                    Is.EqualTo(originalAngularVelocity));
            }
        }

        [Test]
        public void PickupReservation_RejectsSecondPropMuscle()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                GameObject secondOwnerObject = new GameObject("Second Prop Muscle");
                try
                {
                    RagdollPropMuscle second =
                        secondOwnerObject.AddComponent<RagdollPropMuscle>();
                    string error;
                    Assert.That(
                        rig.PropA.TryPreparePickup(
                            rig.Muscle,
                            rig.PhysicalSlot.transform,
                            rig.TargetSlot.transform,
                            out error),
                        Is.True,
                        error);
                    Assert.That(
                        rig.PropA.CanBePickedUpBy(second, out error),
                        Is.False);
                    Assert.That(
                        error,
                        Does.Contain("another RagdollPropMuscle"));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(secondOwnerObject);
                }
            }
        }

        [Test]
        public void Validation_RejectsInactiveStandaloneProp()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.gameObject.SetActive(false);
                string error;
                Assert.That(
                    rig.PropA.TryValidateStandaloneConfiguration(out error),
                    Is.False);
                Assert.That(error, Does.Contain("active in the hierarchy"));
            }
        }

        [Test]
        public void Validation_RejectsRigidbodyBelowRoot()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                GameObject child = new GameObject("Invalid Rigidbody Child");
                child.transform.SetParent(rig.PropA.transform, false);
                child.AddComponent<Rigidbody>();

                string error;
                Assert.That(
                    rig.PropA.TryValidateStandaloneConfiguration(out error),
                    Is.False);
                Assert.That(error, Does.Contain("exactly one Rigidbody"));
            }
        }

        [Test]
        public void Validation_RejectsColliderInVisualMeshRoot()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.MeshA.gameObject.AddComponent<BoxCollider>();
                string error;
                Assert.That(
                    rig.PropA.TryValidateStandaloneConfiguration(out error),
                    Is.False);
                Assert.That(error, Does.Contain("visual-only"));
            }
        }

        [Test]
        public void Validation_RejectsStandaloneJointHierarchy()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.gameObject.AddComponent<FixedJoint>();
                string error;
                Assert.That(
                    rig.PropA.TryValidateStandaloneConfiguration(out error),
                    Is.False);
                Assert.That(error, Does.Contain("Joint hierarchies"));
            }
        }

        static void AssertExactBody(
            RagdollPropTestRig rig,
            Rigidbody restored)
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.mass, Is.EqualTo(rig.Mass).Within(0.0001f));
            Assert.That(restored.drag, Is.EqualTo(rig.Drag).Within(0.0001f));
            Assert.That(
                restored.angularDrag,
                Is.EqualTo(rig.AngularDrag).Within(0.0001f));
            Assert.That(restored.useGravity, Is.False);
            Assert.That(restored.isKinematic, Is.False);
            Assert.That(
                restored.interpolation,
                Is.EqualTo(RigidbodyInterpolation.Interpolate));
            Assert.That(
                restored.collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(
                restored.constraints,
                Is.EqualTo(RigidbodyConstraints.FreezeRotationZ));
            Assert.That(restored.detectCollisions, Is.True);
            Assert.That(restored.centerOfMass, Is.EqualTo(rig.CenterOfMass));
            Assert.That(restored.inertiaTensor, Is.EqualTo(rig.InertiaTensor));
            Assert.That(
                Quaternion.Angle(
                    restored.inertiaTensorRotation,
                    rig.InertiaTensorRotation),
                Is.LessThan(0.001f));
            Assert.That(restored.maxAngularVelocity, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(
                restored.maxDepenetrationVelocity,
                Is.EqualTo(rig.MaxDepenetrationVelocity).Within(0.0001f));
            Assert.That(
                restored.sleepThreshold,
                Is.EqualTo(rig.SleepThreshold).Within(0.0001f));
            Assert.That(restored.solverIterations, Is.EqualTo(9));
            Assert.That(restored.solverVelocityIterations, Is.EqualTo(4));
        }
    }
}
