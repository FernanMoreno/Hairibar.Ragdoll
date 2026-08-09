using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Executable contracts for RootMotion's documented PropMuscle/PuppetMasterProp
    /// model. Every test uses a registered runtime Prop muscle owned by a live
    /// RagdollAnimator; helper-only prop runtimes are intentionally not used here.
    /// </summary>
    public sealed class RagdollPropCapabilityPlayModeTests
    {
        readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();
        bool targetPuppetIgnored;

        [SetUp]
        public void SetUp()
        {
            targetPuppetIgnored = Physics.GetIgnoreLayerCollision(30, 31);
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(30, 31, targetPuppetIgnored);
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index])
                    UnityEngine.Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator F01_PropMuscleRegistersOnePhysicalPropSlot()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);

            Assert.That(rig.Slot.IsInitialized, Is.True);
            Assert.That(rig.Slot.Handle.IsValid, Is.True);
            Assert.That(rig.Animator.Bindings.TryGetBone(
                rig.Slot.Joint, out RagdollBone registered), Is.True);
            Assert.That(registered.Name, Is.EqualTo(new BoneName("RightHandProp")));
            Assert.That(rig.Animator.Bindings.TryGetBoneHandle(
                new BoneName("RightHandProp"), out RagdollBoneHandle byName),
                Is.True);
            Assert.That(byName, Is.EqualTo(rig.Slot.Handle));
            Assert.That(rig.Slot.Joint.connectedBody, Is.SameAs(rig.ChildBody),
                "The prop slot must attach to its compatible hand muscle.");
            Assert.That(rig.Animator.Bindings.Bones.Count(bone =>
                bone.Name == new BoneName("RightHandProp")), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator F02_PickupDropSwitchAreAtomicAndInvalidSwitchRollsBack()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp first = CreateProp("First", false);
            RagdollProp second = CreateProp("Second", false);
            yield return PickUp(rig.Slot, first);

            GameObject invalidObject = Own(new GameObject("Invalid Prop"));
            RagdollProp invalid = invalidObject.AddComponent<RagdollProp>();
            string error;
            Assert.That(rig.Slot.TrySetCurrentProp(invalid, out error), Is.False);
            Assert.That(error, Does.Contain("Mesh Root"));
            Assert.That(rig.Slot.CurrentProp, Is.SameAs(first));
            Assert.That(first.IsHeld, Is.True);

            Assert.That(rig.Slot.TrySetCurrentProp(second, out error), Is.True,
                error);
            yield return WaitForHeld(rig.Slot, second);
            Assert.That(first.IsHeld, Is.False);
            Assert.That(first.CurrentRigidbody, Is.Not.Null);
            Assert.That(second.IsHeld, Is.True);

            yield return Drop(rig.Slot);
            Assert.That(second.IsHeld, Is.False);
            Assert.That(second.CurrentRigidbody, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator F03_MeshRootMovesBetweenTargetAndPropWithoutPoseOrScaleLoss()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Pose Prop", false);
            Transform originalParent = prop.transform.parent;
            Vector3 rootScale = prop.transform.lossyScale;
            Vector3 meshLocalPosition = prop.MeshRoot.localPosition;
            Quaternion meshLocalRotation = prop.MeshRoot.localRotation;
            Vector3 meshLocalScale = prop.MeshRoot.localScale;

            yield return PickUp(rig.Slot, prop);
            Assert.That(prop.transform.parent, Is.SameAs(rig.Slot.Joint.transform));
            Assert.That(prop.MeshRoot.parent, Is.SameAs(rig.Slot.TargetSlot));
            AssertVector(prop.MeshRoot.localPosition, meshLocalPosition);
            AssertQuaternion(prop.MeshRoot.localRotation, meshLocalRotation);
            AssertVector(prop.MeshRoot.localScale, meshLocalScale);

            yield return Drop(rig.Slot);
            Assert.That(prop.transform.parent, Is.SameAs(originalParent));
            AssertVector(prop.transform.lossyScale, rootScale);
            AssertVector(prop.MeshRoot.localPosition, meshLocalPosition);
            AssertQuaternion(prop.MeshRoot.localRotation, meshLocalRotation);
            AssertVector(prop.MeshRoot.localScale, meshLocalScale);
        }

        [UnityTest]
        public IEnumerator F04_CurrentRigidbodyTracksPhysicalOwnershipOnly()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Ownership Prop", false);
            Rigidbody standalone = prop.CurrentRigidbody;
            Assert.That(standalone, Is.SameAs(prop.GetComponent<Rigidbody>()));

            string error;
            Assert.That(rig.Slot.TrySetCurrentProp(prop, out error), Is.True,
                error);
            yield return new WaitForFixedUpdate();
            Assert.That(prop.CurrentRigidbody, Is.SameAs(rig.SlotBody),
                "The slot owns the prop as soon as pickup preparation starts.");
            yield return WaitForHeld(rig.Slot, prop);
            Assert.That(prop.CurrentRigidbody, Is.SameAs(rig.SlotBody));
            Assert.That(prop.CurrentRigidbody, Is.Not.SameAs(standalone));

            yield return Drop(rig.Slot);
            Assert.That(prop.CurrentRigidbody, Is.Not.Null);
            Assert.That(prop.CurrentRigidbody, Is.SameAs(prop.GetComponent<Rigidbody>()));
            Assert.That(prop.CurrentRigidbody, Is.Not.SameAs(standalone),
                "Unity destroys the old component; drop must expose the restored body.");
        }

        [UnityTest]
        public IEnumerator F05_HeldMassLayerMaterialOverrideAndDropRestoreExactly()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Surface Prop", false);
            Collider propCollider = prop.GetComponent<Collider>();
            PhysicsMaterial baseline = Own(new PhysicsMaterial("baseline"));
            PhysicsMaterial held = Own(new PhysicsMaterial("held"));
            propCollider.sharedMaterial = baseline;
            prop.gameObject.layer = 12;
            prop.MeshRoot.gameObject.layer = 13;
            prop.PickedUpMass = 4.75f;
            prop.PickedUpMaterial = held;
            float slotMass = rig.SlotBody.mass;

            yield return PickUp(rig.Slot, prop);
            Assert.That(rig.SlotBody.mass, Is.EqualTo(4.75f).Within(0.0001f));
            Assert.That(prop.gameObject.layer,
                Is.EqualTo(rig.Slot.Joint.gameObject.layer));
            Assert.That(prop.MeshRoot.gameObject.layer,
                Is.EqualTo(rig.Slot.TargetSlot.gameObject.layer));
            Assert.That(propCollider.sharedMaterial, Is.SameAs(held));

            yield return Drop(rig.Slot);
            Assert.That(rig.SlotBody.mass, Is.EqualTo(slotMass).Within(0.0001f));
            Assert.That(prop.gameObject.layer, Is.EqualTo(12));
            Assert.That(prop.MeshRoot.gameObject.layer, Is.EqualTo(13));
            Assert.That(propCollider.sharedMaterial, Is.SameAs(baseline));
        }

        [UnityTest]
        public IEnumerator F06_InternalCollisionIgnoresAreOwnedAndReleasedOnce()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Ignore Prop", false);
            Collider propCollider = prop.GetComponent<Collider>();
            prop.InternalCollisionIgnores.IgnoreAll = true;
            rig.Animator.InternalCollisions = true;
            yield return new WaitForFixedUpdate();
            Assert.That(Physics.GetIgnoreCollision(
                propCollider, rig.RootCollider), Is.False);

            yield return PickUp(rig.Slot, prop);
            Assert.That(prop.ActiveInternalCollisionIgnorePairCount,
                Is.GreaterThan(0));
            Assert.That(Physics.GetIgnoreCollision(
                propCollider, rig.RootCollider), Is.True);

            yield return Drop(rig.Slot);
            yield return null;
            Assert.That(prop.ActiveInternalCollisionIgnorePairCount, Is.Zero);
            Assert.That(Physics.GetIgnoreCollision(
                propCollider, rig.RootCollider), Is.False);
            Assert.That(prop.IsCollisionRestorePending, Is.False);
        }

        [UnityTest]
        public IEnumerator F07_AnimatedTargetChildrenSurviveCompatibleHandReplacement()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Animated Child Prop", false);
            RagdollBehaviourContext context = rig.Animator
                .GetComponent<RagdollBehaviourController>().Context;
            RagdollBoneHandle handHandle =
                rig.Animator.Bindings.GetHandleAt(1);
            RagdollAnimator.AnimatedPair handPair =
                context.Pairs.First(
                    pair => pair.Handle == handHandle);
            Transform animatedChild = new GameObject("Animated Finger").transform;
            animatedChild.SetParent(rig.Slot.TargetSlot, false);
            animatedChild.localPosition = new Vector3(0.1f, 0.2f, -0.05f);
            Quaternion localRotation = Quaternion.Euler(5f, 15f, 25f);
            animatedChild.localRotation = localRotation;
            prop.SetAnimatedTargetChildren(new[] { animatedChild });
            yield return PickUp(rig.Slot, prop);
            Assert.That(context.TryGetPair(
                rig.Slot.Handle,
                out RagdollAnimator.AnimatedPair initialSlotPair), Is.True);
            Assert.That(initialSlotPair.TargetBinding.AnimatedTargetChildren.Count,
                Is.EqualTo(1));
            Assert.That(initialSlotPair.TargetBinding.AnimatedTargetChildren[0],
                Is.SameAs(animatedChild));

            GameObject replacement = new GameObject("Compatible Hand Replacement");
            replacement.transform.SetParent(rig.Puppet.transform, false);
            replacement.transform.SetPositionAndRotation(
                rig.ChildBody.position,
                rig.ChildBody.rotation);
            Rigidbody replacementBody = replacement.AddComponent<Rigidbody>();
            replacementBody.useGravity = false;
            ConfigurableJoint replacementJoint =
                replacement.AddComponent<ConfigurableJoint>();
            replacementJoint.connectedBody = rig.RootBody;
            replacement.AddComponent<BoxCollider>();
            string error;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.TryReplaceMuscle(
                handHandle,
                new RagdollRuntimeMuscleRegistration(
                    new BoneName("RightHand"),
                    replacementJoint,
                    handPair.TargetBone,
                    RagdollMuscleGroup.Hand,
                    null,
                    false,
                    true),
                out RagdollBoneHandle replacementHandle,
                out error), Is.True, error);

            Assert.That(rig.Animator.Bindings.Topology.Contains(handHandle),
                Is.False, "The pre-transaction handle must be stale.");
            Assert.That(rig.Animator.Bindings.Topology.Contains(replacementHandle),
                Is.True);
            Assert.That(rig.Slot.State,
                Is.EqualTo(RagdollPropMuscleState.Holding), rig.Slot.LastError);
            Assert.That(rig.Slot.CurrentProp, Is.SameAs(prop));
            Assert.That(prop.CurrentRigidbody, Is.SameAs(rig.SlotBody));
            Assert.That(rig.Slot.Joint.connectedBody, Is.SameAs(replacementBody));
            context = rig.Animator.GetComponent<RagdollBehaviourController>()
                .Context;
            Assert.That(context.TryGetPair(
                rig.Slot.Handle,
                out RagdollAnimator.AnimatedPair reboundSlotPair), Is.True);
            Assert.That(reboundSlotPair.TargetBinding.AnimatedTargetChildren.Count,
                Is.EqualTo(1));
            Assert.That(reboundSlotPair.TargetBinding.AnimatedTargetChildren[0],
                Is.SameAs(animatedChild));
            AssertVector(animatedChild.localPosition,
                new Vector3(0.1f, 0.2f, -0.05f));
            AssertQuaternion(animatedChild.localRotation, localRotation);

            yield return Drop(rig.Slot);
            Assert.That(reboundSlotPair.TargetBinding.AnimatedTargetChildren,
                Is.Empty,
                "Drop must restore the authored Target binding snapshot.");
            Assert.That(prop.AnimatedTargetChildren.Count, Is.EqualTo(1),
                "Prop configuration remains available for the next pickup.");
        }

        [UnityTest]
        public IEnumerator F08_HeldPropRebindsAcrossDisconnectAndReconnect()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Reconnect Prop", false);
            prop.AddAdditionalPin();
            prop.AdditionalPin.Weight = 0.65f;
            yield return PickUp(rig.Slot, prop);
            RagdollBoneHandle hand = rig.Animator.Bindings.GetHandleAt(1);

            rig.Animator.DisconnectMuscleRecursive(
                hand,
                RagdollMuscleDisconnectMode.Sever,
                false);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.GetMuscleConnectionState(hand),
                Is.EqualTo(RagdollMuscleConnectionState.Disconnected));
            Assert.That(rig.Slot.State,
                Is.EqualTo(RagdollPropMuscleState.Holding), rig.Slot.LastError);
            Assert.That(rig.Slot.CurrentProp, Is.SameAs(prop));
            Assert.That(prop.CurrentRigidbody, Is.SameAs(rig.SlotBody));
            Assert.That(prop.AdditionalPin.Enabled, Is.True);

            rig.Animator.ReconnectMuscleRecursive(hand);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Animator.GetMuscleConnectionState(hand),
                Is.EqualTo(RagdollMuscleConnectionState.Connected));
            Assert.That(rig.Slot.Handle.IsValid, Is.True);
            Assert.That(rig.Animator.Bindings.Topology.Contains(rig.Slot.Handle),
                Is.True);
            Assert.That(rig.Slot.State,
                Is.EqualTo(RagdollPropMuscleState.Holding), rig.Slot.LastError);
            Assert.That(prop.CurrentRigidbody, Is.SameAs(rig.SlotBody));
            Assert.That(prop.AdditionalPin.Weight, Is.EqualTo(0.65f));
        }

        [UnityTest]
        public IEnumerator F09_AdditionalPinAddsAndRemovesAtPhysicsBoundary()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Pin Prop", false);
            prop.RemoveAdditionalPin();
            yield return PickUp(rig.Slot, prop);
            prop.AddAdditionalPin();
            prop.AdditionalPin.Weight = 0.8f;
            prop.AdditionalPin.Mass = 1.5f;
            rig.Slot.TargetSlot.position += Vector3.right * 0.2f;
            Assert.That(prop.LastAdditionalPinStep.AppliedWeight, Is.Zero);
            yield return new WaitForFixedUpdate();
            Assert.That(prop.LastAdditionalPinStep.AppliedWeight,
                Is.EqualTo(0.8f).Within(0.0001f));

            prop.RemoveAdditionalPin();
            yield return new WaitForFixedUpdate();
            Assert.That(prop.LastAdditionalPinStep.Applied, Is.False);
            Assert.That(prop.LastAdditionalPinStep.AppliedWeight, Is.Zero);
        }

        [UnityTest]
        public IEnumerator F10_AdditionalPinLiveSettingsCommitTogetherAndInvalidOwnerPreservesThem()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Dynamic Pin Prop", false);
            prop.AddAdditionalPin();
            yield return PickUp(rig.Slot, prop);

            Vector3 offset = new Vector3(0.15f, 0.05f, -0.1f);
            prop.AdditionalPin.LocalOffset = offset;
            prop.AdditionalPin.Weight = 0.65f;
            prop.AdditionalPin.Mass = 2.25f;
            rig.Slot.TargetSlot.position += Vector3.forward * 0.2f;
            yield return new WaitForFixedUpdate();
            Assert.That(prop.LastAdditionalPinStep.AppliedWeight,
                Is.EqualTo(0.65f).Within(0.0001f));

            RuntimePropSlot secondSlot = CreateAdditionalSlot(rig, "LeftHandProp");
            yield return WaitForState(secondSlot.Muscle,
                RagdollPropMuscleState.Empty);
            string error;
            Assert.That(secondSlot.Muscle.TrySetCurrentProp(prop, out error),
                Is.False);
            Assert.That(error, Does.Contain("another RagdollPropMuscle"));
            Assert.That(prop.CurrentMuscle, Is.SameAs(rig.Slot));
            AssertVector(prop.AdditionalPin.LocalOffset, offset);
            Assert.That(prop.AdditionalPin.Weight,
                Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(prop.AdditionalPin.Mass,
                Is.EqualTo(2.25f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator F11_BehaviourPuppetDropRestoresPropBeforeOtherHierarchyChanges()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Behaviour Drop Prop", false);
            Collider collider = prop.GetComponent<Collider>();
            PhysicsMaterial baseline = Own(new PhysicsMaterial("drop baseline"));
            PhysicsMaterial held = Own(new PhysicsMaterial("drop held"));
            collider.sharedMaterial = baseline;
            prop.PickedUpMaterial = held;
            yield return PickUp(rig.Slot, prop);

            int requested = rig.PuppetBehaviour.DropPropsNow();
            Assert.That(requested, Is.EqualTo(1));
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            Assert.That(prop.IsHeld, Is.False);
            Assert.That(prop.CurrentRigidbody, Is.Not.Null);
            Assert.That(collider.sharedMaterial, Is.SameAs(baseline));
            Assert.That(rig.Animator.Bindings.TryGetBoneHandle(
                new BoneName("Root"), out RagdollBoneHandle root), Is.True);
            Assert.That(rig.Animator.Bindings.TryGetBoneHandle(
                new BoneName("RightHand"), out RagdollBoneHandle hand), Is.True);
            Assert.That(rig.Animator.GetMuscleConnectionState(root),
                Is.EqualTo(RagdollMuscleConnectionState.Connected));
            Assert.That(rig.Animator.GetMuscleConnectionState(hand),
                Is.EqualTo(RagdollMuscleConnectionState.Connected));
        }

        [UnityTest]
        public IEnumerator F12_HeldMeleeUsesCapsuleForWholeHeldStateAndBoxAfterDrop()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Melee Prop", true);
            BoxCollider box = prop.GetComponent<BoxCollider>();
            RagdollPropMelee melee = prop.Melee;

            yield return PickUp(rig.Slot, prop);
            Assert.That(melee.IsHeldSession, Is.True);
            Assert.That(box.enabled, Is.False);
            Assert.That(melee.ActionCollider, Is.TypeOf<CapsuleCollider>());
            Assert.That(melee.ActionCollider.enabled, Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(box.enabled, Is.False);
            Assert.That(melee.ActionCollider.enabled, Is.True);

            yield return Drop(rig.Slot);
            Assert.That(melee.IsHeldSession, Is.False);
            Assert.That(box.enabled, Is.True);
            Assert.That(melee.ActionCollider == null
                || !melee.ActionCollider.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator F13_TimedMeleeRestartsExpiresAndCancelsAtPhysicsBoundary()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("Timed Action Prop", true);
            prop.PickedUpMass = 2f;
            prop.AddAdditionalPin();
            prop.AdditionalPin.Weight = 0.4f;
            RagdollPropMelee melee = prop.Melee;
            melee.Settings.Radius = 0.1f;
            melee.Settings.ActionColliderRadiusMultiplier = 2f;
            melee.Settings.ActionMassMultiplier = 3f;
            melee.Settings.ActionAdditionalPinWeight = 1.7f;
            yield return PickUp(rig.Slot, prop);
            CapsuleCollider capsule = melee.ActionCollider as CapsuleCollider;
            Assert.That(capsule, Is.Not.Null);
            Assert.That(capsule.radius, Is.EqualTo(0.1f).Within(0.0001f));

            Assert.That(melee.StartAction(0.06f), Is.True,
                melee.LastActionError);
            int actionVersion = melee.ActionVersion;
            yield return new WaitForFixedUpdate();
            Assert.That(melee.IsActionActive, Is.True);
            Assert.That(capsule.radius, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(rig.SlotBody.mass, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(prop.LastAdditionalPinStep.AppliedWeight,
                Is.EqualTo(1.7f).Within(0.0001f));

            Assert.That(melee.StartAction(0.04f), Is.True,
                melee.LastActionError);
            Assert.That(melee.ActionVersion, Is.EqualTo(actionVersion),
                "Restart must retain one action snapshot.");
            yield return new WaitForFixedUpdate();
            Assert.That(melee.IsActionActive, Is.True);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(melee.IsActionActive, Is.False);
            Assert.That(capsule.radius, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(rig.SlotBody.mass, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(prop.LastAdditionalPinStep.AppliedWeight,
                Is.EqualTo(0.4f).Within(0.0001f));

            Assert.That(melee.StartAction(0.5f), Is.True,
                melee.LastActionError);
            yield return new WaitForFixedUpdate();
            string error;
            Assert.That(rig.Slot.TryDrop(out error), Is.True, error);
            yield return new WaitForFixedUpdate();
            Assert.That(melee.IsActionActive, Is.False);
            Assert.That(rig.SlotBody.mass, Is.EqualTo(2f).Within(0.0001f));
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            Assert.That(prop.IsHeld, Is.False);
            Assert.That(prop.CurrentRigidbody, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator F14_CenterOfMassOverrideAppliesWhileHeldAndRestoresOnActionAndDrop()
        {
            RuntimePropRig rig = CreateRig();
            yield return WaitForState(rig.Slot, RagdollPropMuscleState.Empty);
            RagdollProp prop = CreateProp("COM Prop", true);
            RagdollPropMelee melee = prop.Melee;
            Vector3 baseline = rig.SlotBody.centerOfMass;
            Vector3 offset = new Vector3(0.2f, -0.1f, 0.15f);
            melee.Settings.CenterOfMassOffset = offset;

            yield return PickUp(rig.Slot, prop);
            yield return new WaitForFixedUpdate();
            Assert.That(prop.IsHeldCenterOfMassOverridden, Is.True);
            AssertVector(rig.SlotBody.centerOfMass, baseline + offset);
            Assert.That(melee.StartAction(0.04f), Is.True, melee.LastActionError);
            yield return new WaitForFixedUpdate();
            AssertVector(rig.SlotBody.centerOfMass, baseline + offset);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(melee.IsActionActive, Is.False);
            AssertVector(rig.SlotBody.centerOfMass, baseline + offset,
                "Ending an action must preserve the held COM override.");

            yield return Drop(rig.Slot);
            AssertVector(rig.SlotBody.centerOfMass, baseline);
            Assert.That(prop.IsHeldCenterOfMassOverridden, Is.False);
        }

        RuntimePropRig CreateRig()
        {
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("RightHand");
            GameObject puppet = Own(new GameObject("Prop Puppet"));
            puppet.SetActive(false);
            GameObject physicalChild = new GameObject("RightHand");
            physicalChild.transform.SetParent(puppet.transform, false);
            physicalChild.transform.localPosition = Vector3.up;
            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            rootBody.useGravity = false;
            ConfigurableJoint rootJoint =
                puppet.AddComponent<ConfigurableJoint>();
            BoxCollider rootCollider = puppet.AddComponent<BoxCollider>();
            Rigidbody childBody = physicalChild.AddComponent<Rigidbody>();
            childBody.useGravity = false;
            ConfigurableJoint childJoint =
                physicalChild.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            physicalChild.AddComponent<BoxCollider>();

            RagdollDefinition definition =
                Own(ScriptableObject.CreateInstance<RagdollDefinition>());
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            RagdollDefinitionBindings bindings =
                puppet.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            puppet.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            GameObject target = Own(new GameObject("Prop Puppet"));
            GameObject targetHand = new GameObject("RightHand");
            targetHand.transform.SetParent(target.transform, false);
            targetHand.transform.localPosition = Vector3.up;
            RagdollAnimationProfile profile =
                Own(ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            RagdollSetupResult setup =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    target.transform, bindings, profile, 30, 31);
            Assert.That(setup.Succeeded, Is.True, setup.Error);

            GameObject standaloneParent =
                Own(new GameObject("Standalone Props"));
            standaloneParent.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);
            RuntimePropRig rig = new RuntimePropRig
            {
                Puppet = puppet,
                Target = target,
                StandaloneParent = standaloneParent.transform,
                Animator = setup.Animator,
                Muscles = setup.Muscles,
                PuppetBehaviour = setup.PuppetBehaviour,
                RootBody = rootBody,
                ChildBody = childBody,
                RootCollider = rootCollider,
                RootHandle = bindings.GetHandleAt(0),
                ChildHandle = bindings.GetHandleAt(1)
            };
            RuntimePropSlot slot = CreateAdditionalSlot(rig, "RightHandProp");
            rig.Slot = slot.Muscle;
            rig.SlotBody = slot.Body;
            return rig;
        }

        RuntimePropSlot CreateAdditionalSlot(RuntimePropRig rig, string boneName)
        {
            GameObject physical = new GameObject(boneName + " Physical");
            physical.transform.SetParent(rig.Puppet.transform, false);
            physical.transform.position = rig.RootBody.position + Vector3.right;
            physical.layer = rig.Puppet.layer;
            Rigidbody body = physical.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1.25f;
            ConfigurableJoint joint = physical.AddComponent<ConfigurableJoint>();
            joint.connectedBody = rig.ChildBody;
            physical.AddComponent<BoxCollider>();

            GameObject targetSlotObject = new GameObject(boneName + " Target");
            targetSlotObject.transform.SetParent(rig.Target.transform, false);
            targetSlotObject.transform.position = physical.transform.position;
            targetSlotObject.layer = rig.Target.layer;
            GameObject muscleObject = new GameObject(boneName + " Muscle");
            muscleObject.transform.SetParent(rig.Animator.transform, false);
            RagdollPropMuscle muscle =
                muscleObject.AddComponent<RagdollPropMuscle>();
            string error;
            Assert.That(muscle.TryConfigureBeforeInitialization(
                rig.Animator,
                joint,
                targetSlotObject.transform,
                rig.Target.transform,
                new BoneName(boneName),
                false,
                true,
                out error), Is.True, error);
            muscle.Initialize();
            return new RuntimePropSlot
            {
                Muscle = muscle,
                Body = body
            };
        }

        RagdollProp CreateProp(string name, bool melee)
        {
            GameObject root = Own(new GameObject(name));
            root.transform.SetParent(
                ownedStandaloneParent != null ? ownedStandaloneParent : null,
                false);
            root.transform.localPosition = new Vector3(0.4f, 1.1f, -0.25f);
            root.transform.localRotation = Quaternion.Euler(5f, 20f, -10f);
            root.transform.localScale = new Vector3(0.8f, 1.15f, 0.95f);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.1f, 0.1f, 1.2f);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 2.5f;
            body.centerOfMass = new Vector3(0.02f, 0.03f, -0.04f);
            RagdollProp prop = root.AddComponent<RagdollProp>();
            if (melee) root.AddComponent<RagdollPropMelee>();
            GameObject visual = new GameObject(name + " Mesh Root");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(1.1f, 0.9f, 1.05f);
            string error;
            Assert.That(prop.TryConfigureStandalone(
                visual.transform, body, out error), Is.True, error);
            return prop;
        }

        Transform ownedStandaloneParent;

        IEnumerator PickUp(RagdollPropMuscle slot, RagdollProp prop)
        {
            ownedStandaloneParent = prop.transform.parent;
            slot.Animator.Bindings.GetBone(slot.Handle).PowerSetting =
                PowerSetting.Powered;
            slot.Joint.GetComponent<Rigidbody>().isKinematic = false;
            string error;
            Assert.That(slot.TrySetCurrentProp(prop, out error), Is.True, error);
            yield return WaitForHeld(slot, prop);
        }

        static IEnumerator WaitForHeld(
            RagdollPropMuscle slot,
            RagdollProp expected)
        {
            for (int frame = 0; frame < 60; frame++)
            {
                if (slot.State == RagdollPropMuscleState.Holding
                    && slot.CurrentProp == expected)
                    yield break;
                Assert.That(slot.State, Is.Not.EqualTo(
                    RagdollPropMuscleState.Faulted), slot.LastError);
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("Prop pickup did not reach Holding: " + slot.LastError);
        }

        static IEnumerator Drop(RagdollPropMuscle slot)
        {
            string error;
            Assert.That(slot.TryDrop(out error), Is.True, error);
            yield return WaitForState(slot, RagdollPropMuscleState.Empty);
        }

        static IEnumerator WaitForState(
            RagdollPropMuscle slot,
            RagdollPropMuscleState expected)
        {
            for (int frame = 0; frame < 60; frame++)
            {
                if (slot.State == expected) yield break;
                Assert.That(slot.State, Is.Not.EqualTo(
                    RagdollPropMuscleState.Faulted), slot.LastError);
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("Prop slot did not reach " + expected + ": "
                + slot.LastError);
        }

        static object CreateBindings(
            BoneName root,
            ConfigurableJoint rootJoint,
            BoneName child,
            ConfigurableJoint childJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary", BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { child, childJoint });
            return dictionary;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        static void AssertVector(
            Vector3 actual,
            Vector3 expected,
            string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected),
                Is.LessThan(0.001f), message);
        }

        static void AssertQuaternion(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.05f));
        }

        sealed class RuntimePropRig
        {
            internal GameObject Puppet;
            internal GameObject Target;
            internal Transform StandaloneParent;
            internal RagdollAnimator Animator;
            internal RagdollMuscleController Muscles;
            internal RagdollPuppetBehaviour PuppetBehaviour;
            internal RagdollPropMuscle Slot;
            internal Rigidbody SlotBody;
            internal Rigidbody RootBody;
            internal Rigidbody ChildBody;
            internal Collider RootCollider;
            internal RagdollBoneHandle RootHandle;
            internal RagdollBoneHandle ChildHandle;
        }

        sealed class RuntimePropSlot
        {
            internal RagdollPropMuscle Muscle;
            internal Rigidbody Body;
        }
    }
}
