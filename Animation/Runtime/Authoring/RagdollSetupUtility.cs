using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Shared layer setup and dual-rig validation helpers.</summary>
    public static class RagdollSetupUtility
    {
        public static void SetLayerRecursively(Transform root, int layer)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (layer < 0 || layer > 31)
                throw new ArgumentOutOfRangeException(nameof(layer));

            root.gameObject.layer = layer;
            for (int index = 0; index < root.childCount; index++)
            {
                SetLayerRecursively(root.GetChild(index), layer);
            }
        }

        public static bool TryValidateDualRig(
            Transform targetRoot,
            RagdollAuthoredRig puppet,
            int targetLayer,
            int puppetLayer,
            out string error)
        {
            if (!targetRoot || !puppet)
            {
                error = "Both Target and Puppet are required.";
                return false;
            }
            if (targetRoot == puppet.transform
                || targetRoot.IsChildOf(puppet.transform)
                || puppet.transform.IsChildOf(targetRoot))
            {
                error = "Target and Puppet must be separate hierarchies.";
                return false;
            }
            if (targetLayer < 0 || targetLayer > 31
                || puppetLayer < 0 || puppetLayer > 31)
            {
                error = "Layer indices must be between 0 and 31.";
                return false;
            }
            if (targetLayer == puppetLayer)
            {
                error = "Target and Puppet must use different layers.";
                return false;
            }
            if (!Physics.GetIgnoreLayerCollision(targetLayer, puppetLayer))
            {
                error = "The Physics layer matrix must ignore Target/Puppet collisions.";
                return false;
            }

            HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
            for (int index = 0; index < puppet.Rigidbodies.Length; index++)
            {
                Rigidbody body = puppet.Rigidbodies[index];
                if (!body || !bodies.Add(body))
                {
                    error = "The Puppet has missing or duplicate Rigidbody ownership.";
                    return false;
                }
            }
            for (int index = 0; index < puppet.Joints.Length; index++)
            {
                ConfigurableJoint joint = puppet.Joints[index];
                if (!joint)
                {
                    error = "The Puppet has a missing ConfigurableJoint.";
                    return false;
                }
                if (joint.connectedBody && !bodies.Contains(joint.connectedBody))
                {
                    error = joint.name + " connects outside the authored Puppet.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
