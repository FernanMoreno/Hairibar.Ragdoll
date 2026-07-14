using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Compatibility and editor utility for converting the previous name-based setup into
    /// explicit bindings. Runtime simulation never performs name searches after initialization.
    /// </summary>
    internal static class RagdollTargetBindingUtility
    {
        internal static bool TryCreateByUniqueName(
            RagdollDefinitionBindings ragdollBindings,
            Transform targetRoot,
            out RagdollTargetBinding[] createdBindings,
            out string error)
        {
            createdBindings = null;
            error = null;

            if (!ragdollBindings)
            {
                error = "No RagdollDefinitionBindings was supplied.";
                return false;
            }

            if (!ragdollBindings.IsInitialized)
            {
                error = "The RagdollDefinitionBindings must be initialized before target bindings can be generated.";
                return false;
            }

            if (!targetRoot)
            {
                error = "No target hierarchy root was supplied.";
                return false;
            }

            int boneCount = ragdollBindings.BoneCount;
            RagdollTargetBinding[] temporary = new RagdollTargetBinding[boneCount];
            List<Transform> matches = new List<Transform>(2);

            for (int index = 0; index < boneCount; index++)
            {
                RagdollBone ragdollBone = ragdollBindings.GetBoneAt(index);
                matches.Clear();
                FindAllByName(targetRoot, ragdollBone.Transform.name, matches);

                if (matches.Count == 0)
                {
                    error = "No target Transform named '" + ragdollBone.Transform.name
                        + "' was found for ragdoll bone '" + ragdollBone.Name + "'.";
                    return false;
                }

                if (matches.Count > 1)
                {
                    error = "More than one target Transform named '" + ragdollBone.Transform.name
                        + "' was found. Assign this bone explicitly before capturing offsets.";
                    return false;
                }

                temporary[index] = new RagdollTargetBinding(
                    ragdollBone.Name,
                    matches[0],
                    ragdollBone.Transform);
            }

            createdBindings = temporary;
            return true;
        }

        static void FindAllByName(
            Transform current,
            string searchedName,
            List<Transform> results)
        {
            if (current.name == searchedName)
            {
                results.Add(current);
            }

            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                FindAllByName(
                    current.GetChild(childIndex),
                    searchedName,
                    results);
            }
        }
    }
}
