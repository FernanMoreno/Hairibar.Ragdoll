using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPropMeleeTests
    {
        [Test]
        public void Settings_NormalizeInvalidValues()
        {
            RagdollPropMeleeSettings settings =
                new RagdollPropMeleeSettings();
            settings.Radius = float.NaN;
            settings.Height = -2f;
            settings.BoxSize = Vector3.zero;
            settings.ActionColliderRadiusMultiplier = 0f;
            settings.ActionMassMultiplier = 0f;

            Assert.That(settings.Radius, Is.GreaterThan(0f));
            Assert.That(settings.Height,
                Is.GreaterThanOrEqualTo(settings.Radius * 2f));
            Assert.That(settings.BoxSize.x, Is.GreaterThan(0f));
            Assert.That(settings.ActionColliderRadiusMultiplier,
                Is.GreaterThan(0f));
            Assert.That(settings.ActionMassMultiplier, Is.GreaterThan(0f));
        }

        [Test]
        public void Settings_InvalidShapeFallsBackToCapsule()
        {
            RagdollPropMeleeSettings settings =
                new RagdollPropMeleeSettings();
            settings.Shape = (RagdollPropMeleeShape)12345;
            Assert.That(settings.Shape,
                Is.EqualTo(RagdollPropMeleeShape.Capsule));
        }

        [Test]
        public void BeginHeldSession_CreatesDedicatedOwnerBeforeAction()
        {
            GameObject go = CreateMinimalProp("Owned Collider");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();

                Assert.That(melee.ActionCollider, Is.Not.Null);
                Assert.That(melee.ActionCollider.transform.parent,
                    Is.EqualTo(go.transform));
                Assert.That(melee.ActionCollider.gameObject,
                    Is.Not.EqualTo(go));
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(
                    melee.ActionCollider.GetComponentInParent<Rigidbody>(),
                    Is.EqualTo(go.GetComponent<Rigidbody>()));
                Assert.That(
                    melee.ActionCollider.gameObject.GetComponent<Rigidbody>(),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CapsuleAction_BoostsRadiusPinAndMass()
        {
            GameObject go = CreateMinimalProp("MeleeProp");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.Shape = RagdollPropMeleeShape.Capsule;
                melee.Settings.Radius = 0.2f;
                melee.Settings.Height = 1f;
                melee.Settings.ActionColliderRadiusMultiplier = 2f;
                melee.Settings.ActionPinWeightMultiplier = 3f;
                melee.Settings.ActionMassMultiplier = 4f;
                melee.BeginHeldSession();

                Assert.That(melee.BeginActionForTesting(), Is.True);
                CapsuleCollider capsule =
                    melee.ActionCollider as CapsuleCollider;
                Assert.That(capsule, Is.Not.Null);
                Assert.That(capsule.radius,
                    Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(melee.EffectivePinWeightMultiplier,
                    Is.EqualTo(3f));
                Assert.That(melee.EffectiveMassMultiplier,
                    Is.EqualTo(4f));

                melee.EndAction();
                Assert.That(melee.ActionCollider.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BoxAction_ExpandsEveryAxisAndRestoresBaseline()
        {
            GameObject go = CreateMinimalProp("BoxMelee");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.Shape = RagdollPropMeleeShape.Box;
                melee.Settings.BoxSize = new Vector3(1f, 2f, 3f);
                melee.Settings.ActionColliderRadiusMultiplier = 2f;
                melee.BeginHeldSession();
                melee.BeginActionForTesting();

                BoxCollider box = melee.ActionCollider as BoxCollider;
                Assert.That(box.size,
                    Is.EqualTo(new Vector3(2f, 4f, 6f)));
                melee.EndAction();
                Assert.That(box.size,
                    Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CapturedSettings_DoNotChangeDuringHeldSession()
        {
            GameObject go = CreateMinimalProp("FrozenMelee");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 2f;
                melee.Settings.CenterOfMassOffset = Vector3.right;
                melee.BeginHeldSession();

                melee.Settings.ActionMassMultiplier = 9f;
                melee.Settings.CenterOfMassOffset = Vector3.up * 9f;
                melee.BeginActionForTesting();

                Assert.That(melee.EffectiveMassMultiplier, Is.EqualTo(2f));
                Assert.That(melee.HeldCenterOfMassOffset,
                    Is.EqualTo(Vector3.right));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CenterOfMassOffset_IsAvailableOnlyDuringHeldSession()
        {
            GameObject go = CreateMinimalProp("ComMelee");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                Vector3 offset = new Vector3(0.1f, 0.2f, 0.3f);
                melee.Settings.CenterOfMassOffset = offset;
                melee.BeginHeldSession();

                Assert.That(melee.HeldCenterOfMassOffset,
                    Is.EqualTo(offset));
                Assert.That(melee.HasHeldCenterOfMassOffset, Is.True);
                melee.EndHeldSession();
                Assert.That(melee.HeldCenterOfMassOffset,
                    Is.EqualTo(Vector3.zero));
                Assert.That(melee.HasHeldCenterOfMassOffset, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BeginAction_OutsideHeldSession_IsRejected()
        {
            GameObject go = CreateMinimalProp("IdleMelee");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                Assert.That(melee.BeginAction(), Is.False);
                Assert.That(melee.ActionCollider, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BeginAction_DisabledComponentIsRejected()
        {
            GameObject go = CreateMinimalProp("DisabledMelee");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.enabled = false;
                melee.BeginHeldSession();
                Assert.That(melee.IsHeldSession, Is.False);
                Assert.That(melee.BeginAction(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EndHeldSession_DisablesAllOwnedCollidersAndBoosts()
        {
            GameObject go = CreateMinimalProp("CleanupMelee");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionPinWeightMultiplier = 4f;
                melee.BeginHeldSession();
                melee.BeginActionForTesting();
                melee.EndHeldSession();

                Assert.That(melee.IsHeldSession, Is.False);
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(melee.EffectivePinWeightMultiplier,
                    Is.EqualTo(1f));
                Assert.That(
                    melee.ActionCollider.gameObject
                        .GetComponents<Collider>()
                        .All(value => !value.enabled),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CapsuleHeight_IsNeverBelowDiameter()
        {
            GameObject go = CreateMinimalProp("CapsuleClamp");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.Shape = RagdollPropMeleeShape.Capsule;
                melee.Settings.Radius = 0.5f;
                melee.Settings.Height = 0.1f;
                melee.BeginHeldSession();
                melee.BeginActionForTesting();

                CapsuleCollider capsule =
                    melee.ActionCollider as CapsuleCollider;
                Assert.That(capsule.height,
                    Is.GreaterThanOrEqualTo(capsule.radius * 2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HierarchyDisable_PreservesSessionButCancelsAction()
        {
            GameObject go = CreateMinimalProp("Hierarchy Disable");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();
                melee.BeginActionForTesting();

                go.SetActive(false);
                Assert.That(melee.IsHeldSession, Is.True);
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(melee.ActionCollider.enabled, Is.False);

                go.SetActive(true);
                Assert.That(melee.IsHeldSession, Is.True);
                Assert.That(melee.IsActionActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComponentDisable_RelinquishesHeldSession()
        {
            GameObject go = CreateMinimalProp("Component Disable");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();
                melee.BeginActionForTesting();
                melee.enabled = false;

                Assert.That(melee.IsHeldSession, Is.False);
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(melee.EffectiveMassMultiplier,
                    Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ShapeSwitch_DoesNotAccumulateOwnedColliders()
        {
            GameObject go = CreateMinimalProp("Shape Switch");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                for (int index = 0; index < 20; index++)
                {
                    melee.Settings.Shape = index % 2 == 0
                        ? RagdollPropMeleeShape.Box
                        : RagdollPropMeleeShape.Capsule;
                    melee.BeginHeldSession();
                    Assert.That(melee.ActionCollider, Is.Not.Null);
                    melee.EndHeldSession();
                }

                Collider[] owned = melee.ActionCollider.gameObject
                    .GetComponents<Collider>();
                Assert.That(owned.Length, Is.EqualTo(2));
                Assert.That(owned.OfType<BoxCollider>().Count(),
                    Is.EqualTo(1));
                Assert.That(owned.OfType<CapsuleCollider>().Count(),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PhysicalColliderList_IncludesOnlySelectedOwnedCollider()
        {
            GameObject go = CreateMinimalProp("Physical Selection");
            try
            {
                RagdollProp prop = go.GetComponent<RagdollProp>();
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.Shape = RagdollPropMeleeShape.Capsule;
                melee.BeginHeldSession();

                Collider[] physical = prop.GetPhysicalColliders();
                Assert.That(physical.Contains(melee.ActionCollider), Is.True);
                Assert.That(physical.Count(melee.IsOwnedCollider),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PickupSurfaceSnapshot_IncludesActionColliderLayerAndMaterial()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                PhysicMaterial material = new PhysicMaterial("Held Melee");
                try
                {
                    rig.PropA.gameObject.layer = 8;
                    rig.PhysicalSlot.layer = 9;
                    rig.PropA.PickedUpMaterial = material;
                    rig.PrimeEmptySlot();
                    rig.PickUp(rig.PropA);

                    Assert.That(melee.ActionCollider.gameObject.layer,
                        Is.EqualTo(9));
                    Assert.That(melee.ActionCollider.sharedMaterial,
                        Is.EqualTo(material));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void DroppedMaterial_AppliesToDisabledActionCollider()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                PhysicMaterial dropped = new PhysicMaterial("Dropped Melee");
                try
                {
                    rig.PropA.DroppedMaterial = dropped;
                    rig.PrimeEmptySlot();
                    rig.PickUp(rig.PropA);
                    rig.DropCurrent();

                    Assert.That(melee.ActionCollider.enabled, Is.False);
                    Assert.That(melee.IsHeldSession, Is.False);
                    Assert.That(melee.ActionCollider.sharedMaterial,
                        Is.EqualTo(dropped));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dropped);
                }
            }
        }

        [Test]
        public void ZeroComOffset_DoesNotClaimExternalCenterOfMassChanges()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = Vector3.zero;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                Vector3 external = new Vector3(0.7f, -0.2f, 0.3f);
                slotBody.centerOfMass = external;
                rig.Muscle.TickForTesting();

                Assert.That(slotBody.centerOfMass, Is.EqualTo(external));
                Assert.That(rig.PropA.IsHeldCenterOfMassOverridden,
                    Is.False);
            }
        }

        [Test]
        public void NonzeroComOffset_AppliesFromPreAttachBaselineAndRestores()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                Vector3 baseline = new Vector3(0.4f, 0.2f, -0.1f);
                Vector3 offset = new Vector3(0.1f, -0.3f, 0.25f);
                slotBody.centerOfMass = baseline;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = offset;

                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                Assert.That(slotBody.centerOfMass,
                    Is.EqualTo(baseline + offset));
                Assert.That(rig.PropA.IsHeldCenterOfMassOverridden,
                    Is.True);

                rig.DropCurrent();
                Assert.That(slotBody.centerOfMass, Is.EqualTo(baseline));
                Assert.That(rig.PropA.IsHeldCenterOfMassOverridden,
                    Is.False);
            }
        }

        [Test]
        public void ActionPinMultiplier_BoostsConfiguredWeightBeyondFullAuthority()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                rig.PropA.AdditionalPin.Enabled = true;
                rig.PropA.AdditionalPin.Weight = 0.5f;
                rig.PropA.AdditionalPin.Mass = 1f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionPinWeightMultiplier = 3f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.TargetSlot.transform.position += Vector3.right;

                Assert.That(melee.BeginAction(), Is.True);
                rig.Muscle.ResetAdditionalPinSampling();
                rig.Muscle.ApplyAdditionalPinForTesting();
                Assert.That(rig.PropA.LastAdditionalPinStep.AppliedWeight,
                    Is.EqualTo(1.5f).Within(0.0001f));

                Assert.That(melee.EndAction(), Is.True);
                rig.Muscle.ResetAdditionalPinSampling();
                rig.Muscle.ApplyAdditionalPinForTesting();
                Assert.That(rig.PropA.LastAdditionalPinStep.AppliedWeight,
                    Is.EqualTo(0.5f).Within(0.0001f));
            }
        }

        [Test]
        public void ActionMass_RefreshesWhenAdditionalPinAuthorityUnavailable()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                rig.PropA.PickedUpMass = 3f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 4f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.Runtime.AdditionalPinAvailable = false;
                rig.Runtime.AdditionalPinContextError =
                    "Synthetic unavailable authority.";

                melee.BeginAction();
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass,
                    Is.EqualTo(12f).Within(0.0001f));
                Assert.That(rig.Muscle.AdditionalPinError,
                    Is.Not.Null);

                melee.EndAction();
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass,
                    Is.EqualTo(3f).Within(0.0001f));
                Assert.That(rig.Muscle.PhysicalOverrideError, Is.Null);
            }
        }

        [Test]
        public void RepeatedActions_DoNotAccumulateMass()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 3f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                for (int index = 0; index < 100; index++)
                {
                    Assert.That(melee.BeginAction(), Is.True);
                    rig.Muscle.TickForTesting();
                    Assert.That(slotBody.mass,
                        Is.EqualTo(6f).Within(0.0001f));
                    Assert.That(melee.EndAction(), Is.True);
                    rig.Muscle.TickForTesting();
                    Assert.That(slotBody.mass,
                        Is.EqualTo(2f).Within(0.0001f));
                }
            }
        }

        [Test]
        public void PickupDrop_RestoresExactStandaloneBodyAfterMeleeOwnerCreation()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody original = rig.PropA.StandaloneRigidbody;
                Vector3 centerOfMass = original.centerOfMass;
                Vector3 inertiaTensor = original.inertiaTensor;
                Quaternion inertiaRotation = original.inertiaTensorRotation;
                float mass = original.mass;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = Vector3.one * 0.25f;

                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                rig.DropCurrent();

                Rigidbody restored = rig.PropA.StandaloneRigidbody;
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.mass,
                    Is.EqualTo(mass).Within(0.0001f));
                Assert.That(restored.centerOfMass,
                    Is.EqualTo(centerOfMass));
                Assert.That(restored.inertiaTensor,
                    Is.EqualTo(inertiaTensor));
                Assert.That(restored.inertiaTensorRotation,
                    Is.EqualTo(inertiaRotation));
                Assert.That(melee.ActionCollider.enabled, Is.False);
            }
        }

        [Test]
        public void RepeatedPickupDrop_DoesNotDriftSlotMassOrCenterOfMass()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 7f;
                Vector3 baseline = new Vector3(0.2f, 0.4f, 0.6f);
                slotBody.centerOfMass = baseline;
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = Vector3.right * 0.15f;
                melee.Settings.ActionMassMultiplier = 5f;
                rig.PrimeEmptySlot();

                for (int index = 0; index < 10; index++)
                {
                    rig.PickUp(rig.PropA);
                    melee.BeginAction();
                    rig.Muscle.TickForTesting();
                    Assert.That(slotBody.mass,
                        Is.EqualTo(10f).Within(0.0001f));
                    rig.DropCurrent();
                    Assert.That(slotBody.mass,
                        Is.EqualTo(7f).Within(0.0001f));
                    Assert.That(slotBody.centerOfMass,
                        Is.EqualTo(baseline));
                }
            }
        }

        [Test]
        public void DisablingMeleeWhileHeld_RestoresBaseMassAndComOnNextTick()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 5f;
                Vector3 baseline = new Vector3(-0.1f, 0.2f, 0.3f);
                slotBody.centerOfMass = baseline;
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = Vector3.up;
                melee.Settings.ActionMassMultiplier = 3f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                melee.BeginAction();
                rig.Muscle.TickForTesting();

                melee.enabled = false;
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass,
                    Is.EqualTo(2f).Within(0.0001f));
                Assert.That(slotBody.centerOfMass, Is.EqualTo(baseline));
                Assert.That(melee.ActionCollider.enabled, Is.False);
            }
        }

        [Test]
        public void CancelPreparedPickup_CleansSessionWithoutChangingSlotBaseline()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 9f;
                Vector3 baseline = new Vector3(0.3f, 0.1f, -0.2f);
                slotBody.centerOfMass = baseline;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = Vector3.one;

                string error;
                Assert.That(rig.PropA.TryPreparePickup(
                    rig.Muscle,
                    rig.PhysicalSlot.transform,
                    rig.TargetSlot.transform,
                    out error), Is.True, error);
                rig.PropA.CompletePendingBodyDestructionForTesting();
                bool pending;
                Assert.That(rig.PropA.TryCancelPreparedPickup(
                    rig.Muscle,
                    out pending,
                    out error), Is.True, error);

                Assert.That(pending, Is.False);
                Assert.That(melee.IsHeldSession, Is.False);
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(slotBody.mass, Is.EqualTo(9f));
                Assert.That(slotBody.centerOfMass, Is.EqualTo(baseline));
            }
        }

        [Test]
        public void FailedStandaloneRestore_RollsBackFrozenMeleeAndSlotBaseline()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                slotBody.mass = 7f;
                Vector3 baseline = new Vector3(0.25f, -0.1f, 0.4f);
                slotBody.centerOfMass = baseline;
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.CenterOfMassOffset = Vector3.right * 0.5f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                FieldInfo snapshotField = typeof(RagdollProp).GetField(
                    "surfaceSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(snapshotField, Is.Not.Null);
                object snapshot = snapshotField.GetValue(rig.PropA);
                FieldInfo materialsField = snapshot.GetType().GetField(
                    "Materials",
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);
                Assert.That(materialsField, Is.Not.Null);
                materialsField.SetValue(snapshot, null);
                snapshotField.SetValue(rig.PropA, snapshot);

                RagdollPropReleaseState release =
                    rig.PropA.CaptureReleaseState(slotBody);
                bool pending;
                string error;
                Assert.That(rig.PropA.TryCompleteDrop(
                    rig.Muscle,
                    release,
                    out pending,
                    out error), Is.False);

                Assert.That(pending, Is.False);
                Assert.That(error, Does.Contain("standalone restoration failed"));
                Assert.That(rig.PropA.IsHeld, Is.True);
                Assert.That(melee.IsHeldSession, Is.True);
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(rig.PropA.transform.parent,
                    Is.EqualTo(rig.PhysicalSlot.transform));
                Assert.That(slotBody.mass,
                    Is.EqualTo(2f).Within(0.0001f));
                Assert.That(slotBody.centerOfMass,
                    Is.EqualTo(baseline + Vector3.right * 0.5f));
            }
        }

        [Test]
        public void PublicAction_IsRejectedUntilPickupCommit()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                string error;
                Assert.That(rig.PropA.TryPreparePickup(
                    rig.Muscle,
                    rig.PhysicalSlot.transform,
                    rig.TargetSlot.transform,
                    out error), Is.True, error);

                Assert.That(melee.IsHeldSession, Is.True);
                Assert.That(melee.BeginAction(), Is.False);
                Assert.That(melee.LastActionError,
                    Does.Contain("committed"));

                rig.PropA.CompletePendingBodyDestructionForTesting();
                bool pending;
                Assert.That(rig.PropA.TryCancelPreparedPickup(
                    rig.Muscle,
                    out pending,
                    out error), Is.True, error);
            }
        }

        [Test]
        public void PublicAction_ImmediatelyArmsOwnedCollisionIgnorePair()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                GameObject otherObject = new GameObject("Other Muscle Collider");
                try
                {
                    RagdollPropMelee melee =
                        rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                    rig.PrimeEmptySlot();
                    rig.PickUp(rig.PropA);
                    Collider other = otherObject.AddComponent<BoxCollider>();
                    RagdollPropInternalCollisionSession session;
                    string error;
                    Assert.That(RagdollPropInternalCollisionSession.TryCreate(
                        new[] { melee.ActionCollider },
                        new[]
                        {
                            new RagdollPropCollisionMuscle(
                                RagdollBoneHandle.Invalid,
                                new BoneName("Other"),
                                RagdollMuscleGroup.Hips,
                                new[] { other })
                        },
                        null,
                        new RagdollPropInternalCollisionSettings(true),
                        out session,
                        out error), Is.True, error);
                    RagdollPropTestRig.SetField(
                        rig.PropA,
                        "collisionSession",
                        session);

                    Assert.That(Physics.GetIgnoreCollision(
                        melee.ActionCollider,
                        other), Is.False);
                    Assert.That(melee.BeginAction(), Is.True,
                        melee.LastActionError);
                    Assert.That(Physics.GetIgnoreCollision(
                        melee.ActionCollider,
                        other), Is.True);
                    melee.EndAction();
                    Physics.IgnoreCollision(melee.ActionCollider, other, false);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(otherObject);
                }
            }
        }

        [Test]
        public void TransientActionCollider_UsesNonIgnoredReleaseBaseline()
        {
            GameObject propObject = new GameObject("Transient Source");
            GameObject muscleObject = new GameObject("Transient Target");
            try
            {
                BoxCollider source = propObject.AddComponent<BoxCollider>();
                BoxCollider other = muscleObject.AddComponent<BoxCollider>();
                Physics.IgnoreCollision(source, other, true);
                source.enabled = false;

                RagdollPropInternalCollisionSession session;
                string error;
                Assert.That(RagdollPropInternalCollisionSession.TryCreate(
                    new Collider[] { source },
                    new[]
                    {
                        new RagdollPropCollisionMuscle(
                            RagdollBoneHandle.Invalid,
                            new BoneName("Target"),
                            RagdollMuscleGroup.Hips,
                            new Collider[] { other })
                    },
                    null,
                    new RagdollPropInternalCollisionSettings(true),
                    new Collider[] { source },
                    out session,
                    out error), Is.True, error);

                session.RequestRelease();
                Assert.That(session.IsReleased, Is.True);
            }
            finally
            {
                Collider source = propObject.GetComponent<Collider>();
                Collider other = muscleObject.GetComponent<Collider>();
                if (source && other)
                {
                    source.enabled = true;
                    Physics.IgnoreCollision(source, other, false);
                }
                UnityEngine.Object.DestroyImmediate(propObject);
                UnityEngine.Object.DestroyImmediate(muscleObject);
            }
        }

        [Test]
        public void DropRequest_CancelsActionBeforeDisconnectCompletes()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 4f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                melee.BeginAction();
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass, Is.EqualTo(8f));

                rig.Muscle.Drop();
                rig.Muscle.TickForTesting();

                Assert.That(rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.Disconnecting));
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(slotBody.mass, Is.EqualTo(2f));
                Assert.That(melee.IsHeldSession, Is.True);
                Assert.That(melee.BeginAction(), Is.False);
                Assert.That(melee.LastActionError, Does.Contain("Holding"));
            }
        }

        [Test]
        public void Fault_CancelsActionAndRestoresBaseMassImmediately()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                rig.PropA.PickedUpMass = 3f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 5f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);
                melee.BeginAction();
                rig.Muscle.TickForTesting();
                Assert.That(slotBody.mass, Is.EqualTo(15f));

                rig.Muscle.EnterFaultForTesting("Synthetic melee fault.");

                Assert.That(rig.Muscle.State,
                    Is.EqualTo(RagdollPropMuscleState.Faulted));
                Assert.That(melee.IsActionActive, Is.False);
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(slotBody.mass, Is.EqualTo(3f));
            }
        }

        [Test]
        public void BeginAction_WhenAlreadyActive_IsIdempotent()
        {
            GameObject go = CreateMinimalProp("Idempotent Action");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();
                Assert.That(melee.BeginActionForTesting(), Is.True);
                int version = melee.ActionVersion;

                melee.ActionCollider.enabled = false;
                Assert.That(melee.BeginActionForTesting(), Is.True);
                Assert.That(melee.ActionVersion, Is.EqualTo(version));
                Assert.That(melee.IsActionActive, Is.True);
                Assert.That(melee.ActionCollider.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EndAction_RestoresBaseMassImmediatelyWithoutMuscleTick()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                Rigidbody slotBody = rig.PhysicalSlot.GetComponent<Rigidbody>();
                rig.PropA.PickedUpMass = 2f;
                RagdollPropMelee melee =
                    rig.PropA.gameObject.AddComponent<RagdollPropMelee>();
                melee.Settings.ActionMassMultiplier = 6f;
                rig.PrimeEmptySlot();
                rig.PickUp(rig.PropA);

                Assert.That(melee.BeginAction(), Is.True);
                Assert.That(slotBody.mass,
                    Is.EqualTo(12f).Within(0.0001f));
                Assert.That(melee.EndAction(), Is.True);
                Assert.That(slotBody.mass,
                    Is.EqualTo(2f).Within(0.0001f));
            }
        }

        [Test]
        public void BeginHeldSession_ResynchronizesOwnedLayerBetweenPickups()
        {
            GameObject go = CreateMinimalProp("Layer Resync");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                go.layer = 8;
                melee.BeginHeldSession();
                Assert.That(melee.ActionCollider.gameObject.layer, Is.EqualTo(8));
                melee.EndHeldSession();

                go.layer = 10;
                melee.BeginHeldSession();
                Assert.That(melee.ActionCollider.gameObject.layer, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DisabledMeleeSetting_DoesNotOpenActionSession()
        {
            GameObject go = CreateMinimalProp("Disabled Setting");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.Settings.Enabled = false;
                melee.BeginHeldSession();

                Assert.That(melee.IsHeldSession, Is.False);
                Assert.That(melee.BeginAction(), Is.False);
                Assert.That(melee.ActionCollider, Is.Not.Null);
                Assert.That(melee.ActionCollider.enabled, Is.False);
                Assert.That(
                    go.GetComponent<RagdollProp>()
                        .GetPhysicalColliders()
                        .Count(melee.IsOwnedCollider),
                    Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DestroyingMeleeComponent_RemovesOwnedColliderObject()
        {
            GameObject go = CreateMinimalProp("Destroy Owner");
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();
                GameObject owned = melee.ActionCollider.gameObject;

                UnityEngine.Object.DestroyImmediate(melee);

                Assert.That(owned == null, Is.True);
                Assert.That(go.GetComponentsInChildren<Collider>(true).Length,
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResetAdditionalPinSampling_DoesNotErasePhysicalOverrideDiagnostic()
        {
            using (RagdollPropTestRig rig = new RagdollPropTestRig())
            {
                const string diagnostic = "Synthetic physical override error.";
                RagdollPropTestRig.SetField(
                    rig.Muscle,
                    "physicalOverrideError",
                    diagnostic);

                rig.Muscle.ResetAdditionalPinSampling();

                Assert.That(rig.Muscle.PhysicalOverrideError,
                    Is.EqualTo(diagnostic));
            }
        }


        [Test]
        public void ExistingGenericNamedChild_IsNeverAdoptedAsOwnedCollider()
        {
            GameObject go = CreateMinimalProp("Melee Ownership Prop");
            GameObject impostor = new GameObject("__RagdollPropMeleeActionCollider");
            impostor.transform.SetParent(go.transform, false);
            BoxCollider impostorCollider = impostor.AddComponent<BoxCollider>();
            try
            {
                RagdollPropMelee melee = go.AddComponent<RagdollPropMelee>();
                melee.BeginHeldSession();

                Assert.That(melee.ActionCollider, Is.Not.Null);
                Assert.That(melee.ActionCollider, Is.Not.SameAs(impostorCollider));
                Assert.That(melee.ActionCollider.transform, Is.Not.SameAs(impostor.transform));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static GameObject CreateMinimalProp(string name)
        {
            GameObject go = new GameObject(name);
            RagdollProp prop = go.AddComponent<RagdollProp>();
            go.AddComponent<BoxCollider>();
            GameObject mesh = new GameObject(name + " Mesh");
            mesh.transform.SetParent(go.transform, false);
            Rigidbody body = go.AddComponent<Rigidbody>();
            RagdollPropTestRig.SetField(prop, "meshRoot", mesh.transform);
            RagdollPropTestRig.SetField(prop, "standaloneRigidbody", body);
            return go;
        }
    }
}
