using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollSupportGeometry
    {
        const float Epsilon = 0.00001f;

        // Compatibility overload: the old API remains a zero-radius world-up hull.
        public static bool Contains(Vector3 point, IReadOnlyList<Vector3> points, out float margin)
        {
            return Contains(point, points, 0f, Vector3.up, out margin);
        }

        public static bool Contains(
            Vector3 point,
            IReadOnlyList<Vector3> points,
            float supportRadius,
            Vector3 supportUp,
            out float margin)
        {
            margin = -Mathf.Max(0f, supportRadius);
            if (!RagdollLabMath.IsFinite(point) || points == null || points.Count == 0) return false;

            Vector3 up = supportUp.sqrMagnitude > Epsilon && RagdollLabMath.IsFinite(supportUp)
                ? supportUp.normalized
                : Vector3.up;
            BuildBasis(up, out Vector3 tangent, out Vector3 bitangent);
            var projected = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 supportPoint = points[i];
                if (!RagdollLabMath.IsFinite(supportPoint)) continue;
                projected.Add(new Vector2(Vector3.Dot(supportPoint, tangent), Vector3.Dot(supportPoint, bitangent)));
            }
            if (projected.Count == 0) return false;

            Vector2 projectedPoint = new(Vector3.Dot(point, tangent), Vector3.Dot(point, bitangent));
            float radius = Mathf.Max(0f, RagdollLabMath.IsFinite(supportRadius) ? supportRadius : 0f);
            if (projected.Count == 1)
            {
                margin = radius - Vector2.Distance(projectedPoint, projected[0]);
                return margin >= -Epsilon;
            }

            var hull = ConvexHull(projected);
            if (hull.Count == 1)
            {
                margin = radius - Vector2.Distance(projectedPoint, hull[0]);
                return margin >= -Epsilon;
            }
            if (hull.Count == 2)
            {
                float distance = DistanceToSegment(projectedPoint, hull[0], hull[1]);
                margin = radius - distance;
                return margin >= -Epsilon;
            }

            bool inside = IsInsideConvexHull(projectedPoint, hull, out float boundaryDistance);
            margin = inside ? radius + boundaryDistance : radius - boundaryDistance;
            return margin >= -Epsilon;
        }

        static void BuildBasis(Vector3 up, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(up, Vector3.right)) < 0.9f ? Vector3.right : Vector3.forward;
            tangent = Vector3.Cross(up, reference).normalized;
            if (tangent.sqrMagnitude < Epsilon) tangent = Vector3.Cross(up, Vector3.up).normalized;
            bitangent = Vector3.Cross(up, tangent).normalized;
        }

        static List<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            var sorted = new List<Vector2>(points);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var unique = new List<Vector2>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
                if (unique.Count == 0 || (sorted[i] - unique[unique.Count - 1]).sqrMagnitude > Epsilon * Epsilon)
                    unique.Add(sorted[i]);
            if (unique.Count <= 2) return unique;

            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < unique.Count; i++)
                {
                    Vector2 p = pass == 0 ? unique[i] : unique[unique.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= Epsilon)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }

        static bool IsInsideConvexHull(Vector2 point, IReadOnlyList<Vector2> hull, out float boundaryDistance)
        {
            bool hasSign = false, positive = true, inside = true;
            boundaryDistance = float.PositiveInfinity;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i], b = hull[(i + 1) % hull.Count];
                Vector2 edge = b - a;
                float cross = Cross(edge, point - a);
                boundaryDistance = Mathf.Min(boundaryDistance, DistanceToSegment(point, a, b));
                if (Mathf.Abs(cross) <= Epsilon) continue;
                if (!hasSign) { hasSign = true; positive = cross > 0f; }
                else if ((cross > 0f) != positive) inside = false;
            }
            return hasSign && inside;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float denominator = edge.sqrMagnitude;
            float t = denominator > Epsilon ? Mathf.Clamp01(Vector2.Dot(point - a, edge) / denominator) : 0f;
            return Vector2.Distance(point, a + edge * t);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
