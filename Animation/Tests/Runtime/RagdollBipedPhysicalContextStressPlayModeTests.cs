using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Real-physics Feature 011 fixtures for the contexts that cannot be
    /// inferred from the flat-ground or analytical support selectors.
    /// These tests observe the existing grounding and balance seams; they do
    /// not alter Stagger transitions or treat a classification as a regression.
    /// </summary>
    public sealed class RagdollBipedPhysicalContextStressPlayModeTests
    {
        StaggerPhysicalRig rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DisposeRig();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicalSlopeMatrix_ReportsAcceptedAndRejectedGrounding()
        {
            Vector3 originalGravity = Physics.gravity;
            int accepted = 0;
            int rejected = 0;
            float[] slopeAngles = { 0f, 30f, 65f };

            try
            {
                Physics.gravity = Vector3.down * 9.81f;
                for (int index = 0; index < slopeAngles.Length; index++)
                {
                    float angle = slopeAngles[index];
                    try
                    {
                        rig = new StaggerPhysicalRig(
                            footOffsetX: 0.5f,
                            freezeBodies: false,
                            groundNormal: SlopeNormal(angle));
                        ConfigureObservationRig();
                        yield return WaitForFixedUpdates(30);

                        RagdollGroundingSnapshot snapshot =
                            rig.Puppet.CenterOfMass.Snapshot;
                        AssertFiniteSnapshot(snapshot, $"slope {angle:F1}");
                        bool expectedAccepted = angle < rig.Puppet.MaximumGroundAngle;
                        Assert.That(snapshot.IsGrounded, Is.EqualTo(expectedAccepted),
                            $"physical slope acceptance at {angle:F1} degrees");
                        if (snapshot.IsGrounded)
                        {
                            float normalDot = Vector3.Dot(
                                snapshot.GroundNormal, rig.SupportUp);
                            Assert.That(normalDot, Is.GreaterThanOrEqualTo(
                                Mathf.Cos(rig.Puppet.MaximumGroundAngle * Mathf.Deg2Rad)
                                - 0.01f));
                            accepted++;
                        }
                        else
                        {
                            rejected++;
                        }
                    }
                    finally
                    {
                        DisposeRig();
                    }
                }

                Assert.That(accepted, Is.EqualTo(2));
                Assert.That(rejected, Is.EqualTo(1));
                TestContext.WriteLine(
                    "Physical context Slope: cells=3, accepted=2, " +
                    "rejected=1, finite=true");
            }
            finally
            {
                Physics.gravity = originalGravity;
            }
        }

        [UnityTest]
        public IEnumerator PhysicalArbitraryGravityMatrix_TracksRuntimeEffectiveUp()
        {
            Vector3 originalGravity = Physics.gravity;
            Vector3[] supportUps =
            {
                Vector3.up,
                Vector3.right,
                new Vector3(1f, 1f, 0f).normalized
            };
            int grounded = 0;

            try
            {
                for (int index = 0; index < supportUps.Length; index++)
                {
                    Vector3 supportUp = supportUps[index];
                    try
                    {
                        Physics.gravity = -supportUp * 9.81f;
                        rig = new StaggerPhysicalRig(
                            footOffsetX: 0.5f,
                            freezeBodies: false,
                            gravityUp: supportUp,
                            groundNormal: supportUp);
                        ConfigureObservationRig();
                        yield return WaitForFixedUpdates(30);

                        RagdollGroundingSnapshot snapshot =
                            rig.Puppet.CenterOfMass.Snapshot;
                        AssertFiniteSnapshot(snapshot,
                            $"gravity up {supportUp}");
                        Assert.That(snapshot.EffectiveUpAvailable, Is.True);
                        Assert.That(Vector3.Dot(snapshot.EffectiveUp, supportUp),
                            Is.GreaterThan(0.999f));
                        Assert.That(snapshot.IsGrounded, Is.True,
                            $"arbitrary gravity support {supportUp}");
                        Assert.That(Vector3.Dot(snapshot.GroundNormal, supportUp),
                            Is.GreaterThan(0.99f));
                        grounded++;
                    }
                    finally
                    {
                        DisposeRig();
                    }
                }

                Assert.That(grounded, Is.EqualTo(supportUps.Length));
                TestContext.WriteLine(
                    "Physical context ArbitraryGravity: cells=3, grounded=3, " +
                    "finite=true");
            }
            finally
            {
                Physics.gravity = originalGravity;
            }
        }

        [UnityTest]
        public IEnumerator PhysicalMovingPlatform_ReportsStableIdentityAndRelativeVelocity()
        {
            Vector3 originalGravity = Physics.gravity;
            const int sampleCount = 20;
            int platformSamples = 0;
            int relativeVelocitySamples = 0;
            int firstColliderId = 0;
            int firstRigidbodyId = 0;
            bool continuityReset = false;

            try
            {
                Physics.gravity = Vector3.down * 9.81f;
                rig = new StaggerPhysicalRig(
                    footOffsetX: 0.5f,
                    freezeBodies: false,
                    movingGround: true);
                ConfigureObservationRig();
                yield return WaitForFixedUpdates(20);

                for (int index = 0; index < sampleCount; index++)
                {
                    rig.MoveGround(new Vector3(0.002f, 0f, 0f));
                    yield return new WaitForFixedUpdate();

                    RagdollGroundingSnapshot snapshot =
                        rig.Puppet.CenterOfMass.Snapshot;
                    AssertFiniteSnapshot(snapshot, $"moving platform {index}");
                    Assert.That(snapshot.IsGrounded, Is.True,
                        $"moving platform support sample {index}");
                    Assert.That(snapshot.HasSupportPlatform, Is.True,
                        $"moving platform identity sample {index}");
                    Assert.That(IsFinite(snapshot.SupportVelocity), Is.True);
                    Assert.That(IsFinite(snapshot.RelativeCenterOfMassVelocity),
                        Is.True);

                    if (firstColliderId == 0)
                    {
                        firstColliderId = snapshot.SupportColliderId;
                        firstRigidbodyId = snapshot.SupportRigidbodyId;
                    }
                    else
                    {
                        Assert.That(snapshot.SupportColliderId,
                            Is.EqualTo(firstColliderId));
                        Assert.That(snapshot.SupportRigidbodyId,
                            Is.EqualTo(firstRigidbodyId));
                        continuityReset |= snapshot.SupportContinuityReset;
                    }

                    platformSamples++;
                    if (snapshot.HasRelativeMotion)
                        relativeVelocitySamples++;
                }

                Assert.That(platformSamples, Is.EqualTo(sampleCount));
                Assert.That(firstColliderId, Is.Not.EqualTo(0));
                Assert.That(firstRigidbodyId, Is.Not.EqualTo(0));
                Assert.That(relativeVelocitySamples, Is.GreaterThan(0));
                Assert.That(continuityReset, Is.False,
                    "moving one unchanged platform must not reset continuity");
                TestContext.WriteLine(
                    "Physical context MovingPlatform: samples=20, " +
                    "stableIdentity=true, relativeVelocity=true, " +
                    "continuityReset=false, finite=true");
            }
            finally
            {
                DisposeRig();
                Physics.gravity = originalGravity;
            }
        }

        [UnityTest]
        public IEnumerator PhysicalPartialContact_DistinguishesSupportedFoot()
        {
            Vector3 originalGravity = Physics.gravity;
            GameObject partialPlatform = null;

            try
            {
                Physics.gravity = Vector3.down * 9.81f;
                rig = new StaggerPhysicalRig(
                    footOffsetX: 0.5f,
                    freezeBodies: false);
                ConfigureObservationRig();
                rig.GroundCollider.enabled = false;

                partialPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                partialPlatform.name = "Stagger Partial Contact Ground";
                partialPlatform.transform.position =
                    new Vector3(-0.25f, -1.15f, 0f);
                partialPlatform.transform.localScale =
                    new Vector3(0.75f, 0.1f, 10f);
                partialPlatform.layer = 0;
                Collider partialCollider = partialPlatform.GetComponent<Collider>();
                rig.LeftFootBody.GetComponent<GroundContactProbe>()
                    .ExpectedGround = partialCollider;
                rig.RightFootBody.GetComponent<GroundContactProbe>()
                    .ExpectedGround = partialCollider;

                yield return WaitForFixedUpdates(30);

                GroundContactProbe leftProbe = rig.LeftFootBody
                    .GetComponent<GroundContactProbe>();
                GroundContactProbe rightProbe = rig.RightFootBody
                    .GetComponent<GroundContactProbe>();
                RagdollGroundingSnapshot snapshot =
                    rig.Puppet.CenterOfMass.Snapshot;
                AssertFiniteSnapshot(snapshot, "partial contact");
                Assert.That(leftProbe.IsGrounded, Is.True);
                Assert.That(rightProbe.IsGrounded, Is.False);
                Assert.That(snapshot.IsGrounded, Is.True,
                    "the COM ray must observe the partial support platform");
                Assert.That(snapshot.SupportColliderId,
                    Is.EqualTo(RagdollUnityObjectId.Get(partialCollider)));
                TestContext.WriteLine(
                    $"Physical context PartialContact: leftContact=true, " +
                    $"rightContact=false, grounded={snapshot.IsGrounded}, " +
                    "finite=true");
            }
            finally
            {
                if (partialPlatform)
                    UnityEngine.Object.DestroyImmediate(partialPlatform);
                DisposeRig();
                Physics.gravity = originalGravity;
            }
        }

        [UnityTest]
        public IEnumerator PhysicalConsecutivePushes_ReusesFixtureAndRemainsFinite()
        {
            Vector3 originalGravity = Physics.gravity;
            const int pushCount = 3;
            const int samplesPerPush = 8;
            int observedSamples = 0;
            int groundId = 0;
            int rootId = 0;

            try
            {
                Physics.gravity = Vector3.down * 9.81f;
                rig = new StaggerPhysicalRig(
                    footOffsetX: 0.5f,
                    freezeBodies: false);
                ConfigureObservationRig();
                yield return WaitForFixedUpdates(20);

                rootId = RagdollUnityObjectId.Get(rig.RootBody);
                groundId = RagdollUnityObjectId.Get(rig.GroundCollider);
                Assert.That(rootId, Is.Not.EqualTo(0));
                Assert.That(groundId, Is.Not.EqualTo(0));

                for (int push = 0; push < pushCount; push++)
                {
                    RagdollGroundingSnapshot before =
                        rig.Puppet.CenterOfMass.Snapshot;
                    AssertFiniteSnapshot(before, $"push {push} before");
                    Vector3 direction = push % 2 == 0
                        ? Vector3.right
                        : Vector3.left;
                    rig.RootBody.AddForce(
                        direction * Mathf.Max(0.05f, before.TotalMass * 0.12f),
                        ForceMode.Impulse);

                    for (int sample = 0; sample < samplesPerPush; sample++)
                    {
                        yield return new WaitForFixedUpdate();
                        RagdollGroundingSnapshot snapshot =
                            rig.Puppet.CenterOfMass.Snapshot;
                        AssertFiniteSnapshot(
                            snapshot, $"push {push} sample {sample}");
                        Assert.That(RagdollUnityObjectId.Get(rig.RootBody),
                            Is.EqualTo(rootId));
                        Assert.That(RagdollUnityObjectId.Get(rig.GroundCollider),
                            Is.EqualTo(groundId));
                        float margin = RagdollBipedBalanceMath.SignedCaptureMargin(
                            snapshot.CenterOfMass,
                            snapshot.CenterOfMassVelocity,
                            rig.LeftFootBody.worldCenterOfMass,
                            rig.RightFootBody.worldCenterOfMass,
                            rig.Stagger.PendulumLength,
                            Physics.gravity.magnitude,
                            rig.Stagger.SupportRadius,
                            snapshot.EffectiveUp);
                        Assert.That(IsFinite(margin), Is.True);
                        observedSamples++;
                    }
                }

                Assert.That(observedSamples,
                    Is.EqualTo(pushCount * samplesPerPush));
                TestContext.WriteLine(
                    "Physical context ConsecutivePushes: pushes=3, samples=24, " +
                    "fixtureReused=true, finite=true");
            }
            finally
            {
                DisposeRig();
                Physics.gravity = originalGravity;
            }
        }

        void ConfigureObservationRig()
        {
            rig.RootBody.constraints = RigidbodyConstraints.FreezeRotation;
            rig.Puppet.CanStagger = false;
            rig.Puppet.LoseBalanceOnTargetDrift = false;
            rig.Puppet.GroundProbeDistance = 3f;
            rig.Puppet.MaximumGroundAngle = 60f;
        }

        void DisposeRig()
        {
            if (rig == null) return;
            rig.Dispose();
            rig = null;
        }

        static Vector3 SlopeNormal(float angle)
        {
            return Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up;
        }

        static IEnumerator WaitForFixedUpdates(int count)
        {
            for (int index = 0; index < count; index++)
                yield return new WaitForFixedUpdate();
        }

        static void AssertFiniteSnapshot(
            RagdollGroundingSnapshot snapshot,
            string label)
        {
            Assert.That(IsFinite(snapshot.StableTime), Is.True,
                $"{label} stable time");
            Assert.That(IsFinite(snapshot.GroundPoint), Is.True,
                $"{label} ground point");
            Assert.That(IsFinite(snapshot.GroundNormal), Is.True,
                $"{label} ground normal");
            Assert.That(IsFinite(snapshot.EffectiveUp), Is.True,
                $"{label} effective up");
            Assert.That(IsFinite(snapshot.CenterOfMass), Is.True,
                $"{label} COM");
            Assert.That(IsFinite(snapshot.CenterOfMassVelocity), Is.True,
                $"{label} COM velocity");
            Assert.That(IsFinite(snapshot.RelativeCenterOfMassVelocity), Is.True,
                $"{label} relative COM velocity");
            Assert.That(IsFinite(snapshot.SupportVelocity), Is.True,
                $"{label} support velocity");
            Assert.That(IsFinite(snapshot.TotalMass), Is.True,
                $"{label} total mass");
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
