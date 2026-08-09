using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Hairibar.Ragdoll.Animation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hairibar.Ragdoll.Demo
{
    public enum RegressionScenario
    {
        CoreLifecycle,
        HumanoidBakerFall,
        HierarchyProps,
        CollisionsPerformance
    }

    public sealed class RegressionAnimationEventReceiver : MonoBehaviour
    {
        public int Count { get; private set; }

        public void OnHairibarCertificationAnimationEvent()
        {
            Count++;
        }
    }

    public sealed class RegressionDeterministicIKSolver : MonoBehaviour,
        IRagdollIKSolver
    {
        public bool IsSolverEnabled => enabled && gameObject.activeInHierarchy;
        public bool AutomaticUpdates { get; set; } = true;
        public int SolveCount { get; private set; }
        public void Solve() { SolveCount++; }
    }

    sealed class RegressionRealtimeClipRecorder : IDisposable
    {
        readonly RagdollBaker baker;
        readonly Transform root;
        // Realtime Baker emits at most one sample per rendered frame. Reserving the
        // complete certification window prevents List<Keyframe> growth from being
        // misreported as an allocation made by the Baker runtime path.
        readonly List<Keyframe> x = new List<Keyframe>(1024);
        float time;
        public AnimationClip Clip { get; private set; }
        public float RecordedLastX => x.Count > 0 ? x[x.Count - 1].value : 0f;

        internal RegressionRealtimeClipRecorder(RagdollBaker baker, Transform root)
        {
            this.baker = baker;
            this.root = root;
            baker.SampleRequested += Sample;
            baker.SegmentFinished += Finish;
            baker.SegmentCanceled += Cancel;
        }

        void Sample(RagdollBaker source, float deltaTime)
        {
            if (x.Count > 0) time += Mathf.Max(0f, deltaTime);
            x.Add(new Keyframe(time, root.localPosition.x));
        }

        void Finish(RagdollBaker source, string name, AnimationClip sourceClip)
        {
            Clip = new AnimationClip { name = name, legacy = true };
            Clip.SetCurve(string.Empty, typeof(Transform),
                "m_LocalPosition.x", new AnimationCurve(x.ToArray()));
        }

        void Cancel(RagdollBaker source, string name, AnimationClip sourceClip)
        {
            x.Clear();
        }

        public void Dispose()
        {
            baker.SampleRequested -= Sample;
            baker.SegmentFinished -= Finish;
            baker.SegmentCanceled -= Cancel;
        }
    }

    public enum RegressionTeleportBoundary
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    public sealed class RegressionTeleportBoundaryDriver : MonoBehaviour
    {
        RagdollAnimator animator;
        Vector3 position;
        Quaternion rotation;
        RegressionTeleportBoundary boundary;
        bool pending;

        public bool Completed { get; private set; }

        public void Request(
            RagdollAnimator owner,
            Vector3 worldPosition,
            Quaternion worldRotation,
            RegressionTeleportBoundary updateBoundary)
        {
            animator = owner;
            position = worldPosition;
            rotation = worldRotation;
            boundary = updateBoundary;
            pending = true;
            Completed = false;
        }

        void Update() { TryRun(RegressionTeleportBoundary.Update); }
        void FixedUpdate() { TryRun(RegressionTeleportBoundary.FixedUpdate); }
        void LateUpdate() { TryRun(RegressionTeleportBoundary.LateUpdate); }

        void TryRun(RegressionTeleportBoundary current)
        {
            if (!pending || boundary != current) return;
            pending = false;
            animator.Teleport(position, rotation, true);
            Completed = true;
        }
    }

    /// <summary>
    /// Development Player certification runner. All physical policy used below is
    /// Hairibar-owned test design on Unity PhysX; RootMotion documentation defines the
    /// observable features, not these masses, impulses, durations or thresholds.
    /// </summary>
    public sealed class RegressionScenarioRunner : MonoBehaviour
    {
        const int WarmupFrames = 120;
        const int MeasurementFrames = 600;
        const int TransitionFrameLimit = 180;

        [SerializeField] RegressionScenario scenario;
        [SerializeField] GameObject humanoidPrefab;
        [SerializeField] GameObject ragdollPrefab;
        [SerializeField] RagdollAnimationProfile animationProfile;
        [SerializeField] RuntimeAnimatorController humanoidController;

        static RegressionScenarioRunner active;
        static readonly List<ScenarioResult> Results =
            new List<ScenarioResult>();

        [Serializable]
        sealed class PerformanceResult
        {
            public int puppets;
            public string mode;
            public long cpuMedianNanoseconds;
            public long cpuP95Nanoseconds;
            public long memoryMedianBytes;
            public long memoryP95Bytes;
            public long maximumGcAllocatedInFrame;
        }

        [Serializable]
        sealed class ScenarioAssertion
        {
            public string name;
            public bool succeeded;
        }

        [Serializable]
        sealed class ScenarioResult
        {
            public string name;
            public bool succeeded;
            public string error;
            // Whole-frame diagnostic. This includes coroutine/player/engine work and
            // is not attributed to a Hairibar subsystem.
            public long maximumGcAllocatedInFrame;
            public CriticalPathEvidence[] criticalPaths;
            public int frames;
            public int assertionCount;
            public ScenarioAssertion[] assertions;
            public PerformanceResult[] performance;
            [NonSerialized] public readonly List<ScenarioAssertion> assertionBuffer =
                new List<ScenarioAssertion>();
        }

        [Serializable]
        sealed class CertificationResult
        {
            public int schemaVersion = 2;
            public string unityVersion;
            public string platform;
            public bool succeeded;
            public ScenarioResult[] scenarios;
        }

        [Serializable]
        sealed class SceneEvidence
        {
            public int schemaVersion = 2;
            public bool succeeded;
            public ScenarioResult[] scenes;
        }

        [Serializable]
        sealed class DistributionEvidence
        {
            public double median;
            public double p95;
        }

        [Serializable]
        sealed class CriticalPathEvidence
        {
            public string name;
            public string measurementScope;
            public bool succeeded;
            public int samples;
            public long gcAllocatedBytes;
            public long maxGcAllocatedBytesInFrame;
        }

        [Serializable]
        sealed class ProfilerEvidence
        {
            public int schemaVersion = 2;
            public bool succeeded;
            public int warmupFrames;
            public int measuredFrames;
            public DistributionEvidence cpuMilliseconds;
            public DistributionEvidence memoryBytes;
            public CriticalPathEvidence[] criticalPaths;
        }

        sealed class RigInstance
        {
            internal GameObject Owner;
            internal RagdollFallBehaviour Fall;
            internal RegressionDeterministicIKSolver IKSolver;
            internal RagdollIKScheduler IKScheduler;
            internal RagdollSetupResult Setup;
            internal RagdollDefinitionBindings Bindings;
            internal Transform OriginalChildParent;
        }

        void Awake()
        {
            if (active && active != this)
            {
                Destroy(gameObject);
                return;
            }
            active = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            ScenarioResult result = new ScenarioResult
            {
                name = scenario.ToString(),
                succeeded = false,
                assertions = Array.Empty<ScenarioAssertion>(),
                performance = new PerformanceResult[0],
                criticalPaths = new CriticalPathEvidence[0]
            };
            yield return RunGuarded(result);
            Results.Add(result);

            if (Application.isBatchMode
                && SceneManager.GetActiveScene().buildIndex + 1
                    < SceneManager.sceneCountInBuildSettings)
            {
                int next = SceneManager.GetActiveScene().buildIndex + 1;
                active = null;
                Destroy(gameObject);
                SceneManager.LoadScene(next);
                yield break;
            }
            WriteResultAndQuit();
        }

        IEnumerator RunGuarded(ScenarioResult result)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(RunScenario(result));
            while (stack.Count > 0)
            {
                bool moved;
                object current = null;
                try
                {
                    IEnumerator operation = stack.Peek();
                    moved = operation.MoveNext();
                    if (moved) current = operation.Current;
                }
                catch (Exception exception)
                {
                    Fail(result, exception.GetType().Name + ": " + exception.Message);
                    yield break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                IEnumerator nested = current as IEnumerator;
                if (nested != null) stack.Push(nested);
                else
                {
                    result.frames++;
                    yield return current;
                }
            }
        }

        IEnumerator RunScenario(ScenarioResult result)
        {
            switch (scenario)
            {
                case RegressionScenario.CoreLifecycle:
                    yield return RunCoreLifecycle(result);
                    break;
                case RegressionScenario.HumanoidBakerFall:
                    yield return RunHumanoidBakerFall(result);
                    break;
                case RegressionScenario.HierarchyProps:
                    yield return RunHierarchyProps(result);
                    break;
                case RegressionScenario.CollisionsPerformance:
                    yield return RunPerformanceMatrix(result);
                    break;
                default:
                    Fail(result, "Unsupported regression scenario.");
                    break;
            }
        }

        IEnumerator RunCoreLifecycle(ScenarioResult result)
        {
            RigInstance rig = CreateRig(Vector3.up * 3f, result);
            if (rig == null) yield break;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            List<GameObject> contactSaturation = new List<GameObject>(40);
            ground.name = "Regression Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            try
            {
                yield return null;
                Require(result, rig.Bindings.IsInitialized,
                    "RagdollAnimator did not initialize.");
                Require(result, rig.Setup.PuppetBehaviour.IsInitialized,
                    "BehaviourPuppet did not initialize.");
                Rigidbody[] bodies = rig.Setup.Puppet
                    .GetComponentsInChildren<Rigidbody>(true);
                Require(result, bodies.Length >= 2,
                    "The physical fixture has fewer than two muscles.");
                if (!string.IsNullOrEmpty(result.error)) yield break;

                float initialHeight = bodies[0].position.y;
                WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();
                for (int frame = 0; frame < 90; frame++) yield return fixedUpdate;
                Require(result, bodies[0].position.y < initialHeight - 0.25f,
                    "The real puppet did not fall onto the static ground.");
                RequireFinite(result, bodies);

                int observedContacts = 0;
                Action<RagdollCollisionEvent> observed = _ => observedContacts++;
                rig.Setup.PuppetBehaviour.CollisionObserved += observed;
                for (int index = 0; index < 40; index++)
                {
                    GameObject contact = GameObject.CreatePrimitive(
                        PrimitiveType.Cube);
                    contact.name = "Saturated Contact " + index;
                    contact.transform.localScale = Vector3.one * 0.12f;
                    contact.transform.position = bodies[0].position
                        + new Vector3(
                            ((index % 5) - 2) * 0.04f,
                            ((index / 5) % 4 - 1) * 0.04f,
                            ((index / 20) - 1) * 0.04f);
                    contactSaturation.Add(contact);
                }
                for (int frame = 0; frame < 12; frame++)
                    yield return fixedUpdate;
                rig.Setup.PuppetBehaviour.CollisionObserved -= observed;
                Require(result, observedContacts > 0,
                    "Saturated real PhysX contacts produced no observed collision.");
                RequireFinite(result, bodies);

                // Warm first, then scope each production call independently. The
                // whole-frame recorder remains ambient diagnostic only: engine and
                // coroutine work must not be attributed to an individual subsystem.
                for (int frame = 0; frame < WarmupFrames; frame++)
                    yield return fixedUpdate;
                var matchingPath = NewCriticalPath(
                    "matching", "RagdollAnimator.DoAnimationMatching");
                var mappingPath = NewCriticalPath(
                    "mapping", "RagdollAnimator.MapRagdollToTarget");
                var relayPath = NewCriticalPath(
                    "collision-relay", "RagdollCollisionHub.Dispatch");
                var comPath = NewCriticalPath(
                    "com", "RagdollCenterOfMassSubBehaviour.FixedUpdate");
                RagdollBoneHandle relayHandle = RagdollBoneHandle.Invalid;
                for (int boneIndex = 0;
                    boneIndex < rig.Bindings.IndexedBones.Count; boneIndex++)
                {
                    if (!rig.Bindings.IndexedBones[boneIndex].IsRoot) continue;
                    relayHandle = rig.Bindings.GetHandleAt(boneIndex);
                    break;
                }
                Require(result, relayHandle.IsValid,
                    "Collision relay diagnostic could not resolve the root handle.");
                result.criticalPaths = new[]
                {
                    matchingPath, mappingPath, relayPath, comPath
                };
                int measuredContacts = 0;
                Action<RagdollCollisionEvent> measured = _ => measuredContacts++;
                rig.Setup.PuppetBehaviour.CollisionObserved += measured;
                int previousCollisionBudget =
                    rig.Setup.Collisions.MaxEventsPerFixedStep;
                rig.Setup.Collisions.MaxEventsPerFixedStep = 4096;
                long combinedCriticalGc = 0L;
                string combinedGcName;
                using (ProfilerRecorder combinedGc = StartAvailableRecorder(
                    new[] { "GC Allocated In Frame" }, out combinedGcName))
                {
                    for (int frame = 0; frame < MeasurementFrames; frame++)
                    {
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        rig.Setup.Animator.RunAnimationMatchingForDiagnostics(
                            Time.fixedDeltaTime);
                        AddCriticalSample(matchingPath,
                            GC.GetAllocatedBytesForCurrentThread() - before);

                        before = GC.GetAllocatedBytesForCurrentThread();
                        rig.Setup.Animator.RunMappingForDiagnostics();
                        AddCriticalSample(mappingPath,
                            GC.GetAllocatedBytesForCurrentThread() - before);

                        before = GC.GetAllocatedBytesForCurrentThread();
                        rig.Setup.Behaviours.Context
                            .DispatchCollisionForDiagnostics(
                                relayHandle,
                                RagdollCollisionPhase.Exit);
                        AddCriticalSample(relayPath,
                            GC.GetAllocatedBytesForCurrentThread() - before);

                        before = GC.GetAllocatedBytesForCurrentThread();
                        rig.Setup.PuppetBehaviour.CenterOfMass.FixedUpdate(
                            Time.fixedDeltaTime);
                        AddCriticalSample(comPath,
                            GC.GetAllocatedBytesForCurrentThread() - before);
                        yield return fixedUpdate;
                        if (combinedGc.LastValue > combinedCriticalGc)
                            combinedCriticalGc = combinedGc.LastValue;
                    }
                    Require(result, combinedGc.Valid,
                        "Core critical-path GC ProfilerRecorder counter is unavailable ("
                        + combinedGcName + ").");
                    Require(result, combinedGc.UnitType
                            == ProfilerMarkerDataUnit.Bytes,
                        "Core GC counter does not report bytes: "
                        + combinedGc.UnitType + ".");
                }
                rig.Setup.Collisions.MaxEventsPerFixedStep =
                    previousCollisionBudget;
                rig.Setup.PuppetBehaviour.CollisionObserved -= measured;
                result.maximumGcAllocatedInFrame = combinedCriticalGc;
                FinalizeCriticalPath(matchingPath);
                FinalizeCriticalPath(mappingPath);
                FinalizeCriticalPath(relayPath);
                FinalizeCriticalPath(comPath);
                Require(result, measuredContacts > 0,
                    "Collision relay was not exercised during profiling.");
                RequireCriticalPath(result, matchingPath);
                RequireCriticalPath(result, mappingPath);
                RequireCriticalPath(result, relayPath);
                RequireCriticalPath(result, comPath);

                RagdollSimulationModeController simulation = rig.Setup.Simulation;
                Require(result, simulation.SetModeImmediate(
                    RagdollSimulationMode.Kinematic), "Kinematic mode was rejected.");
                yield return fixedUpdate;
                Require(result, AllKinematic(bodies),
                    "Kinematic mode left a dynamic muscle.");
                Require(result, simulation.SetModeImmediate(
                    RagdollSimulationMode.Active), "Active mode was rejected.");
                yield return fixedUpdate;
                Require(result, HasDynamicBody(bodies),
                    "Active mode did not restore dynamic physics.");

                bodies[0].mass = 0.001f;
                bodies[1].mass = 1000f;
                bodies[1].AddForce(Vector3.right * 20f, ForceMode.Impulse);
                for (int frame = 0; frame < 60; frame++) yield return fixedUpdate;
                RequireFinite(result, bodies);

                RagdollLifecycleSettings immediate =
                    new RagdollLifecycleSettings(0f, 0f, 2f, float.MaxValue,
                        false, true, true);
                rig.Setup.Animator.Kill(immediate);
                yield return null;
                Require(result, rig.Setup.Animator.ActiveState ==
                    RagdollLifecycleState.Dead, "Kill did not reach Dead.");
                rig.Setup.Animator.Resurrect();
                yield return null;
                Require(result, rig.Setup.Animator.ActiveState ==
                    RagdollLifecycleState.Alive, "Resurrect did not restore Alive.");
                rig.Setup.Animator.Freeze(immediate);
                yield return null;
                yield return null;
                Require(result, rig.Setup.Animator.ActiveState ==
                    RagdollLifecycleState.Frozen, "Freeze did not reach Frozen.");
                rig.Setup.Animator.Resurrect();
                yield return null;

                Vector3 respawn = new Vector3(2f, 4f, -1f);
                rig.Setup.PuppetBehaviour.Respawn(
                    respawn, Quaternion.Euler(0f, 35f, 0f));
                Require(result, Vector3.Distance(
                    rig.Setup.Target.position, respawn) < 0.001f,
                    "Respawn did not move the Target atomically.");
                Require(result, VelocitiesAreZero(bodies),
                    "Respawn left muscle velocity behind.");
                Require(result, rig.Setup.PuppetBehaviour.LoseBalance(),
                    "Puppet did not enter Unpinned for GetUp certification.");
                Require(result, rig.Setup.PuppetBehaviour.BeginGetUpImmediately(
                    RagdollGetUpOrientation.Prone),
                    "Explicit prone GetUp did not start.");

                RegressionTeleportBoundaryDriver boundaryDriver =
                    rig.Setup.Target.gameObject
                        .AddComponent<RegressionTeleportBoundaryDriver>();
                RegressionTeleportBoundary[] boundaries =
                {
                    RegressionTeleportBoundary.Update,
                    RegressionTeleportBoundary.FixedUpdate,
                    RegressionTeleportBoundary.LateUpdate
                };
                for (int index = 0; index < boundaries.Length; index++)
                {
                    boundaryDriver.Request(
                        rig.Setup.Animator,
                        respawn + Vector3.right * (index + 1),
                        Quaternion.Euler(0f, 45f + index * 15f, 0f),
                        boundaries[index]);
                    float driverDeadline = Time.realtimeSinceStartup + 1f;
                    while (!boundaryDriver.Completed
                        && Time.realtimeSinceStartup < driverDeadline)
                        yield return null;
                    float teleportDeadline = Time.realtimeSinceStartup + 1f;
                    while (rig.Setup.Animator.HasPendingTeleport
                        && Time.realtimeSinceStartup < teleportDeadline)
                        yield return fixedUpdate;
                    Require(result, boundaryDriver.Completed
                        && !rig.Setup.Animator.HasPendingTeleport,
                        boundaries[index]
                        + " teleport did not commit at a stable boundary; driver="
                        + boundaryDriver.Completed + "; pending="
                        + rig.Setup.Animator.HasPendingTeleport + "; getUp="
                        + rig.Setup.PuppetBehaviour.State + ".");
                }

                SimulationMode previousMode = Physics.simulationMode;
                try
                {
                    Physics.simulationMode = SimulationMode.Script;
                    rig.Setup.Animator.enabled = false;
                    rig.Setup.Animator.Teleport(
                        respawn + Vector3.forward * 2f,
                        Quaternion.Euler(0f, 90f, 0f), true);
                    rig.Setup.Animator.OnPreSimulate(0.02f);
                    Require(result, !rig.Setup.Animator.HasPendingTeleport,
                        "Manual-simulation teleport was not consumed during prepare.");
                    Physics.Simulate(0.02f);
                    rig.Setup.Animator.OnPostSimulate();
                    Require(result,
                        !rig.Setup.Animator.IsManualSimulationPrepared,
                        "Manual simulation did not complete.");
                }
                finally
                {
                    rig.Setup.Animator.enabled = true;
                    Physics.simulationMode = previousMode;
                }

                ConfigurableJoint breakJoint = bodies[1]
                    .GetComponent<ConfigurableJoint>();
                bodies[0].isKinematic = true;
                bodies[1].isKinematic = false;
                breakJoint.xMotion = ConfigurableJointMotion.Locked;
                breakJoint.yMotion = ConfigurableJointMotion.Locked;
                breakJoint.zMotion = ConfigurableJointMotion.Locked;
                breakJoint.angularXMotion = ConfigurableJointMotion.Locked;
                breakJoint.angularYMotion = ConfigurableJointMotion.Locked;
                breakJoint.angularZMotion = ConfigurableJointMotion.Locked;
                breakJoint.breakForce = 0.01f;
                breakJoint.breakTorque = 0.01f;
                bodies[1].AddForce(Vector3.right * 5000f, ForceMode.Impulse);
                for (int frame = 0; frame < 120 && breakJoint; frame++)
                    yield return fixedUpdate;
                Require(result, !breakJoint,
                    "PhysX did not produce the configured irreversible joint break.");
                RequireFinite(result, bodies);
                result.succeeded = string.IsNullOrEmpty(result.error);
            }
            finally
            {
                for (int index = 0; index < contactSaturation.Count; index++)
                    if (contactSaturation[index]) Destroy(contactSaturation[index]);
                Destroy(ground);
                DestroyRig(rig);
            }
        }

        IEnumerator RunHumanoidBakerFall(ScenarioResult result)
        {
            if (!humanoidPrefab || !humanoidController)
            {
                Fail(result, "Humanoid prefab or generated controller is missing.");
                yield break;
            }
            GameObject first = Instantiate(humanoidPrefab);
            GameObject second = Instantiate(humanoidPrefab);
            RigInstance integrationRig = null;
            float originalScale = Time.timeScale;
            int originalTargetFrameRate = Application.targetFrameRate;
            int originalVSyncCount = QualitySettings.vSyncCount;
            try
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 120;
                Animator a = first.GetComponentInChildren<Animator>(true);
                Animator b = second.GetComponentInChildren<Animator>(true);
                Require(result, IsValidHumanoid(a) && IsValidHumanoid(b),
                    "Retarget fixtures are not valid Humanoid Avatars.");
                a.runtimeAnimatorController = humanoidController;
                b.runtimeAnimatorController = humanoidController;
                a.applyRootMotion = true;
                b.applyRootMotion = true;
                Require(result, a.layerCount >= 2 && b.layerCount >= 2,
                    "The generated controller is not multilayer.");
                integrationRig = CreateRig(
                    Vector3.up * 2f, result, true, a);
                if (integrationRig == null) yield break;
                // RagdollAnimator initializes modifiers and the behaviour controller
                // in Start. This coroutine itself begins from the scene runner's
                // Awake, so the newly activated Target must cross one player-loop
                // boundary before its initialized state can be certified.
                yield return null;
                Require(result, integrationRig.Fall
                        && integrationRig.Fall.IsInitialized,
                    "BehaviourFall was not initialized by the real controller.");
                Require(result, integrationRig.IKScheduler
                        && integrationRig.IKScheduler.SolverCount == 1,
                    "IK scheduler did not take ownership of real solver.");
                Require(result, !integrationRig.IKSolver.AutomaticUpdates,
                    "IK scheduler did not disable solver automatic updates.");
                Require(result, integrationRig.Fall.Activate(),
                    "BehaviourFall could not become active.");
                RegressionAnimationEventReceiver eventReceiver = a.gameObject
                    .AddComponent<RegressionAnimationEventReceiver>();

                AnimatorUpdateMode[] modes =
                {
                    AnimatorUpdateMode.Normal,
                    AnimatorUpdateMode.Fixed,
                    AnimatorUpdateMode.UnscaledTime
                };
                float[] scales = { 0f, 0.5f, 1f, 2f };
                for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
                {
                    a.updateMode = modes[modeIndex];
                    for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
                    {
                        Time.timeScale = scales[scaleIndex];
                        a.Play("Locomotion", 0, 0f);
                        yield return null;
                        float before = a.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        for (int frame = 0; frame < 4; frame++) yield return null;
                        float after = a.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        Require(result, IsFinite(after),
                            "Animator produced a non-finite time.");
                        if (scales[scaleIndex] == 0f)
                        {
                            bool shouldAdvance = modes[modeIndex] ==
                                AnimatorUpdateMode.UnscaledTime;
                            Require(result, shouldAdvance
                                    ? after > before
                                    : Mathf.Abs(after - before) < 0.0001f,
                                "Animator update-mode/timeScale contract diverged.");
                        }
                    }
                }
                Time.timeScale = 1f;
                int eventCountBefore = eventReceiver.Count;
                Vector3 rootBefore = a.transform.position;
                a.Play("Locomotion", 0, 0.25f);
                b.Play("Locomotion", 0, 0.25f);
                yield return null;
                Transform aHips = a.GetBoneTransform(HumanBodyBones.Hips);
                Transform bHips = b.GetBoneTransform(HumanBodyBones.Hips);
                Require(result, aHips && bHips,
                    "Humanoid retargeting did not resolve hips.");
                Require(result, Quaternion.Angle(
                    aHips.localRotation, bHips.localRotation) < 0.1f,
                    "Equivalent Humanoid instances did not retarget deterministically.");
                float eventDeadline = Time.realtimeSinceStartup + 2f;
                while (eventReceiver.Count <= eventCountBefore
                    && Time.realtimeSinceStartup < eventDeadline)
                    yield return null;
                Require(result, eventReceiver.Count > eventCountBefore,
                    "The real AnimatorController did not dispatch its AnimationEvent.");
                Require(result,
                    Vector3.Distance(rootBefore, a.transform.position) > 0.0001f,
                    "The generated Humanoid locomotion clip did not apply root motion.");

                RagdollHumanoidBaker baker = a.gameObject
                    .AddComponent<RagdollHumanoidBaker>();
                baker.mode = RagdollBakerMode.Realtime;
                baker.frameRate = 30;
                int samples = 0;
                baker.SampleRequested += (_, __) => samples++;
                RegressionRealtimeClipRecorder clipRecorder =
                    new RegressionRealtimeClipRecorder(baker, a.transform);
                string bakerError;
                Require(result, baker.StartBaking(out bakerError), bakerError);
                for (int frame = 0; frame < WarmupFrames; frame++)
                    yield return null;
                long bakerMaximumGc = 0;
                string bakerGcName;
                using (ProfilerRecorder bakerGc = StartAvailableRecorder(
                    new[] { "GC Allocated In Frame" }, out bakerGcName))
                {
                    for (int frame = 0; frame < MeasurementFrames; frame++)
                    {
                        a.transform.localPosition += Vector3.right * 0.0001f;
                        yield return null;
                        if (bakerGc.LastValue > bakerMaximumGc)
                            bakerMaximumGc = bakerGc.LastValue;
                    }
                    Require(result, bakerGc.Valid,
                        "Baker GC ProfilerRecorder counter is unavailable ("
                        + bakerGcName + ").");
                    Require(result, bakerGc.UnitType
                            == ProfilerMarkerDataUnit.Bytes,
                        "Baker GC counter does not report bytes: "
                        + bakerGc.UnitType + ".");
                }
                result.maximumGcAllocatedInFrame = bakerMaximumGc;
                // Whole-frame Baker GC is ambient diagnostic only. A second Baker
                // below invokes the exact realtime production method synchronously.
                int renderedFrames = WarmupFrames + MeasurementFrames;
                float realtimeDeadline = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < realtimeDeadline
                    && samples <= 1)
                {
                    yield return null;
                    renderedFrames++;
                }
                baker.StopBaking();
                Require(result, samples > 1 && samples <= renderedFrames + 1,
                    "Realtime Baker emitted fabricated or missing samples.");
                Require(result, baker.LastResult.Status ==
                    RagdollBakerCompletionStatus.Succeeded,
                    "Stopping realtime Baker did not commit the recording.");
                Require(result, clipRecorder.Clip
                        && clipRecorder.Clip.length > 0f,
                    "Realtime Baker recorder produced no inspectable clip.");
                if (clipRecorder.Clip)
                {
                    a.enabled = false;
                    Vector3 samplePosition = a.transform.localPosition;
                    samplePosition.x = 0f;
                    a.transform.localPosition = samplePosition;
                    clipRecorder.Clip.SampleAnimation(
                        a.gameObject, clipRecorder.Clip.length);
                    Require(result, Mathf.Abs(a.transform.localPosition.x
                            - clipRecorder.RecordedLastX) < 0.001f,
                        "Generated realtime clip did not preserve final sampled curve.");
                }
                RagdollHumanoidBaker diagnosticBaker = a.gameObject
                    .AddComponent<RagdollHumanoidBaker>();
                diagnosticBaker.mode = RagdollBakerMode.Realtime;
                diagnosticBaker.frameRate = 30;
                int diagnosticSamples = 0;
                Action<RagdollBaker, float> diagnosticListener =
                    (_, __) => diagnosticSamples++;
                diagnosticBaker.SampleRequested += diagnosticListener;
                Require(result, diagnosticBaker.StartBaking(out bakerError),
                    bakerError);
                for (int frame = 0; frame < WarmupFrames; frame++)
                    diagnosticBaker.AdvanceRealtimeSampling(1f / 30f);
                var bakerPath = NewCriticalPath(
                    "baker-realtime", "RagdollBaker.AdvanceRealtimeSampling");
                for (int frame = 0; frame < MeasurementFrames; frame++)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    diagnosticBaker.AdvanceRealtimeSampling(1f / 30f);
                    AddCriticalSample(bakerPath,
                        GC.GetAllocatedBytesForCurrentThread() - before);
                }
                diagnosticBaker.StopBaking();
                diagnosticBaker.SampleRequested -= diagnosticListener;
                Destroy(diagnosticBaker);
                FinalizeCriticalPath(bakerPath);
                result.criticalPaths = new[] { bakerPath };
                Require(result, diagnosticSamples >=
                        WarmupFrames + MeasurementFrames,
                    "Realtime Baker diagnostic did not execute every sample.");
                RequireCriticalPath(result, bakerPath);
                clipRecorder.Dispose();
                Require(result, integrationRig.IKSolver.SolveCount > 0,
                    "Deterministic external IK solver was never executed.");
                Require(result, integrationRig.Fall.CurrentBlend >= 0f
                        && IsFinite(integrationRig.Fall.CurrentBlend),
                    "BehaviourFall did not produce a finite runtime blend.");
                if (clipRecorder.Clip) Destroy(clipRecorder.Clip);
                result.succeeded = string.IsNullOrEmpty(result.error);
            }
            finally
            {
                Time.timeScale = originalScale;
                Application.targetFrameRate = originalTargetFrameRate;
                QualitySettings.vSyncCount = originalVSyncCount;
                Destroy(first);
                Destroy(second);
                DestroyRig(integrationRig);
            }
        }

        IEnumerator RunHierarchyProps(ScenarioResult result)
        {
            RigInstance rig = CreateRig(Vector3.up * 2f, result);
            if (rig == null) yield break;
            GameObject slotObject = null;
            GameObject propObject = null;
            GameObject replacementObject = null;
            int originalTargetFrameRate = Application.targetFrameRate;
            int originalVSyncCount = QualitySettings.vSyncCount;
            try
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 120;
                yield return null;
                Rigidbody rootBody = rig.Setup.Puppet.GetComponent<Rigidbody>();
                slotObject = new GameObject("PropSlot");
                slotObject.transform.SetParent(rig.Setup.Puppet.parent, false);
                slotObject.transform.position = rootBody.position + Vector3.right;
                Rigidbody slotBody = slotObject.AddComponent<Rigidbody>();
                slotBody.mass = 3f;
                ConfigurableJoint slotJoint =
                    slotObject.AddComponent<ConfigurableJoint>();
                slotJoint.connectedBody = rootBody;
                slotObject.AddComponent<BoxCollider>();
                Transform targetSlot = new GameObject("PropTarget").transform;
                targetSlot.SetParent(rig.Setup.Target, false);
                targetSlot.localPosition = Vector3.right;
                RagdollPropMuscle muscle = rig.Setup.Animator.gameObject
                    .AddComponent<RagdollPropMuscle>();
                string error;
                Require(result, muscle.TryConfigureBeforeInitialization(
                    rig.Setup.Animator, slotJoint, targetSlot, rig.Setup.Target,
                    new BoneName("Prop"), false, true, out error), error);
                muscle.Initialize();
                WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();
                for (int frame = 0; frame < TransitionFrameLimit
                    && muscle.State != RagdollPropMuscleState.Empty; frame++)
                    yield return fixedUpdate;
                Require(result, muscle.State == RagdollPropMuscleState.Empty,
                    "Runtime prop slot did not initialize: " + muscle.State
                    + " " + muscle.LastError
                    + "; handle=" + muscle.Handle
                    + "; pending="
                    + rig.Setup.Animator.PendingMuscleConnectionOperationCount
                    + "; mode=" + rig.Setup.Simulation.CurrentMode
                    + "; transitioning=" + rig.Setup.Simulation.IsTransitioning);
                if (muscle.State != RagdollPropMuscleState.Empty) yield break;

                propObject = new GameObject("CertificationProp");
                propObject.transform.position = slotObject.transform.position;
                Rigidbody propBody = propObject.AddComponent<Rigidbody>();
                propBody.mass = 7f;
                propObject.AddComponent<BoxCollider>();
                Transform visual = new GameObject("Visual").transform;
                visual.SetParent(propObject.transform, false);
                RagdollProp prop = propObject.AddComponent<RagdollProp>();
                RagdollPropMelee melee =
                    propObject.AddComponent<RagdollPropMelee>();
                Require(result, prop.TryConfigureStandalone(
                    visual, propBody, out error), error);
                prop.AddAdditionalPin();
                prop.AdditionalPin.LocalOffset = new Vector3(0.1f, 0.2f, 0f);
                prop.AdditionalPin.Weight = 1.25f;
                prop.AdditionalPin.Mass = 2f;
                Require(result, muscle.TrySetCurrentProp(prop, out error), error);
                for (int frame = 0; frame < TransitionFrameLimit
                    && muscle.State != RagdollPropMuscleState.Holding; frame++)
                    yield return fixedUpdate;
                Require(result, muscle.State == RagdollPropMuscleState.Holding
                    && prop.IsHeld, "Prop pickup did not commit: " + muscle.State
                    + " " + muscle.LastError);
                if (muscle.State != RagdollPropMuscleState.Holding) yield break;
                Require(result, prop.CurrentRigidbody == slotBody,
                    "Held CurrentRigidbody is not the slot body.");
                rig.Setup.PuppetBehaviour.DropProps = false;
                Require(result, melee.StartAction(0.08f),
                    melee.LastActionError);
                for (int frame = 0; frame < 12 && melee.IsActionActive; frame++)
                    yield return fixedUpdate;
                Require(result, !melee.IsActionActive,
                    "Timed melee action did not expire at a FixedUpdate boundary.");

                Require(result, melee.StartAction(10f), melee.LastActionError);
                RagdollLifecycleSettings immediate =
                    new RagdollLifecycleSettings(0f, 0f, 2f, float.MaxValue,
                        false, true, true);
                rig.Setup.Animator.Kill(immediate);
                yield return null;
                Require(result, !melee.IsActionActive && prop.IsHeld,
                    "Death did not cancel melee action while preserving held ownership.");
                rig.Setup.Animator.Resurrect();
                yield return null;
                Require(result, melee.StartAction(10f), melee.LastActionError);
                rig.Setup.Animator.Freeze(immediate);
                yield return null;
                yield return null;
                Require(result, !melee.IsActionActive && prop.IsHeld,
                    "Freeze did not cancel melee action while preserving held ownership.");
                rig.Setup.Animator.Resurrect();
                yield return null;

                for (int frame = 0; frame < WarmupFrames; frame++)
                    yield return null;
                long additionalPinMaximumGc = 0;
                long additionalPinCriticalPathMaximumGc = 0;
                long additionalPinCriticalPathTotalGc = 0;
                string additionalPinGcName;
                using (ProfilerRecorder additionalPinGc = StartAvailableRecorder(
                    new[] { "GC Allocated In Frame" }, out additionalPinGcName))
                {
                    for (int frame = 0; frame < MeasurementFrames; frame++)
                    {
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        muscle.ApplyAdditionalPinForDiagnostics();
                        long scopedAllocation =
                            GC.GetAllocatedBytesForCurrentThread() - before;
                        additionalPinCriticalPathTotalGc += scopedAllocation;
                        if (scopedAllocation > additionalPinCriticalPathMaximumGc)
                        {
                            additionalPinCriticalPathMaximumGc = scopedAllocation;
                        }
                        yield return null;
                        if (additionalPinGc.LastValue > additionalPinMaximumGc)
                            additionalPinMaximumGc = additionalPinGc.LastValue;
                    }
                    Require(result, additionalPinGc.Valid,
                        "Additional-pin GC ProfilerRecorder counter is unavailable ("
                        + additionalPinGcName + ").");
                    Require(result, additionalPinGc.UnitType
                            == ProfilerMarkerDataUnit.Bytes,
                        "Additional-pin GC counter does not report bytes: "
                        + additionalPinGc.UnitType + ".");
                }
                result.maximumGcAllocatedInFrame = additionalPinMaximumGc;
                Require(result, additionalPinCriticalPathMaximumGc == 0,
                    "Hairibar additional-pin critical path allocated managed memory: "
                    + additionalPinCriticalPathMaximumGc
                    + " bytes. Whole-frame ambient maximum was "
                    + additionalPinMaximumGc + " bytes.");
                var additionalPinPath = NewCriticalPath(
                    "additional-pin",
                    "RagdollPropMuscle.ApplyAdditionalPinAfterAnimationMatching");
                additionalPinPath.samples = MeasurementFrames;
                additionalPinPath.gcAllocatedBytes =
                    additionalPinCriticalPathTotalGc;
                additionalPinPath.maxGcAllocatedBytesInFrame =
                    additionalPinCriticalPathMaximumGc;
                FinalizeCriticalPath(additionalPinPath);
                result.criticalPaths = new[] { additionalPinPath };

                yield return fixedUpdate;
                List<RagdollRuntimeMuscleRegistration> collection =
                    BuildCurrentCollection(rig, slotJoint, targetSlot);
                replacementObject = CreateReplacementChild(
                    rig, rootBody, out RagdollRuntimeMuscleRegistration replacement);
                collection[1] = replacement;
                RagdollHierarchyTransactionResult transaction;
                Require(result, rig.Setup.Animator.TrySetMuscles(
                    collection, out transaction), transaction.Error);
                Require(result, transaction.Succeeded && prop.IsHeld,
                    "Collection replacement lost the held prop.");
                Require(result, prop.AdditionalPin.Enabled,
                    "Collection replacement lost additional pin settings.");

                int generation = rig.Bindings.RegistryGeneration;
                collection.Add(collection[0]);
                RagdollHierarchyTransactionResult rejected;
                Require(result, !rig.Setup.Animator.TrySetMuscles(
                    collection, out rejected),
                    "Invalid collection unexpectedly committed.");
                Require(result, rig.Bindings.RegistryGeneration == generation
                    && prop.IsHeld && prop.CurrentRigidbody == slotBody,
                    "Rejected collection did not roll back ownership exactly.");

                muscle.Drop();
                for (int frame = 0; frame < TransitionFrameLimit
                    && muscle.State != RagdollPropMuscleState.Empty; frame++)
                    yield return fixedUpdate;
                Require(result, muscle.State == RagdollPropMuscleState.Empty,
                    "Prop drop did not complete after hierarchy replacement.");
                Require(result, prop.CurrentRigidbody != null && !prop.IsHeld,
                    "Drop did not restore a standalone Rigidbody.");
                result.succeeded = string.IsNullOrEmpty(result.error);
            }
            finally
            {
                Application.targetFrameRate = originalTargetFrameRate;
                QualitySettings.vSyncCount = originalVSyncCount;
                if (replacementObject) Destroy(replacementObject);
                if (propObject) Destroy(propObject);
                if (slotObject) Destroy(slotObject);
                DestroyRig(rig);
            }
        }

        IEnumerator RunPerformanceMatrix(ScenarioResult result)
        {
            int[] populations = { 1, 10, 25, 50 };
            string[] modes = { "ActiveTree", "ActiveFlat", "Kinematic", "Disabled" };
            List<RigInstance> rigs = new List<RigInstance>(50);
            List<PerformanceResult> measurements =
                new List<PerformanceResult>(populations.Length * modes.Length);
            try
            {
                for (int index = 0; index < 50; index++)
                {
                    RigInstance rig = CreateRig(new Vector3(
                        (index % 10) * 3f, 3f + index / 10, (index / 10) * 3f),
                        result);
                    if (rig == null) yield break;
                    rigs.Add(rig);
                }
                yield return null;

                for (int populationIndex = 0;
                    populationIndex < populations.Length; populationIndex++)
                {
                    int population = populations[populationIndex];
                    SetPopulation(rigs, population);
                    for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
                    {
                        ConfigurePerformanceMode(
                            rigs, population, modes[modeIndex], result);
                        yield return null;
                        for (int frame = 0; frame < WarmupFrames; frame++)
                            yield return null;

                        long[] cpu = new long[MeasurementFrames];
                        long[] memory = new long[MeasurementFrames];
                        long maxGc = 0;
                        string cpuCounterName;
                        string memoryCounterName;
                        string gcCounterName;
                        using (ProfilerRecorder cpuRecorder = StartAvailableRecorder(
                            new[] { "CPU Main Thread Frame Time", "Main Thread" },
                            out cpuCounterName))
                        using (ProfilerRecorder memoryRecorder = StartAvailableRecorder(
                            new[] { "System Used Memory", "Total Reserved Memory" },
                            out memoryCounterName))
                        using (ProfilerRecorder gcRecorder = StartAvailableRecorder(
                            new[] { "GC Allocated In Frame" }, out gcCounterName))
                        {
                            for (int frame = 0; frame < MeasurementFrames; frame++)
                            {
                                yield return null;
                                cpu[frame] = cpuRecorder.LastValue;
                                memory[frame] = memoryRecorder.LastValue;
                                if (gcRecorder.LastValue > maxGc)
                                    maxGc = gcRecorder.LastValue;
                            }
                            Require(result, cpuRecorder.Valid
                                && memoryRecorder.Valid && gcRecorder.Valid,
                                "A required ProfilerRecorder counter is unavailable: CPU="
                                + cpuCounterName + ", memory=" + memoryCounterName
                                + ", GC=" + gcCounterName + ".");
                            Require(result, cpuRecorder.UnitType
                                    == ProfilerMarkerDataUnit.TimeNanoseconds,
                                "CPU ProfilerRecorder does not report nanoseconds: "
                                + cpuRecorder.UnitType + ".");
                            Require(result, memoryRecorder.UnitType
                                    == ProfilerMarkerDataUnit.Bytes,
                                "Memory ProfilerRecorder does not report bytes: "
                                + memoryRecorder.UnitType + ".");
                            Require(result, gcRecorder.UnitType
                                    == ProfilerMarkerDataUnit.Bytes,
                                "GC ProfilerRecorder does not report bytes: "
                                + gcRecorder.UnitType + ".");
                        }
                        measurements.Add(new PerformanceResult
                        {
                            puppets = population,
                            mode = modes[modeIndex],
                            cpuMedianNanoseconds = Percentile(cpu, 0.5f),
                            cpuP95Nanoseconds = Percentile(cpu, 0.95f),
                            memoryMedianBytes = Percentile(memory, 0.5f),
                            memoryP95Bytes = Percentile(memory, 0.95f),
                            maximumGcAllocatedInFrame = maxGc
                        });
                        if (maxGc > result.maximumGcAllocatedInFrame)
                            result.maximumGcAllocatedInFrame = maxGc;
                        // This is a whole-frame ambient diagnostic. The exact
                        // critical paths have separate synchronous allocation gates.
                    }
                }
                result.performance = measurements.ToArray();
                result.succeeded = string.IsNullOrEmpty(result.error);
            }
            finally
            {
                for (int index = 0; index < rigs.Count; index++)
                    DestroyRig(rigs[index]);
            }
        }

        RigInstance CreateRig(
            Vector3 position,
            ScenarioResult result,
            bool includeHumanoidSystems = false,
            Animator targetAnimator = null)
        {
            if (!ragdollPrefab || !animationProfile)
            {
                Fail(result, "Generated regression rig or profile is missing.");
                return null;
            }
            GameObject owner = Instantiate(ragdollPrefab, position,
                Quaternion.identity);
            RagdollDefinitionBindings bindings = owner
                .GetComponentInChildren<RagdollDefinitionBindings>(true);
            Transform puppet = bindings ? bindings.transform : null;
            Transform target = null;
            for (int index = 0; index < owner.transform.childCount; index++)
            {
                Transform child = owner.transform.GetChild(index);
                if (child != puppet) target = child;
            }
            if (!bindings || !bindings.IsInitialized || !target)
            {
                Destroy(owner);
                Fail(result, "Generated regression bindings did not initialize.");
                return null;
            }
            foreach (RagdollBone bone in bindings.Bones)
            {
                bone.PowerSetting = PowerSetting.Powered;
                bone.Rigidbody.isKinematic = false;
            }
            if (includeHumanoidSystems) target.gameObject.SetActive(false);
            RagdollSetupResult setup =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    target, bindings, animationProfile, 30, 31);
            if (!setup.Succeeded)
            {
                Destroy(owner);
                Fail(result, setup.Error);
                return null;
            }
            RagdollFallBehaviour fall = null;
            RegressionDeterministicIKSolver solver = null;
            RagdollIKScheduler scheduler = null;
            if (includeHumanoidSystems)
            {
                setup.Animator.TargetAnimator = targetAnimator;
                // Behaviours are discovered below Character Behaviours when the
                // controller initializes. The Target is deliberately inactive until
                // every behaviour has been authored, matching the documented
                // PuppetMaster lifecycle where behaviours are initiated by their
                // owning PuppetMaster/controller.
                fall = setup.PuppetBehaviour.gameObject
                    .AddComponent<RagdollFallBehaviour>();
                fall.StateName = "Fall";
                fall.BlendParameter = "FallBlend";
                fall.CanEnd = false;
                fall.enabled = false;
                solver = target.gameObject
                    .AddComponent<RegressionDeterministicIKSolver>();
                scheduler = target.gameObject.AddComponent<RagdollIKScheduler>();
                scheduler.Configure(
                    setup.Animator,
                    RagdollIKSolvePhase.BeforePhysics,
                    new MonoBehaviour[] { solver });
                target.gameObject.SetActive(true);
            }
            Transform physicalChild = puppet.childCount > 0
                ? puppet.GetChild(0)
                : null;
            return new RigInstance
            {
                Owner = owner,
                Setup = setup,
                Bindings = bindings,
                Fall = fall,
                IKSolver = solver,
                IKScheduler = scheduler,
                OriginalChildParent = physicalChild ? physicalChild.parent : null
            };
        }

        static List<RagdollRuntimeMuscleRegistration> BuildCurrentCollection(
            RigInstance rig,
            ConfigurableJoint slotJoint,
            Transform targetSlot)
        {
            List<RagdollRuntimeMuscleRegistration> result =
                new List<RagdollRuntimeMuscleRegistration>(3);
            RagdollTargetBindings targets = rig.Setup.Target
                .GetComponent<RagdollTargetBindings>();
            for (int index = 0; index < rig.Bindings.IndexedBones.Count; index++)
            {
                RagdollBone bone = rig.Bindings.IndexedBones[index];
                bool isProp = bone.Name.ToString() == "Prop";
                RagdollTargetBinding target = null;
                if (!isProp && !targets.TryGetBinding(bone.Name, out target))
                    throw new InvalidOperationException(
                        "Missing target binding for " + bone.Name + ".");
                RagdollMuscleGroup group = bone.IsRoot
                    ? RagdollMuscleGroup.Hips
                    : isProp
                        ? RagdollMuscleGroup.Prop
                        : RagdollMuscleGroup.Spine;
                Transform resolvedTarget = isProp ? targetSlot : target.Target;
                result.Add(new RagdollRuntimeMuscleRegistration(
                    bone.Name, bone.Joint, resolvedTarget, group,
                    resolvedTarget.parent, false, true));
            }
            return result;
        }

        static GameObject CreateReplacementChild(
            RigInstance rig,
            Rigidbody rootBody,
            out RagdollRuntimeMuscleRegistration registration)
        {
            GameObject owner = new GameObject("ReplacementChildOwner");
            GameObject physical = new GameObject("ReplacementChild");
            physical.transform.SetParent(owner.transform, false);
            physical.transform.position = rootBody.position + Vector3.up;
            physical.AddComponent<Rigidbody>().mass = 2f;
            ConfigurableJoint joint = physical.AddComponent<ConfigurableJoint>();
            joint.connectedBody = rootBody;
            physical.AddComponent<BoxCollider>();
            Transform target = new GameObject("ReplacementChildTarget").transform;
            target.SetParent(rig.Setup.Target, false);
            target.localPosition = Vector3.up;
            registration = new RagdollRuntimeMuscleRegistration(
                new BoneName("Child"), joint, target,
                RagdollMuscleGroup.Spine, rig.Setup.Target, false, true);
            return owner;
        }

        static void ConfigurePerformanceMode(
            List<RigInstance> rigs,
            int population,
            string mode,
            ScenarioResult result)
        {
            for (int index = 0; index < population; index++)
            {
                RigInstance rig = rigs[index];
                if (mode == "ActiveFlat")
                {
                    rig.Setup.Animator.FlattenHierarchy();
                    Require(result, rig.Setup.Animator.HierarchyIsFlat(),
                        "FlattenHierarchy did not produce a flat puppet.");
                }
                else
                {
                    rig.Setup.Animator.TreeHierarchy();
                    Require(result, !rig.Setup.Animator.HierarchyIsFlat(),
                        "TreeHierarchy did not restore authored parenting.");
                }
                RagdollSimulationMode requested = mode == "Kinematic"
                    ? RagdollSimulationMode.Kinematic
                    : mode == "Disabled"
                        ? RagdollSimulationMode.Disabled
                        : RagdollSimulationMode.Active;
                rig.Setup.Simulation.SetModeImmediate(requested);
            }
        }

        static Transform FindPhysicalChild(RigInstance rig)
        {
            foreach (RagdollBone bone in rig.Bindings.Bones)
                if (!bone.IsRoot) return bone.Transform;
            return null;
        }

        static void SetPopulation(List<RigInstance> rigs, int count)
        {
            for (int index = 0; index < rigs.Count; index++)
                rigs[index].Owner.SetActive(index < count);
        }

        static long Percentile(long[] samples, float percentile)
        {
            Array.Sort(samples);
            int index = Mathf.Clamp(
                Mathf.CeilToInt(samples.Length * percentile) - 1,
                0, samples.Length - 1);
            return samples[index];
        }

        static ProfilerRecorder StartAvailableRecorder(
            string[] preferredNames,
            out string resolvedName)
        {
            List<ProfilerRecorderHandle> handles =
                new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            for (int preferredIndex = 0;
                preferredIndex < preferredNames.Length;
                preferredIndex++)
            {
                string preferred = preferredNames[preferredIndex];
                for (int handleIndex = 0;
                    handleIndex < handles.Count;
                    handleIndex++)
                {
                    ProfilerRecorderDescription description =
                        ProfilerRecorderHandle.GetDescription(handles[handleIndex]);
                    if (!string.Equals(
                        description.Name,
                        preferred,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    resolvedName = description.Category.Name + "/"
                        + description.Name;
                    return ProfilerRecorder.StartNew(
                        description.Category,
                        description.Name,
                        1);
                }
            }

            resolvedName = "Unavailable";
            return default;
        }

        static bool IsValidHumanoid(Animator animator)
        {
            return animator && animator.avatar && animator.avatar.isValid
                && animator.avatar.isHuman;
        }

        static bool AllKinematic(Rigidbody[] bodies)
        {
            for (int index = 0; index < bodies.Length; index++)
                if (bodies[index] && !bodies[index].isKinematic) return false;
            return true;
        }

        static bool HasDynamicBody(Rigidbody[] bodies)
        {
            for (int index = 0; index < bodies.Length; index++)
                if (bodies[index] && !bodies[index].isKinematic) return true;
            return false;
        }

        static bool VelocitiesAreZero(Rigidbody[] bodies)
        {
            for (int index = 0; index < bodies.Length; index++)
                if (bodies[index] && (bodies[index].linearVelocity.sqrMagnitude
                    > 0.000001f || bodies[index].angularVelocity.sqrMagnitude
                    > 0.000001f)) return false;
            return true;
        }

        static void RequireFinite(ScenarioResult result, Rigidbody[] bodies)
        {
            for (int index = 0; index < bodies.Length; index++)
            {
                if (!bodies[index]) continue;
                Require(result, IsFinite(bodies[index].position)
                    && IsFinite(bodies[index].linearVelocity)
                    && IsFinite(bodies[index].angularVelocity),
                    "PhysX produced a non-finite muscle state.");
            }
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static void Require(ScenarioResult result, bool condition, string error)
        {
            result.assertionBuffer.Add(new ScenarioAssertion
            {
                name = string.IsNullOrWhiteSpace(error)
                    ? "Unnamed assertion"
                    : error,
                succeeded = condition
            });
            if (!condition) Fail(result, error);
        }

        static void Fail(ScenarioResult result, string error)
        {
            if (string.IsNullOrEmpty(error)) error = "Unspecified certification failure.";
            result.error = string.IsNullOrEmpty(result.error)
                ? error
                : result.error + " | " + error;
            result.succeeded = false;
        }

        static void DestroyRig(RigInstance rig)
        {
            if (rig != null && rig.Owner) Destroy(rig.Owner);
        }

        static void WriteResultAndQuit()
        {
            bool succeeded = true;
            for (int index = 0; index < Results.Count; index++)
            {
                Results[index].assertions =
                    Results[index].assertionBuffer.ToArray();
                Results[index].assertionCount = Results[index].assertions.Length;
                succeeded &= Results[index].succeeded;
            }
            CertificationResult result = new CertificationResult
            {
                unityVersion = Application.unityVersion,
                platform = CertificationPlatformName(Application.platform),
                succeeded = succeeded,
                scenarios = Results.ToArray()
            };
            string path = Environment.GetEnvironmentVariable(
                "HAIRIBAR_CERTIFICATION_RESULT");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Application.persistentDataPath,
                    "hairibar-certification.json");
            WriteJson(path, result);

            string scenePath = Environment.GetEnvironmentVariable(
                "HAIRIBAR_SCENE_RESULTS");
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                WriteJson(scenePath, new SceneEvidence
                {
                    succeeded = succeeded,
                    scenes = Results.ToArray()
                });
            }

            string profilerPath = Environment.GetEnvironmentVariable(
                "HAIRIBAR_PROFILER_RESULTS");
            if (!string.IsNullOrWhiteSpace(profilerPath))
            {
                WriteJson(profilerPath, BuildProfilerEvidence(succeeded));
            }
            if (Application.isBatchMode) Application.Quit(succeeded ? 0 : 1);
        }

        static ProfilerEvidence BuildProfilerEvidence(bool playerSucceeded)
        {
            ScenarioResult performance = FindResult(
                RegressionScenario.CollisionsPerformance.ToString());
            ScenarioResult core = FindResult(
                RegressionScenario.CoreLifecycle.ToString());
            ScenarioResult props = FindResult(
                RegressionScenario.HierarchyProps.ToString());
            ScenarioResult baker = FindResult(
                RegressionScenario.HumanoidBakerFall.ToString());
            PerformanceResult activeTree = null;
            if (performance != null && performance.performance != null)
            {
                for (int index = 0; index < performance.performance.Length; index++)
                {
                    PerformanceResult candidate = performance.performance[index];
                    if (candidate != null && candidate.puppets == 50
                        && string.Equals(candidate.mode, "ActiveTree",
                            StringComparison.Ordinal))
                    {
                        activeTree = candidate;
                        break;
                    }
                }
            }

            var paths = new[]
            {
                FindCriticalPath(core, "matching"),
                FindCriticalPath(core, "mapping"),
                FindCriticalPath(core, "collision-relay"),
                FindCriticalPath(core, "com"),
                FindCriticalPath(props, "additional-pin"),
                FindCriticalPath(baker, "baker-realtime")
            };
            bool complete = playerSucceeded && activeTree != null;
            for (int index = 0; index < paths.Length; index++)
                complete &= paths[index].succeeded;
            return new ProfilerEvidence
            {
                succeeded = complete,
                warmupFrames = WarmupFrames,
                measuredFrames = MeasurementFrames,
                cpuMilliseconds = new DistributionEvidence
                {
                    median = activeTree != null
                        ? activeTree.cpuMedianNanoseconds / 1000000d
                        : -1d,
                    p95 = activeTree != null
                        ? activeTree.cpuP95Nanoseconds / 1000000d
                        : -1d
                },
                memoryBytes = new DistributionEvidence
                {
                    median = activeTree != null
                        ? activeTree.memoryMedianBytes
                        : -1d,
                    p95 = activeTree != null
                        ? activeTree.memoryP95Bytes
                        : -1d
                },
                criticalPaths = paths
            };
        }

        static CriticalPathEvidence FindCriticalPath(
            ScenarioResult source,
            string name)
        {
            if (source != null && source.criticalPaths != null)
            {
                for (int index = 0; index < source.criticalPaths.Length; index++)
                {
                    CriticalPathEvidence candidate = source.criticalPaths[index];
                    if (candidate != null && string.Equals(
                        candidate.name, name, StringComparison.Ordinal))
                        return candidate;
                }
            }
            return NewCriticalPath(name, "missing");
        }

        static CriticalPathEvidence NewCriticalPath(string name, string scope)
        {
            return new CriticalPathEvidence
            {
                name = name,
                measurementScope = scope,
                succeeded = false,
                samples = 0,
                gcAllocatedBytes = 0L,
                maxGcAllocatedBytesInFrame = 0L
            };
        }

        static void AddCriticalSample(CriticalPathEvidence path, long bytes)
        {
            long sanitized = Math.Max(0L, bytes);
            path.samples++;
            path.gcAllocatedBytes += sanitized;
            if (sanitized > path.maxGcAllocatedBytesInFrame)
                path.maxGcAllocatedBytesInFrame = sanitized;
        }

        static void FinalizeCriticalPath(CriticalPathEvidence path)
        {
            path.succeeded = path.samples == MeasurementFrames
                && path.gcAllocatedBytes == 0L
                && path.maxGcAllocatedBytesInFrame == 0L;
        }

        static void RequireCriticalPath(
            ScenarioResult result,
            CriticalPathEvidence path)
        {
            Require(result, path.succeeded,
                path.name + " allocated " + path.gcAllocatedBytes
                + " bytes total; maximum scoped call="
                + path.maxGcAllocatedBytesInFrame + ".");
        }

        static ScenarioResult FindResult(string name)
        {
            for (int index = 0; index < Results.Count; index++)
                if (string.Equals(Results[index].name, name,
                    StringComparison.Ordinal)) return Results[index];
            return null;
        }

        static void WriteJson(string path, object value)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(value, true));
        }

        static string CertificationPlatformName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer: return "Windows64";
                case RuntimePlatform.LinuxPlayer: return "Linux64";
                case RuntimePlatform.OSXPlayer: return "macOS";
                case RuntimePlatform.WebGLPlayer: return "WebGL";
                default: return platform.ToString();
            }
        }
    }
}
