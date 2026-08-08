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
    }
}
