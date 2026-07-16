using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Resolved allocation-free forced-ignore matrix indexed by bone handles.</summary>
    internal sealed class RagdollInternalCollisionIgnoreRuntime
    {
        internal struct ResolvedRule
        {
            internal int SourceIndex;
            internal bool IgnoreAll;
            internal int[] MuscleIndices;
            internal RagdollMuscleGroup[] Groups;

            internal ResolvedRule(
                int sourceIndex,
                bool ignoreAll,
                int[] muscleIndices,
                RagdollMuscleGroup[] groups)
            {
                SourceIndex = sourceIndex;
                IgnoreAll = ignoreAll;
                MuscleIndices = muscleIndices ?? new int[0];
                Groups = groups ?? new RagdollMuscleGroup[0];
            }
        }

        readonly int boneCount;
        readonly bool[] forcedIgnores;

        internal int BoneCount => boneCount;
        internal int ForcedBonePairCount { get; }

        RagdollInternalCollisionIgnoreRuntime(
            int boneCount,
            bool[] forcedIgnores,
            int forcedBonePairCount)
        {
            this.boneCount = boneCount;
            this.forcedIgnores = forcedIgnores;
            ForcedBonePairCount = forcedBonePairCount;
        }

        internal static RagdollInternalCollisionIgnoreRuntime CreateEmpty(
            int boneCount)
        {
            if (boneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boneCount));
            }

            return new RagdollInternalCollisionIgnoreRuntime(
                boneCount,
                new bool[boneCount * boneCount],
                0);
        }

        internal static bool TryCreate(
            int boneCount,
            RagdollMuscleGroup[] boneGroups,
            ResolvedRule[] rules,
            out RagdollInternalCollisionIgnoreRuntime runtime,
            out string error)
        {
            runtime = null;
            error = null;

            if (boneCount < 0)
            {
                error = "Bone count cannot be negative.";
                return false;
            }
            if (boneGroups == null || boneGroups.Length != boneCount)
            {
                error = "The internal-collision group table must contain one entry per bone.";
                return false;
            }

            rules = rules ?? new ResolvedRule[0];
            bool[] matrix = new bool[boneCount * boneCount];
            bool[] sourceSeen = new bool[boneCount];
            int forcedPairCount = 0;

            for (int ruleIndex = 0; ruleIndex < rules.Length; ruleIndex++)
            {
                ResolvedRule rule = rules[ruleIndex];
                if (rule.SourceIndex < 0 || rule.SourceIndex >= boneCount)
                {
                    error = "Internal-collision rule " + ruleIndex
                        + " has an invalid source index.";
                    return false;
                }
                if (sourceSeen[rule.SourceIndex])
                {
                    error = "Internal-collision source index " + rule.SourceIndex
                        + " is authored more than once.";
                    return false;
                }
                sourceSeen[rule.SourceIndex] = true;

                if (rule.IgnoreAll)
                {
                    for (int other = 0; other < boneCount; other++)
                    {
                        if (other == rule.SourceIndex) continue;
                        SetForced(
                            matrix,
                            boneCount,
                            rule.SourceIndex,
                            other,
                            ref forcedPairCount);
                    }
                }

                int[] muscleIndices = rule.MuscleIndices ?? new int[0];
                for (int target = 0; target < muscleIndices.Length; target++)
                {
                    int targetIndex = muscleIndices[target];
                    if (targetIndex < 0 || targetIndex >= boneCount)
                    {
                        error = "Internal-collision rule " + ruleIndex
                            + " contains an invalid target index.";
                        return false;
                    }
                    if (targetIndex == rule.SourceIndex) continue;

                    SetForced(
                        matrix,
                        boneCount,
                        rule.SourceIndex,
                        targetIndex,
                        ref forcedPairCount);
                }

                RagdollMuscleGroup[] groups =
                    rule.Groups ?? new RagdollMuscleGroup[0];
                for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    RagdollMuscleGroup group = groups[groupIndex];
                    if (!Enum.IsDefined(typeof(RagdollMuscleGroup), group))
                    {
                        error = "Internal-collision rule " + ruleIndex
                            + " contains an invalid muscle group.";
                        return false;
                    }

                    for (int other = 0; other < boneCount; other++)
                    {
                        if (other == rule.SourceIndex
                            || boneGroups[other] != group)
                        {
                            continue;
                        }

                        SetForced(
                            matrix,
                            boneCount,
                            rule.SourceIndex,
                            other,
                            ref forcedPairCount);
                    }
                }
            }

            runtime = new RagdollInternalCollisionIgnoreRuntime(
                boneCount,
                matrix,
                forcedPairCount);
            return true;
        }

        internal bool IsForcedIgnore(int firstBoneIndex, int secondBoneIndex)
        {
            ValidateBoneIndex(firstBoneIndex, nameof(firstBoneIndex));
            ValidateBoneIndex(secondBoneIndex, nameof(secondBoneIndex));
            if (firstBoneIndex == secondBoneIndex) return false;

            return forcedIgnores[
                firstBoneIndex * boneCount + secondBoneIndex];
        }

        static void SetForced(
            bool[] matrix,
            int boneCount,
            int first,
            int second,
            ref int forcedPairCount)
        {
            int firstOffset = first * boneCount + second;
            if (matrix[firstOffset]) return;

            matrix[firstOffset] = true;
            matrix[second * boneCount + first] = true;
            forcedPairCount++;
        }

        void ValidateBoneIndex(int index, string parameterName)
        {
            if (index < 0 || index >= boneCount)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
