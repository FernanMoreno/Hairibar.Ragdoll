using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    [DisallowMultipleComponent]
    public sealed class RagdollTelemetryRecorder : MonoBehaviour
    {
        [Header("Capture")]
        [SerializeField] Transform trackedRoot;
        [SerializeField] bool captureOnStart = true;
        [SerializeField] bool writeOnDisable = true;
        [SerializeField, Min(1)] int maxFrames = 3000;
        [SerializeField] string scenario = "Idle";
        [SerializeField] int seed;
        [SerializeField] string variant = "unspecified";
        [SerializeField] bool balancerEnabled;
        [SerializeField] string initialConditionFingerprint;
        [SerializeField] string pushDescriptor;
        [SerializeField] string tuningSessionId;
        [SerializeField] string experimentId;
        [SerializeField] string tuningRunRole = "none";
        [SerializeField] string configurationFingerprint;
        [SerializeField] string baselineConfigurationFingerprint;
        [SerializeField] string treatmentParameter;
        [SerializeField] bool treatmentValueAvailable;
        [SerializeField] float treatmentValue;
        [SerializeField] string outputDirectory = "RagdollLab/latest";
        [SerializeField] RagdollLabThresholds thresholds;
        [SerializeField] LayerMask groundLayers = ~0;
        [SerializeField] Transform supportReference;
        [Header("Penetration probe")]
        [SerializeField] bool probePenetration = true;
        [SerializeField] LayerMask penetrationLayers = ~0;
        [SerializeField, Min(1)] int maxPenetrationPairsPerStep = 64;
        [Header("Pose mapping (optional)")]
        [SerializeField] Animator targetAnimator;
        [SerializeField] Transform renderedRoot;

        readonly List<Rigidbody> bodies = new();
        readonly List<ConfigurableJoint> joints = new();
        readonly List<PhysicsFrame> frames = new();
        readonly List<ContactTelemetry> pendingContacts = new();
        readonly HashSet<string> activeContacts = new();
        readonly Dictionary<string, ContactIntervalState> contactIntervals = new();
        readonly List<EventMarker> eventMarkers = new();
        readonly Dictionary<string, float> footSlipDistance = new();
        readonly Dictionary<string, float> footContactDuration = new();
        readonly Dictionary<string, KinematicState> physicsKinematics = new();
        readonly HashSet<string> previousPairIds = new();
        readonly Collider[] penetrationBuffer = new Collider[128];
        string runId;
        string requestedRunId;
        bool capturing;
        long physicsStep;
        int frameIndex;
        string previousBalanceState = "Unavailable";
        string previousActiveBehaviour = "Unavailable";
        string currentStaggerEpisodeId;
        int staggerEpisodeSequence;
        bool hasPreviousSelectedFootSupport;
        bool previousSelectedFootSupport;
        Component puppetStateSource;
        Component behaviourControllerSource;
        Component staggerSource;
        Component simulationModeSource;
        Component animatorStateSource;
        PropertyInfo puppetStateProperty;
        PropertyInfo groundingProperty;
        PropertyInfo groundingEffectiveUpProperty;
        PropertyInfo groundingEffectiveUpAvailableProperty;
        PropertyInfo groundingSupportVelocityProperty;
        PropertyInfo groundingRelativeVelocityProperty;
        PropertyInfo groundingRelativeMotionAvailableProperty;
        PropertyInfo groundingSupportColliderIdProperty;
        PropertyInfo groundingSupportRigidbodyIdProperty;
        PropertyInfo activeBehaviourProperty;
        PropertyInfo lastStaggerClassificationProperty;
        PropertyInfo hasLastStaggerSnapshotProperty;
        PropertyInfo lastStaggerMarginProperty;
        PropertyInfo lastStaggerCapturePointProperty;
        PropertyInfo staggerCurrentStateProperty;
        PropertyInfo staggerMarginProperty;
        PropertyInfo staggerCapturePointProperty;
        PropertyInfo staggerPhaseProperty;
        PropertyInfo staggerStepCountProperty;
        PropertyInfo staggerSwingFootAvailableProperty;
        PropertyInfo staggerSwingFootNameProperty;
        PropertyInfo lastReactiveBalancerTorqueProperty;
        PropertyInfo lastReactiveBalancerAppliedProperty;
        PropertyInfo knockOutBoneProperty;
        PropertyInfo knockOutDistanceProperty;
        PropertyInfo knockOutThresholdProperty;
        PropertyInfo knockOutPinProperty;
        PropertyInfo simulationModeProperty;
        PropertyInfo mappingWeightProperty;
        PropertyInfo pinWeightProperty;
        PropertyInfo muscleWeightProperty;
        PropertyInfo muscleDamperProperty;
        PropertyInfo animatorInitiatedProperty;
        PropertyInfo animatedPairsProperty;
        PropertyInfo pairNameProperty;
        PropertyInfo pairTargetBoneProperty;
        PropertyInfo pairRagdollBoneProperty;
        PropertyInfo pairHandleProperty;
        PropertyInfo pairMappingWeightsProperty;
        PropertyInfo pairEffectiveMappingWeightsProperty;
        PropertyInfo pairEffectiveMappingAvailableProperty;
        PropertyInfo pairTargetLinearVelocityProperty;
        PropertyInfo pairTargetAngularVelocityProperty;
        PropertyInfo pairTargetLinearAccelerationProperty;
        PropertyInfo pairTargetAngularAccelerationProperty;
        PropertyInfo pairTargetLinearJerkProperty;
        PropertyInfo pairTargetAngularJerkProperty;
        PropertyInfo pairTargetSampleDeltaTimeProperty;
        PropertyInfo pairTargetSampleTimeProperty;
        PropertyInfo pairTargetKinematicsAvailableProperty;
        PropertyInfo pairTargetVelocityAvailableProperty;
        PropertyInfo pairTargetAccelerationAvailableProperty;
        PropertyInfo pairTargetJerkAvailableProperty;
        PropertyInfo pairTargetKinematicsResetProperty;
        PropertyInfo ragdollBoneRigidbodyProperty;
        bool previousPairSetAvailable;

        sealed class ContactIntervalState
        {
            public float startTime;
            public bool hasStartTime;
            public bool groundSupport;
            public float supportNormalDot;
            public Vector3 supportNormal;
            public Vector3 supportPoint;
            public Vector3 supportVelocity;
            public bool supportRelativeMotionAvailable;
            public string colliderA;
            public string colliderB;
        }

        sealed class KinematicState
        {
            public float time;
            public Vector3 linearVelocity;
            public Vector3 angularVelocity;
            public Vector3 linearAcceleration;
            public Vector3 angularAcceleration;
            public bool kinematicsAvailable;
        }

        sealed class PairCapture
        {
            public bool sourceAvailable;
            public readonly List<TargetPoseTelemetry> samples = new();
            public readonly List<string> warnings = new();
        }

        public IReadOnlyList<PhysicsFrame> Frames => frames;
        public bool IsCapturing => capturing;
        public int MaxFrames => maxFrames;
        public string OutputPath => Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Application.isEditor || Application.isBatchMode
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputDirectory)
                : Path.Combine(Application.persistentDataPath, outputDirectory);
        public string ScenarioName { get => scenario; set => scenario = value; }
        public string OutputDirectory { get => outputDirectory; set => outputDirectory = string.IsNullOrWhiteSpace(value) ? "Artifacts/RagdollLab/latest" : value; }
        public int Seed { get => seed; set => seed = value; }
        public string Variant { get => variant; set => variant = string.IsNullOrWhiteSpace(value) ? "unspecified" : value; }
        public bool BalancerEnabled { get => balancerEnabled; set => balancerEnabled = value; }
        public string InitialConditionFingerprint { get => initialConditionFingerprint; set => initialConditionFingerprint = value; }
        public string PushDescriptor { get => pushDescriptor; set => pushDescriptor = value; }
        public string RunId => runId;
        public bool CaptureOnStart { get => captureOnStart; set => captureOnStart = value; }
        public void SetMaxFrames(int value) => maxFrames = Mathf.Max(1, value);
        public void ConfigureTracking(Transform root) { trackedRoot = root; CachePhysics(); }
        public void ConfigurePoseMapping(Animator animator, Transform renderRoot)
        { targetAnimator = animator; renderedRoot = renderRoot; }
        public void ConfigureTuningRun(RagdollTuningRunBinding binding)
        {
            tuningSessionId = binding?.sessionId;
            experimentId = binding?.experimentId;
            tuningRunRole = string.IsNullOrWhiteSpace(binding?.runRole) ? "none" : binding.runRole;
            configurationFingerprint = binding?.configurationFingerprint;
            baselineConfigurationFingerprint = binding?.baselineConfigurationFingerprint;
            treatmentParameter = binding?.treatmentParameter;
            treatmentValueAvailable = binding != null && binding.treatmentValueAvailable;
            treatmentValue = binding?.treatmentValue ?? 0f;
            requestedRunId = binding?.runId;
            if (!string.IsNullOrWhiteSpace(binding?.artifactDirectory)) outputDirectory = binding.artifactDirectory;
        }

        void Awake()
        {
            trackedRoot ??= transform;
            thresholds ??= ScriptableObject.CreateInstance<RagdollLabThresholds>();
            CachePhysics();
        }

        void Start()
        {
            if (captureOnStart) Begin();
        }

        public void Begin()
        {
            if (capturing) return;
            CachePhysics();
            frames.Clear();
            eventMarkers.Clear();
            pendingContacts.Clear();
            activeContacts.Clear();
            contactIntervals.Clear();
            footSlipDistance.Clear();
            footContactDuration.Clear();
            physicsKinematics.Clear();
            previousPairIds.Clear();
            previousPairSetAvailable = false;
            previousBalanceState = "Unavailable";
            previousActiveBehaviour = "Unavailable";
            currentStaggerEpisodeId = null;
            staggerEpisodeSequence = 0;
            hasPreviousSelectedFootSupport = false;
            previousSelectedFootSupport = false;
            frameIndex = 0;
            physicsStep = 0;
            runId = string.IsNullOrWhiteSpace(requestedRunId)
                ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")
                : requestedRunId;
            requestedRunId = null;
            capturing = true;
            Debug.Log($"[RagdollLab] scenario start: {scenario} bodies={bodies.Count} joints={joints.Count}", this);
        }

        void FixedUpdate()
        {
            if (!capturing || frames.Count >= maxFrames) return;
            CaptureStep(Time.fixedTime);
        }

        public void ManualCaptureStep(float simulationTime)
        {
            if (!capturing || frames.Count >= maxFrames) return;
            CaptureStep(simulationTime);
        }

        void CaptureStep(float simulationTime)
        {
            RemoveDestroyedPhysicsReferences();
            if (probePenetration) ProbePenetration();
            frames.Add(CaptureFrame(simulationTime));
            physicsStep++; frameIndex++;
        }

        // Runtime systems are allowed to replace a Rigidbody (for example, a standalone
        // prop body is removed after ownership transfers to its PropMuscle). Retaining a
        // Unity fake-null reference would abort every later sample and manufacture a
        // four-frame "benchmark". The remaining bodies keep their stable IDs and order.
        void RemoveDestroyedPhysicsReferences()
        {
            for (int i = bodies.Count - 1; i >= 0; i--) if (!bodies[i]) bodies.RemoveAt(i);
            for (int i = joints.Count - 1; i >= 0; i--) if (!joints[i]) joints.RemoveAt(i);
        }

        void ProbePenetration()
        {
            int checkedPairs = 0;
            for (int i = 0; i < bodies.Count && checkedPairs < maxPenetrationPairsPerStep; i++)
            {
                Rigidbody body = bodies[i]; if (!body) continue;
                Collider[] ownColliders = body.GetComponents<Collider>();
                for (int c = 0; c < ownColliders.Length && checkedPairs < maxPenetrationPairsPerStep; c++)
                {
                    Collider own = ownColliders[c]; if (!own || own.isTrigger) continue;
                    Bounds bounds = own.bounds;
                    int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude, penetrationBuffer, penetrationLayers, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < count && checkedPairs < maxPenetrationPairsPerStep; j++)
                    {
                        Collider other = penetrationBuffer[j]; if (!other || other == own || IsTrackedCollider(other)) continue;
                        checkedPairs++;
                        if (!Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance)) continue;
                        if (distance <= 0f) continue;
                        string a = StableId(own.transform, "Collider"), b = StableId(other.transform, "Collider");
                        string key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
                        pendingContacts.Add(new ContactTelemetry { key = key, bodyA = StableId(body.transform, "Rigidbody"), bodyB = other.attachedRigidbody ? StableId(other.attachedRigidbody.transform, "Rigidbody") : "world",
                            colliderA = a, colliderB = b, point = new(other.ClosestPoint(own.bounds.center)), normal = new(direction), penetration = true, penetrationDepth = distance });
                    }
                }
            }
        }

        bool IsTrackedCollider(Collider candidate)
        {
            return candidate && (candidate.transform == trackedRoot || candidate.transform.IsChildOf(trackedRoot));
        }

        PhysicsFrame CaptureFrame(float simulationTime)
        {
            var bodyData = new BodyTelemetry[bodies.Count];
            for (int i = 0; i < bodies.Count; i++) bodyData[i] = CaptureBody(bodies[i]);
            var jointData = new JointTelemetry[joints.Count];
            for (int i = 0; i < joints.Count; i++) jointData[i] = CaptureJoint(joints[i]);
            ContactTelemetry[] contactData = pendingContacts.ToArray();
            pendingContacts.Clear();
            FootTelemetry[] feet = CaptureFeet(simulationTime);
            CharacterTelemetry character = CaptureCharacter(feet);
            BalanceFrameTelemetry balance = CaptureBalanceTelemetry(character);
            StaggerFrameTelemetry stagger = CaptureStaggerTelemetry(feet, balance);
            TargetPoseTelemetry[] poses = CaptureTargetPoses();
            PairCapture pairCapture = CaptureAnimatedPairs(simulationTime, bodyData);
            EventMarker[] events = eventMarkers.ToArray();
            eventMarkers.Clear();
            return new PhysicsFrame { frameIndex = frameIndex, physicsStepIndex = physicsStep,
                simulationTime = simulationTime, fixedDeltaTime = Time.fixedDeltaTime,
                bodies = bodyData, joints = jointData, contacts = contactData, character = character, targetPoses = poses, events = events, feet = feet,
                balance = balance, stagger = stagger,
                animatedPairCaptureAttempted = true,
                animatedPairSourceAvailable = pairCapture.sourceAvailable,
                animatedPairCount = pairCapture.samples.Count,
                animatedPairs = pairCapture.samples.ToArray(),
                mappingIntegrityWarnings = pairCapture.warnings.ToArray() };
        }

        public void MarkEvent(string name)
        {
            eventMarkers.Add(new EventMarker { name = name, simulationTime = Time.fixedTime, physicsStepIndex = physicsStep, frameIndex = frameIndex });
        }

        TargetPoseTelemetry[] CaptureTargetPoses()
        {
            if (!targetAnimator) return Array.Empty<TargetPoseTelemetry>();
            HumanBodyBones[] bonesToCapture = { HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.Hips,
                HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            var result = new List<TargetPoseTelemetry>(bonesToCapture.Length);
            for (int i = 0; i < bonesToCapture.Length; i++)
            {
                Transform target = targetAnimator.GetBoneTransform(bonesToCapture[i]);
                if (!target) continue;
                Transform rendered = renderedRoot ? FindByName(renderedRoot, target.name) : target;
                Rigidbody physics = FindBodyFor(target.name, bonesToCapture[i].ToString());
                if (!physics) continue;
                Vector3 renderedPosition = rendered ? rendered.position : target.position;
                Quaternion renderedRotation = rendered ? rendered.rotation : target.rotation;
                result.Add(new TargetPoseTelemetry { id = StableId(target, "Pose"), bone = bonesToCapture[i].ToString(),
                    physicsBodyId = StableId(physics.transform, "Rigidbody"),
                    targetPosition = new(target.position), physicsPosition = new(physics.position), renderedPosition = new(renderedPosition),
                    targetRotation = new(target.rotation), physicsRotation = new(physics.rotation), renderedRotation = new(renderedRotation),
                    targetPhysicsDistance = Vector3.Distance(target.position, physics.position),
                    physicsRenderedDistance = Vector3.Distance(physics.position, renderedPosition),
                    targetRenderedDistance = Vector3.Distance(target.position, renderedPosition),
                    targetPhysicsAngularError = Quaternion.Angle(target.rotation, physics.rotation),
                    physicsRenderedAngularError = Quaternion.Angle(physics.rotation, renderedRotation),
                    targetRenderedAngularError = Quaternion.Angle(target.rotation, renderedRotation) });
            }
            return result.ToArray();
        }

        PairCapture CaptureAnimatedPairs(float simulationTime, BodyTelemetry[] bodyData)
        {
            var capture = new PairCapture();
            if (!animatorStateSource || animatedPairsProperty == null)
            {
                physicsKinematics.Clear();
                previousPairIds.Clear();
                previousPairSetAvailable = false;
                return capture;
            }

            if (animatorInitiatedProperty != null
                && !ReadBool(animatorStateSource, animatorInitiatedProperty))
            {
                physicsKinematics.Clear();
                previousPairIds.Clear();
                previousPairSetAvailable = false;
                return capture;
            }

            object sourcePairs = ReadValue(animatorStateSource, animatedPairsProperty);
            if (!(sourcePairs is IEnumerable enumerable))
            {
                physicsKinematics.Clear();
                previousPairIds.Clear();
                previousPairSetAvailable = false;
                return capture;
            }

            capture.sourceAvailable = true;
            var currentPairIds = new HashSet<string>();
            var currentPhysicsStateKeys = new HashSet<string>();
            foreach (object pair in enumerable)
            {
                if (pair == null)
                {
                    capture.warnings.Add("animated_pair_null");
                    continue;
                }

                string boneName = ReadName(pair, pairNameProperty);
                Transform target = ReadValue(pair, pairTargetBoneProperty) as Transform;
                object ragdollBone = ReadValue(pair, pairRagdollBoneProperty);
                Rigidbody body = ReadValue(ragdollBone, ragdollBoneRigidbodyProperty) as Rigidbody;
                string targetId = target ? StableId(target, "Target") : "missing";
                string bodyId = body ? StableId(body.transform, "Rigidbody") : "missing";
                int handleIndex = ReadHandleIndex(ReadValue(pair, pairHandleProperty));
                string pairId = "AnimatedPair:" + handleIndex + ":" + boneName + "|" + targetId + "|" + bodyId;

                if (!currentPairIds.Add(pairId))
                {
                    capture.warnings.Add("duplicate_pair_id:" + pairId);
                    continue;
                }
                if (!target) capture.warnings.Add("missing_target:" + pairId);
                if (!body) capture.warnings.Add("missing_physics_body:" + pairId);

                TargetPoseTelemetry sample = new TargetPoseTelemetry
                {
                    id = pairId,
                    pairId = pairId,
                    bone = boneName,
                    targetTransformId = targetId,
                    physicsBodyId = bodyId,
                    sourceAvailable = true,
                    targetAvailable = target != null,
                    physicsAvailable = body != null,
                    targetSampleTime = ReadFloat(pair, pairTargetSampleTimeProperty),
                    sampleDeltaTime = ReadFloat(pair, pairTargetSampleDeltaTimeProperty),
                    targetKinematicsAvailable = ReadBool(pair, pairTargetKinematicsAvailableProperty),
                    targetVelocityAvailable = ReadBool(pair, pairTargetVelocityAvailableProperty),
                    targetAccelerationAvailable = ReadBool(pair, pairTargetAccelerationAvailableProperty),
                    targetJerkAvailable = ReadBool(pair, pairTargetJerkAvailableProperty),
                    targetKinematicsReset = ReadBool(pair, pairTargetKinematicsResetProperty),
                    targetLinearVelocity = new(ReadVector3(pair, pairTargetLinearVelocityProperty)),
                    targetAngularVelocity = new(ReadVector3(pair, pairTargetAngularVelocityProperty)),
                    targetLinearAcceleration = new(ReadVector3(pair, pairTargetLinearAccelerationProperty)),
                    targetAngularAcceleration = new(ReadVector3(pair, pairTargetAngularAccelerationProperty)),
                    targetLinearJerk = new(ReadVector3(pair, pairTargetLinearJerkProperty)),
                    targetAngularJerk = new(ReadVector3(pair, pairTargetAngularJerkProperty))
                };

                if (target)
                {
                    sample.targetPosition = new(target.position);
                    sample.targetRotation = new(target.rotation);
                    sample.targetTransformId = StableId(target, "Target");
                }
                if (body)
                {
                    sample.physicsPosition = new(body.position);
                    sample.physicsRotation = new(body.rotation);
                    sample.physicsLinearVelocity = new(body.linearVelocity);
                    sample.physicsAngularVelocity = new(body.angularVelocity);
                    sample.physicsAvailable = bodyData != null && HasBody(bodyData, bodyId);
                    sample.physicsVelocityAvailable = sample.physicsAvailable;
                    if (sample.physicsAvailable)
                    {
                        CapturePhysicsKinematics(
                            pairId,
                            simulationTime,
                            body.linearVelocity,
                            body.angularVelocity,
                            sample);
                        currentPhysicsStateKeys.Add(pairId);
                    }
                }
                if (target && body)
                {
                    sample.targetPhysicsDistance = Vector3.Distance(target.position, body.position);
                    sample.targetPhysicsAngularError = Quaternion.Angle(target.rotation, body.rotation);
                }

                object authoredWeights = ReadValue(pair, pairMappingWeightsProperty);
                sample.authoredMappingAvailable = authoredWeights != null;
                sample.authoredMappingPositionWeight = ReadMappingWeight(authoredWeights, "PositionWeight");
                sample.authoredMappingRotationWeight = ReadMappingWeight(authoredWeights, "RotationWeight");
                object effectiveWeights = ReadValue(pair, pairEffectiveMappingWeightsProperty);
                sample.effectiveMappingAvailable = ReadBool(pair, pairEffectiveMappingAvailableProperty)
                    && effectiveWeights != null;
                sample.effectiveMappingPositionWeight = ReadMappingWeight(effectiveWeights, "PositionWeight");
                sample.effectiveMappingRotationWeight = ReadMappingWeight(effectiveWeights, "RotationWeight");

                capture.samples.Add(sample);
            }

            if (previousPairSetAvailable && !SetEquals(previousPairIds, currentPairIds))
                capture.warnings.Add("animated_pair_identity_set_changed");
            previousPairIds.Clear();
            foreach (string id in currentPairIds) previousPairIds.Add(id);
            previousPairSetAvailable = true;

            var stalePhysicsKeys = new List<string>();
            foreach (string key in physicsKinematics.Keys)
                if (!currentPhysicsStateKeys.Contains(key)) stalePhysicsKeys.Add(key);
            for (int i = 0; i < stalePhysicsKeys.Count; i++) physicsKinematics.Remove(stalePhysicsKeys[i]);
            return capture;
        }

        void CapturePhysicsKinematics(
            string pairId,
            float simulationTime,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            TargetPoseTelemetry sample)
        {
            if (!RagdollLabMath.IsFinite(linearVelocity)
                || !RagdollLabMath.IsFinite(angularVelocity)
                || !RagdollLabMath.IsFinite(simulationTime))
            {
                sample.physicsKinematicsReset = true;
                return;
            }

            if (!physicsKinematics.TryGetValue(pairId, out KinematicState state))
            {
                state = new KinematicState
                {
                    time = simulationTime,
                    linearVelocity = linearVelocity,
                    angularVelocity = angularVelocity
                };
                physicsKinematics[pairId] = state;
                sample.physicsKinematicsReset = true;
                return;
            }

            float dt = simulationTime - state.time;
            if (!RagdollLabMath.IsFinite(dt) || dt <= 0.000001f)
            {
                sample.physicsKinematicsReset = true;
                return;
            }

            Vector3 acceleration = (linearVelocity - state.linearVelocity) / dt;
            Vector3 angularAcceleration = (angularVelocity - state.angularVelocity) / dt;
            bool previousAccelerationAvailable = state.kinematicsAvailable;
            Vector3 jerk = previousAccelerationAvailable
                ? (acceleration - state.linearAcceleration) / dt
                : Vector3.zero;
            Vector3 angularJerk = previousAccelerationAvailable
                ? (angularAcceleration - state.angularAcceleration) / dt
                : Vector3.zero;
            state.time = simulationTime;
            state.linearVelocity = linearVelocity;
            state.angularVelocity = angularVelocity;
            state.linearAcceleration = acceleration;
            state.angularAcceleration = angularAcceleration;
            state.kinematicsAvailable = true;
            sample.physicsLinearAcceleration = new(acceleration);
            sample.physicsAngularAcceleration = new(angularAcceleration);
            sample.physicsLinearJerk = new(jerk);
            sample.physicsAngularJerk = new(angularJerk);
            sample.physicsKinematicsAvailable = true;
            sample.physicsAccelerationAvailable = true;
            sample.physicsJerkAvailable = previousAccelerationAvailable;
            sample.sampleDeltaTime = dt;
        }

        static bool HasBody(BodyTelemetry[] bodiesData, string bodyId)
        {
            if (bodiesData == null || string.IsNullOrEmpty(bodyId) || bodyId == "missing") return false;
            for (int i = 0; i < bodiesData.Length; i++)
                if (bodiesData[i] != null && bodiesData[i].id == bodyId) return true;
            return false;
        }

        static bool SetEquals(HashSet<string> first, HashSet<string> second)
        {
            if (first.Count != second.Count) return false;
            foreach (string value in first) if (!second.Contains(value)) return false;
            return true;
        }

        Rigidbody FindBodyFor(string targetName, string boneName)
        {
            for (int i = 0; i < bodies.Count; i++) if (bodies[i] &&
                (string.Equals(bodies[i].name, targetName, StringComparison.OrdinalIgnoreCase) ||
                 bodies[i].name.IndexOf(boneName, StringComparison.OrdinalIgnoreCase) >= 0)) return bodies[i];
            return null;
        }

        static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { Transform found = FindByName(root.GetChild(i), name); if (found) return found; }
            return null;
        }

        BodyTelemetry CaptureBody(Rigidbody body)
        {
            return new BodyTelemetry { id = StableId(body.transform, "Rigidbody"), name = body.name,
                position = new(body.position), rotation = new(body.rotation),
                localPosition = new(body.transform.localPosition), localRotation = new(body.transform.localRotation),
                velocity = new(body.linearVelocity), angularVelocity = new(body.angularVelocity),
                worldCenterOfMass = new(body.worldCenterOfMass), inertiaTensor = new(body.inertiaTensor),
                inertiaTensorRotation = new(body.inertiaTensorRotation), mass = body.mass,
                isSleeping = body.IsSleeping(), isKinematic = body.isKinematic };
        }

        JointTelemetry CaptureJoint(ConfigurableJoint joint)
        {
            Vector3 a = joint.transform.TransformPoint(joint.anchor);
            Vector3 b = RagdollLabMath.ConnectedAnchorWorld(joint, a);
            string bodyId = StableId(joint.transform, "Rigidbody");
            string connectedId = joint.connectedBody != null ? StableId(joint.connectedBody.transform, "Rigidbody") : "world";
            return new JointTelemetry { id = StableId(joint.transform, "ConfigurableJoint"), name = joint.name,
                bodyId = bodyId, connectedBodyId = connectedId, worldAnchor = new(a), connectedWorldAnchor = new(b),
                anchorError = RagdollLabMath.JointAnchorError(joint), currentForce = new(joint.currentForce), currentTorque = new(joint.currentTorque),
                hasCurrentForce = true, hasCurrentTorque = true,
                relativeAngularSpeed = joint.connectedBody != null
                    ? ((joint.GetComponent<Rigidbody>() ? joint.GetComponent<Rigidbody>().angularVelocity : Vector3.zero) - joint.connectedBody.angularVelocity).magnitude
                    : joint.GetComponent<Rigidbody>() ? joint.GetComponent<Rigidbody>().angularVelocity.magnitude : 0f,
                distanceToNearestLimit = EstimateLimitDistance(joint) };
        }

        float EstimateLimitDistance(ConfigurableJoint joint)
        {
            float result = float.PositiveInfinity;
            if (joint.angularXMotion == ConfigurableJointMotion.Limited)
            {
                float angle = Mathf.Abs(Mathf.DeltaAngle(0f, joint.transform.localEulerAngles.x));
                result = Mathf.Min(Mathf.Abs(joint.lowAngularXLimit.limit - angle), Mathf.Abs(joint.highAngularXLimit.limit - angle));
            }
            if (joint.angularYMotion == ConfigurableJointMotion.Limited) result = Mathf.Min(result, joint.angularYLimit.limit);
            if (joint.angularZMotion == ConfigurableJointMotion.Limited) result = Mathf.Min(result, joint.angularZLimit.limit);
            return float.IsPositiveInfinity(result) ? 0f : Mathf.Max(0f, result);
        }

        CharacterTelemetry CaptureCharacter(FootTelemetry[] feet)
        {
            Vector3 weighted = Vector3.zero, velocity = Vector3.zero; float mass = 0f, energy = 0f;
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody body = bodies[i]; if (!body) continue;
                weighted += body.worldCenterOfMass * body.mass;
                velocity += body.worldCenterOfMass * 0f + body.linearVelocity * body.mass;
                mass += body.mass; energy += RagdollLabMath.KineticEnergy(body);
            }
            if (mass <= 0f) return new CharacterTelemetry { finite = false };
            Vector3 com = weighted / mass, comVelocity = velocity / mass;
            int support = 0;
            foreach (ContactIntervalState interval in contactIntervals.Values) if (interval.groundSupport) support++;
            var supportPoints = new List<Vector3>();
            if (feet != null) for (int i = 0; i < feet.Length; i++) if (feet[i].stance)
                if (feet[i].supportPointValid) supportPoints.Add(feet[i].supportPoint.ToVector3());
            Vector3 supportUp = EffectiveSupportUp();
            bool hasReference = false;
            Vector3 supportOrigin = Vector3.zero;
            if (supportReference && RagdollLabMath.IsFinite(supportReference.position) && RagdollLabMath.IsFinite(supportUp))
            {
                supportOrigin = supportReference.position;
                hasReference = true;
            }
            else if (supportPoints.Count > 0)
            {
                for (int i = 0; i < supportPoints.Count; i++) supportOrigin += supportPoints[i];
                supportOrigin /= supportPoints.Count;
                hasReference = RagdollLabMath.IsFinite(supportOrigin);
            }
            bool inside = RagdollSupportGeometry.Contains(com, supportPoints, thresholds.supportRadiusMeters, supportUp, out float margin);
            if (supportPoints.Count == 0) margin = -Mathf.Max(0f, thresholds.supportRadiusMeters);
            float heightAboveSupport = hasReference ? Vector3.Dot(com - supportOrigin, supportUp) : 0f;
            var telemetry = new CharacterTelemetry { centerOfMass = new(com), centerOfMassVelocity = new(comVelocity),
                totalMass = mass, kineticEnergy = energy, potentialEnergy = CalculatePotentialEnergy(), finite = RagdollLabMath.IsFinite(com),
                supportContactCount = support, likelyFallen = RagdollLabMath.IsLikelyFallen(com,
                    trackedRoot ? trackedRoot.rotation : Quaternion.identity, support, supportOrigin, supportUp,
                    thresholds.fallHeightMeters, hasReference), supportPointCount = supportPoints.Count,
                centerOfMassInsideSupport = inside, supportMarginMeters = margin,
                supportOrigin = new(supportOrigin), supportUp = new(supportUp), supportReferenceAvailable = hasReference,
                centerOfMassHeightAboveSupport = hasReference ? heightAboveSupport : 0f };
            CaptureRuntimeAuthority(telemetry);
            return telemetry;
        }

        FootTelemetry[] CaptureFeet(float simulationTime)
        {
            var result = new List<FootTelemetry>();
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody body = bodies[i]; if (!body || body.name.IndexOf("foot", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Collider[] colliders = body.GetComponentsInChildren<Collider>(true); bool stance = false;
                Vector3 supportPoint = Vector3.zero;
                Vector3 supportVelocity = Vector3.zero;
                int supportCount = 0;
                for (int c = 0; c < colliders.Length; c++)
                {
                    string colliderId = StableId(colliders[c].transform, "Collider");
                    foreach (ContactIntervalState interval in contactIntervals.Values)
                    {
                        if (!interval.groundSupport || (interval.colliderA != colliderId && interval.colliderB != colliderId)) continue;
                        stance = true;
                        supportPoint += interval.supportPoint;
                        supportVelocity += interval.supportVelocity;
                        supportCount++;
                    }
                }
                string id = StableId(body.transform, "Foot"); float dt = Time.fixedDeltaTime;
                Vector3 supportUp = EffectiveSupportUp();
                if (!footSlipDistance.ContainsKey(id)) footSlipDistance[id] = 0f;
                if (!footContactDuration.ContainsKey(id)) footContactDuration[id] = 0f;
                bool supportPointValid = supportCount > 0;
                if (supportPointValid) supportPoint /= supportCount;
                if (supportPointValid) supportVelocity /= supportCount;
                float slipSpeed = stance
                    ? Vector3.ProjectOnPlane(body.linearVelocity - supportVelocity, supportUp).magnitude
                    : 0f;
                if (stance) { footSlipDistance[id] += slipSpeed * dt; footContactDuration[id] += dt; }
                result.Add(new FootTelemetry { id = id, name = body.name, stance = stance, tangentialSlipSpeed = slipSpeed,
                    accumulatedSlipDistance = footSlipDistance[id], contactDuration = footContactDuration[id],
                    supportPoint = new(supportPoint), supportPointValid = supportPointValid && RagdollLabMath.IsFinite(supportPoint) });
            }
            return result.ToArray();
        }

        Vector3 EffectiveSupportUp()
        {
            if (supportReference && RagdollLabMath.IsFinite(supportReference.up) && supportReference.up.sqrMagnitude > 0.000001f)
                return supportReference.up.normalized;
            Vector3 gravity = Physics.gravity;
            if (RagdollLabMath.IsFinite(gravity) && gravity.sqrMagnitude > 0.000001f) return -gravity.normalized;
            return Vector3.up;
        }

        bool IsGroundLayer(Collider collider)
        {
            return collider && (groundLayers.value & (1 << collider.gameObject.layer)) != 0;
        }

        bool TryClassifyGroundSupport(Collision collision, Collider own, Collider other, out Vector3 bestPoint, out Vector3 bestNormal, out float bestDot)
        {
            bestPoint = Vector3.zero;
            bestNormal = Vector3.zero;
            bestDot = -1f;
            Collider ground = IsTrackedCollider(own) ? other : own;
            if (!ground || IsTrackedCollider(ground) || !IsGroundLayer(ground)) return false;
            Collider tracked = IsTrackedCollider(own) ? own : other;
            Vector3 up = EffectiveSupportUp();
            bool found = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint point = collision.GetContact(i);
                Vector3 normal = point.normal.normalized;
                Vector3 toTracked = tracked && tracked.attachedRigidbody
                    ? tracked.attachedRigidbody.worldCenterOfMass - point.point
                    : tracked ? tracked.transform.position - point.point : up;
                // ContactPoint.normal is the physical contact normal. Orient it toward
                // the tracked body so the support test remains correct regardless of
                // which collider Unity reports as thisCollider.
                if (Vector3.Dot(normal, toTracked) < 0f) normal = -normal;
                if (!RagdollLabMath.IsGroundSupportNormal(normal, up, thresholds.maximumGroundAngle, out float dot)
                    || dot <= bestDot) continue;
                bestPoint = point.point;
                bestNormal = normal;
                bestDot = dot;
                found = true;
            }
            return found;
        }

        float CalculatePotentialEnergy()
        {
            float result = 0f;
            for (int i = 0; i < bodies.Count; i++) if (bodies[i]) result += bodies[i].mass * -Physics.gravity.y * bodies[i].worldCenterOfMass.y;
            return result;
        }

        internal void RecordCollision(Collider relayCollider, Collision collision, bool start, bool stay, bool end)
        {
            if (!capturing || collision == null) return;
            Collider own = relayCollider;
            Collider other = collision.collider;
            ContactPoint first = default;
            if (collision.contactCount > 0)
            {
                first = collision.GetContact(0);
                for (int i = 0; i < collision.contactCount; i++)
                {
                    ContactPoint candidate = collision.GetContact(i);
                    if (candidate.thisCollider == relayCollider || candidate.otherCollider == relayCollider)
                    {
                        first = candidate;
                        break;
                    }
                }
                if (first.thisCollider != null && first.thisCollider != relayCollider) own = first.otherCollider;
                if (first.thisCollider == relayCollider) other = first.otherCollider != null ? first.otherCollider : collision.collider;
            }
            if (!own || !other) return;
            string a = own != null ? StableId(own.transform, "Collider") : "missing";
            string b = other != null ? StableId(other.transform, "Collider") : "missing";
            string key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
            ContactPoint point = first; Vector3 impulse = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++) impulse += collision.GetContact(i).impulse;
            Vector3 supportPoint = Vector3.zero;
            Vector3 supportNormal = Vector3.zero;
            float supportNormalDot = -1f;
            bool hasGroundContact = collision.contactCount > 0
                && TryClassifyGroundSupport(collision, own, other, out supportPoint, out supportNormal, out supportNormalDot);
            if (start) activeContacts.Add(key); if (end) activeContacts.Remove(key);
            contactIntervals.TryGetValue(key, out ContactIntervalState interval);
            if (start && interval == null)
            {
                interval = new ContactIntervalState { startTime = Time.fixedTime, hasStartTime = true, colliderA = a, colliderB = b };
                contactIntervals[key] = interval;
            }
            if (interval != null && supportNormalDot >= 0f)
            {
                interval.groundSupport = true;
                interval.supportNormalDot = Mathf.Max(interval.supportNormalDot, supportNormalDot);
                interval.supportNormal = supportNormal;
                interval.supportPoint = supportPoint;
                Collider ground = IsTrackedCollider(own) ? other : own;
                Rigidbody supportBody = ground ? ground.attachedRigidbody : null;
                interval.supportVelocity = supportBody
                    ? supportBody.GetPointVelocity(supportPoint)
                    : Vector3.zero;
                interval.supportRelativeMotionAvailable = ground
                    && RagdollLabMath.IsFinite(interval.supportVelocity);
            }
            float endTime = Time.fixedTime;
            bool hasStartTime = interval != null && interval.hasStartTime;
            float startTime = hasStartTime ? interval.startTime : 0f;
            bool hasDuration = end && hasStartTime && endTime >= startTime;
            bool groundSupport = interval != null && interval.groundSupport;
            float intervalNormalDot = interval != null ? interval.supportNormalDot : Mathf.Max(0f, supportNormalDot);
            if (end) contactIntervals.Remove(key);
            float penetrationDepth = 0f;
            for (int i = 0; i < collision.contactCount; i++) penetrationDepth = Mathf.Max(penetrationDepth, Mathf.Max(0f, -collision.GetContact(i).separation));
            pendingContacts.Add(new ContactTelemetry { key = key,
                bodyA = own != null && own.attachedRigidbody ? StableId(own.attachedRigidbody.transform, "Rigidbody") : "world",
                bodyB = other != null && other.attachedRigidbody ? StableId(other.attachedRigidbody.transform, "Rigidbody") : "world",
                colliderA = a, colliderB = b, point = new(point.point), normal = new(point.normal),
                relativeVelocity = new(collision.relativeVelocity), impulse = new(impulse), impulseMagnitude = impulse.magnitude,
                contactStart = start, contactStay = stay, contactEnd = end,
                hasContactStartTime = start && hasStartTime, hasContactEndTime = end,
                hasContactDuration = hasDuration, contactStartTime = startTime, contactEndTime = endTime,
                contactDurationSeconds = hasDuration ? endTime - startTime : 0f,
                groundSupport = groundSupport, supportNormalDot = intervalNormalDot,
                supportVelocity = new(interval != null && interval.supportRelativeMotionAvailable
                    ? interval.supportVelocity
                    : Vector3.zero),
                supportRelativeMotionAvailable = interval != null && interval.supportRelativeMotionAvailable,
                penetration = penetrationDepth > 0f, penetrationDepth = penetrationDepth });
        }

        public void End()
        {
            if (!capturing) return;
            capturing = false;
            WriteArtifacts();
            Debug.Log($"[RagdollLab] scenario finish: {scenario} frames={frames.Count} path={OutputPath}", this);
        }

        void OnDisable() { if (writeOnDisable) End(); }
        void OnApplicationQuit() { End(); }

        void CachePhysics()
        {
            bodies.Clear(); joints.Clear();
            if (!trackedRoot) return;
            bodies.AddRange(trackedRoot.GetComponentsInChildren<Rigidbody>(true));
            joints.AddRange(trackedRoot.GetComponentsInChildren<ConfigurableJoint>(true));
            Collider[] colliders = trackedRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                RagdollContactRelay relay = colliders[i].GetComponent<RagdollContactRelay>() ?? colliders[i].gameObject.AddComponent<RagdollContactRelay>();
                relay.Recorder = this;
            }
            bodies.Sort((a, b) => string.CompareOrdinal(StableId(a.transform, "Rigidbody"), StableId(b.transform, "Rigidbody")));
            joints.Sort((a, b) => string.CompareOrdinal(StableId(a.transform, "ConfigurableJoint"), StableId(b.transform, "ConfigurableJoint")));
            CacheRuntimeAuthoritySources();
        }

        void CacheRuntimeAuthoritySources()
        {
            puppetStateSource = null; behaviourControllerSource = null; staggerSource = null;
            simulationModeSource = null; animatorStateSource = null;
            MonoBehaviour[] components = trackedRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i]; if (!component) continue;
                string typeName = component.GetType().FullName;
                if (typeName == "Hairibar.Ragdoll.Animation.RagdollPuppetBehaviour") puppetStateSource = component;
                else if (typeName == "Hairibar.Ragdoll.Animation.RagdollBehaviourController") behaviourControllerSource = component;
                else if (typeName == "Hairibar.Ragdoll.Animation.RagdollBipedStaggerBehaviour") staggerSource = component;
                else if (typeName == "Hairibar.Ragdoll.Animation.RagdollSimulationModeController") simulationModeSource = component;
                else if (typeName == "Hairibar.Ragdoll.Animation.RagdollAnimator") animatorStateSource = component;
            }
            puppetStateProperty = puppetStateSource?.GetType().GetProperty("State");
            groundingProperty = puppetStateSource?.GetType().GetProperty("Grounding");
            Type groundingType = groundingProperty?.PropertyType;
            groundingEffectiveUpProperty = groundingType?.GetProperty("EffectiveUp");
            groundingEffectiveUpAvailableProperty = groundingType?.GetProperty("EffectiveUpAvailable");
            groundingSupportVelocityProperty = groundingType?.GetProperty("SupportVelocity");
            groundingRelativeVelocityProperty = groundingType?.GetProperty("RelativeCenterOfMassVelocity");
            groundingRelativeMotionAvailableProperty = groundingType?.GetProperty("HasRelativeMotion");
            groundingSupportColliderIdProperty = groundingType?.GetProperty("SupportColliderId");
            groundingSupportRigidbodyIdProperty = groundingType?.GetProperty("SupportRigidbodyId");
            activeBehaviourProperty = behaviourControllerSource?.GetType().GetProperty("ActiveBehaviour");
            lastStaggerClassificationProperty = puppetStateSource?.GetType().GetProperty("LastStaggerClassification");
            hasLastStaggerSnapshotProperty = puppetStateSource?.GetType().GetProperty("HasLastStaggerSnapshot");
            lastStaggerMarginProperty = puppetStateSource?.GetType().GetProperty("LastStaggerSignedSupportMargin");
            lastStaggerCapturePointProperty = puppetStateSource?.GetType().GetProperty("LastStaggerCapturePoint");
            lastReactiveBalancerTorqueProperty = puppetStateSource?.GetType().GetProperty("LastReactiveBalancerTorque");
            lastReactiveBalancerAppliedProperty = puppetStateSource?.GetType().GetProperty("LastReactiveBalancerApplied");
            staggerCurrentStateProperty = staggerSource?.GetType().GetProperty("CurrentState");
            staggerMarginProperty = staggerSource?.GetType().GetProperty("LastSignedSupportMargin");
            staggerCapturePointProperty = staggerSource?.GetType().GetProperty("LastCapturePoint");
            staggerPhaseProperty = staggerSource?.GetType().GetProperty("CurrentPhase");
            staggerStepCountProperty = staggerSource?.GetType().GetProperty("StepCount");
            staggerSwingFootAvailableProperty = staggerSource?.GetType().GetProperty("SwingFootAvailable");
            staggerSwingFootNameProperty = staggerSource?.GetType().GetProperty("SwingFootName");
            knockOutBoneProperty = puppetStateSource?.GetType().GetProperty("LastKnockOutBone");
            knockOutDistanceProperty = puppetStateSource?.GetType().GetProperty("LastKnockOutDistance");
            knockOutThresholdProperty = puppetStateSource?.GetType().GetProperty("LastKnockOutThreshold");
            knockOutPinProperty = puppetStateSource?.GetType().GetProperty("LastKnockOutEffectivePinWeight");
            simulationModeProperty = simulationModeSource?.GetType().GetProperty("CurrentMode")
                ?? simulationModeSource?.GetType().GetProperty("Mode");
            mappingWeightProperty = animatorStateSource?.GetType().GetProperty("MasterMappingWeight");
            pinWeightProperty = animatorStateSource?.GetType().GetProperty("MasterPinWeight");
            muscleWeightProperty = animatorStateSource?.GetType().GetProperty("MasterMuscleWeight");
            muscleDamperProperty = animatorStateSource?.GetType().GetProperty("MasterMuscleDamper");

            animatorInitiatedProperty = animatorStateSource?.GetType().GetProperty("Initiated");
            animatedPairsProperty = animatorStateSource?.GetType().GetProperty("AnimatedPairs");
            pairNameProperty = null;
            pairTargetBoneProperty = null;
            pairRagdollBoneProperty = null;
            pairHandleProperty = null;
            pairMappingWeightsProperty = null;
            pairEffectiveMappingWeightsProperty = null;
            pairEffectiveMappingAvailableProperty = null;
            pairTargetLinearVelocityProperty = null;
            pairTargetAngularVelocityProperty = null;
            pairTargetLinearAccelerationProperty = null;
            pairTargetAngularAccelerationProperty = null;
            pairTargetLinearJerkProperty = null;
            pairTargetAngularJerkProperty = null;
            pairTargetSampleDeltaTimeProperty = null;
            pairTargetSampleTimeProperty = null;
            pairTargetKinematicsAvailableProperty = null;
            pairTargetVelocityAvailableProperty = null;
            pairTargetAccelerationAvailableProperty = null;
            pairTargetJerkAvailableProperty = null;
            pairTargetKinematicsResetProperty = null;
            ragdollBoneRigidbodyProperty = null;
            if (animatedPairsProperty != null)
            {
                Type pairType = animatedPairsProperty.PropertyType.IsArray
                    ? animatedPairsProperty.PropertyType.GetElementType()
                    : animatedPairsProperty.PropertyType.IsGenericType
                        ? animatedPairsProperty.PropertyType.GetGenericArguments()[0]
                        : null;
                if (pairType != null)
                {
                    pairNameProperty = pairType.GetProperty("Name");
                    pairTargetBoneProperty = pairType.GetProperty("TargetBone");
                    pairRagdollBoneProperty = pairType.GetProperty("RagdollBone");
                    pairHandleProperty = pairType.GetProperty("Handle");
                    pairMappingWeightsProperty = pairType.GetProperty("MappingWeights");
                    pairEffectiveMappingWeightsProperty = pairType.GetProperty("EffectiveMappingWeights");
                    pairEffectiveMappingAvailableProperty = pairType.GetProperty("EffectiveMappingAvailable");
                    pairTargetLinearVelocityProperty = pairType.GetProperty("TargetLinearVelocity");
                    pairTargetAngularVelocityProperty = pairType.GetProperty("TargetAngularVelocity");
                    pairTargetLinearAccelerationProperty = pairType.GetProperty("TargetLinearAcceleration");
                    pairTargetAngularAccelerationProperty = pairType.GetProperty("TargetAngularAcceleration");
                    pairTargetLinearJerkProperty = pairType.GetProperty("TargetLinearJerk");
                    pairTargetAngularJerkProperty = pairType.GetProperty("TargetAngularJerk");
                    pairTargetSampleDeltaTimeProperty = pairType.GetProperty("TargetSampleDeltaTime");
                    pairTargetSampleTimeProperty = pairType.GetProperty("TargetSampleTime");
                    pairTargetKinematicsAvailableProperty = pairType.GetProperty("TargetKinematicsAvailable");
                    pairTargetVelocityAvailableProperty = pairType.GetProperty("TargetKinematicsAvailable");
                    pairTargetAccelerationAvailableProperty = pairType.GetProperty("TargetAccelerationAvailable");
                    pairTargetJerkAvailableProperty = pairType.GetProperty("TargetJerkAvailable");
                    pairTargetKinematicsResetProperty = pairType.GetProperty("TargetKinematicsReset");
                    Type boneType = pairRagdollBoneProperty?.PropertyType;
                    ragdollBoneRigidbodyProperty = boneType?.GetProperty("Rigidbody");
                }
            }
        }

        BalanceFrameTelemetry CaptureBalanceTelemetry(CharacterTelemetry character)
        {
            string activeBehaviour = ReadName(behaviourControllerSource, activeBehaviourProperty);
            if (activeBehaviour == "Unavailable" && puppetStateSource) activeBehaviour = puppetStateSource.GetType().Name;
            bool staggerActive = activeBehaviour.IndexOf("Stagger", StringComparison.OrdinalIgnoreCase) >= 0;
            string state = staggerActive
                ? ReadName(staggerSource, staggerCurrentStateProperty)
                : ReadName(puppetStateSource, lastStaggerClassificationProperty);
            bool hasSnapshot = staggerActive || ReadBool(puppetStateSource, hasLastStaggerSnapshotProperty);
            Vector3 capturePoint = staggerActive
                ? ReadVector3(staggerSource, staggerCapturePointProperty)
                : ReadVector3(puppetStateSource, lastStaggerCapturePointProperty);
            float margin = staggerActive
                ? ReadFloat(staggerSource, staggerMarginProperty)
                : ReadFloat(puppetStateSource, lastStaggerMarginProperty);
            object grounding = ReadValue(puppetStateSource, groundingProperty);
            Vector3 effectiveUp = ReadVector3(grounding, groundingEffectiveUpProperty);
            bool effectiveUpAvailable = ReadBool(grounding, groundingEffectiveUpAvailableProperty)
                && RagdollLabMath.IsFinite(effectiveUp)
                && effectiveUp.sqrMagnitude > 0.000001f;
            Vector3 supportVelocity = ReadVector3(grounding, groundingSupportVelocityProperty);
            Vector3 relativeVelocity = ReadVector3(grounding, groundingRelativeVelocityProperty);
            bool relativeMotionAvailable = ReadBool(grounding, groundingRelativeMotionAvailableProperty)
                && RagdollLabMath.IsFinite(relativeVelocity);
            var result = new BalanceFrameTelemetry
            {
                sourceAvailable = hasSnapshot && state != "Unavailable",
                activeBehaviour = activeBehaviour,
                state = state,
                hasCapturePoint = hasSnapshot && RagdollLabMath.IsFinite(capturePoint),
                capturePoint = new(capturePoint),
                hasSignedSupportMargin = hasSnapshot && RagdollLabMath.IsFinite(margin),
                signedSupportMargin = margin,
                supportOrigin = character != null ? character.supportOrigin : default,
                supportUp = character != null ? character.supportUp : default,
                supportReferenceAvailable = character != null && character.supportReferenceAvailable,
                effectiveUpAvailable = effectiveUpAvailable,
                effectiveUp = new(effectiveUpAvailable ? effectiveUp.normalized : Vector3.zero),
                relativeSupportMotionAvailable = relativeMotionAvailable,
                supportVelocity = new(RagdollLabMath.IsFinite(supportVelocity) ? supportVelocity : Vector3.zero),
                relativeCenterOfMassVelocity = new(relativeMotionAvailable ? relativeVelocity : Vector3.zero),
                supportColliderId = ReadInt(grounding, groundingSupportColliderIdProperty),
                supportRigidbodyId = ReadInt(grounding, groundingSupportRigidbodyIdProperty),
                hasBalancerTorque = ReadBool(puppetStateSource, lastReactiveBalancerAppliedProperty)
                    && RagdollLabMath.IsFinite(ReadVector3(puppetStateSource, lastReactiveBalancerTorqueProperty)),
                balancerTorque = new(ReadVector3(puppetStateSource, lastReactiveBalancerTorqueProperty))
            };
            result.transitionObserved = previousActiveBehaviour != "Unavailable"
                && (previousActiveBehaviour != result.activeBehaviour || previousBalanceState != result.state);
            result.transitionFrom = result.transitionObserved ? previousBalanceState : "Unavailable";
            result.transitionTo = result.transitionObserved ? result.state : "Unavailable";
            previousActiveBehaviour = result.activeBehaviour;
            previousBalanceState = result.state;
            return result;
        }

        StaggerFrameTelemetry CaptureStaggerTelemetry(FootTelemetry[] feet, BalanceFrameTelemetry balance)
        {
            string activeBehaviour = balance?.activeBehaviour ?? "Unavailable";
            bool episodeActive = activeBehaviour.IndexOf("Stagger", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(balance?.state, "RequiresStep", StringComparison.Ordinal);
            if (episodeActive && string.IsNullOrEmpty(currentStaggerEpisodeId))
                currentStaggerEpisodeId = $"{runId}-stagger-{++staggerEpisodeSequence:000}";

            string swingFoot = ReadName(staggerSource, staggerSwingFootNameProperty);
            bool swingFootAvailable = ReadBool(staggerSource, staggerSwingFootAvailableProperty)
                && swingFoot != "Unavailable";
            bool selectedSupport = swingFootAvailable && HasStanceFoot(feet, swingFoot);
            var result = new StaggerFrameTelemetry
            {
                sourceAvailable = staggerSource != null,
                episodeId = episodeActive ? currentStaggerEpisodeId : null,
                phase = ReadName(staggerSource, staggerPhaseProperty),
                swingFoot = swingFoot,
                swingFootAvailable = swingFootAvailable,
                stepCount = ReadInt(staggerSource, staggerStepCountProperty),
                selectedFootGroundSupport = selectedSupport
            };
            if (swingFootAvailable && hasPreviousSelectedFootSupport)
            {
                result.liftOffObserved = previousSelectedFootSupport && !selectedSupport;
                result.replantObserved = !previousSelectedFootSupport && selectedSupport;
            }
            hasPreviousSelectedFootSupport = swingFootAvailable;
            previousSelectedFootSupport = selectedSupport;
            if (!episodeActive && !string.IsNullOrEmpty(currentStaggerEpisodeId))
            {
                currentStaggerEpisodeId = null;
                hasPreviousSelectedFootSupport = false;
                previousSelectedFootSupport = false;
            }
            return result;
        }

        static bool HasStanceFoot(FootTelemetry[] feet, string swingFoot)
        {
            if (feet == null || string.IsNullOrEmpty(swingFoot)) return false;
            for (int i = 0; i < feet.Length; i++)
            {
                FootTelemetry foot = feet[i];
                if (foot == null || !foot.stance || !foot.supportPointValid) continue;
                if (foot.name.IndexOf(swingFoot, StringComparison.OrdinalIgnoreCase) >= 0
                    || swingFoot.IndexOf(foot.name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        void CaptureRuntimeAuthority(CharacterTelemetry telemetry)
        {
            telemetry.puppetState = ReadName(puppetStateSource, puppetStateProperty);
            telemetry.simulationMode = ReadName(simulationModeSource, simulationModeProperty);
            telemetry.masterMappingWeight = ReadFloat(animatorStateSource, mappingWeightProperty);
            telemetry.masterPinWeight = ReadFloat(animatorStateSource, pinWeightProperty);
            telemetry.masterMuscleWeight = ReadFloat(animatorStateSource, muscleWeightProperty);
            telemetry.masterMuscleDamper = ReadFloat(animatorStateSource, muscleDamperProperty);
            telemetry.knockOutBoneIndex = ReadHandleIndex(puppetStateSource, knockOutBoneProperty);
            telemetry.knockOutDistance = ReadFloat(puppetStateSource, knockOutDistanceProperty);
            telemetry.knockOutThreshold = ReadFloat(puppetStateSource, knockOutThresholdProperty);
            telemetry.knockOutEffectivePinWeight = ReadFloat(puppetStateSource, knockOutPinProperty);
        }

        static string ReadName(Component source, PropertyInfo property) => ReadName((object)source, property);

        static string ReadName(object source, PropertyInfo property)
        {
            object value = ReadValue(source, property);
            if (value is Component component) return component.GetType().Name;
            return value != null ? value.ToString() : "Unavailable";
        }

        static object ReadValue(object source, PropertyInfo property)
        {
            if (source == null || property == null) return null;
            if (source is UnityEngine.Object unityObject && !unityObject) return null;
            try { return property.GetValue(source); } catch { return null; }
        }

        static Vector3 ReadVector3(object source, PropertyInfo property)
        {
            object value = ReadValue(source, property);
            return value is Vector3 vector && RagdollLabMath.IsFinite(vector) ? vector : Vector3.zero;
        }

        static bool ReadBool(object source, PropertyInfo property)
        {
            return ReadValue(source, property) is bool value && value;
        }

        static int ReadInt(Component source, PropertyInfo property)
        {
            return ReadInt((object)source, property);
        }

        static int ReadInt(object source, PropertyInfo property)
        {
            return ReadValue(source, property) is int value ? Mathf.Max(0, value) : 0;
        }

        static float ReadFloat(object source, PropertyInfo property)
        {
            object value = ReadValue(source, property);
            return value is float number && RagdollLabMath.IsFinite(number) ? number : 0f;
        }

        static int ReadHandleIndex(Component source, PropertyInfo property)
        {
            return ReadHandleIndex(ReadValue(source, property));
        }

        static int ReadHandleIndex(object handle)
        {
            PropertyInfo index = handle?.GetType().GetProperty("Index");
            object value = ReadValue(handle, index);
            return value is int integer ? integer : -1;
        }

        static float ReadMappingWeight(object weights, string propertyName)
        {
            if (weights == null || string.IsNullOrEmpty(propertyName)) return 0f;
            return ReadFloat(weights, weights.GetType().GetProperty(propertyName));
        }

        void WriteArtifacts()
        {
            string directory = OutputPath;
            Directory.CreateDirectory(directory);
            EvaluationReport previous = RagdollLabComparison.Read(Path.Combine(directory, "evaluation.json"));
            var metadata = new RagdollLabMetadata { runId = runId, scenario = scenario, seed = seed,
                scenarioProfile = RagdollLabScenarioProfiles.Resolve(scenario).id,
                unityVersion = Application.unityVersion, physicsScene = gameObject.scene.name,
                fixedDeltaTime = Time.fixedDeltaTime, gravityMagnitude = Physics.gravity.magnitude,
                characterHeight = CalculateHeight(), totalMass = CalculateMass(), captureRoot = trackedRoot.name,
                startedUtc = DateTime.UtcNow.ToString("O"), variant = variant,
                balancerEnabled = balancerEnabled, initialConditionFingerprint = initialConditionFingerprint,
                pushDescriptor = pushDescriptor, tuningSessionId = tuningSessionId,
                experimentId = experimentId, runRole = tuningRunRole,
                configurationFingerprint = configurationFingerprint,
                baselineConfigurationFingerprint = baselineConfigurationFingerprint,
                treatmentParameter = treatmentParameter,
                treatmentValueAvailable = treatmentValueAvailable,
                treatmentValue = treatmentValue };
            var report = new EvaluationReport { metadata = metadata, frameCount = frames.Count,
                bodyCount = bodies.Count, jointCount = joints.Count, completed = frames.Count > 0 };
            report.scenarioReport = RagdollLabAnalyzer.Analyze(frames, metadata.characterHeight, metadata.totalMass, metadata.gravityMagnitude, thresholds);
            report.scenarioReport.name = scenario;
            ValidateFinite(report);
            report.balanceComparison = RagdollLabComparison.BuildBalanceComparison(previous, report, thresholds);
            report.diagnostics = RagdollLabAnalyzer.Diagnose(report, thresholds);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "evaluation.json"), json, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "frames.json"), JsonUtility.ToJson(new FrameContainer { frames = frames }, false), Encoding.UTF8);
            using var csv = new StreamWriter(Path.Combine(directory, "frames.csv"), false, Encoding.UTF8);
            csv.WriteLine("frameIndex,physicsStepIndex,simulationTime,fixedDeltaTime,bodyCount,jointCount");
            for (int i = 0; i < frames.Count; i++) { PhysicsFrame f = frames[i]; csv.WriteLine($"{f.frameIndex},{f.physicsStepIndex},{f.simulationTime:R},{f.fixedDeltaTime:R},{f.bodies.Length},{f.joints.Length}"); }
            ComparisonReport comparison = RagdollLabComparison.Build(report, previous);
            File.WriteAllText(Path.Combine(directory, "comparison.json"), JsonUtility.ToJson(comparison, true), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "balance-comparison.json"), JsonUtility.ToJson(report.balanceComparison, true), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "diagnostics.json"), JsonUtility.ToJson(report.diagnostics, true), Encoding.UTF8);
            WriteSummary(directory, report, comparison);
            if (!string.IsNullOrWhiteSpace(tuningSessionId))
            {
                var binding = new RagdollTuningRunBinding
                {
                    sessionId = tuningSessionId,
                    experimentId = experimentId,
                    runId = runId,
                    runRole = tuningRunRole,
                    artifactDirectory = directory,
                    configurationFingerprint = configurationFingerprint,
                    baselineConfigurationFingerprint = baselineConfigurationFingerprint,
                    treatmentParameter = treatmentParameter,
                    treatmentValueAvailable = treatmentValueAvailable,
                    treatmentValue = treatmentValue
                };
                var transport = new RagdollTuningFileArtifactTransport();
                if (!transport.TryWriteManifest(directory, binding, report, out _, out string reason))
                    Debug.LogError("[RagdollLab] tuning artifact manifest failed: " + reason, this);
            }
        }

        void WriteSummary(string directory, EvaluationReport report, ComparisonReport comparison)
        {
            using var summary = new StreamWriter(Path.Combine(directory, "summary.md"), false, Encoding.UTF8);
            summary.WriteLine($"# RagdollLab — {scenario}");
            summary.WriteLine($"\nFrames: {report.frameCount}\nBodies: {report.bodyCount}\nJoints: {report.jointCount}\nFinite data: {report.finiteData}");
            summary.WriteLine($"Scenario profile: `{report.metadata?.scenarioProfile ?? RagdollLabScenarioProfiles.UnavailableId}`");
            if (report.scenarioReport != null)
            {
                summary.WriteLine($"\nKinetic energy p95: {report.scenarioReport.kineticEnergy.p95:R} J");
                summary.WriteLine($"Foot slip mean: {report.scenarioReport.footSlipSpeed.mean:R} m/s");
                summary.WriteLine($"Dominant frequency: {report.scenarioReport.dominantFrequencyHz:R} Hz");
                summary.WriteLine($"Fallen frames: {report.scenarioReport.fallenFrameCount}; recovery: {report.scenarioReport.recoveryTimeSeconds:R} s");
                summary.WriteLine($"Balance telemetry: {report.scenarioReport.balanceTelemetryAvailable}; support samples/loss: {report.scenarioReport.supportSampleCount}/{report.scenarioReport.supportLossFrameCount}");
                summary.WriteLine($"Animated pairs: source={report.scenarioReport.animatedPairSourceAvailable}; count={report.scenarioReport.animatedPairCount}; samples={report.scenarioReport.animatedPairSampleCount}; mapping warnings={report.scenarioReport.mappingIntegrityWarnings?.Length ?? 0}");
                if (report.scenarioReport.topOffenderIds != null) summary.WriteLine("Top offenders: " + string.Join(", ", report.scenarioReport.topOffenderIds));

                bool wroteEventHeader = false;
                if (report.scenarioReport.joints != null) foreach (JointReport joint in report.scenarioReport.joints)
                {
                    if (joint.anchorErrorEvents == null || joint.anchorErrorEvents.Length == 0) continue;
                    if (!wroteEventHeader) { summary.WriteLine("\n## Anchor Drift Events"); wroteEventHeader = true; }
                    foreach (AnchorDriftEventReport evt in joint.anchorErrorEvents)
                    {
                        summary.WriteLine($"- `{joint.name}` @ {evt.eventName} (frame {evt.eventFrameIndex}, t={evt.eventSimulationTime:R}s): "
                            + $"baseline={evt.baseline:R}m peak={evt.peak:R}m (+{evt.peakOffsetSeconds:R}s) | "
                            + $"+50/100/250/500/1000ms = {evt.sample50ms:R}/{evt.sample100ms:R}/{evt.sample250ms:R}/{evt.sample500ms:R}/{evt.sample1000ms:R} | "
                            + $"settling={evt.settlingTimeSeconds:R}s | AUC={evt.aucError:R} m*s | timeAboveThreshold={evt.timeAboveThresholdSeconds:R}s");
                    }
                }
            }
            if (report.diagnostics != null && report.diagnostics.diagnostics.Count > 0)
            {
                summary.WriteLine($"\n## Diagnostics ({report.diagnostics.scenarioProfile}; profileAvailable={report.diagnostics.profileAvailable})");
                foreach (var d in report.diagnostics.diagnostics)
                {
                    summary.WriteLine($"- **{d.severity}** `{d.type}` `{d.subject}`: {d.observation}. Hypothesis: {d.hypothesis}.");
                    summary.WriteLine($"  - Evidence: frames {d.firstFrame}–{d.peakFrame}, t={d.firstSimulationTime:R}–{d.peakSimulationTime:R}s; metrics={string.Join(" | ", d.metrics ?? Array.Empty<string>())}");
                    summary.WriteLine($"  - Next check: {string.Join("; ", d.recommendedChecks ?? Array.Empty<string>())}");
                    summary.WriteLine($"  - Falsifier: {string.Join("; ", d.falsifiers ?? Array.Empty<string>())}");
                }
            }
            else summary.WriteLine("\n## Diagnostics\nNo configured diagnostic rule fired.");
            if (report.diagnostics != null && report.diagnostics.unavailableReasons != null && report.diagnostics.unavailableReasons.Count > 0)
            {
                summary.WriteLine("\n## Unavailable conclusions");
                foreach (string reason in report.diagnostics.unavailableReasons) summary.WriteLine("- " + reason);
            }
            summary.WriteLine($"\n## Comparison\nBaseline found: {comparison.baselineFound}\nDecision: `{comparison.decision}`; profile: `{comparison.scenarioProfile}`; available: {comparison.profileAvailable}");
            foreach (var metric in comparison.metrics) summary.WriteLine($"- `{metric.name}`: {metric.current:R} {metric.unit}; delta {metric.delta:R}; regression={metric.regression}");
            if (report.balanceComparison != null)
            {
                summary.WriteLine($"\n## Balance A/B\nDecision: `{report.balanceComparison.decision}`; setup matched: {report.balanceComparison.setupMatched}; safety guards passed: {report.balanceComparison.safetyGuardsPassed}");
                if (!string.IsNullOrEmpty(report.balanceComparison.invalidReason)) summary.WriteLine($"Reason: `{report.balanceComparison.invalidReason}`");
                foreach (var guard in report.balanceComparison.safetyGuards) summary.WriteLine($"- safety guard: `{guard}`");
                foreach (var reason in report.balanceComparison.rejectionReasons) summary.WriteLine($"- decision reason: `{reason}`");
                foreach (var metric in report.balanceComparison.stabilityMetrics) summary.WriteLine($"- stability `{metric.name}`: {metric.current:R} {metric.unit}; delta {metric.delta:R}; regression={metric.regression}");
            }
        }

        void ValidateFinite(EvaluationReport report)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                PhysicsFrame frame = frames[i];
                for (int j = 0; j < frame.bodies.Length; j++)
                {
                    BodyTelemetry body = frame.bodies[j];
                    if (!RagdollLabMath.IsFinite(body.position.ToVector3()) ||
                        !RagdollLabMath.IsFinite(body.velocity.ToVector3()) ||
                        !RagdollLabMath.IsFinite(body.angularVelocity.ToVector3()) ||
                        !RagdollLabMath.IsFinite(body.rotation.ToQuaternion()))
                    {
                        report.finiteData = false;
                        report.warnings.Add($"Non-finite body telemetry at frame {frame.frameIndex}: {body.id}");
                    }
                }
                if (frame.joints != null) for (int j = 0; j < frame.joints.Length; j++)
                {
                    JointTelemetry joint = frame.joints[j];
                    if (!RagdollLabMath.IsFinite(joint.anchorError) || !RagdollLabMath.IsFinite(joint.currentForce.ToVector3()) || !RagdollLabMath.IsFinite(joint.currentTorque.ToVector3()))
                    {
                        report.finiteData = false;
                        report.warnings.Add($"Non-finite joint telemetry at frame {frame.frameIndex}: {joint.id}");
                    }
                }
                if (frame.contacts != null) for (int c = 0; c < frame.contacts.Length; c++)
                {
                    ContactTelemetry contact = frame.contacts[c];
                    if (contact == null) continue;
                    bool validDuration = !contact.hasContactDuration ||
                        (RagdollLabMath.IsFinite(contact.contactDurationSeconds) && contact.contactDurationSeconds >= 0f);
                    if (!validDuration || !RagdollLabMath.IsFinite(contact.supportNormalDot))
                    {
                        report.finiteData = false;
                        report.warnings.Add($"Non-finite contact interval telemetry at frame {frame.frameIndex}: {contact.key}");
                    }
                }
                if (frame.feet != null) for (int f = 0; f < frame.feet.Length; f++)
                {
                    FootTelemetry foot = frame.feet[f];
                    if (foot == null || !RagdollLabMath.IsFinite(foot.tangentialSlipSpeed)
                        || !RagdollLabMath.IsFinite(foot.accumulatedSlipDistance)
                        || !RagdollLabMath.IsFinite(foot.contactDuration)
                        || (foot.supportPointValid && !RagdollLabMath.IsFinite(foot.supportPoint.ToVector3())))
                    {
                        report.finiteData = false;
                        report.warnings.Add($"Non-finite foot telemetry at frame {frame.frameIndex}");
                    }
                }
                if (frame.character != null)
                {
                    CharacterTelemetry character = frame.character;
                    if (!character.finite || !RagdollLabMath.IsFinite(character.supportMarginMeters)
                        || !RagdollLabMath.IsFinite(character.supportOrigin.ToVector3())
                        || !RagdollLabMath.IsFinite(character.supportUp.ToVector3())
                        || !RagdollLabMath.IsFinite(character.centerOfMassHeightAboveSupport))
                    {
                        report.finiteData = false;
                    }
                }
                if (frame.balance != null)
                {
                    BalanceFrameTelemetry balance = frame.balance;
                    if ((balance.hasCapturePoint && !RagdollLabMath.IsFinite(balance.capturePoint.ToVector3()))
                        || (balance.hasSignedSupportMargin && !RagdollLabMath.IsFinite(balance.signedSupportMargin))
                        || (balance.supportReferenceAvailable && (!RagdollLabMath.IsFinite(balance.supportOrigin.ToVector3())
                            || !RagdollLabMath.IsFinite(balance.supportUp.ToVector3())))
                        || (balance.hasBalancerTorque && !RagdollLabMath.IsFinite(balance.balancerTorque.ToVector3())))
                    {
                        report.finiteData = false;
                        report.warnings.Add($"Non-finite balance telemetry at frame {frame.frameIndex}");
                    }
                }
                if (frame.stagger != null && frame.stagger.stepCount < 0)
                {
                    report.finiteData = false;
                    report.warnings.Add($"Invalid Stagger step count at frame {frame.frameIndex}");
                }
                if (frame.animatedPairs != null) for (int p = 0; p < frame.animatedPairs.Length; p++)
                {
                    TargetPoseTelemetry pair = frame.animatedPairs[p];
                    bool finite = pair != null
                        && (!pair.targetAvailable || (RagdollLabMath.IsFinite(pair.targetPosition.ToVector3())
                            && RagdollLabMath.IsFinite(pair.targetRotation.ToQuaternion())))
                        && (!pair.physicsAvailable || (RagdollLabMath.IsFinite(pair.physicsPosition.ToVector3())
                            && RagdollLabMath.IsFinite(pair.physicsRotation.ToQuaternion())
                            && RagdollLabMath.IsFinite(pair.physicsLinearVelocity.ToVector3())
                            && RagdollLabMath.IsFinite(pair.physicsAngularVelocity.ToVector3())))
                        && RagdollLabMath.IsFinite(pair.targetPhysicsDistance)
                        && RagdollLabMath.IsFinite(pair.targetPhysicsAngularError)
                        && RagdollLabMath.IsFinite(pair.targetSampleTime)
                        && RagdollLabMath.IsFinite(pair.sampleDeltaTime)
                        && (!pair.targetVelocityAvailable || (RagdollLabMath.IsFinite(pair.targetLinearVelocity.ToVector3())
                            && RagdollLabMath.IsFinite(pair.targetAngularVelocity.ToVector3())))
                        && (!pair.targetAccelerationAvailable || (RagdollLabMath.IsFinite(pair.targetLinearAcceleration.ToVector3())
                            && RagdollLabMath.IsFinite(pair.targetAngularAcceleration.ToVector3())))
                        && (!pair.targetJerkAvailable || (RagdollLabMath.IsFinite(pair.targetLinearJerk.ToVector3())
                            && RagdollLabMath.IsFinite(pair.targetAngularJerk.ToVector3())))
                        && (!pair.physicsVelocityAvailable || (RagdollLabMath.IsFinite(pair.physicsLinearVelocity.ToVector3())
                            && RagdollLabMath.IsFinite(pair.physicsAngularVelocity.ToVector3())))
                        && (!pair.physicsAccelerationAvailable || (RagdollLabMath.IsFinite(pair.physicsLinearAcceleration.ToVector3())
                            && RagdollLabMath.IsFinite(pair.physicsAngularAcceleration.ToVector3())))
                        && (!pair.physicsJerkAvailable || (RagdollLabMath.IsFinite(pair.physicsLinearJerk.ToVector3())
                            && RagdollLabMath.IsFinite(pair.physicsAngularJerk.ToVector3())))
                        && (!pair.authoredMappingAvailable || (RagdollLabMath.IsFinite(pair.authoredMappingPositionWeight)
                            && RagdollLabMath.IsFinite(pair.authoredMappingRotationWeight)))
                        && (!pair.effectiveMappingAvailable || (RagdollLabMath.IsFinite(pair.effectiveMappingPositionWeight)
                            && RagdollLabMath.IsFinite(pair.effectiveMappingRotationWeight)));
                    if (!finite)
                    {
                        report.finiteData = false;
                        report.warnings.Add($"Non-finite animated-pair telemetry at frame {frame.frameIndex}: {pair?.pairId ?? "missing"}");
                    }
                }
            }
        }

        float CalculateMass() { float total = 0f; foreach (var body in bodies) total += body != null ? body.mass : 0f; return total; }
        float CalculateHeight()
        {
            if (!trackedRoot) return 0f;
            Bounds bounds = new(trackedRoot.position, Vector3.zero); foreach (var r in trackedRoot.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds); return bounds.size.y;
        }

        public static string StableId(Transform target, string component)
        {
            if (!target) return "missing";
            var path = new StringBuilder(target.name);
            Transform current = target.parent;
            while (current != null) { path.Insert(0, current.name + "/"); current = current.parent; }
            return component + ":" + path;
        }

        [Serializable] sealed class FrameContainer { public List<PhysicsFrame> frames; }
    }
}
