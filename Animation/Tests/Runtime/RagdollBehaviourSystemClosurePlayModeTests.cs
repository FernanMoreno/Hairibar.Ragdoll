using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollBehaviourSystemClosurePlayModeTests
    {
        BehaviourSystemRig rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (rig != null) rig.Dispose();
            rig = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator C01_BehaviourReceivesOneCompleteOrderedLifecycle()
        {
            rig = new BehaviourSystemRig(BehaviourFixtureMode.Lifecycle);
            yield return null;

            LifecycleBehaviourProbe probe = rig.Factory.Lifecycle;
            RagdollBehaviourController controller = rig.Result.Behaviours;
            Assert.That(probe.IsInitialized, Is.True);
            Assert.That(probe.IsActive, Is.True);
            Assert.That(probe.Count("initialize"), Is.EqualTo(1));
            Assert.That(probe.Count("post-initialize"), Is.EqualTo(1));

            controller.Initialize(controller.Context.Pairs);
            controller.NotifyFixTransforms();
            controller.NotifyRead();
            controller.ModifyPose(controller.Context.Pairs);
            controller.NotifyWrite();
            Assert.That(probe.Deactivate(), Is.True);

            AssertOrdered(
                probe.Trace,
                "initialize",
                "post-initialize",
                "activate",
                "fix",
                "read",
                "fixed",
                "pose",
                "write",
                "deactivate");
            Assert.That(probe.Count("initialize"), Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(controller);
            Assert.That(probe.Trace, Does.Contain("shutdown"));
            Assert.That(probe.Count("shutdown"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator C02_SwitchIsExclusiveNonReentrantAndRollsBackFailure()
        {
            rig = new BehaviourSystemRig(BehaviourFixtureMode.Switching);
            yield return null;

            SwitchingBehaviourProbe first = rig.Factory.FirstSwitch;
            SwitchingBehaviourProbe second = rig.Factory.SecondSwitch;
            SwitchingBehaviourProbe failing = rig.Factory.FailingSwitch;
            RagdollBehaviourController controller = rig.Result.Behaviours;
            second.ReentrantTarget = first;

            int successfulListenerCalls = 0;
            controller.ActiveBehaviourChanged += (previous, current) =>
                throw new InvalidOperationException("synthetic switch listener failure");
            controller.ActiveBehaviourChanged += (previous, current) =>
                successfulListenerCalls++;
            LogAssert.Expect(
                LogType.Exception,
                new Regex("synthetic switch listener failure"));

            Assert.That(second.Activate(), Is.True);
            Assert.That(second.ReentrantRejected, Is.True);
            Assert.That(controller.ActiveBehaviour, Is.SameAs(second));
            AssertExactlyOneSelected(controller, second);
            Assert.That(successfulListenerCalls, Is.EqualTo(1));

            failing.ThrowOnActivation = true;
            Assert.Throws<InvalidOperationException>(() => failing.Activate());
            Assert.That(controller.ActiveBehaviour, Is.SameAs(second));
            AssertExactlyOneSelected(controller, second);
            Assert.That(second.ActivationCount, Is.EqualTo(2),
                "Rollback must reactivate the previous behaviour exactly once.");
        }

        [UnityTest]
        public IEnumerator C03_InjectedContextContainsExactRuntimeDependenciesAndPairs()
        {
            rig = new BehaviourSystemRig(BehaviourFixtureMode.Context);
            yield return null;

            ContextBehaviourProbe probe = rig.Factory.Context;
            RagdollBehaviourContext context = rig.Result.Behaviours.Context;
            Assert.That(probe.CapturedController, Is.SameAs(rig.Result.Behaviours));
            Assert.That(probe.CapturedAnimator, Is.SameAs(rig.Result.Animator));
            Assert.That(probe.CapturedMuscles, Is.SameAs(rig.Result.Muscles));
            Assert.That(probe.CapturedHub, Is.SameAs(rig.Result.Collisions));
            Assert.That(probe.CapturedBindings,
                Is.SameAs(rig.Result.Animator.Bindings));
            Assert.That(probe.CapturedPairs.Count, Is.EqualTo(2));
            Assert.That(probe.CapturedPairs, Is.SameAs(context.Pairs));

            for (int index = 0; index < context.Pairs.Count; index++)
            {
                RagdollAnimator.AnimatedPair pair = context.Pairs[index];
                Assert.That(context.Topology.Contains(pair.Handle), Is.True);
                Assert.That(context.GetPair(pair.Handle), Is.SameAs(pair));
                Assert.That(pair.TargetBone, Is.Not.Null);
                Assert.That(pair.RagdollBone.Rigidbody, Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator C05_SubBehavioursAreReusableAndOneFailureIsIsolated()
        {
            rig = new BehaviourSystemRig(BehaviourFixtureMode.SubBehaviours);
            yield return null;

            SubBehaviourOwnerProbe first = rig.Factory.FirstSubOwner;
            SubBehaviourOwnerProbe second = rig.Factory.SecondSubOwner;
            Assert.That(first.Failing.Owner, Is.SameAs(first));
            Assert.That(first.Survivor.Owner, Is.SameAs(first));
            Assert.That(second.Failing.Owner, Is.SameAs(second));
            Assert.That(second.Survivor.Owner, Is.SameAs(second));
            Assert.That(first.Failing, Is.Not.SameAs(second.Failing));

            first.Failing.ThrowOnFixedUpdate = true;
            LogAssert.Expect(
                LogType.Exception,
                new Regex("synthetic sub-behaviour failure"));
            rig.Result.Behaviours.ModifyPose(
                rig.Result.Behaviours.Context.Pairs);

            Assert.That(first.Failing.FixedUpdateCount, Is.EqualTo(1));
            Assert.That(first.Survivor.FixedUpdateCount, Is.EqualTo(1),
                "A failed reusable module must not block the next module.");
            Assert.That(first.OwnerFixedUpdateCount, Is.EqualTo(1));

            Assert.That(second.Activate(), Is.True);
            Assert.That(first.Failing.IsActive, Is.False);
            Assert.That(first.Survivor.IsActive, Is.False);
            Assert.That(second.Failing.IsActive, Is.True);
            Assert.That(second.Survivor.IsActive, Is.True);
            Assert.That(first.Trace[first.Trace.Count - 1],
                Is.EqualTo("owner-deactivate"));
            Assert.That(first.Survivor.Trace[first.Survivor.Trace.Count - 1],
                Is.EqualTo("deactivate"));
            Assert.That(first.Failing.Trace[first.Failing.Trace.Count - 1],
                Is.EqualTo("deactivate"));
        }

        [UnityTest]
        public IEnumerator C06_PhysXCollisionDispatchesOnceInStableOrderWithinBudget()
        {
            rig = new BehaviourSystemRig(BehaviourFixtureMode.Collision);
            yield return null;

            CollisionBehaviourProbe probe = rig.Factory.Collision;
            RagdollCollisionHub hub = rig.Result.Collisions;
            hub.MaxEventsPerFixedStep = 1;
            List<string> listeners = new List<string>();
            hub.CollisionReported += collisionEvent =>
            {
                if (collisionEvent.Phase != RagdollCollisionPhase.Enter) return;
                listeners.Add("first:" + collisionEvent.Sequence);
                throw new InvalidOperationException(
                    "synthetic collision subscriber failure");
            };
            hub.CollisionReported += collisionEvent =>
            {
                if (collisionEvent.Phase == RagdollCollisionPhase.Enter)
                {
                    listeners.Add("second:" + collisionEvent.Sequence);
                }
            };
            hub.CollisionEntered += collisionEvent =>
                listeners.Add("phase:" + collisionEvent.Sequence);

            LogAssert.Expect(
                LogType.Exception,
                new Regex("synthetic collision subscriber failure"));
            rig.CreateProjectile(new Vector3(-2f, 0f, 0f), Vector3.right * 20f);
            rig.CreateProjectile(new Vector3(2f, 0f, 0f), Vector3.left * 20f);

            for (int step = 0; step < 20 && probe.EnterCount == 0; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(probe.EnterCount, Is.EqualTo(1),
                "The per-step budget must admit one observed Enter event.");
            Assert.That(hub.DroppedEventCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(listeners.Count, Is.EqualTo(3));
            string sequence = listeners[0].Substring(listeners[0].IndexOf(':'));
            Assert.That(listeners[0], Does.StartWith("first:"));
            Assert.That(listeners[1], Is.EqualTo("second" + sequence));
            Assert.That(listeners[2], Is.EqualTo("phase" + sequence));
            Assert.That(probe.Sequences[0],
                Is.EqualTo(long.Parse(sequence.Substring(1))));
        }

        static void AssertExactlyOneSelected(
            RagdollBehaviourController controller,
            RagdollBehaviourBase selected)
        {
            int enabled = 0;
            for (int index = 0; index < controller.Behaviours.Count; index++)
            {
                RagdollBehaviourBase behaviour = controller.Behaviours[index];
                if (behaviour.enabled) enabled++;
                Assert.That(behaviour.IsActive,
                    Is.EqualTo(ReferenceEquals(behaviour, selected)));
            }
            Assert.That(enabled, Is.EqualTo(1));
        }

        static void AssertOrdered(IReadOnlyList<string> trace, params string[] expected)
        {
            int searchFrom = 0;
            for (int expectedIndex = 0;
                expectedIndex < expected.Length;
                expectedIndex++)
            {
                int found = -1;
                for (int index = searchFrom; index < trace.Count; index++)
                {
                    if (trace[index] != expected[expectedIndex]) continue;
                    found = index;
                    break;
                }
                Assert.That(found, Is.GreaterThanOrEqualTo(0),
                    "Missing ordered callback '" + expected[expectedIndex]
                    + "'. Trace: " + string.Join(", ", trace));
                searchFrom = found + 1;
            }
        }
    }

    internal enum BehaviourFixtureMode
    {
        Lifecycle,
        Switching,
        Context,
        SubBehaviours,
        Collision
    }

    internal sealed class BehaviourSystemRig : IDisposable
    {
        readonly GameObject puppet;
        readonly GameObject target;
        readonly RagdollDefinition definition;
        readonly RagdollAnimationProfile profile;
        readonly List<GameObject> projectiles = new List<GameObject>();
        readonly bool ignoredBefore;

        internal BehaviourSystemObjectFactory Factory { get; }
        internal RagdollSetupResult Result { get; }

        internal BehaviourSystemRig(BehaviourFixtureMode mode)
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(28, 29);
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");

            puppet = new GameObject("Puppet");
            puppet.SetActive(false);
            GameObject child = new GameObject("Child");
            child.transform.SetParent(puppet.transform, false);
            child.transform.localPosition = Vector3.up;
            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            rootBody.useGravity = false;
            rootBody.constraints = RigidbodyConstraints.FreezeAll;
            ConfigurableJoint rootJoint =
                puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();
            Rigidbody childBody = child.AddComponent<Rigidbody>();
            childBody.useGravity = false;
            childBody.constraints = RigidbodyConstraints.FreezeAll;
            ConfigurableJoint childJoint =
                child.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            child.AddComponent<BoxCollider>();

            definition = ScriptableObject.CreateInstance<RagdollDefinition>();
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

            target = new GameObject("Puppet");
            GameObject targetChild = new GameObject("Child");
            targetChild.transform.SetParent(target.transform, false);
            targetChild.transform.localPosition = Vector3.up;
            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            Factory = new BehaviourSystemObjectFactory(mode);
            Result = RagdollRuntimeSetupService.ConfigureSeparated(
                target.transform,
                bindings,
                profile,
                28,
                29,
                Factory);
            Assert.That(Result.Succeeded, Is.True, Result.Error);
        }

        internal void CreateProjectile(Vector3 position, Vector3 velocity)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Behaviour collision projectile";
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
            Physics.IgnoreLayerCollision(28, 29, ignoredBefore);
            for (int index = 0; index < projectiles.Count; index++)
            {
                if (projectiles[index])
                {
                    UnityEngine.Object.DestroyImmediate(projectiles[index]);
                }
            }
            if (target) UnityEngine.Object.DestroyImmediate(target);
            if (puppet) UnityEngine.Object.DestroyImmediate(puppet);
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

    internal sealed class BehaviourSystemObjectFactory :
        RagdollRuntimeSetupService.IObjectFactory
    {
        readonly BehaviourFixtureMode mode;

        internal LifecycleBehaviourProbe Lifecycle { get; private set; }
        internal SwitchingBehaviourProbe FirstSwitch { get; private set; }
        internal SwitchingBehaviourProbe SecondSwitch { get; private set; }
        internal SwitchingBehaviourProbe FailingSwitch { get; private set; }
        internal ContextBehaviourProbe Context { get; private set; }
        internal SubBehaviourOwnerProbe FirstSubOwner { get; private set; }
        internal SubBehaviourOwnerProbe SecondSubOwner { get; private set; }
        internal CollisionBehaviourProbe Collision { get; private set; }

        internal BehaviourSystemObjectFactory(BehaviourFixtureMode mode)
        {
            this.mode = mode;
        }

        public T AddComponent<T>(GameObject owner) where T : Component
        {
            T component = owner.AddComponent<T>();
            RagdollPuppetBehaviour puppet = component as RagdollPuppetBehaviour;
            if (puppet) puppet.enabled = false;
            return component;
        }

        public GameObject CreateGameObject(string name)
        {
            GameObject value = new GameObject(name);
            switch (mode)
            {
                case BehaviourFixtureMode.Lifecycle:
                    Lifecycle = value.AddComponent<LifecycleBehaviourProbe>();
                    break;
                case BehaviourFixtureMode.Switching:
                    FirstSwitch = value.AddComponent<SwitchingBehaviourProbe>();
                    SecondSwitch = value.AddComponent<SwitchingBehaviourProbe>();
                    FailingSwitch = value.AddComponent<SwitchingBehaviourProbe>();
                    SecondSwitch.enabled = false;
                    FailingSwitch.enabled = false;
                    break;
                case BehaviourFixtureMode.Context:
                    Context = value.AddComponent<ContextBehaviourProbe>();
                    break;
                case BehaviourFixtureMode.SubBehaviours:
                    FirstSubOwner = value.AddComponent<SubBehaviourOwnerProbe>();
                    SecondSubOwner = value.AddComponent<SubBehaviourOwnerProbe>();
                    SecondSubOwner.enabled = false;
                    break;
                case BehaviourFixtureMode.Collision:
                    Collision = value.AddComponent<CollisionBehaviourProbe>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return value;
        }

        public void Destroy(UnityEngine.Object value)
        {
            if (value) UnityEngine.Object.DestroyImmediate(value);
        }
    }

    public sealed class LifecycleBehaviourProbe : RagdollBehaviourBase
    {
        public readonly List<string> Trace = new List<string>();
        public int Count(string value) => Trace.FindAll(item => item == value).Count;
        protected override void OnBehaviourInitialize() => Trace.Add("initialize");
        protected override void OnBehaviourPostInitialized() => Trace.Add("post-initialize");
        protected override void OnBehaviourActivated() => Trace.Add("activate");
        protected override void OnBehaviourFixTransforms() => Trace.Add("fix");
        protected override void OnBehaviourRead() => Trace.Add("read");
        protected override void OnBehaviourFixedUpdate(float deltaTime) => Trace.Add("fixed");
        protected override void OnModifyTargetPose(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs) => Trace.Add("pose");
        protected override void OnBehaviourWrite() => Trace.Add("write");
        protected override void OnBehaviourDeactivated() => Trace.Add("deactivate");
        protected override void OnBehaviourShutdown() => Trace.Add("shutdown");
    }

    public sealed class SwitchingBehaviourProbe : RagdollBehaviourBase
    {
        public SwitchingBehaviourProbe ReentrantTarget { get; set; }
        public bool ReentrantRejected { get; private set; }
        public bool ThrowOnActivation { get; set; }
        public int ActivationCount { get; private set; }

        protected override void OnBehaviourActivated()
        {
            ActivationCount++;
            if (ReentrantTarget)
            {
                try
                {
                    ReentrantTarget.Activate();
                }
                catch (InvalidOperationException)
                {
                    ReentrantRejected = true;
                }
            }
            if (ThrowOnActivation)
            {
                throw new InvalidOperationException("synthetic activation failure");
            }
        }
    }

    public sealed class ContextBehaviourProbe : RagdollBehaviourBase
    {
        public RagdollBehaviourController CapturedController { get; private set; }
        public RagdollAnimator CapturedAnimator { get; private set; }
        public RagdollMuscleController CapturedMuscles { get; private set; }
        public RagdollCollisionHub CapturedHub { get; private set; }
        public RagdollDefinitionBindings CapturedBindings { get; private set; }
        public IReadOnlyList<RagdollAnimator.AnimatedPair> CapturedPairs { get; private set; }

        protected override void OnBehaviourInitialize()
        {
            CapturedController = Context.Controller;
            CapturedAnimator = Context.Animator;
            CapturedMuscles = Context.Muscles;
            CapturedHub = Context.CollisionHub;
            CapturedBindings = Context.Bindings;
            CapturedPairs = Context.Pairs;
        }
    }

    [Serializable]
    public sealed class ReusableSubBehaviourProbe : RagdollSubBehaviourBase
    {
        public readonly List<string> Trace = new List<string>();
        public bool ThrowOnFixedUpdate { get; set; }
        public int FixedUpdateCount { get; private set; }
        protected override void OnInitialize() => Trace.Add("initialize");
        protected override void OnActivate() => Trace.Add("activate");
        protected override void OnDeactivate() => Trace.Add("deactivate");
        protected override void OnShutdown() => Trace.Add("shutdown");
        protected override void OnFixedUpdate(float deltaTime)
        {
            FixedUpdateCount++;
            Trace.Add("fixed");
            if (ThrowOnFixedUpdate)
            {
                throw new InvalidOperationException(
                    "synthetic sub-behaviour failure");
            }
        }
    }

    public sealed class SubBehaviourOwnerProbe : RagdollBehaviourBase
    {
        public readonly List<string> Trace = new List<string>();
        public readonly ReusableSubBehaviourProbe Failing =
            new ReusableSubBehaviourProbe();
        public readonly ReusableSubBehaviourProbe Survivor =
            new ReusableSubBehaviourProbe();
        public int OwnerFixedUpdateCount { get; private set; }

        protected override void OnBehaviourInitialize()
        {
            RegisterSubBehaviour(Failing);
            RegisterSubBehaviour(Survivor);
        }
        protected override void OnBehaviourActivated() => Trace.Add("owner-activate");
        protected override void OnBehaviourDeactivated() => Trace.Add("owner-deactivate");
        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            OwnerFixedUpdateCount++;
            Trace.Add("owner-fixed");
        }
    }

    public sealed class CollisionBehaviourProbe : RagdollBehaviourBase
    {
        public readonly List<long> Sequences = new List<long>();
        public int EnterCount { get; private set; }
        protected override void OnBehaviourCollision(
            RagdollCollisionEvent collisionEvent)
        {
            if (collisionEvent.Phase != RagdollCollisionPhase.Enter) return;
            EnterCount++;
            Sequences.Add(collisionEvent.Sequence);
        }
    }
}
