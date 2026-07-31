using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollBakerPlayModeTests
    {
        [UnityTest]
        public IEnumerator AnimationClipBatch_UsesExactManualSampleTimesAtZeroTimeScale()
        {
            GameObject root = new GameObject("Batch Baker");
            GameObject child = new GameObject("Child");
            AnimationClip clip = CreatePositionClip(false);
            float originalTimeScale = Time.timeScale;
            try
            {
                child.transform.SetParent(root.transform, false);
                root.AddComponent<Animator>();
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.AnimationClips;
                baker.frameRate = 30;
                baker.animationClips = new[] { clip };
                List<float> deltas = new List<float>();
                List<float> positions = new List<float>();
                baker.SampleRequested += (source, delta) =>
                {
                    deltas.Add(delta);
                    positions.Add(child.transform.localPosition.x);
                };

                Time.timeScale = 0f;
                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                while (baker.IsBaking) yield return null;

                Assert.That(
                    baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Succeeded));
                Assert.That(deltas.Count, Is.EqualTo(3));
                Assert.That(deltas[0], Is.Zero);
                Assert.That(deltas[1], Is.EqualTo(1f / 30f).Within(0.0001f));
                Assert.That(deltas[2], Is.EqualTo(0.05f - (1f / 30f)).Within(0.0001f));
                Assert.That(positions[0], Is.EqualTo(0f).Within(0.001f));
                Assert.That(positions[2], Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator LegacyBatch_SamplesLegacyClipWithoutAnimator()
        {
            GameObject root = new GameObject("Legacy Baker");
            GameObject child = new GameObject("Child");
            AnimationClip clip = CreatePositionClip(true);
            try
            {
                child.transform.SetParent(root.transform, false);
                root.AddComponent<UnityEngine.Animation>();
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.AnimationClips;
                baker.frameRate = 20;
                baker.animationClips = new[] { clip };
                List<float> positions = new List<float>();
                baker.SampleRequested += (source, delta) =>
                    positions.Add(child.transform.localPosition.x);

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                while (baker.IsBaking) yield return null;

                Assert.That(
                    baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Succeeded));
                Assert.That(positions.Count, Is.EqualTo(2));
                Assert.That(positions[0], Is.EqualTo(0f).Within(0.001f));
                Assert.That(positions[1], Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator AnimationClipBatch_IsIndependentFromSupportedTimeScales()
        {
            float[] scales = { 0.5f, 1f, 2f };
            float originalTimeScale = Time.timeScale;
            try
            {
                for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
                {
                    GameObject root = new GameObject("TimeScale Baker");
                    GameObject child = new GameObject("Child");
                    AnimationClip clip = CreatePositionClip(false);
                    try
                    {
                        child.transform.SetParent(root.transform, false);
                        root.AddComponent<Animator>();
                        RagdollGenericBaker baker =
                            root.AddComponent<RagdollGenericBaker>();
                        baker.mode = RagdollBakerMode.AnimationClips;
                        baker.frameRate = 30;
                        baker.animationClips = new[] { clip };
                        List<float> deltas = new List<float>();
                        List<float> positions = new List<float>();
                        baker.SampleRequested += (source, delta) =>
                        {
                            deltas.Add(delta);
                            positions.Add(child.transform.localPosition.x);
                        };

                        Time.timeScale = scales[scaleIndex];
                        string error;
                        Assert.That(baker.StartBaking(out error), Is.True, error);
                        while (baker.IsBaking) yield return null;

                        Assert.That(deltas.Count, Is.EqualTo(3));
                        Assert.That(deltas[0], Is.Zero);
                        Assert.That(positions[0], Is.EqualTo(0f).Within(0.001f));
                        Assert.That(positions[2], Is.EqualTo(1f).Within(0.001f));
                    }
                    finally
                    {
                        Object.DestroyImmediate(clip);
                        Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        static AnimationClip CreatePositionClip(bool legacy)
        {
            AnimationClip clip = new AnimationClip
            {
                legacy = legacy,
                frameRate = 20f
            };
            clip.SetCurve(
                "Child",
                typeof(Transform),
                "m_LocalPosition.x",
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.05f, 1f)));
            return clip;
        }
    }
}
