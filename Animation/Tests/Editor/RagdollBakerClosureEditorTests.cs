using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    /// <summary>
    /// ID-stable Baker closure evidence. Every recording case creates and inspects
    /// the resulting .anim asset through the production Baker session transaction.
    /// </summary>
    public sealed class RagdollBakerClosureEditorTests
    {
        const string Folder = "Assets/__HairibarBakerClosureTests";
        List<Object> transient = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__HairibarBakerClosureTests");
        }

        [TearDown]
        public void TearDown()
        {
            if (transient != null)
            {
                for (int index = transient.Count - 1; index >= 0; index--)
                    if (transient[index]) Object.DestroyImmediate(transient[index]);
                transient.Clear();
            }
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void I01_BakerCommitsInspectableAssetAndCancellationLeavesDestinationIntact()
        {
            const string path = Folder + "/Transactional.anim";
            AnimationClip authored = new AnimationClip { name = "Transactional" };
            SetPositionCurve(authored, string.Empty, 4f, 4f, 1f);
            AnimationClipSettings authoredSettings =
                AnimationUtility.GetAnimationClipSettings(authored);
            authoredSettings.cycleOffset = 0.31f;
            AnimationUtility.SetAnimationClipSettings(authored, authoredSettings);
            AssetDatabase.CreateAsset(authored, path);
            AssetDatabase.SaveAssets();
            AnimationClip identity = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            GameObject root = Own(new GameObject("Transactional Baker"));
            RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
            ConfigureRealtime(baker, "Transactional");
            baker.bakePositionList = new[] { root.transform };
            RagdollBakerSessionManager.Start(baker);
            root.transform.localPosition = Vector3.right * 9f;
            baker.AdvanceRealtimeSampling(1f / baker.frameRate);
            baker.CancelBaking();

            AnimationClip afterCancel =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(afterCancel, Is.SameAs(identity));
            Assert.That(PositionCurve(afterCancel).Evaluate(0.5f), Is.EqualTo(4f));
            Assert.That(baker.LastResult.Status,
                Is.EqualTo(RagdollBakerCompletionStatus.Canceled));
            Assert.That(baker.LastResult.CompletedSegments, Is.Zero);

            root.transform.localPosition = Vector3.right * 2f;
            RagdollBakerSessionManager.Start(baker);
            root.transform.localPosition = Vector3.right * 7f;
            baker.AdvanceRealtimeSampling(1f / baker.frameRate);
            baker.StopBaking();

            AnimationClip committed =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(committed, Is.SameAs(identity),
                "Overwrite must retain the destination asset identity.");
            Assert.That(PositionCurve(committed), Is.Not.Null);
            Assert.That(PositionCurve(committed).keys[0].value,
                Is.EqualTo(2f).Within(0.001f));
            Assert.That(PositionCurve(committed).keys[^1].value,
                Is.EqualTo(7f).Within(0.001f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(committed)
                .cycleOffset, Is.EqualTo(0.31f).Within(0.0001f));
            Assert.That(baker.LastResult.Status,
                Is.EqualTo(RagdollBakerCompletionStatus.Succeeded));
            Assert.That(baker.LastResult.CompletedSegments, Is.EqualTo(1));
        }

        [Test]
        public void I03_GenericAndLegacyBatchOutputsPreserveSettingsAndLoopSeam()
        {
            AnimationClip genericSource = CreateSourceClip(false, "GenericSource");
            AnimationClip legacySource = CreateSourceClip(true, "LegacySource");
            BakeClip(genericSource, false, "GenericResult");
            BakeClip(legacySource, true, "LegacyResult");

            AssertLoopOutput(Folder + "/GenericResult.anim", false, 0.47f);
            AssertLoopOutput(Folder + "/LegacyResult.anim", true, 0.47f);
        }

        [Test]
        public void I04_AnimationClipBatchWritesExactManualTimesWithoutOvershoot()
        {
            AnimationClip source = new AnimationClip
            {
                name = "ExactBatchSource",
                frameRate = 20f
            };
            source.SetCurve("Child", typeof(Transform), "m_LocalPosition.x",
                new AnimationCurve(new Keyframe(0f, 0f),
                    new Keyframe(0.075f, 3f)));
            Own(source);

            GameObject root = Own(new GameObject("Exact Batch Baker"));
            GameObject child = new GameObject("Child");
            child.transform.SetParent(root.transform, false);
            root.AddComponent<Animator>();
            RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
            baker.root = root.transform;
            baker.bakePositionList = new[] { child.transform };
            baker.mode = RagdollBakerMode.AnimationClips;
            baker.animationClips = new[] { source };
            baker.frameRate = 20;
            baker.keyReductionError = 0f;
            baker.appendName = "_Baked";
            baker.saveToFolder = Folder;
            List<float> deltas = new List<float>();
            List<float> poses = new List<float>();
            baker.SampleRequested += (_, delta) =>
            {
                deltas.Add(delta);
                poses.Add(child.transform.localPosition.x);
            };

            string error;
            Assert.That(RagdollBakerSessionManager.RunBatchImmediately(
                baker, out error), Is.True, error);

            Assert.That(deltas.Count, Is.EqualTo(3));
            Assert.That(deltas[0], Is.Zero);
            Assert.That(deltas[1], Is.EqualTo(0.05f).Within(0.00001f));
            Assert.That(deltas[2], Is.EqualTo(0.025f).Within(0.00001f));
            Assert.That(poses[0], Is.EqualTo(0f).Within(0.001f));
            Assert.That(poses[^1], Is.EqualTo(3f).Within(0.001f));
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                Folder + "/ExactBatchSource_Baked.anim");
            Assert.That(output, Is.Not.Null);
            AnimationCurve outputCurve = AnimationUtility.GetEditorCurve(output,
                EditorCurveBinding.FloatCurve("Child", typeof(Transform),
                    "m_LocalPosition.x"));
            Assert.That(outputCurve, Is.Not.Null);
            Assert.That(outputCurve.keys[0].time, Is.Zero);
            Assert.That(outputCurve.keys[^1].time,
                Is.EqualTo(source.length).Within(0.00001f));
            Keyframe[] outputKeys = outputCurve.keys;
            for (int index = 0; index < outputKeys.Length; index++)
                Assert.That(outputKeys[index].time,
                    Is.LessThanOrEqualTo(source.length + 0.000001f));
        }

        [Test]
        public void I05_AnimationStatesRecordsRealBaseLayerAndRejectsLegacy()
        {
            string controllerPath = Folder + "/StateController.controller";
            string sourcePath = Folder + "/StateMotion.anim";
            AnimationClip motion = CreateSourceClip(false, "StateMotion");
            AssetDatabase.CreateAsset(motion, sourcePath);
            transient.Remove(motion);
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.layers[0].stateMachine.AddState("Fall").motion = motion;
            AssetDatabase.SaveAssets();
            GameObject root = Own(new GameObject("Animation States Baker"));
            GameObject child = new GameObject("Child");
            child.transform.SetParent(root.transform, false);
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
            baker.root = root.transform;
            baker.bakePositionList = new[] { child.transform };
            baker.mode = RagdollBakerMode.AnimationStates;
            baker.animationStates = new[] { "Fall" };
            baker.appendName = "_StateBaked";
            baker.saveToFolder = Folder;
            string error;
            Assert.That(RagdollBakerSessionManager.RunBatchImmediately(
                baker, out error), Is.True, error);

            Assert.That(baker.LastResult.Succeeded, Is.True,
                baker.LastResult.Error);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                Folder + "/Fall_StateBaked.anim"), Is.Not.Null);

            UnityEngine.Animation legacy =
                root.AddComponent<UnityEngine.Animation>();
            legacy.enabled = true;
            Assert.That(baker.StartBaking(out error), Is.False);
            Assert.That(error, Does.Contain("Mecanim-only"));

            legacy.enabled = false;
            baker.animationStates = new[] { "AbsentState" };
            Assert.That(baker.StartBaking(out error), Is.False);
            Assert.That(error, Does.Contain("base layer"));
        }

        [Test]
        public void I06_PlayableDirectorRestoresModeTimeAndStateOnEveryExit()
        {
            RunDirectorCase("DirectorSuccess",
                RagdollBakerCompletionStatus.Succeeded);
            RunDirectorCase("DirectorCancel",
                RagdollBakerCompletionStatus.Canceled);
            RunDirectorCase("DirectorFailure",
                RagdollBakerCompletionStatus.Failed);
        }

        [Test]
        public void I07_RealtimeSamplingPolicyUsesActualElapsedWithoutManufacturedSamples()
        {
            GameObject root = Own(new GameObject("Realtime Frame Baker"));
            RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
            ConfigureRealtime(baker, "RealtimeFrameOutput");
            baker.frameRate = 1000;
            List<float> samples = new List<float>();
            baker.SampleRequested += (_, elapsed) => samples.Add(elapsed);
            string error;
            Assert.That(baker.StartBaking(out error), Is.True, error);
            Assert.That(samples, Is.EqualTo(new[] { 0f }));

            Assert.That(baker.AdvanceRealtimeSampling(0.1f), Is.True);
            Assert.That(samples.Count, Is.EqualTo(2),
                "A missed interval must produce one actual pose, not catch-up poses.");
            Assert.That(samples[1], Is.EqualTo(0.1f).Within(0.0001f));
            baker.CancelBaking();
        }

        [Test]
        public void I10_MuscleAndIKReductionUseIndependentErrorsAndKeepEndpoints()
        {
            GameObject root = Own(new GameObject("Reduction Policy Baker"));
            RagdollHumanoidBaker baker = root.AddComponent<RagdollHumanoidBaker>();
            baker.keyReductionError = 0.1f;
            baker.IKKeyReductionError = 0.001f;
            List<Keyframe> source = new List<Keyframe>
            {
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.05f),
                new Keyframe(1f, 0f)
            };

            float muscleError = RagdollHumanoidBakerReductionPolicy
                .ErrorForProperty(baker, "Left Arm Down-Up");
            float ikError = RagdollHumanoidBakerReductionPolicy
                .ErrorForProperty(baker, "LeftFootT.x");
            Keyframe[] muscle = RagdollBakerCurveReduction.Reduce(
                source, muscleError);
            Keyframe[] ik = RagdollBakerCurveReduction.Reduce(source, ikError);

            Assert.That(muscleError, Is.EqualTo(0.1f));
            Assert.That(ikError, Is.EqualTo(0.001f));
            Assert.That(muscle.Length, Is.EqualTo(2));
            Assert.That(ik.Length, Is.EqualTo(3));
            Assert.That(muscle[0].time, Is.Zero);
            Assert.That(muscle[^1].time, Is.EqualTo(1f));
            Assert.That(ik[0].time, Is.Zero);
            Assert.That(ik[^1].time, Is.EqualTo(1f));
        }

        void BakeClip(
            AnimationClip source,
            bool markLegacy,
            string outputName)
        {
            GameObject root = Own(new GameObject(outputName + " Baker"));
            GameObject child = new GameObject("Child");
            child.transform.SetParent(root.transform, false);
            if (source.legacy)
            {
                UnityEngine.Animation animation =
                    root.AddComponent<UnityEngine.Animation>();
                animation.AddClip(source, source.name);
                animation.clip = source;
            }
            else root.AddComponent<Animator>();

            RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
            baker.root = root.transform;
            baker.bakePositionList = new[] { child.transform };
            baker.mode = RagdollBakerMode.AnimationClips;
            baker.animationClips = new[] { source };
            baker.appendName = string.Empty;
            source.name = outputName;
            baker.saveToFolder = Folder;
            baker.markAsLegacy = markLegacy;
            baker.loop = true;
            baker.clipSettingsPolicy =
                RagdollBakerClipSettingsPolicy.InheritSource;
            string error;
            Assert.That(RagdollBakerSessionManager.RunBatchImmediately(
                baker, out error), Is.True, error);
            Assert.That(baker.LastResult.Succeeded, Is.True,
                baker.LastResult.Error);
        }

        void RunDirectorCase(
            string outputName,
            RagdollBakerCompletionStatus expected)
        {
            GameObject root = Own(new GameObject(outputName));
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            TestPlayableAsset asset = Own(
                ScriptableObject.CreateInstance<TestPlayableAsset>());
            asset.TestDuration = 0.08d;
            director.playableAsset = asset;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.Play();
            director.time = 0.025d;
            director.Evaluate();
            director.Pause();
            DirectorUpdateMode originalMode = director.timeUpdateMode;
            double originalTime = director.time;
            PlayState originalState = director.state;

            RagdollGenericBaker baker = root.AddComponent<RagdollGenericBaker>();
            baker.root = root.transform;
            baker.mode = RagdollBakerMode.PlayableDirector;
            baker.playableDirector = director;
            baker.frameRate = 30;
            baker.saveName = outputName;
            baker.saveToFolder = Folder;
            Action afterSessionAttached = null;
            if (expected == RagdollBakerCompletionStatus.Canceled)
                afterSessionAttached = () => baker.SampleRequested += CancelDirectorSample;
            else if (expected == RagdollBakerCompletionStatus.Failed)
                afterSessionAttached = () => baker.SampleRequested += ThrowDirectorSample;
            string error;
            bool completed = RagdollBakerSessionManager.RunBatchImmediately(
                baker, out error, afterSessionAttached);
            Assert.That(completed, Is.EqualTo(
                expected == RagdollBakerCompletionStatus.Succeeded), error);

            Assert.That(baker.LastResult.Status, Is.EqualTo(expected));
            Assert.That(director.timeUpdateMode, Is.EqualTo(originalMode));
            Assert.That(director.time, Is.EqualTo(originalTime).Within(0.000001d));
            Assert.That(director.state, Is.EqualTo(originalState));
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                Folder + "/" + outputName + ".anim");
            Assert.That(output != null,
                Is.EqualTo(expected == RagdollBakerCompletionStatus.Succeeded));
        }

        static void CancelDirectorSample(RagdollBaker source, float deltaTime)
        {
            source.CancelBaking();
        }

        static void ThrowDirectorSample(RagdollBaker source, float deltaTime)
        {
            throw new InvalidOperationException("Synthetic Director sample failure.");
        }

        void ConfigureRealtime(RagdollGenericBaker baker, string name)
        {
            baker.root = baker.transform;
            baker.mode = RagdollBakerMode.Realtime;
            baker.frameRate = 30;
            baker.saveName = name;
            baker.saveToFolder = Folder;
            baker.clipSettingsPolicy =
                RagdollBakerClipSettingsPolicy.PreserveDestination;
        }

        AnimationClip CreateSourceClip(bool legacy, string name)
        {
            AnimationClip clip = new AnimationClip
            {
                legacy = legacy,
                name = name,
                frameRate = 20f
            };
            clip.SetCurve("Child", typeof(Transform), "m_LocalPosition.x",
                new AnimationCurve(new Keyframe(0f, 1f),
                    new Keyframe(0.05f, 6f)));
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            settings.cycleOffset = 0.47f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            Own(clip);
            return clip;
        }

        static void AssertLoopOutput(
            string path,
            bool legacy,
            float cycleOffset)
        {
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(output, Is.Not.Null, path);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(output,
                EditorCurveBinding.FloatCurve("Child", typeof(Transform),
                    "m_LocalPosition.x"));
            Assert.That(curve, Is.Not.Null);
            Assert.That(curve.keys[^1].value,
                Is.EqualTo(curve.keys[0].value).Within(0.0001f));
            Assert.That(output.legacy, Is.EqualTo(legacy));
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(output);
            Assert.That(settings.loopTime, Is.True);
            Assert.That(settings.cycleOffset,
                Is.EqualTo(cycleOffset).Within(0.0001f));
        }

        static void SetPositionCurve(
            AnimationClip clip,
            string path,
            float first,
            float last,
            float duration)
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform),
                    "m_LocalPosition.x"),
                new AnimationCurve(new Keyframe(0f, first),
                    new Keyframe(duration, last)));
        }

        static AnimationCurve PositionCurve(AnimationClip clip)
        {
            return AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform),
                    "m_LocalPosition.x"));
        }

        T Own<T>(T value) where T : Object
        {
            if (transient == null) transient = new List<Object>();
            transient.Add(value);
            return value;
        }

        sealed class TestPlayableAsset : PlayableAsset
        {
            internal double TestDuration;
            public override double duration => TestDuration;
            public override Playable CreatePlayable(
                PlayableGraph graph,
                GameObject owner)
            {
                return Playable.Create(graph);
            }
        }
    }
}
