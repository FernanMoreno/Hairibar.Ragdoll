using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollInternalCollisionTests
    {
        GameObject firstObject;
        GameObject secondObject;
        GameObject thirdObject;
        BoxCollider firstCollider;
        BoxCollider secondCollider;
        BoxCollider thirdCollider;

        [SetUp]
        public void SetUp()
        {
            firstObject = new GameObject("Internal Collision First");
            secondObject = new GameObject("Internal Collision Second");
            thirdObject = new GameObject("Internal Collision Third");
            firstObject.AddComponent<Rigidbody>();
            secondObject.AddComponent<Rigidbody>();
            thirdObject.AddComponent<Rigidbody>();
            firstCollider = firstObject.AddComponent<BoxCollider>();
            secondCollider = secondObject.AddComponent<BoxCollider>();
            thirdCollider = thirdObject.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreCollision(firstCollider, secondCollider, false);
            Physics.IgnoreCollision(firstCollider, thirdCollider, false);
            Physics.IgnoreCollision(secondCollider, thirdCollider, false);
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
            Object.DestroyImmediate(thirdObject);
        }

        [Test]
        public void DefaultSettings_DisableInternalCollisions()
        {
            RagdollInternalCollisionSettings settings =
                RagdollInternalCollisionSettings.Default;

            Assert.That(settings.InternalCollisions, Is.False);
        }

        [Test]
        public void Settings_MigratePreSprint0031DataToDisabledDefault()
        {
            RagdollInternalCollisionSettings settings =
                JsonUtility.FromJson<RagdollInternalCollisionSettings>("{}");

            settings.Normalize();

            Assert.That(settings.InternalCollisions, Is.False);
        }

        [Test]
        public void Settings_PreserveIntentionalEnabledValue()
        {
            RagdollInternalCollisionSettings settings =
                new RagdollInternalCollisionSettings(true);

            settings.Normalize();

            Assert.That(settings.InternalCollisions, Is.True);
        }

        [Test]
        public void IgnoreRuntime_IsSymmetricAcrossExplicitAndGroupRules()
        {
            RagdollInternalCollisionIgnoreRuntime runtime;
            string error;
            bool success = RagdollInternalCollisionIgnoreRuntime.TryCreate(
                3,
                new[]
                {
                    RagdollMuscleGroup.Hips,
                    RagdollMuscleGroup.Arm,
                    RagdollMuscleGroup.Leg
                },
                new[]
                {
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        0,
                        false,
                        new[] { 1 },
                        null),
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        2,
                        false,
                        null,
                        new[] { RagdollMuscleGroup.Hips })
                },
                out runtime,
                out error);

            Assert.That(success, Is.True, error);
            Assert.That(runtime.IsForcedIgnore(0, 1), Is.True);
            Assert.That(runtime.IsForcedIgnore(1, 0), Is.True);
            Assert.That(runtime.IsForcedIgnore(0, 2), Is.True);
            Assert.That(runtime.IsForcedIgnore(2, 0), Is.True);
            Assert.That(runtime.IsForcedIgnore(1, 2), Is.False);
            Assert.That(runtime.ForcedBonePairCount, Is.EqualTo(2));
        }

        [Test]
        public void IgnoreAll_ForcesEveryOtherBoneOnlyOnce()
        {
            RagdollInternalCollisionIgnoreRuntime runtime;
            string error;
            bool success = RagdollInternalCollisionIgnoreRuntime.TryCreate(
                3,
                new[]
                {
                    RagdollMuscleGroup.Hips,
                    RagdollMuscleGroup.Arm,
                    RagdollMuscleGroup.Leg
                },
                new[]
                {
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        1,
                        true,
                        new[] { 0, 2 },
                        null)
                },
                out runtime,
                out error);

            Assert.That(success, Is.True, error);
            Assert.That(runtime.IsForcedIgnore(1, 0), Is.True);
            Assert.That(runtime.IsForcedIgnore(1, 2), Is.True);
            Assert.That(runtime.IsForcedIgnore(0, 2), Is.False);
            Assert.That(runtime.ForcedBonePairCount, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateRuleSources_AreRejected()
        {
            RagdollInternalCollisionIgnoreRuntime runtime;
            string error;
            bool success = RagdollInternalCollisionIgnoreRuntime.TryCreate(
                2,
                new[]
                {
                    RagdollMuscleGroup.Hips,
                    RagdollMuscleGroup.Spine
                },
                new[]
                {
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        0, false, null, null),
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        0, true, null, null)
                },
                out runtime,
                out error);

            Assert.That(success, Is.False);
            Assert.That(runtime, Is.Null);
            StringAssert.Contains("more than once", error);
        }

        [Test]
        public void AutomaticDisabled_IgnoresAllPairsAndReleaseRestoresBaseline()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());

            Assert.That(controller.UpdateAutomatic(false), Is.EqualTo(3));
            AssertAllIgnored(true, true, true);

            controller.Release();
            AssertAllIgnored(false, false, false);
        }

        [Test]
        public void Release_RestoresMixedCapturedBaselineExactly()
        {
            Physics.IgnoreCollision(firstCollider, secondCollider, true);
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());

            controller.UpdateAutomatic(true);
            AssertAllIgnored(false, false, false);

            controller.Release();
            AssertAllIgnored(true, false, false);
        }

        [Test]
        public void AutomaticEnabled_PreservesOnlyAuthoredForcedIgnores()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateRuntimeWithFirstSecondForced());

            controller.UpdateAutomatic(true);

            AssertAllIgnored(true, false, false);
        }

        [Test]
        public void ManualPolicy_CanUseOrBypassAuthoredIgnores()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateRuntimeWithFirstSecondForced());
            controller.SetManualControl(true);

            controller.ApplyManual(true, true);
            AssertAllIgnored(true, false, false);

            controller.ApplyManual(true, false);
            AssertAllIgnored(false, false, false);

            controller.ApplyManual(false, false);
            AssertAllIgnored(true, true, true);
        }

        [Test]
        public void LifecycleOverride_EnablesCollisionsButKeepsAuthoredIgnores()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateRuntimeWithFirstSecondForced());
            controller.UpdateAutomatic(false);
            AssertAllIgnored(true, true, true);

            controller.BeginLifecycleOverride(true);
            AssertAllIgnored(true, false, false);

            controller.EndLifecycleOverride();
            AssertAllIgnored(true, true, true);
        }

        [Test]
        public void LifecycleEnd_UsesGlobalValueChangedWhileOverrideWasActive()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());
            controller.UpdateAutomatic(false);
            controller.BeginLifecycleOverride(true);
            AssertAllIgnored(false, false, false);

            controller.UpdateAutomatic(true);
            controller.EndLifecycleOverride();

            AssertAllIgnored(false, false, false);
        }

        [Test]
        public void ManualControl_SuspendsAutomaticAndLifecycleWrites()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());
            controller.SetManualControl(true);

            controller.UpdateAutomatic(false);
            controller.BeginLifecycleOverride(true);

            Assert.That(controller.LifecycleOverrideActive, Is.False);
            AssertAllIgnored(false, false, false);
        }

        [Test]
        public void ReapplyCurrentPolicy_RestoresAutomaticStateLostByReactivation()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());
            controller.UpdateAutomatic(false);
            AssertAllIgnored(true, true, true);

            // Simulates Unity losing one per-collider ignore while a collider or its
            // attached Rigidbody is inactive and then becoming active again.
            Physics.IgnoreCollision(firstCollider, secondCollider, false);
            controller.ReapplyCurrentPolicy();

            AssertAllIgnored(true, true, true);
        }

        [Test]
        public void ReapplyCurrentPolicy_RestoresLastManualState()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());
            controller.SetManualControl(true);
            controller.ApplyManual(false, false);
            AssertAllIgnored(true, true, true);

            Physics.IgnoreCollision(firstCollider, secondCollider, false);
            controller.ReapplyCurrentPolicy();

            AssertAllIgnored(true, true, true);
        }

        [Test]
        public void ReplacingAuthoredMatrix_ReappliesCurrentAutomaticPolicy()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());
            controller.UpdateAutomatic(true);
            AssertAllIgnored(false, false, false);

            controller.SetAuthoredIgnores(
                CreateRuntimeWithFirstSecondForced());
            controller.UpdateAutomatic(true);

            AssertAllIgnored(true, false, false);
        }

        [Test]
        public void PermanentFreeze_AbandonsBaselineRollback()
        {
            RagdollInternalCollisionController controller =
                CreateController(CreateEmptyRuntime());
            controller.UpdateAutomatic(false);
            AssertAllIgnored(true, true, true);

            controller.AbandonForPermanentFreeze();
            controller.Release();

            AssertAllIgnored(true, true, true);
        }

        RagdollInternalCollisionController CreateController(
            RagdollInternalCollisionIgnoreRuntime runtime)
        {
            return new RagdollInternalCollisionController(
                new[]
                {
                    new RagdollInternalCollisionPair(
                        firstCollider, secondCollider, 0, 1),
                    new RagdollInternalCollisionPair(
                        firstCollider, thirdCollider, 0, 2),
                    new RagdollInternalCollisionPair(
                        secondCollider, thirdCollider, 1, 2)
                },
                runtime);
        }

        static RagdollInternalCollisionIgnoreRuntime CreateEmptyRuntime()
        {
            return RagdollInternalCollisionIgnoreRuntime.CreateEmpty(3);
        }

        static RagdollInternalCollisionIgnoreRuntime
            CreateRuntimeWithFirstSecondForced()
        {
            RagdollInternalCollisionIgnoreRuntime runtime;
            string error;
            bool success = RagdollInternalCollisionIgnoreRuntime.TryCreate(
                3,
                new[]
                {
                    RagdollMuscleGroup.Hips,
                    RagdollMuscleGroup.Arm,
                    RagdollMuscleGroup.Leg
                },
                new[]
                {
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        0,
                        false,
                        new[] { 1 },
                        null)
                },
                out runtime,
                out error);
            Assert.That(success, Is.True, error);
            return runtime;
        }

        void AssertAllIgnored(
            bool firstSecond,
            bool firstThird,
            bool secondThird)
        {
            Assert.That(
                Physics.GetIgnoreCollision(firstCollider, secondCollider),
                Is.EqualTo(firstSecond));
            Assert.That(
                Physics.GetIgnoreCollision(firstCollider, thirdCollider),
                Is.EqualTo(firstThird));
            Assert.That(
                Physics.GetIgnoreCollision(secondCollider, thirdCollider),
                Is.EqualTo(secondThird));
        }
    }
}
