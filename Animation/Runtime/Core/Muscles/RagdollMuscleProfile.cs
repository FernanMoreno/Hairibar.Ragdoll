using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Defines semantic body regions, authored behaviour limits and forced internal-
    /// collision ignores for a ragdoll. The asset is resolved once into arrays indexed
    /// by RagdollBoneHandle.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Ragdoll/Muscle Behaviour Profile",
        fileName = "ragmuscle_New",
        order = 30)]
    public sealed class RagdollMuscleProfile : ScriptableObject
    {
        [SerializeField] RagdollDefinition definition;
        [SerializeField] RagdollMuscleBehaviourSettings defaultSettings =
            RagdollMuscleBehaviourSettings.Default;
        [SerializeField] RagdollMuscleGroupOverride[] groupOverrides =
            new RagdollMuscleGroupOverride[0];
        [SerializeField] RagdollMuscleGroupAssignment[] boneGroups =
            new RagdollMuscleGroupAssignment[0];
        [SerializeField]
        [Tooltip("Per-muscle forced internal-collision ignores. A rule may ignore every other muscle, specific muscles, or semantic groups; matching is symmetric.")]
        RagdollInternalCollisionIgnore[] internalCollisionIgnores =
            new RagdollInternalCollisionIgnore[0];

        public RagdollDefinition Definition => definition;
        public RagdollMuscleBehaviourSettings DefaultSettings => defaultSettings;
        public IReadOnlyList<RagdollMuscleGroupOverride> GroupOverrides => groupOverrides;
        public IReadOnlyList<RagdollMuscleGroupAssignment> BoneGroups => boneGroups;
        public IReadOnlyList<RagdollInternalCollisionIgnore> InternalCollisionIgnores =>
            internalCollisionIgnores;

        public bool TrySynchronizeAssignments(out string error)
        {
            error = null;
            if (!definition)
            {
                error = "A RagdollDefinition must be assigned before synchronizing muscle groups.";
                return false;
            }

            Dictionary<BoneName, RagdollMuscleGroup> existing =
                new Dictionary<BoneName, RagdollMuscleGroup>();
            if (boneGroups != null)
            {
                for (int index = 0; index < boneGroups.Length; index++)
                {
                    RagdollMuscleGroupAssignment assignment = boneGroups[index];
                    if (assignment == null || existing.ContainsKey(assignment.Bone)) continue;
                    existing.Add(assignment.Bone, assignment.Group);
                }
            }

            List<RagdollMuscleGroupAssignment> synchronized =
                new List<RagdollMuscleGroupAssignment>(definition.BoneCount);
            foreach (BoneName bone in definition.Bones)
            {
                RagdollMuscleGroup group;
                if (!existing.TryGetValue(bone, out group))
                {
                    group = definition.IsRoot(bone)
                        ? RagdollMuscleGroup.Hips
                        : RagdollMuscleGroup.Spine;
                }

                synchronized.Add(new RagdollMuscleGroupAssignment(bone, group));
            }

            boneGroups = synchronized.ToArray();
            return true;
        }

        public bool TrySynchronizeInternalCollisionIgnores(out string error)
        {
            error = null;
            if (!definition)
            {
                error = "A RagdollDefinition must be assigned before synchronizing internal-collision ignores.";
                return false;
            }

            Dictionary<BoneName, RagdollInternalCollisionIgnore> existing =
                new Dictionary<BoneName, RagdollInternalCollisionIgnore>();
            if (internalCollisionIgnores != null)
            {
                for (int index = 0;
                    index < internalCollisionIgnores.Length;
                    index++)
                {
                    RagdollInternalCollisionIgnore rule =
                        internalCollisionIgnores[index];
                    if (rule == null || existing.ContainsKey(rule.Bone)) continue;
                    rule.Normalize();
                    existing.Add(rule.Bone, rule);
                }
            }

            List<RagdollInternalCollisionIgnore> synchronized =
                new List<RagdollInternalCollisionIgnore>(definition.BoneCount);
            foreach (BoneName bone in definition.Bones)
            {
                RagdollInternalCollisionIgnore rule;
                if (!existing.TryGetValue(bone, out rule))
                {
                    rule = new RagdollInternalCollisionIgnore(bone);
                }
                synchronized.Add(rule);
            }

            internalCollisionIgnores = synchronized.ToArray();
            return true;
        }

        public bool TryValidate(out string error)
        {
            Dictionary<BoneName, RagdollMuscleGroup> groupsByBone;
            Dictionary<RagdollMuscleGroup, RagdollMuscleBehaviourSettings>
                overridesByGroup;
            if (!TryBuildDefinitionLookup(
                out groupsByBone,
                out overridesByGroup,
                out error))
            {
                return false;
            }

            Dictionary<BoneName, int> indicesByBone =
                BuildDefinitionBoneIndices();
            RagdollInternalCollisionIgnoreRuntime runtime;
            return TryBuildInternalCollisionRuntime(
                indicesByBone,
                groupsByBone,
                out runtime,
                out error);
        }

        internal bool TryCreateRuntime(
            RagdollDefinitionBindings bindings,
            out RagdollMuscleProfileRuntime runtime,
            out string error)
        {
            runtime = null;
            if (!bindings)
            {
                error = "No RagdollDefinitionBindings was supplied.";
                return false;
            }

            if (!bindings.IsInitialized)
            {
                error = "The RagdollDefinitionBindings is not initialized.";
                return false;
            }

            if (definition != bindings.Definition)
            {
                error = "The muscle profile references a different RagdollDefinition.";
                return false;
            }

            Dictionary<BoneName, RagdollMuscleGroup> groupsByBone;
            Dictionary<RagdollMuscleGroup, RagdollMuscleBehaviourSettings> overridesByGroup;
            if (!TryBuildDefinitionLookup(
                out groupsByBone,
                out overridesByGroup,
                out error))
            {
                return false;
            }

            int count = bindings.BoneCount;
            RagdollMuscleGroup[] resolvedGroups = new RagdollMuscleGroup[count];
            RagdollMuscleBehaviourSettings[] resolvedSettings =
                new RagdollMuscleBehaviourSettings[count];

            RagdollMuscleBehaviourSettings normalizedDefault = defaultSettings;
            normalizedDefault.Normalize();

            for (int index = 0; index < count; index++)
            {
                BoneName bone = bindings.GetBoneAt(index).Name;
                RagdollMuscleGroup group = groupsByBone[bone];
                RagdollMuscleBehaviourSettings groupSettings;
                if (!overridesByGroup.TryGetValue(group, out groupSettings))
                {
                    groupSettings = normalizedDefault;
                }

                groupSettings.Normalize();
                resolvedGroups[index] = group;
                resolvedSettings[index] = groupSettings;
            }

            runtime = new RagdollMuscleProfileRuntime(
                resolvedGroups,
                resolvedSettings);
            return true;
        }

        internal bool TryCreateInternalCollisionRuntime(
            RagdollDefinitionBindings bindings,
            out RagdollInternalCollisionIgnoreRuntime runtime,
            out string error)
        {
            runtime = null;
            if (!bindings)
            {
                error = "No RagdollDefinitionBindings was supplied.";
                return false;
            }
            if (!bindings.IsInitialized)
            {
                error = "The RagdollDefinitionBindings is not initialized.";
                return false;
            }
            if (definition != bindings.Definition)
            {
                error = "The muscle profile references a different RagdollDefinition.";
                return false;
            }

            Dictionary<BoneName, RagdollMuscleGroup> groupsByBone;
            Dictionary<RagdollMuscleGroup, RagdollMuscleBehaviourSettings>
                overridesByGroup;
            if (!TryBuildDefinitionLookup(
                out groupsByBone,
                out overridesByGroup,
                out error))
            {
                return false;
            }

            Dictionary<BoneName, int> indicesByBone =
                new Dictionary<BoneName, int>(bindings.BoneCount);
            for (int index = 0; index < bindings.BoneCount; index++)
            {
                indicesByBone.Add(bindings.GetBoneAt(index).Name, index);
            }

            return TryBuildInternalCollisionRuntime(
                indicesByBone,
                groupsByBone,
                out runtime,
                out error);
        }

        Dictionary<BoneName, int> BuildDefinitionBoneIndices()
        {
            Dictionary<BoneName, int> indices =
                new Dictionary<BoneName, int>(definition.BoneCount);
            int index = 0;
            foreach (BoneName bone in definition.Bones)
            {
                indices.Add(bone, index++);
            }
            return indices;
        }

        bool TryBuildInternalCollisionRuntime(
            Dictionary<BoneName, int> indicesByBone,
            Dictionary<BoneName, RagdollMuscleGroup> groupsByBone,
            out RagdollInternalCollisionIgnoreRuntime runtime,
            out string error)
        {
            runtime = null;
            error = null;

            int boneCount = indicesByBone.Count;
            RagdollMuscleGroup[] groups = new RagdollMuscleGroup[boneCount];
            foreach (KeyValuePair<BoneName, int> pair in indicesByBone)
            {
                RagdollMuscleGroup group;
                if (!groupsByBone.TryGetValue(pair.Key, out group))
                {
                    error = "Ragdoll bone '" + pair.Key
                        + "' has no semantic muscle group.";
                    return false;
                }
                groups[pair.Value] = group;
            }

            if (internalCollisionIgnores == null
                || internalCollisionIgnores.Length == 0)
            {
                runtime = RagdollInternalCollisionIgnoreRuntime.CreateEmpty(
                    boneCount);
                return true;
            }

            List<RagdollInternalCollisionIgnoreRuntime.ResolvedRule> resolved =
                new List<RagdollInternalCollisionIgnoreRuntime.ResolvedRule>(
                    internalCollisionIgnores.Length);
            for (int ruleIndex = 0;
                ruleIndex < internalCollisionIgnores.Length;
                ruleIndex++)
            {
                RagdollInternalCollisionIgnore rule =
                    internalCollisionIgnores[ruleIndex];
                if (rule == null)
                {
                    error = "Internal-collision rule " + ruleIndex + " is null.";
                    return false;
                }
                rule.Normalize();

                int sourceIndex;
                if (!indicesByBone.TryGetValue(rule.Bone, out sourceIndex))
                {
                    error = "Internal-collision rule " + ruleIndex
                        + " references unknown source bone '" + rule.Bone + "'.";
                    return false;
                }

                int[] muscleIndices = new int[rule.Muscles.Length];
                for (int targetIndex = 0;
                    targetIndex < rule.Muscles.Length;
                    targetIndex++)
                {
                    BoneName target = rule.Muscles[targetIndex];
                    int resolvedIndex;
                    if (!indicesByBone.TryGetValue(target, out resolvedIndex))
                    {
                        error = "Internal-collision rule for '" + rule.Bone
                            + "' references unknown target bone '" + target + "'.";
                        return false;
                    }
                    muscleIndices[targetIndex] = resolvedIndex;
                }

                RagdollMuscleGroup[] ignoredGroups =
                    new RagdollMuscleGroup[rule.Groups.Length];
                for (int groupIndex = 0;
                    groupIndex < rule.Groups.Length;
                    groupIndex++)
                {
                    ignoredGroups[groupIndex] = rule.Groups[groupIndex];
                }

                resolved.Add(
                    new RagdollInternalCollisionIgnoreRuntime.ResolvedRule(
                        sourceIndex,
                        rule.IgnoreAll,
                        muscleIndices,
                        ignoredGroups));
            }

            return RagdollInternalCollisionIgnoreRuntime.TryCreate(
                boneCount,
                groups,
                resolved.ToArray(),
                out runtime,
                out error);
        }

        bool TryBuildDefinitionLookup(
            out Dictionary<BoneName, RagdollMuscleGroup> groupsByBone,
            out Dictionary<RagdollMuscleGroup, RagdollMuscleBehaviourSettings> overridesByGroup,
            out string error)
        {
            groupsByBone = null;
            overridesByGroup = null;
            error = null;

            if (!definition)
            {
                error = "The muscle profile has no RagdollDefinition.";
                return false;
            }

            if (boneGroups == null || boneGroups.Length != definition.BoneCount)
            {
                error = "The muscle group table must contain exactly one entry per definition bone.";
                return false;
            }

            groupsByBone = new Dictionary<BoneName, RagdollMuscleGroup>(definition.BoneCount);
            for (int index = 0; index < boneGroups.Length; index++)
            {
                RagdollMuscleGroupAssignment assignment = boneGroups[index];
                if (assignment == null)
                {
                    error = "Muscle group entry " + index + " is null.";
                    return false;
                }

                if (groupsByBone.ContainsKey(assignment.Bone))
                {
                    error = "Ragdoll bone '" + assignment.Bone + "' is assigned more than once.";
                    return false;
                }

                groupsByBone.Add(assignment.Bone, assignment.Group);
            }

            foreach (BoneName bone in definition.Bones)
            {
                if (!groupsByBone.ContainsKey(bone))
                {
                    error = "Ragdoll bone '" + bone + "' has no semantic muscle group.";
                    return false;
                }
            }

            overridesByGroup =
                new Dictionary<RagdollMuscleGroup, RagdollMuscleBehaviourSettings>();
            if (groupOverrides == null) return true;

            for (int index = 0; index < groupOverrides.Length; index++)
            {
                RagdollMuscleGroupOverride groupOverride = groupOverrides[index];
                if (groupOverride == null)
                {
                    error = "Muscle group override " + index + " is null.";
                    return false;
                }

                if (overridesByGroup.ContainsKey(groupOverride.Group))
                {
                    error = "Muscle group '" + groupOverride.Group
                        + "' has more than one behaviour override.";
                    return false;
                }

                RagdollMuscleBehaviourSettings settings = groupOverride.Settings;
                settings.Normalize();
                overridesByGroup.Add(groupOverride.Group, settings);
            }

            return true;
        }

        void OnValidate()
        {
            defaultSettings.Normalize();
            if (groupOverrides != null)
            {
                for (int index = 0; index < groupOverrides.Length; index++)
                {
                    if (groupOverrides[index] != null)
                    {
                        groupOverrides[index].Normalize();
                    }
                }
            }

            if (internalCollisionIgnores != null)
            {
                for (int index = 0;
                    index < internalCollisionIgnores.Length;
                    index++)
                {
                    if (internalCollisionIgnores[index] != null)
                    {
                        internalCollisionIgnores[index].Normalize();
                    }
                }
            }
        }
    }
}
