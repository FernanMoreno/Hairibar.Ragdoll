using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollSupportContextStressTests
    {
        [Test]
        public void EffectiveUpMatrix_PreservesSupportMarginAcrossFourDirections()
        {
            var localSupport = new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.3f),
                new Vector3(0.5f, 0f, -0.3f),
                new Vector3(0f, 0f, 0.5f)
            };
            Vector3 localPoint = new Vector3(0f, 0f, -0.1f);
            const float supportRadius = 0.1f;
            Vector3[] supportUps =
            {
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                new Vector3(1f, 2f, 3f).normalized
            };

            Assert.That(
                RagdollSupportGeometry.Contains(
                    localPoint, localSupport, supportRadius, Vector3.up, out float referenceMargin),
                Is.True);

            for (int i = 0; i < supportUps.Length; i++)
            {
                Vector3 supportUp = supportUps[i];
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, supportUp);
                var rotatedSupport = new List<Vector3>(localSupport.Count);
                for (int j = 0; j < localSupport.Count; j++)
                    rotatedSupport.Add(rotation * localSupport[j]);

                bool inside = RagdollSupportGeometry.Contains(
                    rotation * localPoint,
                    rotatedSupport,
                    supportRadius,
                    supportUp,
                    out float margin);
                bool validGround = RagdollLabMath.IsGroundSupportNormal(
                    supportUp, supportUp, 45f, out float dot);

                Assert.That(inside, Is.True, $"effective-up cell {i}");
                Assert.That(IsFinite(margin), Is.True, $"margin cell {i}");
                Assert.That(margin, Is.EqualTo(referenceMargin).Within(0.0001f), $"margin cell {i}");
                Assert.That(validGround, Is.True, $"normal cell {i}");
                Assert.That(IsFinite(dot), Is.True, $"dot cell {i}");
            }

            TestContext.WriteLine(
                $"Support context EffectiveUp: cells={supportUps.Length}, marginDelta=0, finite=true");
        }

        [Test]
        public void SlopeBoundaryMatrix_IsDeterministic()
        {
            float[] angles = { 44.9f, 45f, 45.1f };
            int accepted = 0;

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 normal = i == 1
                    ? new Vector3(1f, 1f, 0f).normalized
                    : Quaternion.AngleAxis(angles[i], Vector3.forward) * Vector3.up;
                bool isSupport = RagdollLabMath.IsGroundSupportNormal(
                    normal, Vector3.up, 45f, out float dot);

                Assert.That(IsFinite(dot), Is.True, $"slope dot cell {i}");
                Assert.That(isSupport, Is.EqualTo(i < 2), $"slope cell {i}");
                if (isSupport) accepted++;
            }

            Assert.That(accepted, Is.EqualTo(2));
            TestContext.WriteLine(
                $"Support context SlopeBoundary: cells={angles.Length}, accepted={accepted}, rejected={angles.Length - accepted}, finite=true");
        }

        [Test]
        public void SupportShapeMatrix_ReportsFinitePointSegmentEdgeAndEmptyResults()
        {
            bool pointInside = RagdollSupportGeometry.Contains(
                new Vector3(0.14f, 0f, 0f),
                new List<Vector3> { Vector3.zero },
                0.15f,
                Vector3.up,
                out float pointMargin);
            bool segmentEdgeInside = RagdollSupportGeometry.Contains(
                new Vector3(0.65f, 0f, 0f),
                new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f) },
                0.15f,
                Vector3.up,
                out float segmentEdgeMargin);
            bool segmentOutside = RagdollSupportGeometry.Contains(
                new Vector3(0.7f, 0f, 0f),
                new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f) },
                0.15f,
                Vector3.up,
                out float segmentOutsideMargin);
            bool emptyInside = RagdollSupportGeometry.Contains(
                Vector3.zero,
                new List<Vector3>(),
                0.2f,
                Vector3.up,
                out float emptyMargin);

            Assert.That(pointInside, Is.True);
            Assert.That(pointMargin, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(segmentEdgeInside, Is.True);
            Assert.That(segmentEdgeMargin, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(segmentOutside, Is.False);
            Assert.That(segmentOutsideMargin, Is.EqualTo(-0.05f).Within(0.0001f));
            Assert.That(emptyInside, Is.False);
            Assert.That(emptyMargin, Is.EqualTo(-0.2f).Within(0.0001f));

            Assert.That(IsFinite(pointMargin), Is.True);
            Assert.That(IsFinite(segmentEdgeMargin), Is.True);
            Assert.That(IsFinite(segmentOutsideMargin), Is.True);
            Assert.That(IsFinite(emptyMargin), Is.True);
            TestContext.WriteLine("Support context SupportShapes: cells=3, finite=true");
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
