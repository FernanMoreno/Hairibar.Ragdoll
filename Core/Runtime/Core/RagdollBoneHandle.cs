using System;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Lightweight runtime identifier for a bone in a specific RagdollDefinitionBindings instance.
    /// Handles are valid only for the bindings instance that created them.
    /// </summary>
    public struct RagdollBoneHandle : IEquatable<RagdollBoneHandle>
    {
        public static RagdollBoneHandle Invalid => new RagdollBoneHandle(0, -1);

        public int Index { get; }

        public bool IsValid => RegistryId != 0 && Index >= 0;

        internal int RegistryId { get; }

        internal RagdollBoneHandle(int registryId, int index)
        {
            RegistryId = registryId;
            Index = index;
        }

        public bool Equals(RagdollBoneHandle other)
        {
            return RegistryId == other.RegistryId && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is RagdollBoneHandle && Equals((RagdollBoneHandle)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RegistryId * 397) ^ Index;
            }
        }

        public static bool operator ==(RagdollBoneHandle left, RagdollBoneHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RagdollBoneHandle left, RagdollBoneHandle right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return IsValid ? "RagdollBoneHandle(" + Index + ")" : "RagdollBoneHandle.Invalid";
        }
    }
}
