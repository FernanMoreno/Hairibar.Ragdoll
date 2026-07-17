using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>How a registered muscle branch is physically separated.</summary>
    public enum RagdollMuscleDisconnectMode
    {
        /// <summary>Release only the requested branch root; descendants remain joined together.</summary>
        Sever,
        /// <summary>Release the requested muscle and every descendant independently.</summary>
        Explode
    }

    /// <summary>Physical policy applied when a branch leaves the runtime registry.</summary>
    public enum RagdollMuscleRemoveMode
    {
        Sever,
        Explode,
        Numb
    }

    public enum RagdollMuscleConnectionState
    {
        Connected,
        Disconnected,
        Deactivated
    }

    /// <summary>Post-commit description of a reversible connection-state change.</summary>
    public sealed class RagdollMuscleConnectionChange
    {
        public BoneName Bone { get; }
        public RagdollBoneHandle Handle { get; }
        public ConfigurableJoint Joint { get; }
        public Rigidbody Rigidbody { get; }
        public Transform Target { get; }
        public RagdollMuscleDisconnectMode Mode { get; }
        public RagdollMuscleConnectionState State { get; }
        public bool Connected => State == RagdollMuscleConnectionState.Connected;

        internal RagdollMuscleConnectionChange(
            BoneName bone,
            RagdollBoneHandle handle,
            ConfigurableJoint joint,
            Rigidbody rigidbody,
            Transform target,
            RagdollMuscleDisconnectMode mode,
            RagdollMuscleConnectionState state)
        {
            Bone = bone;
            Handle = handle;
            Joint = joint;
            Rigidbody = rigidbody;
            Target = target;
            Mode = mode;
            State = state;
        }
    }

    /// <summary>Notification emitted after a broken joint branch has left the registry.</summary>
    public sealed class RagdollJointBreakEvent
    {
        public BoneName Bone { get; }
        public float BreakForce { get; }
        public RagdollMuscleChange[] RemovedMuscles { get; }

        internal RagdollJointBreakEvent(
            BoneName bone,
            float breakForce,
            RagdollMuscleChange[] removedMuscles)
        {
            Bone = bone;
            BreakForce = breakForce;
            RemovedMuscles = removedMuscles ?? new RagdollMuscleChange[0];
        }
    }
}
