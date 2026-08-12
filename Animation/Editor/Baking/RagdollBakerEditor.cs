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

        sealed class AssetBackup
        {
            internal AnimationClip Destination;
            internal AnimationClip Snapshot;
        }

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

        internal static bool RunBatchImmediately(
            RagdollBaker baker,
            out string error,
            Action afterSessionAttached = null)
        {
            if (!baker) throw new ArgumentNullException(nameof(baker));
            BakerSession session = new BakerSession(baker);
            afterSessionAttached?.Invoke();
            if (!baker.StartManualBatch(out error))
            {
                session.Dispose();
                return false;
            }
            baker.ExecuteManualBatch();
            if (!baker.LastResult.Succeeded)
            {
                error = baker.LastResult.Error;
                return false;
            }
            error = string.Empty;
            return true;
        }

        static void DisposeAll()
        {
            foreach (BakerSession session in sessions.Values) session.Dispose();
            sessions.Clear();
        }

        sealed class BakerSession : IDisposable
        {
            readonly RagdollBaker baker;
            readonly List<PendingClip> pending = new List<PendingClip>();
            IClipRecorder recorder;

            sealed class PendingClip
            {
                internal string Path;
                internal AnimationClip Clip;
            }

            public BakerSession(RagdollBaker baker)
            {
                this.baker = baker;
                baker.SegmentStarted += OnSegmentStarted;
                baker.SampleRequested += OnSample;
                baker.SegmentFinished += OnSegmentFinished;
                baker.SegmentCanceled += OnSegmentCanceled;
                baker.RecordingCommitRequested += OnRecordingCommitRequested;
                baker.BakingCompleted += OnBakingCompleted;
                baker.BakingFinished += OnBakingFinished;
            }

            public void Dispose()
            {
                if (!baker) return;
                baker.SegmentStarted -= OnSegmentStarted;
                baker.SampleRequested -= OnSample;
                baker.SegmentFinished -= OnSegmentFinished;
                baker.SegmentCanceled -= OnSegmentCanceled;
                baker.RecordingCommitRequested -= OnRecordingCommitRequested;
                baker.BakingCompleted -= OnBakingCompleted;
                baker.BakingFinished -= OnBakingFinished;
                recorder?.Dispose();
                recorder = null;
                DiscardPending();
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
                AnimationClip temporary = new AnimationClip { name = name };
                try
                {
                    string path = DestinationPath(source, name);
                    for (int index = 0; index < pending.Count; index++)
                    {
                        if (string.Equals(
                            pending[index].Path,
                            path,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                "A Baker session produced the destination twice: "
                                + path);
                        }
                    }

                    UnityEngine.Object mainAsset =
                        AssetDatabase.LoadMainAssetAtPath(path);
                    if (mainAsset && !(mainAsset is AnimationClip))
                    {
                        throw new InvalidOperationException(
                            "Baker refuses to overwrite a non-AnimationClip asset: "
                            + path);
                    }
                    AnimationClip destination = mainAsset as AnimationClip;
                    if (destination && source.clipSettingsPolicy
                        == RagdollBakerClipSettingsPolicy.PreserveDestination)
                    {
                        AnimationUtility.SetAnimationClipSettings(
                            temporary,
                            AnimationUtility.GetAnimationClipSettings(destination));
                        temporary.wrapMode = destination.wrapMode;
                    }

                    recorder.Save(temporary);
                    if (source.loop && source is RagdollGenericBaker)
                        RagdollBakerLoopUtility.MatchEndKeysToStart(temporary);
                    RagdollBakerClipSettingsUtility.Apply(
                        source, sourceClip, temporary);
                    pending.Add(new PendingClip
                    {
                        Path = path,
                        Clip = temporary
                    });
                    temporary = null;
                }
                finally
                {
                    recorder.Dispose();
                    recorder = null;
                    if (temporary) UnityEngine.Object.DestroyImmediate(temporary);
                }
            }

            void OnSegmentCanceled(
                RagdollBaker source,
                string name,
                AnimationClip sourceClip)
            {
                recorder?.Dispose();
                recorder = null;
            }

            string OnRecordingCommitRequested(RagdollBaker source)
            {
                try
                {
                    CommitPending();
                    return string.Empty;
                }
                catch (Exception exception)
                {
                    DiscardPending();
                    UnityEngine.Debug.LogException(exception, source);
                    return exception.Message;
                }
            }

            void OnBakingCompleted(
                RagdollBaker source,
                RagdollBakerResult result)
            {
                if (!result.Succeeded) DiscardPending();
            }

            void OnBakingFinished(RagdollBaker source)
            {
                int id = RagdollUnityObjectId.Get(source);
                sessions.Remove(id);
                Dispose();
            }

            void CommitPending()
            {
                AnimationClip[] clips = new AnimationClip[pending.Count];
                string[] paths = new string[pending.Count];
                for (int index = 0; index < pending.Count; index++)
                {
                    clips[index] = pending[index].Clip;
                    paths[index] = pending[index].Path;
                }
                CommitClipsAtomically(clips, paths);
                DiscardPending();
            }

            void DiscardPending()
            {
                for (int index = 0; index < pending.Count; index++)
                    if (pending[index].Clip)
                        UnityEngine.Object.DestroyImmediate(pending[index].Clip);
                pending.Clear();
            }

            static string DestinationPath(RagdollBaker source, string name)
            {
                string folder = source.saveToFolder.Replace('\\', '/').TrimEnd('/');
                if (!folder.StartsWith("Assets", StringComparison.Ordinal))
                    throw new InvalidOperationException("Baker folder must be inside Assets.");
                string filename = Sanitize(name) + ".anim";
                return folder + "/" + filename;
            }

            internal static void EnsureFolder(string folder)
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

        internal static void CommitClipsAtomically(
            IReadOnlyList<AnimationClip> clips,
            IReadOnlyList<string> paths,
            Action<int> afterWrite = null)
        {
            if (clips == null) throw new ArgumentNullException(nameof(clips));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (clips.Count != paths.Count)
                throw new ArgumentException(
                    "Clip and destination counts must match.");

            var backups = new List<AssetBackup>(clips.Count);
            var createdPaths = new List<string>(clips.Count);
            try
            {
                for (int index = 0; index < clips.Count; index++)
                {
                    AnimationClip clip = clips[index];
                    string path = paths[index];
                    if (!clip) throw new ArgumentException(
                        "Pending Baker clips cannot be null.", nameof(clips));
                    if (string.IsNullOrWhiteSpace(path)
                        || !path.Replace('\\', '/').StartsWith(
                            "Assets/", StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "Every Baker destination must be inside Assets.",
                            nameof(paths));
                    }

                    string directory = Path.GetDirectoryName(path);
                    BakerSession.EnsureFolder(directory.Replace('\\', '/'));
                    UnityEngine.Object mainAsset =
                        AssetDatabase.LoadMainAssetAtPath(path);
                    if (mainAsset && !(mainAsset is AnimationClip))
                    {
                        throw new InvalidOperationException(
                            "Baker refuses to overwrite a non-AnimationClip asset: "
                            + path);
                    }
                    AnimationClip destination = mainAsset as AnimationClip;
                    if (destination)
                    {
                        string destinationName = destination.name;
                        backups.Add(new AssetBackup
                        {
                            Destination = destination,
                            Snapshot = UnityEngine.Object.Instantiate(destination)
                        });
                        EditorUtility.CopySerialized(clip, destination);
                        // ponytail: CopySerialized copies the source's name too;
                        // the main asset's name must keep matching its filename.
                        destination.name = destinationName;
                        EditorUtility.SetDirty(destination);
                    }
                    else
                    {
                        AnimationClip asset = UnityEngine.Object.Instantiate(clip);
                        asset.name = clip.name;
                        AssetDatabase.CreateAsset(asset, path);
                        createdPaths.Add(path);
                    }
                    afterWrite?.Invoke(index);
                }
                AssetDatabase.SaveAssets();
                for (int index = 0; index < paths.Count; index++)
                {
                    AnimationClip committed =
                        AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[index]);
                    UnityEngine.Debug.Log(
                        "Baked animation: " + paths[index], committed);
                }
            }
            catch (Exception commitException)
            {
                var rollbackFailures = new List<Exception>();
                for (int index = backups.Count - 1; index >= 0; index--)
                {
                    AssetBackup backup = backups[index];
                    if (!backup.Destination || !backup.Snapshot) continue;
                    try
                    {
                        string destinationName = backup.Destination.name;
                        EditorUtility.CopySerialized(
                            backup.Snapshot, backup.Destination);
                        backup.Destination.name = destinationName;
                        EditorUtility.SetDirty(backup.Destination);
                    }
                    catch (Exception exception)
                    {
                        rollbackFailures.Add(exception);
                    }
                }
                for (int index = createdPaths.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        if (!AssetDatabase.DeleteAsset(createdPaths[index]))
                            rollbackFailures.Add(new IOException(
                                "Failed to remove Baker asset created by a failed "
                                + "transaction: " + createdPaths[index]));
                    }
                    catch (Exception exception)
                    {
                        rollbackFailures.Add(exception);
                    }
                }
                try { AssetDatabase.SaveAssets(); }
                catch (Exception exception) { rollbackFailures.Add(exception); }
                if (rollbackFailures.Count != 0)
                {
                    rollbackFailures.Insert(0, commitException);
                    throw new AggregateException(
                        "Baker commit and rollback both failed.",
                        rollbackFailures);
                }
                throw;
            }
            finally
            {
                for (int index = 0; index < backups.Count; index++)
                    if (backups[index].Snapshot)
                        UnityEngine.Object.DestroyImmediate(
                            backups[index].Snapshot);
            }
        }

        internal interface IClipRecorder : IDisposable
        {
            void Sample(float deltaTime);
            void Save(AnimationClip clip);
        }

        internal sealed class GenericClipRecorder : IClipRecorder
        {
            readonly RagdollGenericBaker baker;
            readonly GameObjectRecorder recorder;
            float recordedDuration;
            bool hasSample;

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

            public void Sample(float deltaTime)
            {
                float elapsed = Mathf.Max(0f, deltaTime);
                recorder.TakeSnapshot(elapsed);
                if (hasSample) recordedDuration += elapsed;
                hasSample = true;
            }

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
                PreserveExactEndpointTime(clip, recordedDuration);
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

            static void PreserveExactEndpointTime(
                AnimationClip clip,
                float duration)
            {
                if (!clip || duration <= 0f) return;
                EditorCurveBinding[] bindings =
                    AnimationUtility.GetCurveBindings(clip);
                for (int index = 0; index < bindings.Length; index++)
                {
                    EditorCurveBinding binding = bindings[index];
                    AnimationCurve curve =
                        AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null || curve.length == 0) continue;
                    Keyframe[] keys = curve.keys;
                    if (keys.Length == 1)
                    {
                        Keyframe endpoint = keys[0];
                        endpoint.time = duration;
                        curve.AddKey(endpoint);
                    }
                    else
                    {
                        Keyframe endpoint = keys[keys.Length - 1];
                        endpoint.time = duration;
                        keys[keys.Length - 1] = endpoint;
                        curve.keys = keys;
                    }
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
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
                    float error = RagdollHumanoidBakerReductionPolicy
                        .ErrorForProperty(baker, pair.Key);
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

    internal static class RagdollHumanoidBakerReductionPolicy
    {
        internal static bool IsIkProperty(string property)
        {
            return !string.IsNullOrEmpty(property)
                && (property.StartsWith("Root", StringComparison.Ordinal)
                    || property.StartsWith("LeftFoot", StringComparison.Ordinal)
                    || property.StartsWith("RightFoot", StringComparison.Ordinal)
                    || property.StartsWith("LeftHand", StringComparison.Ordinal)
                    || property.StartsWith("RightHand", StringComparison.Ordinal));
        }

        internal static float ErrorForProperty(
            RagdollHumanoidBaker baker,
            string property)
        {
            if (!baker) throw new ArgumentNullException(nameof(baker));
            return IsIkProperty(property)
                ? Mathf.Max(0f, baker.IKKeyReductionError)
                : Mathf.Max(0f, baker.keyReductionError);
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

    /// <summary>
    /// Hairibar-owned loop seam policy. RootMotion documents matching final keys to
    /// initial keys for Generic Baker loop output. Unity curve APIs preserve key time,
    /// tangents and weights while only final value changes.
    /// </summary>
    internal static class RagdollBakerLoopUtility
    {
        internal static void MatchEndKeysToStart(AnimationClip clip)
        {
            if (!clip) throw new ArgumentNullException(nameof(clip));
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int index = 0; index < bindings.Length; index++)
            {
                EditorCurveBinding binding = bindings[index];
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;
                Keyframe[] keys = curve.keys;
                Keyframe last = keys[keys.Length - 1];
                last.value = keys[0].value;
                keys[keys.Length - 1] = last;
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
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
