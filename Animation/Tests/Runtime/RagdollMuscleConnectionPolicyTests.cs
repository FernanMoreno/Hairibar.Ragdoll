using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollMuscleConnectionPolicyTests
    {
        const int RegistryId = 51;
        const int Generation = 7;

        [Test]
        public void Sever_DisconnectsBranchButSeversOnlyRequestedRoot()
        {
            RagdollBoneTopology topology = CreateTopology(
                new[] { -1, 0, 1, 1, 0 });
            bool[] disconnected = new bool[5];
            bool[] severed = new bool[5];

            RagdollMuscleConnectionPolicy.BuildDisconnectMasks(
                topology,
                Handle(1),
                RagdollMuscleDisconnectMode.Sever,
                disconnected,
                severed);

            CollectionAssert.AreEqual(
                new[] { false, true, true, true, false },
                disconnected);
            CollectionAssert.AreEqual(
                new[] { false, true, false, false, false },
                severed);
        }

        [Test]
        public void Explode_SeversEveryMuscleInRequestedBranch()
        {
            RagdollBoneTopology topology = CreateTopology(
                new[] { -1, 0, 1, 1, 0 });
            bool[] disconnected = new bool[5];
            bool[] severed = new bool[5];

            RagdollMuscleConnectionPolicy.BuildDisconnectMasks(
                topology,
                Handle(1),
                RagdollMuscleDisconnectMode.Explode,
                disconnected,
                severed);

            CollectionAssert.AreEqual(
                new[] { false, true, true, true, false },
                disconnected);
            CollectionAssert.AreEqual(disconnected, severed);
        }

        [Test]
        public void Reconnect_UsesHighestContiguousDisconnectedAncestor()
        {
            RagdollBoneTopology topology = CreateTopology(
                new[] { -1, 0, 1, 2, 0 });
            bool[] disconnected =
                { false, true, true, true, false };

            Assert.That(
                RagdollMuscleConnectionPolicy
                    .FindHighestDisconnectedAncestor(
                        topology,
                        Handle(3),
                        disconnected),
                Is.EqualTo(1));

            disconnected[1] = false;
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .FindHighestDisconnectedAncestor(
                        topology,
                        Handle(3),
                        disconnected),
                Is.EqualTo(2));
        }

        [Test]
        public void DisconnectedMapping_SuppressesNormalPassAndHonoursToggle()
        {
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .ShouldSuppressNormalMapping(true),
                Is.True);
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .ShouldForceDisconnectedMapping(true, true),
                Is.True);
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .ShouldForceDisconnectedMapping(false, true),
                Is.False);
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .ShouldForceDisconnectedMapping(true, false),
                Is.False);
        }

        [Test]
        public void CollisionBoundary_IncludesEitherDisconnectedEndpoint()
        {
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .IncludesDisconnectedBone(false, false),
                Is.False);
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .IncludesDisconnectedBone(true, false),
                Is.True);
            Assert.That(
                RagdollMuscleConnectionPolicy
                    .IncludesDisconnectedBone(false, true),
                Is.True);
        }

        static RagdollBoneTopology CreateTopology(int[] parents)
        {
            RagdollBoneTopology topology;
            string error;
            Assert.That(
                RagdollBoneTopology.TryCreate(
                    RegistryId,
                    Generation,
                    parents,
                    out topology,
                    out error),
                Is.True,
                error);
            return topology;
        }

        static RagdollBoneHandle Handle(int index)
        {
            return new RagdollBoneHandle(
                RegistryId,
                Generation,
                index);
        }
    }
}
