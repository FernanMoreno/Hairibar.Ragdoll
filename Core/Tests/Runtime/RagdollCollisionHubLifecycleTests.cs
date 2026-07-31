using NUnit.Framework;

namespace Hairibar.Ragdoll.Tests
{
    public class RagdollCollisionHubLifecycleTests
    {
        [Test]
        public void AddedAfterBindingsInitialization_AttachesEveryRelayImmediately()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            {
                RagdollCollisionHub hub =
                    rig.Bindings.gameObject.AddComponent<RagdollCollisionHub>();

                RagdollCollisionRelay rootRelay =
                    rig.RootBody.GetComponent<RagdollCollisionRelay>();
                RagdollCollisionRelay childRelay =
                    rig.ChildBody.GetComponent<RagdollCollisionRelay>();

                Assert.That(rootRelay, Is.Not.Null);
                Assert.That(childRelay, Is.Not.Null);
                Assert.That(rootRelay.Owner, Is.SameAs(hub));
                Assert.That(childRelay.Owner, Is.SameAs(hub));
            }
        }
    }
}
