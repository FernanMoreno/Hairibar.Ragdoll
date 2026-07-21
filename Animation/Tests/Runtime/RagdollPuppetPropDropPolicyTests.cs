using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetPropDropPolicyTests
    {
        [Test]
        public void ShouldDrop_OnlyOnEntryIntoUnpinned()
        {
            Assert.That(
                RagdollPuppetBehaviour.ShouldDropProps(
                    RagdollPuppetState.Puppet,
                    RagdollPuppetState.Unpinned),
                Is.True);
            Assert.That(
                RagdollPuppetBehaviour.ShouldDropProps(
                    RagdollPuppetState.GetUp,
                    RagdollPuppetState.Unpinned),
                Is.True);
            Assert.That(
                RagdollPuppetBehaviour.ShouldDropProps(
                    RagdollPuppetState.Unpinned,
                    RagdollPuppetState.Unpinned),
                Is.False);
            Assert.That(
                RagdollPuppetBehaviour.ShouldDropProps(
                    RagdollPuppetState.Unpinned,
                    RagdollPuppetState.GetUp),
                Is.False);
        }

        [Test]
        public void EnteringUnpinned_QueuesDropOnHeldProp()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                RagdollPuppetBehaviour behaviour = CreateBehaviour(rig.Muscle);
                try
                {
                    behaviour.HandlePropStateChangeForTesting(
                        RagdollPuppetState.Puppet,
                        RagdollPuppetState.Unpinned);
                    Assert.That(
                        behaviour.LastRequestedPropDropCount,
                        Is.EqualTo(1));
                    Assert.That(rig.Muscle.RequestedProp, Is.Null);
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }

        [Test]
        public void DisabledPolicy_DoesNotDropOnUnpinned()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                RagdollPuppetBehaviour behaviour = CreateBehaviour(
                    rig.Muscle,
                    false);
                try
                {
                    behaviour.HandlePropStateChangeForTesting(
                        RagdollPuppetState.Puppet,
                        RagdollPuppetState.Unpinned);
                    Assert.That(
                        behaviour.LastRequestedPropDropCount,
                        Is.Zero);
                    Assert.That(rig.Muscle.RequestedProp, Is.EqualTo(rig.PropA));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }

        [Test]
        public void CurrentUnpinnedState_DropsWithoutTransitionEvent()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                RagdollPuppetBehaviour behaviour = CreateBehaviour(rig.Muscle);
                try
                {
                    behaviour.ApplyCurrentPropStateForTesting(
                        RagdollPuppetState.Unpinned);
                    Assert.That(
                        behaviour.LastRequestedPropDropCount,
                        Is.EqualTo(1));
                    Assert.That(rig.Muscle.RequestedProp, Is.Null);
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }

        [Test]
        public void DuplicateSlotReferences_AreDroppedOnce()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                RagdollPuppetBehaviour behaviour = CreateBehaviour(
                    new[] { rig.Muscle, rig.Muscle });
                try
                {
                    Assert.That(behaviour.DropPropsNow(), Is.EqualTo(1));
                    Assert.That(
                        behaviour.LastRequestedPropDropCount,
                        Is.EqualTo(1));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }


        [Test]
        public void RaiseStateChanged_QueuesDropBeforePublishingStateEvent()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                RagdollPuppetBehaviour behaviour = CreateBehaviour(rig.Muscle);
                try
                {
                    bool eventObserved = false;
                    behaviour.StateChanged += (previous, current, reason) =>
                    {
                        eventObserved = true;
                        Assert.That(rig.Muscle.RequestedProp, Is.Null);
                    };

                    System.Reflection.MethodInfo raise =
                        typeof(RagdollPuppetBehaviour).GetMethod(
                            "RaiseStateChanged",
                            System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic);
                    Assert.That(raise, Is.Not.Null);
                    raise.Invoke(
                        behaviour,
                        new object[]
                        {
                            RagdollPuppetState.Puppet,
                            RagdollPuppetState.Unpinned,
                            RagdollPuppetTransitionReason.Manual
                        });

                    Assert.That(eventObserved, Is.True);
                    Assert.That(
                        behaviour.LastRequestedPropDropCount,
                        Is.EqualTo(1));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }

        [Test]
        public void LoseBalance_PublicTransitionQueuesDropBeforeReturning()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                RagdollPuppetBehaviour behaviour = CreateBehaviour(rig.Muscle);
                try
                {
                    RagdollPropTestRig.SetField(
                        behaviour,
                        "stateMachine",
                        new RagdollPuppetStateMachine());
                    RagdollPropTestRig.SetField(
                        behaviour,
                        "maxRigidbodyVelocity",
                        float.PositiveInfinity);

                    Assert.That(behaviour.LoseBalance(), Is.True);
                    Assert.That(behaviour.State,
                        Is.EqualTo(RagdollPuppetState.Unpinned));
                    Assert.That(rig.Muscle.RequestedProp, Is.Null);
                    Assert.That(behaviour.LastRequestedPropDropCount,
                        Is.EqualTo(1));
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }

        [Test]
        public void ActivePropMuscleRegistry_DeduplicatesWithCallerSet()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                List<RagdollPropMuscle> found =
                    new List<RagdollPropMuscle>();
                HashSet<RagdollPropMuscle> unique =
                    new HashSet<RagdollPropMuscle>();
                unique.Add(rig.Muscle);
                Assert.That(
                    RagdollPropMuscle.GetRegistered(null, found, unique),
                    Is.Zero);

                unique.Clear();
                Assert.That(
                    RagdollPropMuscle.GetRegistered(null, found, unique),
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(found, Does.Contain(rig.Muscle));
            }
        }

        [Test]
        public void EmptySlots_DoNotCountAsDropRequests()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                RagdollPuppetBehaviour behaviour = CreateBehaviour(rig.Muscle);
                try
                {
                    Assert.That(behaviour.DropPropsNow(), Is.Zero);
                    Assert.That(
                        behaviour.LastRequestedPropDropCount,
                        Is.Zero);
                }
                finally
                {
                    RagdollPropTestRig.DestroyObject(behaviour.gameObject);
                }
            }
        }

        static RagdollPuppetBehaviour CreateBehaviour(
            RagdollPropMuscle muscle,
            bool dropProps = true)
        {
            return CreateBehaviour(new[] { muscle }, dropProps);
        }

        static RagdollPuppetBehaviour CreateBehaviour(
            RagdollPropMuscle[] muscles,
            bool dropProps = true)
        {
            GameObject owner = new GameObject("BehaviourPuppet Prop Test");
            RagdollPuppetBehaviour behaviour =
                owner.AddComponent<RagdollPuppetBehaviour>();
            behaviour.ConfigurePropDropForTesting(
                muscles,
                dropProps,
                false);
            return behaviour;
        }
    }
}
