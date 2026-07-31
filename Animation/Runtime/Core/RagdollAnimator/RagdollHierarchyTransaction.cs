using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Describes one generation-safe runtime muscle replacement.</summary>
    [Serializable]
    public struct RagdollMuscleReplacement
    {
        public RagdollBoneHandle Existing { get; }
        public RagdollRuntimeMuscleRegistration Replacement { get; }

        public RagdollMuscleReplacement(
            RagdollBoneHandle existing,
            RagdollRuntimeMuscleRegistration replacement)
        {
            Existing = existing;
            Replacement = replacement;
        }
    }

    /// <summary>Immutable result of an atomic hierarchy collection transaction.</summary>
    public sealed class RagdollHierarchyTransactionResult
    {
        static readonly RagdollMuscleChange[] EmptyChanges =
            new RagdollMuscleChange[0];

        public bool Succeeded { get; }
        public string Error { get; }
        public IReadOnlyList<RagdollMuscleChange> Added { get; }
        public IReadOnlyList<RagdollMuscleChange> Removed { get; }
        public int RegistryGeneration { get; }

        internal RagdollHierarchyTransactionResult(
            bool succeeded,
            string error,
            RagdollMuscleChange[] added,
            RagdollMuscleChange[] removed,
            int registryGeneration)
        {
            Succeeded = succeeded;
            Error = error;
            Added = added ?? EmptyChanges;
            Removed = removed ?? EmptyChanges;
            RegistryGeneration = registryGeneration;
        }

        internal static RagdollHierarchyTransactionResult Failure(
            string error,
            int registryGeneration)
        {
            return new RagdollHierarchyTransactionResult(
                false,
                error,
                EmptyChanges,
                EmptyChanges,
                registryGeneration);
        }
    }
}
