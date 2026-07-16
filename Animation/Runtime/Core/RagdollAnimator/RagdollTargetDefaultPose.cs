using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal struct RagdollTargetDefaultPose
    {
        readonly Vector3 localPosition;
        readonly Quaternion localRotation;

        RagdollTargetDefaultPose(
            Vector3 localPosition,
            Quaternion localRotation)
        {
            this.localPosition = localPosition;
            this.localRotation = localRotation;
        }

        internal static RagdollTargetDefaultPose Capture(Transform target)
        {
            if (!target) throw new ArgumentNullException(nameof(target));
            return new RagdollTargetDefaultPose(
                target.localPosition,
                target.localRotation);
        }

        internal void Apply(Transform target)
        {
            if (!target) throw new ArgumentNullException(nameof(target));
            target.localPosition = localPosition;
            target.localRotation = localRotation;
        }
    }
}
