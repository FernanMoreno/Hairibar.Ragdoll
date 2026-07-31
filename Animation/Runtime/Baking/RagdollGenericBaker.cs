using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    [AddComponentMenu("Ragdoll/Baking/Ragdoll Generic Baker")]
    public sealed class RagdollGenericBaker : RagdollBaker
    {
        public bool markAsLegacy;
        public Transform root;
        public Transform rootNode;
        public Transform[] ignoreList = new Transform[0];
        public Transform[] bakePositionList = new Transform[0];

        public override Transform RecordingRoot => root ? root : transform;
    }
}
