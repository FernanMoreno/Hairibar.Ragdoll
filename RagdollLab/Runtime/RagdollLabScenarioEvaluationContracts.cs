using System;
using System.Collections.Generic;

namespace Hairibar.Ragdoll.RagdollLab
{
    /// <summary>
    /// Immutable policy metadata for one autonomous scenario evaluation.
    /// Production mutation and telemetry production remain outside this catalog.
    /// </summary>
    public sealed class ScenarioEvaluationContract
    {
        public readonly string id;
        public readonly string version;
        public readonly string[] scenarioIds;
        public readonly string[] requiredSignals;
        public readonly string[] taskMetrics;
        public readonly string[] safetyGates;
        public readonly bool available;
        public readonly string unavailableReason;
        public readonly bool balanceFamily;

        internal ScenarioEvaluationContract(
            string id,
            string version,
            string[] scenarioIds,
            string[] requiredSignals,
            string[] taskMetrics,
            string[] safetyGates,
            bool available = true,
            string unavailableReason = null,
            bool balanceFamily = false)
        {
            this.id = id;
            this.version = version;
            this.scenarioIds = scenarioIds ?? Array.Empty<string>();
            this.requiredSignals = requiredSignals ?? Array.Empty<string>();
            this.taskMetrics = taskMetrics ?? Array.Empty<string>();
            this.safetyGates = safetyGates ?? Array.Empty<string>();
            this.available = available;
            this.unavailableReason = unavailableReason;
            this.balanceFamily = balanceFamily;
        }
    }

    public static class RagdollLabScenarioEvaluationCatalog
    {
        public const string Version = "1.0.0";

        static readonly ScenarioEvaluationContract physicalIntegrity = new(
            "PhysicalIntegrity", Version,
            new[] { "PhysicalIntegrity", "physical_integrity", "Physics" },
            new[] { RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            new[] { "KineticEnergy.mean" },
            new[] { "finite_data", "penetration", "foot_slip", "energy" });

        static readonly ScenarioEvaluationContract tracking = new(
            "Tracking", Version,
            new[] { "Tracking", "tracking", "AnimationTracking" },
            new[] { RagdollLabScenarioSignalIds.TrackingPoseError, RagdollLabScenarioSignalIds.TrackingVelocityError, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            new[] { "TrackingPoseError.mean", "TrackingVelocityError.mean" },
            new[] { "finite_data", "penetration", "foot_slip" });

        static readonly ScenarioEvaluationContract getUp = new(
            "GetUp", Version,
            new[] { "GetUp", "getup", "get_up" },
            new[] { RagdollLabScenarioSignalIds.RecoveryTime, RagdollLabScenarioSignalIds.FallenFrames, RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.RecoveryCompletion },
            new[] { "RecoveryTime.seconds", "FallenFrameCount.count" },
            new[] { "finite_data", "penetration", "foot_slip", "terminal_state" });

        static readonly ScenarioEvaluationContract balance = new(
            "Balance", Version,
            new[] { "Balance", "Idle", "Push", "RecoverablePush", "Balancer", "BalancerOn", "BalancerOff" },
            new[] { RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.CapturePoint, RagdollLabScenarioSignalIds.KineticEnergy, RagdollLabScenarioSignalIds.CenterOfMassSpeed, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            new[] { "SignedSupportMargin.minimum", "CenterOfMassSpeed.mean", "RecoveryTime.seconds" },
            new[] { "finite_data", "penetration", "foot_slip", "energy", "unpinned" },
            balanceFamily: true);

        static readonly ScenarioEvaluationContract stagger = new(
            "Stagger", Version,
            new[] { "Stagger", "stagger", "StaggerRecovery", "stagger_recovery" },
            new[] { RagdollLabScenarioSignalIds.SignedSupportMargin, RagdollLabScenarioSignalIds.CapturePoint, RagdollLabScenarioSignalIds.StaggerReplant, RagdollLabScenarioSignalIds.StaggerTerminalOutcome, RagdollLabScenarioSignalIds.RecoveryTime, RagdollLabScenarioSignalIds.FallenFrames, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration },
            new[] { "SignedSupportMargin.minimum", "RecoveryTime.seconds", "FallenFrameCount.count" },
            new[] { "finite_data", "penetration", "foot_slip", "unpinned", "terminal_state" },
            balanceFamily: true);

        static readonly ScenarioEvaluationContract props = new(
            "Props", Version,
            new[] { "Props", "props", "Prop" },
            new[] { RagdollLabScenarioSignalIds.PropLifecycleCompletion, RagdollLabScenarioSignalIds.ContactPenetration, RagdollLabScenarioSignalIds.FootSlip },
            new[] { "PropLifecycleCompletion" },
            new[] { "finite_data", "penetration", "foot_slip" });

        static readonly ScenarioEvaluationContract locomotion = new(
            "Locomotion", Version,
            new[] { "Locomotion", "locomotion", "Walk", "walk" },
            new[] { RagdollLabScenarioSignalIds.TrackingPoseError, RagdollLabScenarioSignalIds.TrackingVelocityError, RagdollLabScenarioSignalIds.FootSlip, RagdollLabScenarioSignalIds.ContactPenetration, RagdollLabScenarioSignalIds.LocomotionTaskCompletion },
            new[] { "TrackingPoseError.mean", "TrackingVelocityError.mean" },
            new[] { "finite_data", "penetration", "foot_slip" });

        static readonly Dictionary<string, ScenarioEvaluationContract> byAlias = BuildAliases();

        public static ScenarioEvaluationContract Resolve(string scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario)) return Unavailable();
            return byAlias.TryGetValue(scenario.Trim(), out ScenarioEvaluationContract contract)
                ? contract : Unavailable();
        }

        public static ScenarioEvaluationContract ResolveProfile(string scenarioProfile)
        {
            if (string.IsNullOrWhiteSpace(scenarioProfile)) return Unavailable();
            foreach (ScenarioEvaluationContract contract in new[] { physicalIntegrity, tracking, getUp, balance, stagger, props, locomotion })
                for (int i = 0; i < contract.scenarioIds.Length; i++)
                    if (string.Equals(contract.scenarioIds[i], scenarioProfile, StringComparison.OrdinalIgnoreCase)) return contract;
            return Unavailable();
        }

        static Dictionary<string, ScenarioEvaluationContract> BuildAliases()
        {
            var result = new Dictionary<string, ScenarioEvaluationContract>(StringComparer.OrdinalIgnoreCase);
            ScenarioEvaluationContract[] contracts = { physicalIntegrity, tracking, getUp, balance, stagger, props, locomotion };
            for (int i = 0; i < contracts.Length; i++)
                for (int j = 0; j < contracts[i].scenarioIds.Length; j++)
                    result[contracts[i].scenarioIds[j]] = contracts[i];
            return result;
        }

        static ScenarioEvaluationContract Unavailable()
        {
            return new ScenarioEvaluationContract(
                "Unavailable", Version, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                available: false, unavailableReason: "scenario_contract_unavailable");
        }
    }
}
