using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollLabSchema
    {
        public const string Version = "1.1.0";
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
        public int seed;
        public string unityVersion;
        public string physicsScene;
        public float fixedDeltaTime;
        public float gravityMagnitude;
        public float characterHeight;
        public float totalMass;
        public string captureRoot;
        public string startedUtc;
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
        public bool penetration;
        public float penetrationDepth;
    }

    [Serializable] public sealed class TargetPoseTelemetry
    {
        public string id;
        public string bone;
        public string physicsBodyId;
        public Vector3Data targetPosition, physicsPosition, renderedPosition;
        public QuaternionData targetRotation, physicsRotation, renderedRotation;
        public float targetPhysicsDistance;
        public float physicsRenderedDistance;
        public float targetRenderedDistance;
        public float targetPhysicsAngularError;
        public float physicsRenderedAngularError;
        public float targetRenderedAngularError;
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
        public float dominantFrequencyHz;
        public int fallenFrameCount;
        public float recoveryTimeSeconds;
        public string[] topOffenderIds;
        public float contactTransitionsPerSecond;
        public int shortContactCount;
        public JointReport[] joints;
    }

    [Serializable] public sealed class DiagnosticEvidence
    {
        public string type, severity, confidence, subject, scenario, observation, hypothesis;
        public string[] metrics;
        public int firstFrame, peakFrame;
    }

    [Serializable] public sealed class DiagnosticsReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public List<DiagnosticEvidence> diagnostics = new();
    }

    [Serializable] public sealed class ComparisonMetric
    {
        public string name, unit;
        public float current, baseline, delta, relativeDelta;
        public bool regression;
    }

    [Serializable] public sealed class ComparisonReport
    {
        public string schemaVersion = RagdollLabSchema.Version;
        public string currentRunId, baselineRunId;
        public bool baselineFound;
        public List<ComparisonMetric> metrics = new();
        public List<string> regressionGuards = new();
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
    }
}
