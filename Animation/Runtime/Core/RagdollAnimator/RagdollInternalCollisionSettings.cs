using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Global automatic internal-collision policy. Authored forced ignores are supplied
    /// by the active RagdollMuscleProfile and remain effective while collisions are on.
    /// </summary>
    [Serializable]
    public struct RagdollInternalCollisionSettings
    {
        const int CurrentSerializedVersion = 1;

        [SerializeField]
        [Tooltip("When enabled, colliders belonging to different registered ragdoll bones collide unless an authored muscle ignore rule forces that pair to remain ignored.")]
        bool internalCollisions;

        [SerializeField, HideInInspector] int serializedVersion;

        public bool InternalCollisions
        {
            get => internalCollisions;
            set => internalCollisions = value;
        }

        public RagdollInternalCollisionSettings(bool internalCollisions)
        {
            this.internalCollisions = internalCollisions;
            serializedVersion = CurrentSerializedVersion;
        }

        public static RagdollInternalCollisionSettings Default =>
            new RagdollInternalCollisionSettings(false);

        internal void Normalize()
        {
            if (serializedVersion >= CurrentSerializedVersion) return;

            // PuppetMaster's public field defaults to false. The explicit version keeps
            // intentional false values distinguishable from data authored before 0031.
            internalCollisions = false;
            serializedVersion = CurrentSerializedVersion;
        }
    }
}
