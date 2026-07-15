using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Allocation-free recent-contact memory independent of callback execution order.</summary>
    internal struct RagdollPuppetUnmappedContactTracker
    {
        bool hasContact;
        float lastContactFixedTime;

        internal void Reset()
        {
            this = default(RagdollPuppetUnmappedContactTracker);
        }

        internal void Register(float fixedTime)
        {
            if (float.IsNaN(fixedTime) || float.IsInfinity(fixedTime)) return;

            hasContact = true;
            lastContactFixedTime = fixedTime;
        }

        internal bool IsRecent(float currentFixedTime, float fixedDeltaTime)
        {
            if (!hasContact
                || float.IsNaN(currentFixedTime)
                || float.IsInfinity(currentFixedTime))
            {
                return false;
            }

            float step = float.IsNaN(fixedDeltaTime)
                || float.IsInfinity(fixedDeltaTime)
                ? 0f
                : Mathf.Max(0f, fixedDeltaTime);
            float elapsed = Mathf.Max(0f, currentFixedTime - lastContactFixedTime);
            return elapsed <= step + 0.0001f;
        }
    }
}
