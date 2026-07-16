using System;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBehaviourCollectionTests
    {
        GameObject root;
        TestRagdollBehaviour first;
        TestRagdollBehaviour second;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("BehaviourRoot");
            first = new GameObject("First").AddComponent<TestRagdollBehaviour>();
            second = new GameObject("Second").AddComponent<TestRagdollBehaviour>();
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void FindInitiallyEnabled_ReturnsFirstEnabledBehaviour()
        {
            first.enabled = true;
            second.enabled = true;
            RagdollBehaviourCollection collection = CreateCollection();

            int enabledCount;
            RagdollBehaviourBase selected =
                collection.FindInitiallyEnabled(out enabledCount);

            Assert.That(selected, Is.SameAs(first));
            Assert.That(enabledCount, Is.EqualTo(2));
        }

        [Test]
        public void FindInitiallyEnabled_IgnoresInactiveGameObjects()
        {
            first.enabled = true;
            second.enabled = true;
            first.gameObject.SetActive(false);
            RagdollBehaviourCollection collection = CreateCollection();

            int enabledCount;
            RagdollBehaviourBase selected =
                collection.FindInitiallyEnabled(out enabledCount);

            Assert.That(selected, Is.SameAs(second));
            Assert.That(enabledCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsDuplicateComponents()
        {
            Assert.Throws<ArgumentException>(
                () => new RagdollBehaviourCollection(
                    new RagdollBehaviourBase[] { first, first }));
        }

        [Test]
        public void TrySetActive_SameBehaviourIsNoOp()
        {
            RagdollBehaviourCollection collection = CreateCollection();
            RagdollBehaviourBase previous;

            Assert.That(collection.TrySetActive(first, out previous), Is.True);
            Assert.That(previous, Is.Null);
            Assert.That(collection.TrySetActive(first, out previous), Is.False);
            Assert.That(previous, Is.SameAs(first));
            Assert.That(collection.Active, Is.SameAs(first));
        }

        [Test]
        public void TrySetActive_AllowsSwitchAndDeactivation()
        {
            RagdollBehaviourCollection collection = CreateCollection();
            RagdollBehaviourBase previous;

            collection.TrySetActive(first, out previous);
            Assert.That(collection.TrySetActive(second, out previous), Is.True);
            Assert.That(previous, Is.SameAs(first));
            Assert.That(collection.Active, Is.SameAs(second));

            Assert.That(collection.TrySetActive(null, out previous), Is.True);
            Assert.That(previous, Is.SameAs(second));
            Assert.That(collection.Active, Is.Null);
        }

        [Test]
        public void TrySetActive_RejectsForeignBehaviour()
        {
            GameObject foreignObject = new GameObject("Foreign");
            TestRagdollBehaviour foreign =
                foreignObject.AddComponent<TestRagdollBehaviour>();

            try
            {
                RagdollBehaviourCollection collection = CreateCollection();
                RagdollBehaviourBase previous;

                Assert.Throws<ArgumentException>(
                    () => collection.TrySetActive(foreign, out previous));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(foreignObject);
            }
        }

        [Test]
        public void LifecycleDispatch_RequiresRequestedAndActiveAlive()
        {
            Assert.That(
                RagdollBehaviourController.LifecycleAllowsDispatch(
                    RagdollLifecycleState.Alive,
                    RagdollLifecycleState.Alive,
                    false,
                    false),
                Is.True);
            Assert.That(
                RagdollBehaviourController.LifecycleAllowsDispatch(
                    RagdollLifecycleState.Frozen,
                    RagdollLifecycleState.Alive,
                    false,
                    false),
                Is.False);
            Assert.That(
                RagdollBehaviourController.LifecycleAllowsDispatch(
                    RagdollLifecycleState.Alive,
                    RagdollLifecycleState.Frozen,
                    false,
                    false),
                Is.False);
            Assert.That(
                RagdollBehaviourController.LifecycleAllowsDispatch(
                    RagdollLifecycleState.Alive,
                    RagdollLifecycleState.Alive,
                    true,
                    false),
                Is.False);
        }

        [Test]
        public void FrozenState_DisablesEvenTheSelectedBehaviour()
        {
            Assert.That(
                RagdollBehaviourController.ShouldEnableBehaviour(false, true),
                Is.True);
            Assert.That(
                RagdollBehaviourController.ShouldEnableBehaviour(false, false),
                Is.False);
            Assert.That(
                RagdollBehaviourController.ShouldEnableBehaviour(true, true),
                Is.False);
        }

        RagdollBehaviourCollection CreateCollection()
        {
            return new RagdollBehaviourCollection(
                new RagdollBehaviourBase[] { first, second });
        }
    }

    public sealed class TestRagdollBehaviour : RagdollBehaviourBase
    {
    }
}
