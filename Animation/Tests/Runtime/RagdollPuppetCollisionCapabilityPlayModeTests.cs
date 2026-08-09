using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Direct BehaviourPuppet certification for the documented state, normal-mode
    /// and collision contracts. Collision assertions use PhysX callbacks except for
    /// the static-source classification case: Unity does not produce contacts between
    /// two kinematic bodies, so that case injects the collision event at the behaviour
    /// boundary while still exercising the complete acceptance/activation pipeline.
    /// </summary>
    public sealed class RagdollPuppetCollisionCapabilityPlayModeTests
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
        public IEnumerator D01_StateTransitionsExplicitAndAutomaticEmitOnce()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            RagdollPuppetBehaviour puppet = rig.Puppet;
            var transitions = new List<string>();
            puppet.StateChanged += (previous, current, reason) =>
                transitions.Add(previous + ">" + current + ":" + reason);

            puppet.State = RagdollPuppetState.Unpinned;
            puppet.State = RagdollPuppetState.GetUp;
            puppet.State = RagdollPuppetState.Puppet;

            Assert.That(transitions.Count, Is.EqualTo(3));
            Assert.That(transitions[0], Does.StartWith("Puppet>Unpinned"));
            Assert.That(transitions[1], Does.StartWith("Unpinned>GetUp"));
            Assert.That(transitions[2], Does.StartWith("GetUp>Puppet"));

            rig.Animator.MasterPinWeight = 0f;
            rig.Animator.MasterMappingWeight = 0f;
            rig.RootBody.transform.position =
                rig.RootTarget.position + Vector3.right * 1.25f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));
            Assert.That(transitions.Count, Is.EqualTo(4));
            yield return new WaitForFixedUpdate();
            Assert.That(transitions.Count, Is.EqualTo(4),
                "Remaining Unpinned must not repeat the loss callback.");
        }

        [UnityTest]
        public IEnumerator D02_KnockOutDistanceUsesPhysicalTargetDriftBoundary()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            RagdollPuppetBehaviour puppet = rig.Puppet;
            rig.Animator.MasterPinWeight = 0f;
            rig.Animator.MasterMappingWeight = 0f;

            rig.RootBody.transform.position =
                rig.RootTarget.position + Vector3.right * 0.99f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));

            rig.RootBody.transform.position =
                rig.RootTarget.position + Vector3.right * 1.01f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));
            Assert.That(puppet.LastKnockOutBone, Is.EqualTo(rig.RootHandle));
            Assert.That(puppet.LastKnockOutDistance, Is.GreaterThan(1f));
            Assert.That(puppet.LastKnockOutThreshold, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator D03_AcceptedPhysicalCollisionReducesPinAuthority()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            float before = rig.Muscles.GetState(rig.RootHandle).PositionSuppression;

            rig.Shoot(rig.RootBody.position, 0, 12f);
            yield return rig.WaitForAcceptedCollision();

            MuscleRuntimeState after = rig.Muscles.GetState(rig.Puppet.LastAcceptedCollisionBone);
            Assert.That(rig.Puppet.LastCollisionResponse.HasResponse, Is.True);
            Assert.That(rig.Puppet.LastCollisionResponse.PositionSuppression,
                Is.GreaterThan(0f));
            Assert.That(after.PositionSuppression, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator D04_ActiveModeKeepsDynamicSimulationAndFullMapping()
        {
            rig = new PuppetPhysicsRig(RagdollPuppetNormalMode.Active);
            yield return rig.Initialize();
            yield return new WaitForFixedUpdate();

            Assert.That(rig.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Active));
            for (int step = 0; step < 60 && rig.RootBody.isKinematic; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(rig.RootBody.isKinematic, Is.False);
            Assert.That(rig.Puppet.NormalModeMappingWeight,
                Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator D05_UnmappedModeMapsOnlyDuringRecentPhysicalContact()
        {
            rig = new PuppetPhysicsRig(RagdollPuppetNormalMode.Unmapped);
            yield return rig.Initialize();
            rig.Puppet.MappingBlendSpeed = 20f;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.NormalModeMappingWeight, Is.Zero);

            GameObject projectile = rig.Shoot(rig.RootBody.position, 0, 10f);
            yield return rig.WaitForAcceptedCollision();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.UnmappedContactActive, Is.True);
            Assert.That(rig.Puppet.NormalModeMappingWeight, Is.GreaterThan(0f));

            UnityEngine.Object.DestroyImmediate(projectile);
            for (int step = 0; step < 5; step++) yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.UnmappedContactActive, Is.False);
            Assert.That(rig.Puppet.NormalModeMappingWeight, Is.Zero);
        }

        [UnityTest]
        public IEnumerator D06_KinematicModeActivatesFromQualifyingDynamicCollision()
        {
            rig = new PuppetPhysicsRig(RagdollPuppetNormalMode.Kinematic);
            yield return rig.Initialize();
            Assert.That(rig.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Kinematic),
                $"state={rig.Puppet.State}, transitioning={rig.Simulation.IsTransitioning}, " +
                $"rootSuppression={rig.Muscles.GetState(rig.RootHandle).PositionSuppression}, " +
                $"kinematicManaged={rig.Puppet.KinematicModeManaged}");
            Assert.That(rig.RootBody.isKinematic, Is.True);

            rig.Shoot(rig.RootBody.position, 0, 15f);
            for (int step = 0;
                step < 40 && rig.Puppet.KinematicActivationCount == 0;
                step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(rig.Puppet.KinematicActivationCount, Is.EqualTo(1));
            Assert.That(rig.Puppet.LastKinematicActivationSource,
                Is.EqualTo(RagdollPuppetKinematicActivationSource.DynamicRigidbody));
            Assert.That(rig.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Active));
            Assert.That(rig.RootBody.isKinematic, Is.False);
        }

        [UnityTest]
        public IEnumerator D07_RuntimeMappingBlendIsRateLimitedWithoutOvershoot()
        {
            rig = new PuppetPhysicsRig(RagdollPuppetNormalMode.Active);
            yield return rig.Initialize();
            rig.Puppet.MappingBlendSpeed = 0.5f;
            rig.Puppet.SetNormalMode(RagdollPuppetNormalMode.Unmapped, false);
            float before = rig.Puppet.NormalModeMappingWeight;

            yield return new WaitForFixedUpdate();
            float expected = Mathf.MoveTowards(
                before, 0f, 0.5f * Time.fixedDeltaTime);
            Assert.That(rig.Puppet.NormalModeMappingWeight,
                Is.EqualTo(expected).Within(0.0001f));

            for (int step = 0; step < 120; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.NormalModeMappingWeight, Is.Zero);
        }

        [UnityTest]
        public IEnumerator D08_StaticActivationFlagGatesStaticSourceAtBehaviourBoundary()
        {
            rig = new PuppetPhysicsRig(RagdollPuppetNormalMode.Kinematic);
            yield return rig.Initialize();
            rig.Puppet.ActivateOnStaticCollisions = false;
            rig.DispatchStaticCollision();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.KinematicActivationCount, Is.Zero);
            Assert.That(rig.Simulation.CurrentMode,
                Is.EqualTo(RagdollSimulationMode.Kinematic));

            rig.Puppet.ActivateOnStaticCollisions = true;
            rig.DispatchStaticCollision();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.KinematicActivationCount, Is.EqualTo(1));
            Assert.That(rig.Puppet.LastKinematicActivationSource,
                Is.EqualTo(RagdollPuppetKinematicActivationSource.StaticCollider));
        }

        [UnityTest]
        public IEnumerator D09_MinimumImpulseGatesRealActivationAndIsInclusive()
        {
            rig = new PuppetPhysicsRig(RagdollPuppetNormalMode.Kinematic);
            yield return rig.Initialize();
            rig.Puppet.ActivateOnImpulse = float.MaxValue;
            rig.Shoot(rig.RootBody.position, 0, 8f);
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.KinematicActivationCount, Is.Zero);

            rig.Puppet.ActivateOnImpulse = 0f;
            rig.Shoot(rig.ChildBody.position, 0, 8f);
            for (int step = 0;
                step < 40 && rig.Puppet.KinematicActivationCount == 0;
                step++) yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.KinematicActivationCount, Is.EqualTo(1));

            Assert.That(RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                RagdollPuppetNormalMode.Kinematic,
                RagdollPuppetState.Puppet,
                RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                2f, 2f, false, true), Is.True,
                "RootMotion defines this as a minimum, so equality is accepted.");
            Assert.That(RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                RagdollPuppetNormalMode.Kinematic,
                RagdollPuppetState.Puppet,
                RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                float.NaN, 0f, false, true), Is.False);
        }

        [UnityTest]
        public IEnumerator D11_CollisionLayersRejectExcludedAndAcceptIncludedPhysXHits()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Puppet.CollisionLayers = 1 << 9;
            int observed = 0;
            rig.Puppet.CollisionObserved += _ => observed++;

            rig.Shoot(rig.RootBody.position, 8, 10f);
            for (int step = 0; step < 25; step++) yield return new WaitForFixedUpdate();
            Assert.That(observed, Is.GreaterThan(0));
            Assert.That(rig.Puppet.AcceptedCollisionCount, Is.Zero);
            Assert.That(rig.Puppet.LastCollisionRejectionReason,
                Is.EqualTo(RagdollPuppetCollisionRejectionReason.LayerFiltered));

            rig.Shoot(rig.ChildBody.position, 9, 10f);
            yield return rig.WaitForAcceptedCollision();
            Assert.That(rig.Puppet.LastAcceptedCollisionBone, Is.EqualTo(rig.ChildHandle));
        }

        [UnityTest]
        public IEnumerator D12_CollisionThresholdObservesButRejectsBelowThreshold()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Puppet.CollisionThreshold = float.MaxValue;
            int observed = 0;
            int accepted = 0;
            int unpinned = 0;
            rig.Puppet.CollisionObserved += _ => observed++;
            rig.Puppet.CollisionAccepted += _ => accepted++;
            rig.Puppet.CollisionUnpinApplied += (_, __) => unpinned++;

            rig.Shoot(rig.RootBody.position, 9, 10f);
            for (int step = 0; step < 60 && observed == 0; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(observed, Is.GreaterThan(0));
            Assert.That(accepted, Is.Zero);
            Assert.That(unpinned, Is.Zero);
            Assert.That(rig.Puppet.LastCollisionRejectionReason,
                Is.EqualTo(RagdollPuppetCollisionRejectionReason.BelowThreshold));

            // End the rejected contact stream before changing the runtime threshold.
            // Otherwise an accepted Stay from the first projectile can satisfy the
            // accepted-collision wait while correctly applying no second Enter damage.
            rig.ClearProjectiles();
            yield return new WaitForFixedUpdate();
            rig.Puppet.CollisionThreshold = 0f;
            rig.Shoot(rig.ChildBody.position, 0, 10f);
            yield return rig.WaitForAcceptedCollision();
            Assert.That(accepted, Is.GreaterThan(0));
            Assert.That(unpinned, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator D13_GlobalAndGroupResistanceScaleRealCollisionDamage()
        {
            RagdollMuscleBehaviourSettings hips = RagdollMuscleBehaviourSettings.Default;
            hips.collisionResistance = 1f;
            RagdollMuscleBehaviourSettings spine = RagdollMuscleBehaviourSettings.Default;
            spine.collisionResistance = 2f;
            rig = new PuppetPhysicsRig(
                RagdollPuppetNormalMode.Active, hips, spine);
            yield return rig.Initialize();
            rig.Puppet.CollisionResistance.constantResistance = 4f;

            rig.Shoot(rig.ChildBody.position, 0, 10f);
            yield return rig.WaitForAcceptedCollision();
            RagdollPuppetCollisionResponseSnapshot response =
                rig.Puppet.LastCollisionResponse;
            Assert.That(response.MuscleResistanceMultiplier,
                Is.EqualTo(2f).Within(0.001f));
            Assert.That(response.GlobalResistance, Is.EqualTo(4f).Within(0.001f));
            Assert.That(response.EffectiveResistance, Is.EqualTo(8f).Within(0.001f));
            Assert.That(response.UnmitigatedPositionSuppression,
                Is.EqualTo(Mathf.Clamp01(response.DamageImpulseMagnitude / 8f)).Within(0.001f));

            rig.Muscles.ResetBone(rig.ChildHandle);
            rig.Puppet.CollisionResistance.constantResistance = 8f;
            rig.ClearProjectiles();
            yield return new WaitForFixedUpdate();
            rig.Shoot(rig.ChildBody.position, 0, 10f);
            yield return rig.WaitForNewAcceptedCollision();
            response = rig.Puppet.LastCollisionResponse;
            Assert.That(response.EffectiveResistance, Is.EqualTo(16f).Within(0.001f));
            Assert.That(response.UnmitigatedPositionSuppression,
                Is.EqualTo(Mathf.Clamp01(response.DamageImpulseMagnitude / 16f)).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator D14_TargetVelocityEvaluatesResistanceCurveOnRealHit()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            RagdollPuppetCollisionResistance resistance = rig.Puppet.CollisionResistance;
            rig.Animator.MasterMappingWeight = 0f;
            resistance.useTargetSpeedCurve = true;
            resistance.targetSpeedResistance = AnimationCurve.Linear(0f, 2f, 20f, 22f);

            rig.ChildTarget.position += Vector3.up * 0.25f;
            rig.Shoot(rig.ChildBody.position, 0, 10f);
            long acceptedBefore = rig.Puppet.AcceptedCollisionCount;
            for (int step = 0;
                step < 60 && rig.Puppet.AcceptedCollisionCount == acceptedBefore;
                step++)
            {
                rig.ChildTarget.position += Vector3.up * 0.05f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(rig.Puppet.AcceptedCollisionCount,
                Is.GreaterThan(acceptedBefore));
            RagdollPuppetCollisionResponseSnapshot response =
                rig.Puppet.LastCollisionResponse;

            RagdollAnimator.AnimatedPair responsePair =
                rig.Result.Behaviours.Context.GetPair(response.Bone);
            Assert.That(response.TargetSpeed, Is.GreaterThan(0.01f),
                $"bone={response.Bone}, sampledVelocity={responsePair.poseLinearVelocity}, " +
                $"target={responsePair.TargetBone.position}, " +
                $"sampledTarget={responsePair.SampledTargetPose.worldPosition}");
            Assert.That(response.GlobalResistance,
                Is.EqualTo(resistance.targetSpeedResistance.Evaluate(
                    response.TargetSpeed)).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator D15_LayerRuleChangesOnlyItsConfiguredCollisionLayer()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            var rule = new RagdollPuppetCollisionLayerRule
            {
                layers = 1 << 8,
                resistanceMultiplier = 5f,
                overrideCollisionThreshold = true,
                collisionThreshold = float.MaxValue
            };
            rig.Puppet.SetCollisionResistanceMultipliers(
                new[] { rule });

            rig.Shoot(rig.RootBody.position, 8, 10f);
            for (int step = 0; step < 25; step++) yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.AcceptedCollisionCount, Is.Zero);

            rig.Shoot(rig.ChildBody.position, 9, 10f);
            yield return rig.WaitForAcceptedCollision();
            Assert.That(rig.Puppet.LastCollisionResponse.LayerRuleIndex, Is.EqualTo(-1));
            Assert.That(rig.Puppet.LastCollisionResponse.LayerResistanceMultiplier,
                Is.EqualTo(1f));

            long acceptedBefore = rig.Puppet.AcceptedCollisionCount;
            rule.collisionThreshold = 0f;
            rig.Puppet.SetCollisionResistanceMultipliers(new[] { rule });
            rig.ClearProjectiles();
            yield return new WaitForFixedUpdate();
            rig.Shoot(rig.RootBody.position, 8, 10f);
            for (int step = 0;
                step < 40 && rig.Puppet.AcceptedCollisionCount == acceptedBefore;
                step++) yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.AcceptedCollisionCount,
                Is.GreaterThan(acceptedBefore));
            Assert.That(rig.Puppet.LastCollisionResponse.LayerRuleIndex, Is.Zero);
            Assert.That(rig.Puppet.LastCollisionResponse.LayerResistanceMultiplier,
                Is.EqualTo(5f));
        }

        [UnityTest]
        public IEnumerator D16_MaxCollisionsBudgetsDamageButObservesEveryContact()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Puppet.MaximumCollisionsPerFixedStep = 1;
            var observedByStep = new Dictionary<float, int>();
            var acceptedByStep = new Dictionary<float, int>();
            rig.Puppet.CollisionObserved += value => Increment(observedByStep, value.FixedTime);
            rig.Puppet.CollisionAccepted += value => Increment(acceptedByStep, value.FixedTime);

            rig.Shoot(rig.RootBody.position, 8, 12f);
            rig.Shoot(rig.ChildBody.position, 9, 12f);
            for (int step = 0; step < 30; step++) yield return new WaitForFixedUpdate();

            bool sharedStepObserved = false;
            foreach (KeyValuePair<float, int> pair in observedByStep)
            {
                if (pair.Value < 2) continue;
                sharedStepObserved = true;
                int accepted;
                acceptedByStep.TryGetValue(pair.Key, out accepted);
                Assert.That(accepted, Is.LessThanOrEqualTo(1));
            }
            Assert.That(sharedStepObserved, Is.True,
                "The fixture must produce at least two real contacts in one physics step.");
            Assert.That(rig.Puppet.CollisionStep.RejectedBudgetCount,
                Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator D17_MuscleGroupsRegainPinAtIndependentFiniteRates()
        {
            RagdollMuscleBehaviourSettings hips = RagdollMuscleBehaviourSettings.Default;
            hips.regainPositionAuthorityMultiplier = 1f;
            RagdollMuscleBehaviourSettings spine = RagdollMuscleBehaviourSettings.Default;
            spine.regainPositionAuthorityMultiplier = 0.25f;
            rig = new PuppetPhysicsRig(
                RagdollPuppetNormalMode.Active, hips, spine);
            yield return rig.Initialize();
            rig.Muscles.PositionSuppressionRecoveryRate = 1f;
            rig.Puppet.RegainPinSpeed = 1f;
            rig.Muscles.AccumulateSuppression(rig.RootHandle, 1f, 0f);
            rig.Muscles.AccumulateSuppression(rig.ChildHandle, 1f, 0f);

            for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
            float root = rig.Muscles.GetState(rig.RootHandle).PositionSuppression;
            float child = rig.Muscles.GetState(rig.ChildHandle).PositionSuppression;
            Assert.That(root, Is.LessThan(child));
            Assert.That(1f - root, Is.EqualTo((1f - child) * 4f).Within(0.03f));

            rig.Puppet.RegainPinSpeed = float.NaN;
            Assert.That(rig.Puppet.RegainPinSpeed, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator D27_PinWeightThresholdIsStrictAndSanitizesNonFiniteValues()
        {
            rig = new PuppetPhysicsRig();
            yield return rig.Initialize();
            rig.Animator.MasterPinWeight = 0.5f;
            rig.Animator.MasterMappingWeight = 0f;
            rig.Puppet.PinWeightThreshold = 0.5f;
            rig.RootBody.transform.position =
                rig.RootTarget.position + Vector3.right * 1.1f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Puppet),
                "Official BehaviourPuppet wording is strictly 'less than'.");

            rig.Puppet.PinWeightThreshold = 0.5001f;
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));

            rig.Puppet.PinWeightThreshold = float.NaN;
            Assert.That(rig.Puppet.PinWeightThreshold, Is.Zero);
            rig.Puppet.PinWeightThreshold = float.PositiveInfinity;
            Assert.That(rig.Puppet.PinWeightThreshold, Is.Zero);
        }

        static void Increment(Dictionary<float, int> values, float key)
        {
            int count;
            values.TryGetValue(key, out count);
            values[key] = count + 1;
        }
    }

    internal sealed class PuppetPhysicsRig : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<GameObject> projectiles = new List<GameObject>();
        readonly bool ignoredBefore;
        readonly RagdollDefinition definition;
        readonly RagdollAnimationProfile animationProfile;
        readonly RagdollMuscleProfile muscleProfile;
        long acceptedBaseline;

        internal RagdollSetupResult Result { get; }
        internal RagdollPuppetBehaviour Puppet => Result.PuppetBehaviour;
        internal RagdollAnimator Animator => Result.Animator;
        internal RagdollMuscleController Muscles => Result.Muscles;
        internal RagdollSimulationModeController Simulation => Result.Simulation;
        internal Rigidbody RootBody { get; }
        internal Rigidbody ChildBody { get; }
        internal Transform RootTarget { get; }
        internal Transform ChildTarget { get; }
        internal RagdollBoneHandle RootHandle { get; }
        internal RagdollBoneHandle ChildHandle { get; }

        internal PuppetPhysicsRig(
            RagdollPuppetNormalMode mode = RagdollPuppetNormalMode.Active,
            RagdollMuscleBehaviourSettings? hipsSettings = null,
            RagdollMuscleBehaviourSettings? spineSettings = null)
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");

            GameObject puppetObject = Own(new GameObject("D Puppet"));
            puppetObject.SetActive(false);
            GameObject childObject = Own(new GameObject("Child"));
            childObject.transform.SetParent(puppetObject.transform, false);
            childObject.transform.localPosition = Vector3.up * 2f;
            RootBody = puppetObject.AddComponent<Rigidbody>();
            RootBody.useGravity = false;
            RootBody.constraints = RigidbodyConstraints.FreezeAll;
            ConfigurableJoint rootJoint = puppetObject.AddComponent<ConfigurableJoint>();
            BoxCollider rootCollider = puppetObject.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one;
            ChildBody = childObject.AddComponent<Rigidbody>();
            ChildBody.useGravity = false;
            ChildBody.constraints = RigidbodyConstraints.FreezeAll;
            ConfigurableJoint childJoint = childObject.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = RootBody;
            BoxCollider childCollider = childObject.AddComponent<BoxCollider>();
            childCollider.size = Vector3.one;

            definition = Own(ScriptableObject.CreateInstance<RagdollDefinition>());
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            RagdollDefinitionBindings bindings =
                puppetObject.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            puppetObject.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);
            RootHandle = bindings.GetHandleAt(0);
            ChildHandle = bindings.GetHandleAt(1);

            // ConfigureSeparated uses the documented legacy name migration when no
            // explicit semantic target table is supplied. The root participates in
            // that migration just like every other registered muscle.
            GameObject targetObject = Own(new GameObject("D Puppet"));
            RootTarget = targetObject.transform;
            GameObject targetChild = Own(new GameObject("Child"));
            targetChild.transform.SetParent(targetObject.transform, false);
            targetChild.transform.localPosition = Vector3.up * 2f;
            ChildTarget = targetChild.transform;

            muscleProfile = Own(ScriptableObject.CreateInstance<RagdollMuscleProfile>());
            ConfigureMuscleProfile(
                muscleProfile,
                definition,
                rootName,
                childName,
                hipsSettings ?? RagdollMuscleBehaviourSettings.Default,
                spineSettings ?? RagdollMuscleBehaviourSettings.Default);
            animationProfile = Own(
                ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            Result = RagdollRuntimeSetupService.ConfigureSeparated(
                targetObject.transform,
                bindings,
                animationProfile,
                30,
                31,
                new PuppetPhysicsFactory(mode, muscleProfile));
            Assert.That(Result.Succeeded, Is.True, Result.Error);
        }

        internal IEnumerator Initialize()
        {
            yield return null;
            Assert.That(Result.Animator.Initiated, Is.True);
            Assert.That(Puppet.IsInitialized, Is.True);
            Result.Animator.FixTargetTransforms = false;
            Puppet.CanGetUp = false;
            Puppet.LoseBalanceOnTargetDrift = true;
            Puppet.CollisionLayers = -1;
            Puppet.CollisionThreshold = 0f;
            Puppet.MaximumCollisionsPerFixedStep = 30;
            Puppet.CollisionResistance.constantResistance = 0.5f;
            Puppet.SetNormalMode(Puppet.NormalMode, true);
            if (Puppet.NormalMode == RagdollPuppetNormalMode.Kinematic)
            {
                for (int step = 0;
                    step < 30 && (Simulation.CurrentMode
                        != RagdollSimulationMode.Kinematic
                        || Simulation.IsTransitioning);
                    step++)
                {
                    yield return new WaitForFixedUpdate();
                }
            }
            yield return new WaitForFixedUpdate();
            acceptedBaseline = Puppet.AcceptedCollisionCount;
        }

        internal GameObject Shoot(Vector3 target, int layer, float speed)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "D physical collision projectile";
            projectile.layer = layer;
            projectile.transform.position = target + Vector3.left * 2f;
            projectile.transform.localScale = Vector3.one * 0.35f;
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.right * speed;
            projectiles.Add(projectile);
            return projectile;
        }

        internal IEnumerator WaitForAcceptedCollision()
        {
            acceptedBaseline = Puppet.AcceptedCollisionCount;
            for (int step = 0;
                step < 50 && Puppet.AcceptedCollisionCount == acceptedBaseline;
                step++) yield return new WaitForFixedUpdate();
            Assert.That(Puppet.AcceptedCollisionCount,
                Is.GreaterThan(acceptedBaseline),
                "No accepted PhysX collision reached BehaviourPuppet.");
            acceptedBaseline = Puppet.AcceptedCollisionCount;
        }

        internal IEnumerator WaitForNewAcceptedCollision()
        {
            long before = acceptedBaseline;
            for (int step = 0;
                step < 50 && Puppet.AcceptedCollisionCount == before;
                step++) yield return new WaitForFixedUpdate();
            Assert.That(Puppet.AcceptedCollisionCount, Is.GreaterThan(before));
            acceptedBaseline = Puppet.AcceptedCollisionCount;
        }

        internal void DispatchStaticCollision()
        {
            MethodInfo method = typeof(RagdollPuppetBehaviour).GetMethod(
                "OnBehaviourCollision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(Puppet, new object[]
            {
                new RagdollCollisionEvent(
                    RootHandle,
                    RagdollCollisionPhase.Enter,
                    null,
                    Time.fixedTime,
                    1L)
            });
        }

        internal void ClearProjectiles()
        {
            for (int index = 0; index < projectiles.Count; index++)
            {
                if (projectiles[index])
                    UnityEngine.Object.DestroyImmediate(projectiles[index]);
            }
            projectiles.Clear();
            acceptedBaseline = Puppet.AcceptedCollisionCount;
        }

        public void Dispose()
        {
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            ClearProjectiles();
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index]) UnityEngine.Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        static void ConfigureMuscleProfile(
            RagdollMuscleProfile profile,
            RagdollDefinition definition,
            BoneName root,
            BoneName child,
            RagdollMuscleBehaviourSettings hips,
            RagdollMuscleBehaviourSettings spine)
        {
            var hipsOverride = new RagdollMuscleGroupOverride();
            SetField(hipsOverride, "group", RagdollMuscleGroup.Hips);
            SetField(hipsOverride, "settings", hips);
            var spineOverride = new RagdollMuscleGroupOverride();
            SetField(spineOverride, "group", RagdollMuscleGroup.Spine);
            SetField(spineOverride, "settings", spine);
            SetField(profile, "definition", definition);
            SetField(profile, "boneGroups", new[]
            {
                CreateAssignment(root, RagdollMuscleGroup.Hips),
                CreateAssignment(child, RagdollMuscleGroup.Spine)
            });
            SetField(profile, "groupOverrides", new[]
            {
                hipsOverride,
                spineOverride
            });
        }

        static RagdollMuscleGroupAssignment CreateAssignment(
            BoneName bone,
            RagdollMuscleGroup group)
        {
            var value = new RagdollMuscleGroupAssignment();
            SetField(value, "bone", bone);
            SetField(value, "group", group);
            return value;
        }

        static object CreateBindings(
            BoneName root,
            ConfigurableJoint rootJoint,
            BoneName child,
            ConfigurableJoint childJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary",
                BindingFlags.NonPublic);
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
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }

    internal sealed class PuppetPhysicsFactory :
        RagdollRuntimeSetupService.IObjectFactory
    {
        readonly RagdollPuppetNormalMode mode;
        readonly RagdollMuscleProfile muscleProfile;

        internal PuppetPhysicsFactory(
            RagdollPuppetNormalMode mode,
            RagdollMuscleProfile muscleProfile)
        {
            this.mode = mode;
            this.muscleProfile = muscleProfile;
        }

        public T AddComponent<T>(GameObject owner) where T : Component
        {
            T component = owner.AddComponent<T>();
            var muscles = component as RagdollMuscleController;
            if (muscles)
            {
                FieldInfo field = typeof(RagdollMuscleController).GetField(
                    "muscleProfile",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(muscles, muscleProfile);
            }
            var puppet = component as RagdollPuppetBehaviour;
            if (puppet)
            {
                puppet.NormalMode = mode;
                puppet.CanGetUp = false;
            }
            return component;
        }

        public GameObject CreateGameObject(string name)
        {
            return new GameObject(name);
        }

        public void Destroy(UnityEngine.Object value)
        {
            if (value) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
