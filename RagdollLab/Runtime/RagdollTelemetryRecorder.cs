using System;
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
        [SerializeField] string outputDirectory = "RagdollLab/latest";
        [SerializeField] RagdollLabThresholds thresholds;
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
        readonly List<EventMarker> eventMarkers = new();
        readonly Dictionary<string, float> footSlipDistance = new();
        readonly Dictionary<string, float> footContactDuration = new();
        readonly Collider[] penetrationBuffer = new Collider[128];
        string runId;
        bool capturing;
        long physicsStep;
        int frameIndex;
        Component puppetStateSource;
        Component simulationModeSource;
        Component animatorStateSource;
        PropertyInfo puppetStateProperty;
        PropertyInfo knockOutBoneProperty;
        PropertyInfo knockOutDistanceProperty;
        PropertyInfo knockOutThresholdProperty;
        PropertyInfo knockOutPinProperty;
        PropertyInfo simulationModeProperty;
        PropertyInfo mappingWeightProperty;
        PropertyInfo pinWeightProperty;
        PropertyInfo muscleWeightProperty;
        PropertyInfo muscleDamperProperty;

        public IReadOnlyList<PhysicsFrame> Frames => frames;
        public bool IsCapturing => capturing;
        public int MaxFrames => maxFrames;
        public string OutputPath => Application.isEditor || Application.isBatchMode
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputDirectory)
            : Path.Combine(Application.persistentDataPath, outputDirectory);
        public string ScenarioName { get => scenario; set => scenario = value; }
        public string OutputDirectory { get => outputDirectory; set => outputDirectory = string.IsNullOrWhiteSpace(value) ? "Artifacts/RagdollLab/latest" : value; }
        public int Seed { get => seed; set => seed = value; }
        public bool CaptureOnStart { get => captureOnStart; set => captureOnStart = value; }
        public void SetMaxFrames(int value) => maxFrames = Mathf.Max(1, value);
        public void ConfigureTracking(Transform root) { trackedRoot = root; CachePhysics(); }
        public void ConfigurePoseMapping(Animator animator, Transform renderRoot)
        { targetAnimator = animator; renderedRoot = renderRoot; }

        void Awake()
        {
            trackedRoot ??= transform;
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
            frameIndex = 0;
            physicsStep = 0;
            runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
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
            return candidate.transform == trackedRoot || candidate.transform.IsChildOf(trackedRoot);
        }

        PhysicsFrame CaptureFrame(float simulationTime)
        {
            var bodyData = new BodyTelemetry[bodies.Count];
            for (int i = 0; i < bodies.Count; i++) bodyData[i] = CaptureBody(bodies[i]);
            var jointData = new JointTelemetry[joints.Count];
            for (int i = 0; i < joints.Count; i++) jointData[i] = CaptureJoint(joints[i]);
            ContactTelemetry[] contactData = pendingContacts.ToArray();
            pendingContacts.Clear();
            FootTelemetry[] feet = CaptureFeet();
            CharacterTelemetry character = CaptureCharacter(feet);
            TargetPoseTelemetry[] poses = CaptureTargetPoses();
            EventMarker[] events = eventMarkers.ToArray();
            eventMarkers.Clear();
            return new PhysicsFrame { frameIndex = frameIndex, physicsStepIndex = physicsStep,
                simulationTime = simulationTime, fixedDeltaTime = Time.fixedDeltaTime,
                bodies = bodyData, joints = jointData, contacts = contactData, character = character, targetPoses = poses, events = events, feet = feet };
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
            foreach (string key in activeContacts) if (key.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0) support++;
            var supportPoints = new List<Vector3>();
            if (feet != null) for (int i = 0; i < feet.Length; i++) if (feet[i].stance)
                for (int b = 0; b < bodies.Count; b++) if (bodies[b] && StableId(bodies[b].transform, "Foot") == feet[i].id) supportPoints.Add(bodies[b].worldCenterOfMass);
            bool inside = RagdollSupportGeometry.Contains(com, supportPoints, out float margin);
            var telemetry = new CharacterTelemetry { centerOfMass = new(com), centerOfMassVelocity = new(comVelocity),
                totalMass = mass, kineticEnergy = energy, potentialEnergy = CalculatePotentialEnergy(), finite = RagdollLabMath.IsFinite(com),
                supportContactCount = support, likelyFallen = com.y < 0.35f, supportPointCount = supportPoints.Count,
                centerOfMassInsideSupport = inside, supportMarginMeters = margin };
            CaptureRuntimeAuthority(telemetry);
            return telemetry;
        }

        FootTelemetry[] CaptureFeet()
        {
            var result = new List<FootTelemetry>();
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody body = bodies[i]; if (!body || body.name.IndexOf("foot", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Collider[] colliders = body.GetComponents<Collider>(); bool stance = false;
                for (int c = 0; c < colliders.Length && !stance; c++)
                {
                    string colliderId = StableId(colliders[c].transform, "Collider");
                    foreach (string key in activeContacts) if (key.IndexOf(colliderId, StringComparison.Ordinal) >= 0) { stance = true; break; }
                }
                string id = StableId(body.transform, "Foot"); float dt = Time.fixedDeltaTime;
                float slipSpeed = stance ? new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude : 0f;
                if (!footSlipDistance.ContainsKey(id)) footSlipDistance[id] = 0f;
                if (!footContactDuration.ContainsKey(id)) footContactDuration[id] = 0f;
                if (stance) { footSlipDistance[id] += slipSpeed * dt; footContactDuration[id] += dt; }
                result.Add(new FootTelemetry { id = id, name = body.name, stance = stance, tangentialSlipSpeed = slipSpeed,
                    accumulatedSlipDistance = footSlipDistance[id], contactDuration = footContactDuration[id] });
            }
            return result.ToArray();
        }

        float CalculatePotentialEnergy()
        {
            float result = 0f;
            for (int i = 0; i < bodies.Count; i++) if (bodies[i]) result += bodies[i].mass * -Physics.gravity.y * bodies[i].worldCenterOfMass.y;
            return result;
        }

        internal void RecordCollision(Collision collision, bool start, bool stay, bool end)
        {
            if (!capturing || collision == null || collision.contactCount == 0) return;
            ContactPoint first = collision.GetContact(0);
            Collider own = first.thisCollider, other = first.otherCollider != null ? first.otherCollider : collision.collider;
            string a = own != null ? StableId(own.transform, "Collider") : "missing";
            string b = other != null ? StableId(other.transform, "Collider") : "missing";
            string key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
            ContactPoint point = first; Vector3 impulse = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++) impulse += collision.GetContact(i).impulse;
            if (start) activeContacts.Add(key); if (end) activeContacts.Remove(key);
            float penetrationDepth = 0f;
            for (int i = 0; i < collision.contactCount; i++) penetrationDepth = Mathf.Max(penetrationDepth, Mathf.Max(0f, -collision.GetContact(i).separation));
            pendingContacts.Add(new ContactTelemetry { key = key,
                bodyA = own != null && own.attachedRigidbody ? StableId(own.attachedRigidbody.transform, "Rigidbody") : "world",
                bodyB = other != null && other.attachedRigidbody ? StableId(other.attachedRigidbody.transform, "Rigidbody") : "world",
                colliderA = a, colliderB = b, point = new(point.point), normal = new(point.normal),
                relativeVelocity = new(collision.relativeVelocity), impulse = new(impulse), impulseMagnitude = impulse.magnitude,
                contactStart = start, contactStay = stay, contactEnd = end, penetration = penetrationDepth > 0f, penetrationDepth = penetrationDepth });
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
            puppetStateSource = null; simulationModeSource = null; animatorStateSource = null;
            MonoBehaviour[] components = trackedRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i]; if (!component) continue;
                string typeName = component.GetType().FullName;
                if (typeName == "Hairibar.Ragdoll.Animation.RagdollPuppetBehaviour") puppetStateSource = component;
                else if (typeName == "Hairibar.Ragdoll.Animation.RagdollSimulationModeController") simulationModeSource = component;
                else if (typeName == "Hairibar.Ragdoll.Animation.RagdollAnimator") animatorStateSource = component;
            }
            puppetStateProperty = puppetStateSource?.GetType().GetProperty("State");
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

        static string ReadName(Component source, PropertyInfo property)
        {
            if (!source || property == null) return "Unavailable";
            object value = property.GetValue(source);
            return value != null ? value.ToString() : "Unavailable";
        }

        static float ReadFloat(Component source, PropertyInfo property)
        {
            if (!source || property == null) return 0f;
            object value = property.GetValue(source);
            return value is float number && RagdollLabMath.IsFinite(number) ? number : 0f;
        }

        static int ReadHandleIndex(Component source, PropertyInfo property)
        {
            if (!source || property == null) return -1;
            object handle = property.GetValue(source);
            PropertyInfo index = handle?.GetType().GetProperty("Index");
            return index != null && index.GetValue(handle) is int value ? value : -1;
        }

        void WriteArtifacts()
        {
            string directory = OutputPath;
            Directory.CreateDirectory(directory);
            EvaluationReport previous = RagdollLabComparison.Read(Path.Combine(directory, "evaluation.json"));
            var metadata = new RagdollLabMetadata { runId = runId, scenario = scenario, seed = seed,
                unityVersion = Application.unityVersion, physicsScene = gameObject.scene.name,
                fixedDeltaTime = Time.fixedDeltaTime, gravityMagnitude = Physics.gravity.magnitude,
                characterHeight = CalculateHeight(), totalMass = CalculateMass(), captureRoot = trackedRoot.name,
                startedUtc = DateTime.UtcNow.ToString("O") };
            var report = new EvaluationReport { metadata = metadata, frameCount = frames.Count,
                bodyCount = bodies.Count, jointCount = joints.Count, completed = frames.Count > 0 };
            report.scenarioReport = RagdollLabAnalyzer.Analyze(frames, metadata.characterHeight, metadata.totalMass, metadata.gravityMagnitude, thresholds);
            report.scenarioReport.name = scenario;
            report.diagnostics = RagdollLabAnalyzer.Diagnose(report.scenarioReport, thresholds);
            ValidateFinite(report);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "evaluation.json"), json, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "frames.json"), JsonUtility.ToJson(new FrameContainer { frames = frames }, false), Encoding.UTF8);
            using var csv = new StreamWriter(Path.Combine(directory, "frames.csv"), false, Encoding.UTF8);
            csv.WriteLine("frameIndex,physicsStepIndex,simulationTime,fixedDeltaTime,bodyCount,jointCount");
            for (int i = 0; i < frames.Count; i++) { PhysicsFrame f = frames[i]; csv.WriteLine($"{f.frameIndex},{f.physicsStepIndex},{f.simulationTime:R},{f.fixedDeltaTime:R},{f.bodies.Length},{f.joints.Length}"); }
            ComparisonReport comparison = RagdollLabComparison.Build(report, previous);
            File.WriteAllText(Path.Combine(directory, "comparison.json"), JsonUtility.ToJson(comparison, true), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "diagnostics.json"), JsonUtility.ToJson(report.diagnostics, true), Encoding.UTF8);
            WriteSummary(directory, report, comparison);
        }

        void WriteSummary(string directory, EvaluationReport report, ComparisonReport comparison)
        {
            using var summary = new StreamWriter(Path.Combine(directory, "summary.md"), false, Encoding.UTF8);
            summary.WriteLine($"# RagdollLab — {scenario}");
            summary.WriteLine($"\nFrames: {report.frameCount}\nBodies: {report.bodyCount}\nJoints: {report.jointCount}\nFinite data: {report.finiteData}");
            if (report.scenarioReport != null)
            {
                summary.WriteLine($"\nKinetic energy p95: {report.scenarioReport.kineticEnergy.p95:R} J");
                summary.WriteLine($"Foot slip mean: {report.scenarioReport.footSlipSpeed.mean:R} m/s");
                summary.WriteLine($"Dominant frequency: {report.scenarioReport.dominantFrequencyHz:R} Hz");
                summary.WriteLine($"Fallen frames: {report.scenarioReport.fallenFrameCount}; recovery: {report.scenarioReport.recoveryTimeSeconds:R} s");
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
                summary.WriteLine("\n## Diagnostics");
                foreach (var d in report.diagnostics.diagnostics) summary.WriteLine($"- **{d.severity}** `{d.type}` `{d.subject}`: {d.observation}. Hypothesis: {d.hypothesis}.");
            }
            else summary.WriteLine("\n## Diagnostics\nNo configured diagnostic rule fired.");
            summary.WriteLine($"\n## Comparison\nBaseline found: {comparison.baselineFound}");
            foreach (var metric in comparison.metrics) summary.WriteLine($"- `{metric.name}`: {metric.current:R} {metric.unit}; delta {metric.delta:R}; regression={metric.regression}");
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
                if (frame.character != null && !frame.character.finite) report.finiteData = false;
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
