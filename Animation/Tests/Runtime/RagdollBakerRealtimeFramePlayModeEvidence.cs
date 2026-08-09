using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// PlayMode companion evidence for I07. The stable capability ID remains on the
    /// EditMode policy/asset test; this case proves the rendered-frame boundary.
    /// </summary>
    public sealed class RagdollBakerRealtimeFramePlayModeEvidence
    {
        [UnityTest]
        public IEnumerator RealtimeSamplesAtMostOncePerRenderedFrame()
        {
            GameObject root = new GameObject("Realtime rendered-frame evidence");
            try
            {
                RagdollGenericBaker baker =
                    root.AddComponent<RagdollGenericBaker>();
                baker.root = root.transform;
                baker.mode = RagdollBakerMode.Realtime;
                baker.frameRate = 1000;
                baker.saveToFolder = "Assets";
                List<float> samples = new List<float>();
                baker.SampleRequested += (_, elapsed) => samples.Add(elapsed);
                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                Assert.That(samples, Is.EqualTo(new[] { 0f }));

                for (int frame = 0; frame < 8; frame++)
                {
                    int before = samples.Count;
                    yield return null;
                    Assert.That(samples.Count - before, Is.InRange(0, 1),
                        "Realtime emitted catch-up samples for one rendered pose.");
                }

                Assert.That(samples.Count, Is.GreaterThan(1));
                for (int index = 1; index < samples.Count; index++)
                    Assert.That(samples[index], Is.GreaterThan(0f));
                baker.CancelBaking();
                Assert.That(baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Canceled));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
