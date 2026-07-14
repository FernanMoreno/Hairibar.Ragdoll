using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollModifierOrderingTests
    {
        [Test]
        public void StableSort_OrdersStageThenPriority()
        {
            TestModifier behaviour = new TestModifier("behaviour", RagdollModifierStage.Behaviour, 0);
            TestModifier runtimeLate = new TestModifier("runtime-late", RagdollModifierStage.RuntimeState, 10);
            TestModifier runtimeEarly = new TestModifier("runtime-early", RagdollModifierStage.RuntimeState, -10);

            TestModifier[] modifiers = { behaviour, runtimeLate, runtimeEarly };
            RagdollModifierOrdering.StableSort(modifiers);

            Assert.That(modifiers[0], Is.SameAs(runtimeEarly));
            Assert.That(modifiers[1], Is.SameAs(runtimeLate));
            Assert.That(modifiers[2], Is.SameAs(behaviour));
        }

        [Test]
        public void StableSort_PlacesLegacyModifiersBeforeOrderedStages()
        {
            object behaviour = new TestModifier("behaviour", RagdollModifierStage.Behaviour, 0);
            object legacy = new object();
            object runtime = new TestModifier("runtime", RagdollModifierStage.RuntimeState, 0);

            object[] modifiers = { behaviour, legacy, runtime };
            RagdollModifierOrdering.StableSort(modifiers);

            Assert.That(modifiers[0], Is.SameAs(legacy));
            Assert.That(modifiers[1], Is.SameAs(runtime));
            Assert.That(modifiers[2], Is.SameAs(behaviour));
        }

        [Test]
        public void StableSort_PreservesOrderForEqualKeys()
        {
            TestModifier first = new TestModifier("first", RagdollModifierStage.Impact, 2);
            TestModifier second = new TestModifier("second", RagdollModifierStage.Impact, 2);
            TestModifier third = new TestModifier("third", RagdollModifierStage.Impact, 2);

            TestModifier[] modifiers = { first, second, third };
            RagdollModifierOrdering.StableSort(modifiers);

            Assert.That(modifiers[0], Is.SameAs(first));
            Assert.That(modifiers[1], Is.SameAs(second));
            Assert.That(modifiers[2], Is.SameAs(third));
        }

        sealed class TestModifier : IOrderedRagdollModifier
        {
            readonly string name;

            public RagdollModifierStage Stage { get; private set; }
            public int Priority { get; private set; }

            public TestModifier(string name, RagdollModifierStage stage, int priority)
            {
                this.name = name;
                Stage = stage;
                Priority = priority;
            }

            public override string ToString()
            {
                return name;
            }
        }
    }
}
