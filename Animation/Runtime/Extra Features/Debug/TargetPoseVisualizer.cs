using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Debug
{
    /// <summary>
    /// Draws, in the Unity Editor only, the animated Target pose read by the
    /// RagdollAnimator. Player builds retain the component contract but execute
    /// no visualization work, matching PuppetMaster's documented profiling rule.
    /// </summary>
    [AddComponentMenu("Ragdoll/Target Pose Visualizer")]
    [RequireComponent(typeof(RagdollAnimator))]
    public class TargetPoseVisualizer : MonoBehaviour
#if UNITY_EDITOR
        , ITargetPoseModifier
#endif
    {
        #region Inspector
        [Header("Visual Style")]
        public Color boneColor = Color.green;
        [Tooltip("Hairibar-only leaf direction marker; the documented Target-pose segments remain green by default.")]
        public Color leafBoneColor = Color.yellow;
        [Range(0, 1)] public float leafBoneLength = 0.2f;
        #endregion

#if UNITY_EDITOR
        Dictionary<Transform, Bone> bones;
        int lastDrawnSegmentCount;
#endif

        public bool IsInitialized
        {
            get
            {
#if UNITY_EDITOR
                return bones != null;
#else
                return false;
#endif
            }
        }

        public int BindingCount
        {
            get
            {
#if UNITY_EDITOR
                return bones?.Count ?? 0;
#else
                return 0;
#endif
            }
        }

        /// <summary>
        /// Number of parent-to-child segments submitted to Debug.DrawLine by
        /// the last Editor pose pass. Always zero in a Player build.
        /// </summary>
        public int LastDrawnSegmentCount
        {
            get
            {
#if UNITY_EDITOR
                return lastDrawnSegmentCount;
#else
                return 0;
#endif
            }
        }

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
#if UNITY_EDITOR
            Bone bone;
            if (bones == null || !ragdollBone
                || !bones.TryGetValue(ragdollBone, out bone))
                return false;
            targetBone = bone.target;
            worldPosition = bone.lastReadPosition;
            worldRotation = bone.lastReadRotation;
            isLeaf = bone.isLeaf;
            return true;
#else
            return false;
#endif
        }


        public void ModifyPose(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
#if UNITY_EDITOR
            if (bones == null)
                throw new System.InvalidOperationException(
                    "TargetPoseVisualizer must be initialized before it draws.");
            ReadAnimatedPose(pairs);

            lastDrawnSegmentCount = 0;
            foreach (Bone bone in bones.Values)
            {
                DrawBone(bone);
            }
#endif
        }

#if UNITY_EDITOR
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
            lastDrawnSegmentCount++;

            if (bone.isLeaf) UnityEngine.Debug.DrawLine(bone.lastReadPosition, bone.lastReadPosition + bone.lastReadRotation * Vector3.up * leafBoneLength, leafBoneColor);
        }
#endif


        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
#if UNITY_EDITOR
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
#endif
        }

#if UNITY_EDITOR
        class Bone
        {
            public Transform transform;
            public Transform target;
            public Transform parent;
            public bool isLeaf;

            public Vector3 lastReadPosition;
            public Quaternion lastReadRotation;
        }
#endif
    }
}
