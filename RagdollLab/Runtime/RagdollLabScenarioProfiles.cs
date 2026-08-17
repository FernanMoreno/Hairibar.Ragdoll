using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.RagdollLab
{
    /// <summary>
    /// Stable identifiers used by scenario contracts. These are artifact
    /// protocol IDs, not display labels; changing one is a schema migration.
    /// </summary>
    public static class RagdollLabScenarioSignalIds
    {
        public const string KineticEnergy = "energy.kineticEnergy";
        public const string CenterOfMassSpeed = "balance.centerOfMassSpeed";
        public const string SignedSupportMargin = "balance.signedSupportMargin";
        public const string CapturePoint = "balance.capturePoint";
        public const string RecoveryTime = "recovery.time";
        public const string FallenFrames = "recovery.fallenFrames";
        public const string RecoveryCompletion = "recovery.completion";
        public const string TrackingPoseError = "tracking.poseError";
        public const string TrackingVelocityError = "tracking.velocityError";
        public const string LocomotionTaskCompletion = "locomotion.taskCompletion";
        public const string FootSlip = "foot.slip";
        public const string ContactPenetration = "contact.penetration";
        public const string StaggerReplant = "stagger.replant";
        public const string StaggerTerminalOutcome = "stagger.terminalOutcome";
        public const string PropLifecycleCompletion = "prop.lifecycleCompletion";
    }

    /// <summary>
    /// Machine-readable contract metadata for one required signal. The source,
    /// finite rule and falsifier are retained here so a missing signal is
    /// actionable instead of becoming a generic "metrics missing" result.
    /// </summary>
    public sealed class ScenarioSignalDescriptor
    {
        public readonly string id;
        public readonly string source;
        public readonly string unit;
        public readonly string valueType;
        public readonly string availabilityMinimum;
        public readonly string finiteRule;
        public readonly string falsifier;

        internal ScenarioSignalDescriptor(
            string id,
            string source,
            string unit,
            string valueType,
            string availabilityMinimum,
            string finiteRule,
            string falsifier)
        {
            this.id = id;
            this.source = source;
            this.unit = unit;
            this.valueType = valueType;
            this.availabilityMinimum = availabilityMinimum;
            this.finiteRule = finiteRule;
            this.falsifier = falsifier;
        }
    }

    /// <summary>
    /// Executes the scenario signal contract against an analyzed artifact.
    /// No signal is inferred from an unrelated balance metric.
    /// </summary>
    public static class RagdollLabScenarioSignalCatalog
    {
        static readonly Dictionary<string, ScenarioSignalDescriptor> descriptors =
            new(StringComparer.Ordinal)
            {
                [RagdollLabScenarioSignalIds.KineticEnergy] = new(
                    RagdollLabScenarioSignalIds.KineticEnergy, "ScenarioReport.kineticEnergy", "J", "MetricSummary",
                    "mean and max samples", "mean/max finite", "kinetic-energy metric is null or non-finite"),
                [RagdollLabScenarioSignalIds.CenterOfMassSpeed] = new(
                    RagdollLabScenarioSignalIds.CenterOfMassSpeed, "ScenarioReport.centerOfMassSpeed", "m/s", "MetricSummary",
                    "mean and max samples", "mean/max finite", "COM-speed metric is null or non-finite"),
                [RagdollLabScenarioSignalIds.SignedSupportMargin] = new(
                    RagdollLabScenarioSignalIds.SignedSupportMargin, "ScenarioReport.minimumSignedSupportMargin", "m", "float",
                    "signedSupportMarginAvailable", "value finite", "support-margin telemetry was not captured"),
                [RagdollLabScenarioSignalIds.CapturePoint] = new(
                    RagdollLabScenarioSignalIds.CapturePoint, "PhysicsFrame.balance.capturePoint", "world units", "Vector3 samples",
                    "at least one valid capture-point sample", "all captured samples finite", "balance frames did not expose a valid capture point"),
                [RagdollLabScenarioSignalIds.RecoveryTime] = new(
                    RagdollLabScenarioSignalIds.RecoveryTime, "ScenarioReport.recoveryTimeSeconds", "s", "float",
                    "analyzed report", "value finite and non-negative", "recovery-time value is non-finite or negative"),
                [RagdollLabScenarioSignalIds.FallenFrames] = new(
                    RagdollLabScenarioSignalIds.FallenFrames, "ScenarioReport.fallenFrameCount", "frames", "int",
                    "captured frame count", "integer range is inherently finite", "report has no captured frames"),
                [RagdollLabScenarioSignalIds.RecoveryCompletion] = new(
                    RagdollLabScenarioSignalIds.RecoveryCompletion, "ScenarioReport.recoveryCompletionAvailable/recoveryCompleted", "bool", "bool",
                    "producer explicitly reports completion", "boolean source", "recovery completion was not produced or is false"),
                [RagdollLabScenarioSignalIds.TrackingPoseError] = new(
                    RagdollLabScenarioSignalIds.TrackingPoseError, "ScenarioReport.pairTracking.targetPhysicsDistance/targetPhysicsAngularError", "m/deg", "MetricSummary pair samples",
                    "at least one complete animated pair", "metric mean/max finite", "no complete animated-pair pose error was captured"),
                [RagdollLabScenarioSignalIds.TrackingVelocityError] = new(
                    RagdollLabScenarioSignalIds.TrackingVelocityError, "ScenarioReport.pairTracking.targetPhysicsVelocityError", "m/s", "MetricSummary pair samples",
                    "at least one target+physics velocity pair", "metric mean/max finite", "no target/physics velocity error was captured"),
                [RagdollLabScenarioSignalIds.LocomotionTaskCompletion] = new(
                    RagdollLabScenarioSignalIds.LocomotionTaskCompletion, "ScenarioReport.taskCompletionAvailable/taskCompleted", "bool", "bool",
                    "scenario runner explicitly reports task completion", "boolean source", "locomotion task completion was not produced or is false"),
                [RagdollLabScenarioSignalIds.FootSlip] = new(
                    RagdollLabScenarioSignalIds.FootSlip, "ScenarioReport.footSlipSpeed", "m/s", "MetricSummary",
                    "mean and max samples", "mean/max finite", "foot-slip metric is null or non-finite"),
                [RagdollLabScenarioSignalIds.ContactPenetration] = new(
                    RagdollLabScenarioSignalIds.ContactPenetration, "ScenarioReport.penetration", "m", "MetricSummary",
                    "penetration samples", "mean and max finite", "penetration metric is null or non-finite"),
                [RagdollLabScenarioSignalIds.StaggerReplant] = new(
                    RagdollLabScenarioSignalIds.StaggerReplant, "ScenarioReport.staggerEpisodes[].replantFrame", "event", "episode event",
                    "at least one ground-backed replant", "frame index non-negative", "no stagger episode observed a replant"),
                [RagdollLabScenarioSignalIds.StaggerTerminalOutcome] = new(
                    RagdollLabScenarioSignalIds.StaggerTerminalOutcome, "ScenarioReport.staggerEpisodes[].terminalOutcome", "enum", "episode outcome",
                    "at least one terminal outcome", "enum is non-empty", "stagger episode outcome was not finalized"),
                [RagdollLabScenarioSignalIds.PropLifecycleCompletion] = new(
                    RagdollLabScenarioSignalIds.PropLifecycleCompletion, "ScenarioReport.propLifecycleCompletionAvailable/propLifecycleCompleted", "bool", "bool",
                    "prop runner explicitly reports lifecycle completion", "boolean source", "prop lifecycle completion was not produced or is false")
            };

        public static ScenarioSignalDescriptor Describe(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return null;
            descriptors.TryGetValue(signalId, out ScenarioSignalDescriptor descriptor);
            return descriptor;
        }

        public static List<string> MissingRequiredSignals(ScenarioProfile profile, ScenarioReport report, string role)
        {
            var missing = new List<string>();
            if (profile == null || !profile.available)
            {
                missing.Add("required_signal_missing:" + (role ?? "report") + ":profile_unavailable");
                return missing;
            }

            string prefix = "required_signal_missing:" + (role ?? "report") + ":";
            for (int i = 0; i < profile.requiredSignals.Length; i++)
            {
                string signalId = profile.requiredSignals[i];
                string reason = AvailabilityReason(signalId, report);
                if (reason != null) missing.Add(prefix + signalId + ":" + reason);
            }
            return missing;
        }

        public static bool IsAvailable(string signalId, ScenarioReport report)
        {
            return AvailabilityReason(signalId, report) == null;
        }

        static string AvailabilityReason(string signalId, ScenarioReport report)
        {
            if (report == null) return "report_missing";
            switch (signalId)
            {
                case RagdollLabScenarioSignalIds.KineticEnergy: return MetricReason(report.kineticEnergy, "metric_missing");
                case RagdollLabScenarioSignalIds.CenterOfMassSpeed: return MetricReason(report.centerOfMassSpeed, "metric_missing");
                case RagdollLabScenarioSignalIds.SignedSupportMargin:
                    return report.signedSupportMarginAvailable && RagdollLabMath.IsFinite(report.minimumSignedSupportMargin)
                        ? null : "support_margin_unavailable_or_non_finite";
                case RagdollLabScenarioSignalIds.CapturePoint:
                    return report.capturePointSampleCount > 0 && report.capturePointNonFiniteSampleCount == 0
                        ? null : "capture_point_unavailable_or_non_finite";
                case RagdollLabScenarioSignalIds.RecoveryTime:
                    return RagdollLabMath.IsFinite(report.recoveryTimeSeconds) && report.recoveryTimeSeconds >= 0f
                        ? null : "recovery_time_non_finite_or_negative";
                case RagdollLabScenarioSignalIds.FallenFrames:
                    return report.frameCount > 0 && report.fallenFrameCount >= 0 ? null : "captured_frames_missing";
                case RagdollLabScenarioSignalIds.RecoveryCompletion:
                    return report.recoveryCompletionAvailable && report.recoveryCompleted
                        ? null : "recovery_completion_missing_or_false";
                case RagdollLabScenarioSignalIds.TrackingPoseError:
                    return HasPairMetric(report, pose: true) ? null : "pose_tracking_unavailable";
                case RagdollLabScenarioSignalIds.TrackingVelocityError:
                    return HasPairVelocityError(report) ? null : "velocity_tracking_unavailable";
                case RagdollLabScenarioSignalIds.LocomotionTaskCompletion:
                    return report.taskCompletionAvailable && report.taskCompleted
                        ? null : "task_completion_missing_or_false";
                case RagdollLabScenarioSignalIds.FootSlip: return MetricReason(report.footSlipSpeed, "metric_missing");
                case RagdollLabScenarioSignalIds.ContactPenetration: return MetricReason(report.penetration, "metric_missing");
                case RagdollLabScenarioSignalIds.StaggerReplant:
                    return HasReplant(report) ? null : "replant_event_missing";
                case RagdollLabScenarioSignalIds.StaggerTerminalOutcome:
                    return HasTerminalOutcome(report) ? null : "terminal_outcome_missing";
                case RagdollLabScenarioSignalIds.PropLifecycleCompletion:
                    return report.propLifecycleCompletionAvailable && report.propLifecycleCompleted
                        ? null : "prop_lifecycle_completion_missing_or_false";
                default: return "signal_not_registered";
            }
        }

        static string MetricReason(MetricSummary metric, string nullReason)
        {
            return metric != null && RagdollLabMath.IsFinite(metric.mean) && RagdollLabMath.IsFinite(metric.max)
                ? null : nullReason;
        }

        static bool HasPairMetric(ScenarioReport report, bool pose)
        {
            if (report.pairTracking == null) return false;
            for (int i = 0; i < report.pairTracking.Length; i++)
            {
                PairTrackingReport pair = report.pairTracking[i];
                if (pair == null || pair.sampleCount <= 0 || !pair.targetAvailable || !pair.physicsAvailable) continue;
                MetricSummary metric = pose ? pair.targetPhysicsDistance : pair.targetPhysicsAngularError;
                if (metric != null && RagdollLabMath.IsFinite(metric.mean) && RagdollLabMath.IsFinite(metric.max)) return true;
            }
            return false;
        }

        static bool HasPairVelocityError(ScenarioReport report)
        {
            if (report.pairTracking == null) return false;
            for (int i = 0; i < report.pairTracking.Length; i++)
            {
                MetricSummary metric = report.pairTracking[i]?.targetPhysicsVelocityError;
                if (metric != null && RagdollLabMath.IsFinite(metric.mean) && RagdollLabMath.IsFinite(metric.max)) return true;
            }
            return false;
        }

        static bool HasReplant(ScenarioReport report)
        {
            if (report.staggerEpisodes == null) return false;
            for (int i = 0; i < report.staggerEpisodes.Length; i++)
                if (report.staggerEpisodes[i] != null && report.staggerEpisodes[i].replantFrame >= 0) return true;
            return false;
        }

        static bool HasTerminalOutcome(ScenarioReport report)
        {
            if (report.staggerEpisodes == null) return false;
            for (int i = 0; i < report.staggerEpisodes.Length; i++)
            {
                string outcome = report.staggerEpisodes[i]?.terminalOutcome;
                if (!string.IsNullOrEmpty(outcome) && !string.Equals(outcome, "Unavailable", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Defines which measurements are meaningful for a captured lab scenario.
    /// Unknown scenarios deliberately fail closed instead of inheriting Idle.
    /// </summary>
    public sealed class ScenarioProfile
    {
        private readonly Dictionary<string, string> expectations;

        public readonly string id;
        public readonly bool available;
        public readonly string[] requiredSignals;

        internal ScenarioProfile(string id, bool available, string[] requiredSignals, params string[] expectationPairs)
        {
            this.id = id;
            this.available = available;
            this.requiredSignals = requiredSignals ?? Array.Empty<string>();
            expectations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i + 1 < expectationPairs.Length; i += 2)
                expectations[expectationPairs[i]] = expectationPairs[i + 1];
        }

        public string ExpectationFor(string metricName)
        {
            if (string.IsNullOrEmpty(metricName))
                return "neutral";

            if (!expectations.TryGetValue(metricName, out string expectation))
            {
                string canonicalName = metricName switch
                {
                    "PenetrationDepth.max" => "Penetration.max",
                    "RecoveryTime" => "RecoveryTime.seconds",
                    "FallenFrameCount" => "FallenFrameCount.count",
                    "StaggerEpisodeCount" => "StaggerEpisodeCount.count",
                    _ => metricName
                };
                expectations.TryGetValue(canonicalName, out expectation);
            }

            return !string.IsNullOrEmpty(expectation)
                ? expectation
                : "neutral";
        }
    }

    public static class RagdollLabScenarioProfiles
    {
        public const string UnavailableId = "Unavailable";

        private static readonly ScenarioProfile unavailable = new(
            UnavailableId,
            false,
            Array.Empty<string>());

        private static readonly ScenarioProfile idle = new(
            "Idle",
            true,
            new[] { RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.CenterOfMassSpeed, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            "KineticEnergy.mean", "lower",
            "CenterOfMassSpeed.mean", "lower",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower");

        private static readonly ScenarioProfile push = new(
            "Push",
            true,
            new[] { RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.CapturePoint, RagdollLabScenarioSignalIds.RecoveryTime, RagdollLabScenarioSignalIds.FallenFrames, RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.CenterOfMassSpeed, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            "SignedSupportMargin.minimum", "higher",
            "RecoveryTime.seconds", "lower",
            "FallenFrameCount.count", "lower",
            "CenterOfMassSpeed.mean", "lower",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower");

        private static readonly ScenarioProfile getUp = new(
            "GetUp",
            true,
            new[] { RagdollLabScenarioSignalIds.RecoveryTime, RagdollLabScenarioSignalIds.FallenFrames, RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.RecoveryCompletion, RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.CenterOfMassSpeed, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            "RecoveryTime.seconds", "lower",
            "FallenFrameCount.count", "lower",
            "SignedSupportMargin.minimum", "higher",
            "CenterOfMassSpeed.mean", "neutral",
            "KineticEnergy.mean", "neutral");

        private static readonly ScenarioProfile locomotion = new(
            "Locomotion",
            true,
            new[] { RagdollLabScenarioSignalIds.TrackingPoseError, RagdollLabScenarioSignalIds.TrackingVelocityError, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration, RagdollLabScenarioSignalIds.LocomotionTaskCompletion },
            "CenterOfMassSpeed.mean", "neutral",
            "KineticEnergy.mean", "neutral",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower");

        private static readonly ScenarioProfile stagger = new(
            "Stagger",
            true,
            new[] { RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.CapturePoint, RagdollLabScenarioSignalIds.StaggerReplant, RagdollLabScenarioSignalIds.StaggerTerminalOutcome, RagdollLabScenarioSignalIds.RecoveryTime, RagdollLabScenarioSignalIds.FallenFrames, RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.CenterOfMassSpeed, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            "SignedSupportMargin.minimum", "higher",
            "RecoveryTime.seconds", "lower",
            "FallenFrameCount.count", "lower",
            "CenterOfMassSpeed.mean", "lower",
            "FootSlipSpeed.mean", "lower",
            "StaggerEpisodeCount.count", "neutral");

        private static readonly ScenarioProfile balancer = new(
            "Balancer",
            true,
            new[] { RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.CapturePoint, RagdollLabScenarioSignalIds.CenterOfMassSpeed, RagdollLabScenarioSignalIds.RecoveryTime, RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            "SignedSupportMargin.minimum", "higher",
            "CenterOfMassSpeed.mean", "lower",
            "RecoveryTime.seconds", "lower",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower",
            "StaggerEpisodeCount.count", "lower");

        public static ScenarioProfile Resolve(string scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario))
                return unavailable;

            switch (scenario.Trim().ToLowerInvariant())
            {
                case "idle":
                    return idle;
                case "push":
                case "recoverablepush":
                case "recoverable_push":
                    return push;
                case "getup":
                case "get_up":
                    return getUp;
                case "locomotion":
                case "walk":
                    return locomotion;
                case "stagger":
                case "staggerrecovery":
                case "stagger_recovery":
                    return stagger;
                case "balancer":
                case "balanceroff":
                case "balanceron":
                case "balancer_off":
                case "balancer_on":
                    return balancer;
                default:
                    return unavailable;
            }
        }
    }
}
