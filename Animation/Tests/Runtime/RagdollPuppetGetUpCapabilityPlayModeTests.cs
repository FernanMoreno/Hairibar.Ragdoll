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
    /// Direct physical coverage for the BehaviourPuppet GetUp contract. Every test
    /// initializes a real RagdollAnimator, muscle registry, behaviour controller,
    /// collision hub, ConfigurableJoints and dynamic Rigidbodies.
    /// </summary>
    public sealed class RagdollPuppetGetUpCapabilityPlayModeTests
    {
        GetUpPhysicalRig rig;
        Vector3 originalGravity;
        SimulationMode originalSimulationMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalGravity = Physics.gravity;
            originalSimulationMode = Physics.simulationMode;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            rig = new GetUpPhysicalRig();
            yield return null;
            rig.Result.Animator.FixTargetTransforms = false;
            rig.Result.Simulation.SetModeImmediate(RagdollSimulationMode.Active);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Result.PuppetBehaviour.IsInitialized, Is.True);
            Assert.That(rig.Result.PuppetBehaviour.IsActive, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics.simulationMode = originalSimulationMode;
            Physics.gravity = originalGravity;
            if (rig != null) rig.Dispose();
            rig = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator D31_AutomaticGetUpRequiresTheConfiguredCanGetUpGate()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.GetUpDelay = 0f;
            puppet.MaxGetUpVelocity = 10f;
            puppet.CanGetUp = false;
            puppet.State = RagdollPuppetState.Unpinned;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));

            puppet.CanGetUp = true;
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));
            Assert.That(puppet.GetUpOrientation,
                Is.Not.EqualTo(RagdollGetUpOrientation.Unknown));
        }

        [UnityTest]
        public IEnumerator D32_GetUpDelayIsMeasuredFromEnteringUnpinned()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.GetUpDelay = Time.fixedDeltaTime * 4f;
            puppet.MaxGetUpVelocity = 10f;
            puppet.State = RagdollPuppetState.Unpinned;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.TryBeginGetUp(), Is.False);
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.TryBeginGetUp(), Is.True);
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));
        }

        [UnityTest]
        public IEnumerator D33_BlendToAnimationTimeDrivesTheLiveMappingModifier()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.BlendToAnimationTime = Time.fixedDeltaTime * 5f;
            puppet.MinGetUpDuration = 1f;
            rig.BeginGetUp(RagdollGetUpOrientation.Prone);

            RagdollMappingWeights initial = new RagdollMappingWeights(0.25f, 0.25f);
            rig.Result.Behaviours.ModifyMapping(ref initial, rig.RootPair);
            Assert.That(initial.PositionWeight, Is.GreaterThan(0.25f));
            Assert.That(puppet.GetUpProgress, Is.LessThan(1f));

            for (int step = 0; step < 6; step++)
                yield return new WaitForFixedUpdate();

            RagdollMappingWeights completed =
                new RagdollMappingWeights(0.25f, 0.25f);
            rig.Result.Behaviours.ModifyMapping(ref completed, rig.RootPair);
            Assert.That(puppet.GetUpProgress, Is.EqualTo(1f).Within(0.001f));
            Assert.That(completed.PositionWeight, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(completed.RotationWeight, Is.EqualTo(0.25f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator D34_MaxGetUpVelocityUsesTheDocumentedStrictComparison()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.GetUpDelay = 0f;
            puppet.MaxGetUpVelocity = 2f;
            puppet.State = RagdollPuppetState.Unpinned;

            rig.RootBody.constraints = RigidbodyConstraints.None;
            rig.RootBody.linearVelocity = Vector3.right * 2f;
            Assert.That(puppet.TryBeginGetUp(), Is.False,
                "RootMotion documents velocity as less than maxGetUpVelocity.");

            rig.RootBody.linearVelocity = Vector3.right * 1.99f;
            Assert.That(puppet.TryBeginGetUp(), Is.True);
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));
            yield return null;
        }

        [UnityTest]
        public IEnumerator D35_MinGetUpDurationIsIndependentFromBlendDuration()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.BlendToAnimationTime = 0f;
            puppet.MinGetUpDuration = Time.fixedDeltaTime * 5f;
            rig.BeginGetUp(RagdollGetUpOrientation.Supine);

            for (int step = 0; step < 4; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(puppet.GetUpProgress, Is.EqualTo(1f));
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));

            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
        }

        [UnityTest]
        public IEnumerator D36_GetUpCollisionResistanceMultipliesARealPhysXImpact()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.MinGetUpDuration = 10f;
            puppet.GetUpCollisionResistanceMlp = 4f;
            puppet.CollisionLayers = -1;
            puppet.CollisionThreshold = 0f;
            rig.BeginGetUp(RagdollGetUpOrientation.Prone);
            rig.CreateProjectile(new Vector3(-2f, 0f, 0f), Vector3.right * 20f);

            for (int step = 0;
                step < 30 && !puppet.LastCollisionResponse.HasResponse;
                step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(puppet.LastCollisionResponse.HasResponse, Is.True,
                "No real collision reached BehaviourPuppet.");
            Assert.That(puppet.LastCollisionResponse.StateResistanceMultiplier,
                Is.EqualTo(4f).Within(0.001f));
            Assert.That(puppet.LastCollisionResponse.EffectiveResistance,
                Is.EqualTo(
                    puppet.LastCollisionResponse.GlobalResistance
                    * puppet.LastCollisionResponse.LayerResistanceMultiplier
                    * puppet.LastCollisionResponse.MuscleResistanceMultiplier
                    * 4f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator D37_GetUpRegainPinMultiplierIsAppliedAndRestored()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.RegainPinSpeed = 1.5f;
            puppet.GetUpRegainPinSpeedMlp = 3f;
            puppet.MinGetUpDuration = Time.fixedDeltaTime * 2f;
            rig.BeginGetUp(RagdollGetUpOrientation.Supine);

            Assert.That(puppet.AppliedRegainPinSpeed,
                Is.EqualTo(4.5f).Within(0.001f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
            Assert.That(puppet.AppliedRegainPinSpeed,
                Is.EqualTo(1.5f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator D38_GetUpKnockOutDistanceMultiplierUsesLiveTargetDrift()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.CanMoveTarget = false;
            puppet.LoseBalanceOnTargetDrift = true;
            puppet.PinWeightThreshold = 1f;
            puppet.GetUpKnockOutDistanceMlp = 3f;
            puppet.MinGetUpDuration = 10f;
            rig.Result.Animator.MasterPinWeight = 0f;
            rig.Result.Animator.MasterMuscleWeight = 0f;
            rig.BeginGetUp(RagdollGetUpOrientation.Prone);

            rig.Target.position = rig.RootBody.position + Vector3.right * 2.5f;
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));

            rig.Target.position = rig.RootBody.position + Vector3.right * 4f;
            yield return new WaitForFixedUpdate();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Unpinned));
            Assert.That(puppet.LastKnockOutThreshold, Is.EqualTo(3f).Within(0.001f));
            Assert.That(puppet.LastKnockOutDistance,
                Is.GreaterThan(puppet.LastKnockOutThreshold));
        }

        [UnityTest]
        public IEnumerator D39_ProneOffsetAlignsTargetToPhysicalHipAndEffectiveUp()
        {
            Physics.gravity = new Vector3(0f, -6f, -8f);
            Vector3 effectiveUp = -Physics.gravity.normalized;
            yield return new WaitForFixedUpdate();
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.CanMoveTarget = true;
            puppet.GetUpOffsetProne = new Vector3(0.2f, 0.3f, 0.4f);
            puppet.State = RagdollPuppetState.Unpinned;
            Assert.That(puppet.BeginGetUpImmediately(
                RagdollGetUpOrientation.Prone), Is.True);

            Assert.That(puppet.TargetAlignmentPending, Is.True);
            puppet.ModifyTargetPoseInternal(
                rig.Result.Behaviours.Context.Pairs);

            Vector3 expected = rig.RootBody.position
                + rig.Target.rotation * puppet.GetUpOffsetProne;
            Assert.That(Vector3.Distance(rig.Target.position, expected),
                Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(rig.Target.up, effectiveUp),
                Is.GreaterThan(0.999f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator D40_QuadrupedSidesSelectExactlyOneMatchingGetUpEvent()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.QuadrupedGetUp = true;
            puppet.MinimumOrientationDot = 0.1f;
            int prone = 0;
            int supine = 0;
            puppet.OnGetUpProne = CreateEvent(() => prone++);
            puppet.OnGetUpSupine = CreateEvent(() => supine++);

            rig.RootBody.transform.rotation =
                Quaternion.AngleAxis(-90f, Vector3.forward);
            Physics.SyncTransforms();
            puppet.State = RagdollPuppetState.Unpinned;
            Assert.That(puppet.BeginGetUpImmediately(), Is.True);
            Assert.That(puppet.GetUpOrientation,
                Is.EqualTo(RagdollGetUpOrientation.Prone));
            Assert.That(prone, Is.EqualTo(1));
            Assert.That(supine, Is.Zero);

            Assert.That(puppet.InterruptGetUp(), Is.True);
            rig.RootBody.transform.rotation =
                Quaternion.AngleAxis(90f, Vector3.forward);
            Physics.SyncTransforms();
            Assert.That(puppet.BeginGetUpImmediately(), Is.True);
            Assert.That(puppet.GetUpOrientation,
                Is.EqualTo(RagdollGetUpOrientation.Supine));
            Assert.That(prone, Is.EqualTo(1));
            Assert.That(supine, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator D41_BalanceEventsFireOnceForLossAndCompletedRecovery()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.MinGetUpDuration = Time.fixedDeltaTime;
            int lose = 0;
            int loseFromPuppet = 0;
            int regain = 0;
            puppet.OnLoseBalance = CreateEvent(() => lose++);
            puppet.OnLoseBalanceFromPuppet = CreateEvent(() => loseFromPuppet++);
            puppet.OnRegainBalance = CreateEvent(() => regain++);

            Assert.That(puppet.LoseBalance(), Is.True);
            Assert.That(puppet.LoseBalance(), Is.False);
            rig.BeginGetUp(RagdollGetUpOrientation.Prone, false);
            yield return new WaitForFixedUpdate();

            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.Puppet));
            Assert.That(lose, Is.EqualTo(1));
            Assert.That(loseFromPuppet, Is.EqualTo(1));
            Assert.That(regain, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator D42_CanMoveTargetControlsOnlyThePhysicalTargetRootOwnership()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            Vector3 authoredRoot = rig.Target.position;

            puppet.CanMoveTarget = false;
            puppet.State = RagdollPuppetState.Unpinned;
            RagdollMappingWeights blockedRoot = new RagdollMappingWeights(1f, 1f);
            rig.Result.Behaviours.ModifyMapping(ref blockedRoot, rig.RootPair);
            RagdollMappingWeights mappedChild = new RagdollMappingWeights(1f, 1f);
            rig.Result.Behaviours.ModifyMapping(ref mappedChild, rig.ChildPair);
            Assert.That(blockedRoot.PositionWeight, Is.Zero);
            Assert.That(blockedRoot.RotationWeight, Is.Zero);
            Assert.That(mappedChild.PositionWeight, Is.GreaterThan(0f));

            rig.RootBody.transform.position = authoredRoot + Vector3.right * 3f;
            Physics.SyncTransforms();
            Assert.That(puppet.BeginGetUpImmediately(
                RagdollGetUpOrientation.Prone), Is.True);
            Assert.That(puppet.TargetAlignmentPending, Is.True);
            puppet.ModifyTargetPoseInternal(
                rig.Result.Behaviours.Context.Pairs);
            Assert.That(rig.Target.position, Is.EqualTo(authoredRoot));

            puppet.State = RagdollPuppetState.Puppet;
            puppet.State = RagdollPuppetState.Unpinned;
            puppet.CanMoveTarget = true;
            Assert.That(puppet.BeginGetUpImmediately(
                RagdollGetUpOrientation.Prone), Is.True);
            puppet.ModifyTargetPoseInternal(
                rig.Result.Behaviours.Context.Pairs);
            Assert.That(Vector3.Distance(rig.Target.position, authoredRoot),
                Is.GreaterThan(2f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator D43_TeleportPreservesGetUpFromEverySimulationBoundary()
        {
            RagdollPuppetBehaviour puppet = rig.Puppet;
            puppet.CanGetUp = false;
            puppet.MinGetUpDuration = 20f;
            RagdollTeleportPhaseDriver driver =
                rig.Target.gameObject.AddComponent<RagdollTeleportPhaseDriver>();

            rig.BeginGetUp(RagdollGetUpOrientation.Prone);
            Vector3 updateDestination = new Vector3(3f, 1f, 0f);
            driver.Schedule(RagdollTeleportTestPhase.Update, () =>
                rig.Result.Animator.Teleport(
                    updateDestination,
                    Quaternion.Euler(0f, 20f, 0f),
                    true));
            yield return WaitForTeleport(driver, updateDestination);
            AssertValidTeleportedGetUp(puppet, updateDestination);

            rig.RestartGetUp();
            Vector3 fixedDestination = new Vector3(6f, 1f, 0f);
            driver.Schedule(RagdollTeleportTestPhase.FixedUpdate, () =>
                rig.Result.Animator.Teleport(
                    fixedDestination,
                    Quaternion.Euler(0f, 40f, 0f),
                    true));
            yield return WaitForTeleport(driver, fixedDestination);
            AssertValidTeleportedGetUp(puppet, fixedDestination);

            rig.RestartGetUp();
            Vector3 lateDestination = new Vector3(9f, 1f, 0f);
            driver.Schedule(RagdollTeleportTestPhase.LateUpdate, () =>
                rig.Result.Animator.Teleport(
                    lateDestination,
                    Quaternion.Euler(0f, 60f, 0f),
                    true));
            yield return WaitForTeleport(driver, lateDestination);
            AssertValidTeleportedGetUp(puppet, lateDestination);

            rig.RestartGetUp();
            Vector3 manualDestination = new Vector3(12f, 1f, 0f);
            rig.Result.Animator.enabled = false;
            Physics.simulationMode = SimulationMode.Script;
            rig.Result.Animator.Teleport(
                manualDestination,
                Quaternion.Euler(0f, 80f, 0f),
                true);
            rig.Result.Animator.PrepareManualSimulation(Time.fixedDeltaTime);
            AssertValidTeleportedGetUp(puppet, manualDestination);
            Physics.Simulate(Time.fixedDeltaTime);
            rig.Result.Animator.CompleteManualSimulation();
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp),
                "CompleteManualSimulation changed the active GetUp state.");
            rig.Result.Animator.enabled = true;
            Physics.simulationMode = originalSimulationMode;
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));
        }

        IEnumerator WaitForTeleport(
            RagdollTeleportPhaseDriver driver,
            Vector3 destination)
        {
            for (int frame = 0; frame < 10; frame++)
            {
                if (driver.Invoked
                    && Vector3.Distance(rig.Target.position, destination) < 0.001f)
                {
                    yield break;
                }
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("Teleport did not commit from " + driver.Phase + ".");
        }

        void AssertValidTeleportedGetUp(
            RagdollPuppetBehaviour puppet,
            Vector3 destination)
        {
            Assert.That(Vector3.Distance(rig.Target.position, destination),
                Is.LessThan(0.001f));
            Assert.That(puppet.State, Is.EqualTo(RagdollPuppetState.GetUp));
            Assert.That(puppet.TargetAlignmentPending, Is.False);
            Assert.That(puppet.GetUpBlendCompletedByTeleport, Is.True);
            Assert.That(puppet.GetUpProgress, Is.EqualTo(1f));
        }

        static RagdollPuppetEvent CreateEvent(
            UnityEngine.Events.UnityAction listener)
        {
            RagdollPuppetEvent result = new RagdollPuppetEvent();
            result.UnityEvent.AddListener(listener);
            return result;
        }
    }

    internal sealed class GetUpPhysicalRig : IDisposable
    {
        readonly GameObject puppetRoot;
        readonly RagdollDefinition definition;
        readonly RagdollAnimationProfile profile;
        readonly List<GameObject> projectiles = new List<GameObject>();
        readonly bool ignoredBefore;

        internal RagdollSetupResult Result { get; }
        internal RagdollPuppetBehaviour Puppet => Result.PuppetBehaviour;
        internal Transform Target => Result.Target;
        internal Rigidbody RootBody { get; }
        internal RagdollAnimator.AnimatedPair RootPair =>
            Result.Behaviours.Context.Pairs[0];
        internal RagdollAnimator.AnimatedPair ChildPair =>
            Result.Behaviours.Context.Pairs[1];

        internal GetUpPhysicalRig()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(26, 27);
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");

            puppetRoot = new GameObject("GetUp Puppet");
            puppetRoot.SetActive(false);
            GameObject child = new GameObject("Child");
            child.transform.SetParent(puppetRoot.transform, false);
            child.transform.localPosition = Vector3.up;

            RootBody = puppetRoot.AddComponent<Rigidbody>();
            RootBody.useGravity = false;
            RootBody.constraints = RigidbodyConstraints.FreezeAll;
            RootBody.mass = 2f;
            ConfigurableJoint rootJoint =
                puppetRoot.AddComponent<ConfigurableJoint>();
            BoxCollider rootCollider = puppetRoot.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one * 0.75f;

            Rigidbody childBody = child.AddComponent<Rigidbody>();
            childBody.useGravity = false;
            childBody.constraints = RigidbodyConstraints.FreezeAll;
            childBody.mass = 1f;
            ConfigurableJoint childJoint = child.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = RootBody;
            BoxCollider childCollider = child.AddComponent<BoxCollider>();
            childCollider.size = Vector3.one * 0.5f;

            definition = ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            RagdollDefinitionBindings bindings =
                puppetRoot.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            puppetRoot.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            GameObject target = new GameObject("GetUp Puppet");
            GameObject targetChild = new GameObject("Child");
            targetChild.transform.SetParent(target.transform, false);
            targetChild.transform.localPosition = Vector3.up;
            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            Result = RagdollRuntimeSetupService.ConfigureSeparated(
                target.transform,
                bindings,
                profile,
                26,
                27);
            Assert.That(Result.Succeeded, Is.True, Result.Error);
            Result.PuppetBehaviour.CanGetUp = false;
            Result.PuppetBehaviour.LoseBalanceOnTargetDrift = false;
        }

        internal void BeginGetUp(
            RagdollGetUpOrientation orientation,
            bool enterUnpinned = true)
        {
            if (enterUnpinned)
                Puppet.State = RagdollPuppetState.Unpinned;
            Assert.That(Puppet.BeginGetUpImmediately(orientation), Is.True);
            Result.Behaviours.ModifyPose(Result.Behaviours.Context.Pairs);
            Assert.That(Puppet.TargetAlignmentPending, Is.False);
        }

        internal void RestartGetUp()
        {
            if (Puppet.State != RagdollPuppetState.Puppet)
                Puppet.State = RagdollPuppetState.Puppet;
            BeginGetUp(RagdollGetUpOrientation.Prone);
        }

        internal void CreateProjectile(Vector3 position, Vector3 velocity)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "GetUp PhysX projectile";
            projectile.transform.position = position;
            projectile.transform.localScale = Vector3.one * 0.35f;
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            projectiles.Add(projectile);
        }

        public void Dispose()
        {
            Physics.IgnoreLayerCollision(26, 27, ignoredBefore);
            for (int index = 0; index < projectiles.Count; index++)
            {
                if (projectiles[index])
                    UnityEngine.Object.DestroyImmediate(projectiles[index]);
            }
            if (Result != null && Result.Target)
                UnityEngine.Object.DestroyImmediate(Result.Target.gameObject);
            if (puppetRoot) UnityEngine.Object.DestroyImmediate(puppetRoot);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
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

        static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(owner, value);
        }
    }

    internal enum RagdollTeleportTestPhase
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    [DefaultExecutionOrder(10000)]
    internal sealed class RagdollTeleportPhaseDriver : MonoBehaviour
    {
        Action pending;

        internal bool Invoked { get; private set; }
        internal RagdollTeleportTestPhase Phase { get; private set; }

        internal void Schedule(RagdollTeleportTestPhase phase, Action action)
        {
            Phase = phase;
            pending = action ?? throw new ArgumentNullException(nameof(action));
            Invoked = false;
        }

        void Update()
        {
            TryInvoke(RagdollTeleportTestPhase.Update);
        }

        void FixedUpdate()
        {
            TryInvoke(RagdollTeleportTestPhase.FixedUpdate);
        }

        void LateUpdate()
        {
            TryInvoke(RagdollTeleportTestPhase.LateUpdate);
        }

        void TryInvoke(RagdollTeleportTestPhase phase)
        {
            if (pending == null || phase != Phase) return;
            Action action = pending;
            pending = null;
            Invoked = true;
            action();
        }
    }
}
