using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollSupportGeometry
    {
        public static bool Contains(Vector3 point, IReadOnlyList<Vector3> points, out float margin)
        {
            margin = 0f;
            if (points == null || points.Count < 3) return false;
            var hull = ConvexHull(points);
            bool hasSign = false, positive = true; float best = float.PositiveInfinity;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector3 a = hull[i], b = hull[(i + 1) % hull.Count];
                Vector3 edge = b - a; Vector3 toPoint = point - a;
                float cross = edge.x * toPoint.z - edge.z * toPoint.x;
                if (Mathf.Abs(cross) > 0.0001f && !hasSign) { hasSign = true; positive = cross > 0f; }
                else if (hasSign && ((cross > 0f) != positive)) { margin = -best; return false; }
                best = Mathf.Min(best, Mathf.Abs(cross) / Mathf.Max(edge.magnitude, 0.0001f));
            }
            margin = hasSign ? best : 0f;
            return hasSign;
        }

        static List<Vector3> ConvexHull(IReadOnlyList<Vector3> points)
        {
            var sorted = new List<Vector3>(points); sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            var hull = new List<Vector3>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < sorted.Count; i++)
                {
                    Vector3 p = pass == 0 ? sorted[i] : sorted[sorted.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }
        static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
    }
}
