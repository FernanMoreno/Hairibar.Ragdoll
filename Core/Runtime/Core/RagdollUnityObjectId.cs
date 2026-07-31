using System;
using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>Cross-version access to Unity's process-local object identity.</summary>
    public static class RagdollUnityObjectId
    {
        public static int Get(UnityEngine.Object value)
        {
            if (!value) throw new ArgumentNullException(nameof(value));
#if UNITY_6000_0_OR_NEWER
            // Handles are runtime-only and never serialized. Hashing preserves the
            // existing int-shaped registry API without relying on EntityId's obsolete
            // int conversion, sign, ordering or string representation.
            return value.GetEntityId().GetHashCode();
#else
            return value.GetInstanceID();
#endif
        }
    }
}
