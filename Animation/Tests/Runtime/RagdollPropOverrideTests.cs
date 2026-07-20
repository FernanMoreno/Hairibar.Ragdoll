using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropOverrideTests
    {
        [Test]
        public void PickupAndDrop_ApplyAndRestoreMassLayersAndMaterials()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                PhysicMaterial baseline = new PhysicMaterial("Baseline");
                PhysicMaterial held = new PhysicMaterial("Held");
                PhysicMaterial dropped = new PhysicMaterial("Dropped");
                try
                {
                    Collider propCollider = rig.PropA.GetComponent<Collider>();
                    Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                    float slotBaselineMass = 4.5f;
                    slotBody.mass = slotBaselineMass;
                    propCollider.sharedMaterial = baseline;
                    rig.PropA.gameObject.layer = 3;
                    rig.MeshA.gameObject.layer = 4;
                    rig.PhysicalSlot.layer = 8;
                    rig.TargetSlot.layer = 9;

                    RagdollPropTestRig.SetField(rig.PropA, "pickedUpMass", 7.25f);
                    RagdollPropTestRig.SetField(rig.PropA, "forceLayers", true);
                    RagdollPropTestRig.SetField(rig.PropA, "pickedUpMaterial", held);
                    RagdollPropTestRig.SetField(rig.PropA, "droppedMaterial", dropped);

                    string error;
                    Assert.That(
                        rig.PropA.TryPreparePickup(
                            rig.Muscle,
                            rig.PhysicalSlot.transform,
                            rig.TargetSlot.transform,
                            out error),
                        Is.True,
                        error);
                    Assert.That(rig.PropA.gameObject.layer, Is.EqualTo(8));
                    Assert.That(rig.MeshA.gameObject.layer, Is.EqualTo(9));
                    Assert.That(propCollider.sharedMaterial, Is.EqualTo(held));
                    rig.PropA.CompletePendingBodyDestructionForTesting();

                    Assert.That(
                        rig.PropA.TryCommitPickup(
                            rig.Muscle,
                            slotBody,
                            null,
                            RagdollBoneHandle.Invalid,
                            out error),
                        Is.True,
                        error);
                    Assert.That(slotBody.mass, Is.EqualTo(7.25f).Within(0.0001f));

                    bool pending;
                    Assert.That(
                        rig.PropA.TryCompleteDrop(
                            rig.Muscle,
                            rig.PropA.CaptureReleaseState(slotBody),
                            out pending,
                            out error),
                        Is.True,
                        error);
                    Assert.That(pending, Is.False);
                    Assert.That(slotBody.mass, Is.EqualTo(slotBaselineMass).Within(0.0001f));
                    Assert.That(rig.PropA.gameObject.layer, Is.EqualTo(3));
                    Assert.That(rig.MeshA.gameObject.layer, Is.EqualTo(4));
                    Assert.That(propCollider.sharedMaterial, Is.EqualTo(dropped));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(baseline);
                    RagdollPropTestRig.DestroyObject(held);
                    RagdollPropTestRig.DestroyObject(dropped);
                }
            }
        }

        [Test]
        public void NullDroppedMaterial_RestoresPerPickupBaselineExactly()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                PhysicMaterial baseline = new PhysicMaterial("Baseline");
                PhysicMaterial held = new PhysicMaterial("Held");
                try
                {
                    Collider collider = rig.PropA.GetComponent<Collider>();
                    collider.sharedMaterial = baseline;
                    RagdollPropTestRig.SetField(rig.PropA, "pickedUpMaterial", held);
                    RagdollPropTestRig.SetField(
                        rig.PropA,
                        "droppedMaterial",
                        null);

                    string error;
                    Assert.That(
                        rig.PropA.TryPreparePickup(
                            rig.Muscle,
                            rig.PhysicalSlot.transform,
                            rig.TargetSlot.transform,
                            out error),
                        Is.True,
                        error);
                    rig.PropA.CompletePendingBodyDestructionForTesting();
                    Assert.That(collider.sharedMaterial, Is.EqualTo(held));

                    bool pending;
                    Assert.That(
                        rig.PropA.TryCompleteDrop(
                            rig.Muscle,
                            new RagdollPropReleaseState(
                                Vector3.zero,
                                Quaternion.identity,
                                Vector3.zero,
                                Vector3.zero,
                                false),
                            out pending,
                            out error),
                        Is.True,
                        error);
                    Assert.That(collider.sharedMaterial, Is.EqualTo(baseline));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(baseline);
                    RagdollPropTestRig.DestroyObject(held);
                }
            }
        }

        [Test]
        public void CancelledPickup_RestoresLayersAndMaterialWithoutDroppedOverride()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                PhysicMaterial baseline = new PhysicMaterial("Baseline");
                PhysicMaterial held = new PhysicMaterial("Held");
                PhysicMaterial dropped = new PhysicMaterial("Dropped");
                try
                {
                    Collider collider = rig.PropA.GetComponent<Collider>();
                    collider.sharedMaterial = baseline;
                    rig.PropA.gameObject.layer = 2;
                    rig.MeshA.gameObject.layer = 6;
                    rig.PhysicalSlot.layer = 10;
                    rig.TargetSlot.layer = 11;
                    RagdollPropTestRig.SetField(rig.PropA, "pickedUpMaterial", held);
                    RagdollPropTestRig.SetField(rig.PropA, "droppedMaterial", dropped);

                    string error;
                    Assert.That(
                        rig.PropA.TryPreparePickup(
                            rig.Muscle,
                            rig.PhysicalSlot.transform,
                            rig.TargetSlot.transform,
                            out error),
                        Is.True,
                        error);
                    rig.PropA.CompletePendingBodyDestructionForTesting();

                    bool pending;
                    Assert.That(
                        rig.PropA.TryCancelPreparedPickup(
                            rig.Muscle,
                            out pending,
                            out error),
                        Is.True,
                        error);
                    Assert.That(rig.PropA.gameObject.layer, Is.EqualTo(2));
                    Assert.That(rig.MeshA.gameObject.layer, Is.EqualTo(6));
                    Assert.That(collider.sharedMaterial, Is.EqualTo(baseline));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(baseline);
                    RagdollPropTestRig.DestroyObject(held);
                    RagdollPropTestRig.DestroyObject(dropped);
                }
            }
        }

        [Test]
        public void ForceLayersDisabled_PreservesEveryAuthoredLayer()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.gameObject.layer = 5;
                rig.MeshA.gameObject.layer = 7;
                rig.PhysicalSlot.layer = 12;
                rig.TargetSlot.layer = 13;
                RagdollPropTestRig.SetField(rig.PropA, "forceLayers", false);

                string error;
                Assert.That(
                    rig.PropA.TryPreparePickup(
                        rig.Muscle,
                        rig.PhysicalSlot.transform,
                        rig.TargetSlot.transform,
                        out error),
                    Is.True,
                    error);
                Assert.That(rig.PropA.gameObject.layer, Is.EqualTo(5));
                Assert.That(rig.MeshA.gameObject.layer, Is.EqualTo(7));
            }
        }


        [Test]
        public void ForceLayersChangedWhileHeld_StillRestoresCapturedBaseline()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.gameObject.layer = 4;
                rig.MeshA.gameObject.layer = 6;
                rig.PhysicalSlot.layer = 10;
                rig.TargetSlot.layer = 12;
                RagdollPropTestRig.SetField(rig.PropA, "forceLayers", true);

                string error;
                Assert.That(
                    rig.PropA.TryPreparePickup(
                        rig.Muscle,
                        rig.PhysicalSlot.transform,
                        rig.TargetSlot.transform,
                        out error),
                    Is.True,
                    error);
                Assert.That(rig.PropA.gameObject.layer, Is.EqualTo(10));
                Assert.That(rig.MeshA.gameObject.layer, Is.EqualTo(12));
                rig.PropA.CompletePendingBodyDestructionForTesting();

                RagdollPropTestRig.SetField(rig.PropA, "forceLayers", false);
                bool pending;
                Assert.That(
                    rig.PropA.TryCancelPreparedPickup(
                        rig.Muscle,
                        out pending,
                        out error),
                    Is.True,
                    error);
                Assert.That(rig.PropA.gameObject.layer, Is.EqualTo(4));
                Assert.That(rig.MeshA.gameObject.layer, Is.EqualTo(6));
            }
        }

        [Test]
        public void RepeatedCommittedPickup_DoesNotAccumulateSlotMass()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 3.5f;
                RagdollPropTestRig.SetField(rig.PropA, "pickedUpMass", 8f);

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
                    rig.PropA.CompletePendingBodyDestructionForTesting();
                    Assert.That(
                        rig.PropA.TryCommitPickup(
                            rig.Muscle,
                            slotBody,
                            null,
                            RagdollBoneHandle.Invalid,
                            out error),
                        Is.True,
                        error);
                    Assert.That(slotBody.mass, Is.EqualTo(8f).Within(0.0001f));

                    bool pending;
                    Assert.That(
                        rig.PropA.TryCompleteDrop(
                            rig.Muscle,
                            rig.PropA.CaptureReleaseState(slotBody),
                            out pending,
                            out error),
                        Is.True,
                        error);
                    Assert.That(slotBody.mass, Is.EqualTo(3.5f).Within(0.0001f));
                }
            }
        }

        [Test]
        public void Switch_RestoresSlotBaselineBeforeApplyingNextPropMass()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 4f;
                RagdollPropTestRig.SetField(rig.PropA, "pickedUpMass", 7f);
                RagdollPropTestRig.SetField(rig.PropB, "pickedUpMass", 9f);
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                Assert.That(slotBody.mass, Is.EqualTo(7f).Within(0.0001f));

                rig.Muscle.SetCurrentProp(rig.PropB);
                rig.Muscle.TickForTesting();
                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass, Is.EqualTo(4f).Within(0.0001f));

                rig.Muscle.TickForTesting();
                rig.PropB.CompletePendingBodyDestructionForTesting();
                rig.Muscle.TickForTesting();
                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.CurrentProp, Is.EqualTo(rig.PropB));
                Assert.That(slotBody.mass, Is.EqualTo(9f).Within(0.0001f));

                rig.DropCurrent();
                Assert.That(slotBody.mass, Is.EqualTo(4f).Within(0.0001f));
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        public void Validation_RejectsInvalidPickedUpMass(float mass)
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                RagdollPropTestRig.SetField(rig.PropA, "pickedUpMass", mass);
                string error;
                Assert.That(
                    rig.PropA.TryValidateStandaloneConfiguration(out error),
                    Is.False);
                Assert.That(error, Does.Contain("Picked Up Mass"));
            }
        }
    }
}
