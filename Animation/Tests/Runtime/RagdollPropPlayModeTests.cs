using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropPlayModeTests
    {
        [UnityTest]
        public IEnumerator Pickup_WaitsForDeferredRigidbodyDestructionBeforeReconnect()
        {
            RagdollPropTestRig rig = new RagdollPropTestRig();
            try
            {
                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();

                Assert.That(
                    rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.PreparingPickup));
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);

                rig.Muscle.TickForTesting();
                Assert.That(rig.Runtime.PendingReconnect, Is.False);

                yield return null;

                rig.Muscle.TickForTesting();
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(rig.Runtime.PendingReconnect, Is.True);

                rig.Runtime.CommitPending();
                rig.Muscle.TickForTesting();
                Assert.That(
                    rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.Holding));
            }
            finally
            {
                rig.Dispose();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingMuscleDuringDeferredPickup_EventuallyRestoresStandaloneBody()
        {
            RagdollPropTestRig rig = new RagdollPropTestRig();
            try
            {
                rig.PrimeEmptySlot();
                rig.Muscle.SetCurrentProp(rig.PropA);
                rig.Muscle.TickForTesting();
                Assert.That(rig.PropA.IsReserved, Is.True);

                // Empty Puppet prop slots are physically inactive. Emergency recovery
                // must first move the prop out so its own Update can finish after Destroy.
                rig.PhysicalSlot.SetActive(false);
                Object.Destroy(rig.MuscleObject);
                yield return null;
                yield return null;

                Assert.That(rig.PropA.IsReserved, Is.False);
                Assert.That(rig.PropA.IsEmergencyRestorePending, Is.False);
                Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(
                    rig.PropA.transform.parent,
                    Is.EqualTo(rig.StandaloneParent.transform));
            }
            finally
            {
                rig.Dispose();
            }
            yield return null;
        }
    }
}
