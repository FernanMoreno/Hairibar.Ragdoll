using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollPuppetEventTests
    {
        [Test]
        public void AnimatorEvent_UsesOfficialDefaultsAndClampsDuration()
        {
            RagdollAnimatorEvent animatorEvent = new RagdollAnimatorEvent();

            Assert.That(animatorEvent.AnimationState, Is.Empty);
            Assert.That(animatorEvent.CrossfadeTime, Is.EqualTo(0.3f));
            Assert.That(animatorEvent.Layer, Is.Zero);
            Assert.That(animatorEvent.ResetNormalizedTime, Is.False);

            animatorEvent.CrossfadeTime = -1f;
            Assert.That(animatorEvent.CrossfadeTime, Is.Zero);

            animatorEvent.CrossfadeTime = float.NaN;
            Assert.That(animatorEvent.CrossfadeTime, Is.Zero);

            animatorEvent.Layer = -5;
            Assert.That(animatorEvent.Layer, Is.EqualTo(-1));
        }

        [Test]
        public void PuppetEvent_WithoutOwner_StillInvokesUnityEvent()
        {
            RagdollPuppetEvent puppetEvent = new RagdollPuppetEvent();
            int invocationCount = 0;
            puppetEvent.UnityEvent.AddListener(() => invocationCount++);

            puppetEvent.Invoke(null);

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        [Test]
        public void PuppetEvent_NullAnimationEntriesAreIgnored()
        {
            RagdollPuppetEvent puppetEvent = new RagdollPuppetEvent
            {
                Animations = new RagdollAnimatorEvent[] { null }
            };

            Assert.DoesNotThrow(() => puppetEvent.Invoke(null));
        }

        [Test]
        public void SubBehaviour_RejectsUseBeforeInitialization()
        {
            TestSubBehaviour subBehaviour = new TestSubBehaviour();

            Assert.Throws<System.InvalidOperationException>(
                () => subBehaviour.SetActive(true));
            Assert.Throws<System.InvalidOperationException>(
                () => subBehaviour.FixedUpdate(0.02f));
            Assert.DoesNotThrow(subBehaviour.Shutdown);
        }

        sealed class TestSubBehaviour : RagdollSubBehaviourBase
        {
        }
    }
}
