using System;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Immutable runtime topology with allocation-free queries for a single ragdoll registry generation.
    /// Parent and child relationships are derived from ConfigurableJoint.connectedBody.
    /// </summary>
    public sealed class RagdollBoneTopology
    {
        readonly int registryId;
        readonly int generation;
        readonly int[] parentIndices;
        readonly int[] depths;
        readonly int[] childOffsets;
        readonly int[] childIndices;

        public int BoneCount => parentIndices.Length;

        internal RagdollBoneTopology(
            int registryId,
            int generation,
            int[] parentIndices,
            int[] depths,
            int[] childOffsets,
            int[] childIndices)
        {
            this.registryId = registryId;
            this.generation = generation;
            this.parentIndices = parentIndices;
            this.depths = depths;
            this.childOffsets = childOffsets;
            this.childIndices = childIndices;
        }

        /// <summary>
        /// Returns whether a handle belongs to this exact topology generation.
        /// </summary>
        public bool Contains(RagdollBoneHandle handle)
        {
            return handle.RegistryId == registryId
                && handle.Generation == generation
                && handle.Index >= 0
                && handle.Index < parentIndices.Length;
        }

        public bool TryGetParent(RagdollBoneHandle child, out RagdollBoneHandle parent)
        {
            if (!Contains(child))
            {
                parent = RagdollBoneHandle.Invalid;
                return false;
            }

            int parentIndex = parentIndices[child.Index];
            if (parentIndex < 0)
            {
                parent = RagdollBoneHandle.Invalid;
                return false;
            }

            parent = CreateHandle(parentIndex);
            return true;
        }

        public int GetDepth(RagdollBoneHandle bone)
        {
            ValidateHandle(bone, nameof(bone));
            return depths[bone.Index];
        }

        public int GetChildCount(RagdollBoneHandle parent)
        {
            ValidateHandle(parent, nameof(parent));
            return childOffsets[parent.Index + 1] - childOffsets[parent.Index];
        }

        public RagdollBoneHandle GetChild(RagdollBoneHandle parent, int childOffset)
        {
            ValidateHandle(parent, nameof(parent));

            int firstChild = childOffsets[parent.Index];
            int childCount = childOffsets[parent.Index + 1] - firstChild;
            if (childOffset < 0 || childOffset >= childCount)
            {
                throw new ArgumentOutOfRangeException(nameof(childOffset));
            }

            return CreateHandle(childIndices[firstChild + childOffset]);
        }

        public bool IsAncestorOf(RagdollBoneHandle ancestor, RagdollBoneHandle descendant)
        {
            if (!Contains(ancestor) || !Contains(descendant) || ancestor == descendant)
            {
                return false;
            }

            int current = parentIndices[descendant.Index];
            while (current >= 0)
            {
                if (current == ancestor.Index)
                {
                    return true;
                }

                current = parentIndices[current];
            }

            return false;
        }

        /// <summary>
        /// Returns the number of parent-child edges in the shortest path between two bones.
        /// Returns -1 when the bones belong to disconnected trees in the same registry.
        /// </summary>
        public int GetKinshipDistance(RagdollBoneHandle first, RagdollBoneHandle second)
        {
            ValidateHandle(first, nameof(first));
            ValidateHandle(second, nameof(second));

            int firstIndex = first.Index;
            int secondIndex = second.Index;
            if (firstIndex == secondIndex)
            {
                return 0;
            }

            int firstDepth = depths[firstIndex];
            int secondDepth = depths[secondIndex];
            int distance = 0;

            while (firstDepth > secondDepth)
            {
                firstIndex = parentIndices[firstIndex];
                firstDepth--;
                distance++;
            }

            while (secondDepth > firstDepth)
            {
                secondIndex = parentIndices[secondIndex];
                secondDepth--;
                distance++;
            }

            while (firstIndex != secondIndex)
            {
                firstIndex = firstIndex >= 0 ? parentIndices[firstIndex] : -1;
                secondIndex = secondIndex >= 0 ? parentIndices[secondIndex] : -1;
                distance += 2;

                if (firstIndex < 0 || secondIndex < 0)
                {
                    return -1;
                }
            }

            return distance;
        }

        internal static bool TryCreate(
            int registryId,
            int generation,
            int[] sourceParentIndices,
            out RagdollBoneTopology topology,
            out string error)
        {
            topology = null;
            error = null;

            if (registryId == 0)
            {
                error = "A topology registry id cannot be zero.";
                return false;
            }

            if (generation == 0)
            {
                error = "A topology generation cannot be zero.";
                return false;
            }

            if (sourceParentIndices == null)
            {
                error = "Parent indices cannot be null.";
                return false;
            }

            int boneCount = sourceParentIndices.Length;
            int[] parents = new int[boneCount];
            Array.Copy(sourceParentIndices, parents, boneCount);

            for (int i = 0; i < boneCount; i++)
            {
                if (parents[i] < -1 || parents[i] >= boneCount)
                {
                    error = "Parent index " + parents[i] + " is invalid for bone index " + i + ".";
                    return false;
                }
            }

            int[] calculatedDepths;
            if (!TryCalculateDepths(parents, out calculatedDepths, out error))
            {
                return false;
            }

            int[] offsets;
            int[] children;
            CreateChildren(parents, out offsets, out children);

            topology = new RagdollBoneTopology(
                registryId,
                generation,
                parents,
                calculatedDepths,
                offsets,
                children);
            return true;
        }

        static bool TryCalculateDepths(int[] parents, out int[] calculatedDepths, out string error)
        {
            int boneCount = parents.Length;
            calculatedDepths = new int[boneCount];
            int[] visitStamps = new int[boneCount];
            int[] path = new int[boneCount];
            error = null;

            for (int i = 0; i < boneCount; i++)
            {
                calculatedDepths[i] = -1;
            }

            for (int start = 0; start < boneCount; start++)
            {
                if (calculatedDepths[start] >= 0) continue;

                int stamp = start + 1;
                int pathCount = 0;
                int current = start;

                while (current >= 0 && calculatedDepths[current] < 0)
                {
                    if (visitStamps[current] == stamp)
                    {
                        error = "The ragdoll topology contains a cycle at bone index " + current + ".";
                        return false;
                    }

                    visitStamps[current] = stamp;
                    path[pathCount++] = current;
                    current = parents[current];
                }

                int depth = current < 0 ? -1 : calculatedDepths[current];
                for (int i = pathCount - 1; i >= 0; i--)
                {
                    calculatedDepths[path[i]] = ++depth;
                }
            }

            return true;
        }

        static void CreateChildren(int[] parents, out int[] offsets, out int[] children)
        {
            int boneCount = parents.Length;
            int[] childCounts = new int[boneCount];
            int edgeCount = 0;

            for (int child = 0; child < boneCount; child++)
            {
                int parent = parents[child];
                if (parent < 0) continue;

                childCounts[parent]++;
                edgeCount++;
            }

            offsets = new int[boneCount + 1];
            for (int i = 0; i < boneCount; i++)
            {
                offsets[i + 1] = offsets[i] + childCounts[i];
                childCounts[i] = 0;
            }

            children = new int[edgeCount];
            for (int child = 0; child < boneCount; child++)
            {
                int parent = parents[child];
                if (parent < 0) continue;

                int destination = offsets[parent] + childCounts[parent]++;
                children[destination] = child;
            }
        }

        RagdollBoneHandle CreateHandle(int index)
        {
            return new RagdollBoneHandle(registryId, generation, index);
        }

        void ValidateHandle(RagdollBoneHandle handle, string parameterName)
        {
            if (!Contains(handle))
            {
                throw new ArgumentException("The supplied handle does not belong to this topology.", parameterName);
            }
        }
    }
}
