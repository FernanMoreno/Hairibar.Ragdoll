using NUnit.Framework;

namespace Hairibar.Ragdoll.Tests
{
    public class RagdollBoneHandleTests
    {
        [Test]
        public void InvalidHandle_IsNotValid()
        {
            Assert.That(RagdollBoneHandle.Invalid.IsValid, Is.False);
        }

        [Test]
        public void Equality_RequiresSameRegistryGenerationAndIndex()
        {
            RagdollBoneHandle handle = new RagdollBoneHandle(10, 3, 2);

            Assert.That(handle, Is.EqualTo(new RagdollBoneHandle(10, 3, 2)));
            Assert.That(handle, Is.Not.EqualTo(new RagdollBoneHandle(11, 3, 2)));
            Assert.That(handle, Is.Not.EqualTo(new RagdollBoneHandle(10, 4, 2)));
            Assert.That(handle, Is.Not.EqualTo(new RagdollBoneHandle(10, 3, 1)));
        }

        [Test]
        public void IsValid_RequiresNonZeroRegistryAndGeneration()
        {
            Assert.That(new RagdollBoneHandle(10, 3, 0).IsValid, Is.True);
            Assert.That(new RagdollBoneHandle(0, 3, 0).IsValid, Is.False);
            Assert.That(new RagdollBoneHandle(10, 0, 0).IsValid, Is.False);
            Assert.That(new RagdollBoneHandle(10, 3, -1).IsValid, Is.False);
        }

        [Test]
        public void BoneName_ProvidesTypedEqualityForGenericCollections()
        {
            BoneName value = new BoneName("Spine");
            Assert.That(typeof(System.IEquatable<BoneName>)
                .IsAssignableFrom(typeof(BoneName)), Is.True);
            Assert.That(value.Equals(new BoneName("Spine")), Is.True);
            Assert.That(value.Equals(new BoneName("Head")), Is.False);

            System.Collections.Generic.HashSet<BoneName> names =
                new System.Collections.Generic.HashSet<BoneName> { value };
            Assert.That(names.Contains(new BoneName("Spine")), Is.True);
            Assert.That(default(BoneName).GetHashCode(), Is.EqualTo(0));
        }
    }
}
