using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab.Tests
{
    public sealed class RagdollLabMathTests
    {
        [Test] public void QuaternionAngleUsesGeodesicDistance()
        {
            Assert.That(RagdollLabMath.QuaternionAngle(Quaternion.identity, Quaternion.Euler(0f, 90f, 0f)), Is.EqualTo(90f).Within(0.001f));
        }

        [Test] public void RmsAndPercentileAreDeterministic()
        {
            var values = new List<float> { 1f, 2f, 3f, 4f };
            Assert.That(RagdollLabMath.Rms(values), Is.EqualTo(Mathf.Sqrt(7.5f)).Within(0.001f));
            Assert.That(RagdollLabMath.Percentile(values, 0.5f), Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test] public void StableIdDoesNotUseInstanceId()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Body");
            child.transform.SetParent(root.transform);
            string id = RagdollTelemetryRecorder.StableId(child.transform, "Rigidbody");
            Assert.That(id, Is.EqualTo("Rigidbody:Root/Body"));
            Object.DestroyImmediate(root);
        }

        [Test] public void ZeroCrossingsAndSettlingAreStable()
        {
            var signal = new List<float> { -1f, 1f, -1f, 1f, 0.01f, 0.001f };
            Assert.That(RagdollLabMath.ZeroCrossings(signal, 0.02f), Is.EqualTo(3));
            Assert.That(RagdollLabMath.SettlingTime(new List<float> { 1f, 0.5f, 0.01f, 0f }, 0.1f, 0f, 0.02f), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test] public void SyntheticSineFrequencyIsApproximatelyDetected()
        {
            var signal = new List<float>();
            for (int i = 0; i < 100; i++) signal.Add(Mathf.Sin(2f * Mathf.PI * 5f * i / 100f));
            Assert.That(RagdollLabMath.DominantFrequencyByZeroCrossings(signal, 100f), Is.EqualTo(5f).Within(0.2f));
        }

        [Test] public void FreeUnconnectedRootJointDoesNotManufactureAnchorDrift()
        {
            var root = new GameObject("FreeRoot");
            root.transform.position = new Vector3(2f, 1f, -3f);
            root.AddComponent<Rigidbody>();
            ConfigurableJoint joint = root.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = Vector3.zero;
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            Assert.That(RagdollLabMath.JointAnchorError(joint), Is.EqualTo(0f).Within(0.000001f));
            Object.DestroyImmediate(root);
        }

        [Test] public void GroundSupportNormalAcceptsGroundAndRejectsWall()
        {
            bool ground = RagdollLabMath.IsGroundSupportNormal(Vector3.up, Vector3.up, 45f, out float groundDot);
            bool wall = RagdollLabMath.IsGroundSupportNormal(Vector3.right, Vector3.up, 45f, out float wallDot);

            Assert.That(ground, Is.True);
            Assert.That(groundDot, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(wall, Is.False);
            Assert.That(wallDot, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test] public void GroundSupportNormalRejectsInvalidInput()
        {
            bool result = RagdollLabMath.IsGroundSupportNormal(
                new Vector3(float.NaN, 0f, 0f), Vector3.up, 45f, out float dot);

            Assert.That(result, Is.False);
            Assert.That(dot, Is.EqualTo(-1f));
        }

        [Test] public void BaselineAveragesSamplesBeforeTheEvent()
        {
            var values = new List<float> { 1f, 1f, 1f, 5f, 5f, 5f };
            Assert.That(RagdollLabMath.Baseline(values, eventIndex: 3, lookbackFrames: 3), Is.EqualTo(1f).Within(0.001f));
        }

        [Test] public void BaselineAtFrameZeroHasNoAntecedentAndReturnsThatSample()
        {
            var values = new List<float> { 2.5f, 9f, 9f };
            Assert.That(RagdollLabMath.Baseline(values, eventIndex: 0, lookbackFrames: 5), Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test] public void PeakAfterFindsMaximumAndItsIndexWithinRange()
        {
            var values = new List<float> { 0f, 1f, 3f, 2f, 0f };
            (int index, float value) peak = RagdollLabMath.PeakAfter(values, 0, 5);
            Assert.That(peak.index, Is.EqualTo(2));
            Assert.That(peak.value, Is.EqualTo(3f).Within(0.001f));
        }

        [Test] public void SampleAtOffsetPicksTheFrameClosestToTheRequestedTime()
        {
            var values = new List<float> { 0f, 10f, 20f, 30f };
            Assert.That(RagdollLabMath.SampleAtOffset(values, dt: 0.1f, eventIndex: 0, offsetSeconds: 0.2f), Is.EqualTo(20f).Within(0.001f));
        }

        [Test] public void SampleAtOffsetClampsToAvailableData()
        {
            var values = new List<float> { 0f, 10f, 20f };
            Assert.That(RagdollLabMath.SampleAtOffset(values, dt: 0.1f, eventIndex: 0, offsetSeconds: 10f), Is.EqualTo(20f).Within(0.001f));
        }

        [Test] public void AreaUnderCurveIntegratesTrapezoidally()
        {
            var values = new List<float> { 0f, 2f, 0f };
            Assert.That(RagdollLabMath.AreaUnderCurve(values, dt: 1f, 0, 3), Is.EqualTo(2f).Within(0.001f));
        }

        [Test] public void TimeAboveThresholdCountsOnlyExceedingFrames()
        {
            var values = new List<float> { 0f, 5f, 5f, 0f };
            Assert.That(RagdollLabMath.TimeAboveThreshold(values, dt: 0.1f, 0, 4, threshold: 1f), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test] public void TwoFootSupportUsesSegmentAndReturnsFiniteMargin()
        {
            var feet = new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f) };

            bool inside = RagdollSupportGeometry.Contains(Vector3.zero, feet, 0.15f, Vector3.up, out float margin);

            Assert.That(inside, Is.True);
            Assert.That(margin, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(RagdollLabMath.IsFinite(margin), Is.True);
        }

        [Test] public void OneFootSupportUsesConfiguredRadius()
        {
            bool inside = RagdollSupportGeometry.Contains(new Vector3(0.1f, 0f, 0f),
                new List<Vector3> { Vector3.zero }, 0.15f, Vector3.up, out float margin);

            Assert.That(inside, Is.True);
            Assert.That(margin, Is.EqualTo(0.05f).Within(0.001f));
        }

        [Test] public void ThreePointSupportUsesProjectedConvexHull()
        {
            var triangle = new List<Vector3>
            {
                new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(0f, 0f, 0.5f),
            };

            bool inside = RagdollSupportGeometry.Contains(new Vector3(0f, 0f, -0.1f), triangle, 0f, Vector3.up, out float margin);

            Assert.That(inside, Is.True);
            Assert.That(margin, Is.GreaterThan(0f));
        }

        [Test] public void NoSupportPointsFailsClosedWithoutPositiveMargin()
        {
            bool inside = RagdollSupportGeometry.Contains(Vector3.zero, new List<Vector3>(), 0.15f, Vector3.up, out float margin);

            Assert.That(inside, Is.False);
            Assert.That(margin, Is.LessThanOrEqualTo(0f));
        }

        [Test] public void OutsideSupportHullReturnsFiniteNegativeMargin()
        {
            var feet = new List<Vector3>
            {
                new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(0f, 0f, 0.5f),
            };

            bool inside = RagdollSupportGeometry.Contains(new Vector3(2f, 0f, 2f), feet,
                0.15f, Vector3.up, out float margin);

            Assert.That(inside, Is.False);
            Assert.That(margin, Is.LessThan(0f));
            Assert.That(RagdollLabMath.IsFinite(margin), Is.True);
        }

        [Test] public void SchemaAndThresholdDefaultsAreExplicitAndFinite()
        {
            Assert.That(RagdollLabSchema.Version, Is.EqualTo("1.6.0"));
            var thresholds = ScriptableObject.CreateInstance<RagdollLabThresholds>();

            Assert.That(thresholds.shortContactDurationSeconds, Is.EqualTo(0.1f));
            Assert.That(thresholds.supportRadiusMeters, Is.EqualTo(0.15f));
            Assert.That(thresholds.maximumGroundAngle, Is.EqualTo(45f));
            Assert.That(thresholds.fallHeightMeters, Is.EqualTo(0.35f));
            Assert.That(RagdollLabMath.IsFinite(thresholds.shortContactDurationSeconds), Is.True);
            Object.DestroyImmediate(thresholds);
        }

        [Test] public void SupportAndFallClassificationIsTranslationInvariant()
        {
            Quaternion root = Quaternion.identity;
            bool uprightAtZero = RagdollLabMath.IsLikelyFallen(new Vector3(0f, 1f, 0f), root, 2,
                Vector3.zero, Vector3.up, 0.35f);
            bool uprightAtPlatform = RagdollLabMath.IsLikelyFallen(new Vector3(0f, 11f, 0f), root, 2,
                new Vector3(0f, 10f, 0f), Vector3.up, 0.35f);
            bool fallenAtPlatform = RagdollLabMath.IsLikelyFallen(new Vector3(0f, 10.1f, 0f), root, 0,
                new Vector3(0f, 10f, 0f), Vector3.up, 0.35f);

            Assert.That(uprightAtZero, Is.False);
            Assert.That(uprightAtPlatform, Is.False);
            Assert.That(fallenAtPlatform, Is.True);
        }

        [Test] public void FallOrientationUsesEffectiveSupportUp()
        {
            Vector3 inclinedUp = new Vector3(0f, 1f, 1f).normalized;
            Quaternion rootRotation = Quaternion.FromToRotation(Vector3.up, inclinedUp);

            bool fallen = RagdollLabMath.IsLikelyFallen(inclinedUp, rootRotation, 2,
                Vector3.zero, inclinedUp, 0.35f);

            Assert.That(fallen, Is.False);
        }

        [Test]
        public void SupportGeometry_RotatedPlaneMatchesWorldUpReference()
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.right);
            var reference = new List<Vector3>
            {
                new(-0.5f, 0f, -0.3f), new(0.5f, 0f, -0.3f), new(0f, 0f, 0.5f),
            };
            var rotated = new List<Vector3>();
            for (int i = 0; i < reference.Count; i++) rotated.Add(rotation * reference[i]);

            bool referenceInside = RagdollSupportGeometry.Contains(
                Vector3.zero, reference, 0.1f, Vector3.up, out float referenceMargin);
            bool rotatedInside = RagdollSupportGeometry.Contains(
                rotation * Vector3.zero, rotated, 0.1f, Vector3.right, out float rotatedMargin);

            Assert.That(rotatedInside, Is.EqualTo(referenceInside));
            Assert.That(rotatedMargin, Is.EqualTo(referenceMargin).Within(0.0001f));
        }

        [Test]
        public void SupportGeometry_CollinearAndReorderedContactsRemainFiniteAndStable()
        {
            var ordered = new List<Vector3>
            {
                new(-1f, 0f, 0f), Vector3.zero, new(1f, 0f, 0f),
            };
            var reversed = new List<Vector3>
            {
                new(1f, 0f, 0f), Vector3.zero, new(-1f, 0f, 0f),
            };

            bool firstInside = RagdollSupportGeometry.Contains(
                new Vector3(0f, 0f, 0.2f), ordered, 0.1f, Vector3.up, out float firstMargin);
            bool secondInside = RagdollSupportGeometry.Contains(
                new Vector3(0f, 0f, 0.2f), reversed, 0.1f, Vector3.up, out float secondMargin);

            Assert.That(firstInside, Is.EqualTo(secondInside));
            Assert.That(secondMargin, Is.EqualTo(firstMargin).Within(0.0001f));
            Assert.That(RagdollLabMath.IsFinite(firstMargin), Is.True);
        }

        [Test]
        public void SupportGeometry_NonFinitePointIsUnavailableWithoutNaNMargin()
        {
            bool inside = RagdollSupportGeometry.Contains(
                new Vector3(float.NaN, 0f, 0f),
                new List<Vector3> { Vector3.zero },
                0.1f,
                Vector3.up,
                out float margin);

            Assert.That(inside, Is.False);
            Assert.That(RagdollLabMath.IsFinite(margin), Is.True);
            Assert.That(margin, Is.LessThanOrEqualTo(0f));
        }
    }
}
