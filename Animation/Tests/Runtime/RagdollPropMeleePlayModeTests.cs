using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropMeleePlayModeTests
    {
        [UnityTest]
        public IEnumerator ActionCollider_ProducesPhysicsContactOnlyWhenEnabled()
        {
            GameObject propObject = new GameObject("Melee Physics Prop");
            GameObject wall = new GameObject("Melee Physics Wall");
            try
            {
                RagdollProp prop = propObject.AddComponent<RagdollProp>();
                GameObject mesh = new GameObject("Mesh Root");
                mesh.transform.SetParent(propObject.transform, false);
                Rigidbody body = propObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousDynamic;
                RagdollPropTestRig.SetField(prop, "meshRoot", mesh.transform);
                RagdollPropTestRig.SetField(prop, "standaloneRigidbody", body);
                MeleeCollisionCounter counter =
                    propObject.AddComponent<MeleeCollisionCounter>();
                RagdollPropMelee melee =
                    propObject.AddComponent<RagdollPropMelee>();
                melee.Settings.Shape = RagdollPropMeleeShape.Capsule;
                melee.Settings.Radius = 0.5f;
                melee.Settings.Height = 1f;
                melee.BeginHeldSession();

                BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
                wallCollider.size = Vector3.one;
                wall.transform.position = propObject.transform.position;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(counter.EnterCount, Is.EqualTo(0));

                Assert.That(melee.BeginActionForTesting(), Is.True);
                body.WakeUp();
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(counter.EnterCount, Is.GreaterThan(0));
                Assert.That(melee.ActionCollider.enabled, Is.True);
                melee.EndAction();
                Assert.That(melee.ActionCollider.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(propObject);
                Object.Destroy(wall);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentDisable_CancelsLiveActionImmediately()
        {
            GameObject propObject = new GameObject("Melee Disable Prop");
            try
            {
                RagdollProp prop = propObject.AddComponent<RagdollProp>();
                GameObject mesh = new GameObject("Mesh Root");
                mesh.transform.SetParent(propObject.transform, false);
                Rigidbody body = propObject.AddComponent<Rigidbody>();
                RagdollPropTestRig.SetField(prop, "meshRoot", mesh.transform);
                RagdollPropTestRig.SetField(prop, "standaloneRigidbody", body);
                RagdollPropMelee melee =
                    propObject.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();
                melee.BeginActionForTesting();
                Assert.That(melee.ActionCollider.enabled, Is.True);

                melee.enabled = false;
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(melee.IsHeldSession, Is.False);
                yield return new WaitForFixedUpdate();
                Assert.That(melee.ActionCollider.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(propObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnValidate_DoesNotTearDownActiveFrozenAction()
        {
            GameObject propObject = new GameObject("Melee Validate Prop");
            try
            {
                RagdollProp prop = propObject.AddComponent<RagdollProp>();
                GameObject mesh = new GameObject("Mesh Root");
                mesh.transform.SetParent(propObject.transform, false);
                Rigidbody body = propObject.AddComponent<Rigidbody>();
                RagdollPropTestRig.SetField(prop, "meshRoot", mesh.transform);
                RagdollPropTestRig.SetField(prop, "standaloneRigidbody", body);
                RagdollPropMelee melee =
                    propObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 3f;
                melee.BeginHeldSession();
                Assert.That(melee.BeginActionForTesting(), Is.True);
                float frozenMultiplier = melee.EffectiveMassMultiplier;

                melee.Settings.ActionMassMultiplier = 9f;
                melee.SendMessage("OnValidate", SendMessageOptions.RequireReceiver);

                Assert.That(melee.IsActionActive, Is.True);
                Assert.That(melee.ActionCollider.enabled, Is.True);
                Assert.That(melee.EffectiveMassMultiplier,
                    Is.EqualTo(frozenMultiplier));
            }
            finally
            {
                Object.Destroy(propObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeldMassBoost_UpdatesWithoutAdditionalPinAuthority()
        {
            RagdollPropTestRig rig = new RagdollPropTestRig();
            try
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 4f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.Runtime.AdditionalPinAvailable = false;
                rig.Runtime.AdditionalPinContextError = "Kinematic test mode.";
                slotBody.Sleep();
                Assert.That(slotBody.IsSleeping(), Is.True);

                melee.BeginAction();
                Assert.That(slotBody.IsSleeping(), Is.False);
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass,
                    Is.EqualTo(8f).Within(0.0001f));
                melee.EndAction();
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass,
                    Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                rig.Dispose();
            }
            yield return null;
        }
    }

    internal sealed class MeleeCollisionCounter : MonoBehaviour
    {
        internal int EnterCount { get; private set; }

        void OnCollisionEnter(Collision collision)
        {
            EnterCount++;
        }
    }
}
