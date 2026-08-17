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

        static readonly string[] PerturbationEventNames =
        {
            "PushApplied",
            "Impact",
            "EventApplied"
        };

        sealed class JointSample
        {
            public int frameIndex;
            public JointTelemetry telemetry;
        }

        sealed class JointAccumulator
        {
            public JointTelemetry first;
            public readonly List<JointSample> samples = new();
            public readonly List<float> anchors = new();
            public readonly List<float> forces = new();
            public readonly List<float> torques = new();
            public readonly List<float> tracking = new();
            public readonly List<float> signal = new();

            public void Add(int frameIndex, JointTelemetry sample, TargetPoseTelemetry[] targetPoses)
            {
                first ??= sample;
                samples.Add(new JointSample { frameIndex = frameIndex, telemetry = sample });
                anchors.Add(sample.anchorError);
                forces.Add(sample.currentForce.ToVector3().magnitude);
                torques.Add(sample.currentTorque.ToVector3().magnitude);
                signal.Add(sample.relativeAngularSpeed);
                if (targetPoses == null) return;
                for (int i = 0; i < targetPoses.Length; i++)
                {
                    TargetPoseTelemetry pose = targetPoses[i];
                    if (pose != null && pose.physicsBodyId == sample.bodyId
                        && (!pose.sourceAvailable || (pose.targetAvailable && pose.physicsAvailable)))
                        tracking.Add(pose.targetPhysicsAngularError);
                }
            }
        }

        sealed class PairAccumulator
        {
            public TargetPoseTelemetry first;
            public readonly List<float> targetPhysicsDistances = new();
            public readonly List<float> targetPhysicsAngularErrors = new();
            public readonly List<float> targetPhysicsVelocityErrors = new();
            public readonly List<float> targetLinearSpeeds = new();
            public readonly List<float> targetAngularSpeeds = new();
            public readonly List<float> physicsLinearSpeeds = new();
            public readonly List<float> physicsAngularSpeeds = new();
            public readonly List<float> targetLinearAccelerations = new();
            public readonly List<float> targetAngularAccelerations = new();
            public readonly List<float> targetLinearJerks = new();
            public readonly List<float> targetAngularJerks = new();
            public readonly List<float> physicsLinearAccelerations = new();
            public readonly List<float> physicsAngularAccelerations = new();
            public readonly List<float> physicsLinearJerks = new();
            public readonly List<float> physicsAngularJerks = new();
            public readonly HashSet<string> mappingWarnings = new();
            public int sampleCount;
            public bool sourceAvailable;
            public bool targetAvailable;
            public bool physicsAvailable;
            public bool authoredMappingAvailable;
            public float authoredMappingPositionWeight;
            public float authoredMappingRotationWeight;
            public bool effectiveMappingAvailable;
            public float effectiveMappingPositionWeight;
            public float effectiveMappingRotationWeight;

            public void Add(TargetPoseTelemetry sample)
            {
                if (sample == null) return;
                first ??= sample;
                sampleCount++;
                sourceAvailable |= sample.sourceAvailable;
                targetAvailable |= sample.targetAvailable;
                physicsAvailable |= sample.physicsAvailable;
                if (sample.targetAvailable && sample.physicsAvailable)
                {
                    AddFinite(targetPhysicsDistances, sample.targetPhysicsDistance);
                    AddFinite(targetPhysicsAngularErrors, sample.targetPhysicsAngularError);
                }
                if (sample.targetVelocityAvailable)
                {
                    AddFinite(targetLinearSpeeds, sample.targetLinearVelocity.ToVector3().magnitude);
                    AddFinite(targetAngularSpeeds, sample.targetAngularVelocity.ToVector3().magnitude);
                }
                if (sample.targetVelocityAvailable && sample.physicsVelocityAvailable)
                    AddFinite(targetPhysicsVelocityErrors,
                        (sample.targetLinearVelocity.ToVector3() - sample.physicsLinearVelocity.ToVector3()).magnitude);
                if (sample.targetAccelerationAvailable)
                {
                    AddFinite(targetLinearAccelerations, sample.targetLinearAcceleration.ToVector3().magnitude);
                    AddFinite(targetAngularAccelerations, sample.targetAngularAcceleration.ToVector3().magnitude);
                }
                if (sample.targetJerkAvailable)
                {
                    AddFinite(targetLinearJerks, sample.targetLinearJerk.ToVector3().magnitude);
                    AddFinite(targetAngularJerks, sample.targetAngularJerk.ToVector3().magnitude);
                }
                if (sample.physicsAvailable)
                {
                    AddFinite(physicsLinearSpeeds, sample.physicsLinearVelocity.ToVector3().magnitude);
                    AddFinite(physicsAngularSpeeds, sample.physicsAngularVelocity.ToVector3().magnitude);
                }
                if (sample.physicsAccelerationAvailable)
                {
                    AddFinite(physicsLinearAccelerations, sample.physicsLinearAcceleration.ToVector3().magnitude);
                    AddFinite(physicsAngularAccelerations, sample.physicsAngularAcceleration.ToVector3().magnitude);
                }
                if (sample.physicsJerkAvailable)
                {
                    AddFinite(physicsLinearJerks, sample.physicsLinearJerk.ToVector3().magnitude);
                    AddFinite(physicsAngularJerks, sample.physicsAngularJerk.ToVector3().magnitude);
                }
                if (sample.authoredMappingAvailable)
                {
                    authoredMappingAvailable = true;
                    authoredMappingPositionWeight = sample.authoredMappingPositionWeight;
                    authoredMappingRotationWeight = sample.authoredMappingRotationWeight;
                }
                if (sample.effectiveMappingAvailable)
                {
                    effectiveMappingAvailable = true;
                    effectiveMappingPositionWeight = sample.effectiveMappingPositionWeight;
                    effectiveMappingRotationWeight = sample.effectiveMappingRotationWeight;
                }
            }

            public PairTrackingReport Build()
            {
                return new PairTrackingReport
                {
                    id = first?.pairId ?? first?.id,
                    bone = first?.bone,
                    targetTransformId = first?.targetTransformId,
                    physicsBodyId = first?.physicsBodyId,
                    sourceAvailable = sourceAvailable,
                    targetAvailable = targetAvailable,
                    physicsAvailable = physicsAvailable,
                    sampleCount = sampleCount,
                    targetPhysicsDistance = Summary("TargetPhysicsDistance", "m", targetPhysicsDistances, 1f, "animated-pair target and physics transforms", "pair position tracking"),
                    targetPhysicsAngularError = Summary("TargetPhysicsAngularError", "deg", targetPhysicsAngularErrors, 1f, "animated-pair target and physics rotations", "pair rotation tracking"),
                    targetPhysicsVelocityError = Summary("TargetPhysicsVelocityError", "m/s", targetPhysicsVelocityErrors, 1f, "animated-pair target and physics linear velocities", "pair velocity tracking"),
                    targetLinearSpeed = Summary("TargetLinearSpeed", "m/s", targetLinearSpeeds, 1f, "AnimatedPair target pose samples", "target motion whole"),
                    targetAngularSpeed = Summary("TargetAngularSpeed", "rad/s", targetAngularSpeeds, 1f, "AnimatedPair target pose samples", "target rotational motion"),
                    physicsLinearSpeed = Summary("PhysicsLinearSpeed", "m/s", physicsLinearSpeeds, 1f, "exact linked Rigidbody samples", "physics motion"),
                    physicsAngularSpeed = Summary("PhysicsAngularSpeed", "rad/s", physicsAngularSpeeds, 1f, "exact linked Rigidbody samples", "physics rotational motion"),
                    targetLinearAcceleration = Summary("TargetLinearAcceleration", "m/s^2", targetLinearAccelerations, 1f, "animated sample-time finite difference", "target linear acceleration"),
                    targetAngularAcceleration = Summary("TargetAngularAcceleration", "rad/s^2", targetAngularAccelerations, 1f, "animated sample-time finite difference", "target angular acceleration"),
                    targetLinearJerk = Summary("TargetLinearJerk", "m/s^3", targetLinearJerks, 1f, "animated sample-time finite difference", "target linear jerk"),
                    targetAngularJerk = Summary("TargetAngularJerk", "rad/s^3", targetAngularJerks, 1f, "animated sample-time finite difference", "target angular jerk"),
                    physicsLinearAcceleration = Summary("PhysicsLinearAcceleration", "m/s^2", physicsLinearAccelerations, 1f, "physics-step velocity finite difference", "physics linear acceleration"),
                    physicsAngularAcceleration = Summary("PhysicsAngularAcceleration", "rad/s^2", physicsAngularAccelerations, 1f, "physics-step velocity finite difference", "physics angular acceleration"),
                    physicsLinearJerk = Summary("PhysicsLinearJerk", "m/s^3", physicsLinearJerks, 1f, "physics-step velocity finite difference", "physics linear jerk"),
                    physicsAngularJerk = Summary("PhysicsAngularJerk", "rad/s^3", physicsAngularJerks, 1f, "physics-step velocity finite difference", "physics angular jerk"),
                    authoredMappingAvailable = authoredMappingAvailable,
                    authoredMappingPositionWeight = authoredMappingPositionWeight,
                    authoredMappingRotationWeight = authoredMappingRotationWeight,
                    effectiveMappingAvailable = effectiveMappingAvailable,
                    effectiveMappingPositionWeight = effectiveMappingPositionWeight,
                    effectiveMappingRotationWeight = effectiveMappingRotationWeight,
                    mappingIntegrityWarnings = new List<string>(mappingWarnings).ToArray()
                };
            }

            static void AddFinite(List<float> values, float value)
            {
                if (RagdollLabMath.IsFinite(value)) values.Add(value);
            }
        }

        sealed class StaggerEpisodeAccumulator
        {
            public readonly StaggerEpisodeReport report = new();
            public readonly List<string> phases = new();
            readonly float timeoutSeconds;
            public bool hasMargin;

            public StaggerEpisodeAccumulator(float timeoutSeconds)
            {
                this.timeoutSeconds = Mathf.Max(0f, timeoutSeconds);
            }

            public void Add(int frameIndex, PhysicsFrame frame, float currentContactDuration)
            {
                StaggerFrameTelemetry stagger = frame.stagger;
                BalanceFrameTelemetry balance = frame.balance;
                if (report.firstFrame == 0 && report.lastFrame == 0 && phases.Count == 0)
                {
                    report.firstFrame = frameIndex;
                    report.firstSimulationTime = frame.simulationTime;
                    report.initialBalanceState = balance?.state ?? "Unavailable";
                }
                report.lastFrame = frameIndex;
                report.lastSimulationTime = frame.simulationTime;
                report.terminalBalanceState = balance?.state ?? report.terminalBalanceState;
                if (stagger != null)
                {
                    report.swingFoot = stagger.swingFootAvailable ? stagger.swingFoot : report.swingFoot;
                    report.stepCount = Mathf.Max(report.stepCount, stagger.stepCount);
                    if (!string.IsNullOrEmpty(stagger.phase)
                        && (phases.Count == 0 || phases[phases.Count - 1] != stagger.phase))
                        phases.Add(stagger.phase);
                    if (stagger.liftOffObserved && report.liftOffFrame < 0) report.liftOffFrame = frameIndex;
                    if (stagger.replantObserved && report.replantFrame < 0)
                    {
                        report.replantFrame = frameIndex;
                        report.replantContactDuration = currentContactDuration;
                    }
                }
                if (balance != null && balance.hasSignedSupportMargin
                    && RagdollLabMath.IsFinite(balance.signedSupportMargin))
                {
                    report.minimumSignedSupportMargin = hasMargin
                        ? Mathf.Min(report.minimumSignedSupportMargin, balance.signedSupportMargin)
                        : balance.signedSupportMargin;
                    report.finalSignedSupportMargin = balance.signedSupportMargin;
                    hasMargin = true;
                }
                string puppetState = frame.character?.puppetState;
                if (!string.IsNullOrEmpty(puppetState) && puppetState != "Unavailable")
                {
                    report.finalPuppetState = puppetState;
                    if (string.Equals(puppetState, "Unpinned", StringComparison.OrdinalIgnoreCase))
                        report.unpinnedObserved = true;
                }
            }

            public StaggerEpisodeReport Finish()
            {
                report.phaseSamples = phases.ToArray();
                if (report.unpinnedObserved) report.terminalOutcome = "Unpinned";
                else if (phases.Contains("Failed")) report.terminalOutcome = "Failed";
                else if (report.lastSimulationTime - report.firstSimulationTime >= timeoutSeconds)
                {
                    report.terminalOutcome = "TimedOut";
                    report.invalidReason = "stagger_episode_timeout";
                }
                else if (report.replantFrame >= 0 && HasValidTerminalRecoveryState(report))
                    report.terminalOutcome = "Recovered";
                else report.terminalOutcome = "Incomplete";
                if (!hasMargin) report.minimumSignedSupportMargin = 0f;
                return report;
            }

            static bool HasValidTerminalRecoveryState(StaggerEpisodeReport value)
            {
                bool returnedToPuppet = !string.IsNullOrEmpty(value.finalPuppetState)
                    && value.finalPuppetState.IndexOf("Puppet", StringComparison.OrdinalIgnoreCase) >= 0
                    && !string.Equals(value.finalPuppetState, "Unpinned", StringComparison.OrdinalIgnoreCase);
                bool terminalBalanceIsValid = !string.IsNullOrEmpty(value.terminalBalanceState)
                    && !string.Equals(value.terminalBalanceState, "Unavailable", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(value.terminalBalanceState, "RequiresStep", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(value.terminalBalanceState, "Unrecoverable", StringComparison.OrdinalIgnoreCase);
                return returnedToPuppet && terminalBalanceIsValid;
            }
        }

        public static ScenarioReport Analyze(IReadOnlyList<PhysicsFrame> frames, float characterHeight, float totalMass, float gravity, RagdollLabThresholds thresholds = null)
        {
            var report = new ScenarioReport { name = "Captured", frameCount = frames?.Count ?? 0 };
            if (frames == null || frames.Count == 0) return report;
            report.durationSeconds = (frames.Count - 1) * frames[0].fixedDeltaTime;
            var eventFrames = new List<(string name, int frameIndex, float simulationTime)>();
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i]?.events == null) continue;
                for (int e = 0; e < frames[i].events.Length; e++)
                {
                    EventMarker marker = frames[i].events[e];
                    if (marker == null) continue;
                    eventFrames.Add((marker.name, i, marker.simulationTime));
                }
            }
            TryFindFirstPerturbationEvent(frames, eventFrames, report);
            var energy = new List<float>(); var comSpeed = new List<float>(); var impulses = new List<float>(); var penetration = new List<float>();
            var balancerTorque = new List<float>();
            int fallenFrames = 0; int firstFall = -1, recovered = -1;
            int contactTransitions = 0, shortContacts = 0;
            bool hasBalanceMargin = false;
            bool hasMaximumBalanceMargin = false;
            bool perturbationObserved = false;
            bool positiveMarginAfterPerturbation = false;
            float recoveryOvershoot = 0f;
            var footSlip = new List<float>();
            var contactStarts = new Dictionary<string, float>();
            thresholds ??= ScriptableObject.CreateInstance<RagdollLabThresholds>();
            for (int i = 0; i < frames.Count; i++)
            {
                PhysicsFrame frame = frames[i];
                energy.Add(frame.character != null ? frame.character.kineticEnergy : 0f);
                comSpeed.Add(frame.character != null ? frame.character.centerOfMassVelocity.ToVector3().magnitude : 0f);
                bool fallen = frame.character != null && frame.character.likelyFallen;
                if (fallen) { fallenFrames++; if (firstFall < 0) firstFall = i; }
                else if (firstFall >= 0 && recovered < 0) recovered = i;
                if (frame.character != null && frame.character.supportReferenceAvailable)
                {
                    report.supportSampleCount++;
                    if (frame.character.supportContactCount <= 0 || frame.character.supportPointCount <= 0)
                        report.supportLossFrameCount++;
                }
                BalanceFrameTelemetry balance = frame.balance;
                if (balance != null && balance.sourceAvailable)
                {
                    report.balanceTelemetryAvailable = true;
                    report.balanceSampleCount++;
                    if (balance.hasCapturePoint)
                    {
                        if (RagdollLabMath.IsFinite(balance.capturePoint.ToVector3()))
                            report.capturePointSampleCount++;
                        else
                            report.capturePointNonFiniteSampleCount++;
                    }
                    if (string.Equals(balance.state, "RequiresStep", StringComparison.Ordinal))
                    {
                        report.requiresStepFrameCount++;
                        if (report.firstRequiresStepSimulationTime < 0f)
                        {
                            report.firstRequiresStepSimulationTime = frame.simulationTime;
                            report.firstRequiresStepFrame = i;
                        }
                        perturbationObserved = true;
                    }
                    if (string.Equals(balance.state, "Unrecoverable", StringComparison.Ordinal)) report.unrecoverableFrameCount++;
                    if (balance.hasSignedSupportMargin && RagdollLabMath.IsFinite(balance.signedSupportMargin))
                    {
                        report.minimumSignedSupportMargin = hasBalanceMargin
                            ? Mathf.Min(report.minimumSignedSupportMargin, balance.signedSupportMargin)
                            : balance.signedSupportMargin;
                        report.finalSignedSupportMargin = balance.signedSupportMargin;
                        report.maximumSignedSupportMargin = hasMaximumBalanceMargin
                            ? Mathf.Max(report.maximumSignedSupportMargin, balance.signedSupportMargin)
                            : balance.signedSupportMargin;
                        hasBalanceMargin = true;
                        report.signedSupportMarginAvailable = true;
                        hasMaximumBalanceMargin = true;
                        if (balance.signedSupportMargin < 0f)
                        {
                            if (positiveMarginAfterPerturbation)
                                recoveryOvershoot = Mathf.Max(recoveryOvershoot, -balance.signedSupportMargin);
                            perturbationObserved = true;
                        }
                        else if (perturbationObserved)
                        {
                            positiveMarginAfterPerturbation = true;
                        }
                    }
                    if (balance.hasBalancerTorque && RagdollLabMath.IsFinite(balance.balancerTorque.ToVector3()))
                    {
                        balancerTorque.Add(balance.balancerTorque.ToVector3().magnitude);
                        report.balancerAppliedFrameCount++;
                    }
                }
                if (frame.feet != null) for (int f = 0; f < frame.feet.Length; f++) footSlip.Add(frame.feet[f].tangentialSlipSpeed);
                if (frame.contacts != null) for (int j = 0; j < frame.contacts.Length; j++)
                {
                    ContactTelemetry contact = frame.contacts[j];
                    if (contact == null) continue;
                    impulses.Add(contact.impulseMagnitude); penetration.Add(contact.penetrationDepth);
                    if (contact.contactStart || contact.contactEnd) contactTransitions++;
                    if (string.IsNullOrEmpty(contact.key)) continue;
                    if (contact.contactStart && !contactStarts.ContainsKey(contact.key))
                    {
                        float startTime = contact.hasContactStartTime && RagdollLabMath.IsFinite(contact.contactStartTime)
                            ? contact.contactStartTime : frame.simulationTime;
                        contactStarts[contact.key] = startTime;
                    }
                    if (!contact.contactEnd || !contactStarts.TryGetValue(contact.key, out float start)) continue;
                    float endTime = contact.hasContactEndTime && RagdollLabMath.IsFinite(contact.contactEndTime)
                        ? contact.contactEndTime : frame.simulationTime;
                    float duration = contact.hasContactDuration && RagdollLabMath.IsFinite(contact.contactDurationSeconds)
                        ? contact.contactDurationSeconds : endTime - start;
                    if (duration >= 0f && duration < Mathf.Max(0f, thresholds.shortContactDurationSeconds)) shortContacts++;
                    contactStarts.Remove(contact.key);
                }
            }
            if (report.perturbationEventAvailable
                && report.firstRequiresStepFrame >= report.firstPerturbationFrame
                && report.firstRequiresStepSimulationTime >= 0f)
            {
                float latency = report.firstRequiresStepSimulationTime
                    - report.firstPerturbationSimulationTime;
                if (RagdollLabMath.IsFinite(latency) && latency >= 0f)
                {
                    report.requiresStepLatencyAvailable = true;
                    report.requiresStepLatencySeconds = latency;
                }
            }
            report.kineticEnergy = Summary("KineticEnergy", "J", energy, 1f, "RigidBody velocities + principal-axis inertia", "system motion energy");
            report.centerOfMassSpeed = Summary("CenterOfMassSpeed", "m/s", comSpeed, 1f, "mass-weighted body COM", "global balance motion");
            report.contactImpulse = Summary("ContactImpulse", "N*s", impulses, 1f, "Collision ContactPoint.impulse", "impact/contact load");
            report.penetration = Summary("PenetrationDepth", "m", penetration, Mathf.Max(characterHeight, 0.001f), "collision penetration telemetry", "constraint/collision integrity");
            report.footSlipSpeed = Summary("FootSlipSpeed", "m/s", footSlip, 1f, "foot Rigidbody horizontal velocity while stance", "foot sliding");
            report.balancerTorque = Summary("BalancerTorque", "N*m", balancerTorque, 1f, "read-only reactive balancer output", "reactive balance effort");
            report.dominantFrequencyHz = RagdollLabMath.DominantFrequencyDft(comSpeed, 1f / Mathf.Max(frames[0].fixedDeltaTime, 0.0001f));
            report.fallenFrameCount = fallenFrames;
            report.recoveryTimeSeconds = firstFall >= 0 && recovered >= 0 ? (recovered - firstFall) * frames[0].fixedDeltaTime : 0f;
            report.contactTransitionsPerSecond = contactTransitions / Mathf.Max(report.durationSeconds, frames[0].fixedDeltaTime);
            report.shortContactCount = shortContacts;
            report.recoveryOvershootMeters = recoveryOvershoot;

            var jointById = new Dictionary<string, JointAccumulator>();
            var jointAccumulators = new List<JointAccumulator>();
            var pairById = new Dictionary<string, PairAccumulator>();
            var pairAccumulators = new List<PairAccumulator>();
            var mappingWarnings = new HashSet<string>();
            for (int i = 0; i < frames.Count; i++)
            {
                PhysicsFrame frame = frames[i];
                report.animatedPairSourceAvailable |= frame.animatedPairSourceAvailable;
                TargetPoseTelemetry[] pairSamples = GetAnimatedPairSamples(frame);
                report.animatedPairCount = Mathf.Max(report.animatedPairCount, frame.animatedPairCount);
                report.animatedPairCount = Mathf.Max(report.animatedPairCount, pairSamples.Length);
                if (frame.mappingIntegrityWarnings != null)
                    for (int w = 0; w < frame.mappingIntegrityWarnings.Length; w++)
                        if (!string.IsNullOrEmpty(frame.mappingIntegrityWarnings[w]))
                            mappingWarnings.Add(frame.mappingIntegrityWarnings[w]);
                for (int p = 0; p < pairSamples.Length; p++)
                {
                    TargetPoseTelemetry sample = pairSamples[p];
                    string pairId = sample?.pairId;
                    if (string.IsNullOrEmpty(pairId)) pairId = sample?.id;
                    if (sample == null || string.IsNullOrEmpty(pairId))
                    {
                        mappingWarnings.Add("animated_pair_missing_identity");
                        continue;
                    }
                    if (!pairById.TryGetValue(pairId, out PairAccumulator pairAccumulator))
                    {
                        pairAccumulator = new PairAccumulator();
                        pairById.Add(pairId, pairAccumulator);
                        pairAccumulators.Add(pairAccumulator);
                    }
                    pairAccumulator.Add(sample);
                    report.animatedPairSampleCount++;
                }

                JointTelemetry[] frameJoints = frames[i].joints;
                if (frameJoints == null) continue;
                for (int j = 0; j < frameJoints.Length; j++)
                {
                    JointTelemetry sample = frameJoints[j];
                    if (sample == null || string.IsNullOrEmpty(sample.id)) continue;
                    if (!jointById.TryGetValue(sample.id, out JointAccumulator accumulator))
                    {
                        accumulator = new JointAccumulator();
                        jointById.Add(sample.id, accumulator);
                        jointAccumulators.Add(accumulator);
                    }
                    accumulator.Add(i, sample, pairSamples);
                }
            }
            var joints = new JointReport[jointAccumulators.Count];
            for (int j = 0; j < jointAccumulators.Count; j++)
            {
                JointAccumulator accumulator = jointAccumulators[j];
                JointTelemetry first = accumulator.first;
                float dt = frames[0].fixedDeltaTime, norm = Mathf.Max(totalMass * gravity, 0.001f);
                joints[j] = new JointReport { id = first.id, name = first.name,
                    anchorError = Summary("AnchorError", "m", accumulator.anchors, Mathf.Max(characterHeight, 0.001f), "world anchor distance", "constraint drift"),
                    force = Summary("JointForce", "N", accumulator.forces, norm, "ConfigurableJoint.currentForce", "constraint effort"),
                    torque = Summary("JointTorque", "N*m", accumulator.torques, Mathf.Max(norm * characterHeight, 0.001f), "ConfigurableJoint.currentTorque", "constraint effort"),
                    angularTrackingError = Summary("AngularTrackingError", "deg", accumulator.tracking, 1f, "Quaternion.Angle(target, physics)", "pose tracking"),
                    oscillationZeroCrossings = RagdollLabMath.ZeroCrossings(accumulator.signal, 0.001f),
                    dominantFrequencyHz = RagdollLabMath.DominantFrequencyByZeroCrossings(accumulator.signal, 1f / Mathf.Max(dt, 0.0001f), 0.001f),
                    settlingTimeSeconds = RagdollLabMath.SettlingTime(accumulator.signal, dt, 0f, 0.05f),
                    anchorErrorEvents = BuildAnchorEventReports(accumulator.samples, dt, eventFrames, frames.Count, thresholds) };
            }
            report.joints = joints;
            foreach (PairAccumulator pairAccumulator in pairAccumulators)
            {
                string pairId = pairAccumulator.first?.pairId ?? pairAccumulator.first?.id;
                if (string.IsNullOrEmpty(pairId)) continue;
                foreach (string warning in mappingWarnings)
                    if (warning.IndexOf(pairId, StringComparison.Ordinal) >= 0)
                        pairAccumulator.mappingWarnings.Add(warning);
            }
            var pairReports = new PairTrackingReport[pairAccumulators.Count];
            for (int i = 0; i < pairAccumulators.Count; i++) pairReports[i] = pairAccumulators[i].Build();
            report.pairTracking = pairReports;
            var sortedMappingWarnings = new List<string>(mappingWarnings);
            sortedMappingWarnings.Sort(StringComparer.Ordinal);
            report.mappingIntegrityWarnings = sortedMappingWarnings.ToArray();
            report.staggerEpisodes = BuildStaggerEpisodeReports(frames, thresholds);
            for (int i = 0; i < report.staggerEpisodes.Length; i++)
            {
                StaggerEpisodeReport episode = report.staggerEpisodes[i];
                if (string.Equals(episode.terminalOutcome, "Recovered", StringComparison.Ordinal)) report.recoveredStaggerEpisodeCount++;
                else if (string.Equals(episode.terminalOutcome, "Unpinned", StringComparison.Ordinal)) report.unpinnedStaggerEpisodeCount++;
                else report.failedStaggerEpisodeCount++;
            }
            var offenders = new List<JointReport>(joints);
            offenders.Sort((a, b) => (b.torque?.p95 ?? 0f).CompareTo(a.torque?.p95 ?? 0f));
            int offenderCount = Mathf.Min(5, offenders.Count); report.topOffenderIds = new string[offenderCount];
            for (int i = 0; i < offenderCount; i++) report.topOffenderIds[i] = offenders[i].id;
            return report;
        }

        static TargetPoseTelemetry[] GetAnimatedPairSamples(PhysicsFrame frame)
        {
            if (frame == null) return Array.Empty<TargetPoseTelemetry>();
            if (frame.animatedPairCaptureAttempted || frame.animatedPairSourceAvailable)
                return frame.animatedPairs ?? Array.Empty<TargetPoseTelemetry>();
            if (frame.animatedPairs != null && frame.animatedPairs.Length > 0)
                return frame.animatedPairs;
            return frame.targetPoses ?? Array.Empty<TargetPoseTelemetry>();
        }

        static StaggerEpisodeReport[] BuildStaggerEpisodeReports(IReadOnlyList<PhysicsFrame> frames, RagdollLabThresholds thresholds)
        {
            if (frames == null || frames.Count == 0) return Array.Empty<StaggerEpisodeReport>();
            var byId = new Dictionary<string, StaggerEpisodeAccumulator>();
            var order = new List<StaggerEpisodeAccumulator>();
            var stanceStartTimes = new Dictionary<string, float>();
            for (int i = 0; i < frames.Count; i++)
            {
                PhysicsFrame frame = frames[i];
                string id = frame.stagger?.episodeId;
                float currentContactDuration = CurrentFootContactDuration(frame, frame.stagger?.swingFoot, stanceStartTimes);
                if (string.IsNullOrEmpty(id)) continue;
                if (!byId.TryGetValue(id, out StaggerEpisodeAccumulator accumulator))
                {
                    accumulator = new StaggerEpisodeAccumulator(thresholds != null ? thresholds.staggerEpisodeTimeoutSeconds : 2f);
                    byId.Add(id, accumulator);
                    order.Add(accumulator);
                    accumulator.report.episodeId = id;
                }
                accumulator.Add(i, frame, currentContactDuration);
            }
            var reports = new StaggerEpisodeReport[order.Count];
            for (int i = 0; i < order.Count; i++) reports[i] = order[i].Finish();
            return reports;
        }

        static float CurrentFootContactDuration(
            PhysicsFrame frame, string swingFoot, Dictionary<string, float> stanceStartTimes)
        {
            if (frame?.feet == null)
            {
                stanceStartTimes.Clear();
                return 0f;
            }
            float currentTime = RagdollLabMath.IsFinite(frame.simulationTime) ? frame.simulationTime : 0f;
            float sampleDuration = RagdollLabMath.IsFinite(frame.fixedDeltaTime)
                ? Mathf.Max(0f, frame.fixedDeltaTime) : 0f;
            float selectedDuration = 0f;
            // FootTelemetry.contactDuration is cumulative across the capture.
            // Reconstruct the contiguous stance interval from frame boundaries.
            var observedIds = new HashSet<string>();
            for (int i = 0; i < frame.feet.Length; i++)
            {
                FootTelemetry foot = frame.feet[i];
                if (foot == null) continue;
                string id = !string.IsNullOrEmpty(foot.id) ? foot.id : foot.name;
                if (string.IsNullOrEmpty(id)) continue;
                observedIds.Add(id);
                if (!foot.stance)
                {
                    stanceStartTimes.Remove(id);
                    continue;
                }
                if (!stanceStartTimes.TryGetValue(id, out float startTime) || currentTime < startTime)
                    stanceStartTimes[id] = currentTime;
                float duration = Mathf.Max(0f, currentTime - stanceStartTimes[id]) + sampleDuration;
                if (!string.IsNullOrEmpty(swingFoot)
                    && !string.IsNullOrEmpty(foot.name)
                    && (foot.name.IndexOf(swingFoot, StringComparison.OrdinalIgnoreCase) >= 0
                        || swingFoot.IndexOf(foot.name, StringComparison.OrdinalIgnoreCase) >= 0))
                    selectedDuration = Mathf.Max(selectedDuration, duration);
            }
            var missingIds = new List<string>();
            foreach (string id in stanceStartTimes.Keys)
                if (!observedIds.Contains(id)) missingIds.Add(id);
            for (int i = 0; i < missingIds.Count; i++) stanceStartTimes.Remove(missingIds[i]);
            return selectedDuration;
        }

        public static DiagnosticsReport Diagnose(ScenarioReport report, RagdollLabThresholds thresholds = null)
        {
            thresholds ??= ScriptableObject.CreateInstance<RagdollLabThresholds>();
            var result = new DiagnosticsReport();
            if (report == null) return result;
            ScenarioProfile profile = RagdollLabScenarioProfiles.Resolve(report.name);
            result.scenarioProfile = profile.id;
            result.profileAvailable = profile.available;
            if (!profile.available)
                result.unavailableReasons.Add("scenario_profile_unavailable:" + (string.IsNullOrEmpty(report.name) ? "missing" : report.name));
            else
            {
                List<string> missingSignals = RagdollLabScenarioSignalCatalog.MissingRequiredSignals(profile, report, "report");
                result.unavailableReasons.AddRange(missingSignals);
            }
            if (profile.available && IsRecoveryProfile(profile.id) && !report.balanceTelemetryAvailable)
                result.unavailableReasons.Add("balance_telemetry_unavailable");
            else if (profile.available && IsRecoveryProfile(profile.id) && report.balanceSampleCount > 0
                && !report.signedSupportMarginAvailable)
                result.unavailableReasons.Add("signed_support_margin_unavailable");
            if (report.joints != null) for (int i = 0; i < report.joints.Length; i++)
            {
                JointReport joint = report.joints[i];
                AddAnchorDriftDiagnostic(result, joint, thresholds);
                if (joint.torque != null && joint.torque.p95 > thresholds.torqueSpikeWarning)
                    Add(result, "HighJointTorque", "medium", "0.70", joint.name, "torque p95 exceeds threshold", "overcontrol, collision load, or effective inertia", joint.torque, joint.torque.p95, 0, 0);
                if (joint.oscillationZeroCrossings >= thresholds.oscillationWarningCrossings && joint.dominantFrequencyHz >= thresholds.oscillationWarningHz)
                    Add(result, "Oscillation", "medium", "0.72", joint.name, "repeated angular velocity zero crossings", "possibly underdamped response", null, joint.dominantFrequencyHz, 0, joint.oscillationZeroCrossings);
                if (joint.angularTrackingError != null && joint.angularTrackingError.mean > thresholds.trackingWarningDegrees)
                    Add(result, "TrackingError", "medium", "0.65", joint.name, "mean target-to-physics angular error high", "insufficient drive, collision, or mapping mismatch", joint.angularTrackingError, joint.angularTrackingError.mean, 0, 0);
            }
            if (report.mappingIntegrityWarnings != null && report.mappingIntegrityWarnings.Length > 0)
                Add(result, "MAPPING_INTEGRITY", "medium", "0.91", report.name,
                    "animated-pair identity or mapping availability warnings were captured",
                    "pair identity changed, or the recorder could not prove the mapping state",
                    null, report.mappingIntegrityWarnings.Length, 0, report.mappingIntegrityWarnings.Length);
            if (report.shortContactCount > 0)
                Add(result, "CONTACT_CHATTER", "medium", "0.82", report.name, "short ground/contact intervals detected", "contact filtering, collider geometry, or unstable support", null, report.shortContactCount, 0, report.shortContactCount);
            if (report.penetration != null && report.penetration.max > thresholds.penetrationWarningMeters)
                Add(result, "COLLIDER_PENETRATION", "high", "0.86", report.name, "penetration exceeds threshold", "collision geometry, solver budget, or excessive corrective impulse", report.penetration, report.penetration.max, 0, 0);
            if (report.footSlipSpeed != null && report.footSlipSpeed.mean > thresholds.footSlipWarningMetersPerSecond)
                Add(result, "FOOT_SLIDING", "medium", "0.80", report.name, "mean stance-foot slip exceeds threshold", "insufficient friction, overpowered drives, or invalid stance classification", report.footSlipSpeed, report.footSlipSpeed.mean, 0, 0);
            if (report.kineticEnergy != null && report.kineticEnergy.mean > 0f
                && report.kineticEnergy.max > report.kineticEnergy.mean * Mathf.Max(1f, thresholds.energySpikeRatio))
                Add(result, "ENERGY_SPIKE", "high", "0.78", report.name, "kinetic energy peak exceeds configured ratio", "stored drive energy, collision impulse, or solver instability", report.kineticEnergy, report.kineticEnergy.max, 0, 0);
            if (report.balancerTorque != null && report.balancerTorque.max > thresholds.balancerTorqueWarning)
                Add(result, "BALANCER_OVERCONTROL", "high", "0.80", report.name, "reactive balancer torque exceeds configured warning", "excessive torque limit, gain, or incorrect support estimate", report.balancerTorque, report.balancerTorque.max, 0, 0);
            if (report.balanceTelemetryAvailable
                && report.minimumSignedSupportMargin < -Mathf.Max(0f, thresholds.supportInstabilityMarginMeters))
                Add(result, "COM_INSTABILITY", "high", "0.84", report.name,
                    "signed capture margin crossed the configured instability deficit",
                    "support geometry, capture-point velocity, or insufficient balance authority",
                    null, report.minimumSignedSupportMargin, 0, 0,
                    -1f, report.firstRequiresStepSimulationTime);
            if (report.supportSampleCount > 0 && report.supportLossFrameCount > 0)
                Add(result, "CONTACT_SUPPORT_LOST", "high", "0.84", report.name,
                    "ground-support telemetry contains frames without a valid support contact",
                    "contact classification, support geometry, or loss of stance during recovery",
                    null, report.supportLossFrameCount, 0, report.supportLossFrameCount);
            if (profile.available && IsRecoveryProfile(profile.id)
                && report.recoveryTimeSeconds > thresholds.recoveryTooSlowSeconds)
                Add(result, "RECOVERY_TOO_SLOW", "medium", "0.78", report.name,
                    "recovery exceeded the scenario recovery-time threshold",
                    "insufficient balance authority, delayed GetUp/Stagger transition, or missing support",
                    null, report.recoveryTimeSeconds, 0, 0, 0f, report.recoveryTimeSeconds);
            if (profile.available && IsRecoveryProfile(profile.id)
                && report.recoveryOvershootMeters > thresholds.recoveryOvershootMeters)
                Add(result, "RECOVERY_OVERSHOOT", "medium", "0.75", report.name,
                    "signed support margin became negative again after a positive recovery sample",
                    "under-damped correction, excessive step landing energy, or unstable contact",
                    null, report.recoveryOvershootMeters, 0, 0);
            if (profile.available && IsRecoveryProfile(profile.id)
                && report.firstRequiresStepSimulationTime >= 0f
                && !report.requiresStepLatencyAvailable)
                result.unavailableReasons.Add("requires_step_perturbation_marker_unavailable");
            if (profile.available && IsRecoveryProfile(profile.id)
                && report.requiresStepLatencyAvailable
                && report.requiresStepLatencySeconds < thresholds.requiresStepEarlySeconds)
                Add(result, "STEP_REQUIRED_TOO_EARLY", "medium", "0.70", report.name,
                    "RequiresStep was classified " + report.requiresStepLatencySeconds.ToString("R")
                    + " seconds after perturbation event " + report.firstPerturbationEventName,
                    "capture margin threshold, initial-condition mismatch, or stale balance snapshot",
                    null, report.requiresStepLatencySeconds, report.firstPerturbationFrame,
                    report.firstRequiresStepFrame, report.firstPerturbationSimulationTime,
                    report.firstRequiresStepSimulationTime);
            if (report.fallenFrameCount > 0 && report.recoveryTimeSeconds <= 0f)
                Add(result, "FAILED_TO_RECOVER", "high", "0.84", report.name, "fall samples were recorded without a recovery interval", "unrecoverable balance state, missing support, or incomplete capture", null, report.fallenFrameCount, 0, report.fallenFrameCount);
            if (report.staggerEpisodes != null) for (int i = 0; i < report.staggerEpisodes.Length; i++)
            {
                StaggerEpisodeReport episode = report.staggerEpisodes[i];
                if (episode.unpinnedObserved || string.Equals(episode.terminalOutcome, "Unpinned", StringComparison.Ordinal))
                    Add(result, "STEP_UNPINNED", "high", "0.90", episode.episodeId, "Stagger episode entered Unpinned", "step recovery lost Puppet authority", null, 1f, episode.firstFrame, episode.lastFrame);
                else if (episode.replantFrame < 0)
                    Add(result, "STEP_FAILED_TO_REPLANT", "high", "0.86", episode.episodeId, "Stagger episode ended without corrected ground replant", "Animator timing, selected-foot support, or terrain contact failure", null, episode.lastSimulationTime - episode.firstSimulationTime, episode.firstFrame, episode.lastFrame);
                else if (episode.finalSignedSupportMargin <= episode.minimumSignedSupportMargin)
                    Add(result, "RECOVERY_MARGIN_NOT_IMPROVED", "medium", "0.72", episode.episodeId, "replant occurred without improving the signed support margin", "landing target, support classification, or balancer response is insufficient", null, episode.finalSignedSupportMargin, episode.firstFrame, episode.replantFrame);
            }
            CompleteProvenance(result, report);
            return result;
        }

        public static DiagnosticsReport Diagnose(EvaluationReport evaluation, RagdollLabThresholds thresholds = null)
        {
            DiagnosticsReport result = Diagnose(evaluation?.scenarioReport, thresholds);
            if (evaluation != null && !evaluation.finiteData)
                result.unavailableReasons.Add("non_finite_telemetry");
            BalanceComparisonReport comparison = evaluation?.balanceComparison;
            if (comparison == null || !comparison.profileAvailable
                || !string.Equals(comparison.scenarioProfile, "Balancer", StringComparison.Ordinal))
                return result;

            bool noImprovement = comparison.rejectionReasons != null
                && comparison.rejectionReasons.Contains("balancer_no_stability_improvement");
            if (noImprovement)
                Add(result, "BALANCER_INEFFECTIVE", "medium", "0.76", comparison.candidateRunId,
                    "paired Balancer comparison found no accepted stability improvement",
                    "reactive torque mapping is ineffective for this perturbation or is masked by another limit",
                    null, 0f, -1, -1);
            CompleteProvenance(result, evaluation?.scenarioReport);
            return result;
        }

        static bool IsRecoveryProfile(string profileId)
        {
            return string.Equals(profileId, "Push", StringComparison.Ordinal)
                || string.Equals(profileId, "GetUp", StringComparison.Ordinal)
                || string.Equals(profileId, "Stagger", StringComparison.Ordinal)
                || string.Equals(profileId, "Balancer", StringComparison.Ordinal);
        }

        static void TryFindFirstPerturbationEvent(
            IReadOnlyList<PhysicsFrame> frames,
            List<(string name, int frameIndex, float simulationTime)> eventFrames,
            ScenarioReport report)
        {
            if (frames == null || eventFrames == null || report == null) return;
            for (int i = 0; i < eventFrames.Count; i++)
            {
                var marker = eventFrames[i];
                if (!IsPerturbationEvent(marker.name)) continue;
                int frameIndex = Mathf.Clamp(marker.frameIndex, 0, frames.Count - 1);
                float frameTime = frames[frameIndex] != null
                    ? frames[frameIndex].simulationTime
                    : marker.simulationTime;
                if (!RagdollLabMath.IsFinite(frameTime)) frameTime = marker.simulationTime;
                if (!RagdollLabMath.IsFinite(frameTime)) continue;
                report.perturbationEventAvailable = true;
                report.firstPerturbationEventName = marker.name;
                report.firstPerturbationFrame = frameIndex;
                report.firstPerturbationSimulationTime = frameTime;
                return;
            }
        }

        static bool IsPerturbationEvent(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < PerturbationEventNames.Length; i++)
                if (string.Equals(name, PerturbationEventNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            return name.IndexOf("push", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("impact", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void AddAnchorDriftDiagnostic(
            DiagnosticsReport result, JointReport joint, RagdollLabThresholds thresholds)
        {
            bool hasEvents = joint.anchorErrorEvents != null && joint.anchorErrorEvents.Length > 0;
            if (!hasEvents)
            {
                if (joint.anchorError.p95 <= thresholds.anchorErrorWarningMeters) return;
                Add(result, "AnchorDrift", "high", "0.85", joint.name,
                    "anchor p95 exceeds threshold (no event markers captured to classify further)",
                    "constraint anchor or solver instability", joint.anchorError, joint.anchorError.p95, 0, 0);
                return;
            }

            bool qualifyingExcursion = false;
            bool persistent = false;
            for (int e = 0; e < joint.anchorErrorEvents.Length; e++)
            {
                AnchorDriftEventReport evt = joint.anchorErrorEvents[e];
                // EventMarker also records lifecycle events such as GetUp.
                // It is only anchor-drift evidence once its actual peak crossed
                // the configured anchor-error threshold.
                if (evt.peak <= thresholds.anchorErrorWarningMeters) continue;
                qualifyingExcursion = true;
                if (evt.settlingTimeSeconds >= PersistentAnchorSettlingSeconds
                    || evt.timeAboveThresholdSeconds >= PersistentAnchorTimeAboveThresholdSeconds)
                {
                    persistent = true;
                    break;
                }
            }

            if (!qualifyingExcursion)
            {
                if (joint.anchorError.p95 > thresholds.anchorErrorWarningMeters)
                    Add(result, "AnchorDrift", "high", "0.85", joint.name,
                        "anchor p95 exceeds threshold but no event had a qualifying anchor excursion",
                        "constraint anchor or solver instability", joint.anchorError,
                        joint.anchorError.p95, 0, 0);
                return;
            }

            if (persistent)
            {
                Add(result, "PersistentAnchorDrift", "high", "0.85", joint.name,
                    "anchor error has not settled within " + PersistentAnchorSettlingSeconds
                    + "s of a qualifying event", "constraint anchor or solver instability",
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

        static AnchorDriftEventReport[] BuildAnchorEventReports(
            List<JointSample> samples,
            float dt,
            List<(string name, int frameIndex, float simulationTime)> eventFrames,
            int frameCount,
            RagdollLabThresholds thresholds)
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
                var window = new List<float>();
                var windowFrames = new List<int>();
                var before = new List<float>();
                for (int i = 0; i < samples.Count; i++)
                {
                    JointSample sample = samples[i];
                    if (sample.frameIndex >= evt.frameIndex && sample.frameIndex < windowEnd)
                    {
                        window.Add(sample.telemetry.anchorError);
                        windowFrames.Add(sample.frameIndex);
                    }
                    else if (sample.frameIndex < evt.frameIndex) before.Add(sample.telemetry.anchorError);
                }
                int baselineCount = Mathf.Min(before.Count, AnchorEventBaselineLookbackFrames);
                float baseline = before.Count > 0
                    ? MeanTail(before, baselineCount)
                    : (window.Count > 0 ? window[0] : 0f);
                (int peakIndex, float peakValue) = RagdollLabMath.PeakAfter(window, 0, window.Count);
                reports[e] = new AnchorDriftEventReport
                {
                    eventName = evt.name, eventFrameIndex = evt.frameIndex, eventSimulationTime = evt.simulationTime,
                    baseline = baseline, peak = peakValue,
                    peakOffsetSeconds = windowFrames.Count > peakIndex ? (windowFrames[peakIndex] - evt.frameIndex) * safeDt : 0f,
                    sample50ms = SampleAtOffset(samples, evt.frameIndex, safeDt, 0.05f, frameCount),
                    sample100ms = SampleAtOffset(samples, evt.frameIndex, safeDt, 0.1f, frameCount),
                    sample250ms = SampleAtOffset(samples, evt.frameIndex, safeDt, 0.25f, frameCount),
                    sample500ms = SampleAtOffset(samples, evt.frameIndex, safeDt, 0.5f, frameCount),
                    sample1000ms = SampleAtOffset(samples, evt.frameIndex, safeDt, 1f, frameCount),
                    settlingTimeSeconds = RagdollLabMath.SettlingTime(window, safeDt, baseline, thresholds.anchorErrorWarningMeters),
                    aucError = RagdollLabMath.AreaUnderCurve(window, safeDt, 0, window.Count),
                    timeAboveThresholdSeconds = RagdollLabMath.TimeAboveThreshold(window, safeDt, 0, window.Count, thresholds.anchorErrorWarningMeters),
                };
            }
            return reports;
        }

        static float SampleAtOffset(List<JointSample> samples, int eventFrame, float dt, float offsetSeconds, int frameCount)
        {
            if (samples == null || samples.Count == 0) return 0f;
            int requestedFrame = Mathf.Clamp(eventFrame + Mathf.RoundToInt(offsetSeconds / dt), 0, Mathf.Max(0, frameCount - 1));
            JointSample best = samples[0];
            int bestDistance = Mathf.Abs(best.frameIndex - requestedFrame);
            for (int i = 1; i < samples.Count; i++)
            {
                int distance = Mathf.Abs(samples[i].frameIndex - requestedFrame);
                if (distance < bestDistance)
                {
                    best = samples[i];
                    bestDistance = distance;
                }
            }
            return best.telemetry.anchorError;
        }

        static float MeanTail(List<float> values, int count)
        {
            if (values == null || values.Count == 0 || count <= 0) return 0f;
            int start = Mathf.Max(0, values.Count - count);
            double sum = 0d;
            for (int i = start; i < values.Count; i++) sum += values[i];
            return (float)(sum / (values.Count - start));
        }

        static void Add(DiagnosticsReport report, string type, string severity, string confidence, string subject, string observation, string hypothesis, MetricSummary metric, float peak, int first, int count, float firstSimulationTime = -1f, float peakSimulationTime = -1f)
        {
            report.diagnostics.Add(new DiagnosticEvidence { type = type, severity = severity, confidence = confidence, subject = subject,
                scenario = report.scenarioProfile, observation = observation, hypothesis = hypothesis,
                metrics = metric == null ? new[] { peak.ToString("R") } : new[] { metric.name + ".p95=" + metric.p95.ToString("R"), metric.unit },
                firstFrame = first, peakFrame = count, firstSimulationTime = firstSimulationTime, peakSimulationTime = peakSimulationTime,
                recommendedChecks = RecommendationsFor(type), falsifiers = FalsifiersFor(type) });
        }

        static string[] RecommendationsFor(string type)
        {
            switch (type)
            {
                case "CONTACT_SUPPORT_LOST": return new[] { "verify ground-support normal and support collider layer" };
                case "COM_INSTABILITY": return new[] { "inspect capture point, support margin, and stance contact telemetry" };
                case "RECOVERY_TOO_SLOW": return new[] { "compare recovery transition times against the paired baseline" };
                case "RECOVERY_OVERSHOOT": return new[] { "inspect post-recovery margin samples and reactive torque" };
                case "STEP_REQUIRED_TOO_EARLY": return new[] { "verify PushApplied/impact EventMarker and RequiresStep transition timing" };
                case "BALANCER_INEFFECTIVE": return new[] { "rerun a paired Balancer OFF/ON capture with identical setup" };
                case "MAPPING_INTEGRITY": return new[] { "inspect animated-pair identity set and authored/effective mapping availability per frame" };
                case "STEP_FAILED_TO_REPLANT": return new[] { "verify selected-foot ground support and Animator phase timing" };
                case "STEP_UNPINNED": return new[] { "trace behaviour activation and Puppet state transitions by physics tick" };
                default: return new[] { "capture a paired run and inspect the cited metric at its first and peak frames" };
            }
        }

        static string[] FalsifiersFor(string type)
        {
            switch (type)
            {
                case "CONTACT_SUPPORT_LOST": return new[] { "all support samples are ground-backed after filtering" };
                case "COM_INSTABILITY": return new[] { "signed support margin remains within the configured deficit" };
                case "RECOVERY_TOO_SLOW": return new[] { "recovery completes below the configured time threshold" };
                case "RECOVERY_OVERSHOOT": return new[] { "no negative margin sample follows a positive recovery sample" };
                case "STEP_REQUIRED_TOO_EARLY": return new[] { "the perturbation marker is valid and RequiresStep latency is outside the early window" };
                case "BALANCER_INEFFECTIVE": return new[] { "paired comparison accepts a stability improvement without safety regressions" };
                case "MAPPING_INTEGRITY": return new[] { "pair identities remain stable and effective mapping weights are available for every animated pair" };
                default: return new[] { "the cited metric stays below its configured diagnostic threshold" };
            }
        }

        static void CompleteProvenance(DiagnosticsReport report, ScenarioReport source)
        {
            if (report?.diagnostics == null) return;
            int lastFrame = Mathf.Max(0, (source?.frameCount ?? 1) - 1);
            float lastTime = Mathf.Max(0f, source?.durationSeconds ?? 0f);
            for (int i = 0; i < report.diagnostics.Count; i++)
            {
                DiagnosticEvidence evidence = report.diagnostics[i];
                if (evidence.firstFrame < 0) evidence.firstFrame = 0;
                if (evidence.peakFrame < evidence.firstFrame) evidence.peakFrame = lastFrame;
                if (evidence.firstSimulationTime < 0f) evidence.firstSimulationTime = 0f;
                if (evidence.peakSimulationTime < evidence.firstSimulationTime) evidence.peakSimulationTime = lastTime;
            }
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
