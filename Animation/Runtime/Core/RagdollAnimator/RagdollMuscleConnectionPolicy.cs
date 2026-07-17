using System;

namespace Hairibar.Ragdoll.Animation
{
    internal static class RagdollMuscleConnectionPolicy
    {
        internal static void BuildDisconnectMasks(
            RagdollBoneTopology topology,
            RagdollBoneHandle root,
            RagdollMuscleDisconnectMode mode,
            bool[] disconnected,
            bool[] severed)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (!topology.Contains(root)) throw new ArgumentException("The root handle does not belong to the topology.", nameof(root));
            if (disconnected == null || disconnected.Length != topology.BoneCount)
            {
                throw new ArgumentException("The disconnected mask length must match the topology.", nameof(disconnected));
            }
            if (severed == null || severed.Length != topology.BoneCount)
            {
                throw new ArgumentException("The severed mask length must match the topology.", nameof(severed));
            }

            Array.Clear(disconnected, 0, disconnected.Length);
            Array.Clear(severed, 0, severed.Length);
            for (int index = 0; index < topology.BoneCount; index++)
            {
                RagdollBoneHandle candidate = topology.GetHandleAt(index);
                bool inBranch = candidate == root || topology.IsAncestorOf(root, candidate);
                if (!inBranch) continue;
                disconnected[index] = true;
                severed[index] = mode == RagdollMuscleDisconnectMode.Explode || candidate == root;
            }
        }

        internal static int FindHighestDisconnectedAncestor(
            RagdollBoneTopology topology,
            RagdollBoneHandle requested,
            bool[] disconnected)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (!topology.Contains(requested)) throw new ArgumentException("The requested handle does not belong to the topology.", nameof(requested));
            if (disconnected == null || disconnected.Length != topology.BoneCount)
            {
                throw new ArgumentException("The disconnected mask length must match the topology.", nameof(disconnected));
            }

            int highest = requested.Index;
            RagdollBoneHandle current = requested;
            RagdollBoneHandle parent;
            while (topology.TryGetParent(current, out parent))
            {
                if (!disconnected[parent.Index]) break;
                highest = parent.Index;
                current = parent;
            }
            return highest;
        }

        internal static bool ShouldSuppressNormalMapping(bool disconnected)
        {
            return disconnected;
        }

        internal static bool ShouldForceDisconnectedMapping(
            bool mapDisconnectedMuscles,
            bool disconnected)
        {
            return mapDisconnectedMuscles && disconnected;
        }

        internal static bool IncludesDisconnectedBone(
            bool firstDisconnected,
            bool secondDisconnected)
        {
            return firstDisconnected || secondDisconnected;
        }
    }
}
