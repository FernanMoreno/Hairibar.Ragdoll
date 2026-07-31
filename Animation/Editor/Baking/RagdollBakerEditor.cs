using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollBaker), true)]
    public sealed class RagdollBakerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RagdollBaker baker = (RagdollBaker)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (!baker.IsBaking)
                {
                    if (GUILayout.Button("Start Baking"))
                        RagdollBakerSessionManager.Start(baker);
                }
                else if (GUILayout.Button("Stop Baking"))
                {
                    baker.StopBaking();
                }
            }
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to bake animation.", MessageType.Info);
            }
        }
    }

    [InitializeOnLoad]
    static class RagdollBakerSessionManager
    {
        static readonly Dictionary<int, BakerSession> sessions =
            new Dictionary<int, BakerSession>();

        static RagdollBakerSessionManager()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    DisposeAll();
            };
        }

        internal static void Start(RagdollBaker baker)
        {
            int id = RagdollUnityObjectId.Get(baker);
            if (sessions.ContainsKey(id)) return;
            BakerSession session = new BakerSession(baker);
            sessions.Add(id, session);
            string error;
            if (!baker.StartBaking(out error))
            {
                sessions.Remove(id);
                session.Dispose();
                UnityEngine.Debug.LogError(error, baker);
            }
        }

        static void DisposeAll()
        {
            foreach (BakerSession session in sessions.Values) session.Dispose();
            sessions.Clear();
        }

        sealed class BakerSession : IDisposable
        {
            readonly RagdollBaker baker;
            IClipRecorder recorder;

            public BakerSession(RagdollBaker baker)
            {
                this.baker = baker;
                baker.SegmentStarted += OnSegmentStarted;
                baker.SampleRequested += OnSample;
                baker.SegmentFinished += OnSegmentFinished;
                baker.BakingFinished += OnBakingFinished;
            }

            public void Dispose()
            {
                if (!baker) return;
                baker.SegmentStarted -= OnSegmentStarted;
                baker.SampleRequested -= OnSample;
                baker.SegmentFinished -= OnSegmentFinished;
                baker.BakingFinished -= OnBakingFinished;
                recorder?.Dispose();
                recorder = null;
            }

            void OnSegmentStarted(
                RagdollBaker source,
                string name,
                AnimationClip sourceClip)
            {
                recorder?.Dispose();
                RagdollHumanoidBaker humanoid = source as RagdollHumanoidBaker;
                recorder = humanoid
                    ? (IClipRecorder)new HumanoidClipRecorder(humanoid)
                    : new GenericClipRecorder((RagdollGenericBaker)source);
            }

            void OnSample(RagdollBaker source, float deltaTime)
            {
                recorder?.Sample(deltaTime);
            }

            void OnSegmentFinished(
                RagdollBaker source,
                string name,
                AnimationClip sourceClip)
            {
                if (recorder == null) return;
                AnimationClip clip = LoadOrCreateClip(source, name);
                recorder.Save(clip);
                RagdollBakerClipSettingsUtility.Apply(source, sourceClip, clip);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("Baked animation: " + AssetDatabase.GetAssetPath(clip), clip);
                recorder.Dispose();
                recorder = null;
            }

            void OnBakingFinished(RagdollBaker source)
            {
                int id = RagdollUnityObjectId.Get(source);
                sessions.Remove(id);
                Dispose();
            }

            static AnimationClip LoadOrCreateClip(RagdollBaker source, string name)
            {
                string folder = source.saveToFolder.Replace('\\', '/').TrimEnd('/');
                if (!folder.StartsWith("Assets", StringComparison.Ordinal))
                    throw new InvalidOperationException("Baker folder must be inside Assets.");
                EnsureFolder(folder);
                string filename = Sanitize(name) + ".anim";
                string path = folder + "/" + filename;
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing)
                {
                    existing.ClearCurves();
                    return existing;
                }
                AnimationClip clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(path));
                return clip;
            }

            static void EnsureFolder(string folder)
            {
                string[] parts = folder.Split('/');
                string current = parts[0];
                for (int index = 1; index < parts.Length; index++)
                {
                    string next = current + "/" + parts[index];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[index]);
                    current = next;
                }
            }

            static string Sanitize(string value)
            {
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    value = value.Replace(invalid, '_');
                return string.IsNullOrEmpty(value) ? "Baked Clip" : value;
            }
        }

        interface IClipRecorder : IDisposable
        {
            void Sample(float deltaTime);
            void Save(AnimationClip clip);
        }

        sealed class GenericClipRecorder : IClipRecorder
        {
            readonly RagdollGenericBaker baker;
            readonly GameObjectRecorder recorder;

            public GenericClipRecorder(RagdollGenericBaker baker)
            {
                this.baker = baker;
                Transform root = baker.RecordingRoot;
                recorder = new GameObjectRecorder(root.gameObject);
                HashSet<Transform> ignored = Set(baker.ignoreList);
                HashSet<Transform> positions = Set(baker.bakePositionList);
                if (baker.rootNode) positions.Add(baker.rootNode);

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    Transform value = transforms[index];
                    string path = AnimationUtility.CalculateTransformPath(value, root);
                    if (RagdollGenericBakerBindingPolicy.ShouldBindRotation(
                        value,
                        ignored))
                    {
                        BindQuaternion(path);
                    }
                    if (RagdollGenericBakerBindingPolicy.ShouldBindPosition(
                        value,
                        positions))
                    {
                        BindPosition(path);
                    }
                }
            }

            public void Sample(float deltaTime) => recorder.TakeSnapshot(deltaTime);

            public void Save(AnimationClip clip)
            {
                CurveFilterOptions filter = new CurveFilterOptions
                {
                    keyframeReduction = true,
                    unrollRotation = true,
                    positionError = baker.keyReductionError,
                    rotationError = baker.keyReductionError,
                    scaleError = baker.keyReductionError,
                    floatError = baker.keyReductionError
                };
                recorder.SaveToClip(clip, baker.frameRate, filter);
                clip.legacy = baker.markAsLegacy;
            }

            public void Dispose()
            {
                if (recorder) UnityEngine.Object.DestroyImmediate(recorder);
            }

            void BindPosition(string path)
            {
                Bind(path, "m_LocalPosition.x");
                Bind(path, "m_LocalPosition.y");
                Bind(path, "m_LocalPosition.z");
            }

            void BindQuaternion(string path)
            {
                Bind(path, "m_LocalRotation.x");
                Bind(path, "m_LocalRotation.y");
                Bind(path, "m_LocalRotation.z");
                Bind(path, "m_LocalRotation.w");
            }

            void Bind(string path, string property)
            {
                recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
            }

            static HashSet<Transform> Set(Transform[] values)
            {
                HashSet<Transform> set = new HashSet<Transform>();
                if (values == null) return set;
                for (int index = 0; index < values.Length; index++)
                    if (values[index]) set.Add(values[index]);
                return set;
            }
        }

        sealed class HumanoidClipRecorder : IClipRecorder
        {
            static readonly string[] GoalNames =
                { "LeftFoot", "RightFoot", "LeftHand", "RightHand" };

            readonly RagdollHumanoidBaker baker;
            readonly HumanPoseHandler handler;
            readonly Dictionary<string, List<Keyframe>> curves =
                new Dictionary<string, List<Keyframe>>();
            HumanPose pose;
            float time;
            int sampleIndex;
            bool hasSample;

            public HumanoidClipRecorder(RagdollHumanoidBaker baker)
            {
                this.baker = baker;
                Animator animator = baker.Animator;
                if (!animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Humanoid Baker requires a valid Humanoid Avatar.");
                handler = new HumanPoseHandler(animator.avatar, animator.avatarRoot);
                pose = new HumanPose();
            }

            public void Sample(float deltaTime)
            {
                if (hasSample) time += Mathf.Max(0f, deltaTime);
                handler.GetHumanPose(ref pose);
                AddVector("RootT", pose.bodyPosition, time);
                AddQuaternion("RootQ", pose.bodyRotation, time);

                int divisor = Mathf.Max(1, baker.muscleFrameRateDiv);
                if (sampleIndex % divisor == 0)
                {
                    string[] names = HumanTrait.MuscleName;
                    for (int index = 0; index < names.Length; index++)
                        Add(names[index], time, pose.muscles[index]);
                }

                int goalCount = baker.bakeHandIK ? 4 : 2;
                for (int index = 0; index < goalCount; index++)
                {
                    AddVector(GoalNames[index] + "T", pose.ikGoalPositions[index], time);
                    AddQuaternion(GoalNames[index] + "Q", pose.ikGoalRotations[index], time);
                }
                sampleIndex++;
                hasSample = true;
            }

            public void Save(AnimationClip clip)
            {
                clip.legacy = false;
                clip.frameRate = baker.frameRate;
                foreach (KeyValuePair<string, List<Keyframe>> pair in curves)
                {
                    bool ik = pair.Key.StartsWith("Root", StringComparison.Ordinal)
                        || pair.Key.StartsWith("LeftFoot", StringComparison.Ordinal)
                        || pair.Key.StartsWith("RightFoot", StringComparison.Ordinal)
                        || pair.Key.StartsWith("LeftHand", StringComparison.Ordinal)
                        || pair.Key.StartsWith("RightHand", StringComparison.Ordinal);
                    float error = ik
                        ? Mathf.Max(0f, baker.IKKeyReductionError)
                        : Mathf.Max(0f, baker.keyReductionError);
                    Keyframe[] reduced = RagdollBakerCurveReduction.Reduce(pair.Value, error);
                    if (baker.loop && reduced.Length > 1)
                        reduced[reduced.Length - 1].value = reduced[0].value;
                    AnimationCurve curve = new AnimationCurve(reduced);
                    for (int keyIndex = 0; keyIndex < curve.length; keyIndex++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(
                            curve, keyIndex, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(
                            curve, keyIndex, AnimationUtility.TangentMode.Linear);
                    }
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve("", typeof(Animator), pair.Key),
                        curve);
                }
            }

            public void Dispose()
            {
                handler.Dispose();
            }

            void AddVector(string prefix, Vector3 value, float keyTime)
            {
                Add(prefix + ".x", keyTime, value.x);
                Add(prefix + ".y", keyTime, value.y);
                Add(prefix + ".z", keyTime, value.z);
            }

            void AddQuaternion(string prefix, Quaternion value, float keyTime)
            {
                Add(prefix + ".x", keyTime, value.x);
                Add(prefix + ".y", keyTime, value.y);
                Add(prefix + ".z", keyTime, value.z);
                Add(prefix + ".w", keyTime, value.w);
            }

            void Add(string property, float keyTime, float value)
            {
                List<Keyframe> keys;
                if (!curves.TryGetValue(property, out keys))
                {
                    keys = new List<Keyframe>();
                    curves.Add(property, keys);
                }
                keys.Add(new Keyframe(keyTime, value));
            }
        }
    }

    internal static class RagdollGenericBakerBindingPolicy
    {
        internal static bool ShouldBindRotation(
            Transform value,
            ISet<Transform> ignored)
        {
            return value && (ignored == null || !ignored.Contains(value));
        }

        internal static bool ShouldBindPosition(
            Transform value,
            ISet<Transform> positions)
        {
            return value && positions != null && positions.Contains(value);
        }
    }

    internal static class RagdollBakerClipSettingsUtility
    {
        internal static void Apply(
            RagdollBaker baker,
            AnimationClip source,
            AnimationClip destination)
        {
            if (!baker) throw new ArgumentNullException(nameof(baker));
            if (!destination) throw new ArgumentNullException(nameof(destination));

            if (baker.clipSettingsPolicy
                    == RagdollBakerClipSettingsPolicy.InheritSource
                && source)
            {
                AnimationUtility.SetAnimationClipSettings(
                    destination,
                    AnimationUtility.GetAnimationClipSettings(source));
            }
            else if (baker.clipSettingsPolicy
                == RagdollBakerClipSettingsPolicy.UseDefaults)
            {
                AnimationUtility.SetAnimationClipSettings(
                    destination,
                    new AnimationClipSettings());
            }

            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(destination);
            settings.loopTime = baker.loop;
            AnimationUtility.SetAnimationClipSettings(destination, settings);
            destination.wrapMode = baker.loop ? WrapMode.Loop : WrapMode.Once;
        }
    }

    static class RagdollBakerCurveReduction
    {
        internal static Keyframe[] Reduce(IReadOnlyList<Keyframe> source, float error)
        {
            if (source == null || source.Count == 0) return new Keyframe[0];
            if (source.Count <= 2 || error <= 0f)
            {
                Keyframe[] copy = new Keyframe[source.Count];
                for (int index = 0; index < source.Count; index++) copy[index] = source[index];
                return copy;
            }

            bool[] keep = new bool[source.Count];
            keep[0] = true;
            keep[source.Count - 1] = true;
            ReduceRange(source, 0, source.Count - 1, error, keep);
            List<Keyframe> result = new List<Keyframe>();
            for (int index = 0; index < source.Count; index++)
                if (keep[index]) result.Add(source[index]);
            return result.ToArray();
        }

        static void ReduceRange(
            IReadOnlyList<Keyframe> source,
            int first,
            int last,
            float error,
            bool[] keep)
        {
            if (last <= first + 1) return;
            Keyframe a = source[first];
            Keyframe b = source[last];
            float duration = b.time - a.time;
            float largest = -1f;
            int largestIndex = -1;
            for (int index = first + 1; index < last; index++)
            {
                float t = duration > 0f ? (source[index].time - a.time) / duration : 0f;
                float expected = Mathf.LerpUnclamped(a.value, b.value, t);
                float deviation = Mathf.Abs(source[index].value - expected);
                if (deviation > largest)
                {
                    largest = deviation;
                    largestIndex = index;
                }
            }
            if (largest <= error) return;
            keep[largestIndex] = true;
            ReduceRange(source, first, largestIndex, error, keep);
            ReduceRange(source, largestIndex, last, error, keep);
        }
    }
}
