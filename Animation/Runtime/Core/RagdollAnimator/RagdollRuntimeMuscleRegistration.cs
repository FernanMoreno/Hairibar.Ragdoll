using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Runtime-only description of a muscle to append to an initialized ragdoll.
    /// The caller must position the joint and target at the desired bind pose before
    /// submitting the registration from FixedUpdate.
    /// </summary>
    [Serializable]
    public struct RagdollRuntimeMuscleRegistration
    {
        [SerializeField] BoneName bone;
        [SerializeField] ConfigurableJoint joint;
        [SerializeField] Transform target;
        [SerializeField] Transform targetParent;
        [SerializeField] RagdollMuscleGroup group;
        [SerializeField] bool forceTreeHierarchy;
        [SerializeField] bool forceLayers;

        public BoneName Bone => bone;
        public ConfigurableJoint Joint => joint;
        public Transform Target => target;
        public Transform TargetParent => targetParent;
        public RagdollMuscleGroup Group => group;
        public bool ForceTreeHierarchy => forceTreeHierarchy;
        public bool ForceLayers => forceLayers;

        public RagdollRuntimeMuscleRegistration(
            BoneName bone,
            ConfigurableJoint joint,
            Transform target,
            RagdollMuscleGroup group,
            Transform targetParent = null,
            bool forceTreeHierarchy = false,
            bool forceLayers = true)
        {
            this.bone = bone;
            this.joint = joint;
            this.target = target;
            this.targetParent = targetParent;
            this.group = group;
            this.forceTreeHierarchy = forceTreeHierarchy;
            this.forceLayers = forceLayers;
        }
    }

    /// <summary>
    /// Immutable description emitted after a hierarchy transaction has committed.
    /// </summary>
    public sealed class RagdollMuscleChange
    {
        public BoneName Bone { get; }
        public ConfigurableJoint Joint { get; }
        public Transform Target { get; }
        public RagdollBoneHandle Handle { get; }
        public bool Added { get; }

        internal RagdollMuscleChange(
            BoneName bone,
            ConfigurableJoint joint,
            Transform target,
            RagdollBoneHandle handle,
            bool added)
        {
            Bone = bone;
            Joint = joint;
            Target = target;
            Handle = handle;
            Added = added;
        }
    }
}
