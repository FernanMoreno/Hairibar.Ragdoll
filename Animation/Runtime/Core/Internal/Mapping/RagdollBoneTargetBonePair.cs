using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal class RagdollBoneTargetBonePair
    {
        #region Public API
        public RagdollBone RagdollBone { get; }
        public RagdollTargetBinding TargetBinding { get; }
        public Transform TargetBone => TargetBinding.Target;
        #endregion

        internal RagdollBoneTargetBonePair(
            RagdollBone ragdollBone,
            RagdollTargetBinding targetBinding)
        {
            RagdollBone = ragdollBone;
            TargetBinding = targetBinding;
        }
    }
}
