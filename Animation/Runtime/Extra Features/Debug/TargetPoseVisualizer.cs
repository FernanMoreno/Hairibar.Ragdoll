using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Debug
{
    /// <summary>
    /// Draws the animated pose that the RagdollAnimator has read this frame. Not suitable for Release.
    /// </summary>
    [AddComponentMenu("Ragdoll/Target Pose Visualizer")]
    [RequireComponent(typeof(RagdollAnimator))]
    public class TargetPoseVisualizer : MonoBehaviour, ITargetPoseModifier
    {
        #region Inspector
        [Header("Visual Style")]
        public Color boneColor = Color.red;
        public Color leafBoneColor = Color.yellow;
        [Range(0, 1)] public float leafBoneLength = 0.2f;
        #endregion

        Dictionary<Transform, Bone> bones = null;

        public bool IsInitialized => bones != null;
        public int BindingCount => bones?.Count ?? 0;

        /// <summary>
        /// Returns the last pose observed for one physical binding. This is a
        /// read-only diagnostic snapshot; the visualizer never writes Target,
        /// Rigidbody, joint or simulation state.
        /// </summary>
        public bool TryGetSnapshot(
            Transform ragdollBone,
            out Transform targetBone,
            out Vector3 worldPosition,
            out Quaternion worldRotation,
            out bool isLeaf)
        {
            targetBone = null;
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            isLeaf = false;
            Bone bone;
            if (bones == null || !ragdollBone
                || !bones.TryGetValue(ragdollBone, out bone))
                return false;
            targetBone = bone.target;
            worldPosition = bone.lastReadPosition;
            worldRotation = bone.lastReadRotation;
            isLeaf = bone.isLeaf;
            return true;
        }


        public void ModifyPose(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            ReadAnimatedPose(pairs);

            foreach (Bone bone in bones.Values)
            {
                DrawBone(bone);
            }
        }

        void ReadAnimatedPose(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                Bone bone;
                if (pair == null || !bones.TryGetValue(
                    pair.RagdollBone.Transform, out bone))
                    continue;
                bone.lastReadPosition = pair.currentPose.worldPosition;
                bone.lastReadRotation = pair.currentPose.worldRotation;
            }
        }

        void DrawBone(Bone bone)
        {
            if (bone.parent == null) return;

            Bone parent;
            if (!bones.TryGetValue(bone.parent, out parent)) return;

            UnityEngine.Debug.DrawLine(parent.lastReadPosition, bone.lastReadPosition, boneColor);

            if (bone.isLeaf) UnityEngine.Debug.DrawLine(bone.lastReadPosition, bone.lastReadPosition + bone.lastReadRotation * Vector3.up * leafBoneLength, leafBoneColor);
        }


        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            if (pairs == null)
                throw new System.ArgumentNullException(nameof(pairs));
            bones = new Dictionary<Transform, Bone>();

            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                if (pair == null || pair.RagdollBone == null
                    || !pair.RagdollBone.Transform)
                    continue;
                Bone bone = new Bone
                {
                    transform = pair.RagdollBone.Transform,
                    target = pair.TargetBone,
                    lastReadRotation = Quaternion.identity
                };
                bones.Add(bone.transform, bone);
            }

            // Physical topology is defined by ConfigurableJoint.connectedBody,
            // not Transform parenting (the authored rig can be flat).
            foreach (Bone bone in bones.Values)
            {
                ConfigurableJoint joint = bone.transform
                    .GetComponent<ConfigurableJoint>();
                Transform physicalParent = joint && joint.connectedBody
                    ? joint.connectedBody.transform
                    : null;
                bone.parent = physicalParent && bones.ContainsKey(physicalParent)
                    ? physicalParent
                    : null;
            }
            foreach (Bone bone in bones.Values) bone.isLeaf = true;
            foreach (Bone bone in bones.Values)
            {
                Bone parent;
                if (bone.parent && bones.TryGetValue(bone.parent, out parent))
                    parent.isLeaf = false;
            }
        }


        class Bone
        {
            public Transform transform;
            public Transform target;
            public Transform parent;
            public bool isLeaf;

            public Vector3 lastReadPosition;
            public Quaternion lastReadRotation;
        }
    }
}
