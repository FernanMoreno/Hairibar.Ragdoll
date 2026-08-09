using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_6000_0_OR_NEWER
using PhysicMaterial = UnityEngine.PhysicsMaterial;
#endif

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Direct PlayMode evidence for BehaviourPuppet's documented boost, per-group
    /// authority, collider-surface and prop-drop contracts. The two-muscle rig is
    /// initialized through the public runtime setup service so every assertion is
    /// evaluated against registered muscles and the active behaviour pipeline.
    /// </summary>
    public sealed class RagdollPuppetAuthorityCapabilityPlayModeTests
    {
        PuppetPhysicsRig rig;
        Vector3 originalGravity;

        [SetUp]
        public void SetUp()
        {
            originalGravity = Physics.gravity;
            Physics.gravity = Vector3.zero;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (rig != null) rig.Dispose();
            rig = null;
            Physics.gravity = originalGravity;
            yield return null;
        }

        [UnityTest]
        public IEnumerator D19_ImmunityBoostRenewsFallsOffAndDoesNotAffectOtherGroup()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Puppet.BoostFalloff = 1f;

            Assert.That(
                rig.Puppet.BoostImmunity(RagdollMuscleGroup.Hips, 1f),
                Is.EqualTo(1));
            float first = rig.Muscles.GetImmunity(rig.RootHandle);
            Assert.That(first, Is.GreaterThan(0f));
            Assert.That(rig.Muscles.GetImmunity(rig.ChildHandle), Is.Zero,
                "A semantic group boost must not leak into an unrelated group.");
            rig.Puppet.BoostImmunity(0, 0.8f);
            Assert.That(rig.Muscles.GetImmunity(rig.RootHandle),
                Is.EqualTo(0.8f).Within(0.0001f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                rig.Puppet.BoostImmunity(2, 1f));

            yield return new WaitForFixedUpdate();
            float decayed = rig.Muscles.GetImmunity(rig.RootHandle);
            Assert.That(decayed, Is.LessThan(first));

            rig.Puppet.BoostImmunity(RagdollMuscleGroup.Hips, 1f);
            Assert.That(rig.Muscles.GetImmunity(rig.RootHandle),
                Is.GreaterThan(decayed), "Calling BoostImmunity again renews it.");
            rig.Puppet.BoostImmunity(RagdollMuscleGroup.Hips, 0.25f);
            Assert.That(rig.Muscles.GetImmunity(rig.RootHandle),
                Is.EqualTo(0.25f).Within(0.0001f),
                "The official API sets the specified immunity; it is not max-only.");
            rig.Puppet.BoostImmunity(RagdollMuscleGroup.Hips, 1f);

            for (int step = 0; step < 70; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(rig.Muscles.GetImmunity(rig.RootHandle), Is.Zero);
            Assert.That(rig.Puppet.HasActiveBoosts, Is.False);
        }

        [UnityTest]
        public IEnumerator D20_ImpulseBoostRenewsFallsOffAndResolvesFromPhysicalSource()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Puppet.BoostFalloff = 2f;

            Assert.That(
                rig.Puppet.BoostImpulseMlp(RagdollMuscleGroup.Hips, 3f),
                Is.EqualTo(1));
            float first = rig.Muscles.GetImpulseMultiplier(rig.RootHandle);
            Assert.That(first, Is.GreaterThan(1f));
            Assert.That(rig.Muscles.GetImpulseMultiplier(rig.ChildHandle),
                Is.EqualTo(1f));
            rig.Puppet.BoostImpulseMlp(0, 2.5f);
            first = rig.Muscles.GetImpulseMultiplier(rig.RootHandle);
            Assert.That(first, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(
                RagdollMuscleController.ResolveExternalImpulseMultiplier(
                    rig.RootBody,
                    null),
                Is.EqualTo(first).Within(0.0001f),
                "The Rigidbody registered for the physical muscle carries the outgoing boost.");

            yield return new WaitForFixedUpdate();
            float decayed = rig.Muscles.GetImpulseMultiplier(rig.RootHandle);
            Assert.That(decayed, Is.LessThan(first).And.GreaterThanOrEqualTo(1f));

            rig.Puppet.BoostImpulseMlp(RagdollMuscleGroup.Hips, 4f);
            Assert.That(rig.Muscles.GetImpulseMultiplier(rig.RootHandle),
                Is.GreaterThan(decayed));
            rig.Puppet.BoostImpulseMlp(RagdollMuscleGroup.Hips, 2f);
            Assert.That(rig.Muscles.GetImpulseMultiplier(rig.RootHandle),
                Is.EqualTo(2f).Within(0.0001f),
                "The official API sets the multiplier; it is not max-only.");

            for (int step = 0; step < 320; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(rig.Muscles.GetImpulseMultiplier(rig.RootHandle),
                Is.EqualTo(1f));
            Assert.That(rig.Puppet.HasActiveBoosts, Is.False);
        }

        [UnityTest]
        public IEnumerator D21_MinimumMappingWeightClampsOnlyConfiguredGroup()
        {
            RagdollMuscleBehaviourSettings hips =
                RagdollMuscleBehaviourSettings.Default;
            hips.minimumMappingAuthority = 0.65f;
            hips.maximumMappingAuthority = 1f;
            RagdollMuscleBehaviourSettings spine =
                RagdollMuscleBehaviourSettings.Default;
            spine.minimumMappingAuthority = 0.1f;
            spine.maximumMappingAuthority = 1f;
            rig = new PuppetPhysicsRig(
                RagdollPuppetNormalMode.Active,
                hips,
                spine);
            yield return rig.Initialize();

            RagdollMappingWeights root = RagdollMappingWeights.Full;
            RagdollMappingWeights child = RagdollMappingWeights.Full;
            rig.Muscles.ModifyMapping(
                ref root,
                rig.Result.Behaviours.Context.Pairs[0]);
            rig.Muscles.ModifyMapping(
                ref child,
                rig.Result.Behaviours.Context.Pairs[1]);

            Assert.That(root.PositionWeight, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(root.RotationWeight, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(child.PositionWeight, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(child.RotationWeight, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator D22_MaximumMappingWeightClampsOnlyConfiguredGroup()
        {
            RagdollMuscleBehaviourSettings hips =
                RagdollMuscleBehaviourSettings.Default;
            hips.minimumMappingAuthority = 0f;
            hips.maximumMappingAuthority = 0.35f;
            RagdollMuscleBehaviourSettings spine =
                RagdollMuscleBehaviourSettings.Default;
            spine.minimumMappingAuthority = 0f;
            spine.maximumMappingAuthority = 0.9f;
            rig = new PuppetPhysicsRig(
                RagdollPuppetNormalMode.Active,
                hips,
                spine);
            yield return rig.Initialize();
            rig.Muscles.PositionSuppressionRecoveryRate = 0f;
            rig.Muscles.AccumulateSuppression(rig.RootHandle, 1f, 0f);
            rig.Muscles.AccumulateSuppression(rig.ChildHandle, 1f, 0f);

            RagdollMappingWeights root = RagdollMappingWeights.Full;
            RagdollMappingWeights child = RagdollMappingWeights.Full;
            rig.Muscles.ModifyMapping(
                ref root,
                rig.Result.Behaviours.Context.Pairs[0]);
            rig.Muscles.ModifyMapping(
                ref child,
                rig.Result.Behaviours.Context.Pairs[1]);

            Assert.That(root.PositionWeight, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(child.PositionWeight, Is.EqualTo(0.9f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator D23_MinimumPinWeightClampsOnlyConfiguredGroup()
        {
            RagdollMuscleBehaviourSettings hips =
                RagdollMuscleBehaviourSettings.Default;
            hips.minimumPositionAuthority = 0.6f;
            RagdollMuscleBehaviourSettings spine =
                RagdollMuscleBehaviourSettings.Default;
            spine.minimumPositionAuthority = 0.2f;
            rig = new PuppetPhysicsRig(
                RagdollPuppetNormalMode.Active,
                hips,
                spine);
            yield return rig.Initialize();
            rig.Muscles.PositionSuppressionRecoveryRate = 0f;
            rig.Muscles.AccumulateSuppression(rig.RootHandle, 1f, 0f);
            rig.Muscles.AccumulateSuppression(rig.ChildHandle, 1f, 0f);

            BoneProfile root = FullBoneProfile();
            BoneProfile child = FullBoneProfile();
            rig.Muscles.Modify(
                ref root,
                rig.Result.Behaviours.Context.Pairs[0],
                Time.fixedDeltaTime);
            rig.Muscles.Modify(
                ref child,
                rig.Result.Behaviours.Context.Pairs[1],
                Time.fixedDeltaTime);

            Assert.That(root.PositionPinWeight,
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(child.PositionPinWeight,
                Is.EqualTo(0.2f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator D24_DisableCollidersAffectsOnlyConfiguredGroup()
        {
            RagdollMuscleBehaviourSettings hips =
                RagdollMuscleBehaviourSettings.Default;
            hips.disableColliders = true;
            RagdollMuscleBehaviourSettings spine =
                RagdollMuscleBehaviourSettings.Default;
            spine.disableColliders = false;
            rig = new PuppetPhysicsRig(
                RagdollPuppetNormalMode.Active,
                hips,
                spine);
            BoxCollider root = rig.RootBody.GetComponent<BoxCollider>();
            BoxCollider child = rig.ChildBody.GetComponent<BoxCollider>();
            root.enabled = true;
            child.enabled = true;
            yield return rig.Initialize();

            Assert.That(root.enabled, Is.False);
            Assert.That(child.enabled, Is.True);
            Assert.That(rig.Puppet.SurfaceDisabledColliderCount, Is.EqualTo(1));

            rig.Puppet.State = RagdollPuppetState.Unpinned;
            Assert.That(root.enabled, Is.True);
            Assert.That(child.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator D25_StateMaterialsAndDisableRestoreExactAuthoredSurface()
        {
            PhysicMaterial rootBaseline = new PhysicMaterial("root authored");
            PhysicMaterial childBaseline = new PhysicMaterial("child authored");
            PhysicMaterial rootPuppet = new PhysicMaterial("root puppet");
            PhysicMaterial childPuppet = new PhysicMaterial("child puppet");
            PhysicMaterial rootUnpinned = new PhysicMaterial("root unpinned");
            PhysicMaterial childUnpinned = new PhysicMaterial("child unpinned");
            try
            {
                RagdollMuscleBehaviourSettings hips =
                    RagdollMuscleBehaviourSettings.Default;
                hips.puppetMaterial = rootPuppet;
                hips.unpinnedMaterial = rootUnpinned;
                RagdollMuscleBehaviourSettings spine =
                    RagdollMuscleBehaviourSettings.Default;
                spine.puppetMaterial = childPuppet;
                spine.unpinnedMaterial = childUnpinned;
                rig = new PuppetPhysicsRig(
                    RagdollPuppetNormalMode.Active,
                    hips,
                    spine);
                BoxCollider root = rig.RootBody.GetComponent<BoxCollider>();
                BoxCollider child = rig.ChildBody.GetComponent<BoxCollider>();
                root.sharedMaterial = rootBaseline;
                child.sharedMaterial = childBaseline;
                yield return rig.Initialize();

                Assert.That(root.sharedMaterial, Is.SameAs(rootPuppet));
                Assert.That(child.sharedMaterial, Is.SameAs(childPuppet));

                rig.Puppet.State = RagdollPuppetState.Unpinned;
                Assert.That(root.sharedMaterial, Is.SameAs(rootUnpinned));
                Assert.That(child.sharedMaterial, Is.SameAs(childUnpinned));

                rig.Puppet.State = RagdollPuppetState.GetUp;
                Assert.That(root.sharedMaterial, Is.SameAs(rootPuppet));
                Assert.That(child.sharedMaterial, Is.SameAs(childPuppet));

                rig.Puppet.enabled = false;
                Assert.That(root.sharedMaterial, Is.SameAs(rootBaseline));
                Assert.That(child.sharedMaterial, Is.SameAs(childBaseline));
            }
            finally
            {
                Object.DestroyImmediate(rootBaseline);
                Object.DestroyImmediate(childBaseline);
                Object.DestroyImmediate(rootPuppet);
                Object.DestroyImmediate(childPuppet);
                Object.DestroyImmediate(rootUnpinned);
                Object.DestroyImmediate(childUnpinned);
            }
        }

        [UnityTest]
        public IEnumerator D29_UnpinnedMuscleMultiplierAppliesAndRestoresDriveAuthority()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Puppet.UnpinnedMuscleWeightMultiplier = 0.25f;
            RagdollAnimator.AnimatedPair pair =
                rig.Result.Behaviours.Context.Pairs[0];

            BoneProfile balanced = FullBoneProfile();
            rig.Result.Behaviours.Modify(
                ref balanced,
                pair,
                Time.fixedDeltaTime);
            Assert.That(balanced.rotationAlpha, Is.EqualTo(1f));

            rig.Puppet.State = RagdollPuppetState.Unpinned;
            BoneProfile unpinned = FullBoneProfile();
            rig.Result.Behaviours.Modify(
                ref unpinned,
                pair,
                Time.fixedDeltaTime);
            Assert.That(unpinned.rotationAlpha,
                Is.EqualTo(0.25f).Within(0.0001f));

            rig.Puppet.State = RagdollPuppetState.Puppet;
            BoneProfile restored = FullBoneProfile();
            rig.Result.Behaviours.Modify(
                ref restored,
                pair,
                Time.fixedDeltaTime);
            Assert.That(restored.rotationAlpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator D30_LosingBalanceDropsActuallyHeldPropTransaction()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            GameObject propObject = new GameObject("D30 real standalone prop");
            propObject.transform.SetParent(rig.RootTarget, false);
            propObject.AddComponent<BoxCollider>();
            Rigidbody standaloneBody = propObject.AddComponent<Rigidbody>();
            standaloneBody.useGravity = false;
            RagdollProp prop = propObject.AddComponent<RagdollProp>();
            GameObject meshRoot = new GameObject("D30 prop mesh root");
            meshRoot.transform.SetParent(propObject.transform, false);
            string configurationError;
            Assert.That(prop.TryConfigureStandalone(
                meshRoot.transform,
                standaloneBody,
                out configurationError), Is.True, configurationError);

                GameObject physicalSlot = new GameObject("D30 real physical prop slot");
                physicalSlot.transform.SetParent(rig.RootBody.transform, false);
                physicalSlot.transform.localPosition = Vector3.right;
                Rigidbody slotBody = physicalSlot.AddComponent<Rigidbody>();
                slotBody.useGravity = false;
                ConfigurableJoint slotJoint =
                    physicalSlot.AddComponent<ConfigurableJoint>();
                slotJoint.connectedBody = rig.ChildBody;
                physicalSlot.AddComponent<BoxCollider>();

                GameObject targetSlotObject = new GameObject("D30 real target prop slot");
                targetSlotObject.transform.SetParent(rig.RootTarget, false);
                targetSlotObject.transform.localPosition = Vector3.right;
                RagdollPropMuscle realMuscle =
                    rig.Animator.gameObject.AddComponent<RagdollPropMuscle>();
                string error;
                Assert.That(realMuscle.TryConfigureBeforeInitialization(
                    rig.Animator,
                    slotJoint,
                    targetSlotObject.transform,
                    rig.RootTarget,
                    new BoneName("D30Prop"),
                    false,
                    true,
                    out error), Is.True, error);
                realMuscle.Initialize();
                for (int frame = 0;
                    frame < 60 && realMuscle.State != RagdollPropMuscleState.Empty;
                    frame++)
                {
                    Assert.That(realMuscle.State,
                        Is.Not.EqualTo(RagdollPropMuscleState.Faulted),
                        realMuscle.LastError);
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(realMuscle.State,
                    Is.EqualTo(RagdollPropMuscleState.Empty),
                    realMuscle.LastError);

                rig.Animator.Bindings.GetBone(realMuscle.Handle).PowerSetting =
                    PowerSetting.Powered;
                slotBody.isKinematic = false;
                Assert.That(realMuscle.TrySetCurrentProp(prop, out error),
                    Is.True, error);
                for (int frame = 0;
                    frame < 60 && realMuscle.State != RagdollPropMuscleState.Holding;
                    frame++)
                {
                    Assert.That(realMuscle.State,
                        Is.Not.EqualTo(RagdollPropMuscleState.Faulted),
                        realMuscle.LastError);
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(realMuscle.IsHoldingProp, Is.True);
                Assert.That(prop.IsHeld, Is.True);
                Assert.That(prop.CurrentRigidbody,
                    Is.SameAs(slotBody));

                rig.Puppet.AutoDiscoverPropMuscles = false;
                rig.Puppet.PropMuscles = new[] { realMuscle };
                rig.Puppet.DropProps = true;
                rig.Puppet.State = RagdollPuppetState.Unpinned;

                Assert.That(rig.Puppet.LastRequestedPropDropCount, Is.EqualTo(1));
                Assert.That(realMuscle.RequestedProp, Is.Null);
                for (int frame = 0;
                    frame < 60 && realMuscle.State != RagdollPropMuscleState.Empty;
                    frame++)
                {
                    Assert.That(realMuscle.State,
                        Is.Not.EqualTo(RagdollPropMuscleState.Faulted),
                        realMuscle.LastError);
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(realMuscle.State,
                    Is.EqualTo(RagdollPropMuscleState.Empty),
                    realMuscle.LastError);
                Assert.That(prop.IsHeld, Is.False);
                Assert.That(prop.CurrentRigidbody,
                    Is.SameAs(prop.GetComponent<Rigidbody>()));
        }

        [UnityTest]
        public IEnumerator D44_RespawnAndDisableRollbackSurfaceToExactAuthoredSnapshot()
        {
            PhysicMaterial authoredRoot = new PhysicMaterial("authored root");
            PhysicMaterial authoredChild = new PhysicMaterial("authored child");
            PhysicMaterial puppetMaterial = new PhysicMaterial("puppet");
            PhysicMaterial unpinnedMaterial = new PhysicMaterial("unpinned");
            try
            {
                RagdollMuscleBehaviourSettings settings =
                    RagdollMuscleBehaviourSettings.Default;
                settings.disableColliders = true;
                settings.puppetMaterial = puppetMaterial;
                settings.unpinnedMaterial = unpinnedMaterial;
                rig = new PuppetPhysicsRig(
                    RagdollPuppetNormalMode.Active,
                    settings,
                    settings);
                BoxCollider root = rig.RootBody.GetComponent<BoxCollider>();
                BoxCollider child = rig.ChildBody.GetComponent<BoxCollider>();
                root.enabled = false;
                child.enabled = true;
                root.sharedMaterial = authoredRoot;
                child.sharedMaterial = authoredChild;
                yield return rig.Initialize();

                Assert.That(root.enabled, Is.False,
                    "Authored-disabled colliders are never enabled by a state policy.");
                Assert.That(child.enabled, Is.False);
                rig.Puppet.State = RagdollPuppetState.Unpinned;
                Assert.That(root.enabled, Is.False);
                Assert.That(child.enabled, Is.True);
                Assert.That(root.sharedMaterial, Is.SameAs(unpinnedMaterial));
                Assert.That(child.sharedMaterial, Is.SameAs(unpinnedMaterial));

                rig.Puppet.Respawn(Vector3.zero, Quaternion.identity);
                Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
                Assert.That(root.enabled, Is.False);
                Assert.That(child.enabled, Is.False);
                Assert.That(root.sharedMaterial, Is.SameAs(puppetMaterial));
                Assert.That(child.sharedMaterial, Is.SameAs(puppetMaterial));

                rig.Puppet.enabled = false;
                Assert.That(root.enabled, Is.False);
                Assert.That(child.enabled, Is.True);
                Assert.That(root.sharedMaterial, Is.SameAs(authoredRoot));
                Assert.That(child.sharedMaterial, Is.SameAs(authoredChild));
            }
            finally
            {
                Object.DestroyImmediate(authoredRoot);
                Object.DestroyImmediate(authoredChild);
                Object.DestroyImmediate(puppetMaterial);
                Object.DestroyImmediate(unpinnedMaterial);
            }
        }

        static BoneProfile FullBoneProfile()
        {
            return new BoneProfile
            {
                positionAlpha = 1f,
                rotationAlpha = 1f,
                positionDampingRatio = 1f,
                rotationDampingRatio = 1f,
                maxLinearAcceleration = 100f,
                maxAngularAcceleration = 100f
            };
        }
    }
}
