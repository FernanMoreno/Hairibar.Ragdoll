using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollBakerMode
    {
        AnimationClips,
        AnimationStates,
        PlayableDirector,
        Realtime
    }

    public enum RagdollBakerClipSettingsPolicy
    {
        PreserveDestination,
        InheritSource,
        UseDefaults
    }

    public enum RagdollBakerCompletionStatus
    {
        None,
        Succeeded,
        Canceled,
        Failed
    }

    [Serializable]
    public struct RagdollBakerResult
    {
        public RagdollBakerCompletionStatus Status { get; private set; }
        public string Error { get; private set; }
        public int CompletedSegments { get; private set; }

        public bool Succeeded => Status == RagdollBakerCompletionStatus.Succeeded;

        internal RagdollBakerResult(
            RagdollBakerCompletionStatus status,
            string error,
            int completedSegments)
        {
            Status = status;
            Error = error ?? string.Empty;
            CompletedSegments = Mathf.Max(0, completedSegments);
        }
    }

    /// <summary>Runtime sampling controller shared by Generic and Humanoid bakers.</summary>
    public abstract class RagdollBaker : MonoBehaviour
    {
        [Min(1)] public int frameRate = 30;
        [Min(0f)] public float keyReductionError = 0.01f;
        public RagdollBakerMode mode;
        public AnimationClip[] animationClips = new AnimationClip[0];
        public string[] animationStates = new string[0];
        public Component playableDirector;
        public string saveToFolder = "Assets";
        public string appendName = "_Baked";
        public string saveName = "Baked Clip";
        public bool loop;
        public RagdollBakerClipSettingsPolicy clipSettingsPolicy =
            RagdollBakerClipSettingsPolicy.PreserveDestination;

        [Obsolete("Use clipSettingsPolicy instead.")]
        public bool inheritClipSettings
        {
            get => clipSettingsPolicy == RagdollBakerClipSettingsPolicy.InheritSource;
            set => clipSettingsPolicy = value
                ? RagdollBakerClipSettingsPolicy.InheritSource
                : RagdollBakerClipSettingsPolicy.PreserveDestination;
        }

        Coroutine batch;
        bool segmentActive;
        string activeSegmentName;
        AnimationClip activeSourceClip;
        float realtimeElapsedSinceSample;
        PlayableGraph activeGraph;
        bool hasActiveGraph;
        PlayableDirector activeDirector;
        DirectorUpdateMode directorOriginalUpdateMode;
        double directorOriginalTime;
        PlayState directorOriginalState;
        bool directorOriginalGraphValid;
        int completedSegments;

        public bool IsBaking { get; private set; }
        public bool IsSegmentActive => segmentActive;
        public RagdollBakerResult LastResult { get; private set; }
        public abstract Transform RecordingRoot { get; }

        public event Action<RagdollBaker, string, AnimationClip> SegmentStarted;
        public event Action<RagdollBaker, float> SampleRequested;
        public event Action<RagdollBaker, string, AnimationClip> SegmentFinished;
        public event Action<RagdollBaker, string, AnimationClip> SegmentCanceled;
        public event Action<RagdollBaker> BakingFinished;
        public event Action<RagdollBaker, RagdollBakerResult> BakingCompleted;

        // The recording backend must commit before the public completion result is
        // published. Returning an error keeps runtime code independent from
        // UnityEditor while allowing the Editor recorder to make an asset
        // transaction part of the Baker's observable result.
        internal event Func<RagdollBaker, string> RecordingCommitRequested;

        public bool StartBaking(out string error)
        {
            if (IsBaking)
            {
                error = "The baker is already recording.";
                return false;
            }

            if (frameRate <= 0)
            {
                error = "Baker frame rate must be greater than zero.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(saveToFolder))
            {
                error = "A destination folder inside Assets is required.";
                return false;
            }
            Normalize();
            if (!ValidateConfiguration(out error)) return false;

            completedSegments = 0;
            LastResult = new RagdollBakerResult(
                RagdollBakerCompletionStatus.None,
                string.Empty,
                0);
            IsBaking = true;
            if (mode == RagdollBakerMode.Realtime)
            {
                try
                {
                    BeginSegment(saveName, null);
                }
                catch (Exception exception)
                {
                    error = "Realtime recording could not start: " + exception.Message;
                    CompleteBaking(RagdollBakerCompletionStatus.Failed, error);
                    return false;
                }
            }
            else
            {
                batch = StartCoroutine(RunBatch());
            }
            error = string.Empty;
            return true;
        }

        public void StopBaking()
        {
            if (!IsBaking) return;
            CompleteBaking(RagdollBakerCompletionStatus.Succeeded, string.Empty);
        }

        /// <summary>
        /// Aborts the active bake. Use <see cref="StopBaking"/> to finish and
        /// commit a recording normally.
        /// </summary>
        public void CancelBaking()
        {
            if (!IsBaking) return;
            CompleteBaking(RagdollBakerCompletionStatus.Canceled, string.Empty);
        }

        void LateUpdate()
        {
            if (!IsBaking
                || !segmentActive
                || mode != RagdollBakerMode.Realtime)
            {
                return;
            }

            AdvanceRealtimeSampling(Time.unscaledDeltaTime);
        }

        internal bool AdvanceRealtimeSampling(float deltaTime)
        {
            if (!IsBaking
                || !segmentActive
                || mode != RagdollBakerMode.Realtime)
            {
                return false;
            }

            deltaTime = SanitizeNonNegative(deltaTime);
            realtimeElapsedSinceSample += deltaTime;
            float interval = 1f / frameRate;
            if (realtimeElapsedSinceSample + 0.000001f < interval)
            {
                return false;
            }

            // RootMotion's public Baker contract explicitly does not guarantee the
            // requested rate in realtime when the player cannot reach it. One sample
            // per rendered frame preserves the actual pose/time pair instead of
            // fabricating multiple timestamps for the same pose.
            float elapsed = realtimeElapsedSinceSample;
            realtimeElapsedSinceSample = 0f;
            EmitSample(elapsed);
            return true;
        }

        IEnumerator RunBatch()
        {
            IEnumerator operation = mode == RagdollBakerMode.AnimationClips
                ? RunAnimationClips()
                : mode == RagdollBakerMode.AnimationStates
                    ? RunAnimationStates()
                    : RunPlayableDirector();

            Stack<IEnumerator> operations = new Stack<IEnumerator>();
            operations.Push(operation);
            while (IsBaking && operations.Count > 0)
            {
                bool moved;
                object current = null;
                try
                {
                    IEnumerator active = operations.Peek();
                    moved = active.MoveNext();
                    if (moved) current = active.Current;
                }
                catch (Exception exception)
                {
                    CompleteBaking(
                        RagdollBakerCompletionStatus.Failed,
                        exception.Message,
                        false);
                    yield break;
                }
                if (!moved)
                {
                    IDisposable disposable = operations.Pop() as IDisposable;
                    disposable?.Dispose();
                    continue;
                }
                IEnumerator nested = current as IEnumerator;
                if (nested != null)
                {
                    operations.Push(nested);
                    continue;
                }
                yield return current;
            }

            if (IsBaking)
            {
                CompleteBaking(
                    RagdollBakerCompletionStatus.Succeeded,
                    string.Empty,
                    false);
            }
        }

        IEnumerator RunAnimationClips()
        {
            Animator animator = RecordingRoot.GetComponent<Animator>();
            UnityEngine.Animation legacy =
                RecordingRoot.GetComponent<UnityEngine.Animation>();

            for (int index = 0; index < animationClips.Length && IsBaking; index++)
            {
                AnimationClip clip = animationClips[index];
                if (!clip) continue;

                if (clip.legacy)
                {
                    yield return SampleLegacyClip(legacy, clip);
                }
                else
                {
                    yield return SampleAnimatorClip(animator, clip);
                }
            }
        }

        IEnumerator SampleAnimatorClip(Animator animator, AnimationClip clip)
        {
            CreateAnimationClipGraph(animator, clip);
            activeGraph.Evaluate(0f);
            BeginSegment(clip.name + appendName, clip);

            float current = 0f;
            float interval = 1f / frameRate;
            while (IsBaking && current + 0.000001f < clip.length)
            {
                float next = Mathf.Min(current + interval, clip.length);
                float delta = next - current;
                activeGraph.Evaluate(delta);
                current = next;
                EmitSample(delta);
                yield return null;
            }

            if (segmentActive) EndSegment();
            DestroyActiveGraph();
        }

        IEnumerator SampleLegacyClip(
            UnityEngine.Animation legacy,
            AnimationClip clip)
        {
            clip.SampleAnimation(RecordingRoot.gameObject, 0f);
            BeginSegment(clip.name + appendName, clip);

            float current = 0f;
            float interval = 1f / frameRate;
            while (IsBaking && current + 0.000001f < clip.length)
            {
                float next = Mathf.Min(current + interval, clip.length);
                float delta = next - current;
                clip.SampleAnimation(RecordingRoot.gameObject, next);
                current = next;
                EmitSample(delta);
                yield return null;
            }

            if (segmentActive) EndSegment();
            if (legacy && legacy.clip) legacy.Sample();
        }

        IEnumerator RunAnimationStates()
        {
            Animator animator = RecordingRoot.GetComponent<Animator>();
            for (int index = 0; index < animationStates.Length && IsBaking; index++)
            {
                string state = animationStates[index];
                if (string.IsNullOrEmpty(state)) continue;

                activeGraph = PlayableGraph.Create("Ragdoll Baker State");
                activeGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                hasActiveGraph = true;
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                    activeGraph,
                    "Baker State Output",
                    animator);
                AnimatorControllerPlayable controller =
                    AnimatorControllerPlayable.Create(
                        activeGraph,
                        animator.runtimeAnimatorController);
                output.SetSourcePlayable(controller);
                activeGraph.Play();
                controller.Play(state, 0, 0f);
                activeGraph.Evaluate(0f);

                AnimatorStateInfo info = controller.GetCurrentAnimatorStateInfo(0);
                if (float.IsNaN(info.length) || float.IsInfinity(info.length)
                    || info.length < 0f)
                {
                    throw new InvalidOperationException(
                        "Animator state '" + state + "' has an invalid duration.");
                }
                float duration = info.length;
                BeginSegment(state + appendName, null);
                float current = 0f;
                float interval = 1f / frameRate;
                while (IsBaking && current + 0.000001f < duration)
                {
                    float next = Mathf.Min(current + interval, duration);
                    float delta = next - current;
                    activeGraph.Evaluate(delta);
                    current = next;
                    EmitSample(delta);
                    yield return null;
                }

                if (segmentActive) EndSegment();
                DestroyActiveGraph();
            }
        }

        IEnumerator RunPlayableDirector()
        {
            activeDirector = playableDirector as PlayableDirector;
            directorOriginalUpdateMode = activeDirector.timeUpdateMode;
            directorOriginalTime = activeDirector.time;
            directorOriginalState = activeDirector.state;
            directorOriginalGraphValid = activeDirector.playableGraph.IsValid();

            activeDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            activeDirector.Play();
            activeDirector.time = 0d;
            activeDirector.Evaluate();
            double duration = activeDirector.duration;
            BeginSegment(saveName, null);

            double current = 0d;
            double interval = 1d / frameRate;
            while (IsBaking && current + 0.0000001d < duration)
            {
                double next = Math.Min(current + interval, duration);
                float delta = (float)(next - current);
                activeDirector.time = next;
                activeDirector.Evaluate();
                current = next;
                EmitSample(delta);
                yield return null;
            }

            if (segmentActive) EndSegment();
            RestoreActiveDirector();
        }

        void CreateAnimationClipGraph(Animator animator, AnimationClip clip)
        {
            activeGraph = PlayableGraph.Create("Ragdoll Baker Clip");
            activeGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            hasActiveGraph = true;
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                activeGraph,
                "Baker Clip Output",
                animator);
            AnimationClipPlayable playable =
                AnimationClipPlayable.Create(activeGraph, clip);
            output.SetSourcePlayable(playable);
            activeGraph.Play();
        }

        void BeginSegment(string segmentName, AnimationClip source)
        {
            activeSegmentName = string.IsNullOrEmpty(segmentName)
                ? "Baked Clip"
                : segmentName;
            activeSourceClip = source;
            realtimeElapsedSinceSample = 0f;
            segmentActive = true;
            SegmentStarted?.Invoke(this, activeSegmentName, activeSourceClip);
            EmitSample(0f);
        }

        void EmitSample(float deltaTime)
        {
            SampleRequested?.Invoke(this, SanitizeNonNegative(deltaTime));
        }

        void EndSegment()
        {
            segmentActive = false;
            SegmentFinished?.Invoke(this, activeSegmentName, activeSourceClip);
            completedSegments++;
            activeSegmentName = null;
            activeSourceClip = null;
        }

        void CancelSegment()
        {
            if (!segmentActive) return;
            string name = activeSegmentName;
            AnimationClip source = activeSourceClip;
            segmentActive = false;
            activeSegmentName = null;
            activeSourceClip = null;
            realtimeElapsedSinceSample = 0f;
            SegmentCanceled?.Invoke(this, name, source);
        }

        bool ValidateConfiguration(out string error)
        {
            if (!RecordingRoot)
            {
                error = "A recording root is required.";
                return false;
            }
            if (!Enum.IsDefined(typeof(RagdollBakerMode), mode))
            {
                error = "The Baker mode is invalid.";
                return false;
            }
            if (!Enum.IsDefined(
                typeof(RagdollBakerClipSettingsPolicy),
                clipSettingsPolicy))
            {
                error = "The clip settings policy is invalid.";
                return false;
            }
            string normalizedFolder = saveToFolder.Replace('\\', '/').TrimEnd('/');
            string[] folderSegments = normalizedFolder.Split('/');
            bool parentTraversal = false;
            for (int index = 0; index < folderSegments.Length; index++)
            {
                if (folderSegments[index] == "..") parentTraversal = true;
            }
            if (parentTraversal
                || (normalizedFolder != "Assets"
                    && !normalizedFolder.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal)))
            {
                error = "Baker folder must be inside Assets.";
                return false;
            }

            if (mode == RagdollBakerMode.Realtime)
            {
                error = string.Empty;
                return true;
            }

            if (mode == RagdollBakerMode.AnimationClips)
                return ValidateAnimationClips(out error);
            if (mode == RagdollBakerMode.AnimationStates)
                return ValidateAnimationStates(out error);

            PlayableDirector director = playableDirector as PlayableDirector;
            if (!director || !director.playableAsset)
            {
                error = "PlayableDirector mode requires a PlayableDirector with a PlayableAsset.";
                return false;
            }
            if (double.IsNaN(director.duration)
                || double.IsInfinity(director.duration)
                || director.duration < 0d)
            {
                error = "The PlayableDirector duration must be finite and non-negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        bool ValidateAnimationClips(out string error)
        {
            if (animationClips == null || animationClips.Length == 0)
            {
                error = "AnimationClips mode requires at least one clip.";
                return false;
            }

            bool needsAnimator = false;
            bool needsLegacy = false;
            for (int index = 0; index < animationClips.Length; index++)
            {
                AnimationClip clip = animationClips[index];
                if (!clip)
                {
                    error = "AnimationClips mode does not accept null clips.";
                    return false;
                }
                needsLegacy |= clip.legacy;
                needsAnimator |= !clip.legacy;
            }

            if (needsAnimator && !RecordingRoot.GetComponent<Animator>())
            {
                error = "Non-Legacy clips require an Animator on the recording root.";
                return false;
            }
            if (needsLegacy
                && !RecordingRoot.GetComponent<UnityEngine.Animation>())
            {
                error = "Legacy clips require an Animation component on the recording root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        bool ValidateAnimationStates(out string error)
        {
            Animator animator = RecordingRoot.GetComponent<Animator>();
            if (!animator || !animator.runtimeAnimatorController)
            {
                error = "AnimationStates mode requires an Animator with a RuntimeAnimatorController.";
                return false;
            }
            if (animationStates == null || animationStates.Length == 0)
            {
                error = "AnimationStates mode requires at least one base-layer state.";
                return false;
            }

            for (int index = 0; index < animationStates.Length; index++)
            {
                string state = animationStates[index];
                if (string.IsNullOrEmpty(state))
                {
                    error = "Animation state names cannot be empty.";
                    return false;
                }
                int shortHash = Animator.StringToHash(state);
                int fullHash = Animator.StringToHash("Base Layer." + state);
                if (!animator.HasState(0, shortHash)
                    && !animator.HasState(0, fullHash))
                {
                    error = "Animator base layer does not contain state '" + state + "'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        void Normalize()
        {
            frameRate = Mathf.Max(1, frameRate);
            keyReductionError = SanitizeNonNegative(keyReductionError);
            if (animationClips == null) animationClips = new AnimationClip[0];
            if (animationStates == null) animationStates = new string[0];
            if (string.IsNullOrEmpty(saveToFolder)) saveToFolder = "Assets";
        }

        void CompleteBaking(
            RagdollBakerCompletionStatus status,
            string error,
            bool stopCoroutine = true)
        {
            if (!IsBaking) return;

            if (stopCoroutine && batch != null)
            {
                StopCoroutine(batch);
            }
            batch = null;
            List<Exception> cleanupFailures = new List<Exception>();
            try { RestoreActiveDirector(); }
            catch (Exception exception) { cleanupFailures.Add(exception); }
            try { DestroyActiveGraph(); }
            catch (Exception exception) { cleanupFailures.Add(exception); }
            try
            {
                if (segmentActive)
                {
                    if (status == RagdollBakerCompletionStatus.Succeeded)
                        EndSegment();
                    else
                        CancelSegment();
                }
            }
            catch (Exception exception) { cleanupFailures.Add(exception); }
            IsBaking = false;
            if (cleanupFailures.Count > 0)
            {
                status = RagdollBakerCompletionStatus.Failed;
                string cleanupError = new AggregateException(
                    "Baker cleanup failed.",
                    cleanupFailures).Message;
                error = string.IsNullOrEmpty(error)
                    ? cleanupError
                    : error + " " + cleanupError;
            }
            if (status == RagdollBakerCompletionStatus.Succeeded)
            {
                List<Exception> commitFailures = InvokeRecordingCommit();
                if (commitFailures.Count > 0)
                {
                    status = RagdollBakerCompletionStatus.Failed;
                    string commitError = new AggregateException(
                        "Baker recording commit failed.",
                        commitFailures).Message;
                    error = string.IsNullOrEmpty(error)
                        ? commitError
                        : error + " " + commitError;
                }
            }
            LastResult = new RagdollBakerResult(
                status,
                error,
                completedSegments);
            InvokeCompletionSafely(BakingCompleted, LastResult);
            InvokeFinishedSafely(BakingFinished);
        }

        List<Exception> InvokeRecordingCommit()
        {
            List<Exception> failures = new List<Exception>();
            Delegate[] subscribers = RecordingCommitRequested
                ?.GetInvocationList();
            if (subscribers == null) return failures;
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    string commitError =
                        ((Func<RagdollBaker, string>)subscribers[index])(this);
                    if (!string.IsNullOrWhiteSpace(commitError))
                        failures.Add(new InvalidOperationException(commitError));
                }
                catch (Exception exception) { failures.Add(exception); }
            }
            return failures;
        }

        void InvokeCompletionSafely(
            Action<RagdollBaker, RagdollBakerResult> callback,
            RagdollBakerResult result)
        {
            if (callback == null) return;
            foreach (Delegate subscriber in callback.GetInvocationList())
            {
                try
                {
                    ((Action<RagdollBaker, RagdollBakerResult>)subscriber)(this, result);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeFinishedSafely(Action<RagdollBaker> callback)
        {
            if (callback == null) return;
            foreach (Delegate subscriber in callback.GetInvocationList())
            {
                try { ((Action<RagdollBaker>)subscriber)(this); }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void RestoreActiveDirector()
        {
            if (!activeDirector) return;

            activeDirector.timeUpdateMode = directorOriginalUpdateMode;
            if (directorOriginalState == PlayState.Playing)
            {
                activeDirector.time = directorOriginalTime;
                activeDirector.Evaluate();
                activeDirector.Play();
            }
            else if (directorOriginalGraphValid)
            {
                activeDirector.time = directorOriginalTime;
                activeDirector.Evaluate();
                activeDirector.Pause();
            }
            else
            {
                activeDirector.Stop();
                activeDirector.time = directorOriginalTime;
            }
            activeDirector = null;
        }

        void DestroyActiveGraph()
        {
            if (!hasActiveGraph) return;
            if (activeGraph.IsValid()) activeGraph.Destroy();
            hasActiveGraph = false;
        }

        static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }

        void OnDisable()
        {
            CancelBaking();
        }

        void OnDestroy()
        {
            CancelBaking();
        }
    }
}
