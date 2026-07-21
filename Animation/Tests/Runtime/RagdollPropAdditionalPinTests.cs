using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropAdditionalPinTests
    {
        [Test]
        public void PropMuscle_UsesLateExecutionOrderForPostMatchingPin()
        {
            object[] attributes = typeof(RagdollPropMuscle).GetCustomAttributes(
                typeof(DefaultExecutionOrder),
                false);
            Assert.That(attributes.Length, Is.EqualTo(1));
            DefaultExecutionOrder order =
                (DefaultExecutionOrder) attributes[0];
            Assert.That(order.order, Is.EqualTo(2000));
        }

        [Test]
        public void Settings_NormalizeInvalidValuesDeterministically()
        {
            RagdollPropAdditionalPinSettings settings =
                new RagdollPropAdditionalPinSettings(
                    true,
                    new Vector3(float.NaN, 1f, 2f),
                    float.PositiveInfinity,
                    -1f);

            Assert.That(settings.LocalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(settings.Weight, Is.EqualTo(0f));
            Assert.That(settings.Mass, Is.EqualTo(1f));
        }

        [Test]
        public void Solver_DisabledPinSamplesButDoesNotApplyImpulse()
        {
            using (AdditionalPinRig rig = new AdditionalPinRig())
            {
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinStep step;
                string error;

                Assert.That(
                    solver.TryApply(
                        rig.Body,
                        rig.Target.transform,
                        new RagdollPropAdditionalPinSnapshot(
                            false,
                            Vector3.zero,
                            1f,
                            2f),
                        1f,
                        0.02f,
                        out step,
                        out error),
                    Is.True,
                    error);
                Assert.That(step.Applied, Is.False);
                Assert.That(step.AppliedWeight, Is.EqualTo(0f));
                Assert.That(solver.HasPreviousTargetPoint, Is.True);
            }
        }

        [Test]
        public void Solver_WeightMassAndAuthorityComposeMultiplicatively()
        {
            using (AdditionalPinRig rig = new AdditionalPinRig())
            {
                rig.Target.transform.position = Vector3.right;
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinStep step;
                string error;

                Assert.That(
                    solver.TryApply(
                        rig.Body,
                        rig.Target.transform,
                        new RagdollPropAdditionalPinSnapshot(
                            true,
                            Vector3.zero,
                            0.5f,
                            4f),
                        0.25f,
                        0.02f,
                        out step,
                        out error),
                    Is.True,
                    error);

                Assert.That(step.AppliedWeight, Is.EqualTo(0.125f).Within(0.0001f));
                Assert.That(step.PositionError, Is.EqualTo(Vector3.right));
                Assert.That(step.Impulse.x, Is.EqualTo(25f).Within(0.001f));
                Assert.That(step.Impulse.y, Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void Solver_UsesPointVelocityInsteadOfCenterVelocity()
        {
            using (AdditionalPinRig rig = new AdditionalPinRig())
            {
                rig.Body.angularVelocity = Vector3.forward;
                Vector3 offset = Vector3.right;
                rig.Target.transform.position = Vector3.zero;
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinStep step;
                string error;

                Assert.That(
                    solver.TryApply(
                        rig.Body,
                        rig.Target.transform,
                        new RagdollPropAdditionalPinSnapshot(
                            true,
                            offset,
                            1f,
                            1f),
                        1f,
                        0.02f,
                        out step,
                        out error),
                    Is.True,
                    error);

                Assert.That(step.PhysicalPointVelocity.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(step.Impulse.y, Is.EqualTo(-1f).Within(0.001f));
            }
        }

        [Test]
        public void Solver_MovingTargetAddsFeedForwardVelocity()
        {
            using (AdditionalPinRig rig = new AdditionalPinRig())
            {
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinStep first;
                RagdollPropAdditionalPinStep second;
                string error;

                Assert.That(solver.TryApply(
                    rig.Body,
                    rig.Target.transform,
                    new RagdollPropAdditionalPinSnapshot(
                        true,
                        Vector3.zero,
                        1f,
                        1f),
                    1f,
                    0.02f,
                    out first,
                    out error), Is.True, error);

                rig.Target.transform.position += Vector3.right * 0.1f;
                rig.Body.transform.position += Vector3.right * 0.1f;
                Assert.That(solver.TryApply(
                    rig.Body,
                    rig.Target.transform,
                    new RagdollPropAdditionalPinSnapshot(
                        true,
                        Vector3.zero,
                        1f,
                        1f),
                    1f,
                    0.02f,
                    out second,
                    out error), Is.True, error);

                Assert.That(second.PositionError.sqrMagnitude, Is.LessThan(0.000001f));
                Assert.That(second.TargetPointVelocity.x, Is.EqualTo(5f).Within(0.001f));
                Assert.That(second.Impulse.x, Is.EqualTo(5f).Within(0.001f));
            }
        }

        [Test]
        public void Solver_ResetPreventsTargetVelocityAccumulation()
        {
            using (AdditionalPinRig rig = new AdditionalPinRig())
            {
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinSnapshot settings =
                    new RagdollPropAdditionalPinSnapshot(
                        true,
                        Vector3.right,
                        1f,
                        1f);
                RagdollPropAdditionalPinStep step;
                string error;

                Assert.That(solver.TryApply(
                    rig.Body,
                    rig.Target.transform,
                    settings,
                    1f,
                    0.02f,
                    out step,
                    out error), Is.True, error);
                rig.Target.transform.position = Vector3.one * 100f;
                solver.Reset();
                rig.Body.transform.position = rig.Target.transform.position;

                Assert.That(solver.TryApply(
                    rig.Body,
                    rig.Target.transform,
                    settings,
                    1f,
                    0.02f,
                    out step,
                    out error), Is.True, error);
                Assert.That(step.TargetPointVelocity, Is.EqualTo(Vector3.zero));
            }
        }

        [Test]
        public void Solver_RejectsInvalidFixedDeltaAndResetsSampler()
        {
            using (AdditionalPinRig rig = new AdditionalPinRig())
            {
                RagdollPropAdditionalPinSolver solver =
                    new RagdollPropAdditionalPinSolver();
                RagdollPropAdditionalPinStep step;
                string error;
                Assert.That(solver.TryApply(
                    rig.Body,
                    rig.Target.transform,
                    new RagdollPropAdditionalPinSnapshot(
                        true,
                        Vector3.zero,
                        1f,
                        1f),
                    1f,
                    0f,
                    out step,
                    out error), Is.False);
                Assert.That(error, Does.Contain("delta time"));
                Assert.That(solver.HasPreviousTargetPoint, Is.False);
            }
        }

        [Test]
        public void HeldTransaction_FreezesAdditionalPinAndPickedUpMass()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 2f;
                rig.PropA.PickedUpMass = 6f;
                rig.PropA.AdditionalPin.Enabled = true;
                rig.PropA.AdditionalPin.LocalOffset = Vector3.right;
                rig.PropA.AdditionalPin.Weight = 0.5f;
                rig.PropA.AdditionalPin.Mass = 3f;

                string error;
                Assert.That(rig.PropA.TryPreparePickup(
                    rig.Muscle,
                    rig.PhysicalSlot.transform,
                    rig.TargetSlot.transform,
                    out error), Is.True, error);
                rig.PropA.PickedUpMass = 20f;
                rig.PropA.AdditionalPin.Weight = 0f;
                rig.PropA.CompletePendingBodyDestructionForTesting();
                Assert.That(rig.PropA.TryCommitPickup(
                    rig.Muscle,
                    slotBody,
                    null,
                    RagdollBoneHandle.Invalid,
                    out error), Is.True, error);

                Assert.That(slotBody.mass, Is.EqualTo(6f).Within(0.0001f));
                rig.TargetSlot.transform.position = Vector3.right;
                Assert.That(rig.PropA.TryApplyAdditionalPin(
                    rig.TargetSlot.transform,
                    slotBody,
                    1f,
                    0.02f,
                    out error), Is.True, error);
                Assert.That(
                    rig.PropA.LastAdditionalPinStep.AppliedWeight,
                    Is.EqualTo(0.5f).Within(0.0001f));
            }
        }

        [Test]
        public void Muscle_AppliesAdditionalPinOnlyWhileHolding()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.AdditionalPin.Enabled = true;
                rig.PropA.AdditionalPin.Weight = 1f;
                rig.PropA.AdditionalPin.Mass = 1f;
                rig.TargetSlot.transform.position = Vector3.right;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                int holdingCount = rig.PropA.AdditionalPinApplicationCount;
                Assert.That(holdingCount, Is.GreaterThan(0));

                rig.Muscle.Drop();
                rig.Muscle.TickForTesting();
                Assert.That(rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.Disconnecting));
                Assert.That(rig.PropA.AdditionalPinApplicationCount,
                    Is.EqualTo(holdingCount));
                Assert.That(rig.PropA.LastAdditionalPinStep.Applied, Is.False);
            }
        }

        [Test]
        public void ResetAdditionalPinSampling_PreventsTeleportVelocityCarryover()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.AdditionalPin.Enabled = true;
                rig.PropA.AdditionalPin.Weight = 1f;
                rig.TargetSlot.transform.position = Vector3.right;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                rig.TargetSlot.transform.position += Vector3.one * 100f;
                rig.PhysicalSlot.transform.position += Vector3.one * 100f;
                rig.Muscle.ResetAdditionalPinSampling();
                rig.Muscle.ApplyAdditionalPinForTesting();

                Assert.That(
                    rig.PropA.LastAdditionalPinStep.TargetPointVelocity,
                    Is.EqualTo(Vector3.zero));
            }
        }

        [Test]
        public void Muscle_SuspendsAdditionalPinWhenRuntimeAuthorityUnavailable()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.AdditionalPin.Enabled = true;
                rig.TargetSlot.transform.position = Vector3.right;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                int count = rig.PropA.AdditionalPinApplicationCount;
                rig.Runtime.AdditionalPinAvailable = false;

                rig.Muscle.TickForTesting();

                Assert.That(rig.PropA.AdditionalPinApplicationCount,
                    Is.EqualTo(count));
                Assert.That(rig.PropA.LastAdditionalPinStep.Applied, Is.False);
                Assert.That(rig.Muscle.AdditionalPinError, Is.Null);
            }
        }

        [Test]
        public void UnknownSemanticGroup_DoesNotMatchGroupRule()
        {
            RagdollPropInternalCollisionSettings settings =
                new RagdollPropInternalCollisionSettings(
                    false,
                    null,
                    new[] { RagdollMuscleGroup.Spine });

            Assert.That(settings.Matches(
                new BoneName("Arm"),
                RagdollMuscleGroup.Spine,
                false), Is.False);
            Assert.That(settings.Matches(
                new BoneName("Arm"),
                RagdollMuscleGroup.Spine,
                true), Is.True);
        }

        [Test]
        public void CollisionSession_UnknownGroupDoesNotCreateGroupPair()
        {
            GameObject propObject = new GameObject("Unknown Group Prop");
            GameObject muscleObject = new GameObject("Unknown Group Muscle");
            try
            {
                Rigidbody propBody = propObject.AddComponent<Rigidbody>();
                propBody.isKinematic = true;
                Collider propCollider = propObject.AddComponent<BoxCollider>();
                Rigidbody muscleBody = muscleObject.AddComponent<Rigidbody>();
                muscleBody.isKinematic = true;
                Collider muscleCollider = muscleObject.AddComponent<BoxCollider>();
                RagdollPropInternalCollisionSession session;
                string error;

                Assert.That(RagdollPropInternalCollisionSession.TryCreate(
                    new[] { propCollider },
                    new[]
                    {
                        new RagdollPropCollisionMuscle(
                            default(RagdollBoneHandle),
                            new BoneName("Arm"),
                            RagdollMuscleGroup.Spine,
                            false,
                            new[] { muscleCollider })
                    },
                    (RagdollBoneHandle?) null,
                    new RagdollPropInternalCollisionSettings(
                        false,
                        null,
                        new[] { RagdollMuscleGroup.Spine }),
                    out session,
                    out error), Is.True, error);
                Assert.That(session.PairCount, Is.Zero);
            }
            finally
            {
                RagdollPropTestRig.DestroyObject(propObject);
                RagdollPropTestRig.DestroyObject(muscleObject);
            }
        }

        [Test]
        public void ExactBoneRuleStillMatchesWhenSemanticGroupIsUnknown()
        {
            BoneName arm = new BoneName("Arm");
            RagdollPropInternalCollisionSettings settings =
                new RagdollPropInternalCollisionSettings(
                    false,
                    new[] { arm },
                    new[] { RagdollMuscleGroup.Spine });

            Assert.That(settings.Matches(
                arm,
                default(RagdollMuscleGroup),
                false), Is.True);
        }

        [Test]
        public void Drop_ReappliesCoreCollisionPolicyAfterOverlayRelease()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                int before = rig.Runtime.InternalCollisionPolicyReapplyCount;

                rig.DropCurrent();

                Assert.That(
                    rig.Runtime.InternalCollisionPolicyReapplyCount,
                    Is.GreaterThan(before));
            }
        }

        [Test]
        public void FailedCoreCollisionPolicyReapply_BlocksPickupAndRetries()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.Runtime.InternalCollisionPolicyReapplySucceeds = false;

                rig.DropCurrent();

                Assert.That(
                    rig.PropA.IsCoreCollisionPolicyReapplyPending,
                    Is.True);
                Assert.That(
                    rig.PropA.CoreCollisionPolicyReapplyError,
                    Does.Contain("Synthetic"));
                string error;
                Assert.That(
                    rig.PropA.CanBePickedUpBy(rig.Muscle, out error),
                    Is.False);
                Assert.That(error, Does.Contain("reconciling"));

                int beforeRetry =
                    rig.Runtime.InternalCollisionPolicyReapplyCount;
                rig.Runtime.InternalCollisionPolicyReapplySucceeds = true;
                rig.PropA.AdvanceCoreCollisionPolicyForTesting();

                Assert.That(
                    rig.Runtime.InternalCollisionPolicyReapplyCount,
                    Is.EqualTo(beforeRetry + 1));
                Assert.That(
                    rig.PropA.IsCoreCollisionPolicyReapplyPending,
                    Is.False);
                Assert.That(rig.PropA.CoreCollisionPolicyReapplyError, Is.Null);
                Assert.That(
                    rig.PropA.CanBePickedUpBy(rig.Muscle, out error),
                    Is.True,
                    error);
            }
        }

        sealed class AdditionalPinRig : System.IDisposable
        {
            internal readonly GameObject BodyObject;
            internal readonly GameObject Target;
            internal readonly Rigidbody Body;

            internal AdditionalPinRig()
            {
                BodyObject = new GameObject("Additional Pin Body");
                Body = BodyObject.AddComponent<Rigidbody>();
                Body.useGravity = false;
                Target = new GameObject("Additional Pin Target");
            }

            public void Dispose()
            {
                RagdollPropTestRig.DestroyObject(BodyObject);
                RagdollPropTestRig.DestroyObject(Target);
            }
        }
    }
}
