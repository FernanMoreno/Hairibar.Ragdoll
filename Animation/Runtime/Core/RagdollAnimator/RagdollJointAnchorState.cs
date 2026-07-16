using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Owns one ConfigurableJoint connected anchor while runtime anchor updates are
    /// available. The authored values are captured once and can be restored without
    /// giving ownership back to PhysX, or released exactly during shutdown.
    /// </summary>
    internal sealed class RagdollJointAnchorState
    {
        readonly ConfigurableJoint joint;
        readonly Vector3 authoredConnectedAnchor;
        readonly bool authoredAutoConfigureConnectedAnchor;
        bool runtimeAnchorApplied;

        internal bool IsValid => joint;
        internal bool RuntimeAnchorApplied => runtimeAnchorApplied;
        internal Vector3 AuthoredConnectedAnchor =>
            authoredConnectedAnchor;
        internal bool AuthoredAutoConfigureConnectedAnchor =>
            authoredAutoConfigureConnectedAnchor;

        internal RagdollJointAnchorState(ConfigurableJoint joint)
        {
            if (!joint) throw new ArgumentNullException(nameof(joint));

            this.joint = joint;
            authoredConnectedAnchor = joint.connectedAnchor;
            authoredAutoConfigureConnectedAnchor =
                joint.autoConfigureConnectedAnchor;

            // PuppetMaster owns connected anchors after initialization, even for records
            // whose direct Target relationship allows per-frame updates to be skipped.
            joint.autoConfigureConnectedAnchor = false;
        }

        internal bool TryApply(Vector3 connectedAnchor)
        {
            if (!joint || !IsFinite(connectedAnchor)) return false;

            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = connectedAnchor;
            runtimeAnchorApplied = true;
            return true;
        }

        internal void RestoreAuthoredAnchor()
        {
            if (!joint) return;
            if (!runtimeAnchorApplied
                && joint.connectedAnchor == authoredConnectedAnchor
                && !joint.autoConfigureConnectedAnchor)
            {
                return;
            }

            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = authoredConnectedAnchor;
            runtimeAnchorApplied = false;
        }

        internal void ReleaseRuntimeOwnership()
        {
            if (!joint) return;

            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = authoredConnectedAnchor;
            joint.autoConfigureConnectedAnchor =
                authoredAutoConfigureConnectedAnchor;
            runtimeAnchorApplied = false;
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
