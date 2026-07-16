using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollLifecyclePhysicsPolicyTests
    {
        GameObject firstObject;
        GameObject secondObject;
        ConfigurableJoint firstJoint;
        ConfigurableJoint secondJoint;

        [SetUp]
        public void SetUp()
        {
            firstObject = new GameObject("Lifecycle Policy First");
            secondObject = new GameObject("Lifecycle Policy Second");
            firstObject.AddComponent<Rigidbody>();
            secondObject.AddComponent<Rigidbody>();
            firstJoint = firstObject.AddComponent<ConfigurableJoint>();
            secondJoint = secondObject.AddComponent<ConfigurableJoint>();

            firstJoint.angularXMotion = ConfigurableJointMotion.Limited;
            firstJoint.angularYMotion = ConfigurableJointMotion.Locked;
            firstJoint.angularZMotion = ConfigurableJointMotion.Free;
            secondJoint.angularXMotion = ConfigurableJointMotion.Locked;
            secondJoint.angularYMotion = ConfigurableJointMotion.Limited;
            secondJoint.angularZMotion = ConfigurableJointMotion.Limited;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
        }

        [Test]
        public void KillPolicy_AppliesAuthoredLimitsAndRestoresExactPreKillState()
        {
            RagdollLifecyclePhysicsPolicy policy = CreatePolicy();

            firstJoint.angularXMotion = ConfigurableJointMotion.Free;
            firstJoint.angularYMotion = ConfigurableJointMotion.Free;
            firstJoint.angularZMotion = ConfigurableJointMotion.Free;

            policy.BeginKill(true);

            Assert.That(firstJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(firstJoint.angularYMotion,
                Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(firstJoint.angularZMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));

            policy.RestoreAfterDeath();

            Assert.That(firstJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(firstJoint.angularYMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(firstJoint.angularZMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
        }

        [Test]
        public void DisabledKillPolicy_DoesNotChangeLimits()
        {
            RagdollLifecyclePhysicsPolicy policy = CreatePolicy();
            firstJoint.angularXMotion = ConfigurableJointMotion.Free;

            policy.BeginKill(false);

            Assert.That(firstJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));

            policy.RestoreAfterDeath();
        }

        [Test]
        public void AngularLimitPolicy_FreesAndRestoresAuthoredMotions()
        {
            RagdollLifecyclePhysicsPolicy policy = CreatePolicy();

            policy.SetAngularLimits(false);

            Assert.That(policy.AngularLimitsMatch(false), Is.True);
            Assert.That(firstJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(firstJoint.angularYMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));
            Assert.That(secondJoint.angularZMotion,
                Is.EqualTo(ConfigurableJointMotion.Free));

            policy.SetAngularLimits(true);

            Assert.That(policy.AngularLimitsMatch(true), Is.True);
            Assert.That(firstJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(firstJoint.angularYMotion,
                Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(secondJoint.angularZMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
        }

        [Test]
        public void GlobalFreeLimits_AreTemporarilyAuthoredDuringKill()
        {
            RagdollLifecyclePhysicsPolicy policy = CreatePolicy();
            policy.SetAngularLimits(false);

            policy.BeginKill(true);
            Assert.That(policy.AngularLimitsMatch(true), Is.True);

            policy.RestoreAfterDeath();
            Assert.That(policy.AngularLimitsMatch(false), Is.True);
        }

        [Test]
        public void PermanentFreeze_AbandonsRollbackWithoutMutatingFrozenState()
        {
            RagdollLifecyclePhysicsPolicy policy = CreatePolicy();
            firstJoint.angularXMotion = ConfigurableJointMotion.Free;

            policy.BeginKill(true);
            policy.AbandonForPermanentFreeze();

            Assert.That(policy.IsActive, Is.False);
            Assert.That(firstJoint.angularXMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
        }

        RagdollLifecyclePhysicsPolicy CreatePolicy()
        {
            return new RagdollLifecyclePhysicsPolicy(
                new[]
                {
                    new RagdollLifecyclePhysicsPolicy.JointRecord(firstJoint),
                    new RagdollLifecyclePhysicsPolicy.JointRecord(secondJoint)
                });
        }
    }
}
