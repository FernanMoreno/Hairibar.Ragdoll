using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetBehaviourStaggerTriggerTests
    {
        const float MinimumDuration = 0.1f;

        GameObject owner;
        RagdollPuppetBehaviour behaviour;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("puppet-stagger-trigger-test");
            behaviour = owner.AddComponent<RagdollPuppetBehaviour>();
            behaviour.CanStagger = true;
            behaviour.MinimumRequiresStepDuration = MinimumDuration;
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
        }

        [Test]
        public void CanStaggerDisabled_NeverInvokesOnRequiresStep()
        {
            behaviour.CanStagger = false;
            int invoked = 0;
            behaviour.OnRequiresStep = CreateEvent(() => invoked++);

            Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 2f);

            Assert.That(invoked, Is.Zero);
        }

        [Test]
        public void SustainedRequiresStep_InvokesOnRequiresStepExactlyOnce()
        {
            int invoked = 0;
            behaviour.OnRequiresStep = CreateEvent(() => invoked++);

            Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f);
            Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f);
            Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f);

            Assert.That(invoked, Is.EqualTo(1));
        }

        [Test]
        public void StableClassification_ResetsAccumulationAndNeverInvokes()
        {
            int invoked = 0;
            behaviour.OnRequiresStep = CreateEvent(() => invoked++);

            Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f);
            Evaluate(RagdollBipedBalanceState.Stable, MinimumDuration);
            Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f);

            Assert.That(invoked, Is.Zero);
        }

        static RagdollPuppetEvent CreateEvent(UnityAction action)
        {
            RagdollPuppetEvent result = new RagdollPuppetEvent();
            result.UnityEvent.AddListener(action);
            return result;
        }

        void Evaluate(RagdollBipedBalanceState classification, float deltaTime)
        {
            MethodInfo method = typeof(RagdollPuppetBehaviour).GetMethod(
                "EvaluateStaggerTrigger",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(behaviour, new object[] { classification, deltaTime });
        }
    }
}
