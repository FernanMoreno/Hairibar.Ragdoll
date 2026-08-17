using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollLabSchema
    {
        public const string Version = "1.7.0";
    }

    [Serializable] public struct Vector3Data
    {
        public float x, y, z;
        public Vector3Data(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new(x, y, z);
    }

    [Serializable] public struct QuaternionData
    {
        public float x, y, z, w;
        public QuaternionData(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
        public Quaternion ToQuaternion() => new(x, y, z, w);
    }

    [Serializable] public sealed class RagdollLabMetadata
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public string runId;
        public string scenario = "Unspecified";
        public string scenarioProfile = RagdollLabScenarioProfiles.UnavailableId;
        public int seed;
        public string unityVersion;
        public string physicsScene;
        public float fixedDeltaTime;
        public float gravityMagnitude;
        public float characterHeight;
        public float totalMass;
        public string captureRoot;
        public string startedUtc;
        public string variant = "unspecified";
        public bool balancerEnabled;
        public string initialConditionFingerprint;
        public string pushDescriptor;
        public string tuningSessionId;
        public string experimentId;
        public string runRole = "none";
        public string configurationFingerprint;
        public string baselineConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
        public string scenarioContractId;
        public string scenarioContractVersion;
    }

    [Serializable] public sealed class RagdollTuningRunBinding
    {
        public string sessionId;
        public string experimentId;
        public string runId;
        public string runRole = "none";
        public string artifactDirectory;
        public string configurationFingerprint;
        public string baselineConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
    }

    public static class RagdollTuningArtifactSchema
    {
        public const string Version = "1.1.0";
        public const string NormativeDecisionVersion = "1.0.0";
        public const string SessionVersion = "1.0.0";
        public const string SessionFileName = "tuning-session.json";
        public const string ManifestFileName = "tuning-manifest.json";
        public const string EvaluationFileName = "evaluation.json";
        public const string ScenarioComparisonFileName = "scenario-comparison.json";
        public const string BalanceComparisonFileName = "balance-comparison.json";
        public const string ComparisonFileName = "comparison.json";
    }

    [Serializable] public sealed class RagdollTuningArtifactManifest
    {
        public string schemaVersion = RagdollTuningArtifactSchema.Version;
        public string sessionId;
        public string experimentId;
        public string runId;
        public string runRole = "none";
        public string configurationFingerprint;
        public string baselineConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
        public string evaluationFile = RagdollTuningArtifactSchema.EvaluationFileName;
        public string evaluationSha256;
        public string normativeDecisionFile = RagdollTuningArtifactSchema.ScenarioComparisonFileName;
        public string normativeDecisionSha256;
        public string normativeDecisionSchemaVersion = RagdollTuningArtifactSchema.NormativeDecisionVersion;
        public string balanceComparisonFile = RagdollTuningArtifactSchema.BalanceComparisonFileName;
        public string balanceComparisonSha256;
        public string comparisonFile = RagdollTuningArtifactSchema.ComparisonFileName;
        public string comparisonSha256;
        public string scenarioContractId;
        public string scenarioContractVersion;
        public string specializedComparisonKind = "balance";
        public bool specializedComparisonAvailable = true;
        public string publishedUtc;
    }

    [Serializable] public sealed class BodyTelemetry
    {
        public string id;
        public string name;
        public Vector3Data position;
        public QuaternionData rotation;
        public Vector3Data localPosition;
        public QuaternionData localRotation;
        public Vector3Data velocity;
        public Vector3Data angularVelocity;
        public Vector3Data worldCenterOfMass;
        public Vector3Data inertiaTensor;
        public QuaternionData inertiaTensorRotation;
        public float mass;
        public bool isSleeping;
        public bool isKinematic;
    }

    [Serializable] public sealed class JointTelemetry
    {
        public string id;
        public string name;
        public string bodyId;
        public string connectedBodyId;
        public Vector3Data worldAnchor;
        public Vector3Data connectedWorldAnchor;
        public float anchorError;
        public Vector3Data currentForce;
        public Vector3Data currentTorque;
        public bool hasCurrentForce;
        public bool hasCurrentTorque;
        public float relativeAngularSpeed;
        public float distanceToNearestLimit;
    }

    [Serializable] public sealed class ContactTelemetry
    {
        public string key;
        public string bodyA, bodyB;
        public string colliderA, colliderB;
        public Vector3Data point, normal;
        public Vector3Data relativeVelocity, impulse;
        public float impulseMagnitude;
        public bool contactStart, contactStay, contactEnd;
        public bool hasContactStartTime, hasContactEndTime, hasContactDuration;
        public float contactStartTime, contactEndTime, contactDurationSeconds;
        public bool groundSupport;
        public float supportNormalDot;
        public Vector3Data supportVelocity;
        public bool supportRelativeMotionAvailable;
        public bool penetration;
        public float penetrationDepth;
    }

    [Serializable] public sealed class TargetPoseTelemetry
    {
        public string id;
        public string pairId;
        public string bone;
        public string targetTransformId;
        public string physicsBodyId;
        public bool sourceAvailable;
        public bool targetAvailable;
        public bool physicsAvailable;
        public Vector3Data targetPosition, physicsPosition, renderedPosition;
        public QuaternionData targetRotation, physicsRotation, renderedRotation;
        public float targetPhysicsDistance;
        public float physicsRenderedDistance;
        public float targetRenderedDistance;
        public float targetPhysicsAngularError;
        public float physicsRenderedAngularError;
        public float targetRenderedAngularError;
        public Vector3Data targetLinearVelocity, targetAngularVelocity;
        public Vector3Data targetLinearAcceleration, targetAngularAcceleration;
        public Vector3Data targetLinearJerk, targetAngularJerk;
        public Vector3Data physicsLinearVelocity, physicsAngularVelocity;
        public Vector3Data physicsLinearAcceleration, physicsAngularAcceleration;
        public Vector3Data physicsLinearJerk, physicsAngularJerk;
        public float targetSampleTime;
        public float sampleDeltaTime;
        public bool targetKinematicsAvailable;
        public bool targetVelocityAvailable;
        public bool targetAccelerationAvailable;
        public bool targetJerkAvailable;
        public bool targetKinematicsReset;
        public bool physicsKinematicsAvailable;
        public bool physicsVelocityAvailable;
        public bool physicsAccelerationAvailable;
        public bool physicsJerkAvailable;
        public bool physicsKinematicsReset;
        public bool authoredMappingAvailable;
        public float authoredMappingPositionWeight;
        public float authoredMappingRotationWeight;
        public bool effectiveMappingAvailable;
        public float effectiveMappingPositionWeight;
        public float effectiveMappingRotationWeight;
    }

    [Serializable] public sealed class BalanceFrameTelemetry
    {
        public bool sourceAvailable;
        public string activeBehaviour = "Unavailable";
        public string state = "Unavailable";
        public bool hasCapturePoint;
        public Vector3Data capturePoint;
        public bool hasSignedSupportMargin;
        public float signedSupportMargin;
        public Vector3Data supportOrigin, supportUp;
        public bool supportReferenceAvailable;
        public bool effectiveUpAvailable;
        public Vector3Data effectiveUp;
        public bool relativeSupportMotionAvailable;
        public Vector3Data supportVelocity;
        public Vector3Data relativeCenterOfMassVelocity;
        public int supportColliderId;
        public int supportRigidbodyId;
        public bool transitionObserved;
        public string transitionFrom = "Unavailable";
        public string transitionTo = "Unavailable";
        public bool hasBalancerTorque;
        public Vector3Data balancerTorque;
    }

    [Serializable] public sealed class StaggerFrameTelemetry
    {
        public bool sourceAvailable;
        public string episodeId;
        public string phase = "Unavailable";
        public string swingFoot = "Unavailable";
        public bool swingFootAvailable;
        public int stepCount;
        public bool selectedFootGroundSupport;
        public bool liftOffObserved;
        public bool replantObserved;
    }

    [Serializable] public sealed class StaggerEpisodeReport
    {
        public string episodeId;
        public int firstFrame, lastFrame;
        public float firstSimulationTime, lastSimulationTime;
        public string initialBalanceState = "Unavailable";
        public string terminalBalanceState = "Unavailable";
        public string terminalOutcome = "Unavailable";
        public string swingFoot = "Unavailable";
        public int stepCount;
        public int liftOffFrame = -1, replantFrame = -1;
        public float replantContactDuration;
        public float minimumSignedSupportMargin;
        public float finalSignedSupportMargin;
        public string finalPuppetState = "Unavailable";
        public bool unpinnedObserved;
        public string invalidReason;
        public string[] phaseSamples = Array.Empty<string>();
    }

    [Serializable] public sealed class CharacterTelemetry
    {
        public Vector3Data centerOfMass, centerOfMassVelocity, centerOfMassAcceleration;
        public float kineticEnergy, potentialEnergy;
        public float totalMass;
        public bool finite = true;
        public int supportContactCount;
        public bool likelyFallen;
        public int supportPointCount;
        public bool centerOfMassInsideSupport;
        public float supportMarginMeters;
        public Vector3Data supportOrigin, supportUp;
        public bool supportReferenceAvailable;
        public float centerOfMassHeightAboveSupport;
        public string puppetState;
        public string simulationMode;
        public float masterMappingWeight;
        public float masterPinWeight;
        public float masterMuscleWeight;
        public float masterMuscleDamper;
        public int knockOutBoneIndex = -1;
        public float knockOutDistance;
        public float knockOutThreshold;
        public float knockOutEffectivePinWeight;
    }

    [Serializable] public sealed class FootTelemetry
    {
        public string id, name;
        public bool stance;
        public float tangentialSlipSpeed;
        public float accumulatedSlipDistance;
        public float contactDuration;
        public Vector3Data supportPoint;
        public bool supportPointValid;
    }

    [Serializable] public sealed class EventMarker
    {
        public string name;
        public float simulationTime;
        public long physicsStepIndex;
        public int frameIndex;
    }

    [Serializable] public sealed class PhysicsFrame
    {
        public int frameIndex;
        public long physicsStepIndex;
        public float simulationTime;
        public float fixedDeltaTime;
        public BodyTelemetry[] bodies;
        public JointTelemetry[] joints;
        public ContactTelemetry[] contacts;
        public TargetPoseTelemetry[] targetPoses;
        public CharacterTelemetry character;
        public EventMarker[] events;
        public FootTelemetry[] feet;
        public BalanceFrameTelemetry balance;
        public StaggerFrameTelemetry stagger;
        public bool animatedPairCaptureAttempted;
        public bool animatedPairSourceAvailable;
        public int animatedPairCount;
        public TargetPoseTelemetry[] animatedPairs = Array.Empty<TargetPoseTelemetry>();
        public string[] mappingIntegrityWarnings = Array.Empty<string>();
    }

    [Serializable] public sealed class MetricSummary
    {
        public string name, unit, source, interpretation;
        public float current, mean, rms, p95, max, normalizedMean;
        public int count;
    }

    [Serializable] public sealed class AnchorDriftEventReport
    {
        public string eventName;
        public int eventFrameIndex;
        public float eventSimulationTime;
        public float baseline, peak, peakOffsetSeconds;
        public float sample50ms, sample100ms, sample250ms, sample500ms, sample1000ms;
        public float settlingTimeSeconds, aucError, timeAboveThresholdSeconds;
    }

    [Serializable] public sealed class JointReport
    {
        public string id, name;
        public MetricSummary anchorError, force, torque, angularTrackingError;
        public int oscillationZeroCrossings;
        public float dominantFrequencyHz, overshootPercent, settlingTimeSeconds;
        public AnchorDriftEventReport[] anchorErrorEvents = Array.Empty<AnchorDriftEventReport>();
    }

    [Serializable] public sealed class ScenarioReport
    {
        public string name;
        public int frameCount;
        public float durationSeconds;
        public MetricSummary kineticEnergy, centerOfMassSpeed, contactImpulse, penetration, footSlipSpeed;
        public MetricSummary balancerTorque;
        public float dominantFrequencyHz;
        public int fallenFrameCount;
        public float recoveryTimeSeconds;
        public string[] topOffenderIds;
        public float contactTransitionsPerSecond;
        public int shortContactCount;
        public StaggerEpisodeReport[] staggerEpisodes = Array.Empty<StaggerEpisodeReport>();
        public bool balanceTelemetryAvailable;
        public bool signedSupportMarginAvailable;
        public float minimumSignedSupportMargin;
        public float finalSignedSupportMargin;
        public int balanceSampleCount;
        public int requiresStepFrameCount;
        public int unrecoverableFrameCount;
        public int balancerAppliedFrameCount;
        public int recoveredStaggerEpisodeCount;
        public int failedStaggerEpisodeCount;
        public int unpinnedStaggerEpisodeCount;
        public int capturePointSampleCount;
        public int capturePointNonFiniteSampleCount;
        public int supportSampleCount;
        public int supportLossFrameCount;
        public float maximumSignedSupportMargin;
        public float recoveryOvershootMeters;
        public float firstRequiresStepSimulationTime = -1f;
        public int firstRequiresStepFrame = -1;
        public bool perturbationEventAvailable;
        public string firstPerturbationEventName;
        public int firstPerturbationFrame = -1;
        public float firstPerturbationSimulationTime = -1f;
        public bool requiresStepLatencyAvailable;
        public float requiresStepLatencySeconds = -1f;
        public bool recoveryCompletionAvailable;
        public bool recoveryCompleted;
        public bool taskCompletionAvailable;
        public bool taskCompleted;
        public bool propLifecycleCompletionAvailable;
        public bool propLifecycleCompleted;
        public JointReport[] joints;
        public bool animatedPairSourceAvailable;
        public int animatedPairCount;
        public int animatedPairSampleCount;
        public PairTrackingReport[] pairTracking = Array.Empty<PairTrackingReport>();
        public string[] mappingIntegrityWarnings = Array.Empty<string>();
    }

    [Serializable] public sealed class PairTrackingReport
    {
        public string id;
        public string bone;
        public string targetTransformId;
        public string physicsBodyId;
        public bool sourceAvailable;
        public bool targetAvailable;
        public bool physicsAvailable;
        public int sampleCount;
        public MetricSummary targetPhysicsDistance;
        public MetricSummary targetPhysicsAngularError;
        public MetricSummary targetPhysicsVelocityError;
        public MetricSummary targetLinearSpeed;
        public MetricSummary targetAngularSpeed;
        public MetricSummary physicsLinearSpeed;
        public MetricSummary physicsAngularSpeed;
        public MetricSummary targetLinearAcceleration;
        public MetricSummary targetAngularAcceleration;
        public MetricSummary targetLinearJerk;
        public MetricSummary targetAngularJerk;
        public MetricSummary physicsLinearAcceleration;
        public MetricSummary physicsAngularAcceleration;
        public MetricSummary physicsLinearJerk;
        public MetricSummary physicsAngularJerk;
        public bool authoredMappingAvailable;
        public float authoredMappingPositionWeight;
        public float authoredMappingRotationWeight;
        public bool effectiveMappingAvailable;
        public float effectiveMappingPositionWeight;
        public float effectiveMappingRotationWeight;
        public string[] mappingIntegrityWarnings = Array.Empty<string>();
    }

    [Serializable] public sealed class DiagnosticEvidence
    {
        public string type, severity, confidence, subject, scenario, observation, hypothesis;
        public string[] metrics;
        public int firstFrame, peakFrame;
        public string availability = "available";
        public float firstSimulationTime = -1f;
        public float peakSimulationTime = -1f;
        public string[] recommendedChecks = Array.Empty<string>();
        public string[] falsifiers = Array.Empty<string>();
    }

    [Serializable] public sealed class DiagnosticsReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public string scenarioProfile = RagdollLabScenarioProfiles.UnavailableId;
        public bool profileAvailable;
        public List<string> unavailableReasons = new();
        public List<DiagnosticEvidence> diagnostics = new();
    }

    [Serializable] public sealed class ComparisonMetric
    {
        public string name, unit;
        public string expectation = "neutral";
        public float current, baseline, delta, relativeDelta;
        public bool regression;
    }

    [Serializable] public sealed class ScenarioMetric
    {
        public string name, unit;
        public string expectation = "neutral";
        public float current, baseline, delta, relativeDelta;
        public bool regression;
    }

    [Serializable] public sealed class RequiredSignalStatus
    {
        public string signalId;
        public string role;
        public bool available;
        public bool finite;
        public string reason;
    }

    [Serializable] public sealed class SafetyGateResult
    {
        public string id;
        public bool passed;
        public string reason;
        public float observed;
        public float baseline;
    }

    [Serializable] public sealed class ScenarioEvaluationReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public string scenarioProfile = RagdollLabScenarioProfiles.UnavailableId;
        public string contractId;
        public string contractVersion;
        public string decision = "unavailable";
        public string taskDecision = "unavailable";
        public string safetyDecision = "unavailable";
        public string invalidReason;
        public bool available;
        public bool setupMatched;
        public bool provenanceAvailable;
        public bool safetyGuardsPassed;
        public bool balanceFallbackUsed;
        public string tuningSessionId;
        public string experimentId;
        public string baselineRunId;
        public string candidateRunId;
        public string baselineConfigurationFingerprint;
        public string candidateConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
        public List<ScenarioMetric> taskMetrics = new();
        public List<ScenarioMetric> safetyMetrics = new();
        public List<SafetyGateResult> safetyGates = new();
        public List<RequiredSignalStatus> requiredSignalStatuses = new();
        public List<string> rejectionReasons = new();
    }

    [Serializable] public sealed class ComparisonReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public string currentRunId, baselineRunId;
        public string tuningSessionId, experimentId;
        public string configurationFingerprint, baselineConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
        public bool baselineFound;
        public List<ComparisonMetric> metrics = new();
        public List<string> regressionGuards = new();
        public string decision = "unavailable";
        public bool safetyGuardsPassed;
        public string invalidReason;
        public string scenarioProfile = RagdollLabScenarioProfiles.UnavailableId;
        public bool profileAvailable;
        public List<string> rejectionReasons = new();
        public string decisionAuthority;
        public string normativeDecision;
        public string normativeDecisionFile;
        public string normativeDecisionSchemaVersion;
    }

    [Serializable] public sealed class BalanceComparisonReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public string decision = "invalid";
        public string invalidReason;
        public string tuningSessionId;
        public string experimentId;
        public string baselineRunId, candidateRunId;
        public string baselineConfigurationFingerprint, candidateConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
        public bool provenanceAvailable;
        public string scenarioProfile = RagdollLabScenarioProfiles.UnavailableId;
        public bool profileAvailable;
        public bool setupMatched;
        public bool safetyGuardsPassed;
        public List<ComparisonMetric> stabilityMetrics = new();
        public List<ComparisonMetric> safetyMetrics = new();
        public List<string> safetyGuards = new();
        public List<string> rejectionReasons = new();
        public string decisionAuthority;
        public string viewKind = "normative";
        public string normativeDecision;
        public string normativeDecisionFile;
        public string normativeDecisionSchemaVersion;
    }

    /// <summary>
    /// The versioned, scenario-level decision envelope. Its nested payload is
    /// currently the balance comparator, but the envelope is the stable
    /// authority boundary for future GetUp, locomotion and prop comparators.
    /// </summary>
    [Serializable] public sealed class ScenarioComparisonReport
    {
        public string schemaVersion = RagdollTuningArtifactSchema.NormativeDecisionVersion;
        public string decisionAuthority = RagdollTuningArtifactSchema.ScenarioComparisonFileName;
        public string comparisonKind = "balance";
        public string contractId;
        public string contractVersion;
        public string scenarioProfile = RagdollLabScenarioProfiles.UnavailableId;
        public string decision = "invalid";
        public string invalidReason;
        public bool profileAvailable;
        public bool setupMatched;
        public bool safetyGuardsPassed;
        public string tuningSessionId;
        public string experimentId;
        public string baselineRunId;
        public string candidateRunId;
        public string baselineConfigurationFingerprint;
        public string candidateConfigurationFingerprint;
        public string treatmentParameter;
        public bool treatmentValueAvailable;
        public float treatmentValue;
        public List<string> rejectionReasons = new();
        public ScenarioEvaluationReport scenarioEvaluation;
        public BalanceComparisonReport balanceComparison;
    }

    [Serializable] public sealed class EvaluationReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public RagdollLabMetadata metadata;
        public int frameCount;
        public int bodyCount;
        public int jointCount;
        public bool completed;
        public bool finiteData = true;
        public string failure;
        public List<string> warnings = new();
        public ScenarioReport scenarioReport;
        public DiagnosticsReport diagnostics;
        public ScenarioComparisonReport scenarioComparison;
        public BalanceComparisonReport balanceComparison;
    }
}
