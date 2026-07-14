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
    }
}
