using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropSprint35PlayModeTests
    {
        [UnityTest]
        public IEnumerator ForcedIgnore_IsRearmedAfterColliderDisableEnable()
        {
            GameObject prop = new GameObject("Held Prop");
            GameObject muscle = new GameObject("Puppet Muscle");
            try
            {
                Rigidbody propBody = prop.AddComponent<Rigidbody>();
                propBody.isKinematic = true;
                Collider propCollider = prop.AddComponent<BoxCollider>();
                Rigidbody muscleBody = muscle.AddComponent<Rigidbody>();
                muscleBody.isKinematic = true;
                Collider muscleCollider = muscle.AddComponent<CapsuleCollider>();

                RagdollPropInternalCollisionSession session;
                string error;
                Assert.That(
                    RagdollPropInternalCollisionSession.TryCreate(
                        new[] { propCollider },
                        new[]
                        {
                            new RagdollPropCollisionMuscle(
                                default(RagdollBoneHandle),
                                new BoneName("Spine"),
                                RagdollMuscleGroup.Spine,
                                new[] { muscleCollider })
                        },
                        (RagdollBoneHandle?)null,
                        new RagdollPropInternalCollisionSettings(true),
                        out session,
                        out error),
                    Is.True,
                    error);
                Assert.That(
                    Physics.GetIgnoreCollision(propCollider, muscleCollider),
                    Is.True);

                muscle.SetActive(false);
                yield return null;
                muscle.SetActive(true);
                yield return null;

                session.ReapplyForcedIgnores();
                Assert.That(
                    Physics.GetIgnoreCollision(propCollider, muscleCollider),
                    Is.True);
            }
            finally
            {
                Object.Destroy(prop);
                Object.Destroy(muscle);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BehaviourPuppetDropRequest_CompletesPropStateMachineDrop()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                GameObject owner = new GameObject("BehaviourPuppet Prop Test");
                try
                {
                    RagdollPuppetBehaviour behaviour =
                        owner.AddComponent<RagdollPuppetBehaviour>();
                    behaviour.ConfigurePropDropForTesting(
                        new[] { rig.Muscle },
                        true,
                        false);
                    behaviour.HandlePropStateChangeForTesting(
                        RagdollPuppetState.Puppet,
                        RagdollPuppetState.Unpinned);

                    rig.Muscle.TickForTesting();
                    Assert.That(
                        rig.Muscle.State,
                        Is.EqualTo(RagdollPropMuscleState.Disconnecting));
                    rig.Runtime.CommitPending();
                    rig.Muscle.TickForTesting();
                    rig.Muscle.TickForTesting();

                    Assert.That(
                        rig.Muscle.State,
                        Is.EqualTo(RagdollPropMuscleState.Empty));
                    Assert.That(rig.PropA.GetComponent<Rigidbody>(), Is.Not.Null);
                }
                finally
                {
                    Object.Destroy(owner);
                }
            }
            yield return null;
        }
    }
}
