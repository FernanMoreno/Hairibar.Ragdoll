using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBakerTests
    {
        [Test]
        public void Realtime_StartAndStopEmitSegmentAndSamples()
        {
            GameObject root = new GameObject("Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                baker.saveName = "Physics Capture";
                int started = 0;
                int sampled = 0;
                int finished = 0;
                baker.SegmentStarted += (source, name, clip) =>
                {
                    started++;
                    Assert.That(name, Is.EqualTo("Physics Capture"));
                };
                baker.SampleRequested += (source, deltaTime) => sampled++;
                baker.SegmentFinished += (source, name, clip) => finished++;

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                baker.StopBaking();

                Assert.That(started, Is.EqualTo(1));
                Assert.That(sampled, Is.EqualTo(1));
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(baker.IsBaking, Is.False);
                Assert.That(
                    baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Succeeded));
                Assert.That(baker.LastResult.CompletedSegments, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Realtime_CancelReportsCanceledAndCleansActiveSegment()
        {
            GameObject root = new GameObject("Canceled Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                int finished = 0;
                int canceled = 0;
                baker.SegmentFinished += (source, name, clip) => finished++;
                baker.SegmentCanceled += (source, name, clip) => canceled++;

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                baker.CancelBaking();

                Assert.That(baker.IsBaking, Is.False);
                Assert.That(baker.IsSegmentActive, Is.False);
                Assert.That(
                    baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Canceled));
                Assert.That(baker.LastResult.CompletedSegments, Is.Zero);
                Assert.That(finished, Is.Zero);
                Assert.That(canceled, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RecordingCommitFailure_IsPublishedAsFailedBeforeCompletion()
        {
            GameObject root = new GameObject("Failed commit Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                RagdollBakerResult observed = default;
                int commitRequests = 0;
                baker.RecordingCommitRequested += source =>
                {
                    commitRequests++;
                    return "Synthetic destination commit failure.";
                };
                baker.BakingCompleted += (source, result) => observed = result;

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                baker.StopBaking();

                Assert.That(commitRequests, Is.EqualTo(1));
                Assert.That(baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Failed));
                Assert.That(observed.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Failed));
                Assert.That(observed.Error, Does.Contain("Synthetic"));
                Assert.That(observed.CompletedSegments, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Cancellation_DoesNotRequestDestinationCommit()
        {
            GameObject root = new GameObject("Canceled commit Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                int commitRequests = 0;
                baker.RecordingCommitRequested += source =>
                {
                    commitRequests++;
                    return string.Empty;
                };

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                baker.CancelBaking();

                Assert.That(commitRequests, Is.Zero);
                Assert.That(baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Canceled));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Realtime_DisableCancelsInsteadOfCommittingRecording()
        {
            GameObject root = new GameObject("Disabled Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                baker.enabled = false;

                Assert.That(baker.IsBaking, Is.False);
                Assert.That(
                    baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.Canceled));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Realtime_DroppedFrameEmitsOneSampleWithActualElapsedTime()
        {
            GameObject root = new GameObject("Realtime Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                baker.frameRate = 30;
                int samples = 0;
                float lastDelta = -1f;
                baker.SampleRequested += (source, deltaTime) =>
                {
                    samples++;
                    lastDelta = deltaTime;
                };

                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);
                Assert.That(samples, Is.EqualTo(1), "The initial t=0 sample is required.");

                Assert.That(baker.AdvanceRealtimeSampling(0.1f), Is.True);
                Assert.That(samples, Is.EqualTo(2));
                Assert.That(lastDelta, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(baker.AdvanceRealtimeSampling(0f), Is.False);
                Assert.That(samples, Is.EqualTo(2));
                baker.StopBaking();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealtimeSamplingPath_DoesNotAllocateAfterWarmup()
        {
            GameObject root = new GameObject("Allocation-free realtime Baker");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                baker.frameRate = 60;
                baker.SampleRequested += IgnoreSample;
                string error;
                Assert.That(baker.StartBaking(out error), Is.True, error);

                for (int index = 0; index < 32; index++)
                    baker.AdvanceRealtimeSampling(1f / 60f);

                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < 4096; index++)
                    baker.AdvanceRealtimeSampling(1f / 60f);
                long allocated = System.GC.GetAllocatedBytesForCurrentThread()
                    - before;

                baker.StopBaking();
                Assert.That(allocated, Is.Zero,
                    "The Baker sampling/event path allocated after warm-up.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void IgnoreSample(RagdollBaker source, float deltaTime)
        {
        }

        [Test]
        public void AnimationClips_RejectsMissingRequiredPlaybackComponent()
        {
            GameObject root = new GameObject("Invalid Baker");
            AnimationClip clip = new AnimationClip();
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.AnimationClips;
                baker.animationClips = new[] { clip };

                string error;
                Assert.That(baker.StartBaking(out error), Is.False);
                Assert.That(error, Does.Contain("Animator"));
                Assert.That(baker.IsBaking, Is.False);
                Assert.That(
                    baker.LastResult.Status,
                    Is.EqualTo(RagdollBakerCompletionStatus.None));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AnimationClips_RejectsNullClipInsteadOfFinishingEmpty()
        {
            GameObject root = new GameObject("Null Clip Baker");
            try
            {
                root.AddComponent<Animator>();
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.AnimationClips;
                baker.animationClips = new AnimationClip[] { null };

                string error;
                Assert.That(baker.StartBaking(out error), Is.False);
                Assert.That(error, Does.Contain("null"));
                Assert.That(baker.IsBaking, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(0, "Assets")]
        [TestCase(-30, "Assets")]
        [TestCase(30, "AssetsSibling")]
        [TestCase(30, "Assets/../Outside")]
        public void StartBaking_RejectsInvalidRateAndDestination(
            int rate,
            string destination)
        {
            GameObject root = new GameObject("Invalid Baker configuration");
            try
            {
                RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                baker.frameRate = rate;
                baker.saveToFolder = destination;

                string error;
                Assert.That(baker.StartBaking(out error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(baker.IsBaking, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
