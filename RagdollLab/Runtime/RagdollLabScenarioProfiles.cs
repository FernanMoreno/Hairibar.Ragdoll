using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.RagdollLab
{
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
            new[] { "kinetic energy", "center-of-mass speed", "foot slip", "penetration" },
            "KineticEnergy.mean", "lower",
            "CenterOfMassSpeed.mean", "lower",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower");

        private static readonly ScenarioProfile push = new(
            "Push",
            true,
            new[] { "signed support margin", "recovery time", "fallen frames", "foot slip" },
            "SignedSupportMargin.minimum", "higher",
            "RecoveryTime.seconds", "lower",
            "FallenFrameCount.count", "lower",
            "CenterOfMassSpeed.mean", "lower",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower");

        private static readonly ScenarioProfile getUp = new(
            "GetUp",
            true,
            new[] { "recovery time", "fallen frames", "support margin", "completion" },
            "RecoveryTime.seconds", "lower",
            "FallenFrameCount.count", "lower",
            "SignedSupportMargin.minimum", "higher",
            "CenterOfMassSpeed.mean", "neutral",
            "KineticEnergy.mean", "neutral");

        private static readonly ScenarioProfile locomotion = new(
            "Locomotion",
            true,
            new[] { "tracking", "foot slip", "penetration", "task completion" },
            "CenterOfMassSpeed.mean", "neutral",
            "KineticEnergy.mean", "neutral",
            "FootSlipSpeed.mean", "lower",
            "Penetration.max", "lower");

        private static readonly ScenarioProfile stagger = new(
            "Stagger",
            true,
            new[] { "signed support margin", "replant", "recovery time", "fallen frames" },
            "SignedSupportMargin.minimum", "higher",
            "RecoveryTime.seconds", "lower",
            "FallenFrameCount.count", "lower",
            "CenterOfMassSpeed.mean", "lower",
            "FootSlipSpeed.mean", "lower",
            "StaggerEpisodeCount.count", "neutral");

        private static readonly ScenarioProfile balancer = new(
            "Balancer",
            true,
            new[] { "signed support margin", "center-of-mass speed", "settling time", "foot slip" },
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
