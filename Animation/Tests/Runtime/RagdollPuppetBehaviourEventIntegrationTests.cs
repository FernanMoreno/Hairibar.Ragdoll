using System;
using System.Reflection;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetBehaviourEventIntegrationTests
    {
        GameObject owner;
        RagdollPuppetBehaviour behaviour;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("puppet-event-integration-test");
            behaviour = owner.AddComponent<RagdollPuppetBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
        }

        [Test]
        public void PuppetToUnpinned_InvokesGeneralAndPuppetEvents()
        {
            int general = 0;
            int specific = 0;
            behaviour.OnLoseBalance = CreateEvent(() => general++);
            behaviour.OnLoseBalanceFromPuppet = CreateEvent(() => specific++);

            InvokeTransition(
                RagdollPuppetState.Puppet,
                RagdollPuppetState.Unpinned,
                RagdollPuppetTransitionReason.TargetDrift);

            Assert.That(general, Is.EqualTo(1));
            Assert.That(specific, Is.EqualTo(1));
        }

        [Test]
        public void LifecycleDeath_DoesNotReportGameplayLossOfBalance()
        {
            int invoked = 0;
            behaviour.OnLoseBalance = CreateEvent(() => invoked++);

            InvokeTransition(
                RagdollPuppetState.Puppet,
                RagdollPuppetState.Unpinned,
                RagdollPuppetTransitionReason.LifecycleDeath);

            Assert.That(invoked, Is.Zero);
        }

        [Test]
        public void GetUpCompletion_InvokesRegainBalance()
        {
            int invoked = 0;
            behaviour.OnRegainBalance = CreateEvent(() => invoked++);

            InvokeTransition(
                RagdollPuppetState.GetUp,
                RagdollPuppetState.Puppet,
                RagdollPuppetTransitionReason.GetUpCompleted);

            Assert.That(invoked, Is.EqualTo(1));
        }

        [TestCase(RagdollGetUpOrientation.Prone, true)]
        [TestCase(RagdollGetUpOrientation.Supine, false)]
        public void GetUpOrientation_InvokesMatchingSerializedEvent(
            RagdollGetUpOrientation orientation,
            bool expectProne)
        {
            int prone = 0;
            int supine = 0;
            behaviour.OnGetUpProne = CreateEvent(() => prone++);
            behaviour.OnGetUpSupine = CreateEvent(() => supine++);

            InvokePrivate("InvokeGetUpEvent", orientation);

            Assert.That(prone, Is.EqualTo(expectProne ? 1 : 0));
            Assert.That(supine, Is.EqualTo(expectProne ? 0 : 1));
        }

        [Test]
        public void FallCompletion_InvokesFullPuppetEventExactlyOnce()
        {
            RagdollFallBehaviour fall = owner.AddComponent<RagdollFallBehaviour>();
            int serialized = 0;
            int runtime = 0;
            fall.OnEnd = CreateEvent(() => serialized++);
            fall.Ended += () => runtime++;

            InvokePrivate(fall, "CompleteFall");
            InvokePrivate(fall, "CompleteFall");

            Assert.That(serialized, Is.EqualTo(1));
            Assert.That(runtime, Is.EqualTo(1));
        }

        [Test]
        public void CollisionObserved_PrecedesFiltersAndIncludesAllPhases()
        {
            int observed = 0;
            int accepted = 0;
            behaviour.CollisionLayers = 0;
            behaviour.CollisionObserved += _ => observed++;
            behaviour.CollisionAccepted += _ => accepted++;

            InvokePrivate(
                "OnBehaviourCollision",
                new RagdollCollisionEvent(
                    RagdollBoneHandle.Invalid,
                    RagdollCollisionPhase.Enter,
                    null,
                    1f,
                    1));
            InvokePrivate(
                "OnBehaviourCollision",
                new RagdollCollisionEvent(
                    RagdollBoneHandle.Invalid,
                    RagdollCollisionPhase.Stay,
                    null,
                    1f,
                    2));
            InvokePrivate(
                "OnBehaviourCollision",
                new RagdollCollisionEvent(
                    RagdollBoneHandle.Invalid,
                    RagdollCollisionPhase.Exit,
                    null,
                    1f,
                    3));

            Assert.That(observed, Is.EqualTo(3));
            Assert.That(accepted, Is.Zero);
        }

        [Test]
        public void CollisionStay_IsAcceptedWithoutApplyingRepeatedUnpinDamage()
        {
            int accepted = 0;
            int unpinned = 0;
            behaviour.CollisionLayers = -1;
            behaviour.CollisionThreshold = 0f;
            behaviour.CollisionAccepted += _ => accepted++;
            behaviour.CollisionUnpinApplied += (_, __) => unpinned++;

            InvokePrivate(
                "OnBehaviourCollision",
                new RagdollCollisionEvent(
                    RagdollBoneHandle.Invalid,
                    RagdollCollisionPhase.Stay,
                    null,
                    7f,
                    0));

            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(unpinned, Is.Zero);
        }

        [Test]
        public void CollisionSubscribers_AreIsolatedAndOfficialAliasSharesStream()
        {
            int afterFailure = 0;
            int alias = 0;
            behaviour.CollisionLayers = 0;
            behaviour.CollisionObserved += _ =>
                throw new InvalidOperationException("expected collision subscriber failure");
            behaviour.CollisionObserved += _ => afterFailure++;
            behaviour.OnCollision += _ => alias++;
            LogAssert.Expect(
                LogType.Exception,
                new Regex("expected collision subscriber failure"));

            InvokePrivate(
                "OnBehaviourCollision",
                new RagdollCollisionEvent(
                    RagdollBoneHandle.Invalid,
                    RagdollCollisionPhase.Enter,
                    null,
                    1f,
                    1));

            Assert.That(afterFailure, Is.EqualTo(1));
            Assert.That(alias, Is.EqualTo(1));
        }

        [Test]
        public void OnCollisionImpulseAliasPublishesResolvedSuppression()
        {
            float observed = 0f;
            behaviour.OnCollisionImpulse += (_, suppression) =>
                observed = suppression;
            FieldInfo cacheField = typeof(RagdollPuppetBehaviour).GetField(
                "collisionUnpinSubscribers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object cache = cacheField.GetValue(behaviour);
            PropertyInfo snapshotProperty = cache.GetType().GetProperty(
                "Snapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Delegate[] snapshot = (Delegate[])snapshotProperty.GetValue(cache);

            InvokePrivate(
                "InvokeCollisionImpulseSafely",
                snapshot,
                default(RagdollCollisionEvent),
                0.25f);

            Assert.That(observed, Is.EqualTo(0.25f));
        }

        [Test]
        public void SerializedUnityEventFailureIsExposedAsAggregateDiagnostic()
        {
            RagdollPuppetEvent puppetEvent = new RagdollPuppetEvent();
            int laterListener = 0;
            puppetEvent.UnityEvent.AddListener(() =>
                throw new InvalidOperationException(
                    "expected serialized listener failure"));
            puppetEvent.UnityEvent.AddListener(() => laterListener++);

            RagdollPuppetEventInvocationResult result =
                puppetEvent.InvokeWithDiagnostics(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failures.Count, Is.EqualTo(1));
            Assert.That(result.Failures[0].Message,
                Does.Contain("expected serialized listener failure"));
            Assert.That(laterListener, Is.Zero,
                "Unity exposes the serialized UnityEvent as one invocation unit; callers can now detect its aborted remainder.");
            Assert.Throws<AggregateException>(() => result.ThrowIfFailed());
        }

        static RagdollPuppetEvent CreateEvent(UnityEngine.Events.UnityAction action)
        {
            RagdollPuppetEvent result = new RagdollPuppetEvent();
            result.UnityEvent.AddListener(action);
            return result;
        }

        void InvokeTransition(
            RagdollPuppetState previous,
            RagdollPuppetState current,
            RagdollPuppetTransitionReason reason)
        {
            InvokePrivate("InvokeTransitionEvents", previous, current, reason);
        }

        void InvokePrivate(string name, params object[] arguments)
        {
            InvokePrivate(behaviour, name, arguments);
        }

        static void InvokePrivate(
            object target,
            string name,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, arguments);
        }
    }
}
