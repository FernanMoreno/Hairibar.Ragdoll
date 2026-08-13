using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollLabAnalyzer
    {
        const float AnchorEventHorizonSeconds = 1.5f;
        const int AnchorEventBaselineLookbackFrames = 5;
        // Hairibar-owned diagnostic thresholds (not part of RagdollLabThresholds):
        // an anchor-drift event is "persistent" rather than a transient impact
        // excursion when it still hasn't settled, or is still above the warning
        // threshold, half a second after the event -- either signal alone is
        // enough, since a spike that settles fast but keeps re-crossing the
        // threshold is just as real a problem as one that never comes down.
        const float PersistentAnchorSettlingSeconds = 0.5f;
        const float PersistentAnchorTimeAboveThresholdSeconds = 0.5f;

        public static ScenarioReport Analyze(IReadOnlyList<PhysicsFrame> frames, float characterHeight, float totalMass, float gravity, RagdollLabThresholds thresholds = null)
        {
            var report = new ScenarioReport { name = "Captured", frameCount = frames?.Count ?? 0 };
            if (frames == null || frames.Count == 0) return report;
            report.durationSeconds = (frames.Count - 1) * frames[0].fixedDeltaTime;
            var energy = new List<float>(); var comSpeed = new List<float>(); var impulses = new List<float>(); var penetration = new List<float>();
            int fallenFrames = 0; int firstFall = -1, recovered = -1;
            int contactTransitions = 0, shortContacts = 0;
            var footSlip = new List<float>();
            for (int i = 0; i < frames.Count; i++)
            {
                PhysicsFrame frame = frames[i];
                energy.Add(frame.character != null ? frame.character.kineticEnergy : 0f);
                comSpeed.Add(frame.character != null ? frame.character.centerOfMassVelocity.ToVector3().magnitude : 0f);
                bool fallen = frame.character != null && (frame.character.likelyFallen || RagdollLabMath.IsLikelyFallen(frame.character.centerOfMass.ToVector3(), Quaternion.identity, frame.character.supportContactCount));
                if (fallen) { fallenFrames++; if (firstFall < 0) firstFall = i; }
                else if (firstFall >= 0 && recovered < 0) recovered = i;
                if (frame.feet != null) for (int f = 0; f < frame.feet.Length; f++) footSlip.Add(frame.feet[f].tangentialSlipSpeed);
                if (frame.contacts != null) for (int j = 0; j < frame.contacts.Length; j++)
                {
                    ContactTelemetry contact = frame.contacts[j];
                    impulses.Add(contact.impulseMagnitude); penetration.Add(contact.penetrationDepth);
                    if (contact.contactStart || contact.contactEnd) contactTransitions++;
                    if (contact.contactEnd && frame.simulationTime < 0.1f) shortContacts++;
                }
            }
            report.kineticEnergy = Summary("KineticEnergy", "J", energy, 1f, "RigidBody velocities + principal-axis inertia", "system motion energy");
            report.centerOfMassSpeed = Summary("CenterOfMassSpeed", "m/s", comSpeed, 1f, "mass-weighted body COM", "global balance motion");
            report.contactImpulse = Summary("ContactImpulse", "N*s", impulses, 1f, "Collision ContactPoint.impulse", "impact/contact load");
            report.penetration = Summary("PenetrationDepth", "m", penetration, Mathf.Max(characterHeight, 0.001f), "collision penetration telemetry", "constraint/collision integrity");
            report.footSlipSpeed = Summary("FootSlipSpeed", "m/s", footSlip, 1f, "foot Rigidbody horizontal velocity while stance", "foot sliding");
            report.dominantFrequencyHz = RagdollLabMath.DominantFrequencyDft(comSpeed, 1f / Mathf.Max(frames[0].fixedDeltaTime, 0.0001f));
            report.fallenFrameCount = fallenFrames;
            report.recoveryTimeSeconds = firstFall >= 0 && recovered >= 0 ? (recovered - firstFall) * frames[0].fixedDeltaTime : 0f;
            report.contactTransitionsPerSecond = contactTransitions / Mathf.Max(report.durationSeconds, frames[0].fixedDeltaTime);
            report.shortContactCount = shortContacts;

            var eventFrames = new List<(string name, int frameIndex, float simulationTime)>();
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].events == null) continue;
                for (int e = 0; e < frames[i].events.Length; e++)
                    eventFrames.Add((frames[i].events[e].name, i, frames[i].events[e].simulationTime));
            }

            int jointCount = frames[0].joints?.Length ?? 0;
            var joints = new JointReport[jointCount];
            for (int j = 0; j < jointCount; j++)
            {
                JointTelemetry first = frames[0].joints[j];
                var anchors = new List<float>(); var forces = new List<float>(); var torques = new List<float>(); var tracking = new List<float>(); var signal = new List<float>();
                for (int i = 0; i < frames.Count; i++)
                {
                    JointTelemetry sample = frames[i].joints[j]; anchors.Add(sample.anchorError); forces.Add(sample.currentForce.ToVector3().magnitude); torques.Add(sample.currentTorque.ToVector3().magnitude); signal.Add(sample.relativeAngularSpeed);
                    if (frames[i].targetPoses != null) for (int p = 0; p < frames[i].targetPoses.Length; p++) if (frames[i].targetPoses[p].physicsBodyId == sample.bodyId) tracking.Add(frames[i].targetPoses[p].targetPhysicsAngularError);
                }
                float dt = frames[0].fixedDeltaTime, norm = Mathf.Max(totalMass * gravity, 0.001f);
                joints[j] = new JointReport { id = first.id, name = first.name,
                    anchorError = Summary("AnchorError", "m", anchors, Mathf.Max(characterHeight, 0.001f), "world anchor distance", "constraint drift"),
                    force = Summary("JointForce", "N", forces, norm, "ConfigurableJoint.currentForce", "constraint effort"),
                    torque = Summary("JointTorque", "N*m", torques, Mathf.Max(norm * characterHeight, 0.001f), "ConfigurableJoint.currentTorque", "constraint effort"),
                    angularTrackingError = Summary("AngularTrackingError", "deg", tracking, 1f, "Quaternion.Angle(target, physics)", "pose tracking"),
                    oscillationZeroCrossings = RagdollLabMath.ZeroCrossings(signal, 0.001f),
                    dominantFrequencyHz = RagdollLabMath.DominantFrequencyByZeroCrossings(signal, 1f / Mathf.Max(dt, 0.0001f), 0.001f),
                    settlingTimeSeconds = RagdollLabMath.SettlingTime(signal, dt, 0f, 0.05f),
                    anchorErrorEvents = BuildAnchorEventReports(anchors, dt, eventFrames, frames.Count, thresholds) };
            }
            report.joints = joints;
            var offenders = new List<JointReport>(joints);
            offenders.Sort((a, b) => (b.torque?.p95 ?? 0f).CompareTo(a.torque?.p95 ?? 0f));
            int offenderCount = Mathf.Min(5, offenders.Count); report.topOffenderIds = new string[offenderCount];
            for (int i = 0; i < offenderCount; i++) report.topOffenderIds[i] = offenders[i].id;
            return report;
        }

        public static DiagnosticsReport Diagnose(ScenarioReport report, RagdollLabThresholds thresholds = null)
        {
            thresholds ??= ScriptableObject.CreateInstance<RagdollLabThresholds>();
            var result = new DiagnosticsReport();
            if (report?.joints == null) return result;
            for (int i = 0; i < report.joints.Length; i++)
            {
                JointReport joint = report.joints[i];
                // Event reports preserve temporal evidence that p95 can hide
                // in a long capture. If events exist, classify them even when
                // their aggregate share is below the global p95 threshold.
                if (joint.anchorErrorEvents != null && joint.anchorErrorEvents.Length > 0)
                    AddAnchorDriftDiagnostic(result, joint);
                else if (joint.anchorError.p95 > thresholds.anchorErrorWarningMeters)
                    AddAnchorDriftDiagnostic(result, joint);
                if (joint.torque.p95 > thresholds.torqueSpikeWarning)
                    Add(result, "HighJointTorque", "medium", "0.70", joint.name, "torque p95 exceeds threshold", "overcontrol, collision load, or effective inertia", joint.torque, joint.torque.p95, 0, 0);
                if (joint.oscillationZeroCrossings >= thresholds.oscillationWarningCrossings && joint.dominantFrequencyHz >= thresholds.oscillationWarningHz)
                    Add(result, "Oscillation", "medium", "0.72", joint.name, "repeated angular velocity zero crossings", "possibly underdamped response", null, joint.dominantFrequencyHz, 0, joint.oscillationZeroCrossings);
                if (joint.angularTrackingError != null && joint.angularTrackingError.mean > thresholds.trackingWarningDegrees)
                    Add(result, "TrackingError", "medium", "0.65", joint.name, "mean target-to-physics angular error high", "insufficient drive, collision, or mapping mismatch", joint.angularTrackingError, joint.angularTrackingError.mean, 0, 0);
            }
            return result;
        }

        static void AddAnchorDriftDiagnostic(DiagnosticsReport result, JointReport joint)
        {
            bool hasEvents = joint.anchorErrorEvents != null && joint.anchorErrorEvents.Length > 0;
            if (!hasEvents)
            {
                Add(result, "AnchorDrift", "high", "0.85", joint.name,
                    "anchor p95 exceeds threshold (no event markers captured to classify further)",
                    "constraint anchor or solver instability", joint.anchorError, joint.anchorError.p95, 0, 0);
                return;
            }

            bool persistent = false;
            for (int e = 0; e < joint.anchorErrorEvents.Length; e++)
            {
                AnchorDriftEventReport evt = joint.anchorErrorEvents[e];
                if (evt.settlingTimeSeconds >= PersistentAnchorSettlingSeconds
                    || evt.timeAboveThresholdSeconds >= PersistentAnchorTimeAboveThresholdSeconds)
                {
                    persistent = true;
                    break;
                }
            }

            if (persistent)
            {
                Add(result, "PersistentAnchorDrift", "high", "0.85", joint.name,
                    "anchor error has not settled within " + PersistentAnchorSettlingSeconds
                    + "s of an impact event", "constraint anchor or solver instability",
                    joint.anchorError, joint.anchorError.p95, 0, 0);
            }
            else
            {
                Add(result, "TransientAnchorExcursion", "medium", "0.6", joint.name,
                    "anchor error settled within " + PersistentAnchorSettlingSeconds
                    + "s of every captured impact event",
                    "transient impact response, not persistent drift",
                    joint.anchorError, joint.anchorError.p95, 0, 0);
            }
        }

        static AnchorDriftEventReport[] BuildAnchorEventReports(List<float> anchors, float dt, List<(string name, int frameIndex, float simulationTime)> eventFrames, int frameCount, RagdollLabThresholds thresholds)
        {
            if (eventFrames == null || eventFrames.Count == 0) return Array.Empty<AnchorDriftEventReport>();
            thresholds ??= ScriptableObject.CreateInstance<RagdollLabThresholds>();
            float safeDt = Mathf.Max(dt, 0.0001f);
            int horizonFrames = Mathf.Max(1, Mathf.RoundToInt(AnchorEventHorizonSeconds / safeDt));
            var reports = new AnchorDriftEventReport[eventFrames.Count];
            for (int e = 0; e < eventFrames.Count; e++)
            {
                (string name, int frameIndex, float simulationTime) evt = eventFrames[e];
                int windowEnd = Mathf.Min(frameCount, evt.frameIndex + horizonFrames);
                if (e + 1 < eventFrames.Count) windowEnd = Mathf.Min(windowEnd, eventFrames[e + 1].frameIndex);
                windowEnd = Mathf.Max(windowEnd, evt.frameIndex + 1);
                List<float> window = anchors.GetRange(evt.frameIndex, windowEnd - evt.frameIndex);
                float baseline = RagdollLabMath.Baseline(anchors, evt.frameIndex, AnchorEventBaselineLookbackFrames);
                (int peakIndex, float peakValue) = RagdollLabMath.PeakAfter(window, 0, window.Count);
                reports[e] = new AnchorDriftEventReport
                {
                    eventName = evt.name, eventFrameIndex = evt.frameIndex, eventSimulationTime = evt.simulationTime,
                    baseline = baseline, peak = peakValue, peakOffsetSeconds = peakIndex * safeDt,
                    sample50ms = RagdollLabMath.SampleAtOffset(window, safeDt, 0, 0.05f),
                    sample100ms = RagdollLabMath.SampleAtOffset(window, safeDt, 0, 0.1f),
                    sample250ms = RagdollLabMath.SampleAtOffset(window, safeDt, 0, 0.25f),
                    sample500ms = RagdollLabMath.SampleAtOffset(window, safeDt, 0, 0.5f),
                    sample1000ms = RagdollLabMath.SampleAtOffset(window, safeDt, 0, 1f),
                    settlingTimeSeconds = RagdollLabMath.SettlingTime(window, safeDt, baseline, thresholds.anchorErrorWarningMeters),
                    aucError = RagdollLabMath.AreaUnderCurve(window, safeDt, 0, window.Count),
                    timeAboveThresholdSeconds = RagdollLabMath.TimeAboveThreshold(window, safeDt, 0, window.Count, thresholds.anchorErrorWarningMeters),
                };
            }
            return reports;
        }

        static void Add(DiagnosticsReport report, string type, string severity, string confidence, string subject, string observation, string hypothesis, MetricSummary metric, float peak, int first, int count)
        {
            report.diagnostics.Add(new DiagnosticEvidence { type = type, severity = severity, confidence = confidence, subject = subject,
                scenario = "Captured", observation = observation, hypothesis = hypothesis,
                metrics = metric == null ? new[] { peak.ToString("R") } : new[] { metric.name + ".p95=" + metric.p95.ToString("R"), metric.unit }, firstFrame = first, peakFrame = count });
        }

        static MetricSummary Summary(string name, string unit, List<float> values, float divisor, string source, string interpretation)
        {
            if (values == null || values.Count == 0) return new MetricSummary { name = name, unit = unit, source = source, interpretation = interpretation };
            float safe = Mathf.Max(divisor, 0.000001f);
            return new MetricSummary { name = name, unit = unit, source = source, interpretation = interpretation, count = values.Count,
                current = values[values.Count - 1], mean = Mean(values), rms = RagdollLabMath.Rms(values), p95 = RagdollLabMath.Percentile(values, 0.95f), max = Max(values), normalizedMean = Mean(values) / safe };
        }
        static float Mean(List<float> values) { double sum = 0; for (int i = 0; i < values.Count; i++) sum += values[i]; return (float)(sum / values.Count); }
        static float Max(List<float> values) { float max = float.MinValue; for (int i = 0; i < values.Count; i++) max = Mathf.Max(max, values[i]); return max; }
    }
}
