using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollPuppetStateMachineTests
    {
        [Test]
        public void StartsInPuppetState()
        {
            RagdollPuppetStateMachine machine =
                new RagdollPuppetStateMachine();

            Assert.That(machine.State, Is.EqualTo(RagdollPuppetState.Puppet));
            Assert.That(machine.StateElapsedTime, Is.EqualTo(0f));
        }

        [Test]
        public void DocumentedStateCycle_IsAccepted()
        {
            RagdollPuppetStateMachine machine =
                new RagdollPuppetStateMachine();

            Assert.That(machine.TryTransition(RagdollPuppetState.Unpinned), Is.True);
            Assert.That(machine.TryTransition(RagdollPuppetState.GetUp), Is.True);
            Assert.That(machine.TryTransition(RagdollPuppetState.Puppet), Is.True);
        }

        [Test]
        public void UnpinnedCannotSkipGetUp()
        {
            RagdollPuppetStateMachine machine =
                new RagdollPuppetStateMachine();
            machine.TryTransition(RagdollPuppetState.Unpinned);

            Assert.That(machine.TryTransition(RagdollPuppetState.Puppet), Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollPuppetState.Unpinned));
        }

        [Test]
        public void GetUpCanBeInterrupted()
        {
            RagdollPuppetStateMachine machine =
                new RagdollPuppetStateMachine();
            machine.TryTransition(RagdollPuppetState.Unpinned);
            machine.TryTransition(RagdollPuppetState.GetUp);

            Assert.That(machine.TryTransition(RagdollPuppetState.Unpinned), Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollPuppetState.Unpinned));
        }

        [Test]
        public void GetUpCompletesAfterBlendDuration()
        {
            RagdollPuppetStateMachine machine =
                new RagdollPuppetStateMachine();
            machine.TryTransition(RagdollPuppetState.Unpinned);
            machine.TryTransition(RagdollPuppetState.GetUp);

            Assert.That(machine.Advance(0.4f, 1f), Is.False);
            Assert.That(machine.GetUpProgress(1f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(machine.Advance(0.6f, 1f), Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollPuppetState.Puppet));
        }

        [Test]
        public void ZeroDurationGetUpCompletesOnNextAdvance()
        {
            RagdollPuppetStateMachine machine =
                new RagdollPuppetStateMachine();
            machine.TryTransition(RagdollPuppetState.Unpinned);
            machine.TryTransition(RagdollPuppetState.GetUp);

            Assert.That(machine.Advance(0f, 0f), Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollPuppetState.Puppet));
        }
    }
}
