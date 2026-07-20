using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropMuscleStateMachineTests
    {
        [Test]
        public void Initialization_RegistersPropGroupAndPrimesDeactivatedSlot()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();

                Assert.That(rig.Runtime.RegistrationCount, Is.EqualTo(1));
                Assert.That(
                    rig.Runtime.LastRegistration.Group,
                    Is.EqualTo(RagdollMuscleGroup.Prop));
                Assert.That(
                    rig.Runtime.LastRegistration.Joint,
                    Is.EqualTo(rig.PhysicalSlot.GetComponent<ConfigurableJoint>()));
                Assert.That(
                    rig.Runtime.LastRegistration.Target,
                    Is.EqualTo(rig.TargetSlot.transform));
                Assert.That(rig.Runtime.LastRegistration.ForceTreeHierarchy, Is.False);
                Assert.That(rig.Runtime.LastRegistration.ForceLayers, Is.True);
                Assert.That(
                    rig.Runtime.ConnectionState,
                    Is.EqualTo(RagdollMuscleConnectionState.Deactivated));
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));
            }
        }

        [Test]
        public void ExistingRegisteredPropSlot_IsReusedWithoutHierarchyMutation()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig(true))
            {
                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.RegistrationCount, Is.Zero);
                Assert.That(
                    rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.PrimingEmptySlot));
                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.PendingDisconnect, Is.True);
            }
        }

        [Test]
        public void ExistingRegisteredSlot_MustResolveToPropGroup()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig(true))
            {
                rig.Runtime.GroupValid = false;
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Faulted));
                Assert.That(rig.Muscle.LastError, Does.Contain("not Prop"));
            }
        }

        [Test]
        public void Configuration_RejectsTargetParentCycle()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                GameObject invalidParent = new GameObject("Invalid Target Parent");
                invalidParent.transform.SetParent(rig.TargetSlot.transform, false);
                RagdollPropTestRig.SetField(
                    rig.Muscle,
                    "targetParent",
                    invalidParent.transform);

                string error;
                Assert.That(
                    rig.Muscle.TryValidateConfiguration(out error),
                    Is.False);
                Assert.That(error, Does.Contain("cannot be the Target slot"));
            }
        }

        [Test]
        public void PickupAndDrop_RunThroughConnectionStateMachineAndEvents()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                List<string> events = new List<string>();
                rig.Muscle.PropPickedUp += prop => events.Add("pickup:" + prop.name);
                rig.Muscle.PropDropped += prop => events.Add("drop:" + prop.name);
                rig.Muscle.PropChanged += (previous, next) => events.Add(
                    "change:"
                    + (previous ? previous.name : "null")
                    + ">"
                    + (next ? next.name : "null"));

                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                Assert.That(rig.PropA.IsHeld, Is.True);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(
                    rig.PropA.transform.parent,
                    Is.EqualTo(rig.PhysicalSlot.transform));
                Assert.That(rig.MeshA.parent, Is.EqualTo(rig.TargetSlot.transform));

                rig.DropCurrent();

                Assert.That(rig.PropA.IsHeld, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "pickup:Prop A",
                        "change:null>Prop A",
                        "drop:Prop A",
                        "change:Prop A>null"
                    },
                    events);
            }
        }

        [Test]
        public void Switch_DropsOldPropBeforePreparingNewProp()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                List<string> events = new List<string>();
                rig.Muscle.PropPickedUp += prop => events.Add("pickup:" + prop.name);
                rig.Muscle.PropDropped += prop => events.Add("drop:" + prop.name);

                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.Muscle.SetCurrentProp(rig.PropB);

                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.PendingDisconnect, Is.True);
                Assert.That(rig.PropB.IsReserved, Is.False);

                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(rig.PropB.IsReserved, Is.False);
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));

                rig.Muscle.TickForTesting();
                Assert.That(rig.PropB.IsReserved, Is.True);
                rig.Muscle.TickForTesting();
                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.CurrentProp, Is.EqualTo(rig.PropB));
                Assert.That(rig.PropA.IsHeld, Is.False);
                Assert.That(rig.PropB.IsHeld, Is.True);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "pickup:Prop A",
                        "drop:Prop A",
                        "pickup:Prop B"
                    },
                    events);
            }
        }

        [Test]
        public void LatestRequestWins_DuringSwitchSelectsNewestPropAfterDrop()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                GameObject thirdObject = new GameObject("Prop C");
                thirdObject.transform.SetParent(rig.StandaloneParent.transform, false);
                RagdollProp propC = thirdObject.AddComponent<RagdollProp>();
                thirdObject.AddComponent<BoxCollider>();
                GameObject meshC = new GameObject("Prop C Mesh Root");
                meshC.transform.SetParent(thirdObject.transform, false);
                Rigidbody bodyC = thirdObject.AddComponent<Rigidbody>();
                rig.ConfigureBody(bodyC);
                RagdollPropTestRig.SetField(propC, "meshRoot", meshC.transform);
                RagdollPropTestRig.SetField(propC, "standaloneRigidbody", bodyC);

                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.Muscle.SetCurrentProp(rig.PropB);
                rig.Muscle.TickForTesting();
                rig.Muscle.SetCurrentProp(propC);

                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));

                rig.Muscle.TickForTesting();
                Assert.That(propC.IsReserved, Is.True);
                Assert.That(rig.PropB.IsReserved, Is.False);
                rig.Muscle.TickForTesting();
                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.CurrentProp, Is.EqualTo(propC));
                Assert.That(rig.PropB.IsReserved, Is.False);
            }
        }

        [Test]
        public void LatestRequestWins_CancelsUncommittedPickupExactly()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Vector3 originalPosition = rig.PropA.transform.localPosition;
                Quaternion originalRotation = rig.PropA.transform.localRotation;

                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.PreparingPickup));
                rig.CompletePendingBodyDestruction(rig.PropA);

                rig.Muscle.SetCurrentProp(rig.PropB);
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.CancellingPickup));
                rig.Muscle.TickForTesting();

                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(rig.PropA.transform.localPosition, Is.EqualTo(originalPosition));
                Assert.That(
                    Quaternion.Angle(
                        rig.PropA.transform.localRotation,
                        originalRotation),
                    Is.LessThan(0.001f));
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));

                rig.Muscle.TickForTesting();
                Assert.That(rig.PropB.IsReserved, Is.True);
                Assert.That(rig.PropA.IsReserved, Is.False);
            }
        }

        [Test]
        public void ReconnectMayRemainQueuedAcrossFramesWithoutCommittingPickupEarly()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();
                rig.CompletePendingBodyDestruction(rig.PropA);
                rig.Muscle.TickForTesting();

                Assert.That(rig.Runtime.PendingReconnect, Is.True);
                for (int index = 0; index < 5; index++)
                {
                    rig.Muscle.TickForTesting();
                    Assert.That(
                        rig.Muscle.State,
                        Is.EqualTo(RagdollPropMuscleState.Reconnecting));
                    Assert.That(rig.Muscle.CurrentProp, Is.Null);
                    Assert.That(rig.PropA.IsReserved, Is.True);
                }

                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.CurrentProp, Is.EqualTo(rig.PropA));
                Assert.That(rig.PropA.IsHeld, Is.True);
            }
        }

        [Test]
        public void RequestChangesAfterReconnect_AreDroppedWithoutFalsePickupEvent()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                int pickupCount = 0;
                int dropCount = 0;
                rig.Muscle.PropPickedUp += ignored => pickupCount++;
                rig.Muscle.PropDropped += ignored => dropCount++;

                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();
                rig.CompletePendingBodyDestruction(rig.PropA);
                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.PendingReconnect, Is.True);

                rig.Muscle.SetCurrentProp(rig.PropB);
                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Disconnecting));
                Assert.That(pickupCount, Is.Zero);
                Assert.That(dropCount, Is.Zero);

                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(pickupCount, Is.Zero);
                Assert.That(dropCount, Is.Zero);
            }
        }

        [Test]
        public void DisabledMode_AllowsDropWhenPuppetSlotIsInactive()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                rig.Runtime.IsSimulationDisabled = true;
                rig.PhysicalSlot.SetActive(false);
                rig.Muscle.Drop();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Disconnecting));

                // The core request remains queued while Disabled, but the prop must still
                // regain standalone physics instead of being trapped below an inactive root.
                rig.Muscle.TickForTesting();
                Assert.That(
                    rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.RestoringStandaloneBody));
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(rig.Runtime.PendingDisconnect, Is.True);
            }
        }

        [Test]
        public void ArbitraryInactiveSlot_DoesNotMasqueradeAsDisabledMode()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                rig.Runtime.IsSimulationDisabled = false;
                rig.PhysicalSlot.SetActive(false);
                rig.Muscle.Drop();
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Disconnecting));
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Null);
            }
        }

        [Test]
        public void FaultRecovery_RestoresPropAndReprimesEmptySlot()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.Muscle.EnterFaultForTesting("Injected failure");
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Faulted));

                rig.Muscle.ClearFaultAndRetry();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Recovering));
                rig.Muscle.TickForTesting();

                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(
                    rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.PrimingEmptySlot));

                rig.Muscle.TickForTesting();
                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));
            }
        }

        [Test]
        public void DisablingDuringPreparation_StabilizesPropStandalone()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();
                Assert.That(rig.PropA.IsReserved, Is.True);
                rig.CompletePendingBodyDestruction(rig.PropA);

                rig.Muscle.enabled = false;

                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(
                    rig.Runtime.ConnectionState,
                    Is.EqualTo(RagdollMuscleConnectionState.Deactivated));
            }
        }

        [Test]
        public void DisablingWhileHolding_EmergencyDropsAndDeactivatesSlot()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                rig.Muscle.enabled = false;

                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(rig.Runtime.PendingDisconnect, Is.True);
                Assert.That(rig.Muscle.RequestedProp, Is.Null);

                rig.Runtime.CommitPending();
                rig.Muscle.enabled = true;
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));
                Assert.That(rig.PropA.IsReserved, Is.False);
            }
        }

        [Test]
        public void DestroyingMuscleWhileHolding_EmergencyDropsAndDeactivatesSlot()
        {
            RagdollPropTestRig rig = new RagdollPropTestRig();
            try
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                GameObject muscleObject = rig.MuscleObject;

                Object.DestroyImmediate(muscleObject);

                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(rig.Runtime.PendingDisconnect, Is.True);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [Test]
        public void DestroyingDuringReconnect_DrivesPendingReconnectBackToDeactivated()
        {
            RagdollPropTestRig rig = new RagdollPropTestRig();
            try
            {
                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();
                rig.CompletePendingBodyDestruction(rig.PropA);
                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.PendingReconnect, Is.True);

                Object.DestroyImmediate(rig.MuscleObject);
                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.IsEmergencySlotCleanupPending, Is.True);

                rig.Runtime.CommitPending();
                rig.PropA.ProcessEmergencyForTesting();
                Assert.That(rig.Runtime.PendingDisconnect, Is.True);
                rig.Runtime.CommitPending();
                rig.PropA.ProcessEmergencyForTesting();

                Assert.That(
                    rig.Runtime.ConnectionState,
                    Is.EqualTo(RagdollMuscleConnectionState.Deactivated));
                Assert.That(rig.PropA.IsEmergencySlotCleanupPending, Is.False);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [Test]
        public void HandleIsResolvedEveryTick_ForRegistryGenerationChanges()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                int before = rig.Runtime.ResolveCount;
                rig.Muscle.TickForTesting();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.ResolveCount, Is.GreaterThan(before));
            }
        }
    }
}
