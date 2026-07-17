using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Holds removed Target transforms at their committed local poses after Animator writes.
    /// It is intentionally independent from the removed RagdollAnimator registry.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class RagdollTargetAnimationBlocker : MonoBehaviour
    {
        [Serializable]
        struct Entry
        {
            internal Transform Transform;
            internal Vector3 LocalPosition;
            internal Quaternion LocalRotation;
        }

        Entry[] entries = new Entry[0];

        public int TransformCount => entries.Length;

        internal void Configure(IReadOnlyList<Transform> transforms)
        {
            if (transforms == null) throw new ArgumentNullException(nameof(transforms));
            List<Entry> resolved = new List<Entry>(
                entries.Length + transforms.Count);
            HashSet<Transform> seen = new HashSet<Transform>();
            for (int index = 0; index < entries.Length; index++)
            {
                Entry existing = entries[index];
                if (!existing.Transform || !seen.Add(existing.Transform)) continue;
                resolved.Add(existing);
            }
            for (int index = 0; index < transforms.Count; index++)
            {
                Transform value = transforms[index];
                if (!value || !seen.Add(value)) continue;
                resolved.Add(new Entry
                {
                    Transform = value,
                    LocalPosition = value.localPosition,
                    LocalRotation = value.localRotation
                });
            }
            entries = resolved.ToArray();
        }

        void LateUpdate()
        {
            for (int index = 0; index < entries.Length; index++)
            {
                Transform value = entries[index].Transform;
                if (!value) continue;
                value.localPosition = entries[index].LocalPosition;
                value.localRotation = entries[index].LocalRotation;
            }
        }
    }
}
