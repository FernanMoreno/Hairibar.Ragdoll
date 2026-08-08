using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Ownership record for components created by RagdollRuntimeAuthoring.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class RagdollAuthoredRig : MonoBehaviour
    {
        [SerializeField] Rigidbody[] rigidbodies = new Rigidbody[0];
        [SerializeField] Collider[] colliders = new Collider[0];
        [SerializeField] ConfigurableJoint[] joints = new ConfigurableJoint[0];
        [SerializeField] Transform[] treeParents = new Transform[0];
        [SerializeField] bool flatHierarchy;

        public Rigidbody[] Rigidbodies => rigidbodies;
        public Collider[] Colliders => colliders;
        public ConfigurableJoint[] Joints => joints;
        public bool IsFlatHierarchy => flatHierarchy;

        internal void SetOwnedComponents(
            Rigidbody[] bodies,
            Collider[] authoredColliders,
            ConfigurableJoint[] authoredJoints)
        {
            rigidbodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            colliders = authoredColliders
                ?? throw new ArgumentNullException(nameof(authoredColliders));
            joints = authoredJoints
                ?? throw new ArgumentNullException(nameof(authoredJoints));
            treeParents = new Transform[rigidbodies.Length];
            for (int index = 0; index < rigidbodies.Length; index++)
            {
                treeParents[index] = rigidbodies[index].transform.parent;
            }
        }

        /// <summary>
        /// Flattens transform parenting without changing ConfigurableJoint.connectedBody
        /// topology. World poses are preserved.
        /// </summary>
        public void SetFlatHierarchy(Transform container)
        {
            if (flatHierarchy) return;
            if (!container) throw new ArgumentNullException(nameof(container));
            for (int index = 0; index < rigidbodies.Length; index++)
            {
                if (rigidbodies[index]
                    && rigidbodies[index].transform != container)
                {
                    rigidbodies[index].transform.SetParent(container, true);
                }
            }
            flatHierarchy = true;
        }

        /// <summary>Restores the Transform parents captured during authoring.</summary>
        public void SetTreeHierarchy()
        {
            if (!flatHierarchy) return;
            if (treeParents == null || treeParents.Length != rigidbodies.Length)
            {
                throw new InvalidOperationException(
                    "The authored tree-parent snapshot is missing or incompatible.");
            }
            for (int index = 0; index < rigidbodies.Length; index++)
            {
                if (rigidbodies[index])
                    rigidbodies[index].transform.SetParent(treeParents[index], true);
            }
            flatHierarchy = false;
        }

        public void ReplaceCollider(int index, Collider replacement)
        {
            if (index < 0 || index >= colliders.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (!replacement)
            {
                throw new ArgumentNullException(nameof(replacement));
            }
            colliders[index] = replacement;
        }

        /// <summary>Returns missing, duplicate, external-joint and overlap diagnostics.</summary>
        public string[] GetDiagnostics()
        {
            List<string> issues = new List<string>();
            HashSet<Rigidbody> bodySet = new HashSet<Rigidbody>();
            for (int index = 0; index < rigidbodies.Length; index++)
            {
                Rigidbody body = rigidbodies[index];
                if (!body) issues.Add("Rigidbody entry " + index + " is missing.");
                else if (!bodySet.Add(body))
                    issues.Add("Rigidbody '" + body.name + "' is owned more than once.");
            }
            HashSet<Collider> colliderSet = new HashSet<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (!collider) issues.Add("Collider entry " + index + " is missing.");
                else if (!colliderSet.Add(collider))
                    issues.Add("Collider '" + collider.name + "' is owned more than once.");
            }
            HashSet<ConfigurableJoint> jointSet = new HashSet<ConfigurableJoint>();
            for (int index = 0; index < joints.Length; index++)
            {
                ConfigurableJoint joint = joints[index];
                if (!joint)
                {
                    issues.Add("Joint entry " + index + " is missing.");
                    continue;
                }
                if (!jointSet.Add(joint))
                    issues.Add("Joint '" + joint.name + "' is owned more than once.");
                if (joint.connectedBody && !bodySet.Contains(joint.connectedBody))
                    issues.Add("Joint '" + joint.name + "' connects outside the authored rig.");
            }
            for (int first = 0; first < colliders.Length; first++)
            {
                Collider a = colliders[first];
                if (!a || !a.enabled) continue;
                for (int second = first + 1; second < colliders.Length; second++)
                {
                    Collider b = colliders[second];
                    if (!b || !b.enabled || !a.bounds.Intersects(b.bounds)) continue;
                    issues.Add("Collider bounds overlap: '" + a.name + "' and '"
                        + b.name + "'. Verify the shapes in Scene view.");
                }
            }
            return issues.ToArray();
        }
    }
}
