using NUnit.Framework;

namespace Hairibar.Ragdoll.Tests
{
    public class RagdollBoneTopologyTests
    {
        const int RegistryId = 17;
        const int Generation = 4;

        [Test]
        public void Create_BuildsDeterministicParentChildAndDepthData()
        {
            RagdollBoneTopology topology = CreateTopology(new[] { -1, 0, 0, 1, 3 });
            RagdollBoneHandle root = Handle(0);
            RagdollBoneHandle firstChild = Handle(1);
            RagdollBoneHandle secondChild = Handle(2);
            RagdollBoneHandle grandchild = Handle(3);
            RagdollBoneHandle deepestChild = Handle(4);

            Assert.That(topology.BoneCount, Is.EqualTo(5));
            Assert.That(topology.GetDepth(root), Is.EqualTo(0));
            Assert.That(topology.GetDepth(firstChild), Is.EqualTo(1));
            Assert.That(topology.GetDepth(secondChild), Is.EqualTo(1));
            Assert.That(topology.GetDepth(grandchild), Is.EqualTo(2));
            Assert.That(topology.GetDepth(deepestChild), Is.EqualTo(3));

            Assert.That(topology.GetChildCount(root), Is.EqualTo(2));
            Assert.That(topology.GetChild(root, 0), Is.EqualTo(firstChild));
            Assert.That(topology.GetChild(root, 1), Is.EqualTo(secondChild));
            Assert.That(topology.GetChildCount(firstChild), Is.EqualTo(1));
            Assert.That(topology.GetChild(firstChild, 0), Is.EqualTo(grandchild));

            RagdollBoneHandle parent;
            Assert.That(topology.TryGetParent(root, out parent), Is.False);
            Assert.That(parent, Is.EqualTo(RagdollBoneHandle.Invalid));
            Assert.That(topology.TryGetParent(deepestChild, out parent), Is.True);
            Assert.That(parent, Is.EqualTo(grandchild));

            Assert.That(topology.IsAncestorOf(root, deepestChild), Is.True);
            Assert.That(topology.IsAncestorOf(firstChild, deepestChild), Is.True);
            Assert.That(topology.IsAncestorOf(secondChild, deepestChild), Is.False);
            Assert.That(topology.IsAncestorOf(root, root), Is.False);
        }

        [Test]
        public void Create_CopiesParentIndices()
        {
            int[] parents = { -1, 0 };
            RagdollBoneTopology topology = CreateTopology(parents);
            parents[1] = -1;

            RagdollBoneHandle parent;
            Assert.That(topology.TryGetParent(Handle(1), out parent), Is.True);
            Assert.That(parent, Is.EqualTo(Handle(0)));
        }

        [Test]
        public void HandlesFromAnotherGeneration_AreRejected()
        {
            RagdollBoneTopology topology = CreateTopology(new[] { -1, 0 });
            RagdollBoneHandle staleHandle = new RagdollBoneHandle(RegistryId, Generation - 1, 1);

            Assert.That(topology.Contains(staleHandle), Is.False);
            Assert.That(() => topology.GetDepth(staleHandle), Throws.ArgumentException);
        }

        [Test]
        public void TryCreate_RejectsCycles()
        {
            RagdollBoneTopology topology;
            string error;

            bool created = RagdollBoneTopology.TryCreate(
                RegistryId,
                Generation,
                new[] { 1, 2, 0 },
                out topology,
                out error);

            Assert.That(created, Is.False);
            Assert.That(topology, Is.Null);
            Assert.That(error, Does.Contain("cycle"));
        }

        [Test]
        public void TryCreate_RejectsOutOfRangeParent()
        {
            RagdollBoneTopology topology;
            string error;

            bool created = RagdollBoneTopology.TryCreate(
                RegistryId,
                Generation,
                new[] { -1, 2 },
                out topology,
                out error);

            Assert.That(created, Is.False);
            Assert.That(topology, Is.Null);
            Assert.That(error, Does.Contain("invalid"));
        }

        [Test]
        public void GetChild_RejectsOutOfRangeOffset()
        {
            RagdollBoneTopology topology = CreateTopology(new[] { -1, 0 });

            Assert.That(() => topology.GetChild(Handle(0), -1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => topology.GetChild(Handle(0), 1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        static RagdollBoneTopology CreateTopology(int[] parents)
        {
            RagdollBoneTopology topology;
            string error;
            bool created = RagdollBoneTopology.TryCreate(
                RegistryId,
                Generation,
                parents,
                out topology,
                out error);

            Assert.That(created, Is.True, error);
            return topology;
        }

        static RagdollBoneHandle Handle(int index)
        {
            return new RagdollBoneHandle(RegistryId, Generation, index);
        }
    }
}
