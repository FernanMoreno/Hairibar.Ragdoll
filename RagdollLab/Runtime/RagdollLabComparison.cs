using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    public static class RagdollLabComparison
    {
        public static ComparisonReport Build(EvaluationReport current, EvaluationReport baseline)
        {
            var result = new ComparisonReport { currentRunId = current?.metadata?.runId, baselineRunId = baseline?.metadata?.runId, baselineFound = baseline != null };
            if (current?.scenarioReport == null || baseline?.scenarioReport == null) return result;
            Add(result, "KineticEnergy.mean", "J", current.scenarioReport.kineticEnergy.mean, baseline.scenarioReport.kineticEnergy.mean, false);
            Add(result, "CenterOfMassSpeed.mean", "m/s", current.scenarioReport.centerOfMassSpeed.mean, baseline.scenarioReport.centerOfMassSpeed.mean, true);
            Add(result, "ContactImpulse.p95", "N*s", current.scenarioReport.contactImpulse.p95, baseline.scenarioReport.contactImpulse.p95, true);
            Add(result, "PenetrationDepth.max", "m", current.scenarioReport.penetration.max, baseline.scenarioReport.penetration.max, true);
            Add(result, "FootSlipSpeed.mean", "m/s", current.scenarioReport.footSlipSpeed.mean, baseline.scenarioReport.footSlipSpeed.mean, true);
            if (current.scenarioReport.penetration.max > 0f) result.regressionGuards.Add("penetration present");
            if (current.scenarioReport.footSlipSpeed.mean > 0.15f) result.regressionGuards.Add("foot slip above default warning");
            return result;
        }

        static void Add(ComparisonReport result, string name, string unit, float current, float baseline, bool lowerIsBetter)
        {
            float delta = current - baseline;
            float relative = Mathf.Abs(baseline) > 0.000001f ? delta / Mathf.Abs(baseline) : 0f;
            result.metrics.Add(new ComparisonMetric { name = name, unit = unit, current = current, baseline = baseline, delta = delta, relativeDelta = relative,
                regression = lowerIsBetter ? delta > 0f : delta < 0f });
        }

        public static EvaluationReport Read(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<EvaluationReport>(File.ReadAllText(path)); } catch { return null; }
        }

        public static void CopyBaseline(string source, string destination)
        {
            if (!File.Exists(source)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }
    }
}
