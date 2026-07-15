using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Allocation-free distance-band selection with symmetric hysteresis.</summary>
    internal static class RagdollPhysicsQualitySelector
    {
        internal static int Evaluate(
            float squaredDistance,
            int currentLevel,
            float[] minimumDistances,
            float hysteresis)
        {
            if (minimumDistances == null || minimumDistances.Length == 0)
            {
                return -1;
            }

            float distanceSquared = Mathf.Max(0f, squaredDistance);
            float margin = Mathf.Max(0f, hysteresis);

            if (currentLevel < 0 || currentLevel >= minimumDistances.Length)
            {
                return FindRawLevel(distanceSquared, minimumDistances);
            }

            int resolved = currentLevel;
            while (resolved + 1 < minimumDistances.Length)
            {
                float boundary = minimumDistances[resolved + 1] + margin;
                if (distanceSquared < boundary * boundary) break;
                resolved++;
            }

            while (resolved > 0)
            {
                float boundary = Mathf.Max(
                    0f,
                    minimumDistances[resolved] - margin);
                if (distanceSquared >= boundary * boundary) break;
                resolved--;
            }

            return resolved;
        }

        static int FindRawLevel(
            float squaredDistance,
            float[] minimumDistances)
        {
            int result = 0;
            for (int index = 1; index < minimumDistances.Length; index++)
            {
                float boundary = minimumDistances[index];
                if (squaredDistance < boundary * boundary) break;
                result = index;
            }

            return result;
        }
    }
}
