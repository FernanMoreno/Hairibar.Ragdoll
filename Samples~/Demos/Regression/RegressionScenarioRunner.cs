using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Hairibar.Ragdoll.Animation;
using Unity.Profiling;
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
        sealed class ScenarioResult
        {
            public string name;
            public bool succeeded;
            public string error;
            public long maximumGcAllocatedInFrame;
            public int assertions;
            public PerformanceResult[] performance;
        }

        [Serializable]
        sealed class CertificationResult
        {
            public string unityVersion;
            public string platform;
            public bool succeeded;
            public ScenarioResult[] scenarios;
        }

        sealed class RigInstance
        {
            internal GameObject Owner;
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
                performance = new PerformanceResult[0]
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
                else yield return current;
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
                    rig.Setup.Animator.Teleport(
                        respawn + Vector3.forward * 2f,
                        Quaternion.Euler(0f, 90f, 0f), true);
                    rig.Setup.Animator.PrepareManualSimulation(0.02f);
                    Require(result, !rig.Setup.Animator.HasPendingTeleport,
                        "Manual-simulation teleport was not consumed during prepare.");
                    Physics.Simulate(0.02f);
                    rig.Setup.Animator.CompleteManualSimulation();
                    Require(result,
                        !rig.Setup.Animator.IsManualSimulationPrepared,
                        "Manual simulation did not complete.");
                }
                finally
                {
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
            float originalScale = Time.timeScale;
            int originalTargetFrameRate = Application.targetFrameRate;
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
                string bakerError;
                Require(result, baker.StartBaking(out bakerError), bakerError);
                for (int frame = 0; frame < WarmupFrames; frame++)
                    yield return null;
                long bakerMaximumGc = 0;
                using (ProfilerRecorder bakerGc = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory, "GC Allocated In Frame", 1))
                {
                    for (int frame = 0; frame < MeasurementFrames; frame++)
                    {
                        yield return null;
                        if (bakerGc.LastValue > bakerMaximumGc)
                            bakerMaximumGc = bakerGc.LastValue;
                    }
                    Require(result, bakerGc.Valid,
                        "Baker GC ProfilerRecorder counter is unavailable.");
                }
                result.maximumGcAllocatedInFrame = bakerMaximumGc;
                Require(result, bakerMaximumGc == 0,
                    "Realtime Baker allocated after warm-up: "
                    + bakerMaximumGc + " bytes.");
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
                    RagdollBakerCompletionStatus.Canceled,
                    "Stopping realtime Baker did not report cancellation.");
                result.succeeded = string.IsNullOrEmpty(result.error);
            }
            finally
            {
                Time.timeScale = originalScale;
                Application.targetFrameRate = originalTargetFrameRate;
                Destroy(first);
                Destroy(second);
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
                using (ProfilerRecorder additionalPinGc = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory, "GC Allocated In Frame", 1))
                {
                    for (int frame = 0; frame < MeasurementFrames; frame++)
                    {
                        yield return null;
                        if (additionalPinGc.LastValue > additionalPinMaximumGc)
                            additionalPinMaximumGc = additionalPinGc.LastValue;
                    }
                    Require(result, additionalPinGc.Valid,
                        "Additional-pin GC ProfilerRecorder counter is unavailable.");
                }
                result.maximumGcAllocatedInFrame = additionalPinMaximumGc;
                Require(result, additionalPinMaximumGc == 0,
                    "Additional pin allocated after warm-up: "
                    + additionalPinMaximumGc + " bytes.");

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
                        ConfigurePerformanceMode(rigs, population, modes[modeIndex]);
                        yield return null;
                        for (int frame = 0; frame < WarmupFrames; frame++)
                            yield return null;

                        long[] cpu = new long[MeasurementFrames];
                        long[] memory = new long[MeasurementFrames];
                        long maxGc = 0;
                        using (ProfilerRecorder cpuRecorder = ProfilerRecorder.StartNew(
                            ProfilerCategory.Internal,
                            "CPU Main Thread Frame Time", 1))
                        using (ProfilerRecorder memoryRecorder = ProfilerRecorder.StartNew(
                            ProfilerCategory.Memory,
                            "Total Reserved Memory", 1))
                        using (ProfilerRecorder gcRecorder = ProfilerRecorder.StartNew(
                            ProfilerCategory.Memory,
                            "GC Allocated In Frame", 1))
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
                                "A required ProfilerRecorder counter is unavailable.");
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
                        Require(result, maxGc == 0,
                            "A warmed critical-path frame allocated managed memory: "
                            + population + " " + modes[modeIndex] + " = " + maxGc);
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

        RigInstance CreateRig(Vector3 position, ScenarioResult result)
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
            RagdollSetupResult setup =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    target, bindings, animationProfile, 30, 31);
            if (!setup.Succeeded)
            {
                Destroy(owner);
                Fail(result, setup.Error);
                return null;
            }
            Transform physicalChild = puppet.childCount > 0
                ? puppet.GetChild(0)
                : null;
            return new RigInstance
            {
                Owner = owner,
                Setup = setup,
                Bindings = bindings,
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
            string mode)
        {
            for (int index = 0; index < population; index++)
            {
                RigInstance rig = rigs[index];
                Transform child = FindPhysicalChild(rig);
                if (mode == "ActiveFlat" && child
                    && child.parent == rig.Setup.Puppet)
                {
                    child.SetParent(rig.Setup.Puppet.parent, true);
                }
                else if (mode != "ActiveFlat" && child
                    && child.parent != rig.Setup.Puppet)
                {
                    child.SetParent(rig.Setup.Puppet, true);
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
            result.assertions++;
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
                succeeded &= Results[index].succeeded;
            CertificationResult result = new CertificationResult
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                succeeded = succeeded,
                scenarios = Results.ToArray()
            };
            string path = Environment.GetEnvironmentVariable(
                "HAIRIBAR_CERTIFICATION_RESULT");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Application.persistentDataPath,
                    "hairibar-certification.json");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            if (Application.isBatchMode) Application.Quit(succeeded ? 0 : 1);
        }
    }
}
