using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Runtime joint controls that are independent from authored muscle spring and
    /// damping profiles. Joint anchors are updated from the resolved Target pose while
    /// angular limits can either follow the serialized global toggle or be managed
    /// explicitly through the manual API.
    /// </summary>
    [Serializable]
    public struct RagdollJointRuntimeSettings
    {
        const int CurrentSerializedVersion = 1;

        [SerializeField]
        [Tooltip("Updates ConfigurableJoint.connectedAnchor from the current resolved Target pose. This is required when animated bones exist between registered ragdoll muscles.")]
        bool updateJointAnchors;

        [SerializeField]
        [Tooltip("Also updates anchors when a registered Target bone is parented directly to its registered parent. Enable this when those Target bones contain translation animation.")]
        bool supportTranslationAnimation;

        [SerializeField]
        [Tooltip("Uses the angular motions authored on each ConfigurableJoint. When disabled, all registered angular motions are set to Free unless lifecycle or manual control temporarily owns them.")]
        bool angularLimits;

        [SerializeField, HideInInspector] int serializedVersion;

        public bool UpdateJointAnchors
        {
            get => updateJointAnchors;
            set => updateJointAnchors = value;
        }

        public bool SupportTranslationAnimation
        {
            get => supportTranslationAnimation;
            set => supportTranslationAnimation = value;
        }

        public bool AngularLimits
        {
            get => angularLimits;
            set => angularLimits = value;
        }

        public RagdollJointRuntimeSettings(
            bool updateJointAnchors,
            bool supportTranslationAnimation,
            bool angularLimits)
        {
            this.updateJointAnchors = updateJointAnchors;
            this.supportTranslationAnimation = supportTranslationAnimation;
            this.angularLimits = angularLimits;
            serializedVersion = CurrentSerializedVersion;
        }

        public static RagdollJointRuntimeSettings Default =>
            new RagdollJointRuntimeSettings(
                true,
                false,
                false);

        internal void Normalize()
        {
            if (serializedVersion >= CurrentSerializedVersion) return;

            // Missing sprint-0030 data receives the published PuppetMaster defaults.
            // The version marker preserves intentional false values authored later.
            updateJointAnchors = true;
            supportTranslationAnimation = false;
            angularLimits = false;
            serializedVersion = CurrentSerializedVersion;
        }
    }
}
